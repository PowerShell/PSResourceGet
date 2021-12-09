# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Uninstall-PSResource for Modules' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        $testModuleName = "test_module"
        $testScriptName = "test_script"
        Get-NewPSResourceRepositoryFile
        Uninstall-PSResource -name ContosoServer -Version "*"
    }
    BeforeEach{
        $null = Install-PSResource ContosoServer -Repository $TestGalleryName -TrustRepository -WarningAction SilentlyContinue
    }
    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Uninstall a specific module by name" {
        Uninstall-PSResource -name ContosoServer 
        Get-Module ContosoServer -ListAvailable | Should -Be $null
    }

    $testCases = @{Name="Test?Module";      ErrorId="ErrorFilteringNamesForUnsupportedWildcards"},
                 @{Name="Test[Module";      ErrorId="ErrorFilteringNamesForUnsupportedWildcards"}

    It "not uninstall module given Name with invalid wildcard characters" -TestCases $testCases {
        param($Name, $ErrorId)
        Uninstall-PSResource -Name $Name -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "$ErrorId,Microsoft.PowerShell.PowerShellGet.Cmdlets.UninstallPSResource"
    }

    It "Uninstall a list of modules by name" {
        $null = Install-PSResource BaseTestPackage -Repository $TestGalleryName -TrustRepository -WarningAction SilentlyContinue

        Uninstall-PSResource -Name BaseTestPackage, ContosoServer 
        Get-Module ContosoServer, BaseTestPackage -ListAvailable | Should -be $null
    }

    It "Uninstall a specific script by name" {
        $null = Install-PSResource "test_script" -Repository $TestGalleryName -TrustRepository
        $res = Get-PSResource -Name "test_script"
        $res.Name | Should -Be "test_script"

        Uninstall-PSResource -name "test_script"
        $res = Get-PSResource -Name "test_script"
        $res | Should -BeNullOrEmpty
    }

    It "Uninstall a list of scripts by name" {
        $null = Install-PSResource "test_script", "TestTestScript" -Repository $TestGalleryName -TrustRepository
        $res = Get-PSResource -Name "test_script"
        $res2 = Get-PSResource -Name "TestTestScript"
        $res.Name | Should -Be "test_script"
        $res2.Name | Should -Be "TestTestScript"

        Uninstall-PSResource -Name "test_script", "TestTestScript"
        $res = Get-PSResource -Name "test_script"
        $res2 = Get-PSResource -Name "TestTestScript"
        $res | Should -BeNullOrEmpty
        $res2 | Should -BeNullOrEmpty
    }

    It "Uninstall a module when given name and specifying all versions" {
        $null = Install-PSResource ContosoServer -Repository $TestGalleryName -Version "1.0.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource ContosoServer -Repository $TestGalleryName -Version "1.5.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource ContosoServer -Repository $TestGalleryName -Version "2.0.0" -TrustRepository -WarningAction SilentlyContinue

        Uninstall-PSResource -Name ContosoServer -version "*"
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

        Uninstall-PSResource -Name ContosoServer
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

        Uninstall-PSResource -Name "ContosoServer" -Version "1.0.0"
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

        Uninstall-PSResource -Name ContosoServer -Version $Version
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

    It "Uninstall prerelease version module when prerelease version specified" {
        Install-PSResource -Name $testModuleName -Version "5.2.5-alpha001" -Repository $TestGalleryName
        Uninstall-PSResource -Name $testModuleName -Version "5.2.5-alpha001"
        $res = Get-PSResource $testModuleName -Version "5.2.5-alpha001"
        $res | Should -BeNullOrEmpty
    }

    It "Not uninstall non-prerelease version module when similar prerelease version is specified" {
        Install-PSResource -Name $testModuleName -Version "5.0.0.0" -Repository $TestGalleryName
        Uninstall-PSResource -Name $testModuleName -Version "5.0.0-preview"
        $res = Get-PSResource -Name $testModuleName -Version "5.0.0.0"
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0.0"
    }

    It "Uninstall prerelease version script when prerelease version specified" {
        Install-PSResource -Name $testScriptName -Version "3.0.0-alpha001" -Repository $TestGalleryName
        Uninstall-PSResource -Name $testScriptName -Version "3.0.0-alpha001"
        $res = Get-PSResource -Name $testScriptName
        $res | Should -BeNullOrEmpty
    }

    It "Not uninstall non-prerelease version module when prerelease version specified" {
        Install-PSResource -Name $testScriptName -Version "2.5.0.0" -Repository $TestGalleryName
        Uninstall-PSResource -Name $testScriptName -Version "2.5.0-alpha001"
        $res = Get-PSResource -Name $testScriptName -Version "2.5.0.0"
        $res.Name | Should -Be $testScriptName
        $res.Version | Should -Be "2.5.0.0"
    }

    It "Uninstall module using -WhatIf, should not uninstall the module" {
        Uninstall-PSResource -Name "ContosoServer" -WhatIf
        $pkg = Get-Module ContosoServer -ListAvailable
        $pkg.Version | Should -Be "2.5"
    }

    It "Do not Uninstall module that is a dependency for another module" {
        $null = Install-PSResource $testModuleName -Repository $TestGalleryName -TrustRepository -WarningAction SilentlyContinue
    
        Uninstall-PSResource -Name "RequiredModule1" -ErrorVariable ev -ErrorAction SilentlyContinue

        $pkg = Get-Module "RequiredModule1" -ListAvailable
        $pkg | Should -Not -Be $null

        $ev.FullyQualifiedErrorId | Should -BeExactly 'UninstallPSResourcePackageIsaDependency,Microsoft.PowerShell.PowerShellGet.Cmdlets.UninstallPSResource'
    }

    It "Uninstall module that is a dependency for another module using -SkipDependencyCheck" {
        $null = Install-PSResource $testModuleName -Repository $TestGalleryName -TrustRepository -WarningAction SilentlyContinue

        Uninstall-PSResource -Name "RequiredModule1" -SkipDependencyCheck
        
        $pkg = Get-Module "RequiredModule1" -ListAvailable
        $pkg | Should -BeNullOrEmpty
    }

    It "Uninstall PSResourceInfo object piped in" {
        Install-PSResource -Name "ContosoServer" -Version "1.5.0.0" -Repository $TestGalleryName
        Get-PSResource -Name "ContosoServer" -Version "1.5.0.0" | Uninstall-PSResource
        $res = Get-PSResource -Name "ContosoServer" -Version "1.5.0.0"
        $res | Should -BeNullOrEmpty
    }

    It "Uninstall PSResourceInfo object piped in for prerelease version object" {
        Install-PSResource -Name $testModuleName -Version "4.5.2-alpha001" -Repository $TestGalleryName
        Get-PSResource -Name $testModuleName -Version "4.5.2-alpha001" | Uninstall-PSResource
        $res = Get-PSResource -Name $testModuleName -Version "4.5.2-alpha001"
        $res | Should -BeNullOrEmpty
    }
}
