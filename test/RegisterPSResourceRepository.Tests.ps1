# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test Register-PSResourceRepository" {
    BeforeEach {
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        Get-NewPSResourceRepositoryFile
    }
    AfterEach {
        Get-RevertPSResourceRepositoryFile
    }

    # also have tests for errors categories

    It "register repository given Name, URL (bare minimum for NameParmaterSet" {
        Register-PSResourceRepository -Name "testRepository" -URL Get-CurrentUserDocumentsPath
        $res = Get-PSResourceRepository -Name "testRepository"
        $res.Name | Should -Be "testRepository"
    }

    It "register repository with Name, URL, Trusted (NameParameterSet)" {
        Register-PSResourceRepository -Name "testRepository" -URL Get-CurrentUserDocumentsPath -Trusted
        $res = Get-PSResourceRepository -Name "testRepository"
        $res.Name | Should -Be "testRepository"
        $res.Trusted | Should -Be True
    }

    It "register repository given Name, URL, Trusted, Priority (NameParameterSet)" {
        Register-PSResourceRepository -Name "testRepository" -URL Get-CurrentUserDocumentsPath -Trusted -Priority 20
        $res = Get-PSResourceRepository -Name "testRepository"
        $res.Name | Should -Be "testRepository"
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 20
    }

    It "register repository with PSGallery parameter (PSGalleryParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -PSGallery
        $res = Get-PSResourceRepository -Name $PSGalleryName
        $res.Name | Should -Be $PSGalleryName
        $res.URL | Should -Be "https://www.powershellgallery.com/api/v2"
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "register repository with PSGallery, Trusted parameters (PSGalleryParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -PSGallery -Trusted
        $res = Get-PSResourceRepository -Name $PSGalleryName
        $res.Name | Should -Be $PSGalleryName
        $res.URL | Should -Be "https://www.powershellgallery.com/api/v2"
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 50
    }

    It "register repository with PSGallery, Trusted, Priority parameters (PSGalleryParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -PSGallery -Trusted -Priority 20
        $res = Get-PSResourceRepository -Name $PSGalleryName
        $res.Name | Should -Be $PSGalleryName
        $res.URL | Should -Be "https://www.powershellgallery.com/api/v2"
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 20
    }

    It "register repositories with Repositories parameter, all name parameter style repositories (RepositoriesParameterSet)" {
        $hashtable1 = @{Name = "testRepository"; URL = Get-CurrentUserDocumentsPath}
        $hashtable2 = @{Name = "testRepository2"; URL = Get-TempPath; Trusted = $True}
        $hashtable3 = @{Name = "testRepository3"; URL = "https://www.google.com/search"; Trusted = $True; Priority = 20}
        $listOfHashtables = $hashtable1, $hashtable2, $hashtable3
        Register-PSResourceRepository -Repositories $listOfHashtables
        $res = Get-PSResourceRepository -Name "testRepository"
        # $res.URL | Should -Be (Get-CurrentUserDocumentsPath)
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50

        $res2 = Get-PSResourceRepository -Name "testRepository2"
        # $res2.URL | Should -Be (Get-TempPath)
        $res2.Trusted | Should -Be True
        $res2.Priority | Should -Be 50

        $res3 = Get-PSResourceRepository -Name "testRepository3"
        $res3.Trusted | Should -Be True
        $res3.Priority | Should -Be 20
    }

    It "register repositories with Repositories parameter, psgallery style repository (RepositoriesParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $hashtable1 = @{PSGallery = $True}
        Register-PSResourceRepository -Repositories $hashtable1
        $res = Get-PSResourceRepository -Name $PSGalleryName
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "register repositories with Repositories parameter, name and psgallery parameter styles (RepositoriesParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $hashtable1 = @{PSGallery = $True}
        $hashtable2 = @{Name = "testRepository"; URL = Get-CurrentUserDocumentsPath}
        $hashtable3 = @{Name = "testRepository2"; URL = Get-TempPath; Trusted = $True}
        $hashtable4 = @{Name = "testRepository3"; URL = "https://www.google.com/search"; Trusted = $True; Priority = 20}
        $listOfHashtables = $hashtable1, $hashtable2, $hashtable3, $hashtable4


        Register-PSResourceRepository -Repositories $listOfHashtables

        $res1 = Get-PSResourceRepository -Name $PSGalleryName
        $res1.Trusted | Should -Be False
        $res1.Priority | Should -Be 50

        $res2 = Get-PSResourceRepository -Name "testRepository"
        # $res.URL | Should -Be (Get-CurrentUserDocumentsPath)
        $res2.Trusted | Should -Be False
        $res2.Priority | Should -Be 50

        $res3 = Get-PSResourceRepository -Name "testRepository2"
        # $res2.URL | Should -Be (Get-TempPath)
        $res3.Trusted | Should -Be True
        $res3.Priority | Should -Be 50

        $res4 = Get-PSResourceRepository -Name "testRepository3"
        $res4.Trusted | Should -Be True
        $res4.Priority | Should -Be 20
    }

    # add tests with bad URIs. Do I need code in parameter set/get for URI?
    # add test with PassThru cmdlet
}