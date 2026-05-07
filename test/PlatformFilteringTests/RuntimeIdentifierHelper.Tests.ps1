# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
Param()

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

Describe 'RuntimeIdentifierHelper Tests' -tags 'CI' {

    BeforeAll {
        $InternalHooks = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]
    }

    Context 'GetCurrentRuntimeIdentifier' {

        It "Should return a non-empty RID string" {
            $rid = $InternalHooks::GetCurrentRuntimeIdentifier()
            $rid | Should -Not -BeNullOrEmpty
        }

        It "Should return a RID matching the expected platform prefix" {
            $rid = $InternalHooks::GetCurrentRuntimeIdentifier()
            if ($IsWindows -or ($PSVersionTable.PSVersion.Major -eq 5)) {
                $rid | Should -Match '^win-'
            }
            elseif ($IsLinux) {
                $rid | Should -Match '^linux(-musl)?-'
            }
            elseif ($IsMacOS) {
                $rid | Should -Match '^osx-'
            }
        }

        It "Should return a RID with a known architecture suffix" {
            $rid = $InternalHooks::GetCurrentRuntimeIdentifier()
            $rid | Should -Match '-(x64|x86|arm64|arm|s390x|ppc64le|loongarch64)$'
        }

        It "Should return consistent results on repeated calls (caching)" {
            $rid1 = $InternalHooks::GetCurrentRuntimeIdentifier()
            $rid2 = $InternalHooks::GetCurrentRuntimeIdentifier()
            $rid1 | Should -Be $rid2
        }
    }

    Context 'GetCompatibleRuntimeIdentifiers' {

        It "Should return a non-empty list" {
            $rids = $InternalHooks::GetCompatibleRuntimeIdentifiers()
            $rids.Count | Should -BeGreaterThan 0
        }

        It "Should include the current RID as the first entry" {
            $currentRid = $InternalHooks::GetCurrentRuntimeIdentifier()
            $rids = $InternalHooks::GetCompatibleRuntimeIdentifiers()
            $rids[0] | Should -Be $currentRid
        }

        It "Should include 'any' in the compatibility chain" {
            $rids = $InternalHooks::GetCompatibleRuntimeIdentifiers()
            $rids | Should -Contain 'any'
        }
    }

    Context 'GetCompatibleRuntimeIdentifiersFor - Windows' {

        It "Should build compatibility chain for win-x64" {
            $rids = $InternalHooks::GetCompatibleRuntimeIdentifiersFor('win-x64')
            $rids | Should -Contain 'win-x64'
            $rids | Should -Contain 'win'
            $rids | Should -Contain 'any'
        }

        It "Should build compatibility chain for win10-x64" {
            $rids = $InternalHooks::GetCompatibleRuntimeIdentifiersFor('win10-x64')
            $rids | Should -Contain 'win10-x64'
            $rids | Should -Contain 'win-x64'
            $rids | Should -Contain 'win'
            $rids | Should -Contain 'any'
        }

        It "Should build compatibility chain for win-arm64" {
            $rids = $InternalHooks::GetCompatibleRuntimeIdentifiersFor('win-arm64')
            $rids | Should -Contain 'win-arm64'
            $rids | Should -Contain 'win'
            $rids | Should -Contain 'any'
        }
    }

    Context 'GetCompatibleRuntimeIdentifiersFor - Linux' {

        It "Should build compatibility chain for linux-x64" {
            $rids = $InternalHooks::GetCompatibleRuntimeIdentifiersFor('linux-x64')
            $rids | Should -Contain 'linux-x64'
            $rids | Should -Contain 'linux'
            $rids | Should -Contain 'unix'
            $rids | Should -Contain 'any'
        }

        It "Should build compatibility chain for linux-musl-x64" {
            $rids = $InternalHooks::GetCompatibleRuntimeIdentifiersFor('linux-musl-x64')
            $rids | Should -Contain 'linux-musl-x64'
            $rids | Should -Contain 'linux-x64'
            $rids | Should -Contain 'unix'
            $rids | Should -Contain 'any'
        }
    }

    Context 'GetCompatibleRuntimeIdentifiersFor - macOS' {

        It "Should build compatibility chain for osx-arm64" {
            $rids = $InternalHooks::GetCompatibleRuntimeIdentifiersFor('osx-arm64')
            $rids | Should -Contain 'osx-arm64'
            $rids | Should -Contain 'osx'
            $rids | Should -Contain 'unix'
            $rids | Should -Contain 'any'
        }

        It "Should build compatibility chain for osx.12-arm64" {
            $rids = $InternalHooks::GetCompatibleRuntimeIdentifiersFor('osx.12-arm64')
            $rids | Should -Contain 'osx.12-arm64'
            $rids | Should -Contain 'osx-arm64'
            $rids | Should -Contain 'osx'
            $rids | Should -Contain 'unix'
            $rids | Should -Contain 'any'
        }
    }

    Context 'IsCompatibleRid' {

        It "Should return true for the current platform RID" {
            $currentRid = $InternalHooks::GetCurrentRuntimeIdentifier()
            $InternalHooks::IsCompatibleRid($currentRid) | Should -BeTrue
        }

        It "Should return true for 'any'" {
            $InternalHooks::IsCompatibleRid('any') | Should -BeTrue
        }

        It "Should return false for null or empty" {
            $InternalHooks::IsCompatibleRid($null) | Should -BeFalse
            $InternalHooks::IsCompatibleRid('') | Should -BeFalse
        }

        It "Should return false for a clearly incompatible RID" {
            $currentRid = $InternalHooks::GetCurrentRuntimeIdentifier()
            # Pick an OS that is definitely not the current one
            if ($currentRid -match '^win') {
                $InternalHooks::IsCompatibleRid('osx-arm64') | Should -BeFalse
            }
            elseif ($currentRid -match '^linux') {
                $InternalHooks::IsCompatibleRid('win-x64') | Should -BeFalse
            }
            elseif ($currentRid -match '^osx') {
                $InternalHooks::IsCompatibleRid('win-x64') | Should -BeFalse
            }
        }

        It "Should return true for a more-specific RID in the same platform family" {
            $currentRid = $InternalHooks::GetCurrentRuntimeIdentifier()
            if ($currentRid -eq 'win-x64') {
                # win10-x64 package folder should be compatible on a win-x64 machine
                $InternalHooks::IsCompatibleRid('win10-x64') | Should -BeTrue
            }
            elseif ($currentRid -eq 'osx-arm64') {
                $InternalHooks::IsCompatibleRid('osx.12-arm64') | Should -BeTrue
            }
        }
    }
}
