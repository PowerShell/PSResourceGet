# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test Get-PSResourceRepository" {
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
        Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be $TestRepoName1
    }

    It "get all repositories matching single wildcard name" {
        Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path
        Register-PSResourceRepository -Name $TestRepoName2 -URL $tmpDir2Path
        Register-PSResourceRepository -Name $TestRepoName3 -URL $tmpDir3Path
        $res = Get-PSResourceRepository -Name "testReposit*"
        foreach ($entry in $res) {
            $entry.Name | Should -Match "testReposit"
        }
    }

    It "get all repositories matching multiple wildcard names" {
        Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path
        Register-PSResourceRepository -Name $TestRepoName2 -URL $tmpDir2Path
        Register-PSResourceRepository -Name "MyGallery" -URL $tmpDir3Path

        $res = Get-PSResourceRepository -Name "testReposit*","*Gallery"
        foreach ($entry in $res) {
            $entry.Name | Should -Match "testReposit|Gallery"
        }
    }

    It "get all repositories matching multiple valid names provided" {
        Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path
        Register-PSResourceRepository -Name "MyGallery" -URL $tmpDir2Path

        $res = Get-PSResourceRepository -Name $TestRepoName1,"MyGallery"
        foreach ($entry in $res) {
            $entry.Name | Should -BeIn $TestRepoName1,"MyGallery"
        }
    }

    It "not get repository that hasn't been registered/invalid name" {
        $nonRegisteredRepoName = "nonRegisteredRepository"
        $res = Get-PSResourceRepository -Name $nonRegisteredRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ErrorGettingSpecifiedRepo,Microsoft.PowerShell.PowerShellGet.Cmdlets.GetPSResourceRepository"
    }

    It "given invalid and valid Names, get valid ones and write error for non valid ones" {
        $nonRegisteredRepoName = "nonRegisteredRepository"

        Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path
        Register-PSResourceRepository -Name $TestRepoName2 -URL $tmpDir2Path

        $res = Get-PSResourceRepository -Name $TestRepoName1,$nonRegisteredRepoName,$TestRepoName2 -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ErrorGettingSpecifiedRepo,Microsoft.PowerShell.PowerShellGet.Cmdlets.GetPSResourceRepository"

        # should have successfully got the other valid/registered repositories with no error
        foreach ($entry in $res) {
            $entry.Name | Should -BeIn $TestRepoName1,$TestRepoName2
        }
    }

    It "given invalid and valid CredentialInfo, get valid ones and write error for non valid ones" {
        Get-NewPSResourceRepositoryFileWithCredentialInfo

        $res = Get-PSResourceRepository -Name "localtestrepo*" -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ErrorGettingSpecifiedRepo,Microsoft.PowerShell.PowerShellGet.Cmdlets.GetPSResourceRepository"

        # should have successfully got the other valid/registered repositories with no error
        foreach ($entry in $res) {
            $entry.Name | Should -BeIn "localtestrepo1","localtestrepo2"
        }
    }

    It "throw error and get no repositories when provided null Name" {
        # $errorMsg = "Cannot validate argument on parameter 'Name'. The argument is null or empty. Provide an argument that is not null or empty, and then try the command again."
        {Get-PSResourceRepository -Name $null -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Microsoft.PowerShell.PowerShellGet.Cmdlets.GetPSResourceRepository"
    }

    It "throw error and get no repositories when provided empty string Name" {
        # $errorMsg = "Cannot validate argument on parameter 'Name'. The argument is null, empty, or an element of the argument collection contains a null value. Supply a collection that does not contain any null values and then try the command again."
        {Get-PSResourceRepository -Name "" -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Microsoft.PowerShell.PowerShellGet.Cmdlets.GetPSResourceRepository"
    }

    It "find all repositories if no Name provided" {
        $res = Get-PSResourceRepository
        $res.Count | Should -BeGreaterThan 0
    }
}
