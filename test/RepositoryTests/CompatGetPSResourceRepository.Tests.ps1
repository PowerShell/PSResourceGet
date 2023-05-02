# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose
# Explicitly import build module because in CI PowerShell can autoload PSGetv2
# This ensures the build module is always being tested
$buildModule = "$psscriptroot/../../out/PowerShellGet"
Import-Module $buildModule -Force -Verbose
Write-Verbose -Verbose -Message "PowerShellGet version currently loaded: $($(Get-Module powershellget).Version)"

Describe "Test CompatPowerShellGet: Get-PSResourceRepository" -tags 'CI' {
    BeforeEach {
        $TestRepoName1 = "testRepository"
        $TestRepoName2 = "testRepository2"
        $TestRepoName3 = "testRepository3"
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

    It "get single already registered repo" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        $res = Get-PSRepository -Name $TestRepoName1
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be $TestRepoName1
    }

    It "get all repositories matching single wildcard name" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        Register-PSResourceRepository -Name $TestRepoName2 -Uri $tmpDir2Path
        Register-PSResourceRepository -Name $TestRepoName3 -Uri $tmpDir3Path
        $res = Get-PSRepository -Name "testReposit*"
        foreach ($entry in $res) {
            $entry.Name | Should -Match "testReposit"
        }
    }

    It "get all repositories matching multiple wildcard names" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        Register-PSResourceRepository -Name $TestRepoName2 -Uri $tmpDir2Path
        Register-PSResourceRepository -Name "MyGallery" -Uri $tmpDir3Path

        $res = Get-PSRepository -Name "testReposit*","*Gallery"
        foreach ($entry in $res) {
            $entry.Name | Should -Match "testReposit|Gallery"
        }
    }
    It "get all repositories matching multiple valid names provided" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        Register-PSResourceRepository -Name "MyGallery" -Uri $tmpDir2Path

        $res = Get-PSRepository -Name $TestRepoName1,"MyGallery"
        foreach ($entry in $res) {
            $entry.Name | Should -BeIn $TestRepoName1,"MyGallery"
        }
    }

    It "not get repository that hasn't been registered/invalid name" {
        $nonRegisteredRepoName = "nonRegisteredRepository"
        $res = Get-PSRepository -Name $nonRegisteredRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ErrorGettingSpecifiedRepo,Microsoft.PowerShell.PowerShellGet.Cmdlets.GetPSResourceRepository"
    }

    It "given invalid and valid Names, get valid ones and write error for non valid ones" {
        $nonRegisteredRepoName = "nonRegisteredRepository"

        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        Register-PSResourceRepository -Name $TestRepoName2 -Uri $tmpDir2Path

        $res = Get-PSRepository -Name $TestRepoName1,$nonRegisteredRepoName,$TestRepoName2 -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ErrorGettingSpecifiedRepo,Microsoft.PowerShell.PowerShellGet.Cmdlets.GetPSResourceRepository"

        # should have successfully got the other valid/registered repositories with no error
        foreach ($entry in $res) {
            $entry.Name | Should -BeIn $TestRepoName1,$TestRepoName2
        }
    }

    It "given invalid and valid CredentialInfo, get valid ones and write error for non valid ones" {
        Get-NewPSResourceRepositoryFileWithCredentialInfo

        $res = Get-PSRepository -Name "localtestrepo*" -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ErrorGettingSpecifiedRepo,Microsoft.PowerShell.PowerShellGet.Cmdlets.GetPSResourceRepository"

        # should have successfully got the other valid/registered repositories with no error
        foreach ($entry in $res) {
            $entry.Name | Should -BeIn "localtestrepo1","localtestrepo2"
        }
    }

    It "throw error and get no repositories when provided null Name" {
        # $errorMsg = "Cannot validate argument on parameter 'Name'. The argument is null or empty. Provide an argument that is not null or empty, and then try the command again."
        {Get-PSRepository -Name $null -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Get-PSRepository"
    }

    It "throw error and get no repositories when provided empty string Name" {
        # $errorMsg = "Cannot validate argument on parameter 'Name'. The argument is null, empty, or an element of the argument collection contains a null value. Supply a collection that does not contain any null values and then try the command again."
        {Get-PSRepository -Name "" -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Get-PSRepository"
    }

    It "find all repositories if no Name provided" {
        $res = Get-PSRepository
        $res.Count | Should -BeGreaterThan 0
    }
}

# Ensure that PSGet v2 was not loaded during the test via command discovery
$PSGetVersionsLoaded = (Get-Module powershellget).Version
Write-Host "PowerShellGet versions currently loaded: $PSGetVersionsLoaded"
if ($PSGetVersionsLoaded.Count -gt 1) {
    throw  "There was more than one version of PowerShellGet imported into the current session. `
        Imported versions include: $PSGetVersionsLoaded"
}
