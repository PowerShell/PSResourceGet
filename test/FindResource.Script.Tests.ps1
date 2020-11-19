# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -force

Describe "Test Find-PSResource for Script" {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        $ScriptTest = Get-ScriptTest
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
        Unregister-LocalRepos
    }

    It "find resource given Name parameter" {
        $res = Find-PSResource -Name "test_script"
        $res.Name | Should -Be "test_script"
    }

    It "find resource when given Name to <Reason>" -TestCases @(
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
        $res = Find-PSResource -Name "test_script" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "test_script"
        $res.Version | Should -Be $ExpectedVersion
    }

    It "not find resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(1.5.0.0)';       Description="exclusive version (1.5.0.0)"},
        @{Version='[1-5-0-0]';       Description="version formatted with invalid delimiter"}
    ) {
        param($Version, $Description)

        $res = Find-PSResource -Name "test_script" -Version $Version -Repository $TestGalleryName -ErrorAction Ignore
        $res | Should -BeNullOrEmpty
    }

    It "not find resource and throw exception with incorrectly formatted version such as <Description>" -TestCases @(
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
        {Find-PSResource -Name "test_script" -Version $Version -Repository $TestGalleryName -ErrorAction Ignore} | Should -Throw -ExceptionType ([ArgumentException])
    }

    It "find resources when given Name, Version not null --> '*'" {
        $actualModules = @()
        Find-PSResource -Name "test_script" -Version "*" -Repository $TestGalleryName | ForEach-Object {
            if($_.Name -eq "test_script") {
                $actualModules += $_.Name
            }
        }
        $actualModules.Count | Should -Be $ScriptTest.Count
    }

    It "not find script resource when given ModuleName" {
        $res = Find-PSResource -ModuleName "test_script" -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }

    It "not find script resource when given ModuleName to <Reason>" -TestCases @(
        @{Version="[2.0.0.0]";          Reason="validate version, exact match"},
        @{Version="2.0.0.0";            Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.0.0.0, 2.5.0.0]"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0, 2.5.0.0)"; Reason="validate version, exact range exclusive"},
        @{Version="(1.0.0.0,)";         Reason="validate version, minimum version exclusive"},
        @{Version="[1.0.0.0,)";         Reason="validate version, minimum version inclusive"},
        @{Version="(,1.5.0.0)";         Reason="validate version, maximum version exclusive"},
        @{Version="(,1.5.0.0]";         Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0, 2.5.0.0)"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -ModuleName "test_script" -Version $Version -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }

    It "not find script resource with specified ModuleName and range Version parameter -> '*' " {
        $res = Find-PSResource -ModuleName "test_script" -Version "*" -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }

    It "find resource with latest (including prerelease) version given Prerelease parameter" {
        # test_script resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Find-PSResource -Name "test_script" -Repository $TestGalleryName
        $res.Version | Should -Be "2.5.0.0"

        $resPrerelease = Find-PSResource -Name "test_script" -Prerelease -Repository $TestGalleryName
        $resPrerelease.Version | Should -Be "3.0.0.0"
    }

    It "not find resource given Repository parameter where resource does not exist" {
        $res = Find-PSResource -Name "test_script" -Repository $PSGalleryName
        $res | Should -BeNullOrEmpty
    }

    It "find resource given repository parameter where resource does exist" {
        $res = Find-PSResource -Name "test_script" -Repository $TestGalleryName
        $res.Repository | Should -Be $TestGalleryName
        $res.Name | Should -Be "test_script"
        $res.Version | Should -Be "2.5.0.0"
    }

    It "find resource in local repository given Repository parameter" {
        $scriptName = "TestScriptName"
        $repoName = "psgettestlocal"
        Get-ScriptResourcePublishedToLocalRepoTestDrive $scriptName $repoName

        $res = Find-PSResource -Name $scriptName -Repository $repoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be $scriptName
        $res.Repository | Should -Be $repoName
    }

    It "find Resource given repository parameter, where resource exists in multiple LOCAL repos" {
        $scriptName = "test_local_script"
        $repoHigherPriorityRanking = "psgettestlocal"
        $repoLowerPriorityRanking = "psgettestlocal2"

        Get-ScriptResourcePublishedToLocalRepoTestDrive $scriptName $repoHigherPriorityRanking
        Get-ScriptResourcePublishedToLocalRepoTestDrive $scriptName $repoLowerPriorityRanking

        $res = Find-PSResource -Name $scriptName
        $res.Repository | Should -Be $repoHigherPriorityRanking

        $resNonDefault = Find-PSResource -Name $scriptName -Repository $repoLowerPriorityRanking
        $resNonDefault.Repository | Should -Be $repoLowerPriorityRanking
    }

    It "find resource with IncludeDependencies parameter" {
        $res = Find-PSResource -Name "test_script" -IncludeDependencies
        $res.Count | Should -Be 9
    }
}
