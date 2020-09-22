# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Find-PSResource for Command' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
    }
    
    # Purpose: find Command resource given Name paramater
    #
    # Action: Find-PSResource -Name Az.Compute
    #
    # Expected Result: returns Az.Compute resource
    It "find Command resource given Name parameter" {
        $res = Find-PSResource -Name Az.Compute
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "Az.Compute"
    }

    # Purpose: not find Command resource given unavailable name
    #
    # Action: Find-PSResource -Name NonExistantCommand
    #
    # Expected result: should not return NonExistantCommand resource
    It "should not find Command resource given unavailable name" {
        $res = Find-PSResource -Name NonExistantCommand
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find Command resource with exact version, given Version parameter
    #
    # Action: Find-PSResource -Name Az.Compute -Version "[4.3.0.0]"
    #
    # Expected Result: should return Az.Compute resource with version 4.3.0.0
    It "find Command resource given Name to <Reason>" -TestCases @(
        @{Version="[4.3.0.0]";          ExpectedVersion="4.3.0.0"; Reason="validate version, exact match"},
        @{Version="4.3.0.0";            ExpectedVersion="4.3.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[4.2.0.0, 4.4.0.0]"; ExpectedVersion="4.4.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(4.2.0.0, 4.4.0.0)"; ExpectedVersion="4.3.1.0"; Reason="validate version, exact range exclusive"},
        @{Version="[4.4.0.0,)";         ExpectedVersion="4.4.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(4.2.1.0,)";         ExpectedVersion="4.4.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="(,4.3.1.0)";         ExpectedVersion="4.3.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,4.3.1.0]";         ExpectedVersion="4.3.1.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[4.2.0.0, 4.3.1.0)"; ExpectedVersion="4.3.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    )
    {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -Name "Az.Compute" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "Az.Compute"
        $res.Version | Should -Be $ExpectedVersion
    }

    # Purpose: not find resources with invalid version
    #
    # Action: Find-PSResource -Name "Az.Compute" -Version "(4.2.1.0)"
    #
    # Expected Result: should not return a resource
    It "not find resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version="(4.2.1.0)";       Description="exlcusive version (4.2.1.0)"},
        @{Version="[4-2-1-0]";       Description="version formatted with invalid delimiter"},
        @{Version="[4.*.0]";         Description="version with wilcard in middle"},
        @{Version="[*.2.1.0]";       Description="version with wilcard at start"},
        @{Version="[4.*.1.0]";       Description="version with wildcard at second digit"},
        @{Version="[4.2.*.0]";       Description="version with wildcard at third digit"}
        @{Version="[4.2.1.*";        Description="version with wildcard at end"},
        @{Version="[4..1.0]";        Description="version with missing digit in middle"},
        @{Version="[4.2.1.]";        Description="version with missing digit at end"},
        @{Version="[4.2.1.0.0]";     Description="version with more than 4 digits"}
    )
    {
        param($Version, $Description)
        $res = Find-PSResource -Name "Az.Compute" -Version $Version -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }
    
    # Purpose: find Command resource with wildcard version, given Version parameter -> '*' 
    #
    # Action: Find-PSResource -Name Az.Compute -Version "*"
    #
    # Expected Result: should return all Az.Compute resources (versions in descending order)
    It "find Command resource given Name to validate version wilcard match " {
        $res = Find-PSResource -Name "Az.Compute" -Version "*"
        $res.Count | Should -BeGreaterOrEqual 40
    }

    # Purpose: find Command resource with latest-nonpreview versions, by excluding Prerelease parameter
    #
    # Action: Find-PSResource -Name Az.Accounts
    #
    # Expected Result: should return latest non-prerelease/non-preview version of Az.Accounts resource
    It "find Command resource with latest-nonpreview versions, by excluding Prerelease parameter" {
        $res = Find-PSResource -Name Az.Accounts
        $res.Name | Should -Be "Az.Accounts"
        $res.Version | Should -Be "1.9.4.0"
    }

    # Purpose: find Command resource with latest version (including preview versions), with Prerelease parameter
    #
    # Action: Find-PSResource -Name Az.Accounts -Prerelease
    #
    # Expected Result: should return latest version (including preview versions) of Az.Accounts resource
    It "find Command resource with latest version (including preview versions), with Prerelease parameter" {
        $res = Find-PSResource -Name Az.Accounts -Prerelease
        $res.Name | Should -Be "Az.Accounts"
        $res.Version | Should -Be "2.0.1.0"
    }

    # Purpose: find Command resource given ModuleName parameter with Version null or empty
    #
    # Action: Find-PSResource -ModuleName "Az.Accounts" -Repository "PoshTestGallery"
    #
    # Expected Result: should return resource with latest version
    It "find Command resource given ModuleName with Version null or empty" {
        $res = Find-PSResource -ModuleName "Az.Compute" -Repository $TestGalleryName
        $res.Name | Should -Be "Az.Compute"
    }

    # Purpose: find command resource when given ModuleName and any Version parameter
    #
    # Action: Find-PSResource -Name "Az.Accounts" -Version [2.0.0.0] -Repository "PoshTestGallery"
    #
    # Expected Result: should find a command resource when given ModuleName and any version value
    It "find Command resource given ModuleName to <Reason>" -TestCases @(
        @{Version="[4.3.0.0]";          ExpectedVersion="4.3.0.0"; Reason="validate version, exact match"},
        @{Version="4.3.0.0";            ExpectedVersion="4.3.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[4.2.0.0, 4.4.0.0]"; ExpectedVersion="4.4.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(4.2.0.0, 4.4.0.0)"; ExpectedVersion="4.3.1.0"; Reason="validate version, exact range exclusive"},
        @{Version="[4.4.0.0,)";         ExpectedVersion="4.4.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(4.2.1.0,)";         ExpectedVersion="4.4.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="(,4.3.1.0)";         ExpectedVersion="4.3.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,4.3.1.0]";         ExpectedVersion="4.3.1.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[4.2.0.0, 4.3.1.0)"; ExpectedVersion="4.3.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    )
    {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -ModuleName "Az.Compute" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "Az.Compute"
        $res.Version | Should -Be $ExpectedVersion
    }

    # Purpose: find Command resource given ModuleName to validate version match
    #
    # Action: Find-PSResource -ModuleName Az.Compute -Version "*"
    #
    # Expected Result: should return all Az.Compute resources (versions in descending order)
    It "find Command resource given ModuleName to validate version wildcard match " {
        $res = Find-PSResource -ModuleName "Az.Compute" -Version "*"
        $res.Count | Should -BeGreaterOrEqual 40
    }

    # Purpose: find resource with tag, given single Tags parameter
    #
    # Action: Find-PSResource -Tags "Azure" -Repository PoshTestGallery | Where-Object { $_.Name -eq "Az.Accounts" }
    #
    # Expected Result: should return Az.Accounts resource
    It "find resource with single tag, given Tags parameter" {
        $res = Find-PSResource -Tags "Azure" -Repository $TestGalleryName | Where-Object { $_.Name -eq "Az.Accounts" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "Az.Accounts"

    }


    # Purpose: find resource with tags, given multiple Tags parameter values
    #
    # Action: Find-PSResource -Tags "Azure","Authentication","ARM" -Repository PoshTestGallery | Where-Object { $_.Name -eq "Az.Accounts" }
    #
    # Expected Result: should return Az.Accounts resource
    It "find resource with multiple tags, given Tags parameter" {
        $res = Find-PSResource -Tags "Azure","Authentication","ARM" -Repository $TestGalleryName | Where-Object { $_.Name -eq "Az.Accounts" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "Az.Accounts"
    }

    # Purpose: not find Command resource from repository where it is not available, given Repository parameter
    #
    # Action: Find-PSResource -Name "xWindowsUpdate" -Repository PoshTestGallery
    #
    # Expected Result: should not find xWindowsUpdate resource
    It "not find Command resource from repository where it is not available, given Repository parameter" {
        $res = Find-PSResource -Name "xWindowsUpdate" -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find Command resource from repository where it is available, given Repository parameter
    #
    # Action: Find-PSResource -Name "xWindowsUpdate" -Repository PSGallery
    #
    # Expected Result: should find xWindowsUpdate resource
    It "find Command resource, given Repository parameter" {
        $res = Find-PSResource -Name "xWindowsUpdate" -Repository $PSGalleryName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "xWindowsUpdate"
    }    
}
