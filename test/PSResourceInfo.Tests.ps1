# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Read PSGetModuleInfo xml file" -tags 'CI' {

    BeforeAll {
        $fileToRead = Join-Path -Path $PSScriptRoot -ChildPath "PSGetModuleInfo.xml"
    }

    It "Verifies expected error with null path" {
        { [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::ReadPSGetResourceInfo($null) } | Should -Throw -ErrorId 'PSInvalidOperationException'
    }

    It "Verifies expected error with invalid file path" {
        { [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::ReadPSGetResourceInfo('nonePath') } | Should -Throw -ErrorId 'PSInvalidOperationException'
    }

    It "Verifies PSGetModuleInfo.xml file is read successfully" {
        $psGetInfo = [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::ReadPSGetResourceInfo($fileToRead)
        CheckForExpectedPSGetInfo $psGetInfo
    }
}

Describe "Write PSGetModuleInfo xml file" -tags 'CI' {

    BeforeAll {
        $fileToRead = Join-Path -Path $PSScriptRoot -ChildPath "PSGetModuleInfo.xml"
        $fileToWrite = Join-Path -Path $TestDrive -ChildPath "PSGetModuleInfo_Write.xml"
    }

    It "Verifies expected error with null path" {
        $psGetInfo = [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::ReadPSGetResourceInfo($fileToRead)
        { [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::WritePSGetResourceInfo($null, $psGetInfo) } | Should -Throw -ErrorId 'PSInvalidOperationException'
    }

    It "Verifies file write is successful" {
        $psGetInfo = [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::ReadPSGetResourceInfo($fileToRead)
        { [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::WritePSGetResourceInfo($fileToWrite, $psGetInfo) } | Should -Not -Throw
    }

    It "Verifes written file can be read successfully" {
        $newGetInfo = [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::ReadPSGetResourceInfo($fileToWrite)
        CheckForExpectedPSGetInfo $newGetInfo
    }
}
