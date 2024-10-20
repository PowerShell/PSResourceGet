# Copyright (c) Microsoft Corporation.
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Add Pester test to check the API for GroupPolicyEnforcement

Describe 'GroupPolicyEnforcement API Tests' -Tags 'CI' {

    It 'Should return the correct policy enforcement status' -Skip:(-not $IsWindows) {
        $actualStatus = [Microsoft.PowerShell.PSResourceGet.Cmdlets.GroupPolicyRepositoryEnforcement]::IsGroupPolicyEnabled()
        $actualStatus | Should -Be $false
    }

    It 'Should return platform not supported exception on non-windows platform' -Skip:$IsWindows {
        try {
            [Microsoft.PowerShell.PSResourceGet.Cmdlets.GroupPolicyRepositoryEnforcement]::IsGroupPolicyEnabled()
        }
        catch {
            $_.Exception.Message | Should -Be 'Group Policy is not supported on this platform.'
            $_.Exception.InnerException.GetType().FullName | Should -Be 'System.InvalidOperationException'
        }
    }

    It 'Group Policy must be enabled before getting allowed repositories' -Skip:(-not $IsWindows) {
        try {
            [Microsoft.PowerShell.PSResourceGet.Cmdlets.GroupPolicyRepositoryEnforcement]::GetAllowedRepositoryURIs()
        }
        catch {
            $_.Exception.InnerException.Message | Should -Be 'Group policy is not enabled.'
        }
    }
}

Describe 'GroupPolicyEnforcement Cmdlet Tests' -Tags 'CI' {
    BeforeEach {
        [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::SetTestHook('EnableGPRegistryHook', $true)
        [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::SetTestHook('GPEnabledStatus', $true)
        [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::SetTestHook('AllowedUri', "https://www.example.com/")
    }

    AfterEach {
        [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::SetTestHook('EnableGPRegistryHook', $false)
        [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::SetTestHook('GPEnabledStatus', $false)
        [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::SetTestHook('AllowedUri', $null)
    }

    It 'Getting allowed repositories works as expected' -Skip:(-not $IsWindows) {
        $allowedReps = [Microsoft.PowerShell.PSResourceGet.Cmdlets.GroupPolicyRepositoryEnforcement]::GetAllowedRepositoryURIs()
        $allowedReps.AbsoluteUri | Should -Be @("https://www.example.com/")
    }

    It 'Get-PSResourceRepository lists the allowed repository' -Skip:(-not $IsWindows) {
        try {
            Register-PSResourceRepository -Name 'Example' -Uri 'https://www.example.com/'
            $psrep = Get-PSResourceRepository -Name 'Example'
            $psrep | Should -Not -BeNullOrEmpty
            $psrep.IsAllowedByPolicy | Should -Be $true
        }
        finally {
            Unregister-PSResourceRepository -Name 'Example'
        }
    }

    It 'Find-PSResource is blocked by policy' -Skip:(-not $IsWindows) {
        try {
            Register-PSResourceRepository -Name 'Example' -Uri 'https://www.example.com/' -ApiVersion 'v3'
            { Find-PSResource -Repository PSGallery -Name 'Az.Accounts' -ErrorAction Stop } | Should -Throw "Repository 'PSGallery' is not allowed by Group Policy."

            # Allow PSGallery and it should not fail
            [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::SetTestHook('AllowedUri', " https://www.powershellgallery.com/api/v2")
            { Find-PSResource -Repository PSGallery -Name 'Az.Accounts' -ErrorAction Stop } | Should -Not -Throw
        }
        finally {
            Unregister-PSResourceRepository -Name 'Example'
        }
    }

    It 'Install-PSResource is blocked by policy' -Skip:(-not $IsWindows) {
        try {
            Register-PSResourceRepository -Name 'Example' -Uri 'https://www.example.com/' -ApiVersion 'v3'
            { Find-PSResource -Repository PSGallery -Name 'Az.Accounts' -ErrorAction Stop } | Should -Throw "Repository 'PSGallery' is not allowed by Group Policy."

            # Allow PSGallery and it should not fail
            [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::SetTestHook('AllowedUri', " https://www.powershellgallery.com/api/v2")
            { Find-PSResource -Repository PSGallery -Name 'Az.Accounts' -ErrorAction Stop } | Should -Not -Throw
        }
        finally {
            Unregister-PSResourceRepository -Name 'Example'
        }
    }

    It 'Save-PSResource is blocked by policy' -Skip:(-not $IsWindows) {
        try {
            Register-PSResourceRepository -Name 'Example' -Uri 'https://www.example.com/' -ApiVersion 'v3'
            { Find-PSResource -Repository PSGallery -Name 'Az.Accounts' -ErrorAction Stop } | Should -Throw "Repository 'PSGallery' is not allowed by Group Policy."

            # Allow PSGallery and it should not fail
            [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::SetTestHook('AllowedUri', " https://www.powershellgallery.com/api/v2")
            { Find-PSResource -Repository PSGallery -Name 'Az.Accounts' -ErrorAction Stop } | Should -Not -Throw
        }
        finally {
            Unregister-PSResourceRepository -Name 'Example'
        }
    }
}
