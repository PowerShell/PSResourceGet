<#
    .SYNOPSIS
        Integration tests for DSC resource PSModule.

    .NOTES
        The header and footer was removed that is usually part of the
        DscResource.Tests integration test template.
        The header was adding the project folder as a PSModulePath which
        collide with the PowerShellGet test framework that copies the
        module to the regular PowerShell Module folder.
#>

$script:dscResourceFriendlyName = 'PSRepository'
$script:dcsResourceName = "MSFT_$($script:dscResourceFriendlyName)"

#region Integration Tests
$configurationFile = Join-Path -Path $PSScriptRoot -ChildPath "$($script:dcsResourceName).config.ps1"
. $configurationFile

Describe "$($script:dcsResourceName)_Integration" {
    $configurationName = "$($script:dcsResourceName)_AddRepository_Config"

    Context ('When using configuration {0}' -f $configurationName) {
        It 'Should compile and apply the MOF without throwing' {
            {
                $configurationParameters = @{
                    OutputPath        = $TestDrive
                    ConfigurationData = $ConfigurationData
                }

                & $configurationName @configurationParameters

                $startDscConfigurationParameters = @{
                    Path         = $TestDrive
                    ComputerName = 'localhost'
                    Wait         = $true
                    Verbose      = $true
                    Force        = $true
                    ErrorAction  = 'Stop'
                }

                Start-DscConfiguration @startDscConfigurationParameters
            } | Should -Not -Throw
        }

        It 'Should be able to call Get-DscConfiguration without throwing' {
            {
                $script:currentConfiguration = Get-DscConfiguration -Verbose -ErrorAction Stop
            } | Should -Not -Throw
        }

        It 'Should have set the resource and all the parameters should match' {
            $resourceCurrentState = $script:currentConfiguration | Where-Object -FilterScript {
                $_.ConfigurationName -eq $configurationName `
                    -and $_.ResourceId -eq "[$($script:dscResourceFriendlyName)]Integration_Test"
            }

            $resourceCurrentState.Ensure | Should -Be 'Present'
            $resourceCurrentState.Name | Should -Be $ConfigurationData.AllNodes.Name
            $resourceCurrentState.SourceLocation | Should -Be $ConfigurationData.AllNodes.TestSourceLocation
            $resourceCurrentState.PublishLocation | Should -Be $ConfigurationData.AllNodes.TestPublishLocation
            $resourceCurrentState.ScriptSourceLocation | Should -Be $ConfigurationData.AllNodes.TestScriptSourceLocation
            $resourceCurrentState.ScriptPublishLocation | Should -Be $ConfigurationData.AllNodes.TestScriptPublishLocation
            $resourceCurrentState.PackageManagementProvider | Should -Be 'NuGet'
            $resourceCurrentState.Trusted | Should -Be $true
            $resourceCurrentState.Registered | Should -Be $true
        }

        It 'Should return $true when Test-DscConfiguration is run' {
            Test-DscConfiguration -Verbose | Should -Be $true
        }
    }

    $configurationName = "$($script:dcsResourceName)_InstallTestModule_Config"

    Context ('When using configuration {0}' -f $configurationName) {
        It 'Should compile and apply the MOF without throwing' {
            {
                $configurationParameters = @{
                    OutputPath        = $TestDrive
                    ConfigurationData = $ConfigurationData
                }

                & $configurationName @configurationParameters

                $startDscConfigurationParameters = @{
                    Path         = $TestDrive
                    ComputerName = 'localhost'
                    Wait         = $true
                    Verbose      = $true
                    Force        = $true
                    ErrorAction  = 'Stop'
                }

                Start-DscConfiguration @startDscConfigurationParameters
            } | Should -Not -Throw
        }
    }

    $configurationName = "$($script:dcsResourceName)_ChangeRepository_Config"

    Context ('When using configuration {0}' -f $configurationName) {
        It 'Should compile and apply the MOF without throwing' {
            {
                $configurationParameters = @{
                    OutputPath        = $TestDrive
                    ConfigurationData = $ConfigurationData
                }

                & $configurationName @configurationParameters

                $startDscConfigurationParameters = @{
                    Path         = $TestDrive
                    ComputerName = 'localhost'
                    Wait         = $true
                    Verbose      = $true
                    Force        = $true
                    ErrorAction  = 'Stop'
                }

                Start-DscConfiguration @startDscConfigurationParameters
            } | Should -Not -Throw
        }

        It 'Should be able to call Get-DscConfiguration without throwing' {
            {
                $script:currentConfiguration = Get-DscConfiguration -Verbose -ErrorAction Stop
            } | Should -Not -Throw
        }

        It 'Should have set the resource and all the parameters should match' {
            $resourceCurrentState = $script:currentConfiguration | Where-Object -FilterScript {
                $_.ConfigurationName -eq $configurationName `
                    -and $_.ResourceId -eq "[$($script:dscResourceFriendlyName)]Integration_Test"
            }

            $resourceCurrentState.Ensure | Should -Be 'Present'
            $resourceCurrentState.Name | Should -Be $ConfigurationData.AllNodes.Name
            $resourceCurrentState.SourceLocation | Should -Be $ConfigurationData.AllNodes.SourceLocation
            $resourceCurrentState.PublishLocation | Should -Be $ConfigurationData.AllNodes.PublishLocation
            $resourceCurrentState.ScriptSourceLocation | Should -Be $ConfigurationData.AllNodes.ScriptSourceLocation
            $resourceCurrentState.ScriptPublishLocation | Should -Be $ConfigurationData.AllNodes.ScriptPublishLocation
            $resourceCurrentState.PackageManagementProvider | Should -Be $ConfigurationData.AllNodes.PackageManagementProvider
            $resourceCurrentState.Trusted | Should -Be $false
            $resourceCurrentState.Registered | Should -Be $true
        }

        It 'Should return $true when Test-DscConfiguration is run' {
            Test-DscConfiguration -Verbose | Should -Be $true
        }
    }

    $configurationName = "$($script:dcsResourceName)_RemoveRepository_Config"

    Context ('When using configuration {0}' -f $configurationName) {
        It 'Should compile and apply the MOF without throwing' {
            {
                $configurationParameters = @{
                    OutputPath        = $TestDrive
                    ConfigurationData = $ConfigurationData
                }

                & $configurationName @configurationParameters

                $startDscConfigurationParameters = @{
                    Path         = $TestDrive
                    ComputerName = 'localhost'
                    Wait         = $true
                    Verbose      = $true
                    Force        = $true
                    ErrorAction  = 'Stop'
                }

                Start-DscConfiguration @startDscConfigurationParameters
            } | Should -Not -Throw
        }

        It 'Should be able to call Get-DscConfiguration without throwing' {
            {
                $script:currentConfiguration = Get-DscConfiguration -Verbose -ErrorAction Stop
            } | Should -Not -Throw
        }

        It 'Should have set the resource and all the parameters should match' {
            $resourceCurrentState = $script:currentConfiguration | Where-Object -FilterScript {
                $_.ConfigurationName -eq $configurationName `
                    -and $_.ResourceId -eq "[$($script:dscResourceFriendlyName)]Integration_Test"
            }

            $resourceCurrentState.Ensure | Should -Be 'Absent'
            $resourceCurrentState.Name | Should -Be $ConfigurationData.AllNodes.Name
            $resourceCurrentState.SourceLocation | Should -BeNullOrEmpty
            $resourceCurrentState.PublishLocation | Should -BeNullOrEmpty
            $resourceCurrentState.ScriptSourceLocation | Should -BeNullOrEmpty
            $resourceCurrentState.ScriptPublishLocation | Should -BeNullOrEmpty
            $resourceCurrentState.PackageManagementProvider | Should -BeNullOrEmpty
            $resourceCurrentState.Trusted | Should -Be $false
            $resourceCurrentState.Registered | Should -Be $false
        }

        It 'Should return $true when Test-DscConfiguration is run' {
            Test-DscConfiguration -Verbose | Should -Be $true
        }
    }
}
#endregion
