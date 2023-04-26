# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test Test-PSScriptFile" -Tags 'CI' {
    BeforeAll {
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDirPaths = @($tmpDir1Path)
        Get-NewTestDirs($tmpDirPaths)

        # Path to folder, within our test folder, where we store invalid module and script files used for testing
        $script:testFilesFolderPath = Join-Path $psscriptroot -ChildPath "testFiles"

        # Path to specifically to that invalid test scripts folder
        $script:testScriptsFolderPath = Join-Path $testFilesFolderPath -ChildPath "testScripts"
    }

    It "determine script file with minimal required fields as valid" {
        $scriptFilePath = Join-Path -Path $tmpDir1Path -ChildPath "testscript.ps1"
        $scriptDescription = "this is a test script"
        New-PSScriptFile -Path $scriptFilePath -Description $scriptDescription
        Test-PSScriptFile $scriptFilePath | Should -Be $true
    }

    It "not determine script file with Author field missing as valid" {
        $scriptName = "InvalidScriptMissingAuthor.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFile $scriptFilePath | Should -Be $false
    }

    It "not determine script file with Description field missing as valid" {
        $scriptName = "InvalidScriptMissingDescription.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFile $scriptFilePath | Should -Be $false
    }

    It "not determine script that is missing Description block altogether as valid" {
        $scriptName = "InvalidScriptMissingDescriptionCommentBlock.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFile $scriptFilePath | Should -Be $false
    }

    It "not determine script file Guid as valid" {
        $scriptName = "InvalidScriptMissingGuid.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFile $scriptFilePath | Should -Be $false
    }

    It "not determine script file missing Version as valid" {
        $scriptName = "InvalidScriptMissingVersion.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFile $scriptFilePath | Should -Be $false
    }

    It "determine script without empty lines in PSScriptInfo comment content is valid" {
        $scriptName = "ScriptWithoutEmptyLinesInMetadata.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFile $scriptFilePath | Should -Be $true
    }

    It "determine script without empty lines between comment blocks is valid" {
        $scriptName = "ScriptWithoutEmptyLinesBetweenCommentBlocks.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFile $scriptFilePath | Should -Be $true
    }
}
