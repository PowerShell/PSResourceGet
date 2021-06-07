# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

<#// Temporarily comment out tests until Install-PSResource is complete
Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Uninstall-PSResource for Modules' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        Get-NewPSResourceRepositoryFile
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Uninstall a specific module by name" {
        $pkg = Uninstall-PSResource -name Bicep 
    }

    It "Uninstall a list of modules by name" {
        $pkg = Uninstall-PSResource -Name BaseTestPackage, bicep 
    }

    It "Uninstall a module when given name and specifying all versions" {
        $res = Uninstall-PSResource -Name "Carbon" -version "*"
        $res | Should -BeNullOrEmpty
    }

    It "Uninstall module when given Name and specifying exact version" {
        $res = Uninstall-PSResource -Name "ContosoServer" -Version "1.0.0"
        $res | Should -BeNullOrEmpty
    }

    It "Uninstall module when given Name to <Reason> <Version>" -TestCases @(
        @{Version="[2.0.0.0]";          ExpectedVersion="2.0.0.0"; Reason="validate version, exact match"},
        @{Version="2.0.0.0";            ExpectedVersion="2.0.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.0.0.0, 2.5.0.0]"; ExpectedVersion="2.5.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="(1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="[1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(,1.5.0.0)";         ExpectedVersion="1.0.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,1.5.0.0]";         ExpectedVersion="1.5.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Uninstall-PSResource -Name "Pester" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "Pester"
        $res.Version | Should -Be $ExpectedVersion
    }

    It "Do not uninstall module with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(1.5.0.0)';       Description="exlcusive version (8.1.0.0)"},
        @{Version='[1-5-0-0]';       Description="version formatted with invalid delimiter"},
        @{Version='[1.*.0]';         Description="version with wilcard in middle"},
        @{Version='[*.5.0.0]';       Description="version with wilcard at start"},
        @{Version='[1.*.0.0]';       Description="version with wildcard at second digit"},
        @{Version='[1.5.*.0]';       Description="version with wildcard at third digit"}
        @{Version='[1.5.0.*]';        Description="version with wildcard at end"},
        @{Version='[1..0.0]';        Description="version with missing digit in middle"},
        @{Version='[1.5.0.]';        Description="version with missing digit at end"},
        @{Version='[1.5.0.0.0]';     Description="version with more than 4 digits"}
    ) {
        param($Version, $Description)

        $res = $null
        try {
            $res = Uninstall-PSResource -Name "ContosoServer" -Version $Version -Repository $TestGalleryName -ErrorAction Ignore
        }
        catch {}
        
        $res | Should -BeNullOrEmpty
    }

    It "Does not uninstall when given Name and an invalid version" {
        $res = Uninstall-PSResource -Name "ContosoServer" -Version "(0.0.0.1)"
        $res | Should -BeNullOrEmpty
    }

    It "Uninstall lastest version of a particular module " {
        # test_module resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Uninstall-PSResource -Name "test_module"
        $res.Version | Should -Be "5.0.0.0"

        $resPrerelease = Uninstall-PSResource -Name "test_module" -Prerelease
        $resPrerelease.Version | Should -Be "5.2.5.0"        
    }

    It "Uninstall module using -WhatIf, should not uninstall the module" {
        $res = Uninstall-PSResource -Name "ActiveDirectoryTools" -WhatIf
    }

    It "Do not Uninstall module that is a dependency for another module" {
        $res = Uninstall-PSResource -Name "PackageManagement" 
    }

    It "Uninstall module that is a dependency for another module using -Force" {
        $res = Uninstall-PSResource -Name "PackageManagement" -Force
    }
}


Describe 'Test Uninstall-PSResource for Scripts' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        Get-NewPSResourceRepositoryFile
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Uninstall a specific script by name" {
        $pkg = Uninstall-PSResource -name Test-RPC 
    }

    It "Uninstall a list of scripts by name" {
        $pkg = Uninstall-PSResource -Name adsql, airoute 
    }

    It "Uninstall a script when given name and specifying all versions" {
        $res = Uninstall-PSResource -Name "NetworkingDSC" -version "*"
        $res | Should -BeNullOrEmpty
    }

    It "Uninstall script when given Name and specifying exact version" {
        $res = Uninstall-PSResource -Name "ContosoServer" -Version "1.0.0"
        $res | Should -BeNullOrEmpty
    }

    It "Does not uninstall a script when given Name and a version that does not exist" {
        $res = Uninstall-PSResource -Name "ContosoServer" -Version "3.0.0"
        $res | Should -BeNullOrEmpty
    }

    It "Uninstall script when given Name to <Reason> <Version>" -TestCases @(
        @{Version="[2.0.0.0]";          ExpectedVersion="2.0.0.0"; Reason="validate version, exact match"},
        @{Version="2.0.0.0";            ExpectedVersion="2.0.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.0.0.0, 2.5.0.0]"; ExpectedVersion="2.5.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="(1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="[1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(,1.5.0.0)";         ExpectedVersion="1.0.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,1.5.0.0]";         ExpectedVersion="1.5.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Uninstall-PSResource -Name "Pester" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "Pester"
        $res.Version | Should -Be $ExpectedVersion
    }

    It "Do not uninstall module with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(1.5.0.0)';       Description="exlcusive version (8.1.0.0)"},
        @{Version='[1-5-0-0]';       Description="version formatted with invalid delimiter"},
        @{Version='[1.*.0]';         Description="version with wilcard in middle"},
        @{Version='[*.5.0.0]';       Description="version with wilcard at start"},
        @{Version='[1.*.0.0]';       Description="version with wildcard at second digit"},
        @{Version='[1.5.*.0]';       Description="version with wildcard at third digit"}
        @{Version='[1.5.0.*]';        Description="version with wildcard at end"},
        @{Version='[1..0.0]';        Description="version with missing digit in middle"},
        @{Version='[1.5.0.]';        Description="version with missing digit at end"},
        @{Version='[1.5.0.0.0]';     Description="version with more than 4 digits"}
    ) {
        param($Version, $Description)

        $res = $null
        try {
            $res = Uninstall-PSResource -Name "ContosoServer" -Version $Version -Repository $TestGalleryName -ErrorAction Ignore
        }
        catch {}
        
        $res | Should -BeNullOrEmpty
    }

    It "Does not uninstall when given Name and an invalid version" {
        $res = Uninstall-PSResource -Name "ContosoServer" -Version "(0.0.0.1)"
        $res | Should -BeNullOrEmpty
    }

    It "Uninstall script using -WhatIf" {
        $res = Uninstall-PSResource -Name "ActiveDirectoryTools" -WhatIf
    }
}
#>