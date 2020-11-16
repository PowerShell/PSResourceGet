# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Find-PSResource for Module' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        $ModuleTest = Get-ModuleTestModule
        Get-NewPSResourceRepositoryFile
        Get-RegisterLocalRepos
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
        Get-UnregisterLocalRepos
    }

    It "find Specific Module Resource by Name" {
        $specItem = Find-PSResource -Name "test_module"
        $specItem.Name | Should -Be "test_module"
    }

    It "should not find resource given nonexistant name" {
        $res = Find-PSResource -Name NonExistantModule
        $res | Should -BeNullOrEmpty
    }

    It "find resource when given Name to <Reason> <Version>" -TestCases @(
        @{Version="[2.0.0.0]";          ExpectedVersion="2.0.0.0"; Reason="validate version, exact match"},
        @{Version="2.0.0.0";            ExpectedVersion="2.0.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.2.0.0, 5.0.0.0]"; ExpectedVersion="5.0.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(1.2.0.0, 5.0.0.0)"; ExpectedVersion="4.0.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="(1.2.0.0,)";         ExpectedVersion="5.0.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="[1.2.0.0,)";         ExpectedVersion="5.0.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(,5.0.0.0)";         ExpectedVersion="4.0.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,5.0.0.0]";         ExpectedVersion="5.0.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[1.2.0.0, 5.0.0.0)"; ExpectedVersion="4.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -Name "test_module" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "test_module"
        $res.Version | Should -Be $ExpectedVersion
    }

    It "not find resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(1.5.0.0)';       Description="exclusive version (1.5.0.0)"},
        @{Version='[1-5-0-0]';       Description="version formatted with invalid delimiter"}
    ) {
        param($Version, $Description)

        $res = Find-PSResource -Name "test_module" -Version $Version -Repository $TestGalleryName -ErrorAction Ignore
        $res | Should -BeNullOrEmpty
    }

    It "not find resource and throw error with incorrectly formatted version such as <Description>" -TestCases @(
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

        # $res = $null
        # try {
        #     $res = Find-PSResource -Name "test_module" -Version $Version -Repository $TestGalleryName -ErrorAction Ignore
        # }
        # catch {}
        # $res | Should -BeNullOrEmpty
        {Find-PSResource -Name "test_module" -Version $Version -Repository $TestGalleryName -ErrorAction Ignore} | Should -Throw "'$Version' is not a valid version string."
    }

    It "find resources when given Name, Version not null --> '*'" {
        $expectedModules = @()
        Find-PSResource -Name "test_module" -Version "*" -Repository $TestGalleryName | ForEach-Object {
            if($_.Name -eq "test_module") {
                $expectedModules += $_.Name
            }
        }
        $expectedModules.Count | Should -Be $ModuleTest.Count
    }

    It "find resource when given ModuleName to <Reason>" -TestCases @(
        @{Version="[2.0.0.0]";          ExpectedVersion="2.0.0.0"; Reason="validate version, exact match"},
        @{Version="2.0.0.0";            ExpectedVersion="2.0.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.2.0.0, 5.0.0.0]"; ExpectedVersion="5.0.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(1.2.0.0, 5.0.0.0)"; ExpectedVersion="4.0.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="(1.2.0.0,)";         ExpectedVersion="5.0.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="[1.2.0.0,)";         ExpectedVersion="5.0.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(,5.0.0.0)";         ExpectedVersion="4.0.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,5.0.0.0]";         ExpectedVersion="5.0.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[1.2.0.0, 5.0.0.0)"; ExpectedVersion="4.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ){
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -ModuleName "test_module" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "test_module"
        $res.Version | Should -Be $ExpectedVersion
    }

    It "find resources when given ModuleName, Version not null --> '*'" {
        $expectedModules = @()
        Find-PSResource -ModuleName "test_module" -Version "*" -Repository $TestGalleryName | ForEach-Object {
            if($_.Name -eq "test_module") {
                $expectedModules += $_.Name
            }
        }
        $expectedModules.Count | Should -Be $ModuleTest.Count
    }

    It "find resource when given ModuleName, Version param null" {
        $res = Find-PSResource -ModuleName "test_module" -Repository $TestGalleryName
        $res.Name | Should -Be "test_module"
        $res.Version | Should -Be "5.0.0.0"
    }

    It "find resource with latest (including prerelease) version given Prerelease parameter" {
        # test_module resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Find-PSResource -Name "test_module" -Repository $TestGalleryName
        $res.Version | Should -Be "5.0.0.0"

        $resPrerelease = Find-PSResource -Name "test_module" -Prerelease -Repository $TestGalleryName
        $resPrerelease.Version | Should -Be "5.2.5.0"
    }

    It "find a resource given Tags parameter with one value" {
        $res = Find-PSResource -Tags "Test" | Where-Object { $_.Name -eq "test_module" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "test_module"
    }

    It "find a resource given tags parameter with multiple values" {
        $resSingleTag = Find-PSResource -Tags "Test" -Repository $TestGalleryName
        $resMultipleTags = Find-PSResource -Tags "Test","CommandsAndResource","Tag2" -Repository $TestGalleryName
        $resMultipleTags.Count | Should -BeGreaterOrEqual $resSingleTag.Count

        $res = $resMultipleTags | Where-Object { $_.Name -eq "test_module" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "test_module"
    }

    It "not find resource given repository parameter where resource does not exist" {
        $res = Find-PSResource "test_module" -Repository $PSGalleryName
        $res | Should -BeNullOrEmpty
    }

    It "find resource given repository parameter where resource does exist" {
        $res = Find-PSResource "test_module" -Repository $TestGalleryName
        $res.Repository | Should -Be $TestGalleryName
        $res.Name | Should -Be "test_module"
        $res.Version | Should -Be "5.0.0.0"
    }

    It "find resource in local repository given Repository parameter" {
        $publishModuleName = "TestFindModule"
        $repoName = "psgettestlocal"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $publishModuleName $repoName

        $res = Find-PSResource -Name $publishModuleName -Repository $repoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be $publishModuleName
        $res.Repository | Should -Be $repoName
    }

    It "find Resource given repository parameter, where resource exists in multiple LOCAL repos" {
        $moduleName = "test_local_mod"
        $repoHigherPriorityRanking = "psgettestlocal"
        $repoLowerPriorityRanking = "psgettestlocal2"

        Get-ModuleResourcePublishedToLocalRepoTestDrive $moduleName $repoHigherPriorityRanking
        Get-ModuleResourcePublishedToLocalRepoTestDrive $moduleName $repoLowerPriorityRanking

        $res = Find-PSResource -Name $moduleName
        $res.Repository | Should -Be $repoHigherPriorityRanking

        $resNonDefault = Find-PSResource -Name $moduleName -Repository $repoLowerPriorityRanking
        $resNonDefault.Repository | Should -Be $repoLowerPriorityRanking
    }

    It "find resource with IncludeDependencies parameter" {
        $res = Find-PSResource -Name "test_module" -IncludeDependencies
        $res.Count | Should -Be 6
    }
}
