# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -WarningAction SilentlyContinue -force

Describe "Test Find-PSResource for Script" {

    # Purpose: find a script resource with specified Name parameter
    #
    # Action: Find-PSResource -Name Fabrikam-ServerScript
    #
    # Expected Result: should return a resource with specified name Fabrikam-ServerScript
    It "find resource given Name parameter" {
        $res = Find-PSResource -Name Fabrikam-ServerScript
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "Fabrikam-ServerScript"
    }

    # Purpose: successfully not find a resource that doesn't have a valid name
    #
    # Action: Find-PSResource -Name NonExistantScript -Repository PoshTestGallery
    #
    # Expected Result: should not return a resource, as none with specified name exists
    It "not find a resource that doesn't have a valid name" {
        $res = Find-PSResource -Name NonExistantScript -Repository @(Get-PoshTestGalleryName)
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find resources with multiple values provided for Name parameter
    #
    # Action: Find-PSResource -Name Fabrikam-ServerScript,Fabrikam-ClientScript -Repository PoshTestGallery
    #
    # Expected Result: should return the multiple resources specified
    It "find resources with multiple values provided for Name parameter" {
        $res = Find-PSResource -Name Fabrikam-ServerScript,Fabrikam-ClientScript -Repository @(Get-PoshTestGalleryName)
        $res | Should -Not -BeNullOrEmpty
        $res.Count | Should -Be 2
    }

    # Purpose: find resource with specified Name and exact Version parameter -> [2.0.0.0]
    #
    # Action: Find-PSResource -Name Fabrikam-ServerScript -Repository PoshTestGallery -Version "[2.0]"
    #
    # Expected Result: should return Fabrikam-ServerScript resource with Version 2.0.0.0
    It "find resource with specified Name and Version parameter -> [2.0.0.0]" {
        $res = Find-PSResource -Name Fabrikam-ServerScript -Repository @(Get-PoshTestGalleryName) -Version "[2.0.0.0]"
        $res.Name | Should -Be "Fabrikam-ServerScript"
        $res.Version | Should -Be "2.0.0.0"
    }

    # Purpose: find resource with specified Name and range Version parameter -> [1.0.0.0, 2.0.0.0]
    #
    # Action: Find-PSResource -Name Fabrikam-ServerScript -Repository PoshTestGallery -Version "[1.0.0.0, 2.0.0.0]"
    #
    # Expected Result: should return Fabrikam-ServerScript resource with latest version within specified range (i.e 2.0.0.0)
    It "find resource with specified Name and range Version parameter -> [1.0.0.0, 2.0.0.0]" {
        $res = Find-PSResource -Name Fabrikam-ServerScript -Repository @(Get-PoshTestGalleryName) -Version "[1.0.0.0, 2.0.0.0]"
        $res.Name | Should -Be "Fabrikam-ServerScript"
        $res.Version | Should -Be "2.0.0.0"
    }

    # Purpose: find resource with specified Name and wildcard Version -> '*'
    #
    # Action: Find-PSResource -Name Fabrikam-ServerScript -Version "*" -Repository PoshTestGallery
    #
    # Expected Result: returns all Fabrikam-ServerScript resources (i.e all 5 versions in descending order)
    It "find resource with specified Name and range Version parameter -> '*' " {
        $res = Find-PSResource -Name Fabrikam-ServerScript -Version "*" -Repository @(Get-PoshTestGalleryName)
        $res.Count | Should -BeGreaterOrEqual 5
    }

    # Purpose: not find prerelease resource when Prerelease parameter isn't specified
    #
    # Action: Find-PSResource -Name PSGTEST-PublishPrereleaseScript-579
    #
    # Expected Result: should not find PSGTEST-PublishPrereleaseScript-579
    It "not find prerelease resource when Prerelease parameter isn't specified" {
        $res = Find-PSResource -Name PSGTEST-PublishPrereleaseScript-579
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find prerelease resource when Prerelease parameter is specified
    #
    # Action: Find-PSResource -Name PSGTEST-PublishPrereleaseScript-579
    #
    # Expected Result: find PSGTEST-PublishPrereleaseScript-579
    It "find prerelease resource when Prerelease parameter is specified" {
        $res = Find-PSResource -Name PSGTEST-PublishPrereleaseScript-579 -Prerelease
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "PSGTEST-PublishPrereleaseScript-579"
    }
    
    # Purpose: not find un-available resource from specified repository, when given Repository parameter
    #
    # Action: Find-PSResource -Name Get-WindowsAutoPilotInfo -Repository PoshTestGallery
    #
    # Expected Result: should not find resource from specified PSGallery repository becuase resource doesn't exist there
    It "find resource from specified repository, when given Repository parameter" {
        $res = Find-PSResource -Name Get-WindowsAutoPilotInfo -Repository @(Get-PoshTestGalleryName)
        $res | Should -BeNullOrEmpty
    }
    
    # Purpose: find resource from specified repository, when given Repository parameter
    #
    # Action: Find-PSResource -Name Get-WindowsAutoPilotInfo -Repository PSGallery
    #
    # Expected Result: should return resource from specified repository where it exists
    It "find resource from specified repository, when given Repository parameter" {
        $res = Find-PSResource -Name Get-WindowsAutoPilotInfo -Repository @(Get-PSGalleryName)
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "Get-WindowsAutoPilotInfo"
        $res.Repository | Should -Be "PSGallery"
    }

    # Purpose: find a resource but not its dependency resource(s)
    #
    # Action: Find-PSResources -Name Script-WithDependencies1
    #
    # Expected Result: should return resource but none of its dependency resources
    It "find resource and its dependency resource(s) with IncludeDependencies parameter" {
        $res = Find-PSResource -Name Script-WithDependencies1
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "Script-WithDependencies1"
    }

    # Purpose: find a resource and its dependency resource(s) with IncludeDependencies parameter
    #
    # Action: Find-PSResources -Name Script-WithDependencies1 -IncludeDependencies
    #
    # Expected Result: should return resource and all of its dependency resources
    It "find resource and its dependency resource(s) with IncludeDependencies parameter" {
        $res = Find-PSResource -Name Script-WithDependencies1 -IncludeDependencies
        $res | Should -Not -BeNullOrEmpty
        $res.Count | Should -BeGreaterOrEqual 9
    }
}
