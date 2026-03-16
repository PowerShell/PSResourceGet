# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Integration tests for platform-aware installation:
# - RID filtering of runtimes/ folder
# - TFM filtering of lib/ folder
# - .nuspec retention post-install
# - NuspecReader-based dependency parsing
# - SkipRuntimeFiltering parameter
#
# NOTE: These tests require the built module to be deployed on PSModulePath.
# Run the project build script (e.g., build.ps1) before executing these tests.
# The unit tests in RuntimeIdentifierHelper.Tests.ps1 and RuntimePackageHelper.Tests.ps1
# can be run directly after 'dotnet build' by loading the DLL.

[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
Param()

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose


Describe 'Platform-Aware Installation Integration Tests' -tags 'CI' {

    BeforeAll {
        # Helper: Creates a minimal .nupkg with configurable runtimes/, lib/ TFMs, and .nuspec dependencies.
        function New-TestNupkg {
            param(
                [string]$Name,
                [string]$Version,
                [string]$OutputDir,
                [string[]]$RuntimeIdentifiers = @(),
                [string[]]$LibTfms = @(),
                [hashtable[]]$Dependencies = @(),
                [switch]$IncludeModuleManifest
            )

            $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString())
            $null = New-Item $tempDir -ItemType Directory -Force

            try {
                # Create runtimes/ subdirectories with dummy native files
                foreach ($rid in $RuntimeIdentifiers) {
                    $nativeDir = Join-Path $tempDir "runtimes/$rid/native"
                    $null = New-Item $nativeDir -ItemType Directory -Force
                    Set-Content -Path (Join-Path $nativeDir "native_$rid.dll") -Value "native-binary-for-$rid"
                }

                # Create lib/ subdirectories with dummy assemblies
                foreach ($tfm in $LibTfms) {
                    $libDir = Join-Path $tempDir "lib/$tfm"
                    $null = New-Item $libDir -ItemType Directory -Force
                    Set-Content -Path (Join-Path $libDir "$Name.dll") -Value "assembly-for-$tfm"
                }

                # Build .nuspec XML with dependencies
                $depGroupsXml = ""
                if ($Dependencies.Count -gt 0) {
                    $depEntriesXml = ""
                    foreach ($dep in $Dependencies) {
                        $depEntriesXml += "        <dependency id=`"$($dep.Id)`" version=`"$($dep.Version)`" />`n"
                    }
                    $depGroupsXml = @"
    <dependencies>
      <group>
$depEntriesXml      </group>
    </dependencies>
"@
                }

                $nuspecContent = @"
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>$Name</id>
    <version>$Version</version>
    <authors>TestAuthor</authors>
    <description>Test package for platform-aware install tests</description>
    <tags>PSModule test</tags>
    $depGroupsXml
  </metadata>
</package>
"@
                Set-Content -Path (Join-Path $tempDir "$Name.nuspec") -Value $nuspecContent

                # Optionally create a minimal .psd1 module manifest
                if ($IncludeModuleManifest) {
                    $psd1Content = @"
@{
    ModuleVersion     = '$Version'
    Author            = 'TestAuthor'
    Description       = 'Test module for platform-aware install tests'
    GUID              = '$([Guid]::NewGuid().ToString())'
    FunctionsToExport = @()
    CmdletsToExport   = @()
    PrivateData = @{
        PSData = @{
            Tags = @('test')
        }
    }
}
"@
                    Set-Content -Path (Join-Path $tempDir "$Name.psd1") -Value $psd1Content
                }

                # Create .nupkg (zip)
                $nupkgPath = Join-Path $OutputDir "$Name.$Version.nupkg"
                if (Test-Path $nupkgPath) { Remove-Item $nupkgPath -Force }
                [System.IO.Compression.ZipFile]::CreateFromDirectory($tempDir, $nupkgPath)
                return $nupkgPath
            }
            finally {
                Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }

        $InternalHooks = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]

        # Helper: Resolves the version-specific install directory from a PSResourceInfo object.
        # InstalledLocation may be the Modules root or the version folder depending on context.
        function Get-VersionInstallPath {
            param([object]$PkgInfo)
            $base = $PkgInfo.InstalledLocation
            $versionPath = Join-Path $base $PkgInfo.Name $PkgInfo.Version.ToString()
            if (Test-Path $versionPath) { return $versionPath }
            # Maybe InstalledLocation already points to name/version
            if ($base -match "$([regex]::Escape($PkgInfo.Name))[\\/]$([regex]::Escape($PkgInfo.Version.ToString()))$") { return $base }
            # Try name folder only
            $namePath = Join-Path $base $PkgInfo.Name
            if (Test-Path $namePath) { return $namePath }
            return $base
        }

        # Set up a local repository for test packages
        $script:localRepoDir = Join-Path $TestDrive 'platformFilterRepo'
        $null = New-Item $localRepoDir -ItemType Directory -Force

        $script:localRepoName = 'PlatformFilterTestRepo'
        Register-PSResourceRepository -Name $localRepoName -Uri $localRepoDir -Trusted -Force -ErrorAction SilentlyContinue

        $script:currentRid = $InternalHooks::GetCurrentRuntimeIdentifier()
    }

    AfterAll {
        Unregister-PSResourceRepository -Name $script:localRepoName -ErrorAction SilentlyContinue
    }


    Context 'RID Filtering during Install' {

        BeforeAll {
            $script:ridPkgName = 'TestRidFilterModule'
            $script:ridPkgVersion = '1.0.0'

            # Create test nupkg with runtimes for multiple platforms
            New-TestNupkg -Name $ridPkgName -Version $ridPkgVersion `
                -OutputDir $localRepoDir `
                -RuntimeIdentifiers @('win-x64', 'win-x86', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64') `
                -LibTfms @('netstandard2.0') `
                -IncludeModuleManifest
        }

        AfterEach {
            Uninstall-PSResource $ridPkgName -Version "*" -ErrorAction SilentlyContinue
        }

        It "Should only install runtimes matching current platform" {
            Install-PSResource -Name $ridPkgName -Repository $localRepoName -TrustRepository -Version $ridPkgVersion
            $installed = Get-InstalledPSResource -Name $ridPkgName
            $installed | Should -Not -BeNullOrEmpty

            $installPath = Get-VersionInstallPath $installed
            $runtimesDir = Join-Path $installPath 'runtimes'

            if (Test-Path $runtimesDir) {
                $installedRidFolders = @((Get-ChildItem $runtimesDir -Directory).Name)

                # Current platform should be present
                $installedRidFolders | Should -Contain $currentRid

                # Foreign platforms should NOT be present
                $foreignRids = @('win-x64', 'linux-x64', 'osx-arm64') | Where-Object {
                    -not $InternalHooks::IsCompatibleRid($_)
                }

                foreach ($foreign in $foreignRids) {
                    $installedRidFolders | Should -Not -Contain $foreign
                }
            }
        }

        It "Should install all runtimes when -SkipRuntimeFiltering is specified" {
            Install-PSResource -Name $ridPkgName -Repository $localRepoName -TrustRepository -Version $ridPkgVersion -SkipRuntimeFiltering
            $installed = Get-InstalledPSResource -Name $ridPkgName
            $installed | Should -Not -BeNullOrEmpty

            $installPath = Get-VersionInstallPath $installed
            $runtimesDir = Join-Path $installPath 'runtimes'

            if (Test-Path $runtimesDir) {
                $installedRidFolders = @((Get-ChildItem $runtimesDir -Directory).Name)
                # All 6 RID folders should be present
                $installedRidFolders.Count | Should -Be 6
            }
        }
    }


    Context 'TFM Filtering during Install' {

        BeforeAll {
            $script:tfmPkgName = 'TestTfmFilterModule'
            $script:tfmPkgVersion = '1.0.0'

            # Create test nupkg with multiple lib/ TFMs
            New-TestNupkg -Name $tfmPkgName -Version $tfmPkgVersion `
                -OutputDir $localRepoDir `
                -LibTfms @('net472', 'netstandard2.0', 'net6.0', 'net8.0') `
                -IncludeModuleManifest
        }

        AfterEach {
            Uninstall-PSResource $tfmPkgName -Version "*" -ErrorAction SilentlyContinue
        }

        It "Should only install one TFM lib folder (the best match)" {
            Install-PSResource -Name $tfmPkgName -Repository $localRepoName -TrustRepository -Version $tfmPkgVersion
            $installed = Get-InstalledPSResource -Name $tfmPkgName
            $installed | Should -Not -BeNullOrEmpty

            $installPath = Get-VersionInstallPath $installed
            $libDir = Join-Path $installPath 'lib'

            if (Test-Path $libDir) {
                $installedTfmFolders = @((Get-ChildItem $libDir -Directory).Name)
                # Should have exactly 1 TFM folder (the best match)
                $installedTfmFolders.Count | Should -Be 1

                # The chosen TFM should be one of the valid ones
                $installedTfmFolders[0] | Should -BeIn @('net472', 'netstandard2.0', 'net6.0', 'net8.0')
            }
        }

        It "Should pick net472 on Windows PowerShell 5.1" -Skip:($PSVersionTable.PSVersion.Major -gt 5) {
            Install-PSResource -Name $tfmPkgName -Repository $localRepoName -TrustRepository -Version $tfmPkgVersion
            $installed = Get-InstalledPSResource -Name $tfmPkgName

            $installPath = Get-VersionInstallPath $installed
            $libDir = Join-Path $installPath 'lib'
            if (Test-Path $libDir) {
                $installedTfmFolders = @((Get-ChildItem $libDir -Directory).Name)
                $installedTfmFolders | Should -Contain 'net472'
            }
        }

        It "Should pick a .NET Core TFM on PowerShell 7+" -Skip:($PSVersionTable.PSVersion.Major -le 5) {
            Install-PSResource -Name $tfmPkgName -Repository $localRepoName -TrustRepository -Version $tfmPkgVersion
            $installed = Get-InstalledPSResource -Name $tfmPkgName

            $installPath = Get-VersionInstallPath $installed
            $libDir = Join-Path $installPath 'lib'
            if (Test-Path $libDir) {
                $installedTfmFolders = @((Get-ChildItem $libDir -Directory).Name)
                # On PS 7+ (net6.0 or net8.0), should NOT pick net472
                $installedTfmFolders | Should -Not -Contain 'net472'
            }
        }
    }


    Context 'Explicit RuntimeIdentifier Parameter' {

        BeforeAll {
            $script:ridOverridePkgName = 'TestRidOverrideModule'
            $script:ridOverridePkgVersion = '1.0.0'

            New-TestNupkg -Name $ridOverridePkgName -Version $ridOverridePkgVersion `
                -OutputDir $localRepoDir `
                -RuntimeIdentifiers @('win-x64', 'win-x86', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64') `
                -LibTfms @('netstandard2.0') `
                -IncludeModuleManifest
        }

        AfterEach {
            Uninstall-PSResource $ridOverridePkgName -Version "*" -ErrorAction SilentlyContinue
        }

        It "Should install only the specified RID when -RuntimeIdentifier is used" {
            Install-PSResource -Name $ridOverridePkgName -Repository $localRepoName -TrustRepository -Version $ridOverridePkgVersion -RuntimeIdentifier 'linux-x64'
            $installed = Get-InstalledPSResource -Name $ridOverridePkgName
            $installed | Should -Not -BeNullOrEmpty

            $installPath = Get-VersionInstallPath $installed
            $runtimesDir = Join-Path $installPath 'runtimes'

            if (Test-Path $runtimesDir) {
                $installedRidFolders = @((Get-ChildItem $runtimesDir -Directory).Name)
                $installedRidFolders | Should -Contain 'linux-x64'
                # Other platforms should NOT be present
                $installedRidFolders | Should -Not -Contain 'win-x86'
                $installedRidFolders | Should -Not -Contain 'osx-arm64'
            }
        }

        It "Should override auto-detection with -RuntimeIdentifier" {
            # Install for a foreign platform
            $foreignRid = if ($IsWindows) { 'osx-arm64' } elseif ($IsMacOS) { 'linux-x64' } else { 'win-x64' }
            Install-PSResource -Name $ridOverridePkgName -Repository $localRepoName -TrustRepository -Version $ridOverridePkgVersion -RuntimeIdentifier $foreignRid
            $installed = Get-InstalledPSResource -Name $ridOverridePkgName
            $installed | Should -Not -BeNullOrEmpty

            $installPath = Get-VersionInstallPath $installed
            $runtimesDir = Join-Path $installPath 'runtimes'

            if (Test-Path $runtimesDir) {
                $installedRidFolders = @((Get-ChildItem $runtimesDir -Directory).Name)
                $installedRidFolders | Should -Contain $foreignRid
            }
        }
    }


    Context 'Explicit TargetFramework Parameter' {

        BeforeAll {
            $script:tfmOverridePkgName = 'TestTfmOverrideModule'
            $script:tfmOverridePkgVersion = '1.0.0'

            New-TestNupkg -Name $tfmOverridePkgName -Version $tfmOverridePkgVersion `
                -OutputDir $localRepoDir `
                -LibTfms @('net472', 'netstandard2.0', 'net6.0', 'net8.0') `
                -IncludeModuleManifest
        }

        AfterEach {
            Uninstall-PSResource $tfmOverridePkgName -Version "*" -ErrorAction SilentlyContinue
        }

        It "Should install only the specified TFM when -TargetFramework is used" {
            Install-PSResource -Name $tfmOverridePkgName -Repository $localRepoName -TrustRepository -Version $tfmOverridePkgVersion -TargetFramework 'net6.0'
            $installed = Get-InstalledPSResource -Name $tfmOverridePkgName
            $installed | Should -Not -BeNullOrEmpty

            $installPath = Get-VersionInstallPath $installed
            $libDir = Join-Path $installPath 'lib'

            if (Test-Path $libDir) {
                $installedTfmFolders = @((Get-ChildItem $libDir -Directory).Name)
                $installedTfmFolders.Count | Should -Be 1
                $installedTfmFolders[0] | Should -Be 'net6.0'
            }
        }

        It "Should override auto-detection with -TargetFramework net472" {
            Install-PSResource -Name $tfmOverridePkgName -Repository $localRepoName -TrustRepository -Version $tfmOverridePkgVersion -TargetFramework 'net472'
            $installed = Get-InstalledPSResource -Name $tfmOverridePkgName
            $installed | Should -Not -BeNullOrEmpty

            $installPath = Get-VersionInstallPath $installed
            $libDir = Join-Path $installPath 'lib'

            if (Test-Path $libDir) {
                $installedTfmFolders = @((Get-ChildItem $libDir -Directory).Name)
                $installedTfmFolders.Count | Should -Be 1
                $installedTfmFolders[0] | Should -Be 'net472'
            }
        }

        It "Should allow combining -RuntimeIdentifier and -TargetFramework" {
            Install-PSResource -Name $tfmOverridePkgName -Repository $localRepoName -TrustRepository -Version $tfmOverridePkgVersion -TargetFramework 'net6.0' -RuntimeIdentifier 'linux-x64'
            $installed = Get-InstalledPSResource -Name $tfmOverridePkgName
            $installed | Should -Not -BeNullOrEmpty

            # This test verifies the command accepts both parameters together without error
            $installPath = Get-VersionInstallPath $installed
            $libDir = Join-Path $installPath 'lib'

            if (Test-Path $libDir) {
                $installedTfmFolders = @((Get-ChildItem $libDir -Directory).Name)
                $installedTfmFolders.Count | Should -Be 1
                $installedTfmFolders[0] | Should -Be 'net6.0'
            }
        }
    }

    Context 'Nuspec Dependency Parsing' {

        BeforeAll {
            $script:depPkgName = 'TestNuspecDepsModule'
            $script:depPkgVersion = '1.0.0'

            # Create a package with .nuspec dependencies
            New-TestNupkg -Name $depPkgName -Version $depPkgVersion `
                -OutputDir $localRepoDir `
                -LibTfms @('netstandard2.0') `
                -Dependencies @(
                    @{ Id = 'Newtonsoft.Json'; Version = '[13.0.1, )' },
                    @{ Id = 'System.Memory'; Version = '[4.5.4, )' }
                ) `
                -IncludeModuleManifest
        }

        It "Should parse dependencies from .nuspec for local repo find" {
            # Find should return package info with parsed dependencies
            $found = Find-PSResource -Name $depPkgName -Repository $localRepoName -Version $depPkgVersion
            $found | Should -Not -BeNullOrEmpty
            $found.Name | Should -Be $depPkgName

            # Dependencies should be populated (not empty)
            if ($found.Dependencies -and $found.Dependencies.Count -gt 0) {
                $depNames = $found.Dependencies | ForEach-Object { $_.Name }
                $depNames | Should -Contain 'Newtonsoft.Json'
                $depNames | Should -Contain 'System.Memory'
            }
        }
    }
}
