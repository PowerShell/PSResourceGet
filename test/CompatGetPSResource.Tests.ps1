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
        Install-PSResource -Name $testScriptName -Repository $PSGalleryName
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

    It "Get-InstalledScript with MinimumVersion available" {        
        $module = Get-InstalledScript -Name $testModuleName -MinimumVersion "1.0.0"
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
    
    It "Get prerelease version module when version with correct prerelease label is specified" {
        Install-PSResource -Name $testModuleName -Version "5.2.5-alpha001" -Repository $PSGalleryName -TrustRepository
        $res = Get-PSResource -Name $testModuleName -Version "5.2.5"
        $res | Should -BeNullOrEmpty
        $res = Get-PSResource -Name $testModuleName -Version "5.2.5-alpha001"
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.2.5"
        $res.Prerelease | Should -Be "alpha001"
    }

    It "Get prerelease version script when version with correct prerelease label is specified" {
        Install-PSResource -Name $testScriptName -Version "3.0.0-alpha" -Repository $PSGalleryName -TrustRepository
        $res = Get-PSResource -Name $testScriptName -Version "3.0.0"
        $res | Should -BeNullOrEmpty
        $res = Get-PSResource -Name $testScriptName -Version "3.0.0-alpha"
        $res.Name | Should -Be $testScriptName
        $res.Version | Should -Be "3.0.0"
        $res.Prerelease | Should -Be "alpha"
    }


#>

    It "Get-InstalledModule with Wildcard" {
        $module = Get-InstalledModule -Name "testmodule9*"
        $module.Count -ge 3 | Should -Be $true
    }

    It "Get-InstalledModule with Wildcard" {
        $module = Get-InstalledScript -Name "test_scri*"
        $module.Count -ge 1 | Should -Be $true
    }
    
    It "Get modules without any parameter values" {
        $pkgs = Get-InstalledScript
        $pkgs.Count | Should -BeGreaterThan 1
    }

    It "Get scripts without any parameter values" {
        $pkgs = Get-InstalledModule
        $pkgs.Count | Should -BeGreaterThan 1
    }

    It "Get specific module resource by name" {
        $pkg = Get-InstalledModule -Name $testModuleName
        $pkg.Name | Should -Contain $testModuleName
    }

    It "Get specific script resource by name" {
        $pkg = Get-InstalledScript -Name $testScriptName
        $pkg.Name | Should -Be $testScriptName
    }
<#
    # BROKEN - compat issue 
    It "Get resource when given Name to <Reason> <Version>" -TestCases @(
        @{Name="*est_modul*";    ExpectedName=$testModuleName; Reason="validate name, with wildcard at beginning and end of name: *est_modul*"}
      #  @{Name="test_mod*";      ExpectedName=$testModuleName; Reason="validate name, with wildcard at end of name: test_mod*"},
      #  @{Name="*est_module";    ExpectedName=$testModuleName; Reason="validate name, with wildcard at beginning of name: *est_module"},
      #  @{Name="tes*ule";        ExpectedName=$testModuleName; Reason="validate name, with wildcard in middle of name: tes*ule"}
    ) {
        param($Version, $ExpectedVersion)
        $pkgs = Get-InstalledModule -Name $Name
        $pkgs.Name | Should -Contain $testModuleName
    }
#>
}