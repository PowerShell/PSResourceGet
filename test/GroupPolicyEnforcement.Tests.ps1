# Copyright (c) Microsoft Corporation.
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Add Pester test to check the API for GroupPolicyEnforcement

Describe 'GroupPolicyEnforcement API Tests' {
    BeforeAll {
        # Setup code if needed
    }

    AfterAll {
        # Cleanup code if needed
    }

    It 'Should return the correct policy enforcement status' {

        try {
            $expectedStatus = 'Disabled'
            $actualStatus = [Microsoft.PowerShell.PSResourceGet.Cmdlets.GroupPolicyRepositoryEnforcement]::IsGroupPolicyEnabled()
            $actualStatus | Should -Be $expectedStatus
        }
        finally {

        }

    }

    It 'Should throw an error for invalid policy name' {
        # Arrange
        $invalidPolicyName = 'InvalidPolicy'

        # Act & Assert
        { Get-GroupPolicyEnforcementStatus -PolicyName $invalidPolicyName } | Should -Throw
    }

    It 'Should return a list of all enforced policies' {
        # Act
        $policies = Get-AllEnforcedPolicies

        # Assert
        $policies | Should -Not -BeNullOrEmpty
        $policies | Should -BeOfType 'System.Collections.Generic.List[System.String]'
    }
}
