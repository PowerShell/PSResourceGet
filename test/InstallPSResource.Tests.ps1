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
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Install specific module resource by name" {
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName  
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 
        $pkg.Version | Should -Be "1.3.0"
    }

    <#
    It "Install specific script resource by name" {
        Install-PSResource -Name "TestTestScript" -Repository $TestGalleryName  
        $pkg = Get-InstalledPSResource "TestModule"
        $pkg.Name | Should -Be "TestModule" 
        $pkg.Version | Should -Be "1.3.0"
    }
#>

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
