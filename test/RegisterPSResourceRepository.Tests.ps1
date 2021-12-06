# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test Register-PSResourceRepository" {
    BeforeEach {
        $PSGalleryName = Get-PSGalleryName
        $PSGalleryURL = Get-PSGalleryLocation
        $TestRepoName1 = "testRepository"
        $TestRepoName2 = "testRepository2"
        $TestRepoName3 = "testRepository3"
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

    It "register repository given Name, URL (bare minimum for NameParmaterSet)" {
        $res = Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path -PassThru
        $res.Name | Should -Be $TestRepoName1
        $res.URL.LocalPath | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "register repository with Name, URL, Trusted (NameParameterSet)" {
        $res = Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path -Trusted -PassThru
        $res.Name | Should -Be $TestRepoName1
        $res.URL.LocalPath | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 50
    }

    It "register repository given Name, URL, Trusted, Priority (NameParameterSet)" {
        $res = Register-PSResourceRepository -Name $TestRepoName1 -URL $tmpDir1Path -Trusted -Priority 20 -PassThru
        $res.Name | Should -Be $TestRepoName1
        $res.URL.LocalPath | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 20
    }

    It "register repository with PSGallery parameter (PSGalleryParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $res = Register-PSResourceRepository -PSGallery -PassThru
        $res.Name | Should -Be $PSGalleryName
        $res.URL | Should -Be $PSGalleryURL
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "register repository with PSGallery, Trusted parameters (PSGalleryParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $res = Register-PSResourceRepository -PSGallery -Trusted -PassThru
        $res.Name | Should -Be $PSGalleryName
        $res.URL | Should -Be $PSGalleryURL
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 50
    }

    It "register repository with PSGallery, Trusted, Priority parameters (PSGalleryParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $res = Register-PSResourceRepository -PSGallery -Trusted -Priority 20 -PassThru
        $res.Name | Should -Be $PSGalleryName
        $res.URL | Should -Be $PSGalleryURL
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 20
    }

    It "register repositories with Repositories parameter, all name parameter style repositories (RepositoriesParameterSet)" {
        $hashtable1 = @{Name = $TestRepoName1; URL = $tmpDir1Path}
        $hashtable2 = @{Name = $TestRepoName2; URL = $tmpDir2Path; Trusted = $True}
        $hashtable3 = @{Name = $TestRepoName3; URL = $tmpDir3Path; Trusted = $True; Priority = 20}
        $arrayOfHashtables = $hashtable1, $hashtable2, $hashtable3

        Register-PSResourceRepository -Repositories $arrayOfHashtables
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.URL.LocalPath | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50

        $res2 = Get-PSResourceRepository -Name $TestRepoName2
        $res2.URL.LocalPath | Should -Contain $tmpDir2Path
        $res2.Trusted | Should -Be True
        $res2.Priority | Should -Be 50

        $res3 = Get-PSResourceRepository -Name $TestRepoName3
        $res3.URL.LocalPath | Should -Contain $tmpDir3Path
        $res3.Trusted | Should -Be True
        $res3.Priority | Should -Be 20
    }

    It "register repositories with Repositories parameter, psgallery style repository (RepositoriesParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $hashtable1 = @{PSGallery = $True}
        Register-PSResourceRepository -Repositories $hashtable1
        $res = Get-PSResourceRepository -Name $PSGalleryName
        $res.URL | Should -Be $PSGalleryURL
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "register repositories with Repositories parameter, name and psgallery parameter styles (RepositoriesParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $hashtable1 = @{PSGallery = $True}
        $hashtable2 = @{Name = $TestRepoName1; URL = $tmpDir1Path}
        $hashtable3 = @{Name = $TestRepoName2; URL = $tmpDir2Path; Trusted = $True}
        $hashtable4 = @{Name = $TestRepoName3; URL = $tmpDir3Path; Trusted = $True; Priority = 20}
        $arrayOfHashtables = $hashtable1, $hashtable2, $hashtable3, $hashtable4

        Register-PSResourceRepository -Repositories $arrayOfHashtables

        $res1 = Get-PSResourceRepository -Name $PSGalleryName
        $res1.URL | Should -Be $PSGalleryURL
        $res1.Trusted | Should -Be False
        $res1.Priority | Should -Be 50

        $res2 = Get-PSResourceRepository -Name $TestRepoName1
        $res2.URL.LocalPath | Should -Contain $tmpDir1Path
        $res2.Trusted | Should -Be False
        $res2.Priority | Should -Be 50

        $res3 = Get-PSResourceRepository -Name $TestRepoName2
        $res3.URL.LocalPath | Should -Contain $tmpDir2Path
        $res3.Trusted | Should -Be True
        $res3.Priority | Should -Be 50

        $res4 = Get-PSResourceRepository -Name $TestRepoName3
        $res4.URL.LocalPath | Should -Contain $tmpDir3Path
        $res4.Trusted | Should -Be True
        $res4.Priority | Should -Be 20
    }

    It "not register repository when Name is provided but URL is not" {
        {Register-PSResourceRepository -Name $TestRepoName1 -URL "" -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    It "not register repository when Name is empty but URL is provided" {
        {Register-PSResourceRepository -Name "" -URL $tmpDir1Path -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    It "not register rpeository when Name is null but URL is provided" {
        {Register-PSResourceRepository -Name $null -URL $tmpDir1Path -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    It "not register repository when Name is just whitespace but URL is provided" {
        {Register-PSResourceRepository -Name " " -URL $tmpDir1Path -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    It "not register PSGallery with NameParameterSet" {
        {Register-PSResourceRepository -Name $PSGalleryName -URL $PSGalleryURL -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    # this error message comes from the parameter cmdlet tags (earliest point of detection)
    It "not register PSGallery when PSGallery parameter provided with Name or URL" {
        {Register-PSResourceRepository -PSGallery -Name $PSGalleryName -ErrorAction Stop} | Should -Throw -ErrorId "AmbiguousParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
        {Register-PSResourceRepository -PSGallery -URL $PSGalleryURL -ErrorAction Stop} | Should -Throw -ErrorId "AmbiguousParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    $testCases = @{Type = "Name key specified with PSGallery key"; IncorrectHashTable = @{PSGallery = $True; Name=$PSGalleryName}},
                 @{Type = "URL key specified with PSGallery key";  IncorrectHashTable = @{PSGallery = $True; URL=$PSGalleryURL}}

    It "not register incorrectly formatted PSGallery type repo among correct ones when incorrect type is <Type>" -TestCases $testCases {
        param($Type, $IncorrectHashTable)

        $correctHashtable1 = @{Name = $TestRepoName1; URL = $tmpDir1Path}
        $correctHashtable2 = @{Name = $TestRepoName2; URL = $tmpDir2Path; Trusted = $True}
        $correctHashtable3 = @{Name = $TestRepoName3; URL = $tmpDir3Path; Trusted = $True; Priority = 20}
        $arrayOfHashtables = $correctHashtable1, $correctHashtable2, $IncorrectHashTable, $correctHashtable3

        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -Repositories $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "NotProvideNameUrlForPSGalleryRepositoriesParameterSetRegistration,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"

        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1

        $res2 = Get-PSResourceRepository -Name $TestRepoName2
        $res2.Name | Should -Be $TestRepoName2

        $res3 = Get-PSResourceRepository -Name $TestRepoName3
        $res3.Name | Should -Be $TestRepoName3
    }

    $testCases2 = @{Type = "-Name is not specified";                 IncorrectHashTable = @{URL = $tmpDir1Path};                             ErrorId = "NullNameForRepositoriesParameterSetRegistration,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"},
                  @{Type = "-Name is PSGallery";                     IncorrectHashTable = @{Name = "PSGallery"; URL = $tmpDir1Path};         ErrorId = "PSGalleryProvidedAsNameRepoPSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"},
                  @{Type = "-URL not specified";                     IncorrectHashTable = @{Name = "testRepository"};                        ErrorId = "NullURLForRepositoriesParameterSetRegistration,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"},
                  @{Type = "-URL is not valid scheme";               IncorrectHashTable = @{Name = "testRepository"; URL="www.google.com"};  ErrorId = "InvalidUri,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"}

    It "not register incorrectly formatted Name type repo among correct ones when incorrect type is <Type>" -TestCases $testCases2 {
        param($Type, $IncorrectHashTable, $ErrorId)

        $correctHashtable1 = @{Name = $TestRepoName2; URL = $tmpDir2Path; Trusted = $True}
        $correctHashtable2 = @{Name = $TestRepoName3; URL = $tmpDir3Path; Trusted = $True; Priority = 20}
        $correctHashtable3 = @{PSGallery = $True; Priority = 30};

        $arrayOfHashtables = $correctHashtable1, $correctHashtable2, $IncorrectHashTable, $correctHashtable3
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -Repositories $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly $ErrorId

        $res = Get-PSResourceRepository -Name $TestRepoName2
        $res.Name | Should -Be $TestRepoName2

        $res2 = Get-PSResourceRepository -Name $TestRepoName3
        $res2.Name | Should -Be $TestRepoName3

        $res3 = Get-PSResourceRepository -Name $PSGalleryName
        $res3.Name | Should -Be $PSGalleryName
        $res3.Priority | Should -Be 30
    }

    It "should register repository with relative location provided as URL" {
        Register-PSResourceRepository -Name $TestRepoName1 -URL "./"
        $res = Get-PSResourceRepository -Name $TestRepoName1

        $res.Name | Should -Be $TestRepoName1
        $res.URL.LocalPath | Should -Contain $relativeCurrentPath
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "should register local file share NuGet based repository" {
        Register-PSResourceRepository -Name "localFileShareTestRepo" -URL "\\hcgg.rest.of.domain.name\test\ITxx\team\NuGet\"
        $res = Get-PSResourceRepository -Name "localFileShareTestRepo"

        $res.Name | Should -Be "localFileShareTestRepo"
        $res.URL.LocalPath | Should -Contain "\\hcgg.rest.of.domain.name\test\ITxx\team\NuGet\"
    }
}
