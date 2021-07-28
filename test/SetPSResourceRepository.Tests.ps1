# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test Set-PSResourceRepository" {
    BeforeEach {
        $PSGalleryName = Get-PSGalleryName
        $PSGalleryURL = Get-PSGalleryLocation
        Get-NewPSResourceRepositoryFile
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path $TestDrive -ChildPath "tmpDir3"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path)
        Get-NewTestDirs($tmpDirPaths)

        $relativeCurrentPath = Get-Location
    }
    AfterEach {
        Get-RevertPSResourceRepositoryFile
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path $TestDrive -ChildPath "tmpDir3"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path)
        Get-RemoveTestDirs($tmpDirPaths)
    }

    It "set repository given Name and URL parameters" {
        Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path
        Set-PSResourceRepository -Name "testRepository" -URL $tmpDir2Path
        $res = Get-PSResourceRepository -Name "testRepository"
        $res.Name | Should -Be "testRepository"
        $res.URL.LocalPath | Should -Contain $tmpDir2Path
        $res.Priority | Should -Be 50
        $res.Trusted | Should -Be False
    }

    It "set repository given Name and Priority parameters" {
        Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path
        Set-PSResourceRepository -Name "testRepository" -Priority 25
        $res = Get-PSResourceRepository -Name "testRepository"
        $res.Name | Should -Be "testRepository"
        $res.URL.LocalPath | Should -Contain $tmpDir1Path
        $res.Priority | Should -Be 25
        $res.Trusted | Should -Be False
    }

    It "set repository given Name and Trusted parameters" {
        Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path
        Set-PSResourceRepository -Name "testRepository" -Trusted
        $res = Get-PSResourceRepository -Name "testRepository"
        $res.Name | Should -Be "testRepository"
        $res.URL.LocalPath | Should -Contain $tmpDir1Path
        $res.Priority | Should -Be 50
        $res.Trusted | Should -Be True
    }

    It "not set repository and write error given just Name parameter" {
        Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path
        {Set-PSResourceRepository -Name "testRepository" -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"
    }

    $testCases = @{Type = "contains *";     Name = "test*Repository"; ErrorId = "ErrorInNameParameterSet"},
                 @{Type = "is whitespace";  Name = " ";               ErrorId = "ErrorInNameParameterSet"},
                 @{Type = "is null";        Name = $null;             ErrorId = "ParameterArgumentValidationError"}

    It "not set repository and throw error given Name <Type> (NameParameterSet)" -TestCases $testCases {
        param($Type, $Name)

        Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path
        {Set-PSResourceRepository -Name $Name -Priority 25 -ErrorAction Stop} | Should -Throw -ErrorId "$ErrorId,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"
    }

    $testCases2 = @{Type = "contains *";     Name = "test*Repository2";  ErrorId = "ErrorSettingIndividualRepoFromRepositories"},
                  @{Type = "is whitespace";  Name = " ";                 ErrorId = "ErrorSettingIndividualRepoFromRepositories"},
                  @{Type = "is null";        Name = $null;               ErrorId = "NullNameForRepositoriesParameterSetRepo"}
    It "not set repository and write error given Name <Type> (RepositoriesParameterSet)" -TestCases $testCases2 {
        param($Type, $Name, $ErrorId)

        Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path
        Register-PSResourceRepository -Name "testRepository2" -URL $tmpDir2Path

        $hashtable1 = @{Name = "testRepository"; URL = $tmpDir3Path}
        $hashtable2 = @{Name = "testRepository2"; Priority = 25}
        $incorrectHashTable = @{Name = $Name; Trusted = $True}
        $arrayOfHashtables = $hashtable1, $incorrectHashTable, $hashtable2

        Set-PSResourceRepository -Repositories $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "$ErrorId,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"

        $res = Get-PSResourceRepository -Name "testRepository"
        $res.URL.LocalPath | Should -Contain $tmpDir3Path
        $res.Trusted | Should -Be False

        $res2 = Get-PSResourceRepository -Name "testRepository2"
        $res2.Priority | Should -Be 25
        $res2.Trusted | Should -Be False
    }

    It "set repositories with Repositories parameter" {
        Unregister-PSResourceRepository -Name "PSGallery"
        Register-PSResourceRepository -Name "testRepository1" -URL $tmpDir1Path
        Register-PSResourceRepository -Name "testRepository2" -URL $tmpDir2Path
        Register-PSResourceRepository -PSGallery

        $hashtable1 = @{Name = "testRepository1"; URL = $tmpDir2Path};
        $hashtable2 = @{Name = "testRepository2"; Priority = 25};
        $hashtable3 = @{Name = "PSGallery"; Trusted = $True};
        $arrayOfHashtables = $hashtable1, $hashtable2, $hashtable3

        Set-PSResourceRepository -Repositories $arrayOfHashtables
        $res = Get-PSResourceRepository -Name "testRepository1"
        $res.Name | Should -Be "testRepository1"
        $res.URL.LocalPath | Should -Contain $tmpDir2Path
        $res.Priority | Should -Be 50
        $res.Trusted | Should -Be False

        $res2 = Get-PSResourceRepository -Name "testRepository2"
        $res2.Name | Should -Be "testRepository2"
        $res2.URL.LocalPath | Should -Contain $tmpDir2Path
        $res2.Priority | Should -Be 25
        $res2.Trusted | Should -Be False

        $res3 = Get-PSResourceRepository -Name $PSGalleryName
        $res3.Name | Should -Be $PSGalleryName
        $res3.URL | Should -Contain $PSGalleryURL
        $res3.Priority | Should -Be 50
        $res3.Trusted | Should -Be True
    }

    It "not set and throw error for trying to set PSGallery URL (NameParameterSet)" {
        Unregister-PSResourceRepository -Name "PSGallery"
        Register-PSResourceRepository -PSGallery
        {Set-PSResourceRepository -Name "PSGallery" -URL $tmpDir1Path -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"
    }

    It "not set repository and throw error for trying to set PSGallery URL (RepositoriesParameterSet)" {
        Unregister-PSResourceRepository -Name "PSGallery"
        Register-PSResourceRepository -PSGallery

        Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path

        $hashtable1 = @{Name = "PSGallery"; URL = $tmpDir1Path}
        $hashtable2 = @{Name = "testRepository"; Priority = 25}
        $arrayOfHashtables = $hashtable1, $hashtable2

        Set-PSResourceRepository -Repositories $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ErrorSettingIndividualRepoFromRepositories,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"

        $res = Get-PSResourceRepository -Name "testRepository"
        $res.URL.LocalPath | Should -Contain $tmpDir1Path
        $res.Priority | Should -Be 25
        $res.Trusted | Should -Be False
    }

    It "should set repository with relative URL provided" {
        Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path
        Set-PSResourceRepository -Name "testRepository" -URL $relativeCurrentPath
        $res = Get-PSResourceRepository -Name "testRepository"
        $res.Name | Should -Be "testRepository"
        $res.URL.LocalPath | Should -Contain $relativeCurrentPath
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }
}
