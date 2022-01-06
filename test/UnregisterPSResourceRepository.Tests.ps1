# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test Unregister-PSResourceRepository" {
    BeforeEach {
        $TestGalleryName = Get-PoshTestGalleryName
        $TestGalleryUrl = Get-PoshTestGalleryLocation
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
        {Unregister-PSResourceRepository -Name $name -ErrorAction Stop} | Should -Throw -ErrorId "ErrorUnregisteringSpecifiedRepo,Microsoft.PowerShell.PowerShellGet.Cmdlets.UnregisterPSResourceRepository"

    }

    It "not register when -Name contains wildcard" {
        Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path
        Register-PSResourceRepository -Name "testRepository2" -URL $tmpDir2Path
        Unregister-PSResourceRepository -Name "testRepository*" -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "nameContainsWildCardError,Microsoft.PowerShell.PowerShellGet.Cmdlets.UnregisterPSResourceRepository"
    }

    It "when multiple repo Names provided, if one name isn't valid unregister the rest and write error message" {
        $nonRegisteredRepoName = "nonRegisteredRepository"
        Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path
        Unregister-PSResourceRepository -Name $nonRegisteredRepoName,"testRepository" -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ErrorUnregisteringSpecifiedRepo,Microsoft.PowerShell.PowerShellGet.Cmdlets.UnregisterPSResourceRepository"
    }

    It "throw error if Name is null or empty" {
        {Unregister-PSResourceRepository -Name "" -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Microsoft.PowerShell.PowerShellGet.Cmdlets.UnregisterPSResourceRepository"
    }

    It "throw error if Name is null" {
        {Unregister-PSResourceRepository -Name $null -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Microsoft.PowerShell.PowerShellGet.Cmdlets.UnregisterPSResourceRepository"
    }

    It "unregister repository using -PassThru" {
        $res = Unregister-PSResourceRepository -Name $TestGalleryName -PassThru
        $res.Name | Should -Be $TestGalleryName
        $res.Url | Should -Be $TestGalleryURL
        $res = Get-PSResourceRepository -Name $TestGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ErrorGettingSpecifiedRepo,Microsoft.PowerShell.PowerShellGet.Cmdlets.GetPSResourceRepository"
    }
}
