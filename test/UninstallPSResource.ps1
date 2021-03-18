# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Uninstall-PSResource for Modules' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        Get-NewPSResourceRepositoryFile
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

### TESTED
    # Purpose: to uninstall all resources when no parameters are specified
    # Action: Uninstall-PSResource
    # Expected-Result: uninstall all (more than 1) resources
    It "Uninstall Specific Module Resource by Name" {
        $pkg = Uninstall-PSResource -name Bicep
        ### validate that it actually uninstalled 
    }

### TESTED
    # Purpose: to uninstall a specific resource by name
    # Action: Uninstall-PSResource -Name "ContosoServer"
    # Expected Result: Should uninstall ContosoServer resource
    It "Uninstall a List of Module Resources" {
        $pkg = Uninstall-PSResource -Name Benchpress, bogreader 
        #$specItem.Name | Should -Be "ContosoServer"
    }


### Uninstall should not take a wildcard right now
    # Purpose: to uninstall a resource(s) with regex in name parameter
    # Action: Uninstall-PSResource -Name Contoso*
    # Expected Result: should uninstall multiple resources,namely atleast ContosoServer, ContosoClient, Contoso
#    It "Uninstall multiple Resource(s) with Wildcards for Name Param" {
#        $res = Uninstall-PSResource -Name Contoso*
#        $res.Count | Should -BeGreaterOrEqual 1
#    }


    # Test a prerelease version
    # Purpose: not Uninstall resource with invalid verison, given Version parameter -> (1.5.0.0)
    # Action: Uninstall-PSResource -Name "ContosoServer" -Version "(1.5.0.0)"
    # Expected Result: should not return a resource as version is invalid
    It "not Uninstall Command resource given Name to validate handling an invalid version" {
        $res = Uninstall-PSResource -Name "NetworkingDSC" -version "*" -PrereleaseOnly
        $res | Should -BeNullOrEmpty
    }

    
    # Test a prerelease version
    # Purpose: not Uninstall resource with invalid verison, given Version parameter -> (1.5.0.0)
    # Action: Uninstall-PSResource -Name "ContosoServer" -Version "(1.5.0.0)"
    # Expected Result: should not return a resource as version is invalid
    It "not Uninstall Command resource given Name to validate handling an invalid version" {
        $res = Uninstall-PSResource -Name "ContosoServer" -Version "1.0.0" -PrereleaseOnly
        $res | Should -BeNullOrEmpty
    }

### TESTED 
    # Purpose: uninstall resource when given Name, Version param not null
    # Action: Uninstall-PSResource -Name ContosoServer -Repository PoshTestGallery
    # Expected Result: returns ContosoServer resource
    It "find resource when given Name to <Reason> <Version>" -TestCases @(
        @{Version="[2.0.0.0]";          ExpectedVersion="2.0.0.0"; Reason="validate version, exact match"},    ### passed
        @{Version="2.0.0.0";            ExpectedVersion="2.0.0.0"; Reason="validate version, exact match without bracket syntax"},  ### passed
        @{Version="[1.0.0.0, 2.5.0.0]"; ExpectedVersion="2.5.0.0"; Reason="validate version, exact range inclusive"},  ### passed
        @{Version="(1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, exact range exclusive"}, ### passed
        @{Version="(1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version exclusive"},  ### passed
        @{Version="[1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version inclusive"},  ### passed
        @{Version="(,1.5.0.0)";         ExpectedVersion="1.0.0.0"; Reason="validate version, maximum version exclusive"},  ### passed
        @{Version="(,1.5.0.0]";         ExpectedVersion="1.5.0.0"; Reason="validate version, maximum version inclusive"}, ### passed
        @{Version="[1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}   ### passed
    ) {
        param($Version, $ExpectedVersion)
        $res = Uninstall-PSResource -Name "Pester" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "Pester"
        $res.Version | Should -Be $ExpectedVersion
    }

### TODO 
    # Purpose: not uninstall resources with invalid version
    # Action: Uninstall-PSResource -Name "ContosoServer" -Version "(1.5.0.0)"
    # Expected Result: should not return a resource
    It "not find resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(1.5.0.0)';       Description="exlcusive version (8.1.0.0)"},   ### passed
        @{Version='[1-5-0-0]';       Description="version formatted with invalid delimiter"},  ### passed
        @{Version='[1.*.0]';         Description="version with wilcard in middle"},   ### throws error now
        @{Version='[*.5.0.0]';       Description="version with wilcard at start"},    ### throws error now
        @{Version='[1.*.0.0]';       Description="version with wildcard at second digit"},        # throws error now
        @{Version='[1.5.*.0]';       Description="version with wildcard at third digit"}           # throws error now
        @{Version='[1.5.0.*';        Description="version with wildcard at end"},                #  --- uninstalled when it shouldn't have 
        @{Version='[1..0.0]';        Description="version with missing digit in middle"},   ### throws error now
        @{Version='[1.5.0.]';        Description="version with missing digit at end"},   ### throws error now
        @{Version='[1.5.0.0.0]';     Description="version with more than 4 digits"}    #### throws error now
    ) {
        param($Version, $Description)

        $res = $null
        try {
            $res = Uninstall-PSResource -Name "ContosoServer" -Version $Version -Repository $TestGalleryName -ErrorAction Ignore
        }
        catch {}
        
        $res | Should -BeNullOrEmpty
    }

