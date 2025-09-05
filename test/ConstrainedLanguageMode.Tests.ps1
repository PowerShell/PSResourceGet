# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# In future, we will add more tests to be executed in Constrained Language Mode.
Describe "Test UserAgentInfo" {
    It "GetUserString returns a non-null, non-empty string" {
        $userAgentString = [Microsoft.PowerShell.PSResourceGet.InternalHooks]::GetUserString()
        $userAgentString | Should -Not -BeNullOrEmpty
    }
}
