# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
Import-Module "$((Get-Item $psscriptroot).parent)\PSGetTestUtils.psm1" -Force

Describe 'Test HTTP Save-PSResource for V2 Server Protocol' -tags 'CI' {

    BeforeAll {
        $PSGalleryName = Get-PSGalleryName
        $testModuleName = "test_module"
        $testScriptName = "test_script"
        $testModuleName2 = "testmodule99"
        $PackageManagement = "PackageManagement"
        Get-NewPSResourceRepositoryFile
        Set-PSResourceRepository -Name PSGallery -Trusted

        $SaveDir = Join-Path $TestDrive 'SavedResources'
        New-Item -Item Directory $SaveDir -Force
    }

    AfterEach {
        # Delete contents of save directory
        Remove-Item -Path (Join-Path $SaveDir '*') -Recurse -Force -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    ### BROKEN
<#
    It "Save-Module with Find-DscResource output" {
        $DscResourceName = 'SystemLocale'
        $ModuleName = 'SystemLocaleDsc'
        $res1 = Find-DscResource -Name $DscResourceName
        $res1 | Should -Not -BeNullOrEmpty

        Find-DscResource -Name $DscResourceName | Save-Module -Path $SaveDir

        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq $ModuleName }
        $pkgDirs.Count | Should -Be 1
    }
#>

### BROKEN
<#
    It "Save-Module with Find-Command output" {
        $cmdName = "Get-WUJob"
        $ModuleName = "PSWindowsUpdate"
        $res1 = Find-Command -Name $cmdName
        $res1 | Should -Not -BeNullOrEmpty

        Find-DscResource -Name $cmdName | Save-Module -Path $SaveDir

        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq $ModuleName }
        $pkgDirs.Count | Should -Be 1
    }
#>

    It "Save-Module with dependencies" {
        $ModuleName = "test_module"
        $dependency1 = "RequiredModule1"
        $dependency2 = "RequiredModule2"
        $dependency3 = "RequiredModule3"
        $dependency4 = "RequiredModule4"
        $dependency5 = "RequiredModule5"

        Save-Module -Name $ModuleName -Repository PSGallery -Path $SaveDir

        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq $ModuleName -or $dependency1 -or $dependency2 -or $dependency3 -or $dependency4 -or $dependency5 }
        $pkgDirs.Count -ge 6 | Should -Be $true
    }

    It "Save-Module with Find-Module output" {
        $ModuleName = "testmodule99"

        Find-Module -Name $ModuleName -Repository PSGallery | Save-Module -Path $SaveDir -Repository PSGallery

        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq $ModuleName }
        $pkgDirs.Count | Should -Be 1
    }
}