### TESTED 
    # DO NOT INSTALL A MODULE THAT IS NOT THE CORRECT VERSIOn
    # Purpose: not Uninstall resource with invalid verison, given Version parameter -> (1.5.0.0)
    # Action: Uninstall-PSResource -Name "ContosoServer" -Version "(1.5.0.0)"
    # Expected Result: should not return a resource as version is invalid
    It "not Uninstall Command resource given Name to validate handling an invalid version" {
        $res = Uninstall-PSResource -Name "ContosoServer" -Version "(0.0.0.1)"
        $res | Should -BeNullOrEmpty
    }

###  TESTED
    # Purpose: uninstall resources when given Name, Version not null --> '*'
    # Action: Uninstall-PSResource -Name ContosoServer -Version "*" -Repository PoshTestGallery
    # Expected Result: returns 4 ContosoServer resources (of all versions in descending order)
    It "Uninstall resources when given Name, Version not null --> '*'" {
        $res = Uninstall-PSResource -Name ContosoServer -Version "*" -Repository $TestGalleryName
        $res.Count | Should -BeGreaterOrEqual 4
    }


### TESTED
    # should uninstall the latest version
    # Purpose: Uninstall resource with latest version (including prerelease version) given Prerelease parameter
    # Action: Uninstall-PSResource -Name "test_module" -Prerelease
    # Expected Result: should return latest version (may be a prerelease version)
    It "Uninstall resource with latest (including prerelease) version given Prerelease parameter" {
        # test_module resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Uninstall-PSResource -Name "test_module"
        $res.Version | Should -Be "5.0.0.0"

        $resPrerelease = Uninstall-PSResource -Name "test_module" -Prerelease
        $resPrerelease.Version | Should -Be "5.2.5.0"        
    }

    
    # Purpose: UnInstallModuleWithWhatIf
    # Action: Find-Module ContosoServer | Install-Module | UnInstall-Module -WhatIf
    # Expected Result: it should not uninstall the module
    It "Uninstall resource with latest (including prerelease) version given Prerelease parameter" {
        # test_module resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Uninstall-PSResource -Name "ActiveDirectoryTools" -WhatIf

        # check to see if it's still installed 
    }


    # Purpose: this module is a dependency on another module
    # Action: Find-Module ContosoServer | Install-Module | UnInstall-Module -WhatIf
    # Expected Result: it should not uninstall the module
    It "Uninstall resource with latest (including prerelease) version given Prerelease parameter" {
        # test_module resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Uninstall-PSResource -Name "PackageManagement" -Force

        # check to see if it's still installed 
    }


