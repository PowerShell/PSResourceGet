# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

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

    # Purpose: Get all resources when no parameters are specified
    # Action: Get-InstalledPSResource
    # Expected-Result: Should get all (more than 1) resources in PSGallery
    It "Get resources without any parameter values" {
        $pkgs = Get-InstalledPSResource
        $pkgs.Count | Should -BeGreaterThan 0
    }

    # Purpose: Get a specific resource by name
    # Action: Get-InstalledPSResource -Name "ContosoServer"
    # Expected Result: Should get ContosoServer resource
    It "Get specific module resource by name" {
        $pkg = Get-InstalledPSResource -Name ContosoServer
        $pkg.Name | Should -Be "ContosoServer"
    }

    # Purpose: Get resource when given Name, Version param not null
    # Action: Get-InstalledPSResource -Name ContosoServer -Version
    # Expected Result: Returns ContosoServer resource
    It "find resource when given Name to <Reason> <Version>" -TestCases @(
        @{Name="*ShellG*";    ExpectedName="PowerShellGet"; Reason="validate name, with wildcard at beginning and end of name: *ShellG*"},
        @{Name="PowerShell*"; ExpectedName="PowerShellGet"; Reason="validate name, with wildcard at end of name: PowerShellG*"},
        @{Name="*ShellGet";   ExpectedName="PowerShellGet"; Reason="validate name, with wildcard at beginning of name: *ShellGet"},
        @{Name="Power*Get";   ExpectedName="PowerShellGet"; Reason="validate name, with wildcard in middle of name: Power*Get"}
    ) {
        param($Version, $ExpectedVersion)
        $pkgs = Get-InstalledPSResource -Name $Name
        $pkgs.Name | Should -Be "PowerShellGet"
    }

    # Purpose: Get resource when given Name, Version param not null
    # Action: Get-InstalledPSResource -Name ContosoServer -Version
    # Expected Result: Returns ContosoServer resource
    It "find resource when given Name to <Reason> <Version>" -TestCases @(
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

    # Purpose: not find resources with invalid version
    # Action: Find-PSResource -Name "ContosoServer" -Version "(1.5.0.0)"
    # Expected Result: should not return a resource
    It "not find resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(1.5.0.0)';       Description="exlcusive version (8.1.0.0)"},
        @{Version='[1-5-0-0]';       Description="version formatted with invalid delimiter"},
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

    # Purpose: Get installed resources when given Name, and Version is '*'
    # Action: Get-InstalledPSResource -Name ContosoServer -Version "*"
    # Expected Result: returns 4 ContosoServer resources (of all versions in descending order)
    It "Get resources when given Name,  and Version is '*'" {
        $pkgs = Get-InstalledPSResource -Name Carbon -Version "*"
        $pkgs.Count | Should -BeGreaterOrEqual 2
    }
}