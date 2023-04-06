# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Get-PSResource for Module' -Tags 'CI' {

    BeforeAll{
        $PSGalleryName = Get-PSGalleryName
        $testModuleName = "testmodule99"
        $testScriptName = "test_script"
        Get-NewPSResourceRepositoryFile
        Set-PSResourceRepository PSGallery -Trusted

        Install-PSResource -Name $testModuleName -Repository $PSGalleryName
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -Version "0.0.1"
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -Version "0.0.2"
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -Version "0.0.3"
        #Install-PSResource -Name $testScriptName -Repository $PSGalleryName
    }

    AfterAll {
        Uninstall-PSResource -Name $testModuleName -Version "*" -ErrorAction SilentlyContinue
        Uninstall-PSResource -Name $testScriptName -Version "*" -ErrorAction SilentlyContinue
        Get-RevertPSResourceRepositoryFile
    }

    It "Get-InstalledModule with MinimumVersion available" {        
        $module = Get-InstalledModule -Name $testModuleName -MinimumVersion "0.0.1"
        $module.Count -ge 1 | Should -Be $true        
    }

    ### Broken
<#
    It "Get-InstalledModule with MinimumVersion not available" {        
        $module = Get-InstalledModule -Name $testModuleName -MinimumVersion "0.0.4"
        $module.Count -eq 0 | Should -Be $true     
    }

    ### broken
    It "Get-InstalledModule with min/max range" {
        $module = Get-InstalledModule -Name $testModuleName -MinimumVersion "0.0.15" -MaximumVersion "0.0.25"
        $module.Version | Should -Be "0.0.2"        
    }

    ### broken
    It "Get-InstalledModule with -RequiredVersion" {
        $version = "0.0.2"
        $module = Get-InstalledModule -Name $testModuleName -RequiredVersion $version
        $module.Version | Should -Be $version    
    }
#>
    It "Get-InstalledModule with Wildcard" {
        $module = Get-InstalledModule -Name "testmodule9*"
        $module.Count -ge 3 | Should -Be $true
    }

}