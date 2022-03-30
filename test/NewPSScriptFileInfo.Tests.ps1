# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test New-PSScriptFileInfo" {
    BeforeEach {
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path $TestDrive -ChildPath "tmpDir3"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path)
        Get-NewTestDirs($tmpDirPaths)
    }
    AfterEach {
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path $TestDrive -ChildPath "tmpDir3"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path)
        Get-RemoveTestDirs($tmpDirPaths)
    }

    It "create .ps1 file with minimal required fields" {
        $pathTestRes = Test-Path $tmpDir1Path
        $pathTestRes | Should -Be $true
        Write-Host $pathTestRes        
        $basicScriptFilePath = Join-Path -Path $tmpDir1Path -ChildPath "basicTestScript.ps1"
        Write-Host $basicScriptFilePath
        $scriptDescription = "this is a test script"
        # $res = New-PSScriptFileInfo -FilePath $basicScriptFilePath -Description $scriptDescription -PassThru
        # $res.Description | Should -Be $scriptDescription
    }
}
