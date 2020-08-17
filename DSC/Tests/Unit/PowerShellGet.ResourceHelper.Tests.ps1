<#
    .SYNOPSIS
        Automated unit test for helper functions in module PowerShellGet.ResourceHelper.
#>


$script:helperModuleName = 'PowerShellGet.ResourceHelper'

$resourceModuleRoot = Split-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -Parent
$dscResourcesFolderFilePath = Join-Path -Path (Join-Path -Path $resourceModuleRoot -ChildPath 'Modules') `
    -ChildPath $script:helperModuleName

Import-Module -Name (Join-Path -Path $dscResourcesFolderFilePath `
        -ChildPath "$script:helperModuleName.psm1") -Force

InModuleScope $script:helperModuleName {
    Describe 'New-SplatParameterHashTable' {
        Context 'When specific parameters should be returned' {
            It 'Should return a hashtable with the correct values' {
                $mockPSBoundParameters = @{
                    Property1 = '1'
                    Property2 = '2'
                    Property3 = '3'
                    Property4 = '4'
                }

                $extractArgumentsResult = New-SplatParameterHashTable `
                    -FunctionBoundParameters $mockPSBoundParameters `
                    -ArgumentNames @('Property2', 'Property3')

                $extractArgumentsResult | Should -BeOfType [System.Collections.Hashtable]
                $extractArgumentsResult.Count | Should -Be 2
                $extractArgumentsResult.ContainsKey('Property2') | Should -BeTrue
                $extractArgumentsResult.ContainsKey('Property3') | Should -BeTrue
                $extractArgumentsResult.Property2 | Should -Be '2'
                $extractArgumentsResult.Property3 | Should -Be '3'
            }
        }

        Context 'When the specific parameters to be returned does not exist' {
            It 'Should return an empty hashtable' {
                $mockPSBoundParameters = @{
                    Property1 = '1'
                }

                $extractArgumentsResult = New-SplatParameterHashTable `
                    -FunctionBoundParameters $mockPSBoundParameters `
                    -ArgumentNames @('Property2', 'Property3')

                $extractArgumentsResult | Should -BeOfType [System.Collections.Hashtable]
                $extractArgumentsResult.Count | Should -Be 0
            }
        }

        Context 'When and empty hashtable is passed in the parameter FunctionBoundParameters' {
            It 'Should return an empty hashtable' {
                $mockPSBoundParameters = @{
                }

                $extractArgumentsResult = New-SplatParameterHashTable `
                    -FunctionBoundParameters $mockPSBoundParameters `
                    -ArgumentNames @('Property2', 'Property3')

                $extractArgumentsResult | Should -BeOfType [System.Collections.Hashtable]
                $extractArgumentsResult.Count | Should -Be 0
            }
        }
    }

    Describe 'Test-ParameterValue' {
        BeforeAll {
            $mockProviderName = 'PowerShellGet'
        }

        Context 'When passing a correct uri as ''Value'' and type is ''SourceUri''' {
            It 'Should not throw an error' {
                {
                    Test-ParameterValue `
                        -Value 'https://mocked.uri' `
                        -Type 'SourceUri' `
                        -ProviderName $mockProviderName
                } | Should -Not -Throw
            }
        }

        Context 'When passing an invalid uri as ''Value'' and type is ''SourceUri''' {
            It 'Should throw the correct error' {
                $mockParameterName = 'mocked.uri'

                {
                    Test-ParameterValue `
                        -Value $mockParameterName `
                        -Type 'SourceUri' `
                        -ProviderName $mockProviderName
                } | Should -Throw ($LocalizedData.InValidUri -f $mockParameterName)
            }
        }

        Context 'When passing a correct path as ''Value'' and type is ''DestinationPath''' {
            It 'Should not throw an error' {
                {
                    Test-ParameterValue `
                        -Value 'TestDrive:\' `
                        -Type 'DestinationPath' `
                        -ProviderName $mockProviderName
                } | Should -Not -Throw
            }
        }

        Context 'When passing an invalid path as ''Value'' and type is ''DestinationPath''' {
            It 'Should throw the correct error' {
                $mockParameterName = 'TestDrive:\NonExistentPath'

                {
                    Test-ParameterValue `
                        -Value $mockParameterName `
                        -Type 'DestinationPath' `
                        -ProviderName $mockProviderName
                } | Should -Throw ($LocalizedData.PathDoesNotExist -f $mockParameterName)
            }
        }

        #Context 'When passing a correct uri as ''Value'' and type is ''PackageSource''' {
        #    It 'Should not throw an error' {
        #        {
        #            Test-ParameterValue `
        #                -Value 'https://mocked.uri' 
                        #-Type 'PackageSource' `
                        #-ProviderName $mockProviderName
        #        } | Should -Not -Throw
        #    }
        # }

        #Context 'When passing an correct package source as ''Value'' and type is ''PackageSource''' {
        #    BeforeAll {
        #        $mockParameterName = 'PSGallery'

        #        Mock -CommandName Get-PackageSource -MockWith {
        #            return New-Object -TypeName Object |
        #            Add-Member -Name 'Name' -MemberType NoteProperty -Value $mockParameterName -PassThru
        #        }
        #    }
        #}

       # Context 'When passing type is ''PackageSource'' and passing a package source that does not exist' {
       #     BeforeAll {
       #         $mockParameterName = 'PSGallery'

       #         Mock -CommandName Get-PackageSource
       #     }
       # }

        Context 'When passing invalid type in parameter ''Type''' {
            BeforeAll {
                $mockType = 'UnknownType'
            }

            It 'Should throw the correct error' {
                {
                    Test-ParameterValue `
                        -Value 'AnyArgument' `
                        -Type $mockType `
                        -ProviderName $mockProviderName
                } | Should -Throw ($LocalizedData.UnexpectedArgument -f $mockType)
            }
        }
    }

    Describe 'Test-VersionParameter' {
        Context 'When not passing in any parameters (using default values)' {
            It 'Should return true' {
                Test-VersionParameter | Should -BeTrue
            }
        }

        Context 'When only ''RequiredVersion'' are passed' {
            It 'Should return true' {
                #Test-VersionParameter -RequiredVersion '3.0.0.0' | Should -BeTrue
            }
        }
<#
        Context 'When ''MinimumVersion'' has a lower version than ''MaximumVersion''' {
            It 'Should throw the correct error' {
                {
                    Test-VersionParameter `
                        -MinimumVersion '2.0.0.0' `
                        -MaximumVersion '1.0.0.0'
                } | Should -Throw $LocalizedData.VersionError
            }
        }

        Context 'When ''MinimumVersion'' has a lower version than ''MaximumVersion''' {
            It 'Should throw the correct error' {
                {
                    Test-VersionParameter `
                        -MinimumVersion '2.0.0.0' `
                        -MaximumVersion '1.0.0.0'
                } | Should -Throw $LocalizedData.VersionError
            }
        }

        Context 'When ''RequiredVersion'', ''MinimumVersion'', and ''MaximumVersion'' are passed' {
            It 'Should throw the correct error' {
                {
                    Test-VersionParameter `
                        -RequiredVersion '3.0.0.0' `
                        -MinimumVersion '2.0.0.0' `
                        -MaximumVersion '1.0.0.0'
                } | Should -Throw $LocalizedData.VersionError
            }
        }
        #>
    }


    Describe 'Testing Test-DscParameterState' -Tag TestDscParameterState {
        Context -Name 'When passing values' -Fixture {
            It 'Should return true for two identical tables' {
                $mockDesiredValues = @{ Example = 'test' }

                $testParameters = @{
                    CurrentValues = $mockDesiredValues
                    DesiredValues = $mockDesiredValues
                }

                Test-DscParameterState @testParameters | Should -Be $true
            }

            It 'Should return false when a value is different for [System.String]' {
                $mockCurrentValues = @{ Example = [System.String]'something' }
                $mockDesiredValues = @{ Example = [System.String]'test' }

                $testParameters = @{
                    CurrentValues = $mockCurrentValues
                    DesiredValues = $mockDesiredValues
                }

                Test-DscParameterState @testParameters | Should -Be $false
            }

            It 'Should return false when a value is different for [System.Int32]' {
                $mockCurrentValues = @{ Example = [System.Int32]1 }
                $mockDesiredValues = @{ Example = [System.Int32]2 }

                $testParameters = @{
                    CurrentValues = $mockCurrentValues
                    DesiredValues = $mockDesiredValues
                }

                Test-DscParameterState @testParameters | Should -Be $false
            }

            It 'Should return false when a value is different for [Int16]' {
                $mockCurrentValues = @{ Example = [System.Int16]1 }
                $mockDesiredValues = @{ Example = [System.Int16]2 }

                $testParameters = @{
                    CurrentValues = $mockCurrentValues
                    DesiredValues = $mockDesiredValues
                }

                Test-DscParameterState @testParameters | Should -Be $false
            }

            It 'Should return false when a value is different for [UInt16]' {
                $mockCurrentValues = @{ Example = [System.UInt16]1 }
                $mockDesiredValues = @{ Example = [System.UInt16]2 }

                $testParameters = @{
                    CurrentValues = $mockCurrentValues
                    DesiredValues = $mockDesiredValues
                }

                Test-DscParameterState @testParameters | Should -Be $false
            }

            It 'Should return false when a value is missing' {
                $mockCurrentValues = @{ }
                $mockDesiredValues = @{ Example = 'test' }

                $testParameters = @{
                    CurrentValues = $mockCurrentValues
                    DesiredValues = $mockDesiredValues
                }

                Test-DscParameterState @testParameters | Should -Be $false
            }

            It 'Should return true when only a specified value matches, but other non-listed values do not' {
                $mockCurrentValues = @{ Example = 'test'; SecondExample = 'true' }
                $mockDesiredValues = @{ Example = 'test'; SecondExample = 'false'  }

                $testParameters = @{
                    CurrentValues = $mockCurrentValues
                    DesiredValues = $mockDesiredValues
                    ValuesToCheck = @('Example')
                }

                Test-DscParameterState @testParameters | Should -Be $true
            }

            It 'Should return false when only specified values do not match, but other non-listed values do ' {
                $mockCurrentValues = @{ Example = 'test'; SecondExample = 'true' }
                $mockDesiredValues = @{ Example = 'test'; SecondExample = 'false'  }

                $testParameters = @{
                    CurrentValues = $mockCurrentValues
                    DesiredValues = $mockDesiredValues
                    ValuesToCheck = @('SecondExample')
                }

                Test-DscParameterState @testParameters | Should -Be $false
            }

            It 'Should return false when an empty hash table is used in the current values' {
                $mockCurrentValues = @{ }
                $mockDesiredValues = @{ Example = 'test'; SecondExample = 'false'  }

                $testParameters = @{
                    CurrentValues = $mockCurrentValues
                    DesiredValues = $mockDesiredValues
                }

                Test-DscParameterState @testParameters | Should -Be $false
            }

            It 'Should return true when evaluating a table against a CimInstance' {
                $mockCurrentValues = @{ Handle = '0'; ProcessId = '1000'  }

                $mockWin32ProcessProperties = @{
                    Handle    = 0
                    ProcessId = 1000
                }

                $mockNewCimInstanceParameters = @{
                    ClassName  = 'Win32_Process'
                    Property   = $mockWin32ProcessProperties
                    Key        = 'Handle'
                    ClientOnly = $true
                }

                $mockDesiredValues = New-CimInstance @mockNewCimInstanceParameters

                $testParameters = @{
                    CurrentValues = $mockCurrentValues
                    DesiredValues = $mockDesiredValues
                    ValuesToCheck = @('Handle', 'ProcessId')
                }

                Test-DscParameterState @testParameters | Should -Be $true
            }

            It 'Should return false when evaluating a table against a CimInstance and a value is wrong' {
                $mockCurrentValues = @{ Handle = '1'; ProcessId = '1000'  }

                $mockWin32ProcessProperties = @{
                    Handle    = 0
                    ProcessId = 1000
                }

                $mockNewCimInstanceParameters = @{
                    ClassName  = 'Win32_Process'
                    Property   = $mockWin32ProcessProperties
                    Key        = 'Handle'
                    ClientOnly = $true
                }

                $mockDesiredValues = New-CimInstance @mockNewCimInstanceParameters

                $testParameters = @{
                    CurrentValues = $mockCurrentValues
                    DesiredValues = $mockDesiredValues
                    ValuesToCheck = @('Handle', 'ProcessId')
                }

                Test-DscParameterState @testParameters | Should -Be $false
            }

            It 'Should return true when evaluating a hash table containing an array' {
                $mockCurrentValues = @{ Example = 'test'; SecondExample = @('1', '2') }
                $mockDesiredValues = @{ Example = 'test'; SecondExample = @('1', '2')  }

                $testParameters = @{
                    CurrentValues = $mockCurrentValues
                    DesiredValues = $mockDesiredValues
                }

                Test-DscParameterState @testParameters | Should -Be $true
            }

            It 'Should return false when evaluating a hash table containing an array with wrong values' {
                $mockCurrentValues = @{ Example = 'test'; SecondExample = @('A', 'B') }
                $mockDesiredValues = @{ Example = 'test'; SecondExample = @('1', '2')  }

                $testParameters = @{
                    CurrentValues = $mockCurrentValues
                    DesiredValues = $mockDesiredValues
                }

                Test-DscParameterState @testParameters | Should -Be $false
            }

            It 'Should return false when evaluating a hash table containing an array, but the CurrentValues are missing an array' {
                $mockCurrentValues = @{ Example = 'test' }
                $mockDesiredValues = @{ Example = 'test'; SecondExample = @('1', '2')  }

                $testParameters = @{
                    CurrentValues = $mockCurrentValues
                    DesiredValues = $mockDesiredValues
                }

                Test-DscParameterState @testParameters | Should -Be $false
            }

            It 'Should return false when evaluating a hash table containing an array, but the property i CurrentValues is $null' {
                $mockCurrentValues = @{ Example = 'test'; SecondExample = $null }
                $mockDesiredValues = @{ Example = 'test'; SecondExample = @('1', '2')  }

                $testParameters = @{
                    CurrentValues = $mockCurrentValues
                    DesiredValues = $mockDesiredValues
                }

                Test-DscParameterState @testParameters | Should -Be $false
            }
        }

        Context -Name 'When passing invalid types for DesiredValues' -Fixture {
            It 'Should throw the correct error when DesiredValues is of wrong type' {
                $mockCurrentValues = @{ Example = 'something' }
                $mockDesiredValues = 'NotHashTable'

                $testParameters = @{
                    CurrentValues = $mockCurrentValues
                    DesiredValues = $mockDesiredValues
                }

                $mockCorrectErrorMessage = ($script:localizedData.PropertyTypeInvalidForDesiredValues -f $testParameters.DesiredValues.GetType().Name)
                { Test-DscParameterState @testParameters } | Should -Throw $mockCorrectErrorMessage
            }

            It 'Should write a warning when DesiredValues contain an unsupported type' {
                Mock -CommandName Write-Warning -Verifiable

                # This is a dummy type to test with a type that could never be a correct one.
                class MockUnknownType {
                    [ValidateNotNullOrEmpty()]
                    [System.String]
                    $Property1

                    [ValidateNotNullOrEmpty()]
                    [System.String]
                    $Property2

                    MockUnknownType() {
                    }
                }

                $mockCurrentValues = @{ Example = New-Object -TypeName MockUnknownType }
                $mockDesiredValues = @{ Example = New-Object -TypeName MockUnknownType }

                $testParameters = @{
                    CurrentValues = $mockCurrentValues
                    DesiredValues = $mockDesiredValues
                }

                Test-DscParameterState @testParameters | Should -Be $false

                Assert-MockCalled -CommandName Write-Warning -Exactly -Times 1
            }
        }

        Context -Name 'When passing an CimInstance as DesiredValue and ValuesToCheck is $null' -Fixture {
            It 'Should throw the correct error' {
                $mockCurrentValues = @{ Example = 'something' }

                $mockWin32ProcessProperties = @{
                    Handle    = 0
                    ProcessId = 1000
                }

                $mockNewCimInstanceParameters = @{
                    ClassName  = 'Win32_Process'
                    Property   = $mockWin32ProcessProperties
                    Key        = 'Handle'
                    ClientOnly = $true
                }

                $mockDesiredValues = New-CimInstance @mockNewCimInstanceParameters

                $testParameters = @{
                    CurrentValues = $mockCurrentValues
                    DesiredValues = $mockDesiredValues
                    ValuesToCheck = $null
                }

                $mockCorrectErrorMessage = $script:localizedData.PropertyTypeInvalidForValuesToCheck
                { Test-DscParameterState @testParameters } | Should -Throw $mockCorrectErrorMessage
            }
        }

        Assert-VerifiableMock
    }
}
