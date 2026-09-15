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
        $psUserContentPathVariable = Get-Variable -Name PSUserContentPath -ErrorAction SilentlyContinue
        $script:psUserContentPathAvailable = $null -ne $psUserContentPathVariable `
            -and $psUserContentPathVariable.Value -is [string] `
            -and -not [string]::IsNullOrWhiteSpace($psUserContentPathVariable.Value)
        $script:sessionContentPath = if ($script:psUserContentPathAvailable) {
            $psUserContentPathVariable.Value
        } else {
            $null
        }
        $script:legacyContentPath = if (Get-IsWindows) {
            Split-Path -Path (Get-CurrentUserModulesPath) -Parent
        } else {
            Join-Path -Path $env:HOME -ChildPath '.local/share/powershell'
        }
        $script:isCustomContentPath = $script:psUserContentPathAvailable `
            -and $script:sessionContentPath -ne $script:legacyContentPath

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
        # Clean up installed test modules from every scope exercised by the tests
        Uninstall-PSResource $testModuleName -Version "*" -Scope CurrentUser -SkipDependencyCheck -ErrorAction SilentlyContinue
        if ((Get-IsWindows) -and (Test-IsAdmin)) {
            Uninstall-PSResource $testModuleName -Version "*" -Scope AllUsers -SkipDependencyCheck -ErrorAction SilentlyContinue
        }
        # Clear testing hooks
        [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::ClearPSContentPathHooks()
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    Context 'PSResourceGet user content path selection' {
        It 'Should use $PSUserContentPath when available, Legacy when not' {
            Install-PSResource -Name $testModuleName -Repository $localRepo -Scope CurrentUser -TrustRepository
            
            $pathSource = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::GetTestHook("LastUserContentPathSource")
            $pathUsed = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::GetTestHook("LastUserContentPath")
            
            if ($script:psUserContentPathAvailable) {
                $pathSource | Should -Be '$PSUserContentPath'
                $pathUsed | Should -Be $script:sessionContentPath
            } else {
                $pathSource | Should -Be "Legacy"
                $pathUsed | Should -Be $script:legacyContentPath
            }
            
            # Module should be installed
            $res = Get-InstalledPSResource -Name $testModuleName -Scope CurrentUser
            $res.Name | Should -Be $testModuleName
        }
    }

    Context 'When a custom $PSUserContentPath is configured' {
        It "Should install to the custom user content path" {
            if (-not $script:isCustomContentPath) {
                Set-ItResult -Skipped -Because "A custom PSUserContentPath is not configured in this session"
                return
            }
            
            Install-PSResource -Name $testModuleName -Repository $localRepo -Scope CurrentUser -TrustRepository
            
            $pathSource = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::GetTestHook("LastUserContentPathSource")
            $pathUsed = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::GetTestHook("LastUserContentPath")
            
            $pathSource | Should -Be '$PSUserContentPath'
            $pathUsed | Should -Be $script:sessionContentPath
            
            # Module should be installed in custom path
            $res = Get-InstalledPSResource -Name $testModuleName -Scope CurrentUser
            $expectedModulesPath = Join-Path $script:sessionContentPath 'Modules'
            $expectedModulePath = Join-Path $expectedModulesPath $testModuleName
            Test-Path $expectedModulePath | Should -BeTrue
            $res.Name | Should -Be $testModuleName
            $res.InstalledLocation | Should -Be $expectedModulesPath
        }
    }

    Context 'PSResourceGet delegates user content path resolution' {
        It 'Should use $PSUserContentPath when the variable is available' {
            if (-not $script:psUserContentPathAvailable) {
                Set-ItResult -Skipped -Because "PSUserContentPath is not available"
                return
            }
            
            $beforePath = $PSUserContentPath
            
            Install-PSResource -Name $testModuleName -Repository $localRepo -Scope CurrentUser -TrustRepository
            
            $pathSource = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::GetTestHook("LastUserContentPathSource")
            $pathUsed = [Microsoft.PowerShell.PSResourceGet.UtilClasses.InternalHooks]::GetTestHook("LastUserContentPath")
            
            $pathSource | Should -Be '$PSUserContentPath'
            
            # Path should match the engine-provided session value
            $pathUsed | Should -Be $beforePath
            
            # Module should be installed
            $res = Get-InstalledPSResource -Name $testModuleName -Scope CurrentUser
            $res.Name | Should -Be $testModuleName
        }
    }

    Context "AllUsers scope should not be affected by PSContentPath/PSUserContentPath" {
        It "Should install to the shared PowerShell modules path" -Skip:(!((Get-IsWindows) -and (Test-IsAdmin))) {
            Install-PSResource -Name $testModuleName -Repository $localRepo -Scope AllUsers -TrustRepository
            $expectedModulesPath = Get-AllUsersModulesPath
            $expectedModulePath = Join-Path $expectedModulesPath $testModuleName
            Test-Path $expectedModulePath | Should -BeTrue
            $res = Get-InstalledPSResource -Name $testModuleName -Scope AllUsers
            $res.Name | Should -Be $testModuleName
            $res.InstalledLocation | Should -Be $expectedModulesPath
        }
    }
}
