# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test Update-PSScriptFileInfo" {

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
    It "Update .ps1 file with relative path" {
        $RelativeCurrentPath = Get-Location
        $ScriptFilePath = Join-Path -Path $relativeCurrentPath -ChildPath "$script:PSScriptInfoName.ps1"
        $OldDescription = "Old description for test script"
        $NewDescription = "Old description for test script"
        New-PSScriptFileInfo -FilePath $ScriptFilePath -Description $OldDescription
        
        Update-PSScriptFileInfo -FilePath $ScriptFilePath -Description $NewDescription

        Test-PSScriptFileInfo -FilePath $ScriptFilePath | Should -BeTrue
        Remove-Item -Path $ScriptFilePath
    }

    It "Update .ps1 given Version parameter" {
        $Version =  "2.0.0.0"
        $Description = "Test description"
        New-PSScriptFileInfo -FilePath  $script:testPSScriptInfoPath -Description $Description
        
        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -Version $Version

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testPSScriptInfoPath -Raw
        $results.Contains($Version) | Should -BeTrue
        $results.Contains(".VERSION $Version") | Should -BeTrue
    }

    It "Update .ps1 given prerelease version" {
        $Version =  "2.0.0.0-alpha"
        $Description = "Test description"
        New-PSScriptFileInfo -FilePath  $script:testPSScriptInfoPath -Description $Description
        
        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -Version $Version

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testPSScriptInfoPath -Raw
        $results.Contains($Version) | Should -BeTrue
        $results.Contains(".VERSION $Version") | Should -BeTrue
    }

    It "Should not update .ps1 with invalid version" {
        $Version =  "4.0.0.0.0"
        $Description = "Test description"
        New-PSScriptFileInfo -FilePath  $script:testPSScriptInfoPath -Description $Description
        
        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -Version $Version -ErrorVariable err -ErrorAction SilentlyContinue

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeFalse   
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "VersionParseIntoNuGetVersion,Microsoft.PowerShell.PowerShellGet.Cmdlets.UpdatePSScriptFileInfo"
    }

    It "Update .ps1 given Guid parameter" {
        $Guid = [guid]::NewGuid()
        $Description = "Test description"
        New-PSScriptFileInfo -FilePath  $script:testPSScriptInfoPath -Description $Description

        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -Guid $Guid

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testPSScriptInfoPath -Raw
        $results.Contains($Guid) | Should -BeTrue
        $results.Contains(".GUID $Guid") | Should -BeTrue
    }

    It "Update .ps1 given Author parameter" {
        $Author = "New Author" 
        $Description = "Test description"
        New-PSScriptFileInfo -FilePath  $script:testPSScriptInfoPath -Description $Description

        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -Author $Author

        
        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testPSScriptInfoPath -Raw
        $results.Contains($Author) | Should -BeTrue
        $results.Contains(".AUTHOR $Author") | Should -BeTrue
    }

    It "Update .ps1 given Description parameter" {
        $OldDescription = "Old description for test script."
        $NewDescription = "New description for test script."
        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -Description $OldDescription

        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -Description $NewDescription

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($NewDescription) | Should -BeTrue
        $results -like ".DESCRIPTION*$NewDescription" | Should -BeTrue
    }

    It "Update .ps1 given CompanyName parameter" {
        $OldCompanyName =  "Old company name"
        $NewCompanyName =  "New company name"
        $Description = "Test description"
        New-PSScriptFileInfo -FilePath  $script:testPSScriptInfoPath -CompanyName $OldCompanyName -Description $Description

        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -CompanyName $NewCompanyName

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testPSScriptInfoPath -Raw
        $results.Contains($NewCompanyName) | Should -BeTrue
        $results.Contains(".COMPANYNAME $NewCompanyName") | Should -BeTrue
    }

    It "Update .ps1 given Copyright parameter" {
        $OldCopyright =  "(c) Old Test Corporation"
        $NewCopyright =  "(c) New Test Corporation"
        $Description = "Test description"
        New-PSScriptFileInfo -FilePath  $script:testPSScriptInfoPath -Copyright $OldCopyright -Description $Description

        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -Copyright $NewCopyright

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testPSScriptInfoPath -Raw
        $results.Contains($NewCopyright) | Should -BeTrue
        $results.Contains(".COPYRIGHT $NewCopyright") | Should -BeTrue
    }

    It "Update .ps1 given RequiredModules parameter" {
        $RequiredModuleName = 'PackageManagement'
        $OldrequiredModuleVersion = '1.0.0.0'
        $OldRequiredModules =  @(@{ModuleName = $RequiredModuleName; ModuleVersion = $OldrequiredModuleVersion })
        $NewrequiredModuleVersion = '2.0.0.0'
        $NewRequiredModules =  @(@{ModuleName = $RequiredModuleName; ModuleVersion = $NewrequiredModuleVersion })
        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath  -RequiredModules $OldRequiredModules -Description $Description 

        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -RequiredModules $NewRequiredModules

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($RequiredModuleName) | Should -BeTrue
        $results.Contains($NewrequiredModuleVersion) | Should -BeTrue
        $results -like ".REQUIREDMODULES*$RequiredModuleName*$NewrequiredModuleVersion" | Should -BeTrue
    }

    It "Update .ps1 given ReleaseNotes parameter" {
        $Description = "Test Description"
        $OldReleaseNotes = "Old release notes for script."
        $NewReleaseNotes = "New release notes for script."
        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -ReleaseNotes $OldReleaseNotes -Description $Description 

        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -ReleaseNotes $NewReleaseNotes

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($NewReleaseNotes) | Should -BeTrue
        $results -like ".RELEASENOTES*$NewReleaseNotes" | Should -BeTrue
    }

    It "Update .ps1 given Tags parameter" {
        $Description = "Test Description"
        $OldTag1 = "Tag1"
        $OldTag2 = "Tag2"
        $NewTag1 = "NewTag1"
        $NewTag2 = "NewTag2"
        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -Tags $OldTag1, $OldTag2 -Description $Description 

        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -Tags $NewTag1, $NewTag2

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($NewTag1) | Should -BeTrue
        $results.Contains($NewTag2) | Should -BeTrue
        $results.Contains(".TAGS $NewTag1 $NewTag2") | Should -BeTrue
    }

    It "Update .ps1 given ProjectUri parameter" {
        $Description = "Test Description"
        $OldProjectUri = "https://www.oldtestprojecturi.com/"
        $NewProjectUri = "https://www.newtestprojecturi.com/"
        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -ProjectUri $OldProjectUri -Description $Description 

        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -ProjectUri $NewProjectUri

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($NewProjectUri) | Should -BeTrue
        $results.Contains(".PROJECTURI $NewProjectUri") | Should -BeTrue
    }

    It "Update .ps1 given LicenseUri parameter" {
        $Description = "Test Description"
        $OldLicenseUri = "https://www.oldtestlicenseuri.com/"
        $NewLicenseUri = "https://www.newtestlicenseuri.com/"
        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -LicenseUri $OldLicenseUri -Description $Description 

        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -LicenseUri $NewLicenseUri

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($NewLicenseUri) | Should -BeTrue
        $results.Contains(".LICENSEURI $NewLicenseUri") | Should -BeTrue
    }

    It "Update .ps1 given IconUri parameter" {
        $Description = "Test Description"
        $OldIconUri = "https://www.oldtesticonuri.com/"
        $NewIconUri = "https://www.newtesticonuri.com/"
        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -IconUri $OldIconUri -Description $Description 

        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -IconUri $NewIconUri

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($NewIconUri) | Should -BeTrue
        $results.Contains(".ICONURI $NewIconUri") | Should -BeTrue
    }

    It "Update .ps1 given ExternalModuleDependencies parameter" {
        $Description = "Test Description"
        $OldExternalModuleDep1 = "OldExternalModuleDep1"
        $OldExternalModuleDep2 = "OldExternalModuleDep2"
        $OldExternalModuleDep1FileName = "OldExternalModuleDep1.psm1"
        $OldExternalModuleDep2FileName = "OldExternalModuleDep2.psm1"
        $OldExternalModuleDepPath1 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $OldExternalModuleDep1FileName
        $OldExternalModuleDepPath2 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $OldExternalModuleDep2FileName
        $null = New-Item -Path $OldExternalModuleDepPath1 -ItemType File -Force
        $null = New-Item -Path $OldExternalModuleDepPath2 -ItemType File -Force

        $NewExternalModuleDep1 = "NewExternalModuleDep1"
        $NewExternalModuleDep2 = "NewExternalModuleDep2"
        $NewExternalModuleDep1FileName = "NewExternalModuleDep1.psm1"
        $NewExternalModuleDep2FileName = "NewExternalModuleDep2.psm1"
        $NewExternalModuleDepPath1 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $NewExternalModuleDep1FileName
        $NewExternalModuleDepPath2 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $NewExternalModuleDep2FileName
        $null = New-Item -Path $NewExternalModuleDepPath1 -ItemType File -Force
        $null = New-Item -Path $NewExternalModuleDepPath2 -ItemType File -Force

        # NOTE: you may need to add the -NestedModules parameter here as well
        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -ExternalModuleDependencies $OldExternalModuleDep1, $OldExternalModuleDep2 -Description $Description

        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -ExternalModuleDependencies $NewExternalModuleDep1, $NewExternalModuleDep2
        
        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($NewExternalModuleDep1) | Should -BeTrue
        $results.Contains($NewExternalModuleDep2) | Should -BeTrue
        $results -like ".EXTERNALMODULEDEPENDENCIES*$NewExternalModuleDep1*$NewExternalModuleDep2" | Should -BeTrue
    }

    It "Update .ps1 given RequiredAssemblies parameter" {
        $Description = "Test Description"
        $OldRequiredAssembly1 = "OldRequiredAssembly1.dll"
        $OldRequiredAssembly2 = "OldRequiredAssembly2.dll"
        $OldRequiredAssemblyPath1 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $OldRequiredAssembly1
        $OldRequiredAssemblyPath2 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $OldRequiredAssembly2
        $null = New-Item -Path $OldRequiredAssemblyPath1 -ItemType File -Force
        $null = New-Item -Path $OldRequiredAssemblyPath2 -ItemType File -Force

        $NewRequiredAssembly1 = "NewRequiredAssembly1.dll"
        $NewRequiredAssembly2 = "NewRequiredAssembly2.dll"
        $NewRequiredAssemblyPath1 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $NewRequiredAssembly1
        $NewRequiredAssemblyPath2 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $NewRequiredAssembly2
        $null = New-Item -Path $NewRequiredAssemblyPath1 -ItemType File -Force
        $null = New-Item -Path $NewRequiredAssemblyPath2 -ItemType File -Force

        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -RequiredAssemblies $OldRequiredAssembly1, $OldRequiredAssembly2 -Description $Description

        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -RequiredAssemblies $NewRequiredAssembly1, $NewRequiredAssembly2

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($NewRequiredAssembly1) | Should -BeTrue
        $results.Contains($NewRequiredAssembly2) | Should -BeTrue
        $results -like ".REQUIREDASSEMBLIES*$NewRequiredAssembly1*$NewRequiredAssembly2" | Should -BeTrue
    }

    It "Update .ps1 given NestedModules parameter" {
        $Description = "Test Description"
        $OldNestedModule1 = "OldNestedModule1"
        $OldNestedModule2 = "OldNestedModule2"
        $OldNestModuleFileName1 = "OldNestedModule1.dll"
        $OldNestModuleFileName2 = "OldNestedModule2.dll"
        $OldNestedModulePath1 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $OldNestModuleFileName1
        $OldNestedModulePath2 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $OldNestModuleFileName2
        $null = New-Item -Path $OldNestedModulePath1 -ItemType File -Force
        $null = New-Item -Path $OldNestedModulePath2 -ItemType File -Force

        $NewNestedModule1 = "NewNestedModule1"
        $NewNestedModule2 = "NewNestedModule2"
        $NewNestModuleFileName1 = "NewNestedModule1.dll"
        $NewNestModuleFileName2 = "NewNestedModule2.dll"
        $NewNestedModulePath1 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $NewNestModuleFileName1
        $NewNestedModulePath2 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $NewNestModuleFileName2
        $null = New-Item -Path $NewNestedModulePath1 -ItemType File -Force
        $null = New-Item -Path $NewNestedModulePath2 -ItemType File -Force

        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -NestedModules $OldNestedModule1, $OldNestedModule2 -Description $Description

        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -NestedModules $NewNestedModule1, $NewNestedModule2

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($NewNestedModule1) | Should -BeTrue
        $results.Contains($NewNestedModule2) | Should -BeTrue
        $results -like ".NESTEDMODULES*$NewNestedModule1*$NewNestedModule2" | Should -BeTrue
    }

    It "Update .ps1 given RequiredScripts parameter" {
        $Description = "Test Description"
        $OldRequiredScript1 = "OldNestedModule1.ps1"
        $OldRequiredScript2 = "OldNestedModule2.ps1"
        $OldRequiredScript1Path = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $OldRequiredScript1
        $OldRequiredScript2Path = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $OldRequiredScript2
        $null = New-Item -Path $OldRequiredScript1Path -ItemType File -Force
        $null = New-Item -Path $OldRequiredScript2Path -ItemType File -Force
        
        $NewRequiredScript1 = "NewNestedModule1.ps1"
        $NewRequiredScript2 = "NewNestedModule2.ps1"
        $NewRequiredScript1Path = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $NewRequiredScript1
        $NewRequiredScript2Path = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $NewRequiredScript2
        $null = New-Item -Path $NewRequiredScript1Path -ItemType File -Force
        $null = New-Item -Path $NewRequiredScript2Path -ItemType File -Force

        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -RequiredScripts $OldRequiredScript1, $OldRequiredScript2 -Description $Description

        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -RequiredScripts $NewRequiredScript1, $NewRequiredScript2

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($NewRequiredScript1) | Should -BeTrue
        $results.Contains($NewRequiredScript2) | Should -BeTrue
        $results -like ".REQUIREDSCRIPTS*$NewRequiredScript1*$NewRequiredScript2" | Should -BeTrue
    }

    It "Update .ps1 given ExternalScriptDependencies parameter" {
        $Description = "Test Description"
        $OldExternalScriptDep1 = "OldExternalScriptDep1.ps1"
        $OldExternalScriptDep2 = "OldExternalScriptDep2.ps1"
        $OldExternalScriptDepPath1 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $OldExternalScriptDep1
        $OldExternalScriptDepPath2 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $OldExternalScriptDep2
        $null = New-Item -Path $OldExternalScriptDepPath1 -ItemType File -Force
        $null = New-Item -Path $OldExternalScriptDepPath2 -ItemType File -Force

        $NewExternalScriptDep1 = "NewExternalScriptDep1.ps1"
        $NewExternalScriptDep2 = "NewExternalScriptDep2.ps1"
        $NewExternalScriptDepPath1 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $NewExternalScriptDep1
        $NewExternalScriptDepPath2 = Microsoft.PowerShell.Management\Join-Path -Path $script:TempScriptPath -ChildPath $NewExternalScriptDep2
        $null = New-Item -Path $NewExternalScriptDepPath1 -ItemType File -Force
        $null = New-Item -Path $NewExternalScriptDepPath2 -ItemType File -Force

        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -ExternalModuleDependencies $OldExternalModuleDep1, $OldExternalModuleDep2 -Description $Description

        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -ExternalModuleDependencies $NewExternalModuleDep1, $NewExternalModuleDep2

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($NewExternalModuleDep1) | Should -BeTrue
        $results.Contains($NewExternalModuleDep2) | Should -BeTrue
        $results -like ".EXTERNALSCRIPTDEPENDENCIES*$NewExternalModuleDep1*$NewExternalModuleDep2" | Should -BeTrue
    }

    It "Update .ps1 given PrivateData parameter" {
        $Description = "Test Description"
        $OldPrivateData = @{"OldPrivateDataEntry1" = "OldPrivateDataValue1"}
        $NewPrivateData = @{"NewPrivateDataEntry1" = "NewPrivateDataValue1"}
        New-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -PrivateData $OldPrivateData -Description $Description 
        
        Update-PSScriptFileInfo -FilePath $script:testPSScriptInfoPath -PrivateData $NewPrivateData 

        Test-Path -FilePath $script:testPSScriptInfoPath | Should -BeTrue
        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($NewPrivateData) | Should -BeTrue
        $results -like ".PRIVATEDATA*$NewPrivateData" | Should -BeTrue
    }

    It "Update signed script when using RemoveSignature parameter" {
        $scriptName = "ScriptWithSignature.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        # use a copy of the signed script file so we can re-use for other tests
        $null = Copy-Item -Path $scriptFilePath -Destination $TestDrive
        $tmpScriptFilePath = Join-Path -Path $TestDrive -ChildPath $scriptName

        Update-PSScriptFileInfo -FilePath $tmpScriptFilePath -Version "2.0.0.0" -RemoveSignature
        Test-PSScriptFileInfo -FilePath $tmpScriptFilePath | Should -Be $true
    }
    
    It "Throw error when attempting to update a signed script without -RemoveSignature parameter" {
        $scriptName = "ScriptWithSignature.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        # use a copy of the signed script file so we can re-use for other tests
        $null = Copy-Item -Path $scriptFilePath -Destination $TestDrive
        $tmpScriptFilePath = Join-Path -Path $TestDrive -ChildPath $scriptName

        { Update-PSScriptFileInfo -FilePath $tmpScriptFilePath -Version "2.0.0.0" } | Should -Throw -ErrorId "ScriptToBeUpdatedContainsSignature,Microsoft.PowerShell.PowerShellGet.Cmdlets.UpdatePSScriptFileInfo"
    }
#>
}