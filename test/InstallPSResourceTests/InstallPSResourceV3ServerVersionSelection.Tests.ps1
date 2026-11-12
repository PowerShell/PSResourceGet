# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot/../PSGetTestUtils.psm1" -Force

Describe 'Test V3 packageContent url selection for a required version' -tags 'CI' {

    BeforeAll {
        $packageBaseAddress = 'https://api.nuget.org/v3-flatcontainer/test_module'

        # Registration entries returned by the V3 RegistrationsBaseUrl resource, containing the version in "catalogEntry" and the .nupkg url in "packageContent"
        function New-RegistrationEntry([string] $Version, [string] $PackageContent) {
            @{
                catalogEntry = @{ id = 'test_module'; version = $Version }
                packageContent = $PackageContent
            } | ConvertTo-Json -Compress
        }

        function Get-PackageContentUrl([string[]] $Entries, [string] $Version) {
            [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::SelectV3PackageContentUrl($Entries, $Version)
        }

        # Entries are in descending version order, ie the entry for 1.2.30 precedes the entry for 1.2.3
        $entries = @(
            (New-RegistrationEntry '1.2.30' "$packageBaseAddress/1.2.30/test_module.1.2.30.nupkg"),
            (New-RegistrationEntry '1.2.3' "$packageBaseAddress/1.2.3/test_module.1.2.3.nupkg")
        )
    }

    It 'Should select the url for the exact version requested' {
        Get-PackageContentUrl $entries '1.2.3' | Should -BeExactly "$packageBaseAddress/1.2.3/test_module.1.2.3.nupkg"
    }

    It 'Should select the url for a version which another version is a prefix of' {
        Get-PackageContentUrl $entries '1.2.30' | Should -BeExactly "$packageBaseAddress/1.2.30/test_module.1.2.30.nupkg"
    }

    It 'Should select the url for a version with four version parts' {
        $fourPartEntries = @(
            (New-RegistrationEntry '2024.5.20.12' "$packageBaseAddress/2024.5.20.12/test_module.2024.5.20.12.nupkg"),
            (New-RegistrationEntry '2024.5.20.1' "$packageBaseAddress/2024.5.20.1/test_module.2024.5.20.1.nupkg")
        )
        Get-PackageContentUrl $fourPartEntries '2024.5.20.1' | Should -BeExactly "$packageBaseAddress/2024.5.20.1/test_module.2024.5.20.1.nupkg"
    }

    It 'Should select the url for a prerelease version' {
        $prereleaseEntries = @(
            (New-RegistrationEntry '2.5.0-beta10' "$packageBaseAddress/2.5.0-beta10/test_module.2.5.0-beta10.nupkg"),
            (New-RegistrationEntry '2.5.0-beta1' "$packageBaseAddress/2.5.0-beta1/test_module.2.5.0-beta1.nupkg")
        )
        Get-PackageContentUrl $prereleaseEntries '2.5.0-beta1' | Should -BeExactly "$packageBaseAddress/2.5.0-beta1/test_module.2.5.0-beta1.nupkg"
    }

    It 'Should compare versions rather than version text' {
        $nonNormalizedEntries = @(
            (New-RegistrationEntry '2.5.0-Beta1+build.5' "$packageBaseAddress/2.5.0-beta1/test_module.2.5.0-beta1.nupkg"),
            (New-RegistrationEntry '1.2.3.0' "$packageBaseAddress/1.2.3/test_module.1.2.3.nupkg")
        )
        Get-PackageContentUrl $nonNormalizedEntries '2.5.0-beta1' | Should -BeExactly "$packageBaseAddress/2.5.0-beta1/test_module.2.5.0-beta1.nupkg"
        Get-PackageContentUrl $nonNormalizedEntries '1.2.3' | Should -BeExactly "$packageBaseAddress/1.2.3/test_module.1.2.3.nupkg"
    }

    It 'Should not select a url which contains the requested version text but is for another version' {
        $misleadingEntries = @(
            (New-RegistrationEntry '1.2.30' "$packageBaseAddress/1.2.30/test_module.1.2.30.nupkg?ref=1.2.3"),
            (New-RegistrationEntry '1.2.3' "$packageBaseAddress/1.2.3/test_module.1.2.3.nupkg?ref=1.2.3")
        )
        Get-PackageContentUrl $misleadingEntries '1.2.3' | Should -BeExactly "$packageBaseAddress/1.2.3/test_module.1.2.3.nupkg?ref=1.2.3"
    }

    It 'Should select the url when it does not contain the version' {
        $opaqueEntries = @(
            (New-RegistrationEntry '1.2.30' 'https://api.nuget.org/v3-flatcontainer/download/a1b2c3'),
            (New-RegistrationEntry '1.2.3' 'https://api.nuget.org/v3-flatcontainer/download/d4e5f6')
        )
        Get-PackageContentUrl $opaqueEntries '1.2.3' | Should -BeExactly 'https://api.nuget.org/v3-flatcontainer/download/d4e5f6'
    }

    It 'Should skip entries without a version or url' {
        $incompleteEntries = @(
            '{"catalogEntry":{"id":"test_module"},"packageContent":"https://api.nuget.org/v3-flatcontainer/test_module/1.2.3/a.nupkg"}',
            '{"catalogEntry":{"id":"test_module","version":"1.2.3"}}',
            'not json',
            (New-RegistrationEntry '1.2.3' "$packageBaseAddress/1.2.3/test_module.1.2.3.nupkg")
        )
        Get-PackageContentUrl $incompleteEntries '1.2.3' | Should -BeExactly "$packageBaseAddress/1.2.3/test_module.1.2.3.nupkg"
    }

    It 'Should select the url for the latest version when no version is requested, regardless of entry order' {
        $ascendingEntries = @(
            (New-RegistrationEntry '1.2.3' "$packageBaseAddress/1.2.3/test_module.1.2.3.nupkg"),
            (New-RegistrationEntry '1.2.30' "$packageBaseAddress/1.2.30/test_module.1.2.30.nupkg"),
            (New-RegistrationEntry '1.2.4' "$packageBaseAddress/1.2.4/test_module.1.2.4.nupkg")
        )
        Get-PackageContentUrl $ascendingEntries $null | Should -BeExactly "$packageBaseAddress/1.2.30/test_module.1.2.30.nupkg"
    }

    It 'Should not select any url when the requested version is not present' {
        Get-PackageContentUrl $entries '1.2.4' | Should -BeNullOrEmpty
    }
}
