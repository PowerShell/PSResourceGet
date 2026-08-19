# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot/../PSGetTestUtils.psm1" -Force

Describe 'Test V3 packageContent url selection for a required version' -tags 'CI' {

    BeforeAll {
        $packageBaseAddress = 'https://api.nuget.org/v3-flatcontainer/test_module'
        # Responses are returned in descending version order, ie the entry for 1.2.30 precedes the entry for 1.2.3
        $versionedResponses = @(
            "$packageBaseAddress/1.2.30/test_module.1.2.30.nupkg",
            "$packageBaseAddress/1.2.3/test_module.1.2.3.nupkg"
        )
    }

    It 'Should select the url for the exact version requested' {
        $url = [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::SelectV3PackageContentUrl($versionedResponses, '1.2.3')
        $url | Should -BeExactly "$packageBaseAddress/1.2.3/test_module.1.2.3.nupkg"
    }

    It 'Should not select the url of a version which the requested version is a prefix of' {
        $url = [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::SelectV3PackageContentUrl($versionedResponses, '1.2.30')
        $url | Should -BeExactly "$packageBaseAddress/1.2.30/test_module.1.2.30.nupkg"
    }

    It 'Should select the url for a version with four version parts' {
        $responses = @(
            "$packageBaseAddress/2024.5.20.12/test_module.2024.5.20.12.nupkg",
            "$packageBaseAddress/2024.5.20.1/test_module.2024.5.20.1.nupkg"
        )
        $url = [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::SelectV3PackageContentUrl($responses, '2024.5.20.1')
        $url | Should -BeExactly "$packageBaseAddress/2024.5.20.1/test_module.2024.5.20.1.nupkg"
    }

    It 'Should select the url for a prerelease version' {
        $responses = @(
            "$packageBaseAddress/2.5.0-beta10/test_module.2.5.0-beta10.nupkg",
            "$packageBaseAddress/2.5.0-beta1/test_module.2.5.0-beta1.nupkg"
        )
        $url = [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::SelectV3PackageContentUrl($responses, '2.5.0-beta1')
        $url | Should -BeExactly "$packageBaseAddress/2.5.0-beta1/test_module.2.5.0-beta1.nupkg"
    }

    It 'Should select the url when the version is passed as a query parameter' {
        $responses = @(
            "https://www.myget.org/api/download?packageId=test_module&packageVersion=1.2.30",
            "https://www.myget.org/api/download?packageId=test_module&packageVersion=1.2.3"
        )
        $url = [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::SelectV3PackageContentUrl($responses, '1.2.3')
        $url | Should -BeExactly "https://www.myget.org/api/download?packageId=test_module&packageVersion=1.2.3"
    }

    It 'Should not select any url when the requested version is not present' {
        $url = [Microsoft.PowerShell.PSResourceGet.UtilClasses.TestHooks]::SelectV3PackageContentUrl($versionedResponses, '1.2.4')
        $url | Should -BeNullOrEmpty
    }
}
