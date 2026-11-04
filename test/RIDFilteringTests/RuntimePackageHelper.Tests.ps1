# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
Param()

$ProgressPreference = 'SilentlyContinue'
$modPath = "$PSScriptRoot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

Describe 'RuntimePackageHelper' -Tags 'CI' {
    BeforeAll {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $InternalHooks = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]
    }

    Context 'Root-level RID detection' {
        It 'recognizes supported root-level RID folders' {
            $InternalHooks::IsRidFolder('win-x64') | Should -BeTrue
            $InternalHooks::IsRidFolder('linux-musl-arm64') | Should -BeTrue
            $InternalHooks::IsRidFolder('osx.12-arm64') | Should -BeTrue
            $InternalHooks::IsRidFolder('win') | Should -BeTrue
            $InternalHooks::IsRidFolder('any') | Should -BeTrue
        }

        It 'rejects non-RID folder names' {
            $InternalHooks::IsRidFolder('lib') | Should -BeFalse
            $InternalHooks::IsRidFolder('runtimes') | Should -BeFalse
            $InternalHooks::IsRidFolder('content-x64') | Should -BeFalse
            $InternalHooks::IsRidFolder('windows-x64') | Should -BeFalse
        }

        It 'recognizes only root-level RID entries' {
            $InternalHooks::IsRuntimeAssetEntry('win-x64/native/module.dll') | Should -BeTrue
            $InternalHooks::IsRuntimeAssetEntry('linux-arm64/libmodule.so') | Should -BeTrue
            $InternalHooks::IsRuntimeAssetEntry('runtimes/win-x64/native/module.dll') | Should -BeFalse
            $InternalHooks::IsRuntimeAssetEntry('lib/net8.0/module.dll') | Should -BeFalse
        }

        It 'extracts a RID only from a root-level entry' {
            $InternalHooks::GetRidFromRuntimeAssetEntry('win-x64/native/module.dll') |
                Should -Be 'win-x64'
            $InternalHooks::GetRidFromRuntimeAssetEntry('runtimes/win-x64/native/module.dll') |
                Should -BeNullOrEmpty
        }
    }

    Context 'Entry filtering' {
        It 'includes non-runtime content and standard NuGet runtime layouts' {
            $InternalHooks::ShouldIncludeRuntimeAssetEntry('module.psd1') | Should -BeTrue
            $InternalHooks::ShouldIncludeRuntimeAssetEntry('lib/net8.0/module.dll') | Should -BeTrue
            $InternalHooks::ShouldIncludeRuntimeAssetEntry('runtimes/linux-x64/native/module.so') |
                Should -BeTrue
        }

        It 'includes the root-level folder for the current RID' {
            $currentRid = $InternalHooks::GetCurrentRuntimeIdentifier()

            $InternalHooks::ShouldIncludeRuntimeAssetEntry("$currentRid/native/module.dll") |
                Should -BeTrue
        }

        It 'honors an explicit cross-platform target RID' {
            $InternalHooks::ShouldIncludeRuntimeAssetEntryForRid(
                'linux-x64/native/module.so',
                'linux-x64') | Should -BeTrue
            $InternalHooks::ShouldIncludeRuntimeAssetEntryForRid(
                'win-x64/native/module.dll',
                'linux-x64') | Should -BeFalse
        }

        It 'honors generic fallback RID folders' {
            $InternalHooks::ShouldIncludeRuntimeAssetEntryForRid(
                'linux/native/module.so',
                'linux-x64') | Should -BeTrue
            $InternalHooks::ShouldIncludeRuntimeAssetEntryForRid(
                'any/native/module.bin',
                'linux-x64') | Should -BeTrue
            $InternalHooks::ShouldIncludeRuntimeAssetEntryForRid(
                'win/native/module.dll',
                'linux-x64') | Should -BeFalse
        }
    }

    Context 'Available RID discovery' {
        BeforeAll {
            $zipSource = Join-Path $TestDrive 'rid-source'
            $null = New-Item $zipSource -ItemType Directory -Force

            foreach ($rid in @('win-x64', 'linux-x64', 'osx-arm64')) {
                $ridPath = Join-Path $zipSource $rid
                $null = New-Item $ridPath -ItemType Directory -Force
                Set-Content -Path (Join-Path $ridPath 'asset.txt') -Value $rid
            }

            $standardRuntimePath = Join-Path (Join-Path $zipSource 'runtimes') 'linux-arm64'
            $null = New-Item $standardRuntimePath -ItemType Directory -Force
            Set-Content -Path (Join-Path $standardRuntimePath 'asset.txt') -Value 'standard-layout'

            $script:zipPath = Join-Path $TestDrive 'runtime-assets.zip'
            [System.IO.Compression.ZipFile]::CreateFromDirectory($zipSource, $zipPath)
        }

        It 'lists only root-level RIDs' {
            $rids = $InternalHooks::GetAvailableRidsFromZipFile($zipPath)

            $rids.Count | Should -Be 3
            $rids | Should -Contain 'win-x64'
            $rids | Should -Contain 'linux-x64'
            $rids | Should -Contain 'osx-arm64'
            $rids | Should -Not -Contain 'linux-arm64'
        }

        It 'throws when the zip file does not exist' {
            $missingPath = Join-Path $TestDrive 'missing.zip'
            { $InternalHooks::GetAvailableRidsFromZipFile($missingPath) } | Should -Throw
        }
    }
}
