# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Read PSGetModuleInfo xml file" -tags 'CI' {

    BeforeAll {
        $fileToRead = Join-Path -Path $PSScriptRoot -ChildPath "PSGetModuleInfo.xml"
    }

    It "Verifies expected error with null path" {
        { [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::ReadPSGetResourceInfo($null) } | Should -Throw -ErrorId 'PSInvalidOperationException'
    }

    It "Verifies expected error with invalid file path" {
        { [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::ReadPSGetResourceInfo('nonePath') } | Should -Throw -ErrorId 'PSInvalidOperationException'
    }

    It "Verifies PSGetModuleInfo.xml file is read successfully" {
        $psGetInfo = [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::ReadPSGetResourceInfo($fileToRead)
        CheckForExpectedPSGetInfo $psGetInfo
    }
}

Describe "Write PSGetModuleInfo xml file" -tags 'CI' {

    BeforeAll {
        $fileToRead = Join-Path -Path $PSScriptRoot -ChildPath "PSGetModuleInfo.xml"
        $fileToWrite = Join-Path -Path $TestDrive -ChildPath "PSGetModuleInfo_Write.xml"
    }

    It "Verifies expected error with null path" {
        $psGetInfo = [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::ReadPSGetResourceInfo($fileToRead)
        { [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::WritePSGetResourceInfo($null, $psGetInfo) } | Should -Throw -ErrorId 'PSInvalidOperationException'
    }

    It "Verifies file write is successful" {
        $psGetInfo = [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::ReadPSGetResourceInfo($fileToRead)
        { [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::WritePSGetResourceInfo($fileToWrite, $psGetInfo) } | Should -Not -Throw
    }

    It "Verifies written file can be read successfully" {
        $newGetInfo = [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::ReadPSGetResourceInfo($fileToWrite)
        CheckForExpectedPSGetInfo $newGetInfo
    }
}

Describe "Convert local module manifest dependencies" -tags 'CI' {
    $testCases = @(
        @{
            Shape = 'a single hashtable with ModuleVersion'
            RequiredModules = @{ ModuleName = 'Test.Dependency'; ModuleVersion = '2.0.0' }
            ExpectedNames = @('Test.Dependency')
            ExpectedRanges = @('[2.0.0, )')
        },
        @{
            Shape = 'a single hashtable with RequiredVersion'
            RequiredModules = @{ ModuleName = 'Test.Dependency'; RequiredVersion = '2.0.0' }
            ExpectedNames = @('Test.Dependency')
            ExpectedRanges = @('[2.0.0, 2.0.0]')
        },
        @{
            Shape = 'a single hashtable with MaximumVersion'
            RequiredModules = @{ ModuleName = 'Test.Dependency'; MaximumVersion = '2.0.0' }
            ExpectedNames = @('Test.Dependency')
            ExpectedRanges = @('(, 2.0.0]')
        },
        @{
            Shape = 'a single hashtable with a version range'
            RequiredModules = @{ ModuleName = 'Test.Dependency'; ModuleVersion = '1.0.0'; MaximumVersion = '2.0.0' }
            ExpectedNames = @('Test.Dependency')
            ExpectedRanges = @('[1.0.0, 2.0.0]')
        },
        @{
            Shape = 'a single module name'
            RequiredModules = 'Test.Dependency'
            ExpectedNames = @('Test.Dependency')
            ExpectedRanges = @('(, )')
        },
        @{
            Shape = 'an array containing a hashtable'
            RequiredModules = @(@{ ModuleName = 'Test.Dependency'; ModuleVersion = '2.0.0' })
            ExpectedNames = @('Test.Dependency')
            ExpectedRanges = @('[2.0.0, )')
        },
        @{
            Shape = 'an array of module names'
            RequiredModules = @('Test.Dependency', 'Test.OtherDependency')
            ExpectedNames = @('Test.Dependency', 'Test.OtherDependency')
            ExpectedRanges = @('(, )', '(, )')
        },
        @{
            Shape = 'an array mixing module names and hashtables'
            RequiredModules = @('Test.Dependency', @{ ModuleName = 'Test.OtherDependency'; ModuleVersion = '2.0.0' })
            ExpectedNames = @('Test.Dependency', 'Test.OtherDependency')
            ExpectedRanges = @('(, )', '[2.0.0, )')
        },
        @{
            Shape = 'an empty array'
            RequiredModules = @()
            ExpectedNames = @()
            ExpectedRanges = @()
        },
        @{
            Shape = 'null'
            RequiredModules = $null
            ExpectedNames = @()
            ExpectedRanges = @()
        },
        @{
            Shape = 'an array containing null'
            RequiredModules = @($null, 'Test.Dependency')
            ExpectedNames = @('Test.Dependency')
            ExpectedRanges = @('(, )')
        },
        @{
            Shape = 'an omitted RequiredModules entry'
            OmitRequiredModules = $true
            ExpectedNames = @()
            ExpectedRanges = @()
        }
    )

    It "converts RequiredModules specified as <Shape>" -TestCases $testCases {
        param($Shape, $RequiredModules, $ExpectedNames, $ExpectedRanges, $OmitRequiredModules)

        $repository = [Microsoft.PowerShell.PSResourceGet.UtilClasses.PSRepositoryInfo]::new(
            'TestRepository', [uri][string]$TestDrive, 50, $true, $null, 'None', 'Local', $true)
        $metadata = @{
            Id = 'Test.Module'
            ModuleVersion = '1.0.0'
            Tags = [string[]]@()
        }
        if (-not $OmitRequiredModules) {
            $metadata['RequiredModules'] = $RequiredModules
        }

        $resource = $null
        $errorMessage = $null
        $success = [Microsoft.PowerShell.PSResourceGet.UtilClasses.PSResourceInfo]::TryConvertFromHashtableForPsd1(
            $metadata, [ref]$resource, [ref]$errorMessage, $repository)

        $success | Should -BeTrue -Because "$Shape is a supported manifest form. $errorMessage"
        $errorMessage | Should -BeNullOrEmpty
        $resource.Name | Should -BeExactly 'Test.Module'
        $resource.Dependencies | Should -HaveCount $ExpectedNames.Count
        for ($i = 0; $i -lt $ExpectedNames.Count; $i++) {
            $resource.Dependencies[$i].Name | Should -BeExactly $ExpectedNames[$i]
            $resource.Dependencies[$i].VersionRange.ToString() | Should -BeExactly $ExpectedRanges[$i]
        }
    }
}
