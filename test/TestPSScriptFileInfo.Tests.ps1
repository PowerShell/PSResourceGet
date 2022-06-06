# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test New-PSScriptFileInfo" {
    BeforeAll {
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path $TestDrive -ChildPath "tmpDir3"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path)
        Get-NewTestDirs($tmpDirPaths)

        # Path to folder, within our test folder, where we store invalid module and script files used for testing
        $script:testFilesFolderPath = Join-Path $psscriptroot -ChildPath "testFiles"

        # Path to specifically to that invalid test scripts folder
        $script:testScriptsFolderPath = Join-Path $testFilesFolderPath -ChildPath "testScripts"
    }

    AfterAll {
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path $TestDrive -ChildPath "tmpDir3"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path)
        Get-RemoveTestDirs($tmpDirPaths)
    }

    It "determine script file with minimal required fields as valid" {    
        $scriptFilePath = Join-Path -Path $tmpDir1Path -ChildPath "basicTestScript.ps1"
        $scriptDescription = "this is a test script"
        New-PSScriptFileInfo -FilePath $scriptFilePath -Description $scriptDescription
        Test-PSScriptFileInfo $basicScriptFilePath | Should -Be $true
    }

    It "not determine script file with Author field missing as valid" {
        $scriptName = "InvalidScriptMissingAuthor.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFileInfo $scriptFilePath | Should -Be $false
        # TODO: how to test for warnings? (psScriptMissingAuthor)
    }

    It "not determine script file with Description field missing as valid" {
        $scriptName = "InvalidScriptMissingDescription.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFileInfo $scriptFilePath | Should -Be $false
        # TODO: how to test for warnings? (psScriptMissingDescription)
    }

    It "not determine script that is missing Description block altogether as valid" {
        $scriptName = "InvalidScriptMissingDescriptionCommentBlock.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFileInfo $scriptFilePath | Should -Be $false
        # TODO: how to test for warnings? (PSScriptMissingHelpContentCommentBlock)
    }

    It "not determine script file Guid as valid" {
        $scriptName = "InvalidScriptMissingGuid.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFileInfo $scriptFilePath | Should -Be $false
        # TODO: how to test for warnings? (psScriptMissingGuid)
    }

    It "not determine script file missing Version as valid" {
        $scriptName = "InvalidScriptMissingVersion.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        Test-PSScriptFileInfo $scriptFilePath | Should -Be $false
        # TODO: how to test for warnings? (psScriptMissingVersion)
    }
}
