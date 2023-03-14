# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
Import-Module "$((Get-Item $psscriptroot).parent)\PSGetTestUtils.psm1" -Force

Describe 'Test HTTP Save-PSResource for local repositories' {

    BeforeAll {
        $localRepo = "psgettestlocal"
        $moduleName = "test_local_mod"
        $moduleName2 = "test_local_mod2"
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos

        Get-ModuleResourcePublishedToLocalRepoTestDrive $moduleName $localRepo "1.0.0"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $moduleName $localRepo "3.0.0"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $moduleName $localRepo "5.0.0"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $moduleName2 $localRepo "5.0.0"


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
        Save-PSResource -Name $moduleName -Repository $localRepo -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $moduleName
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem $pkgDir.FullName).Count | Should -Be 1
    }

    It "Save multiple resources by name" {
        $pkgNames = @($moduleName, $moduleName2)
        Save-PSResource -Name $pkgNames -Repository $localRepo -Path $SaveDir -TrustRepository
        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq $moduleName -or $_.Name -eq $moduleName2 }
        $pkgDirs.Count | Should -Be 2
        (Get-ChildItem $pkgDirs[0].FullName).Count | Should -Be 1
        (Get-ChildItem $pkgDirs[1].FullName).Count | Should -Be 1
    }

    It "Should not save resource given nonexistant name" {
        Save-PSResource -Name NonExistentModule -Repository $localRepo -Path $SaveDir -ErrorVariable err -ErrorAction SilentlyContinue -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "NonExistentModule"
        $pkgDir.Name | Should -BeNullOrEmpty
    }

    It "Should save resource given name and exact version" {
        Save-PSResource -Name $moduleName -Version "1.0.0" -Repository $localRepo -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $moduleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.0.0"
    }

    It "Should save resource given name and exact version with bracket syntax" {
        Save-PSResource -Name $moduleName -Version "[1.0.0]" -Repository $localRepo -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $moduleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.0.0"
    }

#    It "Should save resource given name and exact range inclusive [1.0.0, 3.0.0]" {
#        Save-PSResource -Name $moduleName -Version "[1.0.0, 3.0.0]" -Repository $localRepo -Path $SaveDir -TrustRepository
#        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $moduleName
#        $pkgDir | Should -Not -BeNullOrEmpty
#        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
#        $pkgDirVersion.Name | Should -Be "3.0.0"
#    }

    It "Should save resource given name and exact range exclusive (1.0.0, 5.0.0)" {
        Save-PSResource -Name $moduleName -Version "(1.0.0, 5.0.0)" -Repository $localRepo -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $moduleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "3.0.0"
    }

    It "Should not save resource with incorrectly formatted version such as exclusive version (1.0.0.0)" {
        $Version="(1.0.0.0)"
        try {
            Save-PSResource -Name $moduleName -Version $Version -Repository $localRepo -Path $SaveDir -ErrorAction SilentlyContinue -TrustRepository
        }
        catch
        {}

        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $moduleName
        $pkgDir | Should -BeNullOrEmpty
        $Error.Count | Should -Not -Be 0
        $Error[0].FullyQualifiedErrorId  | Should -Be "IncorrectVersionFormat,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }

    It "Save PSResourceInfo object piped in for prerelease version object" {
        Find-PSResource -Name $moduleName -Version "5.0.0" -Repository $localRepo | Save-PSResource -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $moduleName
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem -Path $pkgDir.FullName).Count | Should -Be 1   
    }

    It "Save module as a nupkg" {
        Save-PSResource -Name $moduleName -Version "1.0.0" -Repository $localRepo -Path $SaveDir -AsNupkg -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "test_module.1.0.0.nupkg"
        $pkgDir | Should -Not -BeNullOrEmpty
    }

    It "Save module and include XML metadata file" {
        Save-PSResource -Name $moduleName -Version "1.0.0" -Repository $localRepo -Path $SaveDir -IncludeXml -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $moduleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.0.0"
        $xmlFile = Get-ChildItem -Path $pkgDirVersion.FullName | Where-Object Name -eq "PSGetModuleInfo.xml"
        $xmlFile | Should -Not -BeNullOrEmpty
    }

    It "Save module using -PassThru" {
        $res = Save-PSResource -Name $moduleName -Version "1.0.0" -Repository $localRepo -Path $SaveDir -PassThru -TrustRepository
        $res.Name | Should -Be $moduleName
        $res.Version | Should -Be "1.0.0.0"
    }

    # Save module that is not authenticode signed
    # Should FAIL to save the module
    It "Save module that is not authenticode signed" -Skip:(!(Get-IsWindows)) {
        { Save-PSResource -Name $moduleName -Version "5.0.0" -AuthenticodeCheck -Repository $localRepo -TrustRepository -Path $SaveDir -ErrorAction SilentlyContinue } | Should -Throw -ErrorId "GetAuthenticodeSignatureError,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }
    #>
}