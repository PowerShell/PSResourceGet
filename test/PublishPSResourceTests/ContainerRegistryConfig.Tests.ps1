# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$PSScriptRoot\..\PSGetTestUtils.psm1" -Force

Describe 'Container registry ORAS transport' -Tags 'CI' {
    BeforeAll {
        Add-Type -AssemblyName System.Net.Http
        $references = @(
            [System.Net.Http.HttpClient].Assembly.Location
            [System.Net.HttpStatusCode].Assembly.Location
            [Security.Cryptography.SHA256].Assembly.Location
        )
        Add-Type -ReferencedAssemblies $references -TypeDefinition @'
using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class ConfigBlobUploadHandler : HttpMessageHandler
{
    public static object InvokeOnWorker(object api, string methodName, object[] arguments)
    {
        return Task.Run(async () =>
        {
            var task = (Task)api.GetType().GetMethod(methodName).Invoke(api, arguments);
            await task.ConfigureAwait(false);
            return task.GetType().GetProperty("Result").GetValue(task);
        }).GetAwaiter().GetResult();
    }

    public byte[] UploadedContent { get; private set; }
    public byte[] UploadedManifest { get; private set; }
    public byte[] UploadedPackage { get; private set; }
    public Uri UploadUri { get; private set; }
    public HttpStatusCode UploadStatusCode = HttpStatusCode.Created;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string path = request.RequestUri.AbsolutePath;
        if (request.Method == HttpMethod.Head)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        if (request.Method == HttpMethod.Get)
        {
            if (path.EndsWith("/tags/list"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"name\":\"test\",\"tags\":[\"1.0.0\"]}")
                };
            }

            byte[] bytes = path.Contains("/manifests/") ? UploadedManifest : UploadedPackage;
            if (bytes == null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                path.Contains("/manifests/") ? "application/vnd.oci.image.manifest.v1+json" : "application/octet-stream");
            response.Headers.Add("Docker-Content-Digest", "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            return response;
        }

        if (request.Method == HttpMethod.Post)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted);
            response.Headers.Location = new Uri("https://registry.example/v2/test/blobs/uploads/id?upload=1");
            return response;
        }

        var content = await request.Content.ReadAsByteArrayAsync();
        if (path.Contains("/manifests/"))
        {
            UploadedManifest = content;
        }
        else if (Encoding.UTF8.GetString(content) == "{}")
        {
            UploadedContent = content;
            UploadUri = request.RequestUri;
        }
        else
        {
            UploadedPackage = content;
        }

        return new HttpResponseMessage(UploadStatusCode);
    }
}
'@

        function New-ContainerRegistryApi {
            [CmdletBinding()]
            param($Handler)

            $type = [Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource].Assembly.GetType('Microsoft.PowerShell.PSResourceGet.ContainerRegistryServerAPICalls')
            $repository = [Microsoft.PowerShell.PSResourceGet.UtilClasses.PSRepositoryInfo]::new(
                'Test', [uri]'https://registry.example', 50, $true, $null, 'None', 'ContainerRegistry', $true)
            $api = [Activator]::CreateInstance($type, @($repository, $PSCmdlet, $null, 'test'))
            $flags = [Reflection.BindingFlags]'Instance,NonPublic'
            $clientField = $type.GetField('_orasClient', $flags)
            $oldClient = $clientField.GetValue($api)
            $client = [System.Net.Http.HttpClient]::new($Handler)
            $replacement = [Activator]::CreateInstance($oldClient.GetType(), @($client, $oldClient.CredentialProvider, $oldClient.Cache))
            $clientField.SetValue($api, $replacement)
            $type.GetField('_httpClient', $flags).GetValue($api).Dispose()
            $type.GetField('_httpClient', $flags).SetValue($api, $client)
            return $api
        }

        function Invoke-ContainerRegistryMethod {
            [CmdletBinding()]
            param($MethodName, [object[]] $MethodArguments)

            $api.GetType().GetField('_cmdletPassedIn', [Reflection.BindingFlags]'Instance,NonPublic').SetValue($api, $PSCmdlet)
            for ($i = 0; $i -lt $MethodArguments.Length; $i++) {
                if ($null -ne $MethodArguments[$i]) {
                    $MethodArguments[$i] = $MethodArguments[$i].PSObject.BaseObject
                }
            }

            if ($MethodName.EndsWith('Async')) {
                return [ConfigBlobUploadHandler]::InvokeOnWorker($api, $MethodName, $MethodArguments)
            }

            $api.GetType().GetMethod($MethodName, [Reflection.BindingFlags]'Instance,Public,NonPublic').Invoke($api, $MethodArguments)
        }
    }

    BeforeEach {
        $handler = [ConfigBlobUploadHandler]::new()
        $api = New-ContainerRegistryApi $handler
        $outputDirectory = Join-Path $TestDrive ([guid]::NewGuid().ToString())
        $null = New-Item $outputDirectory -ItemType Directory
        $packagePath = Join-Path $outputDirectory 'test.1.0.0.nupkg'
        [IO.File]::WriteAllText($packagePath, 'package')
        $configPath = Join-Path $outputDirectory 'config.json'
        [IO.File]::WriteAllText($configPath, '{"module":true}')
        $method = $api.GetType().GetMethod('PushNupkgContainerRegistry', [Reflection.BindingFlags]'Instance,NonPublic')
        $version = [Activator]::CreateInstance($method.GetParameters()[3].ParameterType, @('1.0.0'))
        $publishArguments = @(
            $outputDirectory, 'test', $null, $version,
            [Microsoft.PowerShell.PSResourceGet.UtilClasses.ResourceType]::Module,
            @{ ModuleVersion = '1.0.0' }, @{}, $false, $null, $null)
    }

    AfterEach {
        $api.GetType().GetField('_httpClient', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($api).Dispose()
    }

    It 'Publishes matching empty JSON config bytes and manifest without overwriting module content' {
        Invoke-ContainerRegistryMethod 'PushNupkgContainerRegistry' $publishArguments | Should -Be $true
        $publishArguments[9] | Should -BeNullOrEmpty
        $handler.UploadedContent.Length | Should -Be 2
        [Text.Encoding]::UTF8.GetString($handler.UploadedContent) | Should -BeExactly '{}'
        $expectedDigest = 'sha256:' + [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($handler.UploadedContent)).ToLowerInvariant()
        [uri]::UnescapeDataString($handler.UploadUri.Query) | Should -Match "digest=$expectedDigest"
        $manifest = [Text.Encoding]::UTF8.GetString($handler.UploadedManifest) | ConvertFrom-Json
        $manifest.config.mediaType | Should -BeExactly 'application/vnd.oci.image.config.v1+json'
        $manifest.config.digest | Should -BeExactly $expectedDigest
        $manifest.config.size | Should -Be 2
        $manifest.layers.Count | Should -Be 1
        $manifest.layers[0].size | Should -Be (Get-Item $packagePath).Length
        [Text.Encoding]::UTF8.GetString($handler.UploadedPackage) | Should -BeExactly 'package'
        [IO.File]::ReadAllText($configPath) | Should -BeExactly '{"module":true}'
    }

    It 'Reports a failed ORAS upload' {
        $handler.UploadStatusCode = [System.Net.HttpStatusCode]::BadRequest
        Invoke-ContainerRegistryMethod 'PushNupkgContainerRegistry' $publishArguments | Should -Be $false
        $publishArguments[9].FullyQualifiedErrorId | Should -BeExactly 'PackagePublishOrasError'
    }

    It 'Preserves the publish authentication information record for Azure authentication' {
        Invoke-ContainerRegistryMethod 'PushNupkgContainerRegistry' $publishArguments -InformationVariable registryInformation | Should -Be $true

        $registryInformation | Should -HaveCount 1
        $registryInformation[0].Tags | Should -Be 'PSRGContainerRegistryUnauthenticatedCheck'
        $registryInformation[0].MessageData | Should -BeExactly 'Value of isRepositoryUnauthenticated: False'
    }

    It 'Finds a package through each concurrent find path' -TestCases @(
        @{ MethodName = 'FindNameAsync' }
        @{ MethodName = 'FindVersionAsync' }
        @{ MethodName = 'FindVersionGlobbingAsync' }
    ) {
        param($MethodName)
        Invoke-ContainerRegistryMethod 'PushNupkgContainerRegistry' $publishArguments | Should -Be $true
        $errors = [Collections.Concurrent.ConcurrentQueue[Management.Automation.ErrorRecord]]::new()
        $warnings = [Collections.Concurrent.ConcurrentQueue[string]]::new()
        $debug = [Collections.Concurrent.ConcurrentQueue[string]]::new()
        $verbose = [Collections.Concurrent.ConcurrentQueue[string]]::new()
        $resourceType = [Microsoft.PowerShell.PSResourceGet.UtilClasses.ResourceType]::Module
        $arguments = switch ($MethodName) {
            'FindNameAsync' { @('test', $false, $resourceType) }
            'FindVersionAsync' { @('test', '1.0.0', $resourceType) }
            'FindVersionGlobbingAsync' {
                $rangeType = $api.GetType().GetMethod($MethodName).GetParameters()[1].ParameterType
                $range = $rangeType.GetField('All', [Reflection.BindingFlags]'Static,Public').GetValue($null)
                @('test', $range, $false, $resourceType, $false)
            }
        }
        $arguments += @($errors, $warnings, $debug, $verbose)
        $result = Invoke-ContainerRegistryMethod $MethodName $arguments
        $errors.Count | Should -Be 0
        $result.GetType().GetProperty('HashtableResponse').GetValue($result).Count | Should -Be 1
        $debug.Count | Should -BeGreaterThan 0
    }

    It 'Downloads the ORAS package through the concurrent install path' {
        Invoke-ContainerRegistryMethod 'PushNupkgContainerRegistry' $publishArguments | Should -Be $true
        $errors = [Collections.Concurrent.ConcurrentQueue[Management.Automation.ErrorRecord]]::new()
        $warnings = [Collections.Concurrent.ConcurrentQueue[string]]::new()
        $debug = [Collections.Concurrent.ConcurrentQueue[string]]::new()
        $verbose = [Collections.Concurrent.ConcurrentQueue[string]]::new()
        $arguments = @('test', '1.0.0', $false, $errors, $warnings, $debug, $verbose)
        $stream = Invoke-ContainerRegistryMethod 'InstallPackageAsync' $arguments
        try {
            $errors.Count | Should -Be 0
            [Text.Encoding]::UTF8.GetString($stream.ToArray()) | Should -BeExactly 'package'
            $verbose.Count | Should -BeGreaterThan 0
        }
        finally {
            $stream.Dispose()
        }
    }
}
