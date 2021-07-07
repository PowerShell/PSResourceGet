# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Install-PSResource for Module' {


    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        $NuGetGalleryName = Get-NuGetGalleryName
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos
        Get-PSResourceRepository
    }

    AfterEach {
        Uninstall-PSResource "TestModule"
        Uninstall-PSResource "TestModule99"

        Uninstall-PSResource "myTestModule"
        Uninstall-PSResource "myTestModule2"
        uninstall-PSResource "testModuleWithlicense"
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Install specific module resource by name" {
        Get-PSResourceRepository

        Install-PSResource -Name "TestModule" -Repository $TestGalleryName  
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 
        $pkg.Version | Should -Be "1.3.0"
    }

    
    It "Install specific script resource by name" {
        Install-PSResource -Name "TestTestScript" -Repository $TestGalleryName  
        $pkg = Get-InstalledPSResource "TestTestScript"
        $pkg.Name | Should -Be "TestTestScript" 
        $pkg.Version | Should -Be "1.3.1.0"
    }

    It "Install multiple resources by name" {
        $pkgNames = @("TestModule","TestModule99")
        Install-PSResource -Name $pkgNames -Repository $TestGalleryName  
        $pkg = Get-Module $pkgNames -ListAvailable
        $pkg.Name | Should -Be $pkgNames
    }

    It "Should not install resource given nonexistant name" {
        Install-PSResource -Name NonExistantModule -Repository $TestGalleryName  
        $pkg = Get-Module "NonExistantModule" -ListAvailable
        $pkg.Name | Should -BeNullOrEmpty
    }

    # Do some version testing, but Find-PSResource should be doing thorough testing
    It "Should install resource given name and exact version" {
        Install-PSResource -Name "TestModule" -Version "1.2.0" -Repository $TestGalleryName  
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule"
        $pkg.Version | Should -Be "1.2.0"
    }


    It "Should install resource given name and exact version with bracket syntax" {
        Install-PSResource -Name "TestModule" -Version "[1.2.0]" -Repository $TestGalleryName  
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule"
        $pkg.Version | Should -Be "1.2.0"
    }

    It "Should install resource given name and exact range inclusive [1.0.0, 1.1.1]" {
        Install-PSResource -Name "TestModule" -Version "[1.0.0, 1.1.1]" -Repository $TestGalleryName  
        $pkg = Get-Module "TestModule" -ListAvailable 
        $pkg.Name | Should -Be "TestModule"
        $pkg.Version | Should -Be "1.1.1"
    }

    It "Should install resource given name and exact range exclusive (1.0.0, 1.1.1)" {
        Install-PSResource -Name "TestModule" -Version "(1.0.0, 1.1.1)" -Repository $TestGalleryName  
        $pkg = Get-Module "TestModule" -ListAvailable 
        $pkg.Name | Should -Be "TestModule"
        $pkg.Version | Should -Be "1.1"
    }

    It "Should not install resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(1.2.0.0)';       Description="exclusive version (2.10.0.0)"},
        @{Version='[1-2-0-0]';       Description="version formatted with invalid delimiter [1-2-0-0]"}
    ) {
        param($Version, $Description)

        Install-PSResource -Name "TestModule" -Version $Version -Repository $TestGalleryName
        $res = Get-Module "TestModule" -ListAvailable
        $res | Should -BeNullOrEmpty
    }

    It "Install resource when given Name, Version '*', should install the latest version" {
        $pkg = Install-PSResource -Name "TestModule" -Version "*" -Repository $TestGalleryName
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule"
        $pkg.Version | Should -Be "1.3.0"
    }

    It "Install resource with latest (including prerelease) version given Prerelease parameter" {
        # test_module resource's latest version is a prerelease version, before that it has a non-prerelease version
        $pkg = Install-PSResource -Name "TestModulePrerelease" -Prerelease -Repository $TestGalleryName 
        $pkg = Get-Module "TestModulePrerelease" -ListAvailable
        $pkg.Version | Should -Be "0.0.1"
        $pkg.PrivateData.PSData.Prerelease | Should -Be "preview"
    }



    
    It "Install a module with a dependency" {
        # test_module resource's latest version is a prerelease version, before that it has a non-prerelease version
        $pkg = Install-PSResource -Name "PSGetTestModule" -Prerelease -Repository $TestGalleryName 
        $pkg = Get-Module "PSGetTestModule" -ListAvailable
        $pkg.Version | Should -Be "2.0.2"
        $pkg.PrivateData.PSData.Prerelease | Should -Be "-alpha1"
    }


    It "Install resource via InputObject by piping from Find-PSresource" {
        Find-PSResource -Name "TestModule" -Repository $TestGalleryName | Install-PSResource
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 
        $pkg.Version | Should -Be "1.3.0"
    }

    # Windows only
    It "Install resource under CurrentUser scope" {
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName -Scope CurrentUser
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 
        $pkg.Path.Contains("Documents") | Should -Be $true

    }

    # Windows only
    It "Install resource under AllUsers scope" {
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName -Scope AllUsers
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 
        write-host $pkg.Path
        $pkg.Path.Contains("Program Files") | Should -Be $true
    }

    # Windows only
    It "Install resource under no specified scope" {
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 
        $pkg.Path.Contains("Documents") | Should -Be $true
    }




