# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Find-PSResource for Module' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        Get-NewPSResourceRepositoryFile
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    # Purpose: to find all resources when no parameters are specified
    #
    # Action: Find-PSResource
    #
    # Expected-Result: finds all (more than 1) resources in PSGallery
    It "find Resources Without Any Parameter Values" {
        $psGetItemInfo = Find-PSResource
        $psGetItemInfo.Count | Should -BeGreaterThan 1
    }

    # Purpose: to find a specific resource by name
    #
    # Action: Find-PSResource -Name "ContosoServer"
    #
    # Expected Result: Should find ContosoServer resource
    It "find Specific Module Resource by Name" {
        $specItem = Find-PSResource -Name ContosoServer
        $specItem.Name | Should -Be "ContosoServer"
    }

    # Purpose: to find a resource(s) with regex in name parameter
    #
    # Action: Find-PSResource -Name Contoso*
    #
    # Expected Result: should find multiple resources,namely atleast ContosoServer, ContosoClient, Contoso
    It "find multiple Resource(s) with Wildcards for Name Param" {
        $res = Find-PSResource -Name Contoso*
        $res.Count | Should -BeGreaterOrEqual 1
    }

    # Purpose: to find a specific resource with wildcard in name
    #
    # Action: Find-PSResource *ontosoServe*
    #
    # Expected Result: should find the ContosoServer resource
    It "find Specific Resource with Wildcards for Name Param" {
        $res = Find-PSResource *ontosoServe*
        $res.Name | Should -Be "ContosoServer"
    }

    # Purpose: find resource when given Name, Version param not null
    #
    # Action: Find-PSResource -Name ContosoServer -Repository PoshTestGallery
    #
    # Expected Result: returns ContosoServer resource
    It "find resource when given Name to <Reason> <Version>" -TestCases @(
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
        $res = Find-PSResource -Name "ContosoServer" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "ContosoServer"
        $res.Version | Should -Be $ExpectedVersion
    }

    # Purpose: not find resources with invalid version
    #
    # Action: Find-PSResource -Name "ContosoServer" -Version "(1.5.0.0)"
    #
    # Expected Result: should not return a resource
    It "not find resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(1.5.0.0)';       Description="exlcusive version (8.1.0.0)"},
        @{Version='[1-5-0-0]';       Description="version formatted with invalid delimiter"},
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

        $res = $null
        try {
            $res = Find-PSResource -Name "ContosoServer" -Version $Version -Repository $TestGalleryName -ErrorAction Ignore
        }
        catch {}
        
        $res | Should -BeNullOrEmpty
    }

    # Purpose: not find resource with invalid verison, given Version parameter -> (1.5.0.0)
    #
    # Action: Find-PSResource -Name "ContosoServer" -Version "(1.5.0.0)"
    #
    # Expected Result: should not return a resource as version is invalid
    It "not find Command resource given Name to validate handling an invalid version" {
        $res = Find-PSResource -Name "ContosoServer" -Version "(1.5.0.0)"
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find resources when given Name, Version not null --> '*'
    #
    # Action: Find-PSResource -Name ContosoServer -Version "*" -Repository PoshTestGallery
    #
    # Expected Result: returns 4 ContosoServer resources (of all versions in descending order)
    It "find resources when given Name, Version not null --> '*'" {
        $res = Find-PSResource -Name ContosoServer -Version "*" -Repository $TestGalleryName
        $res.Count | Should -BeGreaterOrEqual 4
    }

    # Purpose: find resource when given ModuleName, Version param null
    #
    # Action: Find-PSResource -ModuleName ContosoServer -Repository PoshTestGallery
    #
    # Expected Result: returns nothing, prints error message
    It "find resource when given ModuleName, Version param null" {
        $res = Find-PSResource -ModuleName "ContosoServer" -Repository $TestGalleryName
        $res.Name | Should -Be "ContosoServer"
        $res.Version | Should -Be "2.5.0.0"
    }

    # Purpose: find resource when given ModuleName, Version param not null
    #
    # Action: Find-PSResource -ModuleName ContosoServer -Repository PoshTestGallery
    #
    # Expected Result: returns ContosoServer resource
    It "find resource when given ModuleName to <Reason>" -TestCases @(
        @{Version="[2.0.0.0]";          ExpectedVersion="2.0.0.0"; Reason="validate version, exact match"},
        @{Version="2.0.0.0";            ExpectedVersion="2.0.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.0.0.0, 2.5.0.0]"; ExpectedVersion="2.5.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0,2.5.0.0)";  ExpectedVersion="2.0.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="(1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="[1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(,1.5.0.0)";         ExpectedVersion="1.0.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,1.5.0.0]";         ExpectedVersion="1.5.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0,2.5.0.0)";  ExpectedVersion="2.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -ModuleName "ContosoServer" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "ContosoServer"
        $res.Version | Should -Be $ExpectedVersion
    }

    # Purpose: find resources when given ModuleName, Version not null --> '*'
    #
    # Action: Find-PSResource -ModuleName ContosoServer -Version "*" -Repository PoshTestGallery
    #
    # Expected Result: returns 4 ContosoServer resources (of all versions in descending order)
    It "find resources when given ModuleName, Version not null --> '*'" {
        $res = Find-PSResource -ModuleName ContosoServer -Version "*" -Repository $TestGalleryName
        $res.Count | Should -BeGreaterOrEqual 4
    }

    # Purpose: find resource with latest version (including prerelease version) given Prerelease parameter
    #
    # Action: Find-PSResource -Name "test_module" -Prerelease
    #
    # Expected Result: should return latest version (may be a prerelease version)
    It "find resource with latest (including prerelease) version given Prerelease parameter" {
        # test_module resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Find-PSResource -Name "test_module"
        $res.Version | Should -Be "5.0.0.0"

        $resPrerelease = Find-PSResource -Name "test_module" -Prerelease
        $resPrerelease.Version | Should -Be "5.2.5.0"        
    }

    # Purpose: to find a resource given Tags parameter with one value
    #
    # Action: Find-PSResource -Tags CommandsAndResource | Where-Object { $_.Name -eq "DscTestModule" }
    #
    # Expected Result: should return all resources with that tag, and then filer by name
    It "find a resource given Tags parameter with one value" {
        $res = Find-PSResource -Tags CommandsAndResource | Where-Object { $_.Name -eq "DscTestModule" }
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "DscTestModule"
    }

    # Purpose: find resource(s) given multiple tags for the Tags parameter.
    #
    # Action: Find-PSResource -Tags CommandsAndResource,DSC,Tag1
    #
    # Expected Result: Should return more resources than if just queried with -Tags CommandsAndResources
    It "find a resource given tags parameter with multiple values" {
        $resSingleTag = Find-PSResource -Tags CommandsAndResource
        $res = Find-PSResource -Tags CommandsAndResource,DSC,Tag1
        $res | Should -Not -BeNullOrEmpty
        $res.Count | Should -BeGreaterOrEqual $resSingleTag.Count
    }

    # Purpose: find a resource in a specific repository, given Repository parameter
    #
    # Action: Find-PSResource ContosoServer -Repository $PoshTestGalleryName
    #
    # Expected Result: Should find the resource from the specified repository
    It "find resource given repository parameter" {
        $res = Find-PSResource ContosoServer -Repository $PSGalleryName
        $res | Should -BeNullOrEmpty

        # ContosoServer resource exists in the PostTestGalleryRepository
        $resCorrectRepo = Find-PSResource ContosoServer -Repository $TestGalleryName
        $resCorrectRepo | Should -Not -BeNullOrEmpty
        $resCorrectRepo.Repository | Should -Be "PoshTestGallery"
    }

    # Purpose: find resource in first repository where it exists given Repository parameter
    #
    # Action: Find-PSResource "Az.Compute"
    #         Find-PSResource "Az.Compute" -Repository PSGallery
    #
    # Expected Result: Returns resource from first avaiable or specfied repository
    It "find Resource given repository parameter, where resource exists in multiple repos" {
        # first availability found in PoshTestGallery
        $res = Find-PSResource "Az.Compute"
        $res.Repository | Should -Be "PoshTestGallery"

        # check that same resource can be returned from non-first-availability/non-default repo
        $resNonDefault = Find-PSResource "Az.Compute" -Repository $PSGalleryName
        $resNonDefault.Repository | Should -Be "PSGallery"
    }

    # Purpose: find a resource and associated dependecies given IncludeDependencies parameter
    #
    # Action: Find-PSResource ModuleWithDependencies1 -IncludeDependencies
    #
    # Expected Result: should return resource specified and all associated dependecy resources
    It "find resource with IncludeDependencies parameter" {
        $res = Find-PSResource ModuleWithDependencies1 -IncludeDependencies -Version "[1.0,2.0]"
        $res.Count | Should -BeGreaterOrEqual 11
    }

    # Purpose: find resource in local repository given Repository parameter
    #
    # Action: Find-PSResource -Name "local_command_module" -Repository "psgettestlocal"
    #
    # Expected Result: should find resource from local repository
    It "find resource in local repository given Repository parameter" {
        $publishModuleName = "TestFindModule"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $publishModuleName

        $res = Find-PSResource -Name $publishModuleName -Repository "psgettestlocal"
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be $publishModuleName
        $res.Repository | Should -Be "psgettestlocal"
    }
}
