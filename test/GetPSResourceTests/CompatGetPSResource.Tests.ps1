# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose
# Explicitly import build module because in CI PowerShell can autoload PSGetv2
# This ensures the build module is always being tested
$buildModule = "$psscriptroot/../../out/PowerShellGet"
Import-Module $buildModule -Force -Verbose

Describe 'Test CompatPowerShellGet: Get-PSResource' -tags 'CI' {

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
        Install-PSResource -Name $testScriptName -Repository $PSGalleryName -SkipDependencyCheck
    }

    AfterAll {
        Uninstall-PSResource -Name $testModuleName -Version "*" -ErrorAction SilentlyContinue
        Uninstall-PSResource -Name $testScriptName -Version "*" -ErrorAction SilentlyContinue
        Get-RevertPSResourceRepositoryFile
    }

    It "Get-InstalledModule with MinimumVersion available" {        
        $res = Get-InstalledModule -Name $testModuleName -MinimumVersion "0.0.1"
        $res.Count | Should -BeGreaterThan 1   
        foreach ($pkg in $res)
        {
            $pkg.Version | Should -BeGreaterOrEqual [System.Version] "0.0.1"
        }
    }

    It "Get-InstalledScript with MinimumVersion available" {        
        $res = Get-InstalledScript -Name $testScriptName -MinimumVersion "1.0.0"
        $res.Count | Should -BeGreaterOrEqual 1     
        foreach ($pkg in $res)
        {
            $pkg.Version | Should -BeGreaterThanEqual [System.Version] "1.0.0"
        }    
    }

    It "Get-InstalledModule with MinimumVersion not available" {        
        $res = Get-InstalledModule -Name $testModuleName -MinimumVersion "1.0.0"
        $res.Count | Should -BeExactly 0    
    }

    It "Get-InstalledModule with min/max range" {
        $res = Get-InstalledModule -Name $testModuleName -MinimumVersion "0.0.15" -MaximumVersion "0.0.25" 
        foreach ($pkg in $res)
        {
            $pkg.Version | Should -BeGreaterOrEqual [System.Version] "0.0.2"
        }       
    }

    It "Get-InstalledModule with -RequiredVersion" {
        $version = "0.0.2"
        $res = Get-InstalledModule -Name $testModuleName -RequiredVersion $version
        $res.Version | Should -Be $version    
    }
    
    It "Get prerelease version module when version with correct prerelease label is specified" {
        Install-PSResource -Name $testModuleName -Version "1.0.0-beta2" -Repository $PSGalleryName
        $res = Get-PSResource -Name $testModuleName -Version "1.0.0"
        $res | Should -BeNullOrEmpty
        $res = Get-PSResource -Name $testModuleName -Version "1.0.0-beta2"
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "1.0.0"
        $res.Prerelease | Should -Be "beta2"
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

    It "Get-InstalledModule with Wildcard" {
        $module = Get-InstalledModule -Name "testmodule9*"
        $module.Count | Should -BeGreaterOrEqual 3   
    }

    It "Get-InstalledModule with Wildcard" {
        $module = Get-InstalledScript -Name "test_scri*"
        $module.Count | Should -BeGreaterOrEqual 1
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

    It "Get resource when given Name to " {
        $pkgs = Get-InstalledModule -Name "*estmodul*"
        $pkgs.Name | Should -Contain $testModuleName
    }

    It "Get resource when given Name to <Reason>" -TestCases @(
        @{Name="*estmodul*";    Reason="validate name, with wildcard at beginning and end of name: *estmodul*"},
        @{Name="testmod*";      Reason="validate name, with wildcard at end of name: testmod*"},
        @{Name="*estmodule99";  Reason="validate name, with wildcard at beginning of name: *estmodule99"},
        @{Name="tes*ule99";     Reason="validate name, with wildcard in middle of name: tes*ule99"}
    ) {
        param($Name)
        $pkgs = Get-InstalledModule -Name $Name
        $pkgs.Name | Should -Contain $testModuleName
    }
}