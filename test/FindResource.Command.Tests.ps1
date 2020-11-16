# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Find-PSResource for Command' {

    BeforeAll{
       $TestGalleryName = Get-PoshTestGalleryName
       $PSGalleryName = Get-PSGalleryName
       $CommandTest = Get-CommandTestModule
       Get-NewPSResourceRepositoryFile
       Get-RegisterLocalRepos
    }

    AfterAll {
       Get-RevertPSResourceRepositoryFile
       Get-UnregisterLocalRepos
    }

    It "find Command resource given Name parameter" {
        $res = Find-PSResource -Name "test_command_module"
        $res.Name | Should -Be "test_command_module"
    }

    It "find Command resource given Name to <Reason>" -TestCases @(
        @{Version="[1.5.0.0]";          ExpectedVersion="1.5.0.0"; Reason="validate version, exact match"},
        @{Version="1.5.0.0";            ExpectedVersion="1.5.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.0.0.0, 2.5.0.0]"; ExpectedVersion="2.5.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="[2.5.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(2.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="(,2.5.0.0)";         ExpectedVersion="2.0.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,2.5.0.0]";         ExpectedVersion="2.5.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -Name "test_command_module" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "test_command_module"
        $res.Version | Should -Be $ExpectedVersion
    }

    It "not find resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(2.5.0.0)';       Description="exclusive version (2.5.0.0)"},
        @{Version='[2-5-0-0]';       Description="version formatted with invalid delimiter"}
    ) {
        param($Version, $Description)

        $res = Find-PSResource -Name "test_command_module" -Version $Version -Repository $TestGalleryName -ErrorAction Ignore
        $res | Should -BeNullOrEmpty
    }

    It "not find resource and throw error with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='[2.*.0]';         Description="version with wilcard in middle"},
        @{Version='[*.5.0.0]';       Description="version with wilcard at start"},
        @{Version='[2.*.0.0]';       Description="version with wildcard at second digit"},
        @{Version='[2.5.*.0]';       Description="version with wildcard at third digit"}
        @{Version='[2.5.0.*]';       Description="version with wildcard at end"},
        @{Version='[2..0.0]';        Description="version with missing digit in middle"},
        @{Version='[2.5.0.]';        Description="version with missing digit at end"},
        @{Version='[2.5.0.0.0]';     Description="version with more than 4 digits"}
    ) {
        param($Version, $Description)

        {Find-PSResource -Name "test_command_module" -Version $Version -Repository $TestGalleryName -ErrorAction Ignore} | Should -Throw "'$Version' is not a valid version string."
    }

    It "find resources when given Name, Version not null --> '*'" {
        $expectedModules = @()
        Find-PSResource -Name "test_command_module" -Version "*" -Repository $TestGalleryName | ForEach-Object {
            if($_.Name -eq "test_command_module") {
                $expectedModules += $_.Name
            }
        }
        $expectedModules.Count | Should -Be $CommandTest.Count
    }

    It "find Command resource given ModuleName to <Reason>" -TestCases @(
        @{Version="[1.5.0.0]";          ExpectedVersion="1.5.0.0"; Reason="validate version, exact match"},
        @{Version="1.5.0.0";            ExpectedVersion="1.5.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.0.0.0, 2.5.0.0]"; ExpectedVersion="2.5.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="[2.5.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(2.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="(,2.5.0.0)";         ExpectedVersion="2.0.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,2.5.0.0]";         ExpectedVersion="2.5.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -ModuleName "test_command_module" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "test_command_module"
        $res.Version | Should -Be $ExpectedVersion
    }

    It "find resources when given ModuleName, Version not null --> '*'" {
        $expectedModules = @()
        Find-PSResource -ModuleName "test_command_module" -Version "*" -Repository $TestGalleryName | ForEach-Object {
            if($_.Name -eq "test_command_module") {
                $expectedModules += $_.Name
            }
        }
        $expectedModules.Count | Should -Be $CommandTest.Count
    }

    It "find Command resource given ModuleName with Version null or empty" {
        $res = Find-PSResource -ModuleName "test_command_module" -Repository $TestGalleryName
        $res.Name | Should -Be "test_command_module"
        $res.Version | Should -Be "2.5.0.0"
    }

    It "find Command resource with latest version (including preview versions), with Prerelease parameter" {
        $res = Find-PSResource -Name "test_command_module" -Repository $TestGalleryName
        $res.Version | Should -Be "2.5.0.0"

        $resPrerelease = Find-PSResource -Name "test_command_module" -Prerelease -Repository $TestGalleryName
        $resPrerelease.Version | Should -Be "2.6.5.0"
    }

    It "find resource with single tag, given Tags parameter" {
        $res = Find-PSResource -Tags "Test" -Repository $TestGalleryName | Where-Object { $_.Name -eq "test_command_module" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "test_command_module"
    }

    It "find resource with multiple tags, given Tags parameter" {
        $resSingleTag = Find-PSResource -Tags "Test" -Repository $TestGalleryName
        $resMultipleTags = Find-PSResource -Tags "Test", "Subscription", "Tag1" -Repository $TestGalleryName
        $resMultipleTags.Count | Should -BeGreaterOrEqual $resSingleTag.Count

        $res = $resMultipleTags | Where-Object { $_.Name -eq "test_command_module" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "test_command_module"
    }

    It "not find resource given Repository parameter where resource does not exist" {
        $res = Find-PSResource -Name "test_command_module" -Repository $PSGalleryName
        $res | Should -BeNullOrEmpty
    }

    It "find resource given repository parameter where resource does exist" {
        $res = Find-PSResource -Name "test_command_module" -Repository $TestGalleryName
        $res.Name | Should -Be "test_command_module"
        $res.Version | Should -Be "2.5.0.0"
    }

    It "find resource in local repository given Repository parameter" {
        $publishCmdName = "TestFindCommandModule"
        $repoName = "psgettestlocal"
        Get-CommandResourcePublishedToLocalRepoTestDrive $publishCmdName $repoName

        $res = Find-PSResource -Name $publishCmdName -Repository $repoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be $publishCmdName
        $res.Repository | Should -Be $repoName
    }

    It "find Resource given repository parameter, where resource exists in multiple LOCAL repos" {
        $cmdName = "test_local_cmd"
        $repoHigherPriorityRanking = "psgettestlocal"
        $repoLowerPriorityRanking = "psgettestlocal2"

        Get-CommandResourcePublishedToLocalRepoTestDrive $cmdName $repoHigherPriorityRanking
        Get-CommandResourcePublishedToLocalRepoTestDrive $cmdName $repoLowerPriorityRanking

        $res = Find-PSResource -Name $cmdName
        $res.Repository | Should -Be $repoHigherPriorityRanking

        $resNonDefault = Find-PSResource -Name $cmdName -Repository $repoLowerPriorityRanking
        $resNonDefault.Repository | Should -Be $repoLowerPriorityRanking
    }

    It "find resource with IncludeDependencies parameter" {
        $res = Find-PSResource -Name "test_command_module" -IncludeDependencies
        $res.Count | Should -Be 6
    }
}
