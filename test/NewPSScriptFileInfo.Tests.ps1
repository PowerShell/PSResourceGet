# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test New-PSScriptFileInfo" {
    BeforeAll {
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDirPaths = @($tmpDir1Path)
        Get-NewTestDirs($tmpDirPaths)
    }
    BeforeEach {
        $script:PSScriptInfoName = "test_script"
        $script:testScriptFilePath = Join-Path -Path $tmpDir1Path -ChildPath "$script:PSScriptInfoName.ps1"
    }
    AfterEach {
        if (Test-Path -Path $script:testScriptFilePath)
        {
            Remove-Item $script:testScriptFilePath
        }
    }

    It "Create .ps1 file with minimal required fields" {    
        $description = "Test description"
        New-PSScriptFileInfo -Path  $script:testScriptFilePath -Description $description
        Test-PSScriptFileInfo -Path $script:testScriptFilePath | Should -BeTrue
    }

    It "Create .ps1 file with relative path" {
        $relativeCurrentPath = Get-Location
        $scriptFilePath = Join-Path -Path $relativeCurrentPath -ChildPath "$script:PSScriptInfoName.ps1"

        $description = "Test description"
        New-PSScriptFileInfo -Path $scriptFilePath -Description $description

        Test-PSScriptFileInfo -Path $scriptFilePath | Should -BeTrue
        Remove-Item -Path $scriptFilePath
    }

    It "Create new .ps1 given Version parameter" {
        $version =  "2.0.0.0"
        $description = "Test description"

        New-PSScriptFileInfo -Path $script:testScriptFilePath -Version $version -Description $description

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($version) | Should -BeTrue
        $results.Contains(".VERSION $version") | Should -BeTrue
    }

    It "Create new .ps1 given Guid parameter" {
        $guid = [guid]::NewGuid()
        $description = "Test description"

        New-PSScriptFileInfo -Path  $script:testScriptFilePath -Guid $guid -Description $description

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($guid) | Should -BeTrue
        $results.Contains(".GUID $guid") | Should -BeTrue
    }

    It "Create new .ps1 given Author parameter" {
        $author = "Test Author" 
        $description = "Test description"

        New-PSScriptFileInfo -Path  $script:testScriptFilePath -Author $author -Description $description

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($author) | Should -BeTrue
        $results.Contains(".AUTHOR $author") | Should -BeTrue
    }

    It "Create new .ps1 given Description parameter" {
        $description = "PowerShellGet test description"

        New-PSScriptFileInfo -Path $script:testScriptFilePath -Description $description

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($description) | Should -BeTrue
        $results -like "*.DESCRIPTION$script:newline*$description*" | Should -BeTrue
    }

    It "Create new .ps1 given CompanyName parameter" {
        $companyName =  "Microsoft"
        $description = "Test description"

        New-PSScriptFileInfo -Path $script:testScriptFilePath -CompanyName $companyName -Description $description

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($companyName) | Should -BeTrue
        $results.Contains(".COMPANYNAME $companyname") | Should -BeTrue
    }

    It "Create new .ps1 given Copyright parameter" {
        $copyright =  "(c) Test Corporation"
        $description = "Test description"

        New-PSScriptFileInfo -Path $script:testScriptFilePath -Copyright $copyright -Description $description

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($copyright) | Should -BeTrue
        $results.Contains(".COPYRIGHT $copyright") | Should -BeTrue
    }

    It "Create new .ps1 given RequiredModules parameter" {
        $requiredModuleName = 'PackageManagement'
        $requiredModuleVersion = '1.0.0.0'
        $RequiredModules =  @(@{ModuleName = $requiredModuleName; ModuleVersion = $requiredModuleVersion })

        $description = "Test description"        

        New-PSScriptFileInfo -Path $script:testScriptFilePath -RequiredModules $RequiredModules -Description $Description 

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($requiredModuleName) | Should -BeTrue
        $results.Contains($requiredModuleVersion) | Should -BeTrue
        $results -like "*#Requires*$requiredModuleName*$requiredModuleVersion*" | Should -BeTrue
    }

    It "Create new .ps1 given ReleaseNotes parameter" {
        $description = "Test Description"
        $releaseNotes = "Release notes for script."

        New-PSScriptFileInfo -Path $script:testScriptFilePath -ReleaseNotes $releaseNotes -Description $description 

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($releaseNotes) | Should -BeTrue
        $results -like "*.RELEASENOTES$script:newline*$ReleaseNotes*" | Should -BeTrue
    }

    It "Create new .ps1 given Tags parameter" {
        $description = "Test Description"
        $tag1 = "tag1"
        $tag2 = "tag2"

        New-PSScriptFileInfo -Path $script:testScriptFilePath -Tags $tag1, $tag2 -Description $description 

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($tag1) | Should -BeTrue
        $results.Contains($tag2) | Should -BeTrue
        $results.Contains(".TAGS $tag1 $tag2") | Should -BeTrue
    }

    It "Create new .ps1 given ProjectUri parameter" {
        $description = "Test Description"
        $projectUri = "https://www.testprojecturi.com/"

        New-PSScriptFileInfo -Path $script:testScriptFilePath -ProjectUri $projectUri -Description $description 

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($projectUri) | Should -BeTrue
        $results.Contains(".PROJECTURI $projectUri") | Should -BeTrue
    }

    It "Create new .ps1 given LicenseUri parameter" {
        $description = "Test Description"
        $licenseUri = "https://www.testlicenseuri.com/"

        New-PSScriptFileInfo -Path $script:testScriptFilePath -LicenseUri $licenseUri -Description $description 

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($licenseUri) | Should -BeTrue
        $results.Contains(".LICENSEURI $licenseUri") | Should -BeTrue
    }

    It "Create new .ps1 given IconUri parameter" {
        $description = "Test Description"
        $iconUri = "https://www.testiconuri.com/"

        New-PSScriptFileInfo -Path $script:testScriptFilePath -IconUri $iconUri -Description $description 

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($iconUri) | Should -BeTrue
        $results.Contains(".ICONURI $iconUri") | Should -BeTrue
    }

    It "Create new .ps1 given ExternalModuleDependencies parameter" {
        $description = "Test Description"
        $externalModuleDep1 = "ExternalModuleDep1"
        $externalModuleDep2 = "ExternalModuleDep2"

        New-PSScriptFileInfo -Path $script:testScriptFilePath -ExternalModuleDependencies $externalModuleDep1, $externalModuleDep2 -Description $description

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($externalModuleDep1) | Should -BeTrue
        $results.Contains($externalModuleDep2) | Should -BeTrue
        $results -like "*.EXTERNALMODULEDEPENDENCIES*$externalModuleDep1*$externalModuleDep2*" | Should -BeTrue
    }

    It "Create new .ps1 given RequiredScripts parameter" {
        $description = "Test Description"
        $requiredScript1 = "RequiredScript1"
        $requiredScript2 = "RequiredScript2"

        New-PSScriptFileInfo -Path $script:testScriptFilePath -RequiredScripts $requiredScript1, $requiredScript2 -Description $description

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($requiredScript1) | Should -BeTrue
        $results.Contains($requiredScript2) | Should -BeTrue
        $results -like "*.REQUIREDSCRIPTS*$requiredScript1*$requiredScript2*" | Should -BeTrue
    }

    It "Create new .ps1 given ExternalScriptDependencies parameter" {
        $description = "Test Description"
        $externalScriptDep1 = "ExternalScriptDep1"
        $externalScriptDep2 = "ExternalScriptDep2"

        New-PSScriptFileInfo -Path $script:testScriptFilePath -ExternalScriptDependencies $externalScriptDep1, $externalScriptDep2 -Description $description

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($externalScriptDep1) | Should -BeTrue
        $results.Contains($externalScriptDep2) | Should -BeTrue
        $results -like "*.EXTERNALSCRIPTDEPENDENCIES*$externalScriptDep1*$externalScriptDep2*" | Should -BeTrue
    }

    It "Create new .ps1 given PrivateData parameter" {
        $description = "Test Description"
        $privateData = @{"PrivateDataEntry1" = "PrivateDataValue1"}
        New-PSScriptFileInfo -Path $script:testScriptFilePath -PrivateData $privateData -Description $description 

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($privateData) | Should -BeTrue
        $results -like "*.PRIVATEDATA*$privateData*" | Should -BeTrue
    }
}
