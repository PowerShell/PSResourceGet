# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test Register-PSResourceRepository" {
    BeforeEach {
        Get-NewPSResourceRepositoryFile
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path $TestDrive -ChildPath "tmpDir3"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path)
        New-TestDirs($tmpDirPaths)
    }
    AfterEach {
        Get-RevertPSResourceRepositoryFile
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path $TestDrive -ChildPath "tmpDir3"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path)
        Remove-TestDirs($tmpDirPaths)
    }

    It "unregister single repository previously registered" {
        Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path
        Unregister-PSResourceRepository -Name "testRepository"

        $res = Get-PSResourceRepository -Name "testRepository" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
    }

    It "unregister multiple repositories previously registered" {
        Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path
        Register-PSResourceRepository -Name "testRepository2" -URL $tmpDir2Path
        Unregister-PSResourceRepository -Name "testRepository","testRepository2"

        $res = Get-PSResourceRepository -Name "testRepository","testRepository2" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
    }

    It "not unregister repo not previously registered and throw expected error message" {
        $name = "nonRegisteredRepository"
        $errorMsg = "Unable to find repository '$name'.  Use Get-PSResourceRepository to see all available repositories."
        {Unregister-PSResourceRepository -Name $name -ErrorAction Stop} | Should -Throw $errorMsg

    }

    It "when multiple repo Names provided, if one name isn't valid unregister the rest and write error message" {
        $nonRegisteredRepoName = "nonRegisteredRepository"
        Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path
        Unregister-PSResourceRepository -Name $nonRegisteredRepoName,"testRepository" -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ErrorUnregisteringSpecifiedRepo,Microsoft.PowerShell.PowerShellGet.Cmdlets.UnregisterPSResourceRepository"
    }

    It "throw error if Name is null or empty" {
        $errorMsg = "Cannot validate argument on parameter 'Name'. The argument is null, empty, or an element of the argument collection contains a null value. Supply a collection that does not contain any null values and then try the command again."
        {Unregister-PSResourceRepository -Name "" -ErrorAction Stop} | Should -Throw $errorMsg
    }

    It "throw error if Name is null" {
        $errorMsg = "Cannot validate argument on parameter 'Name'. The argument is null or empty. Provide an argument that is not null or empty, and then try the command again."
        {Unregister-PSResourceRepository -Name $null -ErrorAction Stop} | Should -Throw $errorMsg
    }
}
