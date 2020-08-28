#region HEADER
# This must be same name as the root folder, and module manifest.
$script:DSCModuleName = 'DSC'
$script:DSCResourceName = 'MSFT_PSRepository'

# Unit Test Template Version: 1.2.4
$script:moduleRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ( (-not (Test-Path -Path (Join-Path -Path $script:moduleRoot -ChildPath 'DSCResource.Tests'))) -or `
    (-not (Test-Path -Path (Join-Path -Path $script:moduleRoot -ChildPath 'DSCResource.Tests\TestHelper.psm1'))) ) {
    & git @('clone', 'https://github.com/PowerShell/DscResource.Tests.git', (Join-Path -Path $script:moduleRoot -ChildPath 'DscResource.Tests'))
}

Import-Module -Name (Join-Path -Path $script:moduleRoot -ChildPath (Join-Path -Path 'DSCResource.Tests' -ChildPath 'TestHelper.psm1')) -Force

$TestEnvironment = Initialize-TestEnvironment `
    -DSCModuleName $script:DSCModuleName `
    -DSCResourceName $script:DSCResourceName `
    -ResourceType 'Mof' `
    -TestType Unit

#endregion HEADER

function Invoke-TestSetup {
}

function Invoke-TestCleanup {
    Restore-TestEnvironment -TestEnvironment $TestEnvironment
}

# Begin Testing
try {
    Invoke-TestSetup

    InModuleScope $script:DSCResourceName {
        $mockRepositoryName = 'PSTestGallery'
        $mockSourceLocation = 'https://www.poshtestgallery.com/api/v2'
        $mockPriority = 1
        $mockInstallationPolicy_Trusted = $true
        $mockInstallationPolicy_NotTrusted = $false

        #$mockInstallationPolicy_Trusted = 'Trusted'
        #$mockInstallationPolicy_NotTrusted = 'Untrusted'


        $mockRepository = New-Object -TypeName Object |
            Add-Member -Name 'Name' -MemberType NoteProperty -Value $mockRepositoryName -PassThru |
            Add-Member -Name 'URL' -MemberType NoteProperty -Value $mockSourceLocation -PassThru |
            Add-Member -Name 'InstallationPolicy' -MemberType NoteProperty -Value $mockInstallationPolicy_Trusted -PassThru |
            Add-Member -Name 'Priority' -MemberType NoteProperty -Value $mockPriority  -PassThru |
            #Add-Member -Name 'Trusted' -MemberType NoteProperty -Value $mockInstallationPolicy_Trusted -PassThru |
            Add-Member -Name 'Trusted' -MemberType NoteProperty -Value $true -PassThru |
            Add-Member -Name 'Registered' -MemberType NoteProperty -Value $true -PassThru -Force

        $mockGetPSRepository = {
            return @($mockRepository)
        }

        Describe 'MSFT_PSRepository\Get-TargetResource' -Tag 'Get' {
            Context 'When the system is in the desired state' {
                Context 'When the configuration is present' {
                    BeforeAll {
                        Mock -CommandName Get-PSResourceRepository -MockWith $mockGetPSRepository
                    }

                    #It 'Should return the same values as passed as parameters' {
                     #   $getTargetResourceResult = Get-TargetResource -Name $mockRepositoryName
                    #    $getTargetResourceResult.Name | Should -Be $mockRepositoryName

                     #   Assert-MockCalled -CommandName Get-PSResourceRepository -Exactly -Times 1 -Scope It
                   # }

                    It 'Should return the correct values for the other properties' {
                        $getTargetResourceResult = Get-TargetResource -Name $mockRepositoryName -Verbose

                        $getTargetResourceResult.Ensure | Should -Be 'Present'
                        #$getTargetResourceResult.URL | Should -Be $mockRepository.URL
                       # $getTargetResourceResult.Priority | Should -Be $mockRepository.Priority
                        #$getTargetResourceResult.InstallationPolicy | Should -Be $mockRepository.InstallationPolicy
                        #$getTargetResourceResult.Trusted | Should -Be $mockRepository.Trusted
                        #$getTargetResourceResult.Trusted | Should -Be $true
                        $getTargetResourceResult.Registered | Should -Be $true

                        Assert-MockCalled -CommandName Get-PSResourceRepository -Exactly -Times 1 -Scope It
                    }
                }

                Context 'When the configuration is absent' {
                    BeforeAll {
                        Mock -CommandName Get-PSResourceRepository
                    }

                    It 'Should return the same values as passed as parameters' {
                        $getTargetResourceResult = Get-TargetResource -Name $mockRepositoryName
                        $getTargetResourceResult.Name | Should -Be $mockRepositoryName

                        Assert-MockCalled -CommandName Get-PSResourceRepository -Exactly -Times 1 -Scope It
                    }

                    It 'Should return the correct values for the other properties' {
                        $getTargetResourceResult = Get-TargetResource -Name $mockRepositoryName

                        $getTargetResourceResult.Ensure | Should -Be 'Absent'
                        $getTargetResourceResult.URL | Should -BeNullOrEmpty
                        $getTargetResourceResult.Priority | Should -BeNullOrEmpty
                        #$getTargetResourceResult.InstallationPolicy | Should -BeNullOrEmpty
                        #$getTargetResourceResult.Trusted | Should -Be $false
                        $getTargetResourceResult.Registered | Should -Be $false

                        Assert-MockCalled -CommandName Get-PSResourceRepository -Exactly -Times 1 -Scope It
                    }
                }
            }
        }

        Describe 'MSFT_PSRepository\Set-TargetResource' -Tag 'Set' {
            Context 'When the system is not in the desired state' {
                BeforeAll {
                    Mock -CommandName Register-PSResourceRepository
                    Mock -CommandName Unregister-PSResourceRepository
                    Mock -CommandName Set-PSResourceRepository
                }

                Context 'When the configuration should be present' {
                    Context 'When the repository does not exist' {
                        BeforeEach {
                            Mock -CommandName Get-TargetResource -MockWith {
                                return @{
                                    Ensure                    = 'Absent'
                                    Name                      = $mockRepositoryName
                                    URL                       = $null
                                    Priority                  = $null
                                    #InstallationPolicy        = $null
                                    #Trusted                   = $false
                                    Registered                = $false
                                }
                            }
                        }

                        It 'Should return call the correct mocks' {
                            $setTargetResourceParameters = @{
                                Name                      = $mockRepository.Name
                                URL                       = $mockRepository.URL
                                #  Priority                  = $mockRepository.Priority
                                #Trusted                   = $mockRepository.Trusted
                                #InstallationPolicy        = $mockRepository.InstallationPolicy
                            }

                            { Set-TargetResource @setTargetResourceParameters } | Should -Not -Throw

                            Assert-MockCalled -CommandName Register-PSResourceRepository -Exactly -Times 1 -Scope It
                            Assert-MockCalled -CommandName Unregister-PSResourceRepository -Exactly -Times 0 -Scope It
                            Assert-MockCalled -CommandName Set-PSresourceRepository -Exactly -Times 0 -Scope It
                        }
                    }

                    Context 'When the repository do exist but with wrong properties' {
                        BeforeEach {
                            Mock -CommandName Get-TargetResource -MockWith {
                                return @{
                                    Ensure                    = 'Present'
                                    Name                      = $mockRepository.Name
                                    URL                       = 'https://www.powershellgallery.com/api/v2'
                                    Priority                  = '0'
                                    #InstallationPolicy        = $mockRepository.InstallationPolicy
                                    #Trusted                   = $mockRepository.Trusted
                                    Registered                = $mockRepository.Registered
                                }
                            }
                        }

                        It 'Should return call the correct mocks' {
                            $setTargetResourceParameters = @{
                                Name                      = $mockRepository.Name
                                URL                       = $mockRepository.URL
                                #Priority                  = $mockRepository.Priority
                                #Trusted                   = $mockRepository.Trusted
                                #InstallationPolicy        = $mockRepository.InstallationPolicy
                            }

                            { MSFT_PSRepository\Set-TargetResource @setTargetResourceParameters } | Should -Not -Throw

                            Assert-MockCalled -CommandName Register-PSResourceRepository -Exactly -Times 0 -Scope It
                            Assert-MockCalled -CommandName Unregister-PSresourceRepository -Exactly -Times 0 -Scope It
                            Assert-MockCalled -CommandName Set-PSResourceRepository -Exactly -Times 1 -Scope It
                         }
                    }
                }

                Context 'When the configuration should be absent' {
                    Context 'When the repository do exist' {
                        BeforeEach {
                            Mock -CommandName Get-TargetResource -MockWith {
                                return @{
                                    Ensure                    = 'Present'
                                    Name                      = $mockRepository.Name
                                    URL                       = $mockRepository.URL
                                    Priority                  = $mockRepository.Priority
                                    #InstallationPolicy        = $mockRepository.InstallationPolicy
                                    #Trusted                   = $mockRepository.Trusted
                                    Registered                = $mockRepository.Registered
                                }
                            }
                        }

                        It 'Should return call the correct mocks' {
                            $setTargetResourceParameters = @{
                                Ensure = 'Absent'
                                Name   = $mockRepositoryName
                            }

                            { Set-TargetResource @setTargetResourceParameters } | Should -Not -Throw

                            Assert-MockCalled -CommandName Register-PSResourceRepository -Exactly -Times 0 -Scope It
                            Assert-MockCalled -CommandName Unregister-PSResourceRepository -Exactly -Times 1 -Scope It
                            Assert-MockCalled -CommandName Set-PSResourceRepository -Exactly -Times 0 -Scope It
                         }
                    }
                }
            }
        }

        Describe 'MSFT_PSRepository\Test-TargetResource' -Tag 'Test' {
            Context 'When the system is in the desired state' {
                Context 'When the configuration is present' {
                    BeforeEach {
                        Mock -CommandName Get-TargetResource -MockWith {
                            return @{
                                Ensure                    = 'Present'
                                Name                      = $mockRepository.Name
                                URL                       = $mockRepository.URL
                                Priority                  = $mockRepository.Priority
                                #InstallationPolicy        = $mockRepository.InstallationPolicy
                                #Trusted                   = $mockRepository.Trusted
                                Registered                = $mockRepository.Registered
                            }
                        }
                    }

                    It 'Should return the state as $true' {
                        $testTargetResourceResult = Test-TargetResource -Name $mockRepositoryName
                        $testTargetResourceResult | Should -Be $true

                        Assert-MockCalled -CommandName Get-TargetResource -Exactly -Times 1 -Scope It
                    }
                }

                Context 'When the configuration is absent' {
                    BeforeEach {
                        Mock -CommandName Get-TargetResource -MockWith {
                            return @{
                                Ensure                    = 'Absent'
                                Name                      = $mockRepositoryName
                                URL                       = $null
                                Priority                  = $null
                                #InstallationPolicy        = $null
                                #Trusted                   = $false
                                Registered                = $false
                            }
                        }
                    }

                    It 'Should return the state as $true' {
                        $testTargetResourceResult = Test-TargetResource -Ensure 'Absent' -Name $mockRepositoryName
                        $testTargetResourceResult | Should -Be $true

                        Assert-MockCalled -CommandName Get-TargetResource -Exactly -Times 1 -Scope It
                    }
                }
            }

            Context 'When the system is not in the desired state' {
                Context 'When the configuration should be present' {
                    BeforeEach {
                        Mock -CommandName Get-TargetResource -MockWith {
                            return @{
                                Ensure                    = 'Absent'
                                Name                      = $mockRepositoryName
                                URL                       = $null
                                Priority                  = $null
                                #InstallationPolicy        = $null
                                #Trusted                   = $false
                                Registered                = $false
                            }
                        }
                    }

                    It 'Should return the state as $false' {
                        $testTargetResourceParameters = @{
                            Name                      = $mockRepository.Name
                            URL                       = $mockRepository.URL
                            Priority                  = $mockRepository.Priority
                            #Trusted                   = $mockRepository.Trusted
                            #InstallationPolicy        = $mockRepository.InstallationPolicy
                        }

                        $testTargetResourceResult = Test-TargetResource @testTargetResourceParameters
                        $testTargetResourceResult | Should -Be $false

                        Assert-MockCalled -CommandName Get-TargetResource -Exactly -Times 1 -Scope It
                    }
                }

                Context 'When a property is not in desired state' {
                    BeforeEach {
                        Mock -CommandName Get-TargetResource -MockWith {
                            return @{
                                Ensure                    = 'Present'
                                Name                      = $mockRepository.Name
                                URL                       = $mockRepository.URL
                                Priority                  = $mockRepository.Priority
                                #InstallationPolicy        = $mockRepository.InstallationPolicy
                                #Trusted                   = $mockRepository.Trusted
                                Registered                = $mockRepository.Registered
                            }
                        }
                    }

                    $defaultTestCase = @{
                        URL                       = $mockRepository.URL
                        Priority                  = $mockRepository.Priority
                        #Trusted                   = $mockRepository.Trusted
                        #InstallationPolicy        = $mockRepository.InstallationPolicy
                    }

                    $testCaseSourceLocationIsMissing = $defaultTestCase.Clone()
                    $testCaseSourceLocationIsMissing['TestName'] = 'SourceLocation is missing'
                    $testCaseSourceLocationIsMissing['URL'] = 'https://www.powershellgallery.com/api/v2'

               
                    $testCasePriorityIsMissing = $defaultTestCase.Clone()
                    $testCasePriorityIsMissing['TestName'] = 'Priority is missing'
                    $testCasePriorityIsMissing['Priority'] = '50'


                    #$testCaseInstallationPolicyIsMissing = $defaultTestCase.Clone()
                    #$testCaseInstallationPolicyIsMissing['TestName'] = 'InstallationPolicy is missing'
                    #$testCaseInstallationPolicyIsMissing['Trusted'] = $mockInstallationPolicy_NotTrusted

                    $testCases = @(
                        $testCaseSourceLocationIsMissing
                        $testCasePriorityIsMissing
                        #$testCaseInstallationPolicyIsMissing
                    )

                    It 'Should return the state as $false when the correct <TestName>' -TestCases $testCases {
                        param
                        (
                            $URL,
                            $Priority
                            #$Trusted,
                            #$InstallationPolicy
                        )

                        $testTargetResourceParameters = @{
                            Name                      = $mockRepositoryName
                            URL                       = $URL
                            Priority                  = $Priority
                            #Trusted                   = $Trusted
                            #InstallationPolicy        = $InstallationPolicy
                        }

                        $testTargetResourceResult = Test-TargetResource @testTargetResourceParameters
                        $testTargetResourceResult | Should -Be $false

                        Assert-MockCalled -CommandName Get-TargetResource -Exactly -Times 1 -Scope It
                    }
                }

                Context 'When the configuration should be absent' {
                    BeforeEach {
                        Mock -CommandName Get-TargetResource -MockWith {
                            return @{
                                Ensure                    = 'Present'
                                Name                      = $mockRepositoryName
                                URL                       = $mockRepository.URL
                                Priority                  = $mockRepository.Priority
                                #Trusted                   = $mockRepository.Trusted
                                #InstallationPolicy        = $mockRepository.InstallationPolicy
                                Registered                = $mockRepository.Registered
                            }
                        }
                    }

                    It 'Should return the state as $false' {
                        $testTargetResourceResult = Test-TargetResource -Ensure 'Absent' -Name $mockRepositoryName
                        $testTargetResourceResult | Should -Be $false

                        Assert-MockCalled -CommandName Get-TargetResource -Exactly -Times 1 -Scope It
                    }
                }
            }
        }
    }
}
finally {
    Invoke-TestCleanup
}
