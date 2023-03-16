# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
Import-Module "$((Get-Item $psscriptroot).parent)\PSGetTestUtils.psm1" -Force

Describe 'Test HTTP Save-PSResource for V2 Server Protocol' {

    BeforeAll {
        $PSGalleryName = Get-PSGalleryName
        $testModuleName = "test_module"
        $testScriptName = "test_script"
        $testModuleName2 = "testmodule99"
        $PackageManagement = "PackageManagement"
        Get-NewPSResourceRepositoryFile

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

    It "Save specific module resource by name" {
        Save-PSResource -Name $testModuleName -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem $pkgDir.FullName).Count | Should -Be 1
    }

    It "Save specific script resource by name" {
        Save-PSResource -Name $testScriptName -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "test_script.ps1"
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem $pkgDir.FullName).Count | Should -Be 1
    }

    It "Save multiple resources by name" {
        $pkgNames = @($testModuleName, $testModuleName2)
        Save-PSResource -Name $pkgNames -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq $testModuleName -or $_.Name -eq $testModuleName2 }
        $pkgDirs.Count | Should -Be 2
        (Get-ChildItem $pkgDirs[0].FullName).Count | Should -Be 1
        (Get-ChildItem $pkgDirs[1].FullName).Count | Should -Be 1
    }

    It "Should not save resource given nonexistant name" {
        Save-PSResource -Name NonExistentModule -Repository $PSGalleryName -Path $SaveDir -ErrorVariable err -ErrorAction SilentlyContinue -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "NonExistentModule"
        $pkgDir.Name | Should -BeNullOrEmpty
    }

    It "Not Save module with Name containing wildcard" {
        Save-PSResource -Name "TestModule*" -Repository $PSGalleryName -Path $SaveDir -ErrorVariable err -ErrorAction SilentlyContinue -TrustRepository
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "NameContainsWildcard,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }

    It "Should save resource given name and exact version" {
        Save-PSResource -Name $testModuleName -Version "1.0.0" -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.0.0.0"
    }

    It "Should save resource given name and exact version with bracket syntax" {
        Save-PSResource -Name $testModuleName -Version "[1.0.0]" -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.0.0.0"
    }

    It "Should save resource given name and exact range inclusive [1.0.0, 3.0.0]" {
        Save-PSResource -Name $testModuleName -Version "[1.0.0, 3.0.0]" -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "3.0.0.0"
    }

    It "Should save resource given name and exact range exclusive (1.0.0, 5.0.0)" {
        Save-PSResource -Name $testModuleName -Version "(1.0.0, 5.0.0)" -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "3.0.0.0"
    }

    It "Should not save resource with incorrectly formatted version such as exclusive version (1.0.0.0)" {
        $Version="(1.0.0.0)"
        try {
            Save-PSResource -Name $testModuleName -Version $Version -Repository $PSGalleryName -Path $SaveDir -ErrorAction SilentlyContinue -TrustRepository
        }
        catch
        {}

        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -BeNullOrEmpty
        $Error.Count | Should -Not -Be 0
        $Error[0].FullyQualifiedErrorId  | Should -Be "IncorrectVersionFormat,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }

    It "Save resource with latest (including prerelease) version given Prerelease parameter" {
        Save-PSResource -Name $testModuleName -Prerelease -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "5.2.5"
    }

   It "Save a module with a dependency" {
        Save-PSResource -Name "TestModuleWithDependencyE" -Version "1.0.0.0" -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq "TestModuleWithDependencyE" -or $_.Name -eq "TestModuleWithDependencyC" -or $_.Name -eq "TestModuleWithDependencyB" -or $_.Name -eq "TestModuleWithDependencyD"}
        $pkgDirs.Count | Should -BeGreaterThan 1
        (Get-ChildItem $pkgDirs[0].FullName).Count | Should -BeGreaterThan 0
        (Get-ChildItem $pkgDirs[1].FullName).Count | Should -BeGreaterThan 0
        (Get-ChildItem $pkgDirs[2].FullName).Count | Should -BeGreaterThan 0
        (Get-ChildItem $pkgDirs[3].FullName).Count | Should -BeGreaterThan 0
    }

    It "Save a module with a dependency and skip saving the dependency" {
        Save-PSResource -Name "TestModuleWithDependencyE" -Version "1.0.0.0" -SkipDependencyCheck -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq "TestModuleWithDependencyE"}
        $pkgDirs.Count | Should -Be 1
        (Get-ChildItem $pkgDirs[0].FullName).Count | Should -Be 1
    }

    It "Save PSResourceInfo object piped in for prerelease version object" {
        Find-PSResource -Name $testModuleName -Version "5.2.5-alpha001" -Repository $PSGalleryName | Save-PSResource -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem -Path $pkgDir.FullName).Count | Should -Be 1   
    }

    It "Save module as a nupkg" {
        Save-PSResource -Name $testModuleName -Version "1.0.0" -Repository $PSGalleryName -Path $SaveDir -AsNupkg -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "test_module.1.0.0.nupkg"
        $pkgDir | Should -Not -BeNullOrEmpty
    }

    It "Save module and include XML metadata file" {
        Save-PSResource -Name $testModuleName -Version "1.0.0" -Repository $PSGalleryName -Path $SaveDir -IncludeXml -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.0.0.0"
        $xmlFile = Get-ChildItem -Path $pkgDirVersion.FullName | Where-Object Name -eq "PSGetModuleInfo.xml"
        $xmlFile | Should -Not -BeNullOrEmpty
    }

    It "Save module using -PassThru" {
        $res = Save-PSResource -Name $testModuleName -Version "1.0.0" -Repository $PSGalleryName -Path $SaveDir -PassThru -TrustRepository
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "1.0.0.0"
    }

    # Save module that is not authenticode signed
    # Should FAIL to save the module
    It "Save module that is not authenticode signed" -Skip:(!(Get-IsWindows)) {
        Save-PSResource -Name $testModuleName -Version "5.0.0" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository -Path $SaveDir -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "InstallPackageFailure,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }
}