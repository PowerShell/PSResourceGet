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

        UploadedContent = await request.Content.ReadAsByteArrayAsync();
        UploadUri = request.RequestUri;
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
}
