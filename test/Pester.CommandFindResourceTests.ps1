# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -WarningAction SilentlyContinue -Force

Describe 'Test Find-PSResource for Command' {
    
    # Purpose: find Command resource given Name paramater
    #
    # Action: Find-PSResource -Name Az.Compute
    #
    # Expected Result: returns Az.Compute resource
    It "find Command resource given Name parameter" {
        $res = Find-PSResource -Name Az.Compute
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "Az.Compute"
    }

    # Purpose: not find Command resource given unavailable name
    #
    # Action: Find-PSResource -Name NonExistantCommand
    #
    # Expected result: should not return NonExistantCommand resource
    It "should not find Command resource given unavailable name" {
        $res = Find-PSResource -Name NonExistantCommand
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find Command resource with exact version, given Version parameter -> [4.3.0.0]
    #
    # Action: Find-PSResource -Name Az.Compute -Version "[4.3.0.0]"
    #
    # Expected Result: should return Az.Compute resource with version 4.3.0.0
    It "find Command resource with exact version, given Version parameter -> [4.3.0.0]" {
        $res = Find-PSResource -Name Az.Compute -Version "[4.3.0.0]"
        $res.Name | Should -Be "Az.Compute"
        $res.Version | Should -Be "4.3.0.0"
    }

    # Purpose: find Command resource with range version, given Version parameter -> [4.1.0.0, 4.3.0.0]
    #
    # Action: Find-PSResource -Name Az.Compute -Version "[4.1.0.0, 4.3.0.0]"
    #
    # Expected Result: should return Az.Compute resource with latest version in range (i.e. 4.3.0.0)
    It "find Command resource with range version, given Version parameter -> [4.1.0.0, 4.3.0.0]" {
        $res = Find-PSResource -Name Az.Compute -Version "[4.1.0.0, 4.3.0.0]"
        $res.Name | Should -Be "Az.Compute"
        $res.Version | Should -Be "4.3.0.0"
    }

    # Purpose: find Command resource with wildcard version, given Version parameter -> '*' 
    #
    # Action: Find-PSResource -Name Az.Compute -Version "*"
    #
    # Expected Result: should return all Az.Compute resources (versions in descending order)
    It "find Command resource with exact version, given Version parameter -> '*' " {
        $res = Find-PSResource -Name Az.Compute -Version "*"
        $res.Count | Should -BeGreaterOrEqual 40
    }

    # Purpose: find Command resource with latest-nonpreview versions, by excluding Prerelease parameter
    #
    # Action: Find-PSResource -Name Az.Accounts
    #
    # Expected Result: should return latest non-prerelease/non-preview version of Az.Accounts resource
    It "find Command resource with latest-nonpreview versions, by excluding Prerelease parameter" {
        $res = Find-PSResource -Name Az.Accounts
        $res.Name | Should -Be "Az.Accounts"
        $res.Version | Should -Be "1.9.4.0"
    }

    # Purpose: find Command resource with latest version (including preview versions), with Prerelease parameter
    #
    # Action: Find-PSResource -Name Az.Accounts -Prerelease
    #
    # Expected Result: should return latest version (including preview versions) of Az.Accounts resource
    It "find Command resource with latest version (including preview versions), with Prerelease parameter" {
        $res = Find-PSResource -Name Az.Accounts -Prerelease
        $res.Name | Should -Be "Az.Accounts"
        $res.Version | Should -Be "2.0.1.0"
    }

    # Purpose: find Command resource, of package type module, with ModuleName parameter
    #
    # Action: Find-PSResource -ModuleName AzureRM.OperationalInsights
    #
    # Expected Result: should return AzureRM.OperationalInsights resource
    It "find Command resource, of package type module, with ModuleName parameter" {
        $res = Find-PSResource -ModuleName "AzureRM.OperationalInsights"
        $res.Name | Should -Be "AzureRM.OperationalInsights"
        $res2 = Find-PSResource -ModuleName "xWindowsUpdate" -Repository @(Get-PSGalleryName)
        $res2.Name | Should -Be "xWindowsUpdate"
    }

    # Purpose: find resource with tag, given single Tags parameter
    #
    # Action: Find-PSResource -Tags "Azure" -Repository PoshTestGallery | Where-Object { $_.Name -eq "Az.Accounts" }
    #
    # Expected Result: should return Az.Accounts resource
    It "find resource with single tag, given Tags parameter" {
        $tagValue = "Azure"
        $res = Find-PSResource -Tags $tagValue -Repository @(Get-PoshTestGalleryName) | Where-Object { $_.Name -eq "Az.Accounts" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "Az.Accounts"

    }


    # Purpose: find resource with tags, given multiple Tags parameter values
    #
    # Action: Find-PSResource -Tags "Azure","Authentication","ARM" -Repository PoshTestGallery | Where-Object { $_.Name -eq "Az.Accounts" }
    #
    # Expected Result: should return Az.Accounts resource
    It "find resource with multiple tags, given Tags parameter" {
        $tagValue1 = "Azure"
        $tagValue2 = "Authentication"
        $tagValue3 = "ARM"
        $res = Find-PSResource -Tags $tagValue1,$tagValue2,$tagValue3 -Repository @(Get-PoshTestGalleryName) | Where-Object { $_.Name -eq "Az.Accounts" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "Az.Accounts"
    }

    # Purpose: not find Command resource from repository where it is not available, given Repository parameter
    #
    # Action: Find-PSResource -Name "xWindowsUpdate" -Repository PoshTestGallery
    #
    # Expected Result: should not find xWindowsUpdate resource
    It "not find Command resource from repository where it is not available, given Repository parameter" {
        $res = Find-PSResource -Name "xWindowsUpdate" -Repository @(Get-PoshTestGalleryName)
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find Command resource from repository where it is available, given Repository parameter
    #
    # Action: Find-PSResource -Name "xWindowsUpdate" -Repository PSGallery
    #
    # Expected Result: should find xWindowsUpdate resource
    It "find Command resource, given Repository parameter" {
        $res = Find-PSResource -Name "xWindowsUpdate" -Repository @(Get-PSGalleryName)
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "xWindowsUpdate"
    }    
}