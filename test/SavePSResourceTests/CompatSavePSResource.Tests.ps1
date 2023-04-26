# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose
# Explicitly import build module because in CI PowerShell can autoload PSGetv2
# This ensures the build module is always being tested
$buildModule = "$psscriptroot/../../out/PowerShellGet"
Import-Module $buildModule -Force -Verbose

Describe 'Test CompatPowerShellGet: Save-PSResource' -tags 'CI' {

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

    It "Save-Module with Find-DscResource output" {
        $DscResourceName = 'SystemLocale'
        $ModuleName = 'SystemLocaleDsc'
        $res1 = Find-DscResource -Name $DscResourceName
        $res1 | Should -Not -BeNullOrEmpty

        Find-DscResource -Name $DscResourceName | Save-Module -Path $SaveDir

        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq $ModuleName }
        $pkgDirs.Count | Should -Be 1
    }

    It "Save-Module with Find-Command output" {
        $cmdName = "Get-WUJob"
        $ModuleName = "PSWindowsUpdate"
        $res1 = Find-Command -Name $cmdName
        $res1 | Should -Not -BeNullOrEmpty

        Find-Command -Name $cmdName | Save-Module -Path $SaveDir

        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq $ModuleName }
        $pkgDirs.Count | Should -Be 1
    }

    It "Save-Module with dependencies" {
        $ModuleName = "test_module"
        $dependency1 = "RequiredModule1"
        $dependency2 = "RequiredModule2"
        $dependency3 = "RequiredModule3"
        $dependency4 = "RequiredModule4"
        $dependency5 = "RequiredModule5"

        Save-Module -Name $ModuleName -Repository PSGallery -Path $SaveDir

        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq $ModuleName -or $dependency1 -or $dependency2 -or $dependency3 -or $dependency4 -or $dependency5 }
        $pkgDirs.Count | Should -BeGreaterOrEqual 6  
    }

    It "Save-Module with Find-Module output" {
        Find-PSResource -Name $testModuleName2 -Repository PSGallery | Save-Module -Path $SaveDir

        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq $testModuleName2 }
        $pkgDirs.Count | Should -Be 1
    }

    It "Save specific module resource by name" {
        Save-Module -Name $testModuleName2 -Repository $PSGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName2
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem $pkgDir.FullName).Count | Should -Be 1
    }

    It "Save specific script resource by name" {
        Save-Script -Name $testScriptName -Repository $PSGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "$testScriptName.ps1"
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem $pkgDir.FullName).Count | Should -Be 1
    }

    It "Save multiple resources by name" {
        $pkgNames = @($testModuleName, $testModuleName2)
        Save-Module -Name $pkgNames -Repository $PSGalleryName -Path $SaveDir
        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq $testModuleName -or $_.Name -eq $testModuleName2 }
        $pkgDirs.Count | Should -Be 2
        (Get-ChildItem $pkgDirs[0].FullName).Count | Should -Be 1
        (Get-ChildItem $pkgDirs[1].FullName).Count | Should -Be 1
    }

    It "Should not save resource given nonexistant name" {
        Save-Module -Name NonExistentModule -Repository $PSGalleryName -Path $SaveDir -ErrorVariable err -ErrorAction SilentlyContinue
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "NonExistentModule"
        $pkgDir.Name | Should -BeNullOrEmpty
    }

    It "Not Save module with Name containing wildcard" {
        Save-Module -Name "TestModule*" -Repository $PSGalleryName -Path $SaveDir -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "NameContainsWildcard,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }

    It "Should save resource given name and exact version" {
        Save-Module -Name $testModuleName2 -RequiredVersion "0.0.2" -Repository $PSGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName2
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "0.0.2"
    }

    It "Save resource with latest (including prerelease) version given Prerelease parameter" {
        Save-Module -Name $testModuleName2 -AllowPrerelease -Repository $PSGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName2
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.0.0"
    }

   It "Save a module with a dependency" {
        Save-Module -Name "TestModuleWithDependencyE" -RequiredVersion "1.0.0.0" -Repository $PSGalleryName -Path $SaveDir
        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq "TestModuleWithDependencyE" -or $_.Name -eq "TestModuleWithDependencyC" -or $_.Name -eq "TestModuleWithDependencyB" -or $_.Name -eq "TestModuleWithDependencyD"}
        $pkgDirs.Count | Should -BeGreaterThan 1
        (Get-ChildItem $pkgDirs[0].FullName).Count | Should -BeGreaterThan 0
        (Get-ChildItem $pkgDirs[1].FullName).Count | Should -BeGreaterThan 0
        (Get-ChildItem $pkgDirs[2].FullName).Count | Should -BeGreaterThan 0
        (Get-ChildItem $pkgDirs[3].FullName).Count | Should -BeGreaterThan 0
    }

    It "Save a module with a dependency and skip saving the dependency" {
        Save-Module -Name "TestModuleWithDependencyE" -RequiredVersion "1.0.0.0" -Repository $PSGalleryName -Path $SaveDir
        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq "TestModuleWithDependencyE"}
        $pkgDirs.Count | Should -Be 1
        (Get-ChildItem $pkgDirs[0].FullName).Count | Should -Be 1
    }

    It "Save PSResourceInfo object piped in for prerelease version object" -Pending {
        Find-PSResource -Name $testModuleName -Version "5.2.5-alpha001" -Repository $PSGalleryName | Save-Module -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem -Path $pkgDir.FullName).Count | Should -Be 1   
    }
}
