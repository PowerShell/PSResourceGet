# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Get-InstalledPSResource for Module' -Tags 'CI' {

    BeforeAll{
        $PSGalleryName = Get-PSGalleryName
        $testModuleName = "test_module"
        $testScriptName = "test_script"
        Get-NewPSResourceRepositoryFile

        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -Verbose
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -Version "1.0"
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -Version "3.0"
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -Version "5.0"
        Install-PSResource -Name $testScriptName -Repository $PSGalleryName -TrustRepository
    }

    AfterAll {
        Uninstall-PSResource -Name $testModuleName -Version "*" -ErrorAction SilentlyContinue
        Uninstall-PSResource -Name $testScriptName -Version "*" -ErrorAction SilentlyContinue
        Get-RevertPSResourceRepositoryFile
    }

    It "Get resources without any parameter values" {
        $pkgs = Get-InstalledPSResource
        $pkgs.Count | Should -BeGreaterThan 1
    }

    It "Get specific module resource by name" {
        $pkg = Get-InstalledPSResource -Name $testModuleName
        $pkg.Name | Should -Contain $testModuleName
    }

    It "Get specific script resource by name" {
        $pkg = Get-InstalledPSResource -Name $testScriptName
        $pkg.Name | Should -Be $testScriptName
    }

    It "Get resource when given Name to <Reason> <Version>" -TestCases @(
        @{Name="*est_modul*";    ExpectedName=$testModuleName; Reason="validate name, with wildcard at beginning and end of name: *est_modul*"},
        @{Name="test_mod*";      ExpectedName=$testModuleName; Reason="validate name, with wildcard at end of name: test_mod*"},
        @{Name="*est_module";    ExpectedName=$testModuleName; Reason="validate name, with wildcard at beginning of name: *est_module"},
        @{Name="tes*ule";        ExpectedName=$testModuleName; Reason="validate name, with wildcard in middle of name: tes*ule"}
    ) {
        param($Version, $ExpectedVersion)
        $pkgs = Get-InstalledPSResource -Name $Name
        $pkgs.Name | Should -Contain $testModuleName
    }

