# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$PSScriptRoot\..\PSGetTestUtils.psm1" -Force

Describe 'Container registry config blob' -Tags 'CI' {
    BeforeAll {
        Add-Type -AssemblyName System.Net.Http
        $references = @(
            [System.Net.Http.HttpClient].Assembly.Location
            [System.Net.HttpStatusCode].Assembly.Location
        )
        Add-Type -ReferencedAssemblies $references -TypeDefinition @'
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public class ConfigBlobUploadHandler : HttpMessageHandler
{
    public byte[] UploadedContent { get; private set; }
    public byte[] UploadedManifest { get; private set; }
    public Uri UploadUri { get; private set; }
    public HttpStatusCode UploadStatusCode = HttpStatusCode.Created;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted);
            response.Headers.Location = new Uri("/v2/test/blobs/uploads/id?upload=1", UriKind.Relative);
            return response;
        }

        var content = await request.Content.ReadAsByteArrayAsync();
        if (request.RequestUri.AbsolutePath.Contains("/manifests/"))
        {
            UploadedManifest = content;
        }
        else
        {
            UploadedContent = content;
            UploadUri = request.RequestUri;
        }

        return new HttpResponseMessage(UploadStatusCode);
    }
}
'@

        function Invoke-ContainerRegistryMethod {
            [CmdletBinding()]
            param($MethodName, [object[]] $MethodArguments, $Handler)

            $type = [Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource].Assembly.GetType('Microsoft.PowerShell.PSResourceGet.ContainerRegistryServerAPICalls')
            $repository = [Microsoft.PowerShell.PSResourceGet.UtilClasses.PSRepositoryInfo]::new(
                'Test', [uri]'https://registry.example', 50, $true, $null, 'None', 'ContainerRegistry', $true)
            $api = [Activator]::CreateInstance($type, @($repository, $PSCmdlet, $null, 'test'))
            $flags = [Reflection.BindingFlags]'Instance,NonPublic'
            $clientProperty = $type.GetProperty('_sessionClient', $flags)
            try {
                if ($Handler) {
                    $clientProperty.GetValue($api).Dispose()
                    $clientProperty.SetValue($api, [System.Net.Http.HttpClient]::new($Handler))
                    $type.GetField('_cachedContainterRegistryToken', $flags).SetValue($api, 'test-token')
                }

                for ($i = 0; $i -lt $MethodArguments.Length; $i++) {
                    if ($null -ne $MethodArguments[$i]) {
                        $MethodArguments[$i] = $MethodArguments[$i].PSObject.BaseObject
                    }
                }

                $type.GetMethod($MethodName, $flags).Invoke($api, $MethodArguments)
            }
            finally {
                $clientProperty.GetValue($api).Dispose()
            }
        }
    }

    BeforeEach {
        $configPath = Join-Path $TestDrive "$([guid]::NewGuid()).json"
        $configArguments = @($configPath, $null)
        $configDigest = Invoke-ContainerRegistryMethod 'CreateConfigFile' $configArguments
    }

    It 'Creates a config containing exactly an empty JSON object and hashes those bytes' {
        $configArguments[1] | Should -BeNullOrEmpty
        $content = [IO.File]::ReadAllBytes($configPath)
        $content.Length | Should -Be 2
        [Text.Encoding]::UTF8.GetString($content) | Should -BeExactly '{}'
        $configDigest | Should -BeExactly (Get-FileHash $configPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }

    It 'Describes the config bytes in the manifest without changing the package layer' {
        $manifestArguments = @(
            'packageDigest', $configDigest, [long]123, 'test.nupkg', 'test',
            [Microsoft.PowerShell.PSResourceGet.UtilClasses.ResourceType]::Module, '{}')
        $manifest = Invoke-ContainerRegistryMethod 'CreateManifestContent' $manifestArguments | ConvertFrom-Json

        $manifest.config.mediaType | Should -BeExactly 'application/vnd.oci.image.config.v1+json'
        $manifest.config.digest | Should -BeExactly "sha256:$configDigest"
        $manifest.config.size | Should -Be (Get-Item $configPath).Length
        $manifest.layers.Count | Should -Be 1
        $manifest.layers[0].digest | Should -BeExactly 'sha256:packageDigest'
        $manifest.layers[0].size | Should -Be 123
    }

    It 'Uploads the config JSON with its matching digest' {
        $handler = [ConfigBlobUploadHandler]::new()
        $uploadArguments = @($configPath, $configDigest, 'test', 'test-token', $null)
        Invoke-ContainerRegistryMethod 'UploadConfigFile' $uploadArguments $handler

        $uploadArguments[4] | Should -BeNullOrEmpty
        $handler.UploadedContent.Length | Should -Be 2
        [Text.Encoding]::UTF8.GetString($handler.UploadedContent) | Should -BeExactly '{}'
        $handler.UploadUri.Query | Should -Match "digest=sha256:$configDigest"
    }

    It 'Reports a failed config upload' {
        $handler = [ConfigBlobUploadHandler]::new()
        $handler.UploadStatusCode = [System.Net.HttpStatusCode]::BadRequest
        $uploadArguments = @($configPath, $configDigest, 'test', 'test-token', $null)
        Invoke-ContainerRegistryMethod 'UploadConfigFile' $uploadArguments $handler

        $uploadArguments[4].FullyQualifiedErrorId | Should -BeExactly 'UploadConfigFileError'
    }

    It 'Publishes matching config bytes and manifest when the module already contains config.json' {
        $outputDirectory = Join-Path $TestDrive 'nupkg'
        $null = New-Item $outputDirectory -ItemType Directory
        $existingConfigPath = Join-Path $outputDirectory 'config.json'
        $existingConfigContent = '{"module":true}'
        [IO.File]::WriteAllText($existingConfigPath, $existingConfigContent)
        [IO.File]::WriteAllText((Join-Path $outputDirectory 'test.1.0.0.nupkg'), 'package')

        $handler = [ConfigBlobUploadHandler]::new()
        $type = [Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource].Assembly.GetType('Microsoft.PowerShell.PSResourceGet.ContainerRegistryServerAPICalls')
        $versionType = $type.GetMethod('PushNupkgContainerRegistry', [Reflection.BindingFlags]'Instance,NonPublic').GetParameters()[3].ParameterType
        $version = [Activator]::CreateInstance($versionType, @('1.0.0'))
        $publishArguments = @(
            $outputDirectory, 'test', $null, $version,
            [Microsoft.PowerShell.PSResourceGet.UtilClasses.ResourceType]::Module,
            @{ ModuleVersion = '1.0.0' }, @{}, $false, $null, $null)
        Push-Location $TestDrive
        try {
            $published = Invoke-ContainerRegistryMethod 'PushNupkgContainerRegistry' $publishArguments $handler
        }
        finally {
            Pop-Location
        }

        $published | Should -Be $true
        $publishArguments[9] | Should -BeNullOrEmpty
        [Text.Encoding]::UTF8.GetString($handler.UploadedContent) | Should -BeExactly '{}'
        $handler.UploadUri.Query | Should -Match "digest=sha256:$configDigest"
        $manifest = [Text.Encoding]::UTF8.GetString($handler.UploadedManifest) | ConvertFrom-Json
        $manifest.config.digest | Should -BeExactly "sha256:$configDigest"
        $manifest.config.size | Should -Be $handler.UploadedContent.Length
        [IO.File]::ReadAllText($existingConfigPath) | Should -BeExactly $existingConfigContent
    }
}
