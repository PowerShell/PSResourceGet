# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Uninstall-PSResource for Modules' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        Get-NewPSResourceRepositoryFile
        $res = Uninstall-PSResource -name ContosoServer -Version "*"
    }
    BeforeEach{
        $null = Install-PSResource ContosoServer -Repository $TestGalleryName -TrustRepository -WarningAction SilentlyContinue
    }
    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Uninstall a specific module by name" {
        $res = Uninstall-PSResource -name ContosoServer 
        Get-Module ContosoServer -ListAvailable | Should -Be $null
    }

    It "Uninstall a list of modules by name" {
        $null = Install-PSResource BaseTestPackage -Repository $TestGalleryName -TrustRepository -WarningAction SilentlyContinue

        $res = Uninstall-PSResource -Name BaseTestPackage, ContosoServer 
        Get-Module ContosoServer, BaseTestPackage -ListAvailable | Should -be $null
    }

    It "Uninstall a specific script by name" {
        $null = Install-PSResource Test-RPC -Repository $TestGalleryName -TrustRepository -WarningAction SilentlyContinue

        $pkg = Uninstall-PSResource -name Test-RPC 
    }

    It "Uninstall a list of scripts by name" {
        $null = Install-PSResource adsql, airoute -Repository $TestGalleryName -TrustRepository -WarningAction SilentlyContinue

        $pkg = Uninstall-PSResource -Name adsql, airoute 
    }

    It "Uninstall a module when given name and specifying all versions" {
        $null = Install-PSResource ContosoServer -Repository $TestGalleryName -Version "1.0.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource ContosoServer -Repository $TestGalleryName -Version "1.5.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource ContosoServer -Repository $TestGalleryName -Version "2.0.0" -TrustRepository -WarningAction SilentlyContinue

        $res = Uninstall-PSResource -Name ContosoServer -version "*"
        $pkgs = Get-Module ContosoServer -ListAvailable
        $pkgs.Version | Should -Not -Contain "1.0.0"
        $pkgs.Version | Should -Not -Contain "1.5.0"
        $pkgs.Version | Should -Not -Contain "2.0.0"
        $pkgs.Version | Should -Not -Contain "2.5.0"
    }

    It "Uninstall a module when given name and using the default version (ie all versions, not explicitly specified)" {
        $null = Install-PSResource ContosoServer -Repository $TestGalleryName -Version "1.0.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource ContosoServer -Repository $TestGalleryName -Version "1.5.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource ContosoServer -Repository $TestGalleryName -Version "2.0.0" -TrustRepository -WarningAction SilentlyContinue

        $res = Uninstall-PSResource -Name ContosoServer
        $pkgs = Get-Module ContosoServer -ListAvailable
        $pkgs.Version | Should -Not -Contain "1.0.0"
        $pkgs.Version | Should -Not -Contain "1.5.0"
        $pkgs.Version | Should -Not -Contain "2.0.0"
        $pkgs.Version | Should -Not -Contain "2.5.0"
    }

    It "Uninstall module when given Name and specifying exact version" {
        $null = Install-PSResource ContosoServer -Repository $TestGalleryName -Version "1.0.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource ContosoServer -Repository $TestGalleryName -Version "1.5.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource ContosoServer -Repository $TestGalleryName -Version "2.0.0" -TrustRepository -WarningAction SilentlyContinue

        $res = Uninstall-PSResource -Name "ContosoServer" -Version "1.0.0"
        $pkgs = Get-Module ContosoServer -ListAvailable
        $pkgs.Version | Should -Not -Contain "1.0.0"
    }

    $testCases = @{Version="[2.0.0.0]";          ExpectedVersion="2.0.0.0"; Reason="validate version, exact match"},
                @{Version="2.0.0.0";            ExpectedVersion="2.0.0.0"; Reason="validate version, exact match without bracket syntax"},
                @{Version="[1.0.0.0, 2.5.0.0]"; ExpectedVersion="2.5.0.0"; Reason="validate version, exact range inclusive"},
                @{Version="(1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, exact range exclusive"},
                @{Version="(1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version exclusive"},
                @{Version="[1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version inclusive"},
                @{Version="(,1.5.0.0)";         ExpectedVersion="1.0.0.0"; Reason="validate version, maximum version exclusive"},
                @{Version="(,1.5.0.0]";         ExpectedVersion="1.5.0.0"; Reason="validate version, maximum version inclusive"},
                @{Version="[1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
                
    It "Uninstall module when given Name to <Reason> <Version>" -TestCases $testCases {
        param($Version, $ExpectedVersion)
        $null = Install-PSResource ContosoServer -Repository $TestGalleryName -Version "1.0.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource ContosoServer -Repository $TestGalleryName -Version "1.5.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource ContosoServer -Repository $TestGalleryName -Version "2.0.0" -TrustRepository -WarningAction SilentlyContinue

        $res = Uninstall-PSResource -Name ContosoServer -Version $Version
        $pkgs = Get-Module ContosoServer -ListAvailable
        $pkgs.Version | Should -Not -Contain $Version
    }

    $testCases2 =  @{Version='[1.*.0]';         Description="version with wilcard in middle"},
                @{Version='[*.5.0.0]';       Description="version with wilcard at start"},
                @{Version='[1.*.0.0]';       Description="version with wildcard at second digit"},
                @{Version='[1.5.*.0]';       Description="version with wildcard at third digit"}
                @{Version='[1.5.0.*]';       Description="version with wildcard at end"},
                @{Version='[1..0.0]';        Description="version with missing digit in middle"},
                @{Version='[1.5.0.]';        Description="version with missing digit at end"},
                @{Version='[1.5.0.0.0]';     Description="version with more than 4 digits"}

    It "Do not uninstall module with incorrectly formatted version such as <Description>" -TestCases $testCases2 {
        param($Version, $Description)

        {Uninstall-PSResource -Name "ContosoServer" -Version $Version} | Should -Throw "Argument for -Version parameter is not in the proper format."
    }

    $testCases3 =  @{Version='(2.5.0.0)';       Description="exclusive version (8.1.0.0)"},
                @{Version='[2-5-0-0]';       Description="version formatted with invalid delimiter"}

    It "Do not uninstall module with incorrectly formatted version such as <Description>" -TestCases $testCases3 {
        param($Version, $Description)

        Uninstall-PSResource -Name "ContosoServer" -Version $Version

        $pkg = Get-Module ContosoServer -ListAvailable
        $pkg.Version | Should -Be "2.5"
    }

    It "Uninstall module using -WhatIf, should not uninstall the module" {
        $res = Uninstall-PSResource -Name "ContosoServer" -WhatIf

        $pkg = Get-Module ContosoServer -ListAvailable
        $pkg.Version | Should -Be "2.5"
    }

    It "Do not Uninstall module that is a dependency for another module" {
        $null = Install-PSResource "test_module" -Repository $TestGalleryName -TrustRepository -WarningAction SilentlyContinue
    
        Uninstall-PSResource -Name "RequiredModule1" -ErrorVariable ev -ErrorAction SilentlyContinue

        $pkg = Get-Module "RequiredModule1" -ListAvailable
        $pkg | Should -Not -Be $null

        $ev | Should -Be "Cannot uninstall 'RequiredModule1', the following package(s) take a dependency on this package: test_module. If you would still like to uninstall, rerun the command with -Force"
    }

    It "Uninstall module that is a dependency for another module using -Force" {
        $null = Install-PSResource "test_module" -Repository $TestGalleryName -TrustRepository -WarningAction SilentlyContinue

        $res = Uninstall-PSResource -Name "RequiredModule1" -Force
        
        $pkg = Get-Module "RequiredModule1" -ListAvailable
        $pkg | Should -Be $null
    }
}
