# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test Set-PSResourceRepository" {
    BeforeEach {
        $PSGalleryName = Get-PSGalleryName
        $PSGalleryURL = Get-PSGalleryLocation
        $TestRepoName1 = "testRepository"
        $TestRepoName2 = "testRepository2"
        $relativeCurrentPath = Get-Location
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

    It "set repository given Name and URL parameters" {
        Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path
        Set-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir2Path
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1
        $res.URL.LocalPath | Should -Contain $tmpDir2Path
        $res.Priority | Should -Be 50
        $res.Trusted | Should -Be False
    }

    It "set repository given Name and Priority parameters" {
        Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path
        Set-PSResourceRepository -Name $TestRepoName1 -Priority 25
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1
        $res.URL.LocalPath | Should -Contain $tmpDir1Path
        $res.Priority | Should -Be 25
        $res.Trusted | Should -Be False
    }

    It "set repository given Name and Trusted parameters" {
        Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path
        Set-PSResourceRepository -Name $TestRepoName1 -Trusted
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1
        $res.URL.LocalPath | Should -Contain $tmpDir1Path
        $res.Priority | Should -Be 50
        $res.Trusted | Should -Be True
    }

    It "not set repository and write error given just Name parameter" {
        Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path
        {Set-PSResourceRepository -Name $TestRepoName1 -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"
    }

    $testCases = @{Type = "contains *";     Name = "test*Repository"; ErrorId = "ErrorInNameParameterSet"},
                 @{Type = "is whitespace";  Name = " ";               ErrorId = "ErrorInNameParameterSet"},
                 @{Type = "is null";        Name = $null;             ErrorId = "ParameterArgumentValidationError"}

    It "not set repository and throw error given Name <Type> (NameParameterSet)" -TestCases $testCases {
        param($Type, $Name)

        Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path
        {Set-PSResourceRepository -Name $Name -Priority 25 -ErrorAction Stop} | Should -Throw -ErrorId "$ErrorId,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"
    }

    $testCases2 = @{Type = "contains *";     Name = "test*Repository2";  ErrorId = "ErrorSettingIndividualRepoFromRepositories"},
                  @{Type = "is whitespace";  Name = " ";                 ErrorId = "ErrorSettingIndividualRepoFromRepositories"},
                  @{Type = "is null";        Name = $null;               ErrorId = "NullNameForRepositoriesParameterSetRepo"}
    It "not set repository and write error given Name <Type> (RepositoriesParameterSet)" -TestCases $testCases2 {
        param($Type, $Name, $ErrorId)

        Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path
        Register-PSResourceRepository -Name $TestRepoName2 -URL $tmpDir2Path

        $hashtable1 = @{Name = $TestRepoName1; URL = $tmpDir3Path}
        $hashtable2 = @{Name = $TestRepoName2; Priority = 25}
        $incorrectHashTable = @{Name = $Name; Trusted = $True}
        $arrayOfHashtables = $hashtable1, $incorrectHashTable, $hashtable2

        Set-PSResourceRepository -Repositories $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "$ErrorId,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"

        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.URL.LocalPath | Should -Contain $tmpDir3Path
        $res.Trusted | Should -Be False

        $res2 = Get-PSResourceRepository -Name $TestRepoName2
        $res2.Priority | Should -Be 25
        $res2.Trusted | Should -Be False
    }

    It "set repositories with Repositories parameter" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path
        Register-PSResourceRepository -Name $TestRepoName2 -URL $tmpDir2Path
        Register-PSResourceRepository -PSGallery

        $hashtable1 = @{Name = $TestRepoName1; URL = $tmpDir2Path};
        $hashtable2 = @{Name = $TestRepoName2; Priority = 25};
        $hashtable3 = @{Name = $PSGalleryName; Trusted = $True};
        $arrayOfHashtables = $hashtable1, $hashtable2, $hashtable3

        Set-PSResourceRepository -Repositories $arrayOfHashtables
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1
        $res.URL.LocalPath | Should -Contain $tmpDir2Path
        $res.Priority | Should -Be 50
        $res.Trusted | Should -Be False

        $res2 = Get-PSResourceRepository -Name $TestRepoName2
        $res2.Name | Should -Be $TestRepoName2
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
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -PSGallery
        {Set-PSResourceRepository -Name $PSGalleryName -URL $tmpDir1Path -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"
    }

    It "not set repository and throw error for trying to set PSGallery URL (RepositoriesParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -PSGallery

        Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path

        $hashtable1 = @{Name = $PSGalleryName; URL = $tmpDir1Path}
        $hashtable2 = @{Name = $TestRepoName1; Priority = 25}
        $arrayOfHashtables = $hashtable1, $hashtable2

        Set-PSResourceRepository -Repositories $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ErrorSettingIndividualRepoFromRepositories,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"

        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.URL.LocalPath | Should -Contain $tmpDir1Path
        $res.Priority | Should -Be 25
        $res.Trusted | Should -Be False
    }

    It "should set repository with relative URL provided" {
        Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path
        Set-PSResourceRepository -Name $TestRepoName1 -URL $relativeCurrentPath
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1
        $res.URL.LocalPath | Should -Contain $relativeCurrentPath
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "should set repository with local file share NuGet based Uri" {
        Register-PSResourceRepository -Name "localFileShareTestRepo" -URL $tmpDir1Path
        Set-PSResourceRepository -Name "localFileShareTestRepo" -URL "\\hcgg.rest.of.domain.name\test\ITxx\team\NuGet\"
        $res = Get-PSResourceRepository -Name "localFileShareTestRepo"
        $res.Name | Should -Be "localFileShareTestRepo"
        $res.URL.LocalPath | Should -Contain "\\hcgg.rest.of.domain.name\test\ITxx\team\NuGet\"
    }

    It "set repository and see updated repository with -PassThru" {
        Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path
        $res = Set-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir2Path -PassThru
        $res.Name | Should -Be $TestRepoName1
        $res.URL.LocalPath | Should -Contain $tmpDir2Path
        $res.Priority | Should -Be 50
        $res.Trusted | Should -Be False
    }
}
