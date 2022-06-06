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
    }
    AfterEach {
        $scriptFilePath = Join-Path -Path $tmpDir1Path -ChildPath "testScript.ps1"
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

    It "create .ps1 file with minimal required fields" {    
        $scriptFilePath = Join-Path -Path $tmpDir1Path -ChildPath "testScript.ps1"
        $scriptDescription = "this is a test script"
        $res = New-PSScriptFileInfo -FilePath $scriptFilePath -Description $scriptDescription -PassThru
        $res | Should -Not -BeNullOrEmpty

        Test-PSScriptFileInfo -FilePath $scriptFilePath | Should -BeTrue
    }

    It "create .ps1 file with relative path" {
        $relativeCurrentPath = Get-Location
        $scriptFilePath = Join-Path -Path $relativeCurrentPath -ChildPath "testScript.ps1"
        $scriptDescription = "this is a test script"
        $res = New-PSScriptFileInfo -FilePath $scriptFilePath -Description $scriptDescription -PassThru
        $res | Should -Not -BeNullOrEmpty

        Test-PSScriptFileInfo -FilePath $scriptFilePath | Should -BeTrue
        Remove-Item -Path (Join-Path -Path $relativeCurrentPath -ChildPath "testScript.ps1")
    }

    It "create .ps1 file with RequiredModules" {
        #MaximumVersion, #RequiredVersion, #ModuleVersion
        $hashtable1 = @{ModuleName = "RequiredModule1"}
        $hashtable2 = @{ModuleName = "RequiredModule2"; ModuleVersion = "1.0.0.0"}
        $hashtable3 = @{ModuleName = "RequiredModule3"; RequiredVersion = "2.5.0.0"}
        $hashtable4 = @{ModuleName = "RequiredModule4"; ModuleVersion = "1.1.0.0"; MaximumVersion = "2.0.0.0"}
        $requiredModulesHashtables = $hashtable1, $hashtable2, $hashtable3, $hashtable4

        $scriptFilePath = Join-Path -Path $tmpDir1Path -ChildPath "testScript2.ps1"
        $scriptDescription = "this is a test script"
        $res = New-PSScriptFileInfo -FilePath $scriptFilePath -Description $scriptDescription -RequiredModules $requiredModulesHashtables -PassThru
        $res | Should -Not -BeNullOrEmpty

        Test-PSScriptFileInfo -FilePath $scriptFilePath | Should -BeTrue
    }

    # TODO: test with each param really....would be easier to test if we returned PSSriptFileInfoObject with PassThru
    # FilePath, Version, Author, Description, Guid, CompanyName, Copyright, RequiredModules,
    # ExternalModuleDependencies, RequiredScripts, ExternalScriptDependencies, Tags
    # ProjectUri, LicenseUri, IconUri, ReleaseNotes, PrivateData, PassThru, Force

    # currently testing with: FilePath, RequiredModules
}
