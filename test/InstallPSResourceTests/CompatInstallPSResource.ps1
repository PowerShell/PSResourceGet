# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Write-Verbose -Verbose -Message "PSGetTestUtils path: $modPath"
Import-Module $modPath -Force -Verbose

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"

Describe 'Test Install-PSResource for V2 Server scenarios' -tags 'CI' {

    BeforeAll {
        $PSGalleryName = Get-PSGalleryName
        $PSGalleryUri = Get-PSGalleryLocation
        $NuGetGalleryName = Get-NuGetGalleryName
        $testModuleName = "test_module"
        $testModuleName2 = "TestModule99"
        $RequiredResourceJSONFileName = "TestRequiredResourceFile.json"
        $RequiredResourcePSD1FileName = "TestRequiredResourceFile.psd1"
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos

        Set-PSResourceRepository -Name PSGallery -Trusted
    }

    AfterEach {
        Uninstall-PSResource "test_module", "TestModule99", "TestModuleWithDependencyB", "TestModuleWithDependencyD", "newTestModule" -SkipDependencyCheck -ErrorAction SilentlyContinue -Version "*"
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Install-Module testmodule99 should return" {
        Install-Module -Name $testModuleName2 -Repository PSGallery

        $res = Get-PSResource -Name $testModuleName2
        $res.Name | Should be $testModuleName2
    }

    It "Install-Module testmodule99 -PassThru should return output" {
        Install-Module -Name $testModuleName2 -Repository PSGallery -PassThru

        $res = Get-PSResource -Name $testModuleName2
        $res.Name | Should be $testModuleName2
    }

    It "Install-Module should not install with wildcard" {
        Install-Module -Name "testmodule9*" -Repository PSGallery -ErrorAction SilentlyContinue

        $res = Get-PSResource -Name $testModuleName2
        $res.Count -eq 0 | Should -Be $true
    }

    ### Broken
#    It "Install-Module with version params" {
#        Install-Module $testModuleName2 -MinimumVersion 0.0.1 -MaximumVersion 0.0.9 -Repository PSGallery
        
#        $res = Get-PSResource -Name $testModuleName2
#        $res.Name | Should be $testModuleName2
#        $res.Version | Should be "0.0.9"
#    }

    It "Install-Module multiple names" {
        Install-Module "TestModuleWithDependencyB", "testmodule99" -Repository PSGallery

        $res = Get-PSResource "TestModuleWithDependencyB", "testmodule99"
        $res.Count -ge 2 | Should -Be $true
    }

    It "Install-Module multiple names with RequiredVersion" {
        Install-Module "newTestModule", "testmodule99" -RequiredVersion 0.0.1 -Repository PSGallery

        $res = Get-PSResource "newTestModule", "testmodule99"
        $res.Count -eq 2 | Should -Be $true
    }

    It "Install-Multiple names with MinimumVersion" {
        Install-Module "TestModuleWithDependencyB", "TestModuleWithDependencyD" -MinimumVersion 1.0 -Repository PSGallery

        $res = Get-PSResource "TestModuleWithDependencyB", "TestModuleWithDependencyD"
        $res.Count -eq 2 | Should -Be $true
    }

    It "Install-Module with MinimumVersion" {
        Install-Module $testModuleName2 -MinimumVersion 0.0.3 -Repository PSGallery
        
        $res = Get-PSResource $testModuleName2
        $res.Count -eq 1 | Should -Be $true    
    }

    It "Install-Module with RequiredVersion" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.3 -Repository PSGallery

        $res = Get-PSResource $testModuleName2
        $res.Count -eq 1 | Should -Be $true 
    }

    It "Install-Module should fail if RequiredVersion is already installed" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.93 -Repository PSGallery

        Install-Module $testModuleName2 -RequiredVersion 0.0.93 -WarningVariable wv -Repository PSGallery
        $wv[1] | Should -Be "Resource 'testmodule99' with version '0.0.93' is already installed.  If you would like to reinstall, please run the cmdlet again with the -Reinstall parameter"
       }

    It "Install-Module should fail if MinimumVersion is already installed" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.93 -Repository PSGallery

        Install-Module $testModuleName2 -MinimumVersion 2.0 -WarningVariable wv -Repository PSGallery
        $wv[1] | Should -Be "Resource 'testmodule99' with version '0.0.93' is already installed.  If you would like to reinstall, please run the cmdlet again with the -Reinstall parameter"
    }

    It "Install-Module with -Force" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.91 -Repository PSGallery
        Install-Module $testModuleName2 -RequiredVersion 0.0.93 -Force -Repository PSGallery -WarningVariable wv
        $wv.Count | Should -Be 1
    }

    It "Install-Module same version with -Force" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.91 -Repository PSGallery
        Install-Module $testModuleName2 -RequiredVersion 0.0.93 -Force -Repository PSGallery -WarningVariable wv
        $wv.Count | Should -Be 1
    }

    It "Install-Module with nonexistent module" {
        Install-Module NonExistentModule -Repository PSGallery -ErrorVariable ev -ErrorAction SilentlyContinue

        $ev.Count | Should -Be 1
    }

    ### broken
    It "Install-Module with PipelineInput" {
        Find-Module $testModuleName2 -Repository PSGallery | Install-Module -Repository PSGallery
        $res = Get-PSResource $testModuleName2
        $res.Count -eq 1 | Should -Be $true   
    }

    ### BROKEN
    It "Install-Module multiple modules with PipelineInput" {
        Find-Module $testModuleName2, "newTestModule" -Repository PSGallery | Install-Module -Repository PSGallery
        $res = Get-PSResource $testModuleName2 , "newTestModule"
        $res.Count -eq 2 | Should -Be $true   
    }

    ### BROKEN
