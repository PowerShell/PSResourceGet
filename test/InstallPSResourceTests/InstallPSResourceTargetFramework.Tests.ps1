# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
Param()

$ProgressPreference = 'SilentlyContinue'
$modPath = "$PSScriptRoot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force

Describe 'Install-PSResource target framework filtering' -Tags 'CI' {
    BeforeAll {
        Add-Type -AssemblyName System.IO.Compression.FileSystem

        $outModulePath = Join-Path $PSScriptRoot '../../out/Microsoft.PowerShell.PSResourceGet'
        Import-Module $outModulePath -Force

        function New-TfmTestNupkg {
            param(
                [Parameter(Mandatory)]
                [string] $Name,

                [Parameter(Mandatory)]
                [string] $Version,

                [Parameter(Mandatory)]
                [string] $OutputDirectory,

                [Parameter(Mandatory)]
                [hashtable] $LibAssets
            )

            $packageRoot = Join-Path ([System.IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString())
            $null = New-Item -Path $packageRoot -ItemType Directory -Force

            try {
                foreach ($targetFramework in $LibAssets.Keys) {
                    foreach ($relativeAssetPath in $LibAssets[$targetFramework].Keys) {
                        $assetPath = Join-Path $packageRoot "lib/$targetFramework/$relativeAssetPath"
                        $null = New-Item -Path (Split-Path $assetPath -Parent) -ItemType Directory -Force
                        Set-Content -Path $assetPath -Value $LibAssets[$targetFramework][$relativeAssetPath]
                    }
                }

                $moduleManifest = @"
@{
    ModuleVersion = '$Version'
    GUID = '$([Guid]::NewGuid())'
    Author = 'TestAuthor'
    Description = 'Target framework filtering test module'
    FunctionsToExport = @()
    CmdletsToExport = @()
}
"@
                Set-Content -Path (Join-Path $packageRoot "$Name.psd1") -Value $moduleManifest
                Set-Content -Path (Join-Path $packageRoot 'DoNotOverwrite.txt') -Value 'package-original'

                $nuspec = @"
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>$Name</id>
    <version>$Version</version>
    <authors>TestAuthor</authors>
    <description>Target framework filtering test package</description>
    <tags>PSModule test</tags>
  </metadata>
</package>
"@
                Set-Content -Path (Join-Path $packageRoot "$Name.nuspec") -Value $nuspec

                $nupkgPath = Join-Path $OutputDirectory "$Name.$Version.nupkg"
                if (Test-Path $nupkgPath) {
                    Remove-Item $nupkgPath -Force
                }

                [System.IO.Compression.ZipFile]::CreateFromDirectory($packageRoot, $nupkgPath)
                return $nupkgPath
            }
            finally {
                Remove-Item $packageRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }

        function Get-VersionInstallPath {
            param(
                [Parameter(Mandatory)]
                [object] $Package
            )

            $namePath = Join-Path $Package.InstalledLocation $Package.Name
            $versionPath = Join-Path $namePath $Package.Version.ToString()
            if (Test-Path $versionPath) {
                return $versionPath
            }

            return $Package.InstalledLocation
        }

        function Get-InstalledAsset {
            param(
                [Parameter(Mandatory)]
                [string] $InstallPath,

                [Parameter(Mandatory)]
                [string] $AssetName
            )

            return @(Get-ChildItem -Path (Join-Path $InstallPath 'lib') -Recurse -File -Filter $AssetName -ErrorAction SilentlyContinue)
        }

        $script:repositoryPath = Join-Path $TestDrive 'TfmAssetRepository'
        $null = New-Item -Path $script:repositoryPath -ItemType Directory -Force
        $script:repositoryName = "TfmAssetRepository$PID"
        Register-PSResourceRepository -Name $script:repositoryName -Uri $script:repositoryPath -Trusted -Force

        $script:packageVersion = '1.0.0'
        $script:packageNames = @(
            'TfmAutomaticSelection',
            'TfmExplicitSelection',
            'TfmAzAccountsShape',
            'TfmMergeSelection',
            'TfmNoCompatibleAssets'
        )

        $null = New-TfmTestNupkg `
            -Name 'TfmAutomaticSelection' `
            -Version $script:packageVersion `
            -OutputDirectory $script:repositoryPath `
            -LibAssets @{
                net472 = @{
                    'Shared.dll' = 'net472-shared'
                    'FrameworkOnly.dll' = 'net472-only'
                }
                'netstandard2.0' = @{
                    'Shared.dll' = 'netstandard-shared'
                    'PortableOnly.dll' = 'netstandard-only'
                }
                'net8.0' = @{
                    'Shared.dll' = 'net8-shared'
                    'CoreOnly.dll' = 'net8-only'
                }
            }

        $null = New-TfmTestNupkg `
            -Name 'TfmExplicitSelection' `
            -Version $script:packageVersion `
            -OutputDirectory $script:repositoryPath `
            -LibAssets @{
                'netstandard2.0' = @{
                    'Shared.dll' = 'netstandard-shared'
                    'PortableOnly.dll' = 'netstandard-only'
                }
                'net6.0' = @{
                    'Shared.dll' = 'net6-shared'
                    'Net6Only.dll' = 'net6-only'
                }
                'net8.0' = @{
                    'Shared.dll' = 'net8-shared'
                    'Net8Only.dll' = 'net8-only'
                }
            }

        $null = New-TfmTestNupkg `
            -Name 'TfmAzAccountsShape' `
            -Version $script:packageVersion `
            -OutputDirectory $script:repositoryPath `
            -LibAssets @{
                net45 = @{
                    'Common.dll' = 'net45-common'
                    'Microsoft.Azure.PowerShell.Authentication.dll' = 'net45-unique'
                }
                net47 = @{
                    'Common.dll' = 'net47-common'
                    'Microsoft.Identity.Client.dll' = 'net47-unique'
                }
                'netstandard2.0' = @{
                    'Azure.Core.dll' = 'netstandard-unique'
                }
            }

        $null = New-TfmTestNupkg `
            -Name 'TfmMergeSelection' `
            -Version $script:packageVersion `
            -OutputDirectory $script:repositoryPath `
            -LibAssets @{
                net472 = @{
                    'Shared.dll' = 'net472-shared'
                }
                'net8.0' = @{
                    'Shared.dll' = 'net8-shared'
                }
            }

        $null = New-TfmTestNupkg `
            -Name 'TfmNoCompatibleAssets' `
            -Version $script:packageVersion `
            -OutputDirectory $script:repositoryPath `
            -LibAssets @{
                'net8.0' = @{
                    'Net8Only.dll' = 'net8-only'
                }
            }
    }

    AfterEach {
        foreach ($packageName in $script:packageNames) {
            Uninstall-PSResource `
                -Name $packageName `
                -Version '*' `
                -SkipDependencyCheck `
                -WarningAction SilentlyContinue `
                -ErrorAction SilentlyContinue
        }
    }

    AfterAll {
        Unregister-PSResourceRepository -Name $script:repositoryName -ErrorAction SilentlyContinue
    }

    It 'exposes only the TargetFramework platform-filtering parameter' {
        $command = Get-Command Install-PSResource

        $command.Parameters.ContainsKey('TargetFramework') | Should -Be $true
        $command.Parameters.ContainsKey('RuntimeIdentifier') | Should -Be $false
    }

    It 'completes known target framework values' {
        $commandLine = 'Install-PSResource -TargetFramework netst'
        $completion = TabExpansion2 $commandLine $commandLine.Length

        $completion.CompletionMatches.CompletionText | Should -Contain 'netstandard2.0'
    }

    It 'automatically selects the nearest duplicate asset while retaining unique compatible assets' {
        Install-PSResource `
            -Name 'TfmAutomaticSelection' `
            -Version $script:packageVersion `
            -Repository $script:repositoryName `
            -TrustRepository

        $package = Get-InstalledPSResource -Name 'TfmAutomaticSelection'
        $installPath = Get-VersionInstallPath $package
        $sharedAssets = Get-InstalledAsset -InstallPath $installPath -AssetName 'Shared.dll'

        $sharedAssets.Count | Should -Be 1
        if ($PSVersionTable.PSVersion.Major -eq 5) {
            $sharedAssets[0].Directory.Name | Should -Be 'net472'
            (Get-InstalledAsset -InstallPath $installPath -AssetName 'FrameworkOnly.dll').Count | Should -Be 1
            (Get-InstalledAsset -InstallPath $installPath -AssetName 'CoreOnly.dll').Count | Should -Be 0
        }
        else {
            $sharedAssets[0].Directory.Name | Should -Be 'net8.0'
            (Get-InstalledAsset -InstallPath $installPath -AssetName 'FrameworkOnly.dll').Count | Should -Be 0
            (Get-InstalledAsset -InstallPath $installPath -AssetName 'CoreOnly.dll').Count | Should -Be 1
        }

        (Get-InstalledAsset -InstallPath $installPath -AssetName 'PortableOnly.dll').Count | Should -Be 1
    }

    It 'uses the explicit target framework for nearest-compatible asset selection' {
        Install-PSResource `
            -Name 'TfmExplicitSelection' `
            -Version $script:packageVersion `
            -Repository $script:repositoryName `
            -TrustRepository `
            -TargetFramework 'net6.0'

        $package = Get-InstalledPSResource -Name 'TfmExplicitSelection'
        $installPath = Get-VersionInstallPath $package
        $sharedAssets = Get-InstalledAsset -InstallPath $installPath -AssetName 'Shared.dll'

        $sharedAssets.Count | Should -Be 1
        $sharedAssets[0].Directory.Name | Should -Be 'net6.0'
        (Get-InstalledAsset -InstallPath $installPath -AssetName 'PortableOnly.dll').Count | Should -Be 1
        (Get-InstalledAsset -InstallPath $installPath -AssetName 'Net6Only.dll').Count | Should -Be 1
        (Get-InstalledAsset -InstallPath $installPath -AssetName 'Net8Only.dll').Count | Should -Be 0
    }

    It 'retains Az.Accounts-shaped unique compatible assets while filtering duplicate variants' {
        Install-PSResource `
            -Name 'TfmAzAccountsShape' `
            -Version $script:packageVersion `
            -Repository $script:repositoryName `
            -TrustRepository `
            -TargetFramework 'net472'

        $package = Get-InstalledPSResource -Name 'TfmAzAccountsShape'
        $installPath = Get-VersionInstallPath $package
        $commonAssets = Get-InstalledAsset -InstallPath $installPath -AssetName 'Common.dll'

        $commonAssets.Count | Should -Be 1
        $commonAssets[0].Directory.Name | Should -Be 'net47'
        (Get-InstalledAsset -InstallPath $installPath -AssetName 'Microsoft.Azure.PowerShell.Authentication.dll').Count | Should -Be 1
        (Get-InstalledAsset -InstallPath $installPath -AssetName 'Microsoft.Identity.Client.dll').Count | Should -Be 1
        (Get-InstalledAsset -InstallPath $installPath -AssetName 'Azure.Core.dll').Count | Should -Be 1
    }

    It 'merges an explicit target into an installed version without overwriting existing files' {
        Install-PSResource `
            -Name 'TfmMergeSelection' `
            -Version $script:packageVersion `
            -Repository $script:repositoryName `
            -TrustRepository `
            -TargetFramework 'net8.0'

        $package = Get-InstalledPSResource -Name 'TfmMergeSelection'
        $installPath = Get-VersionInstallPath $package
        Set-Content -Path (Join-Path $installPath 'DoNotOverwrite.txt') -Value 'locally-modified'

        Install-PSResource `
            -Name 'TfmMergeSelection' `
            -Version $script:packageVersion `
            -Repository $script:repositoryName `
            -TrustRepository `
            -TargetFramework 'net472'

        Test-Path (Join-Path $installPath 'lib/net8.0/Shared.dll') | Should -Be $true
        Test-Path (Join-Path $installPath 'lib/net472/Shared.dll') | Should -Be $true
        (Get-Content -Path (Join-Path $installPath 'DoNotOverwrite.txt') -Raw).Trim() | Should -Be 'locally-modified'
    }

    It 'warns when an explicit target has no compatible lib assets' {
        $warnings = $null
        Install-PSResource `
            -Name 'TfmNoCompatibleAssets' `
            -Version $script:packageVersion `
            -Repository $script:repositoryName `
            -TrustRepository `
            -TargetFramework 'net472' `
            -WarningVariable warnings

        ($warnings | Out-String) | Should -BeLike '*No compatible lib assets*net472*'
        $package = Get-InstalledPSResource -Name 'TfmNoCompatibleAssets'
        $installPath = Get-VersionInstallPath $package
        (Get-InstalledAsset -InstallPath $installPath -AssetName 'Net8Only.dll').Count | Should -Be 0
    }

    It 'rejects an invalid explicit target framework' {
        $caughtError = $null
        try {
            Install-PSResource `
                -Name 'TfmExplicitSelection' `
                -Version $script:packageVersion `
                -Repository $script:repositoryName `
                -TrustRepository `
                -TargetFramework 'not-a-target-framework' `
                -ErrorAction Stop
        }
        catch {
            $caughtError = $_
        }

        $caughtError | Should -Not -BeNullOrEmpty
        $caughtError.FullyQualifiedErrorId | Should -BeLike 'InvalidTargetFramework*'
    }
}