### consider adding uninstall -include dependencies
    # Purpose: Uninstall a resource and associated dependecies given IncludeDependencies parameter
    # Action: Uninstall-PSResource ModuleWithDependencies1 -IncludeDependencies
    # Expected Result: should return resource specified and all associated dependecy resources
#    It "Uninstall resource with IncludeDependencies parameter" {
#        $res = Uninstall-PSResource ModuleWithDependencies1 -IncludeDependencies -Version "[1.0,2.0]"
#        $res.Count | Should -BeGreaterOrEqual 11
#    }

    # Purpose: Uninstall resource in local repository given Repository parameter
    # Action: Uninstall-PSResource -Name "local_command_module" -Repository "psgettestlocal"
    # Expected Result: should Uninstall resource from local repository
    It "Uninstall resource in local repository given Repository parameter" {
        $publishModuleName = "TestFindModule"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $publishModuleName

        $res = Uninstall-PSResource -Name $publishModuleName -Repository "psgettestlocal"
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be $publishModuleName
        $res.Repository | Should -Be "psgettestlocal"
    }






  <#  Purpose: Error in use message
    Action: Install a module, update the module, get module count
    Expected Result: should be able to get the installed module.
    #>


        # Purpose: ValidateModuleIsInUseErrorDuringUninstallModule
    #
    # Action: Install and import a module then try to uninstall the same version
    #
    # Expected Result: should fail with an error
    #


### Uninstall Errors:
    # ValidateUninstallScriptWithMultiNamesAndVersion (any version) 
    # ValidateUninstallScriptWithSingleNameInvalidMinMaxRange 
}



Describe 'Test Uninstall-PSResource for Scripts' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        Get-NewPSResourceRepositoryFile
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }


### TESTED
    # Purpose: to uninstall all resources when no parameters are specified
    # Action: Uninstall-PSResource
    # Expected-Result: uninstall all (more than 1) resources
    It "Uninstall Specific Script by Name" {
        $pkg = Uninstall-PSResource -name Test-RPC
        ### validate that it actually uninstalled 
    }

### TESTED
    # Purpose: to uninstall a specific resource by name
    # Action: Uninstall-PSResource -Name "ContosoServer"
    # Expected Result: Should uninstall ContosoServer resource
    It "Uninstall a List of Scripts" {
        $pkg = Uninstall-PSResource -Name adsql, airoute 
        #$specItem.Name | Should -Be "ContosoServer"
    }


