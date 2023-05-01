# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose
# Explicitly import build module because in CI PowerShell can autoload PSGetv2
# This ensures the build module is always being tested
$buildModule = "$psscriptroot/../../out/PowerShellGet"
Import-Module $buildModule -Force -Verbose
Write-Host "PowerShellGet version currently loaded: $($(Get-Module powershellget).Version)"

Describe "Test CompatPowerShellGet: Unregister-PSResourceRepository" -tags 'CI' {
    BeforeEach {
        $PSGalleryName = Get-PSGalleryName
        $PSGalleryUri = Get-PSGalleryLocation
        Get-NewPSResourceRepositoryFile
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path $TestDrive -ChildPath "tmpDir3"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path)
        Get-NewTestDirs($tmpDirPaths)
    }
    AfterEach {
        Get-RevertPSResourceRepositoryFile
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path $TestDrive -ChildPath "tmpDir3"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path)
        Get-RemoveTestDirs($tmpDirPaths)
    }

    It "unregister single repository previously registered" {
        Register-PSResourceRepository -Name "testRepository" -Uri $tmpDir1Path
        Unregister-PSRepository -Name "testRepository"

        $res = Get-PSResourceRepository -Name "testRepository" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
    }

    It "unregister multiple repositories previously registered" {
        Register-PSResourceRepository -Name "testRepository" -Uri $tmpDir1Path
        Register-PSResourceRepository -Name "testRepository2" -Uri $tmpDir2Path
        Unregister-PSRepository -Name "testRepository","testRepository2"

        $res = Get-PSResourceRepository -Name "testRepository","testRepository2" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
    }

    It "not unregister repo not previously registered and throw expected error message" {
        $name = "nonRegisteredRepository"
        {Unregister-PSRepository -Name $name -ErrorAction Stop} | Should -Throw -ErrorId "ErrorUnregisteringSpecifiedRepo,Microsoft.PowerShell.PowerShellGet.Cmdlets.UnregisterPSResourceRepository"

    }

    It "not register when -Name contains wildcard" {
        Register-PSResourceRepository -Name "testRepository" -Uri $tmpDir1Path
        Register-PSResourceRepository -Name "testRepository2" -Uri $tmpDir2Path
        Unregister-PSRepository -Name "testRepository*" -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "nameContainsWildCardError,Microsoft.PowerShell.PowerShellGet.Cmdlets.UnregisterPSResourceRepository"
    }

    It "when multiple repo Names provided, if one name isn't valid unregister the rest and write error message" {
        $nonRegisteredRepoName = "nonRegisteredRepository"
        Register-PSResourceRepository -Name "testRepository" -Uri $tmpDir1Path
        Unregister-PSRepository -Name $nonRegisteredRepoName,"testRepository" -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ErrorUnregisteringSpecifiedRepo,Microsoft.PowerShell.PowerShellGet.Cmdlets.UnregisterPSResourceRepository"
    }

    It "throw error if Name is null or empty" {
        {Unregister-PSRepository -Name "" -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Unregister-PSRepository"
    }

    It "throw error if Name is null" {
        {Unregister-PSRepository -Name $null -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Unregister-PSRepository"
    }

    It "unregister repository PSGallery" {
        Unregister-PSRepository -Name $PSGalleryName

        $res = Get-PSResourceRepository -Name $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ErrorGettingSpecifiedRepo,Microsoft.PowerShell.PowerShellGet.Cmdlets.GetPSResourceRepository"
    }
}

# Ensure that PSGet v2 was not loaded during the test via command discovery
$PSGetVersionsLoaded = (Get-Module powershellget).Version
Write-Host "PowerShellGet versions currently loaded: $PSGetVersionsLoaded"
if ($PSGetVersionsLoaded.Count -gt 1) {
    throw  "There was more than one version of PowerShellGet imported into the current session. `
        Imported versions include: $PSGetVersionsLoaded"
}