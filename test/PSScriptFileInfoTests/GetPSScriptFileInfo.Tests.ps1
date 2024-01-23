# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose
# Explicitly import build module because in CI PowerShell can autoload PSGetv2
# This ensures the build module is always being tested
$buildModule = "$psscriptroot/../../out/Microsoft.PowerShell.PSResourceGet"
Import-Module $buildModule -Force -Verbose

$testDir = (get-item $psscriptroot).parent.FullName

Describe "Test Get-PSScriptFileInfo" -tags 'CI' {
    BeforeAll {
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDirPaths = @($tmpDir1Path)
        Get-NewTestDirs($tmpDirPaths)

        # Path to folder, within our test folder, where we store invalid module and script files used for testing
        $script:testFilesFolderPath = Join-Path $testDir -ChildPath "testFiles"

        # Path to specifically to that invalid test scripts folder
        $script:testScriptsFolderPath = Join-Path $testFilesFolderPath -ChildPath "testScripts"
    }

    It "should get script file object given script with minimal required fields" {
        $scriptFilePath = Join-Path -Path $tmpDir1Path -ChildPath "testscript.ps1"
        $scriptDescription = "this is a test script"
        New-PSScriptFileInfo -Path $scriptFilePath -Description $scriptDescription

        $res = Get-PSScriptFileInfo $scriptFilePath
        $res.Name | Should -Be "testscript"
        $res.ScriptHelpComment.Description | Should -Be $scriptDescription
    }

    It "should not get script file object given an invalid path" {
        $scriptFilePath = Join-Path -Path $tmpDir1Path -ChildPath "testscript.psd1"

        { Get-PSScriptFileInfo $scriptFilePath -ErrorAction SilentlyContinue } | Should -Throw -ErrorId "InvalidPath,Microsoft.PowerShell.PSResourceGet.Cmdlets.GetPSScriptFileInfo"
    }

    It "should not get script file object given a nonexistent path" {
        $scriptFilePath = Join-Path -Path $tmpDir1Path -ChildPath "testscript1.ps1"

        { Get-PSScriptFileInfo $scriptFilePath -ErrorAction SilentlyContinue } | Should -Throw -ErrorId "PathNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.GetPSScriptFileInfo"
    }

    It "should not get script file object given script with Author and Version field missing" {
        $scriptName = "InvalidScriptMissingAuthorAndVersion.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        { Get-PSScriptFileInfo $scriptFilePath -ErrorAction SilentlyContinue } | Should -Throw -ErrorId "InvalidPSScriptFile,Microsoft.PowerShell.PSResourceGet.Cmdlets.GetPSScriptFileInfo"
    }

    It "should not get script file object given script with Description field missing" {
        $scriptName = "InvalidScriptMissingDescription.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        { Get-PSScriptFileInfo $scriptFilePath -ErrorAction SilentlyContinue } | Should -Throw -ErrorId "InvalidPSScriptFile,Microsoft.PowerShell.PSResourceGet.Cmdlets.GetPSScriptFileInfo"
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

    It "should get script file object given script that has commas in Tags, ExternalModuleDependencies, RequiredScripts, and ExternalScriptDependencies fields" {
        # Note: New-PSScriptFileInfo will NOT create script that has commas in these fields, but per user requests we want to account for scripts that may contain it
        $scriptName = "ScriptWithCommaInTagsInSomeFields.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        $res = Get-PSScriptFileInfo $scriptFilePath
        $foundTags = $res.ScriptMetadataComment.Tags
        $foundExternalModuleDependencies = $res.ScriptMetadataComment.ExternalModuleDependencies
        $foundRequiredScripts = $res.ScriptMetadataComment.RequiredScripts
        $foundExternalScriptDependencies = $res.ScriptMetadataComment.ExternalScriptDependencies

        $foundTags | Should -Be @("tag1", "tag2")
        foreach($tag in $foundTags) {
            $tag | Should -Not -Contain ","
        }

        $foundExternalModuleDependencies | Should -Be @("Storage", "ActiveDirectory")
        foreach($modDep in $foundExternalModuleDependencies) {
            $modDep | Should -Not -Contain ","
        }

        $foundRequiredScripts | Should -Be @("Script1", "Script2")
        foreach($reqScript in $foundRequiredScripts) {
            $modDep | Should -Not -Contain ","
        }

        $foundExternalScriptDependencies | Should -Be @("ExtScript1", "ExtScript2")
        foreach($scriptDep in $foundExternalScriptDependencies) {
            $modDep | Should -Not -Contain ","
        }
    }
}
