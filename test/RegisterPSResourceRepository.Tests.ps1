# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test Register-PSResourceRepository" {
    BeforeEach {
        $PSGalleryName = Get-PSGalleryName
        $PSGalleryURL = Get-PSGalleryLocation
        Get-NewPSResourceRepositoryFile
        $tmpDir1Path = Join-Path -Path (Get-TempPath) -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path (Get-TempPath) -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path (Get-TempPath) -ChildPath "tmpDir3"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path)
        Create-TempDirs($tmpDirPaths)
    }
    AfterEach {
        Get-RevertPSResourceRepositoryFile
        $tmpDir1Path = Join-Path -Path (Get-TempPath) -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path (Get-TempPath) -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path (Get-TempPath) -ChildPath "tmpDir3"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path)
        Remove-TempDirs($tmpDirPaths)
    }

    It "register repository given Name, URL (bare minimum for NameParmaterSet)" {
        $res = Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path -PassThru
        $res.Name | Should -Be "testRepository"
        $res.URL | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "register repository with Name, URL, Trusted (NameParameterSet)" {
        $res = Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path -Trusted -PassThru
        $res.Name | Should -Be "testRepository"
        $res.URL | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 50
    }

    It "register repository given Name, URL, Trusted, Priority (NameParameterSet)" {
        $res = Register-PSResourceRepository -Name "testRepository" -URL $tmpDir1Path -Trusted -Priority 20 -PassThru
        $res.Name | Should -Be "testRepository"
        $res.URL | Should -Contain $tmpDir1Path
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
        $hashtable1 = @{Name = "testRepository"; URL = $tmpDir1Path}
        $hashtable2 = @{Name = "testRepository2"; URL = $tmpDir2Path; Trusted = $True}
        $hashtable3 = @{Name = "testRepository3"; URL = $tmpDir3Path; Trusted = $True; Priority = 20}
        $arrayOfHashtables = $hashtable1, $hashtable2, $hashtable3

        Register-PSResourceRepository -Repositories $arrayOfHashtables
        $res = Get-PSResourceRepository -Name "testRepository"
        $res.URL | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50

        $res2 = Get-PSResourceRepository -Name "testRepository2"
        $res2.URL | Should -Contain $tmpDir2Path
        $res2.Trusted | Should -Be True
        $res2.Priority | Should -Be 50

        $res3 = Get-PSResourceRepository -Name "testRepository3"
        $res3.URL | Should -Contain $tmpDir3Path
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
        $hashtable2 = @{Name = "testRepository"; URL = $tmpDir1Path}
        $hashtable3 = @{Name = "testRepository2"; URL = $tmpDir2Path; Trusted = $True}
        $hashtable4 = @{Name = "testRepository3"; URL = $tmpDir3Path; Trusted = $True; Priority = 20}
        $arrayOfHashtables = $hashtable1, $hashtable2, $hashtable3, $hashtable4

        Register-PSResourceRepository -Repositories $arrayOfHashtables

        $res1 = Get-PSResourceRepository -Name $PSGalleryName
        $res1.URL | Should -Be $PSGalleryURL
        $res1.Trusted | Should -Be False
        $res1.Priority | Should -Be 50

        $res2 = Get-PSResourceRepository -Name "testRepository"
        $res2.URL | Should -Contain $tmpDir1Path
        $res2.Trusted | Should -Be False
        $res2.Priority | Should -Be 50

        $res3 = Get-PSResourceRepository -Name "testRepository2"
        $res3.URL | Should -Contain $tmpDir2Path
        $res3.Trusted | Should -Be True
        $res3.Priority | Should -Be 50

        $res4 = Get-PSResourceRepository -Name "testRepository3"
        $res4.URL | Should -Contain $tmpDir3Path
        $res4.Trusted | Should -Be True
        $res4.Priority | Should -Be 20
    }

    It "not register repository when Name is provided but URL is not" {
        {Register-PSResourceRepository -Name "testRepository" -URL "" -ErrorAction Stop} | Should -Throw "The URL provided is not valid: "
    }

    It "not register repository when Name null but URL is provided" {
        {Register-PSResourceRepository -Name "" -URL $tmpDir1Path -ErrorAction Stop} | Should -Throw "Cannot validate argument on parameter 'Name'. The argument is null or empty. Provide an argument that is not null or empty, and then try the command again."
    }

    It "not register PSGallery with NameParameterSet" {
        $errorMsg = "Cannot register PSGallery with -Name parameter. Try: Register-PSResourceRepository -PSGallery"
        {Register-PSResourceRepository -Name $PSGalleryName -URL $PSGalleryURL -ErrorAction Stop} | Should -Throw $errorMsg
    }

    # this error message comes from the parameter cmdlet tags (earliest point of detection)
    It "not register PSGallery when PSGallery parameter provided with Name or URL" {
        $errorMsg = "Parameter set cannot be resolved using the specified named parameters. One or more parameters issued cannot be used together or an insufficient number of parameters were provided."
        {Register-PSResourceRepository -PSGallery -Name $PSGalleryName -ErrorAction Stop} | Should -Throw $errorMsg
        {Register-PSResourceRepository -PSGallery -URL $PSGalleryURL -ErrorAction Stop} | Should -Throw $errorMsg
    }

    It "not register incorrectly formatted PSGallery type repo among correct ones when incorrect type is <Type>" -TestCases @(
        @{Type = "Name key specified with PSGallery key"; IncorrectHashTable = @{PSGallery = $True; Name=$PSGalleryName}; ErrorMsg = "Repository hashtable cannot contain PSGallery key with -Name and/or -URL key value pairs"},
        @{Type = "URL key specified with PSGallery key";  IncorrectHashTable = @{PSGallery = $True; URL=$PSGalleryURL};   ErrorMsg = "Repository hashtable cannot contain PSGallery key with -Name and/or -URL key value pairs"}
    ){
        $correctHashtable1 = @{Name = "testRepository"; URL = $tmpDir1Path}
        $correctHashtable2 = @{Name = "testRepository2"; URL = $tmpDir2Path; Trusted = $True}
        $correctHashtable3 = @{Name = "testRepository3"; URL = $tmpDir3Path; Trusted = $True; Priority = 20}
        $arrayOfHashtables = $correctHashtable1, $correctHashtable2, $IncorrectHashTable, $correctHashtable3

        Unregister-PSResourceRepository -Name "PSGallery"
        Register-PSResourceRepository -Repositories $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].Exception.Message | Should -Be $ErrorMsg

        $res = Get-PSResourceRepository -Name "testRepository"
        $res.Name | Should -Be "testRepository"

        $res2 = Get-PSResourceRepository -Name "testRepository2"
        $res2.Name | Should -Be "testRepository2"

        $res3 = Get-PSResourceRepository -Name "testRepository3"
        $res3.Name | Should -Be "testRepository3"
    }

    It "not register incorrectly formatted Name type repo among correct ones when incorrect type is <Type>" -TestCases @(
        @{Type = "-Name is not specified";                 IncorrectHashTable = @{URL = $tmpDir1Path};                             ErrorMsg = "Repository name cannot ne null"},
        @{Type = "-Name is PSGallery";                     IncorrectHashTable = @{Name = "PSGallery"; URL = $tmpDir1Path};         ErrorMsg = "Cannot register PSGallery with -Name parameter. Try: Register-PSResourceRepository -PSGallery"},
        @{Type = "-URL not specified";                     IncorrectHashTable = @{Name = "testRepository"};                        ErrorMsg = "Repository url cannot ne null"},
        @{Type = "-URL is not valid scheme";               IncorrectHashTable = @{Name = "testRepository"; URL="www.google.com"};  ErrorMsg = "Invalid url, unable to create"}
    ){

        $correctHashtable1 = @{Name = "testRepository2"; URL = $tmpDir2Path; Trusted = $True}
        $correctHashtable2 = @{Name = "testRepository3"; URL = $tmpDir3Path; Trusted = $True; Priority = 20}
        $correctHashtable3 = @{PSGallery = $True; Priority = 30};

        $arrayOfHashtables = $correctHashtable1, $correctHashtable2, $IncorrectHashTable, $correctHashtable3
        Unregister-PSResourceRepository -Name "PSGallery"
        Register-PSResourceRepository -Repositories $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].Exception.Message | Should -Be $ErrorMsg

        $res = Get-PSResourceRepository -Name "testRepository2"
        $res.Name | Should -Be "testRepository2"

        $res2 = Get-PSResourceRepository -Name "testRepository3"
        $res2.Name | Should -Be "testRepository3"

        $res3 = Get-PSResourceRepository -Name "PSGallery"
        $res3.Name | Should -Be "PSGallery"
        $res3.Priority | Should -Be 30
    }
}
