# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose
# Explicitly import build module because in CI PowerShell can autoload PSGetv2
# This ensures the build module is always being tested
$buildModule = "$psscriptroot/../../out/PowerShellGet"
Import-Module $buildModule -Force -Verbose

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
        $psgetItemInfo.Count | Should -BeGreaterOrEqual 1
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
        $res.Name | Should -Contain "TestModule99" 
        $res | ForEach-Object { $_.Version | Should -BeGreaterOrEqual ([System.Version]"0.0.3") }
    }

    It "Find-Module with min version not available" {
        $res = Find-Module TestModule99 -MinimumVersion 10.0 -Repository PSGallery
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
        $res.Version | Should -Be ([System.Version]"0.0.2")
    }

    It "Find-Module with multiple module names and required version" {
        $res = Find-Module TestModuleWithDependencyB, TestModuleWithDependencyC -RequiredVersion 3.0 -Repository PSGallery
        $res | Should -HaveCount 2
        $res | ForEach-Object { $_.Version | Should -Be ([System.Version]"3.0") }
    }

    It "Find-Module with module name and minimum version" {
        $res = Find-Module TestModule99 -MinimumVersion 0.5 -Repository PSGallery
        $res | Should -HaveCount 0
    }

    It "Find-Module with multiple module names and minimum version" {
        $res = Find-Module TestModule99, TestModuleWithDependencyB -MinimumVersion 0.5 -Repository PSGallery
        $res | Should -HaveCount 2
        $res.Name | Should -Not -Contain "TestModule99"
        $res | ForEach-Object { $_.Version | Should -BeGreaterOrEqual ([System.Version]"0.5") }
    }
    
    It "Find-Module with wildcard name and minimum version" {
        $res = Find-Module TestModule9* -MinimumVersion 0.0.3 -Repository PSGallery -ErrorAction SilentlyContinue -ErrorVariable err
        $err | Should -Not -BeNullOrEmpty
        $res | Should -BeNullOrEmpty
    }

    It "Find-Module with multinames" {
        $res = Find-Module TestModuleWithDependencyB, TestModuleWithDependencyC, TestModuleWithDependencyD -Repository PSGallery
        $res | Should -HaveCount 3
    }
    
    It "Find-Module with all versions" {
        $res = Find-Module TestModule99 -Repository PSGallery -AllVersions
        $res.Count | Should -BeGreaterThan 1
    }

    It "Find-DscResource with single resource name" {
        $res = Find-DscResource -Name SystemLocale -Repository $PSGalleryName
        Foreach ($dscresource in $res) {
            $dscresource.Names | Should -Be "SystemLocale"
            $dscresource.ParentResource | Should -Be ("ComputerManagementDsc" -or "SystemLocaleDsc")
        }   
    }

    It "Find-DscResource with two resource names" {
        $res = Find-DscResource -Name "SystemLocale", "MSFT_PackageManagement" -Repository $PSGalleryName
        Foreach ($dscresource in $res) {
            $dscresource.Names | Should -Be ("SystemLocale" -or "MSFT_PackageManagement")
            $dscresource.ParentResource | Should -Be ("ComputerManagementDsc" -or "SystemLocaleDsc" -or "PackageManagement")
        }
    }
  
    It "Find-Command with single command name" {
        $res = Find-Command -Name $commandName -Repository $PSGalleryName
        $res | ForEach-Object { $_.Names | Should -Be $commandName }
    }
   
    It "Find-Module with IncludeDependencies" {
        $ModuleName = "TestModuleWithDependencyE"
    
        $res = Find-Module -Name $ModuleName -IncludeDependencies
        $res.Count | Should -BeGreaterOrEqual 2
    }

    It "find module given specific Name, Version null" {
        $res = Find-Module -Name $testModuleName2 -Repository $PSGalleryName
        $res.Name | Should -Be $testModuleName2
        $res.Version | Should -Be "0.0.93"
    }

    It "should not find module given nonexistant Name" {
        $res = Find-Module -Name NonExistantModule -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameResponseConversionFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
        $res | Should -BeNullOrEmpty
    }

    It "find script(s) given wildcard Name" {
        $foundScript = $False
        $res = Find-Script -Name "test_scri*" -Repository $PSGalleryName
        $res | Should -HaveCount 1
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
}
