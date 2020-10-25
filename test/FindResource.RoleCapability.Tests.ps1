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

    # Purpose: find Role Capability resource, given Name parameter
    #
    # Action: Find-PSResource -Name TestRoleCapModule
    #
    # Expected Result: return TestRoleCapModule resource
    It "find Role Capability resource, given Name parameter" {
        Write-Verbose -Verbose "using test_rolecap_module"
        $res = Find-PSResource -Name "test_rolecap_module"
        $res.Name | Should -Be "test_rolecap_module"
    }

    # Purpose: not find non-existant Role Capability resource, given Name parameter
    #
    # Action: Find-PSResource -Name NonExistantRoleCap
    #
    # Expected Result: should not return any resource
    It "not find non-existant Role Capability resource, given Name parameter" {
        $res = Find-PSResource -Name "NonExistantRoleCap"
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find a RoleCapability resource given Name, to validate version parameter values
    #
    # Action: Find-PSResource -Name DscTestModule -Version [2.0.0.0]
    #
    # Expected Result: return resource meeting version criteria
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

    # Purpose: not find resources with invalid version
    #
    # Action: Find-PSResource -Name "DscTestModule" -Version "(2.5.0.0)"
    #
    # Expected Result: should not return a resource
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

    # Purpose: find Role Capability resource with wildcard version, given Version parameter
    #
    # Action: Find-PSResource -Name "TestRoleCapModule" -Version "*"
    #
    # Expected Result: returns all versions of DSCTestModule (versions in descending order)
    It "find Role Capability resource with wildcard version, given Version parameter -> '*' " {
        $res = Find-PSResource -Name "test_rolecap_module" -Version "*" -Repository $TestGalleryName
        $res.Count | Should -BeGreaterOrEqual 1
    }
    
    # Purpose: find Role Capability resource, given ModuleName parameter
    #
    # Action: Find-PSResource -ModuleName JeaExamples -Repository PSGallery
    #
    # Expected Result: should return JeaExamples resource
    It "find Role Capability resource, given ModuleName parameter" {
        $res = Find-PSResource -ModuleName "test_rolecap_module" -Repository $TestGalleryName
        $res.Name | Should -Be "test_rolecap_module"
    }

    # Purpose: find a RoleCapability resource given ModuleName, to validate version parameter values
    #
    # Action: Find-PSResource -ModuleName DscTestModule -Version [2.0.0.0]
    #
    # Expected Result: return resource meeting version criteria
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

    # Purpose: find Role Capability resource with given Tags parameter
    #
    # Action: Find-PSResource -Tags "CommandsAndResource" -Repository PoshTestGallery | Where-Object { $_.Name -eq "test_rolecap_module" }
    #
    # Expected Result: should return test_rolecap_module resource
    It "find Role Capability resource with given Tags parameter" {
        $res = Find-PSResource -Tags "Tag1" -Repository $TestGalleryName | Where-Object { $_.Name -eq "test_rolecap_module" }
        $res.Name | Should -Be "test_rolecap_module"
    }

    # Purpose: find Role Capability resource with multiple given Tags parameter
    #
    # Action: Find-PSResource -Tags "CommandsAndResource","Tag2", "PSGet" -Repository PoshTestGallery | Where-Object { $_.Name -eq "test_rolecap_module" }
    #
    # Expected Result: should return test_rolecap_module resource
    It "find Role Capability resource with given Tags parameter" {
        $res = Find-PSResource -Tags "Roles","Tag1", "Tag2" -Repository $TestGalleryName | Where-Object { $_.Name -eq "test_rolecap_module" }
        $res.Name | Should -Be "test_rolecap_module"
    }

    # Purpose: not find Role Capability resource that doesn't exist in specified repository, given Repository parameter
    #
    # Action: Find-PSResource -Name test_rolecap_module -Repository PoshTestGallery
    #
    # Expected Result: should not find test_rolecap_module resource
    It "not find Role Capability resource that doesn't exist in specified repository, given Repository parameter" {
        $res = Find-PSResource -Name "test_rolecap_module" -Repository $PSGalleryName
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find Role Capability resource that exists only in specified repository, given Repository parameter
    #
    # Action: Find-PSResource -Name test_rolecap_module -Repository $PSGalleryName
    #
    # Expected Result: should find test_rolecap_module resource
    It "find resource that exists only in a specific repository, given Repository parameter" {
        $resRightRepo = Find-PSResource -Name "test_rolecap_module" -Repository $TestGalleryName
        $resRightRepo.Name | Should -Be "test_rolecap_module"
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

    It "find resource in local repository given Repository parameter" {
        $roleCapName = "TestFindRoleCapModule"
        $repoName = "psgettestlocal"
        Get-RoleCapabilityResourcePublishedToLocalRepoTestDrive $roleCapName $repoName

        $res = Find-PSResource -Name $roleCapName -Repository $repoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be $roleCapName
        $res.Repository | Should -Be $repoName
    }
}
