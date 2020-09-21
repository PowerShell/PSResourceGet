# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -force

Describe "Test Find-PSResource for Script" {

    BeforeAll {
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
    }

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
        $res = Find-PSResource -Name NonExistantScript -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find resources with multiple values provided for Name parameter
    #
    # Action: Find-PSResource -Name Fabrikam-ServerScript,Fabrikam-ClientScript -Repository PoshTestGallery
    #
    # Expected Result: should return the multiple resources specified
    It "find resources with multiple values provided for Name parameter" {
        $res = Find-PSResource -Name Fabrikam-ServerScript,Fabrikam-ClientScript -Repository $TestGalleryName
        $res | Should -Not -BeNullOrEmpty
        $res.Count | Should -Be 2
    }

    # Purpose: find resource when given Name, Version param not null
    #
    # Action: Find-PSResource -Name ContosoServer -Repository PoshTestGallery
    #
    # Expected Results: should return resource with appropriate version
    It "find resource when given Name to <Reason>" -TestCases @(
        @{Version="[2.0.0.0]";          ExpectedVersion="2.0.0.0"; Reason="validate version, exact match"},
        @{Version="[1.0.0.0, 2.5.0.0]"; ExpectedVersion="2.5.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="(1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="[1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(,1.5.0.0)";         ExpectedVersion="1.2.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,1.5.0.0]";         ExpectedVersion="1.5.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -Name "Fabrikam-ServerScript" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "Fabrikam-ServerScript"
        $res.Version | Should -Be $ExpectedVersion
    }

    # Purpose: not find resource with invalid verison, given Version parameter -> (1.5.0.0)
    #
    # Action: Find-PSResource -Name "Fabrikam-ServerScript" -Version "(1.5.0.0)"
    #
    # Expected Result: should not return a resource as version is invalid
    It "not find Command resource given Name to validate handling an invalid version" {
        $res = Find-PSResource -Name "Fabrikam-ServerScript" -Version "(1.5.0.0)"
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find resources when given Name, Version not null --> '*'
    #
    # Action: Find-PSResource -Name ContosoServer -Version "*" -Repository PoshTestGallery
    #
    # Expected Result: returns 4 ContosoServer resources (of all versions in descending order)
    It "find resources when given Name, Version not null --> '*'" {
        $res = Find-PSResource -Name "Fabrikam-ServerScript" -Version "*" -Repository $TestGalleryName
        $res.Count | Should -BeGreaterOrEqual 5
    }

    # Purpose: not find script resource when given ModuleName and no Version parameter
    #
    # Action: Find-PSResource -ModuleName "Fabrikam-ServerScript" -Repository PoshTestGallery
    #
    # Expected Result: not find a script resource when given ModuleName
    It "not find script resource when given ModuleName" {
        $res = Find-PSResource -ModuleName "Fabrikam-ServerScript" -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }

    # Purpose: not find script resource when given ModuleName and any Version parameter
    #
    # Action: Find-PSResource -Name "Fabrikam-ServerScript" -Version [2.0.0.0] -Repository "PoshTestGallery"
    #
    # Expected Result: should not find a script resource when given ModuleName and any version value
    It "not find script resource when given ModuleName to <Reason>" -TestCases @(
        @{Version="[2.0.0.0]";          Reason="validate version, exact match"},
        @{Version="[1.0.0.0, 2.5.0.0]"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0, 2.5.0.0)"; Reason="validate version, exact range exclusive"},
        @{Version="(1.0.0.0,)";         Reason="validate version, minimum version exclusive"},
        @{Version="[1.0.0.0,)";         Reason="validate version, minimum version inclusive"},
        @{Version="(,1.5.0.0)";         Reason="validate version, maximum version exclusive"},
        @{Version="(,1.5.0.0]";         Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0, 2.5.0.0)"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -ModuleName "Fabrikam-ServerScript" -Version $Version -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }

    # Purpose: not find resource with given ModuleName and wildcard Version -> '*'
    #
    # Action: Find-PSResource -ModuleName Fabrikam-ServerScript -Version "*" -Repository PoshTestGallery
    #
    # Expected Result: not return a resource
    It "not find script resource with specified ModuleName and range Version parameter -> '*' " {
        $res = Find-PSResource -ModuleName Fabrikam-ServerScript -Version "*" -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find prerelease version of resource when Prerelease parameter is specified
    #
    # Action: Find-PSResource -Name PSGTEST-PublishPrereleaseScript-579
    #
    # Expected Result: find PSGTEST-PublishPrereleaseScript-579
    It "find prerelease resource when Prerelease parameter is specified" {
        $res = Find-PSResource -Name PSGTEST-PublishPrereleaseScript-579
        $res | Should -BeNullOrEmpty

        $resPrerelease = Find-PSResource -Name PSGTEST-PublishPrereleaseScript-579 -Prerelease
        $resPrerelease | Should -Not -BeNullOrEmpty
        $resPrerelease.Name | Should -Be "PSGTEST-PublishPrereleaseScript-579"
    }
    
    # Purpose: not find un-available resource from specified repository, when given Repository parameter
    #
    # Action: Find-PSResource -Name Get-WindowsAutoPilotInfo -Repository PoshTestGallery
    #
    # Expected Result: should not find resource from specified PSGallery repository becuase resource doesn't exist there
    It "find resource from specified repository, when given Repository parameter" {
        $res = Find-PSResource -Name Get-WindowsAutoPilotInfo -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }
    
    # Purpose: find resource from specified repository, when given Repository parameter
    #
    # Action: Find-PSResource -Name Get-WindowsAutoPilotInfo -Repository PSGallery
    #
    # Expected Result: should return resource from specified repository where it exists
    It "find resource from specified repository, when given Repository parameter" {
        $res = Find-PSResource -Name Get-WindowsAutoPilotInfo -Repository $PSGalleryName
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
