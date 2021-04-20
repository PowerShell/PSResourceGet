# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$psGetMod = Get-Module -Name PowerShellGet
if ((! $psGetMod) -or (($psGetMod | Select-Object Version) -lt 3.0.0))
{
    Write-Verbose -Message "Importing PowerShellGet 3.0.0 for test" -Verbose
    Import-Module -Name PowerShellGet -MinimumVersion 3.0.0 -Force
}

Describe "Read PSGetModuleInfo xml file" -tags CI {

    It "Verifies expected error with null path" {
        { [Microsoft.PowerShell.PowerShellGet.UtilClasses.TestHooks]::ReadPSGetInfo($null) } | Should -Throw -ErrorId 'PSInvalidOperationException'
    }

    It "Verifies expected error with invalid file path" {
        { [Microsoft.PowerShell.PowerShellGet.UtilClasses.TestHooks]::ReadPSGetInfo('nonePath') } | Should -Throw -ErrorId 'PSInvalidOperationException'
    }

    It "Verifies PSGetModuleInfo.xml file is read successfully" {
        $fileToRead = Join-Path -Path $PSScriptRoot -ChildPath "PSGetModuleInfo.xml"
        $psGetInfo = [Microsoft.PowerShell.PowerShellGet.UtilClasses.TestHooks]::ReadPSGetInfo($fileToRead)
        #
        $psGetInfo.AdditionalMetadata.Keys | Should -HaveCount 22
        $psGetInfo.AdditionalMetadata['copyright'] | Should -BeExactly '(c) Microsoft Corporation. All rights reserved.'
        $psGetInfo.AdditionalMetadata['tags'] | Should -BeLike 'PSModule PSEdition_Core*'
        $psGetInfo.AdditionalMetadata['ItemType'] | Should -BeExactly 'Module'
        #
        $psGetInfo.Author | Should -BeExactly 'Microsoft Corporation'
        $psGetInfo.CompanyName | Should -BeExactly 'Microsoft Corporation'
        $psGetInfo.Copyright | Should -BeExactly '(c) Microsoft Corporation. All rights reserved.'
        $psGetInfo.Dependencies | Should -HaveCount 0
        $psGetInfo.Description | Should -BeLike 'This module provides a convenient way for a user to store*'
        $psGetInfo.IconUri | Should -BeNullOrEmpty
        $psGetInfo.Includes.Cmdlet | Should -HaveCount 10
        $psGetInfo.Includes.Cmdlet[0] | Should -BeExactly 'Register-SecretVault'
        $psGetInfo.InstalledDate.Ticks | Should -BeExactly 637522675617662015
        $psGetInfo.InstalledLocation | Should -BeLike 'C:\Users\*'
        $psGetInfo.LicenseUri | Should -BeExactly 'https://github.com/PowerShell/SecretManagement/blob/master/LICENSE'
        $psGetInfo.Name | Should -BeExactly 'Microsoft.PowerShell.SecretManagement'
        $psGetInfo.PackageManagementProvider | Should -BeExactly 'NuGet'
        $psGetInfo.PowerShellGetFormatVersion | Should -BeNullOrEmpty
        $psGetInfo.ProjectUri | Should -BeExactly 'https://github.com/powershell/secretmanagement'
        $psGetInfo.PublishedDate.Ticks | Should -BeExactly 637522924900000000
        $psGetInfo.ReleasedNotes | Should -BeNullOrEmpty
        $psGetInfo.Repository | Should -BeExactly 'PSGallery'
        $psGetInfo.RepositorySourceLocation | Should -BeExactly 'https://www.powershellgallery.com/api/v2'
        $psGetInfo.Tags | Should -BeExactly @('PSModule', 'PSEdition_Core')
        $psGetInfo.Type | Should -BeExactly 'Module'
        $psGetInfo.UpdatedDate.Ticks | Should -BeExactly 0
        $psGetInfo.Version.ToString() | Should -BeExactly '1.0.0'
    }
}
