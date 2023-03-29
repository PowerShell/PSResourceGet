# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Update-ModuleManifest' -Tags 'CI' {

    BeforeEach {
        # Create temp module manifest to be updated
        $script:TempModulesPath = Join-Path $TestDrive "PSGet_$(Get-Random)"
        $null = New-Item -Path $script:TempModulesPath -ItemType Directory -Force
  
        $script:UpdateModuleManifestName = "PSGetTestModule"
        $script:UpdateModuleManifestBase = Join-Path $script:TempModulesPath $script:UpdateModuleManifestName
        $null = New-Item -Path $script:UpdateModuleManifestBase -ItemType Directory -Force
  
        $script:testManifestPath = Microsoft.PowerShell.Management\Join-Path -Path $script:UpdateModuleManifestBase -ChildPath "$script:UpdateModuleManifestName.psd1"
    }

    AfterEach {
        RemoveItem "$script:TempModulesPath"
    }

    It "Update module manifest given Path parameter" -Pending:(Get-IsWindows) {
        $description = "This is a PowerShellGet test"
        New-ModuleManifest -Path $script:testManifestPath
        Update-ModuleManifest -Path $script:testManifestPath -Description $description

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.Description | Should -Be $description
    }

    It "Update module manifest given Guid parameter" -Pending:(Get-IsWindows) {
        $Guid = [guid]::NewGuid()
        New-ModuleManifest -Path $script:testManifestPath
        Update-ModuleManifest -Path $script:testManifestPath -Guid $Guid 

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.Guid | Should -Be $Guid 
    }

    It "Update module manifest given Author parameter" -Pending:(Get-IsWindows) {
        $Author = "Test Author" 
        New-ModuleManifest -Path $script:testManifestPath
        Update-ModuleManifest -Path $script:testManifestPath -Author $Author 

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.Author | Should -Be $Author 
    }

    It "Update module manifest given Description parameter" -Pending:(Get-IsWindows) {
        $Description = "PowerShellGet test description"
        New-ModuleManifest -Path $script:testManifestPath
        Update-ModuleManifest -Path $script:testManifestPath -Description $Description 

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.Description | Should -Be $Description 
    }

    It "Update module manifest given ModuleVersion parameter" -Pending:(Get-IsWindows) {
        $ModuleVersion =  "7.0.0.0"
        New-ModuleManifest -Path $script:testManifestPath
        Update-ModuleManifest -Path $script:testManifestPath -ModuleVersion $ModuleVersion 

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.Version.ToString() | Should -Be $ModuleVersion 
    }

    It "Update module manifest given RequiredModules parameter" -Pending:(Get-IsWindows) {
        $requiredModuleName = 'PackageManagement'
        $requiredModuleVersion = '1.0.0.0'
        $RequiredModules =  @(@{ModuleName = $requiredModuleName; ModuleVersion = $requiredModuleVersion })
        New-ModuleManifest -Path $script:testManifestPath
        Update-ModuleManifest -Path $script:testManifestPath -RequiredModules $RequiredModules -Description "test"

        $results = Test-ModuleManifest -Path $script:testManifestPath
        foreach ($module in $results.RequiredModules)
        {
            $module | Should -Be $requiredModuleName
            $module.Version | Should -Be $requiredModuleVersion
        }
    }

    It "Update module manifest given Prerelease parameter" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $ModuleVersion = "1.0.0"
        $Prerelease = "preview"
        New-ModuleManifest -Path $script:testManifestPath -Description $Description -ModuleVersion $ModuleVersion
        Update-ModuleManifest -Path $script:testManifestPath -Prerelease $Prerelease

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.PrivateData.PSData.Prerelease | Should -Be $Prerelease
    }

    It "Update module manifest given ReleaseNotes parameter" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $ReleaseNotes = "Release notes for module."
        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -ReleaseNotes $ReleaseNotes

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.PrivateData.PSData.ReleaseNotes | Should -Be $ReleaseNotes
    }

    It "Update module manifest given Tags parameter" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $Tag1 = "tag1"
        $Tag2 = "tag2"
        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -Tags $Tag1, $Tag2

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.PrivateData.PSData.Tags | Should -Be @($Tag1, $Tag2) 
    }

    It "Update module manifest given ProjectUri parameter" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $ProjectUri = "https://www.testprojecturi.com/"
        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -ProjectUri $ProjectUri

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.PrivateData.PSData.ProjectUri | Should -Be $ProjectUri
    }

    It "Update module manifest given LicenseUri parameter" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $LicenseUri = "https://www.testlicenseuri.com/"
        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -LicenseUri $LicenseUri

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.PrivateData.PSData.LicenseUri | Should -Be $LicenseUri
    }

    It "Update module manifest given IconUri parameter" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $IconUri = "https://www.testiconuri.com/"
        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -IconUri $IconUri

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.PrivateData.PSData.IconUri | Should -Be $IconUri
    }

    It "Update module manifest given RequireLicenseAcceptance parameter" -Pending:(Get-IsWindows) {
        $Description = "PowerShellGet test description"
        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -RequireLicenseAcceptance

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.PrivateData.PSData.RequireLicenseAcceptance | Should -Be $true
    }

    It "Update module manifest given ExternalModuleDependencies parameter" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $ExternalModuleDep1 = "ExternalModuleDep1"
        $ExternalModuleDep2 = "ExternalModuleDep2"
        $ExternalModuleDep1FileName = "ExternalModuleDep1.psm1"
        $ExternalModuleDep2FileName = "ExternalModuleDep2.psm1"
        $ExternalModuleDepPath1 = Microsoft.PowerShell.Management\Join-Path -Path $script:UpdateModuleManifestBase -ChildPath $ExternalModuleDep1FileName
        $ExternalModuleDepPath2 = Microsoft.PowerShell.Management\Join-Path -Path $script:UpdateModuleManifestBase -ChildPath $ExternalModuleDep2FileName

        $null = New-Item -Path $ExternalModuleDepPath1 -ItemType File -Force
        $null = New-Item -Path $ExternalModuleDepPath2 -ItemType File -Force

        New-ModuleManifest -Path $script:testManifestPath -Description $Description -NestedModules $ExternalModuleDep1, $ExternalModuleDep2
        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.PrivateData.PSData.ExternalModuleDependencies | Should -Be $null

        Update-ModuleManifest -Path $script:testManifestPath -ExternalModuleDependencies $ExternalModuleDep1, $ExternalModuleDep2
        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.PrivateData.PSData.ExternalModuleDependencies | Should -Be @($ExternalModuleDep1, $ExternalModuleDep2)
    }

    It "Update module manifest given PowerShellHostName parameter" -Pending:(Get-IsWindows) {
        $Description = "PowerShellGet test description"
        $PowerShellHostName = $Host.Name
        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -PowerShellHostName $PowerShellHostName

        $results = Test-ModuleManifest -Path $script:testManifestPath -ErrorAction SilentlyContinue
        $results.PowerShellHostName | Should -Be $PowerShellHostName
    }

    It "Update module manifest given DefaultCommandPrefix parameter" -Pending:(Get-IsWindows) {
        $Description = "PowerShellGet test description"
        $DefaultCommandPrefix = "testprefix"
        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -DefaultCommandPrefix $DefaultCommandPrefix

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.Prefix | Should -Be $DefaultCommandPrefix
    }

    It "Update module manifest given RootModule parameter" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $RootModuleName = $script:UpdateModuleManifestName + ".psm1"
        $RootModulePath = Microsoft.PowerShell.Management\Join-Path -Path $script:UpdateModuleManifestBase -ChildPath $RootModuleName
        $null = New-Item -Path $RootModulePath -ItemType File -Force

        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -RootModule $RootModuleName

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.RootModule | Should -Be $RootModuleName 
    }

    It "Update module manifest given RequiredAssemblies parameter" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $RequiredAssembly1 = "RequiredAssembly1.dll"
        $RequiredAssembly2 = "RequiredAssembly2.dll"
        $RequiredAssemblyPath1 = Microsoft.PowerShell.Management\Join-Path -Path $script:UpdateModuleManifestBase -ChildPath $RequiredAssembly1
        $RequiredAssemblyPath2 = Microsoft.PowerShell.Management\Join-Path -Path $script:UpdateModuleManifestBase -ChildPath $RequiredAssembly2

        $null = New-Item -Path $RequiredAssemblyPath1 -ItemType File -Force
        $null = New-Item -Path $RequiredAssemblyPath2 -ItemType File -Force

        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -RequiredAssemblies $RequiredAssembly1, $RequiredAssembly2

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.RequiredAssemblies | Should -Be @($RequiredAssembly1, $RequiredAssembly2) 
    }

    It "Update module manifest given NestedModules parameter" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $NestedModule1 = "NestedModule1"
        $NestedModule2 = "NestedModule2"
        $NestModuleFileName1 = "NestedModule1.dll"
        $NestModuleFileName2 = "NestedModule2.dll"
        $NestedModulePath1 = Microsoft.PowerShell.Management\Join-Path -Path $script:UpdateModuleManifestBase -ChildPath $NestModuleFileName1
        $NestedModulePath2 = Microsoft.PowerShell.Management\Join-Path -Path $script:UpdateModuleManifestBase -ChildPath $NestModuleFileName2

        $null = New-Item -Path $NestedModulePath1 -ItemType File -Force
        $null = New-Item -Path $NestedModulePath2 -ItemType File -Force

        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -NestedModules $NestedModule1, $NestedModule2

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.NestedModules | Should -Be @($NestedModule1, $NestedModule2) 
    }

    It "Update module manifest given FileList parameter" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $FileList1 = "FileList1.cs"
        $FileList2 = "FileList2.cs"
        $FileListPath1 = Microsoft.PowerShell.Management\Join-Path -Path $script:UpdateModuleManifestBase -ChildPath $FileList1
        $FileListPath2 = Microsoft.PowerShell.Management\Join-Path -Path $script:UpdateModuleManifestBase -ChildPath $FileList2

        $null = New-Item -Path $FileListPath1 -ItemType File -Force
        $null = New-Item -Path $FileListPath2 -ItemType File -Force

        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -FileList $FileList1, $FileList2

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.FileList | Should -Be @($FileListPath1, $FileListPath2) 
    }

    It "Update module manifest given TypesToProcess parameter" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $TypeFile = "TypeFile.ps1xml"
        $TypeFilePath = Microsoft.PowerShell.Management\Join-Path -Path $script:UpdateModuleManifestBase -ChildPath $TypeFile

        $null = New-Item -Path $TypeFilePath -ItemType File -Force

        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -TypesToProcess $TypeFile

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.ExportedTypeFiles | Should -Be $TypeFilePath
    }

    It "Update module manifest given FormatsToProcess parameter" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $FormatFile = "FormatFile.ps1xml"
        $FormatFilePath = Microsoft.PowerShell.Management\Join-Path -Path $script:UpdateModuleManifestBase -ChildPath $FormatFile

        $null = New-Item -Path $FormatFilePath -ItemType File -Force

        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -FormatsToProcess $FormatFile

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.ExportedFormatFiles | Should -Be $FormatFilePath
    }
    
    It "Update module manifest given ScriptsToProcess parameter" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $Script1 = "Script1.ps1"
        $Script2 = "Script2.ps1"
        $ScriptPath1 = Microsoft.PowerShell.Management\Join-Path -Path $script:UpdateModuleManifestBase -ChildPath $Script1
        $ScriptPath2 = Microsoft.PowerShell.Management\Join-Path -Path $script:UpdateModuleManifestBase -ChildPath $Script2

        $null = New-Item -Path $ScriptPath1 -ItemType File -Force
        $null = New-Item -Path $ScriptPath2 -ItemType File -Force

        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -ScriptsToProcess $Script1, $Script2

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.Scripts | Should -Be @($ScriptPath1, $ScriptPath2) 
    }

    It "Update module manifest given ProcessorArchitecture parameter" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $ProcessorArchitecture = [System.Reflection.ProcessorArchitecture]::Amd64
        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -ProcessorArchitecture $ProcessorArchitecture

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.ProcessorArchitecture | Should -Be $ProcessorArchitecture 
    }

    It "Update module manifest given ModuleList parameter" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $ModuleList1 = "PowerShellGet"
        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -ModuleList $ModuleList1

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.ModuleList | Should -Be $ModuleList1
    }

    It "Update module manifest given CompanyName, Copyright, PowerShellHostVersion, ClrVersion, DotnetFrameworkVersion, PowerShellVersion, HelpInfoUri, and CompatiblePSEditions" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $CompanyName = "Test CompanyName"
        $Copyright = "Test Copyright"
        $PowerShellHostVersion = "5.0"
        $ClrVersion = "1.0"
        $DotnetFrameworkVersion = "2.0"
        $PowerShellVersion = "5.1"
        $HelpInfoUri = "https://www.testhelpinfouri.com/"
        $CompatiblePSEditions = @("Desktop", "Core")

        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath `
                              -CompanyName $CompanyName `
                              -Copyright $Copyright `
                              -PowerShellVersion $PowerShellVersion `
                              -ClrVersion $ClrVersion `
                              -DotNetFrameworkVersion $DotnetFrameworkVersion `
                              -PowerShellHostVersion $PowerShellHostVersion `
                              -HelpInfoUri $HelpInfoUri `
                              -CompatiblePSEditions $CompatiblePSEditions

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.CompanyName | Should -Be $CompanyName 
        $results.Copyright | Should -Be $Copyright 
        $results.PowerShellVersion | Should -Be $PowerShellVersion 
        $results.ClrVersion | Should -Be $ClrVersion 
        $results.DotnetFrameworkVersion | Should -Be $DotnetFrameworkVersion 
        $results.PowerShellHostVersion | Should -Be $PowerShellHostVersion 
        $results.HelpInfoUri | Should -Be $HelpInfoUri 
        $results.CompatiblePSEditions | Should -Be $CompatiblePSEditions 
    }

    It "Update module manifest given FunctionsToExport, AliasesToExport, and VariablesToExport parameters" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $ExportedFunctions = "FunctionToExport1", "FunctionToExport2"
        $ExportedAliases = "AliasToExport1", "AliasToExport2"
        $ExportedVariables = "VariablesToExport1", "Variables2Export2"
        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath `
                              -FunctionsToExport $ExportedFunctions `
                              -AliasesToExport $ExportedAliases `
                              -VariablesToExport $ExportedVariables 

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.ExportedFunctions.Keys | Should -Be $ExportedFunctions
        $results.ExportedAliases.Keys | Should -Be $ExportedAliases
        $results.ExportedVariables.Keys | Should -Be $ExportedVariables
    }

    It "Update module manifest given CmdletsToExport parameters" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $CmdletToExport1 = "CmdletToExport1"
        $CmdletToExport2 = "CmdletToExport2"

        New-ModuleManifest -Path $script:testManifestPath -Description $Description
        Update-ModuleManifest -Path $script:testManifestPath -CmdletsToExport $CmdletToExport1, $CmdletToExport2

        $results = Get-Content -Path $script:testManifestPath -Raw
        $results.Contains($CmdletToExport1) | Should -Be $true
        $results.Contains($CmdletToExport2) | Should -Be $true
    }

    It "Update module manifest should not overwrite over old data unless explcitly specified" -Pending:(Get-IsWindows) {
        $Description = "Test Description"
        $ModuleVersion = "2.0.0"
        $Author = "Leto Atriedes"
        $ProjectUri = "https://www.arrakis.gov/"
        $Prerelease = "Preview"
        New-ModuleManifest -Path $script:testManifestPath -Description $Description -ModuleVersion $ModuleVersion -Author $Author -ProjectUri $ProjectUri
        Update-ModuleManifest -Path $script:testManifestPath -Prerelease $Prerelease

        $results = Test-ModuleManifest -Path $script:testManifestPath
        $results.Author | Should -Be $Author
        $results.PrivateData.PSData.ProjectUri | Should -Be $ProjectUri
        $results.PrivateData.PSData.Prerelease | Should -Be $Prerelease
    }
}

