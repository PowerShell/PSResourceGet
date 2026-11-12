# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
Param()

$ProgressPreference = 'SilentlyContinue'
$modPath = "$PSScriptRoot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

Describe 'RuntimeIdentifierHelper' -Tags 'CI' {
    BeforeAll {
        $InternalHooks = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]
    }

    Context 'Current runtime detection' {
        It 'returns a stable RID for the current process' {
            $firstRid = $InternalHooks::GetCurrentRuntimeIdentifier()
            $secondRid = $InternalHooks::GetCurrentRuntimeIdentifier()

            $firstRid | Should -Not -BeNullOrEmpty
            $firstRid | Should -Be $secondRid
            $firstRid | Should -Match '-(x64|x86|arm64|arm|s390x|ppc64le|loongarch64)$'
        }

        It 'returns the expected operating system prefix' {
            $rid = $InternalHooks::GetCurrentRuntimeIdentifier()

            if ($PSVersionTable.PSVersion.Major -eq 5 -or $IsWindows) {
                $rid | Should -Match '^win-'
            }
            elseif ($IsLinux) {
                $rid | Should -Match '^linux(-musl)?-'
            }
            elseif ($IsMacOS) {
                $rid | Should -Match '^osx-'
            }
        }
    }

    Context 'Compatibility chains' {
        It 'builds the Windows portable fallback chain' {
            $rids = $InternalHooks::GetCompatibleRuntimeIdentifiersFor('win10-x64')

            $rids[0] | Should -Be 'win10-x64'
            $rids | Should -Contain 'win-x64'
            $rids | Should -Contain 'win'
            $rids | Should -Contain 'any'
        }

        It 'builds the Linux musl fallback chain' {
            $rids = $InternalHooks::GetCompatibleRuntimeIdentifiersFor('linux-musl-x64')

            $rids[0] | Should -Be 'linux-musl-x64'
            $rids | Should -Contain 'linux-x64'
            $rids | Should -Contain 'linux'
            $rids | Should -Contain 'unix'
            $rids | Should -Contain 'any'
        }

        It 'builds the macOS version fallback chain' {
            $rids = $InternalHooks::GetCompatibleRuntimeIdentifiersFor('osx.12-arm64')

            $rids[0] | Should -Be 'osx.12-arm64'
            $rids | Should -Contain 'osx-arm64'
            $rids | Should -Contain 'osx'
            $rids | Should -Contain 'unix'
            $rids | Should -Contain 'any'
        }

        It 'includes the current RID first' {
            $currentRid = $InternalHooks::GetCurrentRuntimeIdentifier()
            $rids = $InternalHooks::GetCompatibleRuntimeIdentifiers()

            $rids[0] | Should -Be $currentRid
        }
    }

    Context 'Compatibility checks' {
        It 'accepts the current process RID' {
            $currentRid = $InternalHooks::GetCurrentRuntimeIdentifier()

            $InternalHooks::IsCompatibleRid($currentRid) | Should -BeTrue
        }

        It 'accepts portable and version-specific RIDs in the same Windows family' {
            $InternalHooks::IsCompatibleRidWith('win-x64', 'win10-x64') | Should -BeTrue
            $InternalHooks::IsCompatibleRidWith('win10-x64', 'win-x64') | Should -BeTrue
        }

        It 'accepts a fallback RID for a musl target' {
            $InternalHooks::IsCompatibleRidWith('linux-x64', 'linux-musl-x64') | Should -BeTrue
        }

        It 'rejects RIDs from a different operating system family' {
            $InternalHooks::IsCompatibleRidWith('linux-x64', 'win-x64') | Should -BeFalse
            $InternalHooks::IsCompatibleRidWith('osx-arm64', 'linux-arm64') | Should -BeFalse
        }

        It 'rejects empty RID values' {
            $InternalHooks::IsCompatibleRidWith($null, 'win-x64') | Should -BeFalse
            $InternalHooks::IsCompatibleRidWith('win-x64', '') | Should -BeFalse
        }
    }
}
