# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
Param()

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

Describe 'RuntimePackageHelper Tests' -tags 'CI' {

    BeforeAll {
        $InternalHooks = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]
    }

    Context 'IsRuntimesEntry' {

        It "Should return true for a runtimes/ path" {
            $InternalHooks::IsRuntimesEntry('runtimes/win-x64/native/file.dll') | Should -BeTrue
        }

        It "Should return true for runtimes/ path with backslashes" {
            $InternalHooks::IsRuntimesEntry('runtimes\win-x64\native\file.dll') | Should -BeTrue
        }

        It "Should return false for a lib/ path" {
            $InternalHooks::IsRuntimesEntry('lib/net472/MyLib.dll') | Should -BeFalse
        }

        It "Should return false for null" {
            $InternalHooks::IsRuntimesEntry($null) | Should -BeFalse
        }

        It "Should return false for empty string" {
            $InternalHooks::IsRuntimesEntry('') | Should -BeFalse
        }

        It "Should return false for a path that only contains 'runtimes' without separator" {
            $InternalHooks::IsRuntimesEntry('runtimes') | Should -BeFalse
        }

        It "Should be case insensitive" {
            $InternalHooks::IsRuntimesEntry('Runtimes/win-x64/native/file.dll') | Should -BeTrue
            $InternalHooks::IsRuntimesEntry('RUNTIMES/win-x64/native/file.dll') | Should -BeTrue
        }
    }

    Context 'GetRidFromRuntimesEntry' {

        It "Should extract RID from a valid runtimes path" {
            $InternalHooks::GetRidFromRuntimesEntry('runtimes/win-x64/native/file.dll') | Should -Be 'win-x64'
        }

        It "Should extract RID for linux-musl-x64" {
            $InternalHooks::GetRidFromRuntimesEntry('runtimes/linux-musl-x64/lib/file.dll') | Should -Be 'linux-musl-x64'
        }

        It "Should extract RID for osx-arm64" {
            $InternalHooks::GetRidFromRuntimesEntry('runtimes/osx-arm64/native/file.dylib') | Should -Be 'osx-arm64'
        }

        It "Should return null for non-runtimes path" {
            $InternalHooks::GetRidFromRuntimesEntry('lib/net472/MyLib.dll') | Should -BeNullOrEmpty
        }

        It "Should return null for null input" {
            $InternalHooks::GetRidFromRuntimesEntry($null) | Should -BeNullOrEmpty
        }
    }

    Context 'ShouldIncludeEntry' {

        It "Should include non-runtimes entries" {
            $InternalHooks::ShouldIncludeEntry('lib/net472/MyLib.dll') | Should -BeTrue
            $InternalHooks::ShouldIncludeEntry('content/readme.txt') | Should -BeTrue
            $InternalHooks::ShouldIncludeEntry('MyModule.psd1') | Should -BeTrue
        }

        It "Should include runtimes entry for current platform" {
            $currentRid = $InternalHooks::GetCurrentRuntimeIdentifier()
            $InternalHooks::ShouldIncludeEntry("runtimes/$currentRid/native/file.dll") | Should -BeTrue
        }

        It "Should include runtimes entry for 'any' RID" {
            # 'any' is always compatible
            $InternalHooks::ShouldIncludeEntry("runtimes/any/lib/file.dll") | Should -BeTrue
        }

        It "Should exclude runtimes entry for incompatible platform" {
            $currentRid = $InternalHooks::GetCurrentRuntimeIdentifier()
            if ($currentRid -match '^win') {
                $InternalHooks::ShouldIncludeEntry('runtimes/osx-arm64/native/file.dylib') | Should -BeFalse
                $InternalHooks::ShouldIncludeEntry('runtimes/linux-x64/native/file.so') | Should -BeFalse
            }
            elseif ($currentRid -match '^linux') {
                $InternalHooks::ShouldIncludeEntry('runtimes/win-x64/native/file.dll') | Should -BeFalse
                $InternalHooks::ShouldIncludeEntry('runtimes/osx-arm64/native/file.dylib') | Should -BeFalse
            }
            elseif ($currentRid -match '^osx') {
                $InternalHooks::ShouldIncludeEntry('runtimes/win-x64/native/file.dll') | Should -BeFalse
                $InternalHooks::ShouldIncludeEntry('runtimes/linux-x64/native/file.so') | Should -BeFalse
            }
        }
    }

    Context 'GetAvailableRidsFromZipFile' {

        BeforeAll {
            # Create a test .nupkg (zip) with multiple runtimes folders
            $script:testZipDir = Join-Path $TestDrive 'test-rids-zip'
            $null = New-Item $testZipDir -ItemType Directory -Force

            # Create runtimes folder structure
            $rids = @('win-x64', 'win-x86', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')
            foreach ($rid in $rids) {
                $ridNativeDir = Join-Path $testZipDir "runtimes/$rid/native"
                $null = New-Item $ridNativeDir -ItemType Directory -Force
                Set-Content -Path (Join-Path $ridNativeDir "test.dll") -Value "dummy"
            }

            # Also create a non-runtimes file
            Set-Content -Path (Join-Path $testZipDir "MyModule.psd1") -Value "dummy"

            # Create the zip
            $script:testZipPath = Join-Path $TestDrive 'test-rids.zip'
            [System.IO.Compression.ZipFile]::CreateFromDirectory($testZipDir, $testZipPath)
        }

        It "Should list all RIDs present in the zip" {
            $availableRids = $InternalHooks::GetAvailableRidsFromZipFile($testZipPath)
            $availableRids.Count | Should -Be 6
            $availableRids | Should -Contain 'win-x64'
            $availableRids | Should -Contain 'linux-x64'
            $availableRids | Should -Contain 'osx-arm64'
        }

        It "Should throw for non-existent file" {
            { $InternalHooks::GetAvailableRidsFromZipFile('C:\nonexistent\file.zip') } | Should -Throw
        }
    }

    Context 'Integration - ShouldIncludeEntry filters correctly for multi-platform package' {

        It "Should include current platform and exclude others" {
            $currentRid = $InternalHooks::GetCurrentRuntimeIdentifier()

            # Entries that should be included
            $InternalHooks::ShouldIncludeEntry("runtimes/$currentRid/native/file.dll") | Should -BeTrue
            $InternalHooks::ShouldIncludeEntry("lib/net472/MyLib.dll") | Should -BeTrue
            $InternalHooks::ShouldIncludeEntry("MyModule.psd1") | Should -BeTrue

            # At least one of these foreign platforms should be excluded
            $foreignPlatforms = @('win-x64', 'linux-x64', 'osx-arm64') | Where-Object { $_ -ne $currentRid }
            $excludedCount = 0
            foreach ($foreign in $foreignPlatforms) {
                if (-not $InternalHooks::ShouldIncludeEntry("runtimes/$foreign/native/file.dll")) {
                    $excludedCount++
                }
            }
            $excludedCount | Should -BeGreaterThan 0
        }
    }
}