$testCases =
    @{Version="[1.0.0.0]";          ExpectedVersion="1.0.0.0";                           Reason="validate version, exact match"},
    @{Version="1.0.0.0";            ExpectedVersion="1.0.0.0";                           Reason="validate version, exact match without bracket syntax"},
    @{Version="[1.0.0.0, 5.0.0.0]"; ExpectedVersion=@("5.0.0.0", "3.0.0.0", "1.0.0.0");  Reason="validate version, exact range inclusive"},
    @{Version="(1.0.0.0, 5.0.0.0)"; ExpectedVersion=@("3.0.0.0");                        Reason="validate version, exact range exclusive"},
    @{Version="(1.0.0.0,)";         ExpectedVersion=@("5.0.0.0", "3.0.0.0");             Reason="validate version, minimum version exclusive"},
    @{Version="[1.0.0.0,)";         ExpectedVersion=@("5.0.0.0", "3.0.0.0", "1.0.0.0");  Reason="validate version, minimum version inclusive"},
    @{Version="(,5.0.0.0)";         ExpectedVersion=@("3.0.0.0", "1.0.0.0");             Reason="validate version, maximum version exclusive"},
    @{Version="(,5.0.0.0]";         ExpectedVersion=@("5.0.0.0", "3.0.0.0", "1.0.0.0");  Reason="validate version, maximum version inclusive"},
    @{Version="[1.0.0.0, 5.0.0.0)"; ExpectedVersion=@("3.0.0.0", "1.0.0.0");             Reason="validate version, mixed inclusive minimum and exclusive maximum version"}

    It "Get resource when given Name to <Reason> <Version>" -TestCases $testCases {
        param($Version, $ExpectedVersion)
        $pkgs = Get-InstalledPSResource -Name $testModuleName -Version $Version
        $pkgs.Name | Should -Contain $testModuleName
        $pkgs.Version | Should -Be $ExpectedVersion
    }

    It "Throw invalid version error when passing incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='[1.*.0]';         Description="version with wilcard in middle"},
        @{Version='[*.0.0.0]';       Description="version with wilcard at start"},
        @{Version='[1.*.0.0]';       Description="version with wildcard at second digit"},
        @{Version='[1.0.*.0]';       Description="version with wildcard at third digit"}
        @{Version='[1.0.0.*';        Description="version with wildcard at end"},
        @{Version='[1..0.0]';        Description="version with missing digit in middle"},
        @{Version='[1.0.0.]';        Description="version with missing digit at end"},
        @{Version='[1.0.0.0.0]';     Description="version with more than 4 digits"}
    ) {
        param($Version, $Description)

        $res = $null
        try {
            $res = Find-PSResource -Name $testModuleName -Version $Version -Repository $PSGalleryName -ErrorAction Ignore
        }
        catch {}

        $res | Should -BeNullOrEmpty
    }

    # These versions technically parse into proper NuGet versions, but will not return the version expected
    It "Does not return resource when passing incorrectly formatted version such as <Description>, does not throw error" -TestCases @(
        @{Version='(1.0.0.0)';       Description="exlcusive version (8.1.0.0)"},
        @{Version='[1-0-0-0]';       Description="version formatted with invalid delimiter"}

    ) {
        param($Version, $Description)

        $res = $null
        try {
            $res = Find-PSResource -Name $testModuleName -Version $Version -Repository $PSGalleryName -ErrorAction Ignore
        }
        catch {}

        $res | Should -BeNullOrEmpty
    }

    It "Get resources when given Name, and Version is '*'" {
        $pkgs = Get-InstalledPSResource -Name $testModuleName -Version "*"
        $pkgs.Count | Should -BeGreaterOrEqual 2
    }

    It "Get prerelease version module when version with correct prerelease label is specified" {
        Install-PSResource -Name $testModuleName -Version "5.2.5-alpha001" -Repository $PSGalleryName -TrustRepository
        $res = Get-InstalledPSResource -Name $testModuleName -Version "5.2.5"
        $res | Should -BeNullOrEmpty
        $res = Get-InstalledPSResource -Name $testModuleName -Version "5.2.5-alpha001"
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.2.5"
        $res.Prerelease | Should -Be "alpha001"
    }

    It "Get prerelease version script when version with correct prerelease label is specified" {
        Install-PSResource -Name $testScriptName -Version "3.0.0-alpha" -Repository $PSGalleryName -TrustRepository
        $res = Get-InstalledPSResource -Name $testScriptName -Version "3.0.0"
        $res | Should -BeNullOrEmpty
        $res = Get-InstalledPSResource -Name $testScriptName -Version "3.0.0-alpha"
        $res.Name | Should -Be $testScriptName
        $res.Version | Should -Be "3.0.0"
        $res.Prerelease | Should -Be "alpha"
    }

     # Windows only
     It "Get resource under CurrentUser scope - Windows only" -Skip:(!(Get-IsWindows)) {
        $pkg = Get-InstalledPSResource -Name $testModuleName -Scope CurrentUser
        $pkg[0].Name | Should -Be $testModuleName
        $pkg[0].InstalledLocation.ToString().Contains("Documents") | Should -Be $true
    }

    # Windows only
    It "Get resource under AllUsers scope when module is installed under CurrentUser - Windows only" -Skip:(!(Get-IsWindows)) {
        $pkg = Get-InstalledPSResource -Name $testModuleName -Scope AllUsers
        $pkg | Should -BeNullOrEmpty
    }

    # Windows only
    It "Get resource under AllUsers scope - Windows only" -Skip:(!((Get-IsWindows) -and (Test-IsAdmin))) {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -Scope AllUsers
        $pkg = Get-InstalledPSResource -Name $testModuleName -Scope AllUsers
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("Program Files") | Should -Be $true
    }

    # Windows only
    It "Get resource under CurrentUser scope when module is installed under AllUsers - Windows only" -Skip:(!((Get-IsWindows) -and (Test-IsAdmin))) {
        Uninstall-PSResource -Name $testModuleName -Version "*"
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -Scope AllUsers
        $pkg = Get-InstalledPSResource -Name $testModuleName -Scope CurrentUser
        $pkg | Should -BeNullOrEmpty
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Get resource under CurrentUser scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name "testmodule99" -Repository $PSGalleryName -TrustRepository -Scope CurrentUser
        $pkg = Get-InstalledPSResource "testmodule99" -Scope CurrentUser
        $pkg.Name | Should -contain "testmodule99"
        $pkg.InstalledLocation.ToString().Contains("/.local") | Should -Be $true
    }
}
