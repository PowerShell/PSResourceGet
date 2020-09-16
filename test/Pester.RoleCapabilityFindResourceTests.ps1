# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -WarningAction SilentlyContinue -force
Import-Module "C:\code\PowerShellGet\src\bin\Debug\netstandard2.0\publish\PowerShellGet.dll" -force


$PSGalleryName = 'PSGallery'
$PSGalleryLocation = 'https://www.powershellgallery.com/api/v2'

$PoshTestGalleryName = 'PoshTestGallery'
$PostTestGalleryLocation = 'https://www.poshtestgallery.com/api/v2'


$TestLocalDirectory = 'TestLocalDirectory'
$tmpdir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $TestLocalDirectory

if (-not (Test-Path -LiteralPath $tmpdir)){
    New-Item -Path $tmpdir -ItemType Directory > $null
}

##########################
### Find-PSResource ###
##########################
Describe 'Test Find-PSResource for Role Capability' {
    
    # Purpose: to check if v3 installs the PSGallery repo by default
    #
    # Action: Get-PSResourceRepository PSGallery
    #
    # Expected Result: Should find that the PSGallery resource repo is already registered in v3
    It 'find the default registered PSGallery' {

        $repo = Get-PSResourceRepository $PSGalleryName
        $repo | Should -Not -BeNullOrEmpty
        $repo.URL | Should be $PSGalleryLocation
        $repo.Trusted | Should be false
        $repo.Priority | Should be 50
    }

    # Purpose: to register PoshTestGallery resource repo and check it registered successfully
    #
    # Action: Register-PSResourceRepository PoshTestGallery -URL https://www.poshtestgallery.com/api/v2 -Trusted
    #
    # Expected Result: PoshTestGallery resource repo has registered successfully
    It 'register the poshtest repository when -URL is a website and installation policy is trusted' {
        # Register-PSResourceRepository $PoshTestGalleryName -URL $PostTestGalleryLocation -Trusted

        $repo = Get-PSResourceRepository $PoshTestGalleryName
        $repo.Name | should be $PoshTestGalleryName
        $repo.URL | should be $PostTestGalleryLocation
        $repo.Trusted | should be true
    }

    # Purpose: find Role Capability resource, given Name parameter
    #
    # Action: Find-PSResource -Name TestRoleCapModule
    #
    # Expected Result: return TestRoleCapModule resource
    It "find Role Capability resource, given Name parameter" {
        $res = Find-PSResource -Name TestRoleCapModule
        $res.Name | Should -Be "TestRoleCapModule"
    }

    # Purpose: not find Role Capability resource, given wildcard Name parameter
    #
    # Action: Find-PSResource -Name "T[eE]s?Ro?e[a-z]ap[mM]odulD"
    #
    # Expected Result: not return the TestRoleCapModule resource or any other one
    It "not find Role Capability resource, given wilcard Name parameter" {
        $res = Find-PSResource -Name "T[eE]s?Ro?e[a-z]ap[mM]odulD"
        $res.Name | Should -BeNullOrEmpty
    }

    # Purpose: not find non-existant Role Capability resource, given Name parameter
    #
    # Action: Find-PSResource -Name NonExistantRoleCap
    #
    # Expected Result: should not return any resource
    It "not find non-existant Role Capability resource, given Name parameter" {
        $res = Find-PSResource -Name NonExistantRoleCap
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find Role Capability resource with exact version, given Version parameter
    #
    # Action: Find-PSResource -Name "TestRoleCapModule" -Version "[2.0.0.0]"
    #
    # Expected Result: should return TestRoleCapModule with version 2.0.0.0
    It "find Role Capability resource with exact version, given Version parameter -> [2.0.0.0]" {
        $res = Find-PSResource -Name "TestRoleCapModule" -Version "[2.0.0.0]"
        $res.Version | Should -Be "2.0.0.0"
    }

    # Purpose: find Role Capability resource with range version, given Version parameter
    #
    # Action: Find-PSResource -Name "TestRoleCapModule" -Version "[1.0.0.0, 2.0.0.0]"
    #
    # Expected Result: return TestRoleCapModule resource with latest version in range (i.e 2.0.0.0)
    It "find Role Capability resource with range version, given Version parameter -> [1.0.0.0, 2.0.0.0]" {
        $res = Find-PSResource -Name "TestRoleCapModule" -Version "[1.0.0.0, 2.0.0.0]"
        $res.Version | Should -Be "2.0.0.0"
    }

    # Purpose: find Role Capability resource with wildcard version, given Version parameter
    #
    # Action: Find-PSResource -Name "TestRoleCapModule" -Version "*"
    #
    # Expected Result: returns all versions of DSCTestModule (versions in descending order)
    It "find Role Capability resource with wildcard version, given Version parameter -> '*' " {
        # Note: DSCTestModule fulfils both DSCResource and RoleCapability categories
        $res = Find-PSResource -Name DSCTestModule -Version "*"
        $res.Count | Should -BeGreaterOrEqual 1
    }

    # Purpose: find Role Capability resource with preview version, using Prerelease parameter
    #
    # Action: Find-PSResource -Name <NameofRoleCapabilityResource> -Prerelease
    #
    # Expected Result: should return Role Capability resource with latest version (including preview versions)
    # TODO: 0 mathces on server/site end when Prerelease selected. Add, and then test!

    
    # Purpose: find Role Capability resource, given ModuleName parameter
    #
    # Action: Find-PSResource -ModuleName JeaExamples -Repository PSGallery
    #
    # Expected Result: should return JeaExamples resource
    It "find Role Capability resource, given ModuleName parameter" {
        $res = Find-PSResource -ModuleName JeaExamples -Repository PSGallery
        $res.Name | Should -Be "JeaExamples"
    }

    # Purpose: find Role Capability resource with given Tags parameter
    #
    # Action: Find-PSResource -Tags "Tag-testPAckageMetadata-2.5" -Repository PoshTestGallery | Where-Object { $_.Name -eq "PSGETTEST-TestPackageMetadata" }
    #
    # Expected Result: should return PSGETTEST-TestPackageMetadata resource
    It "find Role Capability resource with given Tags parameter" {
        $tagValue = "Tag-testPAckageMetadata-2.5"
        $res = Find-PSResource -Tags $tagValue -Repository $PoshTestGalleryName | Where-Object { $_.Name -eq "PSGETTEST-TestPackageMetadata" }
        $res.Name | Should -Be "PSGETTEST-TestPackageMetadata"
    }

    # Purpose: find Role Capability resource with multiple given Tags parameter
    #
    # Action: Find-PSResource -Tags "Tag-testPAckageMetadata-2.5","Tag2", "PSGet" -Repository PoshTestGallery | Where-Object { $_.Name -eq "PSGETTEST-TestPackageMetadata" }
    #
    # Expected Result: should return PSGETTEST-TestPackageMetadata resource
    It "find Role Capability resource with given Tags parameter" {
        $tagValue1 = "Tag-testPAckageMetadata-2.5"
        $tagValue2 = "PSGet"
        $tagValue3 = "Tag2"
        $res = Find-PSResource -Tags $tagValue1,$tagValue2,$tagValue3 -Repository $PoshTestGalleryName | Where-Object { $_.Name -eq "PSGETTEST-TestPackageMetadata" }
        $res.Name | Should -Be "PSGETTEST-TestPackageMetadata"
    }

    # Purpose: not find Role Capability resource that doesn't exist in specified repository, given Repository parameter
    #
    # Action: Find-PSResource -Name JeaExamples -Repository PoshTestGallery
    #
    # Expected Result: should not find JeaExamples resource
    It "not find Role Capability resource that doesn't exist in specified repository, given Repository parameter" {
        $res = Find-PSResource -Name JeaExamples -Repository $PoshTestGalleryName
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find Role Capability resource that exists only in specified repository, given Repository parameter
    #
    # Action: Find-PSResource -Name JeaExamples -Repository $PSGalleryName
    #
    # Expected Result: should find JeaExamples resource
    It "find resource that exists only in a specific repository, given Repository parameter" {
        $resRightRepo = Find-PSResource -Name JeaExamples -Repository $PSGalleryName
        $resRightRepo.Name | Should -Be "JeaExamples"
    }    
}