# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
Import-Module "$((Get-Item $psscriptroot).parent)\PSGetTestUtils.psm1" -Force

Describe 'Test HTTP Update-PSResource for V2 Server Protocol' -tags 'CI' {

    BeforeAll {
        $PSGalleryName = Get-PSGalleryName
        $NuGetGalleryName = Get-NuGetGalleryName
        $testModuleName = "test_module"
        $testModuleName2 = "TestModule99"
        $PackageManagement = "PackageManagement"
        Get-NewPSResourceRepositoryFile
        Get-PSResourceRepository
        Set-PSResourceRepository -Name PSGallery -Trusted
    }

    AfterEach {
       Uninstall-PSResource "test_module", "TestModule99", "TestModuleWithLicense", "TestModuleWithDependencyB", "TestModuleWithDependencyD" -Version "*" -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    ### working but need to update test
#   It "Update-Module with -WhatIf" {
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

    It "Update-Module with -Force" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.91 -Repository PSGallery
        Update-Module $testModuleName2 -Force

        $res = Get-PSResourcde $testModuleName2
        $res.version | Should -Contain "0.0.91" 
    }

    It "Update-Module with -RequiredVersion" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.91 -Repository PSGallery
        Update-Module $testModuleName2 -RequiredVersion 0.0.93 -WarningVariable wv
        $wv.Count | Should -Be 1
    }

    It "Update-Module multiple modules" {
        Install-Module "TestModuleWithDependencyB" -Repository PSGallery -RequiredVersion "2.0"
        Install-Module "TestModuleWithDependencyD" -Repository PSGallery -RequiredVersion "1.0"

        Update-Module "TestModuleWithDependencyB", "TestModuleWithDependencyD"
        $res = Get-PSResource "TestModuleWithDependencyB", "TestModuleWithDependencyD" 
        $res.count -ge 4 | Should -Be $true
        $res.version | Should -Contain "2.0"
        $res.version | Should -Contain "3.0"
    }

    It "Update-Module with RequiredVersion and wildcard" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.91 -Repository PSGallery
        Update-Module "testModule9*"
        $res = Get-PSResource $testModuleName2
        $res.count -ge 2 | Should -Be $true
        $res.version | Should -Contain "3.0"
    }

    It "Update-Module with -PassThru should return output" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.91 -Repository PSGallery
        $res = Update-Module $testModuleName2 -PassThru
        $res | Should -Not -BeNullOrEmpty
    }

    It "Update-Module with lower -RequiredVersion should not update" {
        Install-Module $testModuleName2 -Repository PSGallery
        Update-Module $testModuleName2 -RequiredVersion 0.0.1
        $res = Get-PSResource $testModuleName2
        $res.Count -eq 1 | Should -Be $true
        $res.version | Should -Be "0.0.93"
    }

    It "UpdateAllModules" {
        Install-Module $testModuleName2 -Repository PSGallery -RequiredVersion 0.0.7
        Install-Module "TestModuleWithDependencyB" -Repository PSGallery -RequiredVersion 2.0
        Install-Module "TestModuleWithDependencyD" -Repository PSGallery -RequiredVersion 1.0

        Update-Module $testModuleName2, "TestModuleWithDependencyB", "TestModuleWithDependencyD"
        $res = Get-PSResource $testModuleName2, "TestModuleWithDependencyB", "TestModuleWithDependencyD" 
        $res.Count -ge 3 | Should -Be $true
        $res.version | Should -Be "0.0.93"
    }

    It "Update-Module with not available -RequiredVersion" {
        Install-Module $testModuleName2 -RequiredVersion 3.0 -Repository PSGallery

        Update-Module $testModuleName2 -RequiredVersion 10.0

        $res = Get-PSResource $testModuleName2
        $res.Count -ge 0 | Should -Be $true
        $res.Version | Should -Not -Contain "10.0"
    }

    ### Broken?
    It "Update-Module with Dependencies" {
        $parentModule = "TestModuleWithDependencyC"
        $childModule1 = "TestModuleWithDependencyB"
        $childModule2 = "TestModuleWithDependencyD"
        $childModule3 = "TestModuleWithDependencyF"
        Install-Module $parentModule -Repository PSGallery -RequiredVersion "3.0"
        Update-Module $parentModule

        $res = Get-PSResource $parentModule, $childModule1, $childModule2, $childModule3
        $res.Count -ge 4 | Should -Be $true
        $res.Version | Should -Contain "5.0"
    }
}
