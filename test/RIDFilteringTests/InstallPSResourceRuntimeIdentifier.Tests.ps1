# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
Param()

$ProgressPreference = 'SilentlyContinue'
$modPath = "$PSScriptRoot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

Describe 'Install-PSResource RuntimeIdentifier filtering' -Tags 'CI' {
    BeforeAll {
        Add-Type -AssemblyName System.IO.Compression.FileSystem

        function New-RidTestPackage {
            param(
                [Parameter(Mandatory)]
                [string] $Name,

                [Parameter(Mandatory)]
                [string] $Version,

                [Parameter(Mandatory)]
                [string] $OutputDirectory,

                [string[]] $RootRuntimeIdentifiers = @(),

                [string[]] $StandardRuntimeIdentifiers = @(),

                [string[]] $LibTfms = @()
            )

            $packageSource = Join-Path ([System.IO.Path]::GetTempPath()) ([Guid]::NewGuid())
            $null = New-Item $packageSource -ItemType Directory -Force

            try {
                foreach ($rid in $RootRuntimeIdentifiers) {
                    $ridPath = Join-Path $packageSource $rid
                    $null = New-Item $ridPath -ItemType Directory -Force
                    Set-Content -Path (Join-Path $ridPath "native_$rid.txt") -Value $rid
                }

                foreach ($rid in $StandardRuntimeIdentifiers) {
                    $runtimesPath = Join-Path $packageSource 'runtimes'
                    $ridPath = Join-Path $runtimesPath $rid
                    $nativePath = Join-Path $ridPath 'native'
                    $null = New-Item $nativePath -ItemType Directory -Force
                    Set-Content -Path (Join-Path $nativePath "standard_$rid.txt") -Value $rid
                }

                foreach ($tfm in $LibTfms) {
                    $libPath = Join-Path (Join-Path $packageSource 'lib') $tfm
                    $null = New-Item $libPath -ItemType Directory -Force
                    Set-Content -Path (Join-Path $libPath "$Name.dll") -Value $tfm
                }

                Set-Content -Path (Join-Path $packageSource 'common.txt') -Value 'common-content'
                Set-Content -Path (Join-Path $packageSource "$Name.psm1") -Value ''

                $manifest = @"
@{
    RootModule = '$Name.psm1'
    ModuleVersion = '$Version'
    GUID = '$([Guid]::NewGuid())'
    Author = 'PSResourceGet Tests'
    Description = 'RID filtering test package'
    FunctionsToExport = @()
    CmdletsToExport = @()
}
"@
                Set-Content -Path (Join-Path $packageSource "$Name.psd1") -Value $manifest

                $nuspec = @"
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>$Name</id>
    <version>$Version</version>
    <authors>PSResourceGet Tests</authors>
    <description>RID filtering test package</description>
    <tags>PSModule test</tags>
  </metadata>
</package>
"@
                Set-Content -Path (Join-Path $packageSource "$Name.nuspec") -Value $nuspec

                $packagePath = Join-Path $OutputDirectory "$Name.$Version.nupkg"
                if (Test-Path $packagePath) {
                    Remove-Item $packagePath -Force
                }

                [System.IO.Compression.ZipFile]::CreateFromDirectory($packageSource, $packagePath)
            }
            finally {
                Remove-Item $packageSource -Recurse -Force -ErrorAction SilentlyContinue
            }
        }

        function Get-VersionInstallPath {
            param(
                [Parameter(Mandatory)]
                [object] $Package
            )

            $basePath = $Package.InstalledLocation
            $version = $Package.Version.ToString()
            $namePath = Join-Path $basePath $Package.Name
            $versionPath = Join-Path $namePath $version

            if (Test-Path $versionPath) {
                return $versionPath
            }

            if ($basePath -match "$([regex]::Escape($Package.Name))[\\/]$([regex]::Escape($version))$") {
                return $basePath
            }

            if (Test-Path $namePath) {
                return $namePath
            }

            return $basePath
        }

        Get-NewPSResourceRepositoryFile

        $script:InternalHooks = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]
        $script:currentRid = $InternalHooks::GetCurrentRuntimeIdentifier()
        $script:foreignRid = if ($currentRid -match '^win') {
            'linux-x64'
        }
        elseif ($currentRid -match '^linux') {
            'osx-arm64'
        }
        else {
            'win-x64'
        }

        $script:repoName = 'RidFilteringTestRepo'
        $script:repoPath = Join-Path $TestDrive 'rid-repository'
        $null = New-Item $repoPath -ItemType Directory -Force
        Register-PSResourceRepository -Name $repoName -Uri $repoPath -Trusted -Force

        $script:version = '1.0.0'
        $script:filterPackage = 'TestRidFilterModule'
        $script:mergePackage = 'TestRidMergeModule'
        $script:freshPackage = 'TestRidFreshModule'
        $script:warningPackage = 'TestRidWarningModule'
        $script:packageNames = @(
            $filterPackage,
            $mergePackage,
            $freshPackage,
            $warningPackage
        )

        New-RidTestPackage -Name $filterPackage -Version $version -OutputDirectory $repoPath `
            -RootRuntimeIdentifiers @($currentRid, $foreignRid) `
            -StandardRuntimeIdentifiers @($currentRid, $foreignRid) `
            -LibTfms @('net472', 'net8.0')

        New-RidTestPackage -Name $mergePackage -Version $version -OutputDirectory $repoPath `
            -RootRuntimeIdentifiers @($currentRid, $foreignRid)

        New-RidTestPackage -Name $freshPackage -Version $version -OutputDirectory $repoPath `
            -RootRuntimeIdentifiers @($currentRid, $foreignRid)

        New-RidTestPackage -Name $warningPackage -Version $version -OutputDirectory $repoPath `
            -RootRuntimeIdentifiers @($currentRid)
    }

    AfterEach {
        foreach ($packageName in $script:packageNames) {
            Uninstall-PSResource -Name $packageName -Version '*' -SkipDependencyCheck `
                -ErrorAction SilentlyContinue
        }

        if ($script:staleFreshPath -and (Test-Path $script:staleFreshPath)) {
            Remove-Item $script:staleFreshPath -Recurse -Force
        }

        $script:staleFreshPath = $null
    }

    AfterAll {
        Unregister-PSResourceRepository -Name $script:repoName -ErrorAction SilentlyContinue
        Get-RevertPSResourceRepositoryFile
    }

    It 'automatically filters root-level RID folders and leaves other layouts unchanged' {
        Install-PSResource -Name $filterPackage -Version $version -Repository $repoName `
            -TrustRepository

        $installed = Get-InstalledPSResource -Name $filterPackage
        $installPath = Get-VersionInstallPath $installed

        Test-Path (Join-Path $installPath $currentRid) | Should -BeTrue
        Test-Path (Join-Path $installPath $foreignRid) | Should -BeFalse
        Test-Path (Join-Path (Join-Path $installPath 'runtimes') $currentRid) | Should -BeTrue
        Test-Path (Join-Path (Join-Path $installPath 'runtimes') $foreignRid) | Should -BeTrue
        Test-Path (Join-Path (Join-Path $installPath 'lib') 'net472') | Should -BeTrue
        Test-Path (Join-Path (Join-Path $installPath 'lib') 'net8.0') | Should -BeTrue
        Test-Path (Join-Path $installPath 'common.txt') | Should -BeTrue
    }

    It 'selects a foreign root-level RID when RuntimeIdentifier is explicit' {
        Install-PSResource -Name $filterPackage -Version $version -Repository $repoName `
            -TrustRepository -RuntimeIdentifier $foreignRid

        $installed = Get-InstalledPSResource -Name $filterPackage
        $installPath = Get-VersionInstallPath $installed

        Test-Path (Join-Path $installPath $foreignRid) | Should -BeTrue
        Test-Path (Join-Path $installPath $currentRid) | Should -BeFalse
    }

    It 'completes common RuntimeIdentifier values' {
        $command = 'Install-PSResource -RuntimeIdentifier lin'
        $completion = TabExpansion2 $command $command.Length

        $completion.CompletionMatches.CompletionText | Should -Contain 'linux-x64'
        $completion.CompletionMatches.CompletionText | Should -Contain 'linux-arm64'
    }

    It 'merges an explicit RID into an existing version without overwriting existing content' {
        Install-PSResource -Name $mergePackage -Version $version -Repository $repoName `
            -TrustRepository

        $installed = Get-InstalledPSResource -Name $mergePackage
        $installPath = Get-VersionInstallPath $installed
        $preservedFile = Join-Path $installPath 'preserve-me.txt'
        Set-Content -Path $preservedFile -Value 'preserve'

        Install-PSResource -Name $mergePackage -Version $version -Repository $repoName `
            -TrustRepository -RuntimeIdentifier $foreignRid

        Test-Path (Join-Path $installPath $currentRid) | Should -BeTrue
        Test-Path (Join-Path $installPath $foreignRid) | Should -BeTrue
        Get-Content $preservedFile | Should -Be 'preserve'
    }

    It 'keeps merge state scoped to the already-installed parent work item' {
        Install-PSResource -Name $mergePackage -Version $version -Repository $repoName `
            -TrustRepository

        $installedMergePackage = Get-InstalledPSResource -Name $mergePackage
        $mergeInstallPath = Get-VersionInstallPath $installedMergePackage
        $moduleInstallRoot = Split-Path (Split-Path $mergeInstallPath -Parent) -Parent
        $script:staleFreshPath = Join-Path (Join-Path $moduleInstallRoot $freshPackage) $version
        $null = New-Item $staleFreshPath -ItemType Directory -Force
        Set-Content -Path (Join-Path $staleFreshPath 'stale.txt') -Value 'stale'

        Install-PSResource -Name @($mergePackage, $freshPackage) -Version $version `
            -Repository $repoName -TrustRepository -RuntimeIdentifier $foreignRid

        $installedFreshPackage = Get-InstalledPSResource -Name $freshPackage
        $freshInstallPath = Get-VersionInstallPath $installedFreshPackage

        Test-Path (Join-Path $mergeInstallPath $currentRid) | Should -BeTrue
        Test-Path (Join-Path $mergeInstallPath $foreignRid) | Should -BeTrue
        Test-Path (Join-Path $freshInstallPath $foreignRid) | Should -BeTrue
        Test-Path (Join-Path $freshInstallPath 'stale.txt') | Should -BeFalse
    }

    It 'warns when an explicit RuntimeIdentifier has no compatible root-level folder' {
        $warnings = $null
        Install-PSResource -Name $warningPackage -Version $version -Repository $repoName `
            -TrustRepository -RuntimeIdentifier $foreignRid -WarningVariable warnings

        $warningText = $warnings | Out-String
        $warningText | Should -Match ([regex]::Escape($foreignRid))
        $warningText | Should -Match 'not found'
        $warningText | Should -Match ([regex]::Escape($currentRid))
    }
}
