# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test Register-PSResourceRepository" {
    BeforeEach {
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
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
        $res.URL | Should -Be "https://www.powershellgallery.com/api/v2"
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "register repository with PSGallery, Trusted parameters (PSGalleryParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $res = Register-PSResourceRepository -PSGallery -Trusted -PassThru
        $res.Name | Should -Be $PSGalleryName
        $res.URL | Should -Be "https://www.powershellgallery.com/api/v2"
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 50
    }

    It "register repository with PSGallery, Trusted, Priority parameters (PSGalleryParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $res = Register-PSResourceRepository -PSGallery -Trusted -Priority 20 -PassThru
        $res.Name | Should -Be $PSGalleryName
        $res.URL | Should -Be "https://www.powershellgallery.com/api/v2"
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 20
    }

    It "register repositories with Repositories parameter, all name parameter style repositories (RepositoriesParameterSet)" {
        $hashtable1 = @{Name = "testRepository"; URL = $tmpDir1Path}
        $hashtable2 = @{Name = "testRepository2"; URL = $tmpDir2Path; Trusted = $True}
        $hashtable3 = @{Name = "testRepository3"; URL = $tmpDir3Path; Trusted = $True; Priority = 20}
        $listOfHashtables = $hashtable1, $hashtable2, $hashtable3


        Register-PSResourceRepository -Repositories $listOfHashtables
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
        $res.URL | Should -Be "https://www.powershellgallery.com/api/v2"
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "register repositories with Repositories parameter, name and psgallery parameter styles (RepositoriesParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $hashtable1 = @{PSGallery = $True}
        $hashtable2 = @{Name = "testRepository"; URL = $tmpDir1Path}
        $hashtable3 = @{Name = "testRepository2"; URL = $tmpDir2Path; Trusted = $True}
        $hashtable4 = @{Name = "testRepository3"; URL = $tmpDir3Path; Trusted = $True; Priority = 20}
        $listOfHashtables = $hashtable1, $hashtable2, $hashtable3, $hashtable4

        Register-PSResourceRepository -Repositories $listOfHashtables

        $res1 = Get-PSResourceRepository -Name $PSGalleryName
        $res1.URL | Should -Be "https://www.powershellgallery.com/api/v2"
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
}
