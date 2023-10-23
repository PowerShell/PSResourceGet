# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose
$testDir = (get-item $psscriptroot).parent.FullName

Describe "Test Test-PSScriptFileInfo" -tags 'CI' {
    BeforeAll {
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDirPaths = @($tmpDir1Path)
        Get-NewTestDirs($tmpDirPaths)

        # Path to folder, within our test folder, where we store invalid module and script files used for testing
        $script:testFilesFolderPath = Join-Path $testDir -ChildPath "testFiles"

        # Path to specifically to that invalid test scripts folder
        $script:testScriptsFolderPath = Join-Path $testFilesFolderPath -ChildPath "testScripts"
    }

    It "determine script file with minimal required fields as valid" {    
        $scriptFilePath = Join-Path -Path $tmpDir1Path -ChildPath "testscript.ps1"
        $scriptDescription = "this is a test script"
        New-PSScriptFileInfo -Path $scriptFilePath -Description $scriptDescription
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "not determine script file with Author field missing as valid" {
        $scriptName = "InvalidScriptMissingAuthor.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFileInfo $scriptFilePath | Should -Be $false
    }

    It "not determine script file with Description field missing as valid" {
        $scriptName = "InvalidScriptMissingDescription.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFileInfo $scriptFilePath | Should -Be $false
    }

    It "not determine script that is missing Description block altogether as valid" {
        $scriptName = "InvalidScriptMissingDescriptionCommentBlock.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFileInfo $scriptFilePath | Should -Be $false
    }

    It "not determine script file Guid as valid" {
        $scriptName = "InvalidScriptMissingGuid.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFileInfo $scriptFilePath | Should -Be $false
    }

    It "not determine script file missing Version as valid" {
        $scriptName = "InvalidScriptMissingVersion.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFileInfo $scriptFilePath | Should -Be $false
    }

    It "determine script without empty lines in PSScriptInfo comment content is valid" {
        $scriptName = "ScriptWithoutEmptyLinesInMetadata.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true        
    }

    It "determine script without empty lines between comment blocks is valid" {
        $scriptName = "ScriptWithoutEmptyLinesBetweenCommentBlocks.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true        
    }

    It "determine script file with varying case sensitivity for Script Metadata or Help Comment keys is valid" {
        $scriptName = "VaryingCaseSensisityKeysScript.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "determine script file with commas in Tags, ExternalModuleDependencies, RequiredScripts, ExternalScriptDependencies is valid" {
        # Note: New-PSScriptFileInfo will NOT create script that has commas in these fields, but per user requests we want to account for scripts that may contain it
        $scriptName = "ScriptWithCommaInTagsInSomeFields.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "determine script with whitespace before closing comment is valid" {
        $scriptName = "ScriptWithWhitespaceBeforeClosingComment.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }
}
