# This is a Pester test suite to validate Find-PSResource.
#
# Copyright (c) Microsoft Corporation, 2020

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -WarningAction SilentlyContinue -force
# Import-Module "C:\code\PowerShellGet\src\bin\Debug\netstandard2.0\publish\PowerShellGet.dll" -force


Import-Module "C:\Users\annavied\Documents\PowerShellGet\src\bin\Debug\netstandard2.0\publish\PowerShellGet.dll" -force


$PSGalleryName = 'PSGallery'
$PSGalleryLocation = 'https://www.powershellgallery.com/api/v2'

$PoshTestGalleryName = 'PoshTestGallery'
$PostTestGalleryLocation = 'https://www.poshtestgallery.com/api/v2'


# Register-PSResourceRepository -PSGallery

$TestLocalDirectory = 'TestLocalDirectory'
$tmpdir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $TestLocalDirectory

if (-not (Test-Path -LiteralPath $tmpdir)){
    New-Item -Path $tmpdir -ItemType Directory > $null
}

##########################
### Find-PSResource ###
##########################
Describe 'Test Find-PSResource' { #todo: add tags?
    # todo: add a BeforeAll and a AfterAll
    # BeforeAll{
    #     Register-PSResourceRepository -PSGallery
    # }

    # Purpose: to check if v3 installs the PSGallery repo by default
    #
    # Action: Get-PSResourceRepository PSGallery
    #
    # Expected Result: Should find that the PSGallery resource repo is already registered in v3
    It 'Find the Default Registered PSGallery' {

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
    It 'Register the Poshtest Repository When -URL is a Website and Installation Policy is Trusted' {
        Register-PSResourceRepository $PoshTestGalleryName -URL $PostTestGalleryLocation -Trusted

        $repo = Get-PSResourceRepository $PoshTestGalleryName
        $repo.Name | should be $PoshTestGalleryName
        $repo.URL | should be $PostTestGalleryLocation
        $repo.Trusted | should be true
    }

    # Purpose: to find all resources when no parameters are specified
    #
    # Action: Find-PSResource
    #
    # Expected-Result: finds all (more than 1) resources in PSGallery
    It "Find Resources Without Any Parameter Values" {
        $psGetItemInfo = Find-PSResource
        $psGetItemInfo.Count | Should -BeGreaterThan 1
    }

    # Purpose: to find a specific resource by name
    #
    # Action: Find-PSResource -Name "ContosoServer"
    #
    # Expected Result: Should find ContosoServer resource
    It "Find Specific Resource by Name" {
        # install ContosoServer module from PoshTestGallery first, todo: change that to be a connection to the link
        $specItem = Find-PSResource -Name ContosoServer
        $specItem.Name | Should -Be "ContosoServer"
    }

    # Purpose: to find a resource(s) with regex in name parameter
    #
    # Action: Find-PSResource -Name Contoso*
    #
    # Expected Result: should find multiple resources,namely atleast ContosoServer, ContosoClient, Contoso
    It "Find multiple Resource(s) with Wildcards for Name Param" {
        # $vals = 'a', 'b', 'c'
        # $vals | should -contain d
        $res = Find-PSResource -Name Contoso*
        $res.Count | Should -BeGreaterOrEqual 1
        # $res[0] | Should -Contain '\@{Name=ContosoServer; Version=2.5; Repository=PoshTestGallery; Description=ContosoServer module}' # todo: ask on SO
    }

    # Purpose: to find a specific resource with wildcard in name
    #
    # Action: Find-PSResource *ontosoServe*
    # 
    # Expected Result: should find the ContosoServer resource
    It "Find Specific Resource with Wildcards for Name Param" {
        $res = Find-PSResource *ontosoServe*
        $res.Name | Should -be "ContosoServer"
    }

    # todo: implement range wildcards
    # Purpose: to find a resource with range wildcards in name parameter
    #
    # Action: Find-PSResource -Name "Co[nN]t?soS[a-z]r?er"
    #
    # Expected Result: should find ContosoServer resource
    # It "Find specific resource with range wildcard" {
    #     $res = Find-PSResource -Name "Co[nN]t?soS[a-z]r?er"
    #     $res.Name | Should -Be "ContosoServer"
    # }

    # Purpose: should not find a not available resource with range wildcards in name parameter
    #
    # Action: Find-PSResource -Name "Co[nN]t?soS[a-z]r?eW"
    #
    # Expected Result: should find ContosoServer resource
    It "Find Not Available Resource with Range Wildcard for Name Param" {
        $res = Find-PSResource -Name "Co[nN]t?soS[a-z]r?eW"
        $res | Should -BeNullOrEmpty
    }

    It "Find Resource with Specified Version Param" {
        $res = Find-PSResource -Name ContosoServer -Version 2.5
        $res.Version | Should -Be "2.5"
        $resDiffVersion = Find-PSResource -Name ContosoServer -Version 2.0
        $resDiffVersion.Version | Should -Be "2.0"
    }

    # Purpose: to find a prerelease resource with the prerelease param
    #
    # Action: Find-PSResource -Name "PSGTEST-PublishPrereleaseModule-3139" -Prerelease
    #
    # Expected Result: without prerelease param null should be returned
    # with it the module should be returned and valid
    It "Find Resource with Prerelease param" {
        # this resource only has a prerelease version, can't be found without the prerelease param
        $res = Find-PSResource -Name "PSGTEST-PublishPrereleaseModule-3139"
        $res | should -BeNullOrEmpty

        # add prerelease param to cmdlet query
        $resWPrelease = Find-PSResource -Name "PSGTEST-PublishPrereleaseModule-3139" -Prerelease
        $resWPrelease | should -not -BeNullOrEmpty
        $resWPrelease.Name | should -be "PSGTEST-PublishPrereleaseModule-3139"
    }

    # todo: does the prerelease param say "only get me a prerelease version or get any including prerelease?"
    # basically, if I include -prerelease param on module without prerelease version should/would it break? perhaps add test

    # Purpose: to find a module resource given the ModuleName param
    #
    # Action: Find-PSResource -ModuleName ContosoServer
    #
    # Expected Result: finds the module resource with the given ModuleName
    It "Find Resource ModuleName param" {
        $res = Find-PSResource -ModuleName ContosoServer
        $res | should -not -BeNullOrEmpty
        $res.Name | Should -be "ContosoServer"
    }

    # Purpose: should not find a resource when given a ModuleName that is a name of a script
    #
    # Action: Find-PSResource -ModuleName "Fabrikam-Script"
    #
    # Expected Result: resource returned should be null/empty
    # todo: FIX! it finds a resource. Checked this rsrc isn't also a module:
    # Find-Module "Fabrikam-Script" --> throws an error saying is a script not module
    # It "Should not find a resource given ModuleName parameter a name that's for a script" {
    #     $res = Find-PSResource -ModuleName "Fabrikam-Script"
    #     $res | should -BeNullOrEmpty
    # }




    # todo: add test for "find by type module", and like get a list of all resources
    # that are a module and iterate thru them to make sure none of their types are something beside module
    # likewise for all other types too

    # todo: add test for "find resource by module name for resource that's not a module". Like i create a script, and test
    # to see if it fails as expected and error message given saying invalid type for that param



}
