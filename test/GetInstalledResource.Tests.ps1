# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

<# // Temporarily commenting out until Install-PSResource is complete 
Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Get-InstalledPSResource for Module' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        Get-NewPSResourceRepositoryFile

        Register-PSRepository $TestGalleryName -SourceLocation "https://www.poshtestgallery.com/api/v2"
        Install-Module ContosoServer -Repository $TestGalleryName
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
        $pkg.Name | Should -Be "ContosoServer"
    }

    It "Get specific script resource by name" {
        $pkg = Get-InstalledPSResource -Name adsql
        $pkg.Name | Should -Be "adsql"
    }

    It "Get resource when given Name to <Reason> <Version>" -TestCases @(
        @{Name="*ShellG*";    ExpectedName="PowerShellGet"; Reason="validate name, with wildcard at beginning and end of name: *ShellG*"},
        @{Name="PowerShell*"; ExpectedName="PowerShellGet"; Reason="validate name, with wildcard at end of name: PowerShellG*"},
        @{Name="*ShellGet";   ExpectedName="PowerShellGet"; Reason="validate name, with wildcard at beginning of name: *ShellGet"},
        @{Name="Power*Get";   ExpectedName="PowerShellGet"; Reason="validate name, with wildcard in middle of name: Power*Get"}
    ) {
        param($Version, $ExpectedVersion)
        $pkgs = Get-InstalledPSResource -Name $Name
        $pkgs.Name | Should -Be "PowerShellGet"
    }

    It "Get resource when given Name to <Reason> <Version>" -TestCases @(
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
        $pkgs = Get-InstalledPSResource -Name "ContosoServer" -Version $Version
        $pkgs.Name | Should -Be "ContosoServer"
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
        $pkgs = Get-InstalledPSResource -Name Carbon -Version "*"
        $pkgs.Count | Should -BeGreaterOrEqual 2
    }
}
#>