# This is a Pester test suite to validate Find-PSResource
# for script resources, retains tests from Find-Script from v2.
#
# Copyright (c) Microsoft Corporation, 2020

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

Describe "Test Find-PSResource for Script" {
    
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

    # # Purpose: find a script resource with range wildcards
    # #
    # # Action: Find-PSResource -Name "Fab[rR]ikam?Ser[a-z]erScr?pt"
    # #
    # # Expected Result: should return a resource with specified name
    # # TODO: add wildcard implementation
    # It "should find a script resource with range wildcards" {
    #     $res = Find-PSResource -Name "Fab[rR]ikam?Ser[a-z]erScr?pt"
    #     $res | Should -Not -BeNullOrEmpty
    #     $res.Name | Should -Be "Fabrikam-ServerScript"
    # }

    # Purpose: not find a non-available script resource with range wildcards
    #
    # Action: Find-PSResource -Name "Fab[rR]ikam?Ser[a-z]erScr?ptW"
    #
    # Expected Result: should not return a resource
    It "not find a non-available script resource with range wildcards" {
        $res = Find-PSResource -Name "Fab[rR]ikam?Ser[a-z]erScr?ptW"
        $res | Should -BeNullOrEmpty
    }

    # Purpose: successfully not find a resource that doesn't have a valid name
    #
    # Action: Find-PSResource -Name NonExistantScript -Repository PoshTestGallery
    #
    # Expected Result: should not return a resource, as none with specified name exists
    It "not find a resource that doesn't have a valid name" {
        $res = Find-PSResource -Name NonExistantScript -Repository PoshTestGallery
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find resources with multiple values provided for Name parameter
    #
    # Action: Find-PSResource -Name Fabrikam-ServerScript,Fabrikam-ClientScript -Repository PoshTestGallery
    #
    # Expected Result: should return the multiple resources specified
    It "find resources with multiple values provided for Name parameter" {
        $res = Find-PSResource -Name Fabrikam-ServerScript,Fabrikam-ClientScript -Repository PoshTestGallery
        $res | Should -Not -BeNullOrEmpty
        $res.Count | Should -Be 2
        # TODO: add check to see that each item has expected name
    }

    # Purpose: find resource with specified Name and exact Version parameter -> [2.0.0.0]
    #
    # Action: Find-PSResource -Name Fabrikam-ServerScript -Repository PoshTestGallery -Version "[2.0]"
    #
    # Expected Result: should return Fabrikam-ServerScript resource with Version 2.0.0.0
    It "find resource with specified Name and Version parameter -> [2.0.0.0]" {
        $res = Find-PSResource -Name Fabrikam-ServerScript -Repository PoshTestGallery -Version "[2.0.0.0]"
        $res.Name | Should -Be "Fabrikam-ServerScript"
        $res.Version | Should -Be "2.0.0.0"
    }

    # Purpose: find resource with specified Name and range Version parameter -> [1.0.0.0, 2.0.0.0]
    #
    # Action: Find-PSResource -Name Fabrikam-ServerScript -Repository PoshTestGallery -Version "[1.0.0.0, 2.0.0.0]"
    #
    # Expected Result: should return Fabrikam-ServerScript resource with latest version within specified range (i.e 2.0.0.0)
    It "find resource with specified Name and range Version parameter -> [1.0.0.0, 2.0.0.0]" {
        $res = Find-PSResource -Name Fabrikam-ServerScript -Repository PoshTestGallery -Version "[1.0.0.0, 2.0.0.0]"
        $res.Name | Should -Be "Fabrikam-ServerScript"
        $res.Version | Should -Be "2.0.0.0"
    }

    # Purpose: find resource with specified Name and wildcard Version -> '*'
    #
    # Action: Find-PSResource -Name Fabrikam-ServerScript -Version "*" -Repository PoshTestGallery
    #
    # Expected Result: returns all Fabrikam-ServerScript resources (i.e all 5 versions in descending order)
    It "find resource with specified Name and range Version parameter -> '*' " {
        $res = Find-PSResource -Name Fabrikam-ServerScript -Version "*" -Repository PoshTestGallery
        #TODO figure out iterating over the returned list
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

    # # Purpose: find resource using Tags parameter, single tag value
    # #
    # # Action: Find-PSResource -Tags "Tag-Fabrikam-Script-2.5" | Where-Object { $_.Name -eq "Fabrikam-Script" }
    # #
    # # Expected Result: should return resource that has specified Tags value
    # # TODO: Fix! it doesn't return anything, most likely does searchAsync() and then filters by params
    # # bc the Tags param works for Module, and if Tags value = Tag1 only returns module resources that have this
    # It "find script resource when Tags parameter is specified with single value" {
    #     $tagValue = "Tag-Fabrikam-Script-2.5"
    #     $res = Find-PSResource -Tags $tagValue | Where-Object { $_.Name -eq "Fabrikam-Script" }
    #     $res | Should -Not -BeNullOrEmpty
    #     $res.Name | Should -Be "Fabrikam-Script"
    # }

    # # Purpose: find resource using Tags parameter, multiple tag values
    # #
    # # Action: Find-PSResource -Tags "Tag-Fabrikam-Script-2.5" | Where-Object { $_.Name -eq "Fabrikam-Script" }
    # #
    # # Expected Result: should return resource that has specified Tags value
    # # TODO: Fix! it doesn't return anything, most likely does searchAsync() and then filters by params
    # # bc the Tags param works for Module, and if Tags value = Tag1 only returns module resources that have this
    # It "find script resource when Tags parameter is specified, with multiple values" {
    #     $tagValue1 = "Tag-Fabrikam-Script-2.5"
    #     $tagValue2 = "Tag1"
    #     $res = Find-PSResource -Tags $tagValue1,$tagValue2 | Where-Object { $_.Name -eq "Fabrikam-Script" }
    #     $res | Should -Not -BeNullOrEmpty
    #     $res.Name | Should -Be "Fabrikam-Script"
    # }

    
    # Purpose: not find un-available resource from specified repository, when given Repository parameter
    #
    # Action: Find-PSResource -Name Get-WindowsAutoPilotInfo -Repository PoshTestGallery
    #
    # Expected Result: should not find resource from specified PSGallery repository becuase resource doesn't exist there
    It "find resource from specified repository, when given Repository parameter" {
        $res = Find-PSResource -Name Get-WindowsAutoPilotInfo -Repository $PoshTestGalleryName
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