# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$testDir = (get-item $psscriptroot).parent.FullName

Describe "Test Update-PSScriptFileInfo" -tags 'CI' {
    BeforeAll {
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDirPaths = @($tmpDir1Path)
        Get-NewTestDirs($tmpDirPaths)

        # Path to folder, within our test folder, where we store invalid module and script files used for testing
        $script:testFilesFolderPath = Join-Path $testDir -ChildPath "testFiles"

        # Path to specifically to that invalid test scripts folder
        $script:testScriptsFolderPath = Join-Path $testFilesFolderPath -ChildPath "testScripts"

        $script:newline = [System.Environment]::NewLine;
    }

    BeforeEach {
        $script:psScriptInfoName = "test_script"
        $scriptDescription = "this is a test script"
        $script:testScriptFilePath = Join-Path -Path $tmpDir1Path -ChildPath "$script:psScriptInfoName.ps1"
        New-PSScriptFileInfo -Path $script:testScriptFilePath -Description $scriptDescription
    }

    AfterEach {
        if (Test-Path -Path $script:testScriptFilePath)
        {
            Remove-Item $script:testScriptFilePath
        }
    }

    It "Update .ps1 file with relative path" {
        $relativeCurrentPath = Get-Location
        $scriptFilePath = Join-Path -Path $relativeCurrentPath -ChildPath "$script:psScriptInfoName.ps1"
        $oldDescription = "Old description for test script"
        $newDescription = "New description for test script"
        New-PSScriptFileInfo -Path $scriptFilePath -Description $oldDescription

        Update-PSScriptFileInfo -Path $scriptFilePath -Description $newDescription
        Test-PSScriptFileInfo -Path $scriptFilePath | Should -BeTrue

        Test-Path -Path $scriptFilePath  | Should -BeTrue
        $results = Get-Content -Path $scriptFilePath -Raw
        $results.Contains($newDescription) | Should -BeTrue
        $results -like "*.DESCRIPTION$script:newline*$newDescription*" | Should -BeTrue

        Remove-Item -Path $scriptFilePath -Force
    }

    It "Update script should not overwrite old script data unless that property is specified" {
        $description = "Test Description"
        $version = "3.0.0"
        $author = "John Doe"
        $newAuthor = "Jane Doe"
        $projectUri = "https://testscript.com/"

        $relativeCurrentPath = Get-Location
        $scriptFilePath = Join-Path -Path $relativeCurrentPath -ChildPath "$script:psScriptInfoName.ps1"

        New-PSScriptFileInfo -Path $scriptFilePath -Description $description -Version $version -Author $author -ProjectUri $projectUri
        Update-PSScriptFileInfo -Path $scriptFilePath -Author $newAuthor

        Test-PSScriptFileInfo -Path $scriptFilePath | Should -BeTrue
        $results = Get-Content -Path $scriptFilePath -Raw
        $results.Contains($newAuthor) | Should -BeTrue
        $results.Contains(".AUTHOR $newAuthor") | Should -BeTrue

        # rest should be original data used when creating the script
        $results.Contains($projectUri) | Should -BeTrue
        $results.Contains(".PROJECTURI $projectUri") | Should -BeTrue

        $results.Contains($version) | Should -BeTrue
        $results.Contains(".VERSION $version") | Should -BeTrue

        $results.Contains($description) | Should -BeTrue
        $results -like "*.DESCRIPTION$script:newline*$description*" | Should -BeTrue

        Remove-Item -Path $scriptFilePath -Force
    }

    It "update script file Author property" {
        $author = "New Author"
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -Author $author
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath  | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath  -Raw
        $results.Contains($author) | Should -BeTrue
        $results.Contains(".AUTHOR $author") | Should -BeTrue
    }

    It "update script file Version property" {
        $version = "2.0.0.0"
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -Version $version
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath  | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath  -Raw
        $results.Contains($version) | Should -BeTrue
        $results.Contains(".VERSION $version") | Should -BeTrue
    }

    It "update script file Version property with prerelease version" {
        $version = "3.0.0-alpha"
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -Version $version
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath  | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath  -Raw
        $results.Contains($version) | Should -BeTrue
        $results.Contains(".VERSION $version") | Should -BeTrue
    }

    It "not update script file with invalid version" {
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -Version "4.0.0.0.0" -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "VersionParseIntoNuGetVersion,Microsoft.PowerShell.PSResourceGet.Cmdlets.UpdatePSScriptFileInfo"
    }

    It "update script file Description property" {
        $description = "this is an updated test script"
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -Description $description
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath  | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath  -Raw
        $results.Contains($description) | Should -BeTrue
        $results -like "*.DESCRIPTION$script:newline*$description*" | Should -BeTrue
    }

    It "update script file Guid property" {
        $guid = [Guid]::NewGuid();
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -Guid $guid
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath  | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath  -Raw
        $results.Contains($guid) | Should -BeTrue
        $results.Contains(".GUID $guid") | Should -BeTrue
    }

    It "update script file CompanyName property" {
        $companyName = "New Corporation"
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -CompanyName $companyName
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath  | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath  -Raw
        $results.Contains($companyName) | Should -BeTrue
        $results.Contains(".COMPANYNAME $companyName") | Should -BeTrue
    }

    It "update script file Copyright property" {
        $copyright = "(c) 2022 New Corporation. All rights reserved"
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -Copyright $copyright
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath  | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath  -Raw
        $results.Contains($copyright) | Should -BeTrue
        $results.Contains(".COPYRIGHT $copyright") | Should -BeTrue
    }

    It "update script file ExternalModuleDependencies property" {
        $externalModuleDep1 = "ExternalModuleDep1"
        $externalModuleDep2 = "ExternalModuleDep2"
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -ExternalModuleDependencies $externalModuleDep1,$externalModuleDep2
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath  | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath  -Raw
        $results.Contains($externalModuleDep1) | Should -BeTrue
        $results.Contains($externalModuleDep2) | Should -BeTrue
        $results -like "*.EXTERNALMODULEDEPENDENCIES*$externalModuleDep1*$externalModuleDep2*" | Should -BeTrue
    }

    It "update script file ExternalScriptDependencies property" {
        $externalScriptDep1 = "ExternalScriptDep1"
        $externalScriptDep2 = "ExternalScriptDep2"
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -ExternalScriptDependencies $externalScriptDep1,$externalScriptDep2
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($externalScriptDep1) | Should -BeTrue
        $results.Contains($externalScriptDep2) | Should -BeTrue
        $results -like "*.EXTERNALMODULEDEPENDENCIES*$externalScriptDep1*$externalScriptDep2*" | Should -BeTrue
    }

    It "update script file IconUri property" {
        $iconUri = "https://testscript.com/icon"
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -IconUri $iconUri
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath  | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath  -Raw
        $results.Contains($iconUri) | Should -BeTrue
        $results.Contains(".ICONURI $iconUri") | Should -BeTrue
    }

    It "update script file LicenseUri property" {
        $licenseUri = "https://testscript.com/license"
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -LicenseUri $licenseUri
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath  | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath  -Raw
        $results.Contains($licenseUri) | Should -BeTrue
        $results.Contains(".LICENSEURI $licenseUri") | Should -BeTrue
    }

    It "update script file ProjectUri property" {
        $projectUri = "https://testscript.com/"
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -ProjectUri $projectUri
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath  | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath  -Raw
        $results.Contains($projectUri) | Should -BeTrue
        $results.Contains(".PROJECTURI $projectUri") | Should -BeTrue
    }

    It "update script file PrivateData property" {
        $privateData = "this is some private data"
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -PrivateData $privateData
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath  | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath  -Raw
        $results.Contains($privateData) | Should -BeTrue
        $results -like "*.PRIVATEDATA*$privateData*" | Should -BeTrue
    }

    It "update script file ReleaseNotes property" {
        $releaseNotes = "Release notes for script."
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -ReleaseNotes $releaseNotes
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($releaseNotes) | Should -BeTrue
        $results -like "*.RELEASENOTES$script:newline*$releaseNotes*" | Should -BeTrue
    }

    It "update script file RequiredModules property" {
        $hashtable1 = @{ModuleName = "RequiredModule1"}
        $hashtable2 = @{ModuleName = "RequiredModule2"; ModuleVersion = "1.0.0.0"}
        $hashtable3 = @{ModuleName = "RequiredModule3"; RequiredVersion = "2.5.0.0"}
        $hashtable4 = @{ModuleName = "RequiredModule4"; ModuleVersion = "1.1.0.0"; MaximumVersion = "2.0.0.0"}
        $requiredModules = $hashtable1, $hashtable2, $hashtable3, $hashtable4

        Update-PSScriptFileInfo -Path $script:testScriptFilePath -RequiredModules $requiredModules
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains("#Requires -Module RequiredModule1") | Should -BeTrue
        $results -like "*#Requires*ModuleName*Version*" | Should -BeTrue
    }

    It "update script file RequiredScripts property" {
        $requiredScript1 = "RequiredScript1"
        $requiredScript2 = "RequiredScript2"
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -RequiredScripts $requiredScript1, $requiredScript2
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($requiredScript1) | Should -BeTrue
        $results.Contains($requiredScript2) | Should -BeTrue
        $results -like "*.REQUIREDSCRIPTS*$requiredScript1*$requiredScript2*" | Should -BeTrue
    }

    It "update script file Tags property" {
        $tag1 = "tag1"
        $tag2 = "tag2"
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -Tags $tag1, $tag2
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($tag1) | Should -BeTrue
        $results.Contains($tag2) | Should -BeTrue
        $results.Contains(".TAGS $tag1 $tag2") | Should -BeTrue
    }

    It "update script file Tag property" {
        $tag1 = "tag1"
        $tag2 = "tag2"
        Update-PSScriptFileInfo -Path $script:testScriptFilePath -Tag $tag1, $tag2
        Test-PSScriptFileInfo $script:testScriptFilePath | Should -Be $true

        Test-Path -Path $script:testScriptFilePath | Should -BeTrue
        $results = Get-Content -Path $script:testScriptFilePath -Raw
        $results.Contains($tag1) | Should -BeTrue
        $results.Contains($tag2) | Should -BeTrue
        $results.Contains(".TAGS $tag1 $tag2") | Should -BeTrue
    }

    It "throw error when attempting to update a signed script without -RemoveSignature parameter" {
        # Note: user should sign the script again once it's been updated

        $scriptName = "ScriptWithSignature.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        # use a copy of the signed script file so we can re-use for other tests
        $null = Copy-Item -Path $scriptFilePath -Destination $TestDrive
        $tmpScriptFilePath = Join-Path -Path $TestDrive -ChildPath $scriptName

        { Update-PSScriptFileInfo -Path $tmpScriptFilePath -Version "2.0.0.0" } | Should -Throw -ErrorId "ScriptToBeUpdatedContainsSignature,Microsoft.PowerShell.PSResourceGet.Cmdlets.UpdatePSScriptFileInfo"
    }

    It "update signed script when using RemoveSignature parameter" {
        $scriptName = "ScriptWithSignature.ps1"
        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName

        # use a copy of the signed script file so we can re-use for other tests
        $null = Copy-Item -Path $scriptFilePath -Destination $TestDrive
        $tmpScriptFilePath = Join-Path -Path $TestDrive -ChildPath $scriptName

        Update-PSScriptFileInfo -Path $tmpScriptFilePath -Version "2.0.0.0" -RemoveSignature
        Test-PSScriptFileInfo -Path $tmpScriptFilePath | Should -Be $true
    }
}
