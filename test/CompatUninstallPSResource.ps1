# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Uninstall-PSResource for Modules' -tags 'CI' {

    BeforeAll{
        $PSGalleryName = Get-PSGalleryName
        $testModuleName = "testmodule99"
        $testScriptName = "test_script"
        Get-NewPSResourceRepositoryFile
        Set-PSResourceRepository PSGallery -Trusted
        Uninstall-PSResource -Name $testModuleName -Version "*" -ErrorAction SilentlyContinue
        Uninstall-PSResource -Name $testScriptName -Version "*" -ErrorAction SilentlyContinue
    }

    BeforeEach {
        Install-PSResource $testModuleName -Repository $PSGalleryName -WarningAction SilentlyContinue
    }

    AfterEach {
        Uninstall-PSResource -Name $testModuleName -Version "*" -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }
<#
    It "Uninstall-Module with -WhatIf" {
        $guid = [system.guid]::newguid().tostring()
        $contentFile = Join-Path -Path $TestDrive -ChildPath $guid -AdditionalChildPath "file.txt"
        New-Item $contentFile

        Start-Transcript $contentFile
        Install-Module -Name testmodule99 -WhatIf
        Stop-Transcript

        $content = Get-Content $contentFile
        $content | Should -Contain "What if"
        $res = Get-PSResourcde "testmodule99"
        $res.Count -eq 0 | Should -Be $true
    } 
#>

    It "Uninstall-Module" {
        Uninstall-Module -Name $testModuleName

        $res = Get-PSResource $testModuleName
        $res.Count | Should -Be 0
    }

    ### broken
<#    It "Uninstall-Module with -AllVersions" {
        Install-PSResource $testModuleName -Repository $PSGalleryName -Version "0.0.1"
        Uninstall-Module -Name $testModuleName -AllVersions

        $res = Get-PSResource $testModuleName
        $res.Count | Should -Be 0
    }

    ### broken
    It "Uninstall-Module with -MinimumVersion" {
        $minVersion = "0.0.2"
        Install-PSResource $testModuleName -Repository $PSGalleryName -Version "0.0.1"
        Uninstall-Module -Name $testModuleName -MinimumVersion $minVersion

        $res = Get-PSResource $testModuleName
        $res.Version -lt $minVersion | Should -Be $true
    }

    ### broken
    It "Uninstall-Module with -MaximumVersion" {
        $maxVersion = "0.0.2"
        Install-PSResource $testModuleName -Repository $PSGalleryName -Version "0.0.1"
        Uninstall-Module -Name $testModuleName -MaximumVersion $maxVersion

        $res = Get-PSResource $testModuleName
        $res.Version | Should -Not -Contain "0.0.1"
    }
#>

}
