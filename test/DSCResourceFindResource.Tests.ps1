# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -WarningAction SilentlyContinue -force

Describe 'Test Find-PSResource for Command' {

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
        @{Version="[6.0.0.0, 8.0.0.0]"; ExpectedVersion="8.0.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(6.0.0.0, 7.4.0.0)"; ExpectedVersion="7.3.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="(6.0.0.0,)";         ExpectedVersion="8.1.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="(,7.4.0.0)";         ExpectedVersion="7.3.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,7.4.0.0]";         ExpectedVersion="7.4.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[6.0.0.0, 7.4.0.0)"; ExpectedVersion="7.3.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -Name "NetworkingDsc" -Version $Version -Repository (Get-PSGalleryName)
        $res.Name | Should -Be "NetworkingDsc"
        $res.Version | Should -Be $ExpectedVersion
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
        $res = Find-PSResource -Name ActiveDirectoryCSDsc -Repository (Get-PSGalleryName)
        $res.Version | Should -Be "5.0.0.0"

        $resPrerelease = Find-PSResource -Name ActiveDirectoryCSDsc -Prerelease -Repository (Get-PSGalleryName)
        $resPrerelease.Version | Should -Be "5.0.1.0"
    }


    # Purpose: find a DSCResource of package type module, given ModuleName parameter
    #
    # Action: Find-PSResource -ModuleName NetworkingDsc
    #
    # Expected Result: returns DSCResource with specified ModuleName
    It "find a DSCResource of package type module, given ModuleName parameter" {
        $res = Find-PSResource -ModuleName NetworkingDsc -Repository (Get-PSGalleryName)
        $res.Name | Should -Be "NetworkingDsc"
    }

    # Purpose: find a DSC resource given Name, to validate version parameter values
    #
    # Action: Find-PSResource -Name NetworkingDsc -Version [6.0.0.0]
    #
    # Expected Result: return resource meeting version criteria
    It "find DSC resource when given ModuleName to <Reason>" -TestCases @(
        @{Version="[6.0.0.0]";          ExpectedVersion="6.0.0.0"; Reason="validate version, exact match"},
        @{Version="[6.0.0.0, 8.0.0.0]"; ExpectedVersion="8.0.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(6.0.0.0, 7.4.0.0)"; ExpectedVersion="7.3.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="(6.0.0.0,)";         ExpectedVersion="8.1.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="(,7.4.0.0)";         ExpectedVersion="7.3.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,7.4.0.0]";         ExpectedVersion="7.4.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[6.0.0.0, 7.4.0.0)"; ExpectedVersion="7.3.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -ModuleName "NetworkingDsc" -Version $Version -Repository (Get-PSGalleryName)
        $res.Name | Should -Be "NetworkingDsc"
        $res.Version | Should -Be $ExpectedVersion
    }


    # Purpose: find a DSCResource with a specific tag, given Tags parameter
    #
    # Action: Find-PSResource -Tags CommandsAndResource -Repository PoshTestGallery | Where-Object { $_.Name -eq "DscTestModule" }
    #
    # Expected Result: return DscTestModule resource
    It "find a DSCResource with specific tag, given Tags parameter"{
        $tagValue = "CommandsAndResource"
        $res = Find-PSResource -Tags $tagValue -Repository (Get-PoshTestGalleryName) | Where-Object { $_.Name -eq "DscTestModule" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "DscTestModule"
    }

    # Purpose: find a DSCResource with multiple tag, given Tags parameter
    #
    # Action: Find-PSResource -Tags CommandsAndResource,Tag-DscTestModule-2.5,Tag1 -Repository PoshTestGallery | Where-Object { $_.Name -eq "DscTestModule" }
    #
    # Expected Result: return DscTestModule resource
    It "find a DSCResource with specific tag, given Tags parameter"{
        $tagValue1 = "CommandsAndResource"
        $tagValue2 = "Tag-DscTestModule-2.5"
        $tagValue3 = "Tag1"
        $res = Find-PSResource -Tags $tagValue1,$tagValue2,$tagValue3 -Repository (Get-PoshTestGalleryName) | Where-Object { $_.Name -eq "DscTestModule" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "DscTestModule"
    }

    # Purpose: not find non-available DSCResource from repository, given Repository parameter
    #
    # Action: Find-PSResource -Name AccessControlDSC -Repository PoshTestGallery
    #
    # Expected Result: should not AccessControlDSC from PoshTestGallery repository
    It "not find DSCResource from repository, given Repository parameter" {
        $res = Find-PSResource -Name AccessControlDSC -Repository (Get-PoshTestGalleryName)
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find DSCResource from repository, given Repository parameter
    #
    # Action: Find-PSResource -Name AccessControlDSC -Repository PSGallery
    #
    # Expected Result: should find AccessControlDSC from PSGallery repository
    It "find DSCResource from repository, given Repository parameter" {
        $res = Find-PSResource -Name AccessControlDSC -Repository (Get-PSGalleryName)
        $res.Name | Should -Be "AccessControlDSC"
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
}
