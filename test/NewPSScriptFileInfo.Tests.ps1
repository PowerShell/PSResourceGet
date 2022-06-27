# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test New-PSScriptFileInfo" {
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
    It "Create .ps1 file with minimal required fields" {    
        $Description = "this is a test script"
        New-PSScriptFileInfo -FilePath  $script:testPSScriptInfoPath -Description $Description

        Test-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath | Should -BeTrue
    }

    It "Create .ps1 file with relative path" {
        $RelativeCurrentPath = Get-Location
        $ScriptFilePath = Join-Path -Path $relativeCurrentPath -ChildPath "$script:PSScriptInfoName.ps1"
        $Description = "this is a test script"
        New-PSScriptFileInfo -FilePath $ScriptFilePath -Description $Description

        Test-PSScriptFileInfo -FilePath $ScriptFilePath | Should -BeTrue
        Remove-Item -Path $ScriptFilePath
    }

    It "Create new .ps1 given Version parameter" {
        $Version =  "2.0.0.0"
        $Description = "Test description"

        New-PSScriptFileInfo -FilePath  $script:testPSScriptInfoPath -Version $Version -Description $Description

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testPSScriptInfoPath -Raw
        $results.Contains($Version) | Should -BeTrue
        $results.Contains(".VERSION $Version") | Should -BeTrue
    }

    It "Create new .ps1 given Guid parameter" {
        $Guid = [guid]::NewGuid()
        $Description = "Test description"

        New-PSScriptFileInfo -FilePath  $script:testPSScriptInfoPath -Guid $Guid -Description $Description

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testPSScriptInfoPath -Raw
        $results.Contains($Guid) | Should -BeTrue
        $results.Contains(".GUID $Guid") | Should -BeTrue
    }

    It "Create new .ps1 given Author parameter" {
        $Author = "Test Author" 
        $Description = "Test description"

        New-PSScriptFileInfo -FilePath  $script:testPSScriptInfoPath -Author $Author -Description $Description

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testPSScriptInfoPath -Raw
        $results.Contains($Author) | Should -BeTrue
        $results.Contains(".AUTHOR $Author") | Should -BeTrue
    }

    It "Create new .ps1 given Description parameter" {
        $Description = "PowerShellGet test description"
       
        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -Description $Description

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($Description) | Should -BeTrue
        $results -like ".DESCRIPTION*$Description" | Should -BeTrue
    }

    It "Create new .ps1 given CompanyName parameter" {
        $CompanyName =  "Microsoft"
        $Description = "Test description"

        New-PSScriptFileInfo -FilePath  $script:testPSScriptInfoPath -CompanyName $CompanyName -Description $Description

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testPSScriptInfoPath -Raw
        $results.Contains($CompanyName) | Should -BeTrue
        $results.Contains(".COMPANYNAME $Companyname") | Should -BeTrue
    }

    It "Create new .ps1 given Copyright parameter" {
        $Copyright =  "(c) Test Corporation"
        $Description = "Test description"

        New-PSScriptFileInfo -FilePath  $script:testPSScriptInfoPath -Copyright $Copyright -Description $Description

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testPSScriptInfoPath -Raw
        $results.Contains($Copyright) | Should -BeTrue
        $results.Contains(".COPYRIGHT $Copyright") | Should -BeTrue
    }

    It "Create new .ps1 given RequiredModules parameter" {
        $requiredModuleName = 'PackageManagement'
        $requiredModuleVersion = '1.0.0.0'
        $RequiredModules =  @(@{ModuleName = $requiredModuleName; ModuleVersion = $requiredModuleVersion })

        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -RequiredModules $RequiredModules -Description $Description 

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($requiredModuleName) | Should -BeTrue
        $results.Contains($requiredModuleVersion) | Should -BeTrue
        $results -like ".REQUIREDMODULES*$requiredModuleName*$requiredModuleVersion" | Should -BeTrue
    }

    It "Create new .ps1 given ReleaseNotes parameter" {
        $Description = "Test Description"
        $ReleaseNotes = "Release notes for script."

        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -ReleaseNotes $ReleaseNotes -Description $Description 

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($ReleaseNotes) | Should -BeTrue
        $results -like ".RELEASENOTES*$ReleaseNotes" | Should -BeTrue
    }

    It "Create new .ps1 given Tags parameter" {
        $Description = "Test Description"
        $Tag1 = "tag1"
        $Tag2 = "tag2"

        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -Tags $Tag1, $Tag2 -Description $Description 

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($Tag1) | Should -BeTrue
        $results.Contains($Tag2) | Should -BeTrue
        $results.Contains(".TAGS $Tag1 $Tag2") | Should -BeTrue
    }

    It "Create new .ps1 given ProjectUri parameter" {
        $Description = "Test Description"
        $ProjectUri = "https://www.testprojecturi.com/"
      
        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -ProjectUri $ProjectUri -Description $Description 

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($ProjectUri) | Should -BeTrue
        $results.Contains(".PROJECTURI $ProjectUri") | Should -BeTrue
    }

    It "Create new .ps1 given LicenseUri parameter" {
        $Description = "Test Description"
        $LicenseUri = "https://www.testlicenseuri.com/"

        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -LicenseUri $LicenseUri -Description $Description 

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($LicenseUri) | Should -BeTrue
        $results.Contains(".LICENSEURI $LicenseUri") | Should -BeTrue
    }

    It "Create new .ps1 given IconUri parameter" {
        $Description = "Test Description"
        $IconUri = "https://www.testiconuri.com/"
  
        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -IconUri $IconUri -Description $Description 

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($IconUri) | Should -BeTrue
        $results.Contains(".ICONURI $IconUri") | Should -BeTrue
    }

    It "Create new .ps1 given ExternalModuleDependencies parameter" {
        $Description = "Test Description"
        $ExternalModuleDep1 = "ExternalModuleDep1"
        $ExternalModuleDep2 = "ExternalModuleDep2"
        $ExternalModuleDep1FileName = "ExternalModuleDep1.psm1"
        $ExternalModuleDep2FileName = "ExternalModuleDep2.psm1"
        $ExternalModuleDepPath1 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $ExternalModuleDep1FileName
        $ExternalModuleDepPath2 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $ExternalModuleDep2FileName

        $null = New-Item -Path $ExternalModuleDepPath1 -ItemType File -Force
        $null = New-Item -Path $ExternalModuleDepPath2 -ItemType File -Force

        # NOTE: you may need to add the -NestedModules parameter here as well
        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -ExternalModuleDependencies $ExternalModuleDep1, $ExternalModuleDep2 -Description $Description

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($ExternalModuleDep1) | Should -BeTrue
        $results.Contains($ExternalModuleDep2) | Should -BeTrue
        $results -like ".EXTERNALMODULEDEPENDENCIES*$ExternalModuleDep1*$ExternalModuleDep2" | Should -BeTrue
    }

    It "Create new .ps1 given RequiredAssemblies parameter" {
        $Description = "Test Description"
        $RequiredAssembly1 = "RequiredAssembly1.dll"
        $RequiredAssembly2 = "RequiredAssembly2.dll"
        $RequiredAssemblyPath1 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $RequiredAssembly1
        $RequiredAssemblyPath2 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $RequiredAssembly2

        $null = New-Item -Path $RequiredAssemblyPath1 -ItemType File -Force
        $null = New-Item -Path $RequiredAssemblyPath2 -ItemType File -Force

        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -RequiredAssemblies $RequiredAssembly1, $RequiredAssembly2 -Description $Description

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($RequiredAssembly1) | Should -BeTrue
        $results.Contains($RequiredAssembly2) | Should -BeTrue
        $results -like ".REQUIREDASSEMBLIES*$RequiredAssembly1*$RequiredAssembly2" | Should -BeTrue
    }

    It "Create new .ps1 given NestedModules parameter" {
        $Description = "Test Description"
        $NestedModule1 = "NestedModule1"
        $NestedModule2 = "NestedModule2"
        $NestModuleFileName1 = "NestedModule1.dll"
        $NestModuleFileName2 = "NestedModule2.dll"
        $NestedModulePath1 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $NestModuleFileName1
        $NestedModulePath2 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $NestModuleFileName2

        $null = New-Item -Path $NestedModulePath1 -ItemType File -Force
        $null = New-Item -Path $NestedModulePath2 -ItemType File -Force

        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -NestedModules $NestedModule1, $NestedModule2 -Description $Description

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($NestedModule1) | Should -BeTrue
        $results.Contains($NestedModule2) | Should -BeTrue
        $results -like ".NESTEDMODULES*$NestedModule1*$NestedModule2" | Should -BeTrue
    }

    It "Create new .ps1 given RequiredScripts parameter" {
        $Description = "Test Description"
        $RequiredScript1 = "NestedModule1.ps1"
        $RequiredScript2 = "NestedModule2.ps1"
        $RequiredScript1Path = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $RequiredScript1
        $RequiredScript2Path = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $RequiredScript2

        $null = New-Item -Path $RequiredScript1Path -ItemType File -Force
        $null = New-Item -Path $RequiredScript2Path -ItemType File -Force

        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -RequiredScripts $RequiredScript1, $RequiredScript2 

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($RequiredScript1) | Should -BeTrue
        $results.Contains($RequiredScript2) | Should -BeTrue
        $results -like ".REQUIREDSCRIPTS*$RequiredScript1*$RequiredScript2" | Should -BeTrue
    }

    It "Create new .ps1 given ExternalScriptDependencies parameter" {
        $Description = "Test Description"
        $ExternalScriptDep1 = "ExternalScriptDep1.ps1"
        $ExternalScriptDep2 = "ExternalScriptDep2.ps1"
        $ExternalScriptDepPath1 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $ExternalScriptDep1
        $ExternalScriptDepPath2 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $ExternalScriptDep2

        $null = New-Item -Path $ExternalScriptDepPath1 -ItemType File -Force
        $null = New-Item -Path $ExternalScriptDepPath2 -ItemType File -Force

        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -ExternalModuleDependencies $ExternalModuleDep1, $ExternalModuleDep2 -Description $Description

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($ExternalModuleDep1) | Should -BeTrue
        $results.Contains($ExternalModuleDep2) | Should -BeTrue
        $results -like ".EXTERNALSCRIPTDEPENDENCIES*$ExternalScriptDep1*$ExternalScriptDep2" | Should -BeTrue
    }

    It "Create new .ps1 given PrivateData parameter" {
        $Description = "Test Description"
        $PrivateData = @{"PrivateDataEntry1" = "PrivateDataValue1"}
        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -PrivateData $PrivateData -Description $Description 

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($PrivateData) | Should -BeTrue
        $results -like ".PRIVATEDATA*$PrivateData" | Should -BeTrue
    }
#>
}