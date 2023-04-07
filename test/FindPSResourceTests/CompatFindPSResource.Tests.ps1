# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$((Get-Item $psscriptroot).parent)\PSGetTestUtils.psm1" -Force

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"

Describe 'Test CompatPowerShellGet: Find-PSResource' -tags 'CI' {

    BeforeAll{
        $PSGalleryName = Get-PSGalleryName
        $testModuleName = "test_module"
        $testScriptName = "test_script"
        $testModuleName2 = "testmodule99"
        $commandName = "Get-TargetResource"
        $dscResourceName = "SystemLocale"
        $parentModuleName = "SystemLocaleDsc"
        Get-NewPSResourceRepositoryFile
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

        It "Find-Module without any parameter values" {
            $psgetItemInfo = Find-Module -Repository PSGallery
            $psgetItemInfo.Count -ge 1 | Should -Be $true 
        }

        It "Find a specific module" {
            $res = Find-Module "testModule99" -Repository PSGallery
            $res | Should -Not -BeNullOrEmpty 
            $res.Name | Should -Be "testModule99"
        }
    
        It "Find-Module with range wildcards" {
            $res = Find-Module -Name "TestModule9*" -Repository PSGallery
            $res | Should -Not -BeNullOrEmpty 
            $res.Name | Should -Be "TestModule99"
        }

        It "Find not available module with wildcards" {
            $res = Find-Module -Name "TestModule5test*" -Repository PSGallery
            $res | Should -BeNullOrEmpty
        }

        It "Find-Module with min version" {
            $res = Find-Module TestModule99 -MinimumVersion 0.0.3 -Repository PSGallery
            $res.Name | Should -Be "TestModule99" 
            $res.Version -ge [Version]"0.0.3" | Should -Be $true
        }

        It "Find-Module with min version not available" {
            Find-Module TestModule99 -MinimumVersion 10.0 -Repository PSGallery
            $res | Should -BeNullOrEmpty
        }

        It "Find-Module with required version not available" {
            $res = Find-Module TestModule99 -RequiredVersion 12.0 -Repository PSGallery -ErrorVariable ev -ErrorAction SilentlyContinue
            $res | Should -BeNullOrEmpty
        }

        It "Find-Module with required version" {
            $res = Find-Module TestModule99 -RequiredVersion 0.0.2 -Repository PSGallery
            $res | Should -Not -BeNullOrEmpty
            $res.Name | Should -Be "TestModule99"
            $res.Version -eq [Version]"0.0.2" | Should -Be $true
        }

        It "Find-Module with multiple module names and required version" {
            $res = Find-Module TestModuleWithDependencyB, TestModuleWithDependencyC -RequiredVersion 3.0 -Repository PSGallery
            $res.Count -eq 2 | Should -Be $true 
            $res[0].Version -eq [Version]"3.0" | Should -Be $true
            $res[1].Version -eq [Version]"3.0" | Should -Be $true
        }

        ### TODO:  Broken -- minimum version is broken
<#
        It "Find-Module with multiple module names and minimum version" {
            $res = Find-Module TestModule99, TestModuleWithDependencyB -MinimumVersion 0.5 -Repository PSGallery
            $res.Count -eq 2 | Should -Be $true 
            $res[0].Version -gt [Version]"0.5" | Should -Be $true
            $res[1].Version -gt [Version]"0.5" | Should -Be $true
        }

        
        ### TODO:  broken
        It "Find-Module with wildcard name and minimum version" {
            $res = Find-Module TestModule9* -MinimumVersion 0.0.3 -Repository PSGallery -ErrorAction SilentlyContinue -ErrorVariable err
            $err | Should -Not -BeNullOrEmpty
            $res | Should -BeNullOrEmpty
        }
#>
        It "Find-Module with wildcard name and required version" {
            $res = Find-Module TestModule9* -RequiredVersion 0.0.1 -Repository PSGallery -ErrorAction SilentlyContinue -ErrorVariable err
            $err | Should -Not -BeNullOrEmpty
            $res | Should -BeNullOrEmpty
        }

        It "Find-Module with multinames" {
            $res = Find-Module TestModuleWithDependencyB, TestModuleWithDependencyC, TestModuleWithDependencyD -Repository PSGallery
            $res.Count -eq 3 | Should -Be $true
        }
        
        It "Find-Module with all versions" {
            $res = Find-Module TestModule99 -Repository PSGallery -AllVersions
            $res.Count -gt 1 | Should -Be $true
        }

        ### Broken
<#
        It "Find-DscResource with single resource name" {
            $psgetDscResourceInfo = Find-DscResource -Name NetworkingDsc
            $psgetDscResourceInfo.Name | Should -Be "NetworkingDsc" 
        }
 
        ### Broken 
        It "Find-DscResource with two resource names" {
            $psgetDscResourceInfos = Find-DscResource -Name DscTestResource, NewDscTestResource
    
            Assert ($psgetDscResourceInfos.Count -ge 2) "Find-DscResource did not return the expected DscResources, $psgetDscResourceInfos"
    
            Assert ($psgetDscResourceInfos.Name -contains "DscTestResource") "DscTestResource is not returned by Find-DscResource, $psgetDscResourceInfos"
            Assert ($psgetDscResourceInfos.Name -contains "NewDscTestResource") "NewDscTestResource is not returned by Find-DscResource, $psgetDscResourceInfos"
        }
     
        It "Find-Command with single command name" {
            $psgetCommandInfo = Find-Command -Name Get-ContosoServer
            AssertEquals $psgetCommandInfo.Name 'Get-ContosoServer' "Get-ContosoServer is not returned by Find-Command, $psgetCommandInfo"
        }

        It "FindCommandWithTwoResourceNames {
            $psgetCommandInfos = Find-Command -Name Get-ContosoServer, Get-ContosoClient
    
            Assert ($psgetCommandInfos.Count -ge 2) "Find-Command did not return the expected command names, $psgetCommandInfos"
    
            Assert ($psgetCommandInfos.Name -contains 'Get-ContosoServer') "Get-ContosoServer is not returned by Find-Command, $psgetCommandInfos"
            Assert ($psgetCommandInfos.Name -contains 'Get-ContosoClient') "Get-ContosoClient is not returned by Find-Command, $psgetCommandInfos"
        }
#>       
        It "Find-Module with IncludeDependencies" {
            $ModuleName = "TestModuleWithDependencyE"
    
            $res = Find-Module -Name $ModuleName -IncludeDependencies
            $res.Count -ge 2 | Should -Be $true
        }

    It "find module given specific Name, Version null" {
        $res = Find-Module -Name $testModuleName2 -Repository $PSGalleryName
        $res.Name | Should -Be $testModuleName2
        $res.Version | Should -Be "0.0.93"
    }

    It "should not find module given nonexistant Name" {
        $res = Find-Module -Name NonExistantModule -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameResponseConversionFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
        $res | Should -BeNullOrEmpty
    }

    It "find script(s) given wildcard Name" {
        $foundScript = $False
        $res = Find-Script -Name "test_scri*" -Repository $PSGalleryName
        $res.Count | Should -Be 1
        foreach ($item in $res)
        {
            if ($item.Type -eq "Script")
            {
                $foundScript = $true
            }
        }

        $foundScript | Should -BeTrue
    }

    It "find all versions of module when given specific Name, Version not null --> '*'" {
        $res = Find-Module -Name $testModuleName2 -AllVersions -Repository $PSGalleryName
        $res | ForEach-Object {
            $_.Name | Should -Be $testModuleName2
        }

        $res.Count | Should -BeGreaterThan 1
    }

    It "find module with latest (including prerelease) version given Prerelease parameter" {
        # test_module resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Find-Module -Name $testModuleName2 -Repository $PSGalleryName
        $res.Version | Should -Be "0.0.93"

        $resPrerelease = Find-Module -Name $testModuleName2 -AllowPrerelease -Repository $PSGalleryName
        $resPrerelease.Version | Should -Be "1.0.0"
        $resPrerelease.Prerelease | Should -Be "beta2"
    }

    It "find script from PSGallery" {
        $resScript = Find-Script -Name $testScriptName -Repository $PSGalleryName
        $resScript.Name | Should -Be $testScriptName
        $resScriptType = Out-String -InputObject $resScript.Type
        $resScriptType.Replace(",", " ").Split() | Should -Contain "Script"
    }
    
    It "find module from PSGallery" {
        $resModule = Find-Module -Name $testModuleName -Repository $PSGalleryName
        $resModule.Name | Should -Be $testModuleName
        $resModuleType = Out-String -InputObject $resModule.Type
        $resModuleType.Replace(",", " ").Split() | Should -Contain "Module"
    }

<#
    ### BROKEN
    It "find resource given CommandName" {
        $res = Find-Command $commandName -Repository $PSGalleryName
        foreach ($item in $res) {
            $item.Names | Should -Be $commandName    
            $item.ParentResource.Includes.Command | Should -Contain $commandName
        }
    }

    ### BROKEN
    It "find resource given DscResourceName" {
        $res = Find-DscResource $dscResourceName -Repository $PSGalleryName
        foreach ($item in $res) {
            $item.Names | Should -Be $dscResourceName    
            $item.ParentResource.Includes.DscResource | Should -Contain $dscResourceName
        }
    }
#>
}
    