# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -force

Describe 'Test Find-PSResource for Role Capability' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        Get-NewPSResourceRepositoryFile
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "find Role Capability resource, given Name parameter" {
        $res = Find-PSResource -Name "test_rolecap_module"
        $res.Name | Should -Be "test_rolecap_module"
    }

    It "find resource when given Name to <Reason>" -TestCases @(
        @{Version="[1.0.0.0]";          ExpectedVersion="1.0.0.0"; Reason="validate version, exact match"},
        @{Version="1.5.0.0";            ExpectedVersion="1.5.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.0.0.0, 2.5.0.0]"; ExpectedVersion="2.5.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="(1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="[1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(,2.5.0.0)";         ExpectedVersion="2.0.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,2.5.0.0]";         ExpectedVersion="2.5.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -Name "test_rolecap_module" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "test_rolecap_module"
        $res.Version | Should -Be $ExpectedVersion
    }

    It "not find resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(2.5.0.0)';       Description="exlcusive version (2.5.0.0)"},
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
            $res = Find-PSResource -Name "test_rolecap_module" -Version $Version -Repository $TestGalleryName -ErrorAction Ignore
        }
        catch {}
        
        $res | Should -BeNullOrEmpty
    }

    It "find Role Capability resource with wildcard version, given Version parameter -> '*' " {
        $res = Find-PSResource -Name "test_rolecap_module" -Version "*" -Repository $TestGalleryName
        $res.Count | Should -BeGreaterOrEqual 1
    }

    It "find resource when given Name to <Reason>" -TestCases @(
        @{Version="[1.0.0.0]";          ExpectedVersion="1.0.0.0"; Reason="validate version, exact match"},
        @{Version="1.5.0.0";            ExpectedVersion="1.5.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.0.0.0, 2.5.0.0]"; ExpectedVersion="2.5.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="(1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="[1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(,2.5.0.0)";         ExpectedVersion="2.0.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,2.5.0.0]";         ExpectedVersion="2.5.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -ModuleName "test_rolecap_module" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "test_rolecap_module"
        $res.Version | Should -Be $ExpectedVersion
    }


    It "find resource given ModuleName to validate version wildcard match " {
        $res = Find-PSResource -ModuleName "test_rolecap_module" -Version "*" -Repository $TestGalleryName
        $res.Count | Should -Be 6
    }

    It "find resource given ModuleName with Version null or empty" {
        $res = Find-PSResource -ModuleName "test_rolecap_module" -Repository $TestGalleryName
        $res.Name | Should -Be "test_rolecap_module"
        $res.Version | Should -Be "2.5.0.0"
    }

    It "find resource with latest version (including preview versions), with Prerelease parameter" {
        $res = Find-PSResource -Name "test_rolecap_module" -Repository $TestGalleryName
        $res.Version | Should -Be "2.5.0.0"

        $resPrerelease = Find-PSResource -Name "test_rolecap_module" -Prerelease -Repository $TestGalleryName
        $resPrerelease.Version | Should -Be "2.6.2.0"
    }

    It "find Role Capability resource with given Tags parameter" {
        $res = Find-PSResource -Tags "Tag1" -Repository $TestGalleryName | Where-Object { $_.Name -eq "test_rolecap_module" }
        $res.Name | Should -Be "test_rolecap_module"
    }

    It "find Role Capability resource with given Tags parameter" {
        $resSingleTag = Find-PSResource -Tags "Tag1" -Repository $TestGalleryName
        $resMultipleTags = Find-PSResource -Tags "Roles","Tag1", "Tag2" -Repository $TestGalleryName
        $resMultipleTags.Count | Should -BeGreaterOrEqual $resSingleTag.Count

        $res = $resMultipleTags | Where-Object { $_.Name -eq "test_rolecap_module" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "test_rolecap_module"
    }

    It "not find Role Capability resource that doesn't exist in specified repository, given Repository parameter" {
        $res = Find-PSResource -Name "test_rolecap_module" -Repository $PSGalleryName
        $res | Should -BeNullOrEmpty
    }

    It "find resource that exists only in a specific repository, given Repository parameter" {
        $res = Find-PSResource -Name "test_rolecap_module" -Repository $TestGalleryName
        $res.Repository | Should -Be $TestGalleryName
        $res.Name | Should -Be "test_rolecap_module"
        $res.Version | Should -Be "2.5.0.0"
    }

    It "find resource in local repository given Repository parameter" {
        $roleCapName = "TestFindRoleCapModule"
        $repoName = "psgettestlocal"
        Get-RoleCapabilityResourcePublishedToLocalRepoTestDrive $roleCapName $repoName

        $res = Find-PSResource -Name $roleCapName -Repository $repoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be $roleCapName
        $res.Repository | Should -Be $repoName
    }

    It "find Resource given repository parameter, where resource exists in multiple LOCAL repos" {
        $roleCapName = "test_local_rolecap"
        $repoHigherPriorityRanking = "psgettestlocal"
        $repoLowerPriorityRanking = "psgettestlocal2"

        Get-RoleCapabilityResourcePublishedToLocalRepoTestDrive $roleCapName $repoHigherPriorityRanking
        Get-RoleCapabilityResourcePublishedToLocalRepoTestDrive $roleCapName $repoLowerPriorityRanking

        $res = Find-PSResource -Name $roleCapName
        $res.Repository | Should -Be $repoHigherPriorityRanking

        $resNonDefault = Find-PSResource -Name $roleCapName -Repository $repoLowerPriorityRanking
        $resNonDefault.Repository | Should -Be $repoLowerPriorityRanking
    }

    It "find resource with IncludeDependencies parameter" {
        $res = Find-PSResource -Name "test_rolecap_module" -IncludeDependencies
        $res.Count | Should -Be 6
    }
}