#    It "Install-Module multiple module using InputObjectParam" {
#        $items = Find-Module $testModuleName2 , "newTestModule"
#        Install-Module -InputObject $items
#        $res = Get-PSResource $testModuleName2 , "newTestModule"
#        $res.Count -eq 2 | Should -Be $true   
#    }

    It "InstallToCurrentUserScope" {
        Install-Module $testModuleName2 -Scope CurrentUser -Repository PSGallery

        $mod = Get-Module $testModuleName2 -ListAvailable
        $mod.ModuleBase.StartsWith($script:MyDocumentsModulesPath, [System.StringComparison]::OrdinalIgnoreCase)
    }

    ### working but need to update test
#    It "Install-Module with -WhatIf" {
#        $guid = [system.guid]::newguid().tostring()
#        $contentFile = Join-Path -Path $TestDrive -ChildPath $guid -AdditionalChildPath "file.txt"
#        New-Item $contentFile

#        Start-Transcript $contentFile
#        Install-Module -Name testmodule99 -WhatIf
#        Stop-Transcript

#        $content = Get-Content $contentFile
#        $content | Should -Contain "What if"
#        $res = Get-PSResourcde "testmodule99"
#        $res.Count -eq 0 | Should -Be $true
#    } 

    ### broken
#    It "Install-Module using Find-DscResource output" {
#        $moduleName = "SystemLocaleDsc"
#        Find-DscResource -Name "SystemLocale" -Repository PSGallery | Install-Module
#        $res = Get-Module $moduleName -ListAvailable
#        $res.Name | Should -Be $moduleName
#    }

    ### broken
#    It "Install-Module using Find-Command Output" {
#        $cmd = "Get-WUJob"
#        $module = "PSWindowsUpdate"
#        Find-Command -Name $cmd | Install-Module

#        $res = Get-Module $module -ListAvailable
#        $res.Name | Should -Be $module
#    }

    ### Broken
#    It "Install-Module with Dependencies" {
#        $parentModule = "TestModuleWithDependencyC"
#        $childModule1 = "TestModuleWithDependencyB"
#        $childModule2 = "TestModuleWithDependencyD"
#        $childModule3 = "TestModuleWithDependencyF"
#        Install-Module $parentModule

#        $res = Get-PSResource $parentModule, $childModule1, $childModule2, $childModule3
#        $res.Count -ge 4 | Should -Be $true
#    }
}