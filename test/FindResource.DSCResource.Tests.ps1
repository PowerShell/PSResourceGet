# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -force

Describe 'Test Find-PSResource for DSC Resource' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        Get-NewPSResourceRepositoryFile
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    # Purpose: find a DSCResource resource given Name parameter
    #
    # Action: Find-PSResource -Name NetworkingDsc
    #
    # Expected Result: returns resource with name NetworkingDsc
    It "find a DSCResource resource given Name parameter" {
        $res = Find-PSResource -Name "test_dsc_module"
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "test_dsc_module"
    }

    # Purpose: find a DSC resource given Name, to validate version parameter values
    #
    # Action: Find-PSResource -Name NetworkingDsc -Version [6.0.0.0]
    #
    # Expected Result: return resource meeting version criteria
    It "find DSC resource when given Name to <Version> ---  <Reason>" -TestCases @(
        @{Version="[2.0.0.0]";          ExpectedVersion="2.0.0.0"; Reason="validate version, exact match"},
        @{Version="2.0.0.0";            ExpectedVersion="2.0.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.0.0.0, 4.0.0.0]"; ExpectedVersion="4.0.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0, 4.0.0.0)"; ExpectedVersion="3.5.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="(1.0.0.0,)";         ExpectedVersion="4.0.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="[3.5.0.0,)";         ExpectedVersion="4.0.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(,4.0.0.0)";         ExpectedVersion="3.5.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,4.0.0.0]";         ExpectedVersion="4.0.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0, 4.0.0.0)"; ExpectedVersion="3.5.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -Name "test_dsc_module" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "test_dsc_module"
        $res.Version | Should -Be $ExpectedVersion
    }

    # Purpose: not find resources with invalid version
    #
    # Action: Find-PSResource -Name "test_dsc_module" -Version "(2.5.*.0)"
    #
    # Expected Result: should not return a resource
    It "not find resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(2.5.0.0)';       Description="exlcusive version (8.1.0.0)"},
        @{Version='[2-5-0-0]';       Description="version formatted with invalid delimiter"},
        @{Version='[2.*.0]';         Description="version with wilcard in middle"},
        @{Version='[*.5.0.0]';       Description="version with wilcard at start"},
        @{Version='[2.*.0.0]';       Description="version with wildcard at second digit"},
        @{Version='[2.5.*.0]';       Description="version with wildcard at third digit"}
        @{Version='[2.5.0.*';        Description="version with wildcard at end"},
        @{Version='[2..0.0]';        Description="version with missing digit in middle"},
        @{Version='[2.5.0.]';        Description="version with missing digit at end"},
        @{Version='[2.5.0.0.0]';     Description="version with more than 4 digits"}
    ) {
        param($Version, $Description)

        $res = $null
        try {
            $res = Find-PSResource -Name "test_dsc_module" -Version $Version -Repository $TestGalleryName -ErrorAction Ignore
        }
        catch {}

        $res | Should -BeNullOrEmpty
    }

    # Purpose: find a DSCResource resource with wilcard range Version parameter -> '*'
    #
    # Action: Find-PSResource -Name test_dsc_module -Version "*"
    #
    # Expected Result: returns all test_dsc_module resources (with versions in descending order)
    It "find a DSCResource resource given Name with Version wildcard match " {
        $res = Find-PSResource -Name "test_dsc_module" -Version "*"
        $res.Count | Should -Be 7
    }

    # Purpose: find DSC resource with all versions (incl preview), with Prerelease parameter
    #
    # Action: Find-PSResource -Name ActiveDirectoryCSDsc -Version "*" -Prerelease
    #
    # Expected Result: should return more versions with prerelease parameter than without
    It "find DSCResource resource with all preview versions, with Prerelease parameter" {
        $res = Find-PSResource -Name "test_dsc_module" -Version "*"
        $withoutPrereleaseVersions = $res.Count

        $resPrerelease = Find-PSResource -Name "test_dsc_module" -Version "*" -Prerelease
        $withPrereleaseVersions = $resPrerelease.Count
        $withPrereleaseVersions | Should -BeGreaterOrEqual $withoutPrereleaseVersions
    }

    # Purpose: find a DSCResource resource, with preview version
    #
    # Action: Find-PSResource -Name ActiveDirectoryCSDsc -Prerelease
    #
    # Expected Result: should return (a later or) preview version than if without Prerelease parameter
    It "find DSCResource resource with preview version, with Prerelease parameter" {
        $res = Find-PSResource -Name "test_dsc_module" -Repository $TestGalleryName
        $res.Version | Should -Be "4.0.0.0"

        $resPrerelease = Find-PSResource -Name "test_dsc_module" -Prerelease -Repository $TestGalleryName
        $resPrerelease.Version | Should -Be "4.5.0.0"
    }

    # Purpose: find a DSCResource of package type module, given ModuleName parameter
    #
    # Action: Find-PSResource -ModuleName NetworkingDsc
    #
    # Expected Result: returns DSCResource with specified ModuleName
    It "find a DSCResource of package type module, given ModuleName parameter" {
        $res = Find-PSResource -ModuleName "test_dsc_module" -Repository $TestGalleryName
        $res.Name | Should -Be "test_dsc_module"
    }

    # Purpose: find a DSC resource given Name, to validate version parameter values
    #
    # Action: Find-PSResource -Name NetworkingDsc -Version [6.0.0.0]
    #
    # Expected Result: return resource meeting version criteria
    It "find DSC resource when given ModuleName to <Version> --- <Reason>" -TestCases @(
        @{Version="[2.0.0.0]";          ExpectedVersion="2.0.0.0"; Reason="validate version, exact match"},
        @{Version="2.0.0.0";            ExpectedVersion="2.0.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.0.0.0, 4.0.0.0]"; ExpectedVersion="4.0.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0, 4.0.0.0)"; ExpectedVersion="3.5.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="(1.0.0.0,)";         ExpectedVersion="4.0.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="[3.5.0.0,)";         ExpectedVersion="4.0.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(,4.0.0.0)";         ExpectedVersion="3.5.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,4.0.0.0]";         ExpectedVersion="4.0.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0, 4.0.0.0)"; ExpectedVersion="3.5.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -ModuleName "test_dsc_module" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "test_dsc_module"
        $res.Version | Should -Be $ExpectedVersion
    }

    # Purpose: find a DSCResource with a specific tag, given Tags parameter
    #
    # Action: Find-PSResource -Tags "DSC" -Repository PoshTestGallery | Where-Object { $_.Name -eq "test_dsc_module" }
    #
    # Expected Result: return DscTestModule resource
    It "find a DSCResource with specific tag, given Tags parameter"{
        $res = Find-PSResource -Tags "DSC" -Repository $TestGalleryName | Where-Object { $_.Name -eq "test_dsc_module" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "test_dsc_module"
    }

    # Purpose: find a DSCResource with multiple tag, given Tags parameter
    #
    # Action: Find-PSResource -Tags "DSC","PSDscResource_" -Repository PoshTestGallery | Where-Object { $_.Name -eq "DscTestModule" }
    #
    # Expected Result: return DscTestModule resource
    It "find a DSCResource with specific tag, given Tags parameter"{
        $res = Find-PSResource -Tags "DSC","PSDscResource_" -Repository $TestGalleryName | Where-Object { $_.Name -eq "test_dsc_module" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "test_dsc_module"
    }

    # Purpose: not find non-available DSCResource from repository, given Repository parameter
    #
    # Action: Find-PSResource -Name "test_dsc_module" -Repository PSGallery
    #
    # Expected Result: should not "test_dsc_module" from PoshTestGallery repository
    It "not find DSCResource from repository, given Repository parameter" {
        $res = Find-PSResource -Name "test_dsc_module" -Repository $PSGalleryName
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find DSCResource from repository, given Repository parameter
    #
    # Action: Find-PSResource -Name "test_dsc_module" -Repository PoshTestGallery
    #
    # Expected Result: should find "test_dsc_module" from PSGallery repository
    It "find DSCResource from repository, given Repository parameter" {
        $res = Find-PSResource -Name "test_dsc_module" -Repository $TestGalleryName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "test_dsc_module"
    }

    # Purpose: find resource in first repository where it exists given Repository parameter
    #
    # Action: Find-PSResource "PackageManagement"
    #         Find-PSResource "PackageManagement" -Repository PSGallery
    #
    # Expected Result: Returns resource from first avaiable or specfied repository
    It "find Resource given repository parameter, where resource exists in multiple repos" {
        # first availability found in PoshTestGallery
        $res = Find-PSResource "PackageManagement"
        $res.Repository | Should -Be "PoshTestGallery"

        # check that same resource can be returned from non-first-availability/non-default repo
        $resNonDefault = Find-PSResource "PackageManagement" -Repository $PSGalleryName
        $resNonDefault.Repository | Should -Be "PSGallery"
    }

    # Purpose: not find existing DSCResource from non-existant repository, given Repository parameter
    #
    # Action: Find-PSResource -Name AccessControlDSC -Repository NonExistantRepo
    #
    # Expected Result: should not find AccessControlDSC resource from NonExistantRepo repository
    It "not find DSCResource from non-existant repository, given Repository parameter" {
        $res = Find-PSResource -Name "test_dsc_module" -Repository NonExistantRepo
        $res.Name | Should -BeNullOrEmpty
    }

    # Purpose: find resource in local repository given Repository parameter
    #
    # Action: Find-PSResource -Name "local_command_module" -Repository "psgettestlocal"
    #
    # Expected Result: should find resource from local repository
    # It "find resource in local repository given Repository parameter" {
    #     $publishDscName = "TestFindDSCModule"
    #     Get-DSCResourcePublishedToLocalRepo $publishDscName

    #     $res = Find-PSResource -Name $publishDscName -Repository "psgettestlocal"
    #     $res | Should -Not -BeNullOrEmpty
    #     $res.Name | Should -Be $publishDscName

    #     RemoveTmpdir
    # }
    It "find resource in local repository given Repository parameter"{
        $publishDscName = "TestFindDSCModule"
        Get-DSCResourcePublishedToLocalRepoTestDrive $publishDscName

        $res = Find-PSResource -Name $publishDscName -Repository "psgettestlocal"
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be $publishDscName
        $res.Repository | Should -Be "psgettestlocal"
    }
}