### TESTED
    # Purpose: uninstall resource when given Name, Version param not null
    # Action: Uninstall-PSResource -Name ContosoServer -Repository PoshTestGallery
    # Expected Result: returns ContosoServer resource
    It "find resource when given Name to <Reason> <Version>" -TestCases @(
        @{Version="[2.0.0.0]";          ExpectedVersion="2.0.0.0"; Reason="validate version, exact match"},    ### passed
        @{Version="2.0.0.0";            ExpectedVersion="2.0.0.0"; Reason="validate version, exact match without bracket syntax"},  ###  passed 
        @{Version="[1.0.0.0, 2.5.0.0]"; ExpectedVersion="2.5.0.0"; Reason="validate version, exact range inclusive"},  ### passed
        @{Version="(1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, exact range exclusive"}, ### passed
        @{Version="(1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version exclusive"},  ### passed
        @{Version="[1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version inclusive"},  ### passed
        @{Version="(,1.5.0.0)";         ExpectedVersion="1.0.0.0"; Reason="validate version, maximum version exclusive"},  ### passed
        @{Version="(,1.5.0.0]";         ExpectedVersion="1.5.0.0"; Reason="validate version, maximum version inclusive"}, ### passed
        @{Version="[1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}   ### passed
    ) {
        param($Version, $ExpectedVersion)
        $res = Uninstall-PSResource -Name "Pester" -Version $Version
        $res.Name | Should -Be "Pester"
        $res.Version | Should -Be $ExpectedVersion
    }





### TESTED
    # Purpose: not uninstall resources with invalid version
    # Action: Uninstall-PSResource -Name "ContosoServer" -Version "(1.5.0.0)"
    # Expected Result: should not return a resource
    It "not find resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(1.5.0.0)';       Description="exlcusive version (8.1.0.0)"},   ### failed, did not throw error 
        @{Version='[1-5-0-0]';       Description="version formatted with invalid delimiter"},  ### failed did not throw error
        @{Version='[1.*.0]';         Description="version with wilcard in middle"},   ### throws error now
        @{Version='[*.5.0.0]';       Description="version with wilcard at start"},    ### throws error now
        @{Version='[1.*.0.0]';       Description="version with wildcard at second digit"},        # throws error now
        @{Version='[1.5.*.0]';       Description="version with wildcard at third digit"}           # throws error now
        @{Version='[1.5.0.*';        Description="version with wildcard at end"},                # throws error now
        @{Version='[1..0.0]';        Description="version with missing digit in middle"},   ### throws error now  
        @{Version='[1.5.0.]';        Description="version with missing digit at end"},   ### throws error now
        @{Version='[1.5.0.0.0]';     Description="version with more than 4 digits"}    #### throws error now
    ) {
        param($Version, $Description)

        $res = $null
        try {
            $res = Uninstall-PSResource -Name "ContosoServer" -Version $Version -Repository $TestGalleryName -ErrorAction Ignore
        }
        catch {}
        
        $res | Should -BeNullOrEmpty
    }

### TESTED
    # DO NOT INSTALL A MODULE THAT IS NOT THE CORRECT VERSIOn
    # Purpose: not Uninstall resource with invalid verison, given Version parameter -> (1.5.0.0)
    # Action: Uninstall-PSResource -Name "ContosoServer" -Version "(1.5.0.0)"
    # Expected Result: should not return a resource as version is invalid
    It "not Uninstall Command resource given Name to validate handling an invalid version" {
        $res = Uninstall-PSResource -Name "adsql" -Version "(0.0.0.1)"
        $res | Should -BeNullOrEmpty
    }

###  TESTED
    # Purpose: uninstall resources when given Name, Version not null --> '*'
    # Action: Uninstall-PSResource -Name ContosoServer -Version "*" -Repository PoshTestGallery
    # Expected Result: returns 4 ContosoServer resources (of all versions in descending order)
    It "Uninstall resources when given Name, Version not null --> '*'" {
        $res = Uninstall-PSResource -Name ADSQL -Version "*"
        $res.Count | Should -BeGreaterOrEqual 4
    }



    # Purpose: UnInstallModuleWithWhatIf
    # Action: Find-Module ContosoServer | Install-Module | UnInstall-Module -WhatIf
    # Expected Result: it should not uninstall the module
    It "Uninstall resource with latest (including prerelease) version given Prerelease parameter" {
        # test_module resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Uninstall-PSResource -Name "ActiveDirectoryTools" -WhatIf

        # check to see if it's still installed 
    }
}


### TODO:
#-whatif functionality  (done)
#-confirm ????  do we want to use confirm?  no v2 does not use confirm

#- prerelease only tests  1))  ########## COME BACK TO THIS


#- fix version tests  2)    #### done

#- add whatif test  3)


# fix what if print statement:  Uninstall-PSResource ActiveDirectoryTools -WhatIf
#What if: Performing the operation "Uninstall-PSResource" on target "Uninstall-PSResource".


## - "this module is a dependency for another module, if you want to uninstall despite this, use the -force parameter "


########################0) update file for old PR 
########################1) clean everything up
########################2) look at find for admin/elevated privileges stuff

########################4) put in PR 



##### LATER
- later have a uninstall -include dependencies to uninstall all Dependencies   ##### add this later????


### complete all of this by wednesday and have the PR up wednesday night for Thursday morning