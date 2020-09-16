# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -WarningAction SilentlyContinue -force

Describe 'Test Find-PSResource for Module' {

    # Purpose: to check if v3 installs the PSGallery repo by default
    #
    # Action: Get-PSResourceRepository PSGallery
    #
    # Expected Result: Should find that the PSGallery resource repo is already registered in v3
    It 'find the default registered PSGallery' {
        $repo = Get-PSResourceRepository -Name @(Get-PSGalleryName)
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
        $repo | Should -Not -BeNullOrEmpty
        $repo.Name | should be @(Get-PoshTestGalleryName)
        $repo.URL | Should be @(Get-PoshTestGalleryLocation)
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
    It "Find Specific Module Resource by Name" {
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
        $res = Find-PSResource -Name Contoso*
        $res.Count | Should -BeGreaterOrEqual 1
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
    # TODO: implement
    # It "Find specific resource with range wildcard" {
    #     $res = Find-PSResource -Name "Co[nN]t?soS[a-z]r?er"
    #     $res.Name | Should -Be "ContosoServer"
    # }

    # Purpose: should not find a not available resource with range wildcards in name parameter
    #
    # Action: Find-PSResource -Name "Co[nN]t?soS[a-z]r?eW"
    #
    # Expected Result: should find ContosoServer resource
    # It "Find Not Available Resource with Range Wildcard for Name Param" {
    #     $res = Find-PSResource -Name "Co[nN]t?soS[a-z]r?eW"
    #     $res | Should -BeNullOrEmpty
    # }

    # Purpose: find resource by Type parameter
    #
    # Action: Find-PSResource -Type Module -Repository PoshTestGallery
    #
    # Expected Result: Should return all resources of type Module
    # It "Find Resource with Type param of value Module" {
    #    $res = Find-PSResource -Type Module -Repository PoshTestGallery
    #    $res.Count | Should -Be 1639
    # }

    # Purpose: find resource by Type parameter
    #
    # Action: Find-PSResource -Type Script -Repository PoshTestGallery
    #
    # Expected Result: Should return all resources of type Module
    # It "Find Resource with Type param of value Script" {
    #    $res = Find-PSResource -Type Script -Repository PoshTestGallery
    #    $res.Count | Should -Be 320
    # }
 
    # Purpose: find resource when given ModuleName, Version param null, and Name is of a script resource
    #
    # Action: Find-PSResource -ModuleName Fabrikam-ServerScript -Repository PoshTestGallery
    #
    # Expected Result: returns nothing, prints error message
    It "find resource when given ModuleName, Version param null, and Name is of a script resource" {
        $res = Find-PSResource -ModuleName Fabrikam-ServerScript -Repository @(Get-PoshTestGalleryName)
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find resource when given ModuleName, Version param null, and Name is of a module resource
    #
    # Action: Find-PSResource -ModuleName ContosoServer -Repository PoshTestGallery
    #
    # Expected Result: returns ContosoServer resource   
    It "find resource when given ModuleName, Version param null, and Name is of a module resource" {
        $res = Find-PSResource -ModuleName ContosoServer -Repository @(Get-PoshTestGalleryName)
        $res.Name | Should -Be "ContosoServer"
    }

    # Purpose: find resource when given Name, Version param null, and name is of a script resource
    #
    # Action: Find-PSResource -Name Fabrikam-ServerScript -Repository PoshTestGallery
    #
    # Expected Result: returns Fabrikam-ServerScript resource   
    It "find resource when given Name, Version param null, and name is of a script resource" {
        $res = Find-PSResource -Name Fabrikam-ServerScript -Repository @(Get-PoshTestGalleryName)
        $res.Name | Should -Be "Fabrikam-ServerScript"
    }

    # Purpose: find resource when given Name, Version param null, and name is of a module resource
    #
    # Action: Find-PSResource -Name ContosoServer -Repository PoshTestGallery
    #
    # Expected Result: returns ContosoServer resource
    It "find resource when given Name, Version param null, and name is of a module resource" {
        $res = Find-PSResource -Name ContosoServer -Repository @(Get-PoshTestGalleryName)
        $res.Name | Should -Be "ContosoServer"
    }

    # Purpose: find resource when given ModuleName, Version not null --> [2.0], and name is of a script resource
    #
    # Action: Find-PSResource -ModuleName Fabrikam-ServerScript -Repository PoshTestGallery -Version "[2.0]"
    #
    # Expected Result: doesn't return anything, prints error message
    It "find resource when given ModuleName, Version not null --> [2.0], and name is of a script resource" {
        $res = Find-PSResource -ModuleName Fabrikam-ServerScript -Repository @(Get-PoshTestGalleryName) -Version "[2.0.0.0]"
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find resource when given ModuleName, Version not null --> [2.0], name is of a module resource
    #
    # Action: Find-PSResource -ModuleName ContosoServer -Repository PoshTestGallery -Version "[2.0]"
    #
    # Expected Result: returns ContosoServer resource with specified version
    It "find resource when given ModuleName, Version not null --> [2.0], name is of a module resource" {
        $res = Find-PSResource -ModuleName ContosoServer -Repository @(Get-PoshTestGalleryName) -Version "[2.0.0.0]"
        $res.Name | Should -Be "ContosoServer"
        $res.Version | Should -Be "2.0.0.0"
    }

    # Purpose: find resource when given ModuleName, Version not null --> [2.0], name is of a script resource
    #
    # Action: Find-PSResource -Name Fabrikam-ServerScript -Repository PoshTestGallery -Version "[2.0]"
    #
    # Expected Result: returns Fabrikam-ServerScript resource with specified version
    It "find resource when given ModuleName, Version not null --> [2.0], name is of a script resource" {
        $res = Find-PSResource -Name Fabrikam-ServerScript -Repository @(Get-PoshTestGalleryName) -Version "[2.0.0.0]"
        $res.Name | Should -Be "Fabrikam-ServerScript"
        $res.Version | Should -Be "2.0.0.0"
    }

    # Purpose: find resource when given Name, Version not null --> [2.0], name is of a module resource
    #
    # Action: Find-PSResource -Name ContosoServer -Repository PoshTestGallery -Version "[2.0]"
    #
    # Expected Result: returns ContosoServer resource with specified version
    It "find resource when given Name, Version not null --> [2.0.0.0], name is of a module resource" {
        $res = Find-PSResource -Name ContosoServer -Repository @(Get-PoshTestGalleryName) -Version "[2.0.0.0]"
        $res.Name | Should -Be "ContosoServer"
        $res.Version | Should -Be "2.0.0.0"
    }

    # Purpose: find resource when given ModuleName, Version not null --> [1.0, 2.5], name is of a script resource
    #
    # Action: Find-PSResource -ModuleName Fabrikam-ServerScript -Repository PoshTestGallery -Version "[1.0, 2.5]"
    #
    # Expected Result: shouldn't return any resource, prints error message
    It "find resource when given ModuleName, Version not null --> [1.0.0.0, 2.5.0.0], name is of a script resource"{
        $res = Find-PSResource -ModuleName Fabrikam-ServerScript -Repository @(Get-PoshTestGalleryName) -Version "[1.0.0.0, 2.5.0.0]"
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find resource when given ModuleName, Version not null --> [1.0, 2.5], name is of a module resource
    #
    # Action: Find-PSResource -ModuleName ContosoServer -Repository PoshTestGallery -Version "[1.0, 2.5]"
    #
    # Expected Result: returns resource with latest version in the specified range
    It "find resource when given ModuleName, Version not null --> [1.0.0.0, 2.5.0.0], name is of a module resource" {
        $res = Find-PSResource -ModuleName ContosoServer -Repository @(Get-PoshTestGalleryName) -Version "[1.0.0.0, 2.5.0.0]"
        $res.Name | Should -Be "ContosoServer"
        $res.Version | Should -Be "2.5.0.0"
    }

    # Purpose: find resource when given Name, Version not null --> [1.0, 2.5], name is of a script resource
    #
    # Action: Find-PSResource -Name Fabrikam-ServerScript -Repository PoshTestGallery -Version "[1.0, 2.5]"
    #
    # Expected Result: returns resource with latest version in the specified range
    It "find resource when given Name, Version not null --> [1.0.0.0, 2.5.0.0], name is of a script resource" {
        $res = Find-PSResource -Name Fabrikam-ServerScript -Repository @(Get-PoshTestGalleryName) -Version "[1.0.0.0, 2.5.0.0]"
        $res.Name | Should -Be "Fabrikam-ServerScript"
        $res.Version | Should -Be "2.5.0.0"
    }

    # Purpose: find resources when given Name, Version not null --> [1.0, 2.5], name is of module resource
    #
    # Action: Find-PSResource -Name ContosoServer -Repository PoshTestGallery -Version "[1.0, 2.5]"
    #
    # Expected Result: returns resource with latest version in the specified range
    It "find resources when given Name, Version not null --> [1.0.0.0, 2.5.0.0], name is of module resource" {
        $res = Find-PSResource -Name ContosoServer -Repository @(Get-PoshTestGalleryName) -Version "[1.0.0.0, 2.5.0.0]"
        $res.Name | Should -Be "ContosoServer"
        $res.Version | Should -Be "2.5.0.0"
    }

    # Purpose: find resources when given ModuleName, Version not null --> '*', name is of a script resource
    #
    # Action: Find-PSResource -ModuleName Fabrikam-ServerScript -Version "*" -Repository PoshTestGallery
    #
    # Expected Result: shouldn't return any resource, prints error message
    It "find resources when given ModuleName, Version not null --> '*', name is of a script resource" {
        $res = Find-PSResource -ModuleName Fabrikam-ServerScript -Version "*" -Repository @(Get-PoshTestGalleryName)
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find resources when given ModuleName, Version not null --> '*', name is of a module resource
    #
    # Action: Find-PSResource -ModuleName ContosoServer -Version "*" -Repository PoshTestGallery
    #
    # Expected Result: returns 4 ContosoServer resources (of all versions in descending order)
    It "find resources when given ModuleName, Version not null --> '*', name is of a module resource" {
        $res = Find-PSResource -ModuleName ContosoServer -Version "*" -Repository @(Get-PoshTestGalleryName)
        $res.Count | Should -BeGreaterOrEqual 4
    }

    # Purpose: find resources when given Name, Version not null --> '*', name is of a script resource
    #
    # Action: Find-PSResource -Name Fabrikam-ServerScript -Version "*" -Repository PoshTestGallery
    #
    # Expected Result: returns 5  Fabrikam-ServerScript resources (of all versions in descending order)
    It "find resources when given Name, Version not null --> '*', name is of a script resource" {
        $res = Find-PSResource -Name Fabrikam-ServerScript -Version "*" -Repository @(Get-PoshTestGalleryName)
        $res.Count | Should -BeGreaterOrEqual 5
    }

    # Purpose: find resources when given Name, Version not null --> '*', name is of a module resource
    #
    # Action: Find-PSResource -Name ContosoServer -Version "*" -Repository PoshTestGallery
    #
    # Expected Result: returns 4  ContosoServer resources (of all versions in descending order)
    It "find resources when given Name, Version not null --> '*', name is of a module resource" {
        $res = Find-PSResource -Name ContosoServer -Version "*" -Repository @(Get-PoshTestGalleryName)
        $res.Count | Should -BeGreaterOrEqual  4
    }

    # Purpose: to find a prerelease resource with the prerelease param
    #
    # Action: Find-PSResource -Name "PSGTEST-PublishPrereleaseModule-3139" -Prerelease
    #
    # Expected Result: without prerelease param latest nonpreview version (or none if none exists)
    # should be returned. With it the module should be returned and valid
    It "Find Resource with Prerelease param" {
        # this resource only has a prerelease version, can't be found without the prerelease param
        $res = Find-PSResource -Name "PSGTEST-PublishPrereleaseModule-3139"
        $res | should -BeNullOrEmpty

        # add prerelease param to cmdlet query
        $resWPrelease = Find-PSResource -Name "PSGTEST-PublishPrereleaseModule-3139" -Prerelease
        $resWPrelease | should -not -BeNullOrEmpty
        $resWPrelease.Name | should -be "PSGTEST-PublishPrereleaseModule-3139"
    }

    # Purpose: to find a resource given Tags parameter with one value
    #
    # Action: Find-PSResource -Tags CommandsAndResource | Where-Object { $_.Name -eq "DscTestModule" }
    #
    # Expected Result: should return all resources with that tag, and then filer by name
    It "Find a resource given Tags parameter with one value" {
        $res = Find-PSResource -Tags CommandsAndResource | Where-Object { $_.Name -eq "DscTestModule" }
        $res | Should -not -BeNullOrEmpty
        $res.Name | Should -Be "DscTestModule"
    }

    # Purpose: find resource(s) given multiple tags for the Tags parameter.
    # Note: if given multiple tags the cmdlet will find all resources that have either of those tags
    # thus testing by count and ensuring multiple tags' count >= only one of those tag's count
    # makes sense here. If the object were to return a .Tags property switch to that in testing.
    #
    # Action: Find-PSResource -Tags CommandsAndResource,DSC,Tag1
    #
    # Expected Result: Should return more resources than if just queried with -Tags CommandsAndResources
    It "Find a resource given tags parameter with multiple values" {
        $resSingleTag = Find-PSResource -Tags CommandsAndResource
        $res = Find-PSResource -Tags CommandsAndResource,DSC,Tag1
        $res | Should -not -BeNullOrEmpty
        $res.Count | Should -BeGreaterOrEqual $resSingleTag.Count
    }

    # Purpose: find a resource in a specific repository, given Repository parameter
    #
    # Action: Find-PSResource ContosoServer -Repository $PoshTestGalleryName
    #
    # Expected Result: Should find the resource from the specified repository
    It "Find resource given repository parameter" {
        # ContosoServer resource doesn't exist in the PSGallery repository
        $res = Find-PSResource ContosoServer -Repository @(Get-PSGalleryName)
        $res | Should -BeNullOrEmpty

        # ContosoServer resource exists in the PostTestGalleryRepository
        $resCorrectRepo = Find-PSResource ContosoServer -Repository @(Get-PoshTestGalleryName)
        $resCorrectRepo | Should -Not -BeNullOrEmpty
        $resCorrectRepo.Repository | Should -Be "PoshTestGallery"
    }

    # Purpose: find resource given Repository param, when resource of same name exists in multiple repos
    # I guess I'm testing that the cmdlet does't just find it in first available repo and return that, may be a redundant test tho
    #
    # Action: Find-PSResource xActiveDirectory -Repository "PSGallery"
    #
    # Expected Result: Returns resource from specified repository
    It "Find Resource given repository parameter, where resource exists in multiple repos" {
        $res = Find-PSResource xActiveDirectory
        $res.Repository | Should -Be "PoshTestGallery" # first availability found in PostTestGallery
        # check that it can be returned from non-first-availability/non-default repo
        $resNonDefault = Find-PSResource xActiveDirectory -Repository @(Get-PSGalleryName)
        $resNonDefault.Repository | Should -Be "PSGallery"
    }

    # Purpose: find a resource and associated dependecies given IncludeDependencies parameter
    #
    # Action: Find-PSResource ModuleWithDependencies1 -IncludeDependencies
    #
    # Expected Result: should return resource specified and all associated dependecy resources
    # Note: Outputs a Errer parsing version range error, but unsure how to include Min and Max version. Todo: fix!
    # also todo: uncomment below code when .Dependencies property is added back
    It "Find resource with IncludeDependencies parameter" {
        # Register-PSResourceRepository $PoshTestGalleryName -URL $PostTestGalleryLocation -Trusted
        $res = Find-PSResource ModuleWithDependencies1 -IncludeDependencies -Version "[1.0,2.0]"
        # $dependencyModuleNames = $res.Dependencies.Name #is currently 0 bc .Dependecies property dne
        # $dependencyModuleNames | ForEach-Object{ res.Name | Should -Contain $_}
        # $dependencyModuleNames.Count | Should -Not -Be 0
        # $res.Count | Should -BeGreaterOrEqual ($dependencyModuleNames.Count + 1)
        $res.Count | Should -BeGreaterThan 1
        $res.Count | Should -Be 11
    }
}
