# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
Param()

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/PSGetTestUtils.psm1"
Import-Module $modPath -Force

Describe 'PSUserContentPath/PSContentPath - End-to-End Install Location' -Tags 'CI' {
    BeforeAll {
        $script:originalPSModulePath = $env:PSModulePath
        $script:actualConfigPath = Join-Path $env:LOCALAPPDATA "PowerShell\powershell.config.json"
        $script:configBackup = $null
        
        # Detect if Get-PSContentPath cmdlet is available (requires PSContentPath experimental feature)
        $script:getPSContentPathAvailable = $false
        $script:isPSContentPathEnabled = $false
        $script:sessionContentPath = $null
        try {
            # Check if Get-PSContentPath cmdlet exists
            $null = Get-Command Get-PSContentPath -ErrorAction Stop
            $script:getPSContentPathAvailable = $true
            
            # Get the actual session path
            $script:sessionContentPath = Get-PSContentPath
            $documentsPath = [Environment]::GetFolderPath('MyDocuments')
            $documentsPS = Join-Path $documentsPath "PowerShell"
            
            # If Get-PSContentPath returns something other than Documents, the feature is enabled
            $script:isPSContentPathEnabled = $script:sessionContentPath -ne $documentsPS
        } catch {
            # Get-PSContentPath not available (feature disabled)
        }

        # Backup existing config if it exists
        if (Test-Path $script:actualConfigPath) {
            $script:configBackup = Get-Content $script:actualConfigPath -Raw
        }

        $localRepo = "psgettestlocal"
        $testModuleName = "PSContentPathTestModule"
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos

        # Create a test module
        New-TestModule -moduleName $testModuleName -repoName $localRepo -packageVersion "1.0.0" -prereleaseLabel "" -tags @()
    }

    AfterEach {
        # Restore PSModulePath
        $env:PSModulePath = $script:originalPSModulePath
        # Clean up installed test modules
        Uninstall-PSResource $testModuleName -Version "*" -SkipDependencyCheck -ErrorAction SilentlyContinue
        # Clear testing hooks
        [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::ClearPSContentPathHooks()
    }

    AfterAll {
        # Restore original config
        if ($null -ne $script:configBackup) {
            Set-Content -Path $script:actualConfigPath -Value $script:configBackup -Force
        }
        Get-RevertPSResourceRepositoryFile
    }

    Context "PSResourceGet behavior on PS 7.7+" {
        It "Should use Get-PSContentPath when available, Legacy when not" {
            Install-PSResource -Name $testModuleName -Repository $localRepo -Scope CurrentUser -TrustRepository
            
            $pathSource = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::GetTestHook("LastUserContentPathSource")
            $pathUsed = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::GetTestHook("LastUserContentPath")
            
            if ($script:getPSContentPathAvailable) {
                # When Get-PSContentPath cmdlet exists, PSResourceGet should use it
                $pathSource | Should -Be "Get-PSContentPath"
                $pathUsed | Should -Be $script:sessionContentPath
            } else {
                # When Get-PSContentPath cmdlet doesn't exist, PSResourceGet should use legacy path
                $pathSource | Should -Be "Legacy"
                $documentsPath = [Environment]::GetFolderPath('MyDocuments')
                $pathUsed | Should -BeLike "*$documentsPath*PowerShell"
            }
            
            # Module should be installed
            $res = Get-InstalledPSResource -Name $testModuleName
            $res.Name | Should -Be $testModuleName
        }
    }

    Context "When PSContentPath feature is enabled in session (PS >= 7.7)" {
        It "Should install to custom LocalAppData path (not Documents)" {
            if (-not $script:getPSContentPathAvailable -or -not $script:isPSContentPathEnabled) {
                Set-ItResult -Skipped -Because "PSContentPath feature not enabled in this session"
                return
            }
            
            Install-PSResource -Name $testModuleName -Repository $localRepo -Scope CurrentUser -TrustRepository
            
            $pathSource = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::GetTestHook("LastUserContentPathSource")
            $pathUsed = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::GetTestHook("LastUserContentPath")
            
            # PSResourceGet should call Get-PSContentPath
            $pathSource | Should -Be "Get-PSContentPath"
            
            # Path should NOT be Documents (feature enabled means custom path)
            $documentsPath = [Environment]::GetFolderPath('MyDocuments')
            $pathUsed | Should -Not -BeLike "*$documentsPath*"
            
            # Module should be installed in custom path
            $res = Get-InstalledPSResource -Name $testModuleName
            $res.Name | Should -Be $testModuleName
            $res.InstalledLocation | Should -Not -BeLike "*$documentsPath*"
        }
    }

    Context "PSResourceGet correctly delegates path resolution (PS >= 7.7)" {
        It "Should always defer to Get-PSContentPath when cmdlet is available" {
            if (-not $script:getPSContentPathAvailable) {
                Set-ItResult -Skipped -Because "Get-PSContentPath cmdlet not available"
                return
            }
            
            $beforePath = Get-PSContentPath
            
            Install-PSResource -Name $testModuleName -Repository $localRepo -Scope CurrentUser -TrustRepository
            
            $pathSource = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::GetTestHook("LastUserContentPathSource")
            $pathUsed = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::GetTestHook("LastUserContentPath")
            
            # PSResourceGet should call Get-PSContentPath
            $pathSource | Should -Be "Get-PSContentPath"
            
            # Path should match what Get-PSContentPath returns
            $pathUsed | Should -Be $beforePath
            
            # Module should be installed
            $res = Get-InstalledPSResource -Name $testModuleName
            $res.Name | Should -Be $testModuleName
        }
    }

    Context "AllUsers scope should not be affected by PSContentPath/PSUserContentPath" {
        BeforeAll {
            if (!$IsWindows -or !(Test-IsAdmin)) { return }
        }
        It "Should install to Program Files (AllUsers not affected by PSContentPath)" {
            if (!$IsWindows -or !(Test-IsAdmin)) {
                Set-ItResult -Skipped -Because "Test requires Windows and Administrator privileges"
                return
            }
            Install-PSResource -Name $testModuleName -Repository $localRepo -Scope AllUsers -TrustRepository
            $programFilesPath = [Environment]::GetFolderPath('ProgramFiles')
            $expectedPath = Join-Path $programFilesPath "PowerShell\Modules\$testModuleName"
            Test-Path $expectedPath | Should -BeTrue
            $res = Get-InstalledPSResource -Name $testModuleName
            $res.Name | Should -Be $testModuleName
            $res.InstalledLocation | Should -BeLike "*Program Files*PowerShell*Modules*"
        }
    }
}

function Test-IsAdmin {
    if ($IsWindows) {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = New-Object Security.Principal.WindowsPrincipal($identity)
        return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    }
    return $false
}
