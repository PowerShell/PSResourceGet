# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test Test-PSScriptFileInfo" {

    BeforeAll {
        $script:TempPath = Get-TempPath
    }
    BeforeEach {
        # Create temp script path
        $script:TempScriptPath = Join-Path $script:TempPath "PSGet_$(Get-Random)"
        $null = New-Item -Path $script:TempScriptPath -ItemType Directory -Force
  
        $script:PSScriptInfoName = "PSGetTestScript"
        $script:testPSScriptInfoPath = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath "$script:PSScriptInfoName.psd1"
    }
    AfterEach {
        RemoveItem "$script:TempScriptPath"
    }

    ### TODO:  Add tests for -Force and -WhatIf if those parameters are applicable
    <#
    It "Test .ps1 file with minimal required fields" {    
        $Description = "This is a test script"
        New-PSScriptFileInfo -FilePath  $script:testPSScriptInfoPath -Description $Description

        Test-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath | Should -BeTrue
    }

    It "Test .ps1 file with relative path" {
        $RelativeCurrentPath = Get-Location
        $ScriptFilePath = Join-Path -Path $relativeCurrentPath -ChildPath "$script:PSScriptInfoName.ps1"
        $Description = "this is a test script"
        New-PSScriptFileInfo -FilePath $ScriptFilePath -Description $Description

        Test-PSScriptFileInfo -FilePath $ScriptFilePath | Should -BeTrue
        Remove-Item -Path $ScriptFilePath
    }
    #>
}
