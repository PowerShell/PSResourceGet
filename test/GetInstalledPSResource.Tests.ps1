# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Get-InstalledPSResource for Module' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $testModuleName = "test_module"
        $testScriptName = "test_script"
        Get-NewPSResourceRepositoryFile

        Install-PSResource ContosoServer -Repository $TestGalleryName -TrustRepository
        Install-PSResource ContosoServer -Repository $TestGalleryName -TrustRepository -Version "2.0"
        Install-PSResource ContosoServer -Repository $TestGalleryName -TrustRepository -Version "1.5"
        Install-PSResource ContosoServer -Repository $TestGalleryName -TrustRepository -Version "1.0"
        Install-PSResource TestTestScript -Repository $TestGalleryName -TrustRepository
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Get resources without any parameter values" {
        $pkgs = Get-InstalledPSResource
        $pkgs.Count | Should -BeGreaterThan 1
    }

    It "Get specific module resource by name" {
        $pkg = Get-InstalledPSResource -Name ContosoServer
        $pkg.Name | Should -Contain "ContosoServer"
    }

    It "Get specific script resource by name" {
        $pkg = Get-InstalledPSResource -Name TestTestScript
        $pkg.Name | Should -Be "TestTestScript"
    }

    It "Get resource when given Name to <Reason> <Version>" -TestCases @(
        @{Name="*tosoSer*";    ExpectedName="ContosoServer"; Reason="validate name, with wildcard at beginning and end of name: *tosoSer*"},
        @{Name="ContosoSer*"; ExpectedName="ContosoServer"; Reason="validate name, with wildcard at end of name: ContosoSer*"},
        @{Name="*tosoServer";   ExpectedName="ContosoServer"; Reason="validate name, with wildcard at beginning of name: *tosoServer"},
        @{Name="Cont*erver";   ExpectedName="ContosoServer"; Reason="validate name, with wildcard in middle of name: Cont*erver"}
    ) {
        param($Version, $ExpectedVersion)
        $pkgs = Get-InstalledPSResource -Name $Name
        $pkgs.Name | Should -Contain "ContosoServer"
    }

$testCases =  
    @{Version="[2.0.0.0]";          ExpectedVersion="2.0.0.0"; Reason="validate version, exact match"},
    @{Version="2.0.0.0";            ExpectedVersion="2.0.0.0"; Reason="validate version, exact match without bracket syntax"},
    @{Version="[1.0.0.0, 2.5.0.0]"; ExpectedVersion=@("2.5.0.0", "2.0.0.0", "1.5.0.0", "1.0.0.0"); Reason="validate version, exact range inclusive"},
    @{Version="(1.0.0.0, 2.5.0.0)"; ExpectedVersion=@("2.0.0.0", "1.5.0.0"); Reason="validate version, exact range exclusive"},
    @{Version="(1.0.0.0,)";         ExpectedVersion=@("2.5.0.0", "2.0.0.0", "1.5.0.0"); Reason="validate version, minimum version exclusive"},
    @{Version="[1.0.0.0,)";         ExpectedVersion=@("2.5.0.0", "2.0.0.0", "1.5.0.0", "1.0.0.0"); Reason="validate version, minimum version inclusive"},
    @{Version="(,1.5.0.0)";         ExpectedVersion="1.0.0.0"; Reason="validate version, maximum version exclusive"},
    @{Version="(,1.5.0.0]";         ExpectedVersion=@("1.5.0.0", "1.0.0.0"); Reason="validate version, maximum version inclusive"},
    @{Version="[1.0.0.0, 2.5.0.0)"; ExpectedVersion=@("2.0.0.0", "1.5.0.0", "1.0.0.0"); Reason="validate version, mixed inclusive minimum and exclusive maximum version"}

    It "Get resource when given Name to <Reason> <Version>" -TestCases $testCases {
        param($Version, $ExpectedVersion)
        $pkgs = Get-InstalledPSResource -Name "ContosoServer" -Version $Version
        $pkgs.Name | Should -Contain "ContosoServer"
        $pkgs.Version | Should -Be $ExpectedVersion
    }

    It "Throw invalid version error when passing incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='[1.*.0]';         Description="version with wilcard in middle"},
        @{Version='[*.5.0.0]';       Description="version with wilcard at start"},
        @{Version='[1.*.0.0]';       Description="version with wildcard at second digit"},
        @{Version='[1.5.*.0]';       Description="version with wildcard at third digit"}
        @{Version='[1.5.0.*';        Description="version with wildcard at end"},
        @{Version='[1..0.0]';        Description="version with missing digit in middle"},
        @{Version='[1.5.0.]';        Description="version with missing digit at end"},
        @{Version='[1.5.0.0.0]';     Description="version with more than 4 digits"}
    ) {
        param($Version, $Description)

        $res = $null
        try {
            $res = Find-PSResource -Name "ContosoServer" -Version $Version -Repository $TestGalleryName -ErrorAction Ignore
        }
        catch {}
        
        $res | Should -BeNullOrEmpty
    }

    # These versions technically parse into proper NuGet versions, but will not return the version expected
    It "Does not return resource when passing incorrectly formatted version such as <Description>, does not throw error" -TestCases @(
        @{Version='(1.5.0.0)';       Description="exlcusive version (8.1.0.0)"},
        @{Version='[1-5-0-0]';       Description="version formatted with invalid delimiter"}

    ) {
        param($Version, $Description)

        $res = $null
        try {
            $res = Find-PSResource -Name "ContosoServer" -Version $Version -Repository $TestGalleryName -ErrorAction Ignore
        }
        catch {}
        
        $res | Should -BeNullOrEmpty
    }

    It "Get resources when given Name, and Version is '*'" {
        $pkgs = Get-InstalledPSResource -Name ContosoServer -Version "*"
        $pkgs.Count | Should -BeGreaterOrEqual 2
    }

    It "Get prerelease version module when version with correct prerelease label is specified" {
        Install-PSResource -Name $testModuleName -Version "5.2.5-alpha001"
        $res = Get-InstalledPSResource -Name $testModuleName -Version "5.2.5"
        $res | Should -BeNullOrEmpty
        $res = Get-InstalledPSResource -Name $testModuleName -Version "5.2.5-alpha001"
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.2.5"
        $res.PrereleaseLabel | Should -Be "alpha001"
    }

    It "Get prerelease version script when version with correct prerelease label is specified" {
        Install-PSResource -Name $testScriptName -Version "3.0.0-alpha001"
        $res = Get-InstalledPSResource -Name $testScriptName -Version "3.0.0"
        $res | Should -BeNullOrEmpty
        $res = Get-InstalledPSResource -Name $testScriptName -Version "3.0.0-alpha001"
        $res.Name | Should -Be $testScriptName
        $res.Version | Should -Be "3.0.0"
        $res.PrereleaseLabel | Should -Be "alpha001"
    }
}