<#
    It "Should not install resource that is already installed" {
        $a = (Get-PSResourceRepository)
        write-host $a.Name
        write-host $a.Priority
        write-host $a.Trusted

        Install-PSResource -Name "TestModule" -Repository $TestGalleryName
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName -WarningVariable WarningVar -warningaction SilentlyContinue
        $WarningVar | Should -Not -BeNullOrEmpty
    }
#>


    It "Reinstall resource that is already installed with -Reinstall parameter" {
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule"
        $pkg.Version | Should -Be "1.3.0"
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName -Reinstall
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule"
        $pkg.Version | Should -Be "1.3.0"
    }

    It "Install resource that requires accept license with -AcceptLicense flag" {
        Install-PSResource -Name "testModuleWithlicense" -Repository $TestGalleryName -AcceptLicense
        $pkg = Get-InstalledPSResource "testModuleWithlicense"
        $pkg.Name | Should -Be "testModuleWithlicense" 
        $pkg.Version | Should -Be "0.0.1.0"
    }

    It "Install resource should not prompt 'trust repository' if repository is not trusted but -TrustRepository is used" {
        Set-PSResourceRepository PoshTestGallery -Trusted:$false

        Install-PSResource -Name "TestModule" -Repository $TestGalleryName -TrustRepository
        
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 

        Set-PSResourceRepository PoshTestGallery -Trusted
    }

    It "Install resource with cmdlet names from a module already installed (should clobber)" {
        Install-PSResource -Name "myTestModule" -Repository $TestGalleryName  
        $pkg = Get-InstalledPSResource "myTestModule"
        $pkg.Name | Should -Be "myTestModule" 
        $pkg.Version | Should -Be "0.0.3.0"

        Install-PSResource -Name "myTestModule2" -Repository $TestGalleryName  
        $pkg = Get-InstalledPSResource "myTestModule2"
        $pkg.Name | Should -Be "myTestModule2" 
        $pkg.Version | Should -Be "0.0.1.0"
    }

<#
    ############ FAILING
    It "Install resource with -NoClobber flag (should not clobber)" {
        Install-PSResource -Name "myTestModule" -Repository $TestGalleryName  
        $pkg = Get-InstalledPSResource "myTestModule"
        $pkg.Name | Should -Be "myTestModule" 
        $pkg.Version | Should -Be "0.0.3.0"

        Install-PSResource -Name "myTestModule2" -Repository $TestGalleryName -NoClobber #-WarningVariable WarningVar -warningaction SilentlyContinue
        $WarningVar | Should -Not -BeNullOrEmpty
        $pkg = Get-InstalledPSResource "myTestModule2"
        $pkg.Name | Should -Be "myTestModule2" 
        $pkg.Version | Should -Be "0.0.1.0"
    }
#>

    ### This needs to be manually tested due to prompt
<#
    It "Install resource that requires accept license without -AcceptLicense flag" {
        Install-PSResource -Name "testModuleWithlicense" -Repository $TestGalleryName
        $pkg = Get-InstalledPSResource "testModuleWithlicense"
        $pkg.Name | Should -Be "testModuleWithlicense" 
        $pkg.Version | Should -Be "0.0.1.0"
    }
#>

    ### PoshTestGallery psgettestlocal NuGetGallery PSGallery psgettestlocal2
    ### 10 40 50 50 50
    ### True False True False False

    <#
    ### This needs to be manually tested due to prompt
    It "Install resource should prompt 'trust repository' if repository is not trusted" {
        Set-PSResourceRepository PoshTestGallery -Trusted:$false

        Install-PSResource -Name "TestModule" -Repository $TestGalleryName -confirm:$false
        
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 

        Set-PSResourceRepository PoshTestGallery -Trusted
    }
#>
<#
    It "Install resource from local repository given Repository parameter" {
        $publishModuleName = "TestFindModule"
        $repoName = "psgettestlocal"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $publishModuleName $repoName

        Install-PSResource -Name $publishModuleName -Repository $repoName
        $pkg = Get-Module $publishModuleName -ListAvailable
        $pkg | Should -Not -BeNullOrEmpty
        $pkg.Name | Should -Be $publishModuleName
        #$pkg.Repository | Should -Be $repoName
    }




    It "Install resource given repository parameter, where resource exists in multiple local repos" {
        $moduleName = "test_local_mod"
        $repoHigherPriorityRanking = "psgettestlocal"
        $repoLowerPriorityRanking = "psgettestlocal2"

        Get-ModuleResourcePublishedToLocalRepoTestDrive $moduleName $repoHigherPriorityRanking
        Get-ModuleResourcePublishedToLocalRepoTestDrive $moduleName $repoLowerPriorityRanking

        $res = Find-PSResource -Name $moduleName
        $res.Repository | Should -Be $repoHigherPriorityRanking

        $resNonDefault = Find-PSResource -Name $moduleName -Repository $repoLowerPriorityRanking
        $resNonDefault.Repository | Should -Be $repoLowerPriorityRanking
    }
    #>
}
