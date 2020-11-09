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

    It "find a DSCResource resource given Name parameter" {
        $res = Find-PSResource -Name "test_dsc_module"
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "test_dsc_module"
    }

    It "find DSC resource when given Name to <Reason>" -TestCases @(
        @{Version="[2.0.0.0]";          ExpectedVersion="2.0.0.0"; Reason="validate version, exact match"},
        @{Version="2.0.0.0";            ExpectedVersion="2.0.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.0.0.0, 5.0.0.0]"; ExpectedVersion="5.0.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0, 5.0.0.0)"; ExpectedVersion="4.0.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="(1.0.0.0,)";         ExpectedVersion="5.0.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="[3.5.0.0,)";         ExpectedVersion="5.0.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(,5.0.0.0)";         ExpectedVersion="4.0.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,5.0.0.0]";         ExpectedVersion="5.0.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0, 5.0.0.0)"; ExpectedVersion="4.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -Name "test_dsc_module" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "test_dsc_module"
        $res.Version | Should -Be $ExpectedVersion
    }

    It "not find resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(2.5.0.0)';       Description="exclusive version (2.5.0.0)"},
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

    It "find resources when given Name, Version not null --> '*' " {
        $unexpectedModules = @()
        Find-PSResource -Name "test_module" -Version "*" -Repository $TestGalleryName | ForEach-Object {
            if($_.Name -ne "test_dsc_module") {
                $unexpectedModules += $_.Name
            }
        }
        $unexpectedModules.Count | Should -Be 0
    }

    It "find DSC resource when given ModuleName to <Reason>" -TestCases @(
        @{Version="[2.0.0.0]";          ExpectedVersion="2.0.0.0"; Reason="validate version, exact match"},
        @{Version="2.0.0.0";            ExpectedVersion="2.0.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.0.0.0, 5.0.0.0]"; ExpectedVersion="5.0.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0, 5.0.0.0)"; ExpectedVersion="4.0.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="(1.0.0.0,)";         ExpectedVersion="5.0.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="[3.5.0.0,)";         ExpectedVersion="5.0.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(,5.0.0.0)";         ExpectedVersion="4.0.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,5.0.0.0]";         ExpectedVersion="5.0.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0, 5.0.0.0)"; ExpectedVersion="4.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -ModuleName "test_dsc_module" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "test_dsc_module"
        $res.Version | Should -Be $ExpectedVersion
    }

    It "find resources when given ModuleName, Version not null --> '*' " {
        $unexpectedModules = @()
        Find-PSResource -ModuleName "test_module" -Version "*" -Repository $TestGalleryName | ForEach-Object {
            if($_.Name -ne "test_dsc_module") {
                $unexpectedModules += $_.Name
            }
        }
        $unexpectedModules.Count | Should -Be 0
    }

    It "find resource when given ModuleName, Version param null" {
        $res = Find-PSResource -ModuleName "test_dsc_module" -Repository $TestGalleryName
        $res.Name | Should -Be "test_dsc_module"
        $res.Version | Should -Be "5.0.0.0"
    }

    It "find resource with latest version (including preview versions), with Prerelease parameter" {
        $res = Find-PSResource -Name "test_dsc_module" -Repository $TestGalleryName
        $res.Version | Should -Be "5.0.0.0"

        $resPrerelease = Find-PSResource -Name "test_dsc_module" -Prerelease -Repository $TestGalleryName
        $resPrerelease.Version | Should -Be "5.2.5.0"
    }

    It "find a DSCResource with specific tag, given Tags parameter"{
        $res = Find-PSResource -Tags "DSC" -Repository $TestGalleryName | Where-Object { $_.Name -eq "test_dsc_module" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "test_dsc_module"
    }

    It "find a DSCResource with specific tag, given Tags parameter"{
        $resSingleTag = Find-PSResource -Tags "DSC" -Repository $TestGalleryName
        $resMultipleTags = Find-PSResource -Tags "DSC","PSDscResource_" -Repository $TestGalleryName
        $resMultipleTags.Count | Should -BeGreaterOrEqual $resSingleTag.Count

        $res = $resMultipleTags | Where-Object { $_.Name -eq "test_dsc_module" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "test_dsc_module"
    }

    It "not find DSCResource from repository, given Repository parameter" {
        $res = Find-PSResource -Name "test_dsc_module" -Repository $PSGalleryName
        $res | Should -BeNullOrEmpty
    }

    It "find DSCResource from repository, given Repository parameter" {
        $res = Find-PSResource -Name "test_dsc_module" -Repository $TestGalleryName
        $res.Repository | Should -Be $TestGalleryName
        $res.Name | Should -Be "test_dsc_module"
        $res.Version | Should -Be "5.0.0.0"
    }

    It "find resource in local repository given Repository parameter"{
        $publishDscName = "TestFindDSCModule"
        $repoName = "psgettestlocal"
        Get-DSCResourcePublishedToLocalRepoTestDrive $publishDscName $repoName

        $res = Find-PSResource -Name $publishDscName -Repository $repoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be $publishDscName
        $res.Repository | Should -Be $repoName
    }

    It "find Resource given repository parameter, where resource exists in multiple LOCAL repos" {
        $dscName = "test_local_dsc"
        $repoHigherPriorityRanking = "psgettestlocal"
        $repoLowerPriorityRanking = "psgettestlocal2"

        Get-DSCResourcePublishedToLocalRepoTestDrive $dscName $repoHigherPriorityRanking
        Get-DSCResourcePublishedToLocalRepoTestDrive $dscName $repoLowerPriorityRanking

        $res = Find-PSResource -Name $dscName
        $res.Repository | Should -Be $repoHigherPriorityRanking

        $resNonDefault = Find-PSResource -Name $dscName -Repository $repoLowerPriorityRanking
        $resNonDefault.Repository | Should -Be $repoLowerPriorityRanking
    }

    It "find DSCResource resource with preview version, with Prerelease parameter" {
        $res = Find-PSResource -Name "test_dsc_module" -Repository $TestGalleryName
        $res.Version | Should -Be "5.0.0.0"

        $resPrerelease = Find-PSResource -Name "test_dsc_module" -Prerelease -Repository $TestGalleryName
        $resPrerelease.Version | Should -Be "5.2.5.0"
    }

    It "find resource with IncludeDependencies parameter" {
        $res = Find-PSResource -Name "test_dsc_module" -IncludeDependencies
        $res.Count | Should -Be 6
    }
}
