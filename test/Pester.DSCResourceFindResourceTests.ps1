# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -WarningAction SilentlyContinue -force

Describe 'Test Find-PSResource for Command' {
    
    # Purpose: to check if v3 installs the PSGallery repo by default
    #
    # Action: Get-PSResourceRepository PSGallery
    #
    # Expected Result: Should find that the PSGallery resource repo is already registered in v3
    It 'find the default registered PSGallery' {

        $repo = Get-PSResourceRepository @(Get-PSGalleryName)
        $repo | Should -Not -BeNullOrEmpty
        $repo.URL | Should be @(Get-PSGalleryLocation)
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

        $repo = Get-PSResourceRepository @(Get-PoshTestGalleryName)
        $repo.Name | should be @(Get-PoshTestGalleryName)
        $repo.URL | should be @(Get-PoshTestGalleryLocation)
        $repo.Trusted | should be true
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

    # Purpose: find a DSCResource resource with specified single/exact Version parameter -> [8.1.0]
    #
    # Action: Find-PSResource -Name NetworkingDsc -Version "[8.1.0]"
    #
    # Expected Result: returns NetworkingDsc resource with version 8.1.0.0
    It "find a DSCResource resource with specified single/exact Version parameter -> [8.1.0]" {
        $res = Find-PSResource -Name NetworkingDsc -Version "[8.1.0]"
        $res.Name | Should -Be "NetworkingDsc"
        $res.Version | Should -Be "8.1.0.0"
    }

    # Purpose: find a DSCResource resource with specified range Version parameter -> [7.4.0.0, 8.1.0.0]
    #
    # Action: Find-PSResource -Name NetworkingDsc -Version "[8.1.0]"
    #
    # Expected Result: returns NetworkingDsc resource with latest version in range (i.e 8.1.0.0)
    It "find a DSCResource resource with specified single/exact Version parameter -> [7.4.0.0, 8.1.0.0]" {
        $res = Find-PSResource -Name NetworkingDsc -Version "[7.4.0.0, 8.1.0.0]"
        $res.Name | Should -Be "NetworkingDsc"
        $res.Version | Should -Be "8.1.0.0"
    }

    # Purpose: find a DSCResource resource with wilcard range Version parameter -> '*'
    #
    # Action: Find-PSResource -Name NetworkingDsc -Version "*"
    #
    # Expected Result: returns all NetworkingDsc resources (with versions in descending order)
    It "find a DSCResource resource with specified single/exact Version parameter -> '*' " {
        $res = Find-PSResource -Name NetworkingDsc -Version "*"
        $res.Count | Should -BeGreaterOrEqual 11
        # TODO: check each item's name when you figure out how to check each item returned
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
    # Expected Result: should return (later) preview version than if without Prerelease parameter
    It "find DSCResource resource with preview version, with Prerelease parameter" {
        # the ActiveDirectoryCDDsc resource has prereview and non-prereview versions
        $res = Find-PSResource -Name ActiveDirectoryCSDsc -Repository @(Get-PSGalleryName)
        $res.Version | Should -Be "5.0.0.0"

        $resPrerelease = Find-PSResource -Name ActiveDirectoryCSDsc -Prerelease -Repository @(Get-PSGalleryName)
        $resPrerelease.Version | Should -Be "5.0.1.0"
    }

    # # Purpose: find a DSCResource of package type module, given ModuleName parameter
    # #
    # # Action: Find-PSResource -ModuleName ActiveDirectoryCSDsc
    # #
    # # Expected Result: returns DSCResource with specified ModuleName
    It "find a DSCResource of package type module, given ModuleName parameter" {
        $res = Find-PSResource -ModuleName ActiveDirectoryCSDsc -Repository @(Get-PSGalleryName)
        $res.Name | Should -Be "ActiveDirectoryCSDsc"
    }

    # Purpose: find a DSCResource with a specific tag, given Tags parameter
    #
    # Action: Find-PSResource -Tags CommandsAndResource -Repository PoshTestGallery | Where-Object { $_.Name -eq "DscTestModule" }
    #
    # Expected Result: return DscTestModule resource
    It "find a DSCResource with specific tag, given Tags parameter"{
        $tagValue = "CommandsAndResource"
        $res = Find-PSResource -Tags $tagValue -Repository @(Get-PoshTestGalleryName) | Where-Object { $_.Name -eq "DscTestModule" }
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
        $res = Find-PSResource -Tags $tagValue1,$tagValue2,$tagValue3 -Repository @(Get-PoshTestGalleryName) | Where-Object { $_.Name -eq "DscTestModule" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "DscTestModule"
    }

    # Purpose: not find non-available DSCResource from repository, given Repository parameter
    #
    # Action: Find-PSResource -Name AccessControlDSC -Repository PoshTestGallery
    #
    # Expected Result: should not AccessControlDSC from PoshTestGallery repository
    It "not find DSCResource from repository, given Repository parameter" {
        $res = Find-PSResource -Name AccessControlDSC -Repository @(Get-PoshTestGalleryName)
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find DSCResource from repository, given Repository parameter
    #
    # Action: Find-PSResource -Name AccessControlDSC -Repository PSGallery
    #
    # Expected Result: should find AccessControlDSC from PSGallery repository
    It "find DSCResource from repository, given Repository parameter" {
        $res = Find-PSResource -Name AccessControlDSC -Repository @(Get-PSGalleryName)
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