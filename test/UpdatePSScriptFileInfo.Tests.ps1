# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test Update-PSScriptFileInfo" {
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

    BeforeEach {
        $scriptFilePath = Join-Path -Path $tmpDir1Path -ChildPath "testscript.ps1"
        $scriptDescription = "this is a test script"
        New-PSScriptFileInfo -FilePath $scriptFilePath -Description $scriptDescription -PassThru
    }

    AfterEach {
        $scriptFilePath = Join-Path -Path $tmpDir1Path -ChildPath "testscript.ps1"
        if (Test-Path -Path $scriptFilePath)
        {
            Remove-Item $scriptFilePath
        }
    }

    AfterAll {
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path $TestDrive -ChildPath "tmpDir3"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path)
        Get-RemoveTestDirs($tmpDirPaths)
    }

    It "update script file Author property" {    
        Update-PSScriptFileInfo -FilePath $scriptFilePath -Author "JohnDoe"
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "update script file Version property" {
        Update-PSScriptFileInfo -FilePath $scriptFilePath -Version "2.0.0.0"
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "update script file Version property with prerelease version" {
        Update-PSScriptFileInfo -FilePath $scriptFilePath -Version "3.0.0-alpha"
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "not update script file with invalid version" {
        Update-PSScriptFileInfo -FilePath $scriptFilePath -Version "4.0.0.0.0" -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "VersionParseIntoNuGetVersion,Microsoft.PowerShell.PowerShellGet.Cmdlets.UpdatePSScriptFileInfo"
    }

    It "update script file Description property" {
        Update-PSScriptFileInfo -FilePath $scriptFilePath -Description "this is an updated test script"
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "update script file Guid property" {
        $testGuid = [Guid]::NewGuid();
        Update-PSScriptFileInfo -FilePath $scriptFilePath -Guid $testGuid
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "update script file CompanyName property" {
        Update-PSScriptFileInfo -FilePath $scriptFilePath -CompanyName "Microsoft Corporation"
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "update script file Copyright property" {
        Update-PSScriptFileInfo -FilePath $scriptFilePath -Copyright "(c) 2022 Microsoft Corporation. All rights reserved"
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "update script file ExternalModuleDependencies property" {
        $testExternalModuleDependencies = @("PowerShellGet", "PackageManagement")
        Update-PSScriptFileInfo -FilePath $scriptFilePath -ExternalModuleDependencies $testExternalModuleDependencies
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "update script file ExternalScriptDependencies property" {
        $testExternalScriptDependencies = @("Required-Script1", "Required-Script2")
        Update-PSScriptFileInfo -FilePath $scriptFilePath -ExternalScriptDependencies $testExternalScriptDependencies
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "update script file IconUri property" {
        Update-PSScriptFileInfo -FilePath $scriptFilePath -IconUri "https://testscript.com/icon"
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "update script file LicenseUri property" {
        Update-PSScriptFileInfo -FilePath $scriptFilePath -LicenseUri "https://testscript.com/license"
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "update script file ProjectUri property" {
        Update-PSScriptFileInfo -FilePath $scriptFilePath -ProjectUri "https://testscript.com/"
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "update script file PrivateData property" {
        Update-PSScriptFileInfo -FilePath $scriptFilePath -PrivateData "this is some private data"
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "update script file ReleaseNotes property" {
        $testReleaseNotes = @("release 3.0.12 includes bug fixes", "release 3.0.13 includes feature requests")
        Update-PSScriptFileInfo -FilePath $scriptFilePath -ReleaseNotes $testReleaseNotes
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "update script file RequiredModules property" {
        $hashtable1 = @{ModuleName = "RequiredModule1"}
        $hashtable2 = @{ModuleName = "RequiredModule2"; ModuleVersion = "1.0.0.0"}
        $hashtable3 = @{ModuleName = "RequiredModule3"; RequiredVersion = "2.5.0.0"}
        $hashtable4 = @{ModuleName = "RequiredModule4"; ModuleVersion = "1.1.0.0"; MaximumVersion = "2.0.0.0"}
        $testRequiredModules = $hashtable1, $hashtable2, $hashtable3, $hashtable4 

        Update-PSScriptFileInfo -FilePath $scriptFilePath -RequiredModules $testRequiredModules
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "update script file RequiredScripts property" {
        $testRequiredScripts = @("Required-Script1", "Required-Script2")
        Update-PSScriptFileInfo -FilePath $scriptFilePath -RequiredScripts $testRequiredScripts
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    It "update script file Tags property" {
        $testTags = @("Tag1", "Tag2")
        Update-PSScriptFileInfo -FilePath $scriptFilePath -Tags $testTags
        Test-PSScriptFileInfo $scriptFilePath | Should -Be $true
    }

    # Validate param needs to be tested
}
