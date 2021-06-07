# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -force

Describe 'Test Find-PSResource for Command' {

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
        $res = Find-PSResource -Name NetworkingDsc
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "NetworkingDsc"
    }

    # Purpose: find a DSC resource given Name, to validate version parameter values
    #
    # Action: Find-PSResource -Name NetworkingDsc -Version [6.0.0.0]
    #
    # Expected Result: return resource meeting version criteria
    It "find DSC resource when given Name to <Reason>" -TestCases @(
        @{Version="[6.0.0.0]";          ExpectedVersion="6.0.0.0"; Reason="validate version, exact match"},
        @{Version="6.0.0.0";            ExpectedVersion="6.0.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[6.0.0.0, 8.0.0.0]"; ExpectedVersion="8.0.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(6.0.0.0, 7.4.0.0)"; ExpectedVersion="7.3.0.0"; Reason="validate version, exact range exclusive"},
        <#
        @{Version="(6.0.0.0,)";         ExpectedVersion="8.1.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="[6.0.0.0,)";         ExpectedVersion="8.1.0.0"; Reason="validate version, minimum version inclusive"},
        #>
        @{Version="(,7.4.0.0)";         ExpectedVersion="7.3.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,7.4.0.0]";         ExpectedVersion="7.4.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[6.0.0.0, 7.4.0.0)"; ExpectedVersion="7.3.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -Name "NetworkingDsc" -Version $Version -Repository $PSGalleryName
        $res.Name | Should -Be "NetworkingDsc"
        $res.Version | Should -Be $ExpectedVersion
    }

    # Purpose: not find resources with invalid version
    #
    # Action: Find-PSResource -Name "NetworkingDsc" -Version "(2.5.0.0)"
    #
    # Expected Result: should not return a resource
    It "not find resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(8.1.0.0)';       Description="exlcusive version (8.1.0.0)"},
        @{Version='[8-1-0-0]';       Description="version formatted with invalid delimiter"},
        @{Version='[8.*.0]';         Description="version with wilcard in middle"},
        @{Version='[*.1.0.0]';       Description="version with wilcard at start"},
        @{Version='[8.*.0.0]';       Description="version with wildcard at second digit"},
        @{Version='[8.1.*.0]';       Description="version with wildcard at third digit"}
        @{Version='[8.1.0.*';        Description="version with wildcard at end"},
        @{Version='[8..0.0]';        Description="version with missing digit in middle"},
        @{Version='[8.1.0.]';        Description="version with missing digit at end"},
        @{Version='[8.1.0.0.0]';     Description="version with more than 4 digits"}
    ) {
        param($Version, $Description)

        $res = $null
        try {
            $res = Find-PSResource -Name "NetworkingDsc" -Version $Version -Repository $PSGalleryName -ErrorAction Ignore
        }
        catch {}

        $res | Should -BeNullOrEmpty
    }

    # Purpose: find a DSCResource resource with wilcard range Version parameter -> '*'
    #
    # Action: Find-PSResource -Name NetworkingDsc -Version "*"
    #
    # Expected Result: returns all NetworkingDsc resources (with versions in descending order)
    It "find a DSCResource resource given Name with Version wildcard match " {
        $res = Find-PSResource -Name NetworkingDsc -Version "*"
        $res.Count | Should -BeGreaterOrEqual 11
    }

    # Purpose: find DSC resource with all versions (incl preview), with Prerelease parameter
    #
    # Action: Find-PSResource -Name ActiveDirectoryCSDsc -Version "*" -Prerelease
    #
    # Expected Result: should return more versions with prerelease parameter than without
    It "find DSCResource resource with all preview versions, with Prerelease parameter" {
        $res = Find-PSResource -Name ActiveDirectoryCSDsc -Version "*"
        $withoutPrereleaseVersions = $res.Count

        $resPrerelease = Find-PSResource -Name ActiveDirectoryCSDsc -Version "*" -Prerelease
        $withPrereleaseVersions = $resPrerelease.Count
        $withPrereleaseVersions | Should -BeGreaterOrEqual $withoutPrereleaseVersions
    }

    # Purpose: find a DSCResource resource, with preview version
    #
    # Action: Find-PSResource -Name ActiveDirectoryCSDsc -Prerelease
    #
    # Expected Result: should return (a later or) preview version than if without Prerelease parameter
    It "find DSCResource resource with preview version, with Prerelease parameter" {
        $res = Find-PSResource -Name ActiveDirectoryCSDsc -Repository $PSGalleryName
        $res.Version | Should -Be "5.0.0.0"

        $resPrerelease = Find-PSResource -Name ActiveDirectoryCSDsc -Prerelease -Repository $PSGalleryName
        $resPrerelease.Version | Should -Be "5.0.1.0"
    }

    # Purpose: find a DSCResource of package type module, given ModuleName parameter
    #
    # Action: Find-PSResource -ModuleName NetworkingDsc
    #
    # Expected Result: returns DSCResource with specified ModuleName
    It "find a DSCResource of package type module, given ModuleName parameter" {
        $res = Find-PSResource -ModuleName NetworkingDsc -Repository $PSGalleryName
        $res.Name | Should -Be "NetworkingDsc"
    }

    # Purpose: find a DSC resource given Name, to validate version parameter values
    #
    # Action: Find-PSResource -Name NetworkingDsc -Version [6.0.0.0]
    #
    # Expected Result: return resource meeting version criteria
    It "find DSC resource when given ModuleName to <Reason>" -TestCases @(
        @{Version="[6.0.0.0]";          ExpectedVersion="6.0.0.0"; Reason="validate version, exact match"},
        @{Version="6.0.0.0";            ExpectedVersion="6.0.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[6.0.0.0, 8.0.0.0]"; ExpectedVersion="8.0.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(6.0.0.0, 7.4.0.0)"; ExpectedVersion="7.3.0.0"; Reason="validate version, exact range exclusive"},
        <#
        @{Version="(6.0.0.0,)";         ExpectedVersion="8.1.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="[6.0.0.0,)";         ExpectedVersion="8.1.0.0"; Reason="validate version, minimum version inclusive"},
        #>
        @{Version="(,7.4.0.0)";         ExpectedVersion="7.3.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,7.4.0.0]";         ExpectedVersion="7.4.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[6.0.0.0, 7.4.0.0)"; ExpectedVersion="7.3.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -ModuleName "NetworkingDsc" -Version $Version -Repository $PSGalleryName
        $res.Name | Should -Be "NetworkingDsc"
        $res.Version | Should -Be $ExpectedVersion
    }

    # Purpose: find a DSCResource with a specific tag, given Tags parameter
    #
    # Action: Find-PSResource -Tags CommandsAndResource -Repository PoshTestGallery | Where-Object { $_.Name -eq "DscTestModule" }
    #
    # Expected Result: return DscTestModule resource
    It "find a DSCResource with specific tag, given Tags parameter"{
        $res = Find-PSResource -Tags "CommandsAndResource" -Repository $TestGalleryName | Where-Object { $_.Name -eq "DscTestModule" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "DscTestModule"
    }

    # Purpose: find a DSCResource with multiple tag, given Tags parameter
    #
    # Action: Find-PSResource -Tags CommandsAndResource,Tag-DscTestModule-2.5,Tag1 -Repository PoshTestGallery | Where-Object { $_.Name -eq "DscTestModule" }
    #
    # Expected Result: return DscTestModule resource
    It "find a DSCResource with specific tag, given Tags parameter"{
        $res = Find-PSResource -Tags "CommandsAndResource","Tag-DscTestModule-2.5","Tag1" -Repository $TestGalleryName | Where-Object { $_.Name -eq "DscTestModule" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "DscTestModule"
    }

    # Purpose: not find non-available DSCResource from repository, given Repository parameter
    #
    # Action: Find-PSResource -Name Carbon -Repository PSGallery
    #
    # Expected Result: should not AccessControlDSC from PoshTestGallery repository
    It "not find DSCResource from repository, given Repository parameter" {
        $res = Find-PSResource -Name AccessControlDSC -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find DSCResource from repository, given Repository parameter
    #
    # Action: Find-PSResource -Name Carbon -Repository PoshTestGallery
    #
    # Expected Result: should find AccessControlDSC from PSGallery repository
    It "find DSCResource from repository, given Repository parameter" {
        $res = Find-PSResource -Name AccessControlDSC -Repository $PSGalleryName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "AccessControlDSC"
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
        $res = Find-PSResource -Name AccessControlDSC -Repository NonExistantRepo
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
