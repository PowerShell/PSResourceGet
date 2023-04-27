# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$PSScriptRoot\PSGetTestUtils.psm1" -Force

Describe "Test Get-PSScriptFileInfo" -Tags 'CI' {
    BeforeAll {
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDirPaths = @($tmpDir1Path)
        Get-NewTestDirs($tmpDirPaths)

        # Path to folder, within our test folder, where we store invalid module and script files used for testing
        $script:testFilesFolderPath = Join-Path $PSScriptRoot -ChildPath "testFiles"

        # Path to specifically to that invalid test scripts folder
        $script:testScriptsFolderPath = Join-Path $testFilesFolderPath -ChildPath "testScripts"
    }

    It "should get script file object given script with minimal required fields" {
        $scriptFilePath = Join-Path -Path $tmpDir1Path -ChildPath "testscript.ps1"
        $scriptDescription = "this is a test script"
        New-PSScriptFile -Path $scriptFilePath -Description $scriptDescription

        $res = Get-PSScriptFileInfo $scriptFilePath
        $res.Name | Should -Be "testscript"
        $res.ScriptHelpComment.Description | Should -Be $scriptDescription
    }

    It "should not get script file object given an invalid path" {
        $scriptFilePath = Join-Path -Path $tmpDir1Path -ChildPath "testscript.psd1"

        { Get-PSScriptFileInfo $scriptFilePath -ErrorAction SilentlyContinue } | Should -Throw -ErrorId "InvalidPath,Microsoft.PowerShell.PowerShellGet.Cmdlets.GetPSScriptFileInfo"
    }

    It "should not get script file object given a nonexistent path" {
        $scriptFilePath = Join-Path -Path $tmpDir1Path -ChildPath "testscript1.ps1"

        { Get-PSScriptFileInfo $scriptFilePath -ErrorAction SilentlyContinue } | Should -Throw -ErrorId "PathNotFound,Microsoft.PowerShell.PowerShellGet.Cmdlets.GetPSScriptFileInfo"
    }

    It "should not get script file object given script with Author and Version field missing" {
        $scriptName = "InvalidScriptMissingAuthorAndVersion.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        { Get-PSScriptFileInfo $scriptFilePath -ErrorAction SilentlyContinue } | Should -Throw -ErrorId "InvalidPSScriptFile,Microsoft.PowerShell.PowerShellGet.Cmdlets.GetPSScriptFileInfo"
    }

    It "should not get script file object given script with Description field missing" {
        $scriptName = "InvalidScriptMissingDescription.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        { Get-PSScriptFileInfo $scriptFilePath -ErrorAction SilentlyContinue } | Should -Throw -ErrorId "InvalidPSScriptFile,Microsoft.PowerShell.PowerShellGet.Cmdlets.GetPSScriptFileInfo"
    }

    It "should get script file object given script without empty lines in PSScriptInfo comment content" {
        $scriptName = "ScriptWithoutEmptyLinesInMetadata.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        $res = Get-PSScriptFileInfo $scriptFilePath
        $res.Name | Should -Be "ScriptWithoutEmptyLinesInMetadata"
    }

    It "should get script file object given script without empty lines between comment blocks" {
        $scriptName = "ScriptWithoutEmptyLinesBetweenCommentBlocks.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        $res = Get-PSScriptFileInfo $scriptFilePath
        $res.Name | Should -Be "ScriptWithoutEmptyLinesBetweenCommentBlocks"
    }

    It "should get script file object given script with invalid ProjectUri" {
        $scriptName = "ScriptWithInvalidProjectUri.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        $res = Get-PSScriptFileInfo $scriptFilePath
        $res.Name | Should -Be "ScriptWithInvalidProjectUri"
        $res.ScriptMetadataComment.ProjectUri | Should -BeNullOrEmpty
    }
}
