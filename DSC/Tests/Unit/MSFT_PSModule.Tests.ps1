#region HEADER
# This must be same name as the root folder, and module manifest.
$script:DSCModuleName = 'DSC'
$script:DSCResourceName = 'MSFT_PSModule'

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
        $mockModuleName = 'MockedModule'
        $mockRepositoryName = 'PSGalleryTest'
        $mockModuleBase = 'TestDrive:\MockPath'

        $mockModule_v1 = New-Object -TypeName Object |
            Add-Member -Name 'Name' -MemberType NoteProperty -Value $mockModuleName -PassThru |
            Add-Member -Name 'Description' -MemberType NoteProperty -Value 'Mocked description' -PassThru |
            Add-Member -Name 'Guid' -MemberType NoteProperty -Value '4c189dbd-d858-4893-bac0-d682423c5fc7' -PassThru |
            Add-Member -Name 'ModuleBase' -MemberType NoteProperty -Value $mockModuleBase -PassThru |
            Add-Member -Name 'ModuleType' -MemberType NoteProperty -Value 'Script' -PassThru |
            Add-Member -Name 'Author' -MemberType NoteProperty -Value 'Mocked Author' -PassThru |
            Add-Member -Name 'Version' -MemberType NoteProperty -Value ([System.Version]'1.0.0.0') -PassThru -Force

        $mockModule_v2 = New-Object -TypeName Object |
            Add-Member -Name 'Name' -MemberType NoteProperty -Value $mockModuleName -PassThru |
            Add-Member -Name 'Description' -MemberType NoteProperty -Value 'Mocked description' -PassThru |
            Add-Member -Name 'Guid' -MemberType NoteProperty -Value '4c189dbd-d858-4893-bac0-d682423c5fc7' -PassThru |
            Add-Member -Name 'ModuleBase' -MemberType NoteProperty -Value $mockModuleBase -PassThru |
            Add-Member -Name 'ModuleType' -MemberType NoteProperty -Value 'Script' -PassThru |
            Add-Member -Name 'Author' -MemberType NoteProperty -Value 'Mocked Author' -PassThru |
            Add-Member -Name 'Version' -MemberType NoteProperty -Value ([System.Version]'2.0.0.0') -PassThru -Force

        $mockGalleryModule = New-Object -TypeName PSCustomObject |
            Add-Member -Name 'Name' -MemberType NoteProperty -Value $mockModuleName -PassThru |
            Add-Member -Name 'Repository' -MemberType NoteProperty -Value 'PSGalleryTest' -PassThru |
            Add-Member -Name 'Version' -MemberType NoteProperty -Value ([System.Version]'3.0.0.0') -PassThru -Force

        $mockGetRightModule_SingleModule = {
            return @($mockModule_v1)
        }

        $mockGetRightModule_MultipleModules = {
            return @(
                $mockModule_v1
                $mockModule_v2
            )
        }

        $mockGetModule_SingleModule = {
            return @($mockModule_v1)
        }

        $mockGetModule_SingleModule2 = {
            return @($mockGalleryModule)
        }

        $mockGetModule_MultipleModules = {
            return @(
                $mockModule_v1
                $mockModule_v2
            )
        }

        $mockGetModuleRepositoryName = {
            return $mockRepositoryName
        }

        $mockGetInstallationPolicy_Trusted = {
            return $true
        }

        $mockGetInstallationPolicy_NotTrusted = {
            return $false
        }

        $mockFindModule = {
            return $mockGalleryModule
        }

        Describe 'MSFT_PSModule\Get-TargetResource' -Tag 'Get','BVT' {
            Context 'When the system is in the desired state' {
                Context 'When the configuration is present' {
                    Context 'When the module is trusted' {
                        BeforeEach {
                            Mock -CommandName Get-RightModule -MockWith $mockGetRightModule_SingleModule
                            Mock -CommandName Get-ModuleRepositoryName -MockWith $mockGetModuleRepositoryName
                        }

                        It 'Should return the same values as passed as parameters' {
                            $getTargetResourceResult = Get-TargetResource -Name $mockModuleName
                            $getTargetResourceResult.Name | Should -Be $mockModuleName

                            Assert-MockCalled -CommandName Get-RightModule -Exactly -Times 1 -Scope It
                            Assert-MockCalled -CommandName Get-ModuleRepositoryName -Exactly -Times 1 -Scope It
                        }

                        It 'Should return the correct values for the other properties' {
                            $getTargetResourceResult = Get-TargetResource -Name $mockModuleName

                            $getTargetResourceResult.Ensure | Should -Be 'Present'
                            $getTargetResourceResult.Repository | Should -Be $mockRepositoryName
                            $getTargetResourceResult.Description | Should -Be $mockModule_v1.Description
                            $getTargetResourceResult.Guid | Should -Be $mockModule_v1.Guid
                            $getTargetResourceResult.ModuleBase | Should -Be $mockModule_v1.ModuleBase
                            $getTargetResourceResult.ModuleType | Should -Be $mockModule_v1.ModuleType
                            $getTargetResourceResult.Author | Should -Be $mockModule_v1.Author
                            $getTargetResourceResult.InstalledVersion | Should -Be $mockModule_v1.Version

                            Assert-MockCalled -CommandName Get-RightModule -Exactly -Times 1 -Scope It
                            Assert-MockCalled -CommandName Get-ModuleRepositoryName -Exactly -Times 1 -Scope It
                        }
                    }

                    Context 'When the module is not trusted' {
                        BeforeEach {
                            Mock -CommandName Get-RightModule -MockWith $mockGetRightModule_SingleModule
                            Mock -CommandName Get-ModuleRepositoryName -MockWith $mockGetModuleRepositoryName
                        }

                        It 'Should return the same values as passed as parameters' {
                            $getTargetResourceResult = Get-TargetResource -Name $mockModuleName
                            $getTargetResourceResult.Name | Should -Be $mockModuleName

                            Assert-MockCalled -CommandName Get-RightModule -Exactly -Times 1 -Scope It
                            Assert-MockCalled -CommandName Get-ModuleRepositoryName -Exactly -Times 1 -Scope It
                       }

                        It 'Should return the correct values for the other properties' {
                            $getTargetResourceResult = Get-TargetResource -Name $mockModuleName

                            $getTargetResourceResult.Ensure | Should -Be 'Present'
                            $getTargetResourceResult.Repository | Should -Be $mockRepositoryName
                            $getTargetResourceResult.Description | Should -Be $mockModule_v1.Description
                            $getTargetResourceResult.Guid | Should -Be $mockModule_v1.Guid
                            $getTargetResourceResult.ModuleBase | Should -Be $mockModule_v1.ModuleBase
                            $getTargetResourceResult.ModuleType | Should -Be $mockModule_v1.ModuleType
                            $getTargetResourceResult.Author | Should -Be $mockModule_v1.Author
                            $getTargetResourceResult.InstalledVersion | Should -Be $mockModule_v1.Version

                            Assert-MockCalled -CommandName Get-RightModule -Exactly -Times 1 -Scope It
                            Assert-MockCalled -CommandName Get-ModuleRepositoryName -Exactly -Times 1 -Scope It
                        }
                    }

                    Context 'When there are multiple version of the same module' {
                        BeforeEach {
                            Mock -CommandName Get-RightModule -MockWith $mockGetRightModule_MultipleModules
                            Mock -CommandName Get-ModuleRepositoryName -MockWith $mockGetModuleRepositoryName
                        }

                        It 'Should return the same values as passed as parameters' {
                            $getTargetResourceResult = Get-TargetResource -Name $mockModuleName
                            $getTargetResourceResult.Name | Should -Be $mockModuleName

                            Assert-MockCalled -CommandName Get-RightModule -Exactly -Times 1 -Scope It
                            Assert-MockCalled -CommandName Get-ModuleRepositoryName -Exactly -Times 1 -Scope It
                        }

                        It 'Should return the correct module version' {
                            $getTargetResourceResult = Get-TargetResource -Name $mockModuleName

                            $getTargetResourceResult.InstalledVersion | Should -Be $mockModule_v2.Version
                        }

                        It 'Should return the correct values for the other properties' {
                            $getTargetResourceResult = Get-TargetResource -Name $mockModuleName

                            $getTargetResourceResult.Ensure | Should -Be 'Present'
                            $getTargetResourceResult.Repository | Should -Be $mockRepositoryName
                            $getTargetResourceResult.Description | Should -Be $mockModule_v2.Description
                            $getTargetResourceResult.Guid | Should -Be $mockModule_v2.Guid
                            $getTargetResourceResult.ModuleBase | Should -Be $mockModule_v2.ModuleBase
                            $getTargetResourceResult.ModuleType | Should -Be $mockModule_v2.ModuleType
                            $getTargetResourceResult.Author | Should -Be $mockModule_v2.Author

                            Assert-MockCalled -CommandName Get-RightModule -Exactly -Times 1 -Scope It
                            Assert-MockCalled -CommandName Get-ModuleRepositoryName -Exactly -Times 1 -Scope It
                        }
                    }
                }

                Context 'When the configuration is absent' {
                    BeforeEach {
                        Mock -CommandName Get-RightModule
                        Mock -CommandName Get-ModuleRepositoryName
                    }

                    It 'Should return the same values as passed as parameters' {
                        $getTargetResourceResult = Get-TargetResource -Name $mockModuleName
                        $getTargetResourceResult.Name | Should -Be $mockModuleName
                        #$getTargetResourceResult.Repository | Should -Be $mockRepositoryName

                        Assert-MockCalled -CommandName Get-RightModule -Exactly -Times 1 -Scope It
                        Assert-MockCalled -CommandName Get-ModuleRepositoryName -Exactly -Times 0 -Scope It
                    }

                    It 'Should return the correct values for the other properties' {
                        $getTargetResourceResult = Get-TargetResource -Name $mockModuleName

                        $getTargetResourceResult.Ensure | Should -Be 'Absent'
                        $getTargetResourceResult.Description | Should -BeNullOrEmpty
                        $getTargetResourceResult.Guid | Should -BeNullOrEmpty
                        $getTargetResourceResult.ModuleBase | Should -BeNullOrEmpty
                        $getTargetResourceResult.ModuleType | Should -BeNullOrEmpty
                        $getTargetResourceResult.Author | Should -BeNullOrEmpty
                        #$getTargetResourceResult.InstalledVersion | Should -BeNullOrEmpty
                        #$getTargetResourceResult.Trusted | Should -BeNullOrEmpty
                        $getTargetResourceResult.Version | Should -BeNullOrEmpty
                        $getTargetResourceResult.NoClobber | Should -Be $false
                        $getTargetResourceResult.SkipPublisherCheck | Should -Be $false

                        Assert-MockCalled -CommandName Get-RightModule -Exactly -Times 1 -Scope It
                        Assert-MockCalled -CommandName Get-ModuleRepositoryName -Exactly -Times 0 -Scope It
                    }
                }
            }
        }

        Describe 'MSFT_PSModule\Set-TargetResource' -Tag 'Set','BVT' {
            Context 'When the system is not in the desired state' {
                Context 'When the configuration should be present' {
                    BeforeAll {
                        Mock -CommandName Find-PSResource -MockWith $mockFindModule
                        Mock -CommandName Install-PSResource -MockWith $mockFindModule
                    }

                    Context 'When the Repository is ''PSGallery''' {
                        BeforeAll {
                            Mock -CommandName Get-InstallationPolicy -MockWith $mockGetInstallationPolicy_Trusted
                            Mock -CommandName Test-ParameterValue
                        }

                        It 'Should call the Install-PSResource with the correct parameters' {
                            { Set-TargetResource -Name $mockModuleName -Repository 'PSGalleryTest' -Version '3.0.0.0'  -verbose} | Should -Not -Throw

                            Assert-MockCalled -CommandName Find-PSResource -ParameterFilter {
                                $Name -eq $mockModuleName -and $Repository -eq 'PSGalleryTest'
                            } -Exactly -Times 1 -Scope It

                            #Assert-MockCalled -CommandName Install-PSResource -ParameterFilter {
                            #    $InputObject.Name -eq $mockModuleName -and $InputObject.Repository -eq 'PSGalleryTest'
                            #} -Exactly -Times 1 -Scope It
                        }
                    }


                    Context 'When the module name cannot be found' {
                        BeforeAll {
                            Mock -CommandName Find-PSResource -MockWith {
                                throw 'Mocked error'
                            }
                        }

                        It 'Should throw the correct error' {
                            { Set-TargetResource -Name $mockModuleName } |
                                Should -Throw ($localizedData.ModuleNotFoundInRepository -f $mockModuleName)
                        }
                    }
                }

                Context 'When the configuration should be absent' {
                    Context 'When uninstalling a module that has a single version' {
                        BeforeAll {
                            Mock -CommandName Get-RightModule -MockWith $mockGetRightModule_SingleModule
                            Mock -CommandName Remove-Item
                        }

                        It 'Should call the Remove-Item with the correct parameters' {
                            { Set-TargetResource -Name $mockModuleName -Ensure 'Absent' } | Should -Not -Throw

                            Assert-MockCalled -CommandName Get-RightModule -Exactly -Times 1 -Scope It
                            Assert-MockCalled -CommandName Remove-Item -ParameterFilter {
                                $path -eq $mockModuleBase
                            } -Exactly -Times 1 -Scope It
                        }
                    }

                    Context 'When the module name cannot be found' {
                        BeforeAll {
                            Mock -CommandName Get-RightModule
                        }

                        It 'Should throw the correct error' {
                            { Set-TargetResource -Name $mockModuleName -Ensure 'Absent' } |
                                Should -Throw ($localizedData.ModuleWithRightPropertyNotFound -f $mockModuleName)
                        }
                    }

                    Context 'When a module cannot be removed' {
                        BeforeAll {
                            Mock -CommandName Get-RightModule -MockWith $mockGetRightModule_SingleModule
                            Mock -CommandName Remove-Item -MockWith {
                                throw 'Mock fail to remove module error'
                            }
                        }

                        It 'Should throw the correct error' {
                            { Set-TargetResource -Name $mockModuleName -Ensure 'Absent' } |
                                Should -Throw ($localizedData.FailToUninstall -f $mockModuleName)
                        }
                    }
                }
            }
        }

        Describe 'MSFT_PSModule\Test-TargetResource' -Tag 'Test','BVT' {
            Context 'When the system is in the desired state' {
                Context 'When the configuration is present' {
                    BeforeEach {
                        Mock -CommandName Get-TargetResource -MockWith {
                            return @{
                                Ensure             = 'Present'
                                Name               = $mockModuleName
                                Repository         = $mockRepositoryName
                                Description        = $mockModule_v1.Description
                                Guid               = $mockModule_v1.Guid
                                ModuleBase         = $mockModule_v1.ModuleBase
                                ModuleType         = $mockModule_v1.ModuleType
                                Author             = $mockModule_v1.Author
                                InstalledVersion   = $mockModule_v1.Version
                                #InstallationPolicy = 'Untrusted'
                            }
                        }
                    }

                    It 'Should return the state as $true' {
                        $testTargetResourceResult = Test-TargetResource -Name $mockModuleName
                        $testTargetResourceResult | Should -Be $true

                        Assert-MockCalled -CommandName Get-TargetResource -Exactly -Times 1 -Scope It
                    }
                }

                Context 'When the configuration is absent' {
                    BeforeEach {
                        Mock -CommandName Get-TargetResource -MockWith {
                            return @{
                                Ensure             = 'Absent'
                                Name               = $mockModuleName
                                Repository         = $null
                                Description        = $null
                                Guid               = $null
                                ModuleBase         = $null
                                ModuleType         = $null
                                Author             = $null
                                InstalledVersion   = $null
                                #InstallationPolicy = $null
                            }
                        }
                    }

                    It 'Should return the state as $true' {
                        $testTargetResourceResult = Test-TargetResource -Ensure 'Absent' -Name $mockModuleName
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
                                Ensure             = 'Absent'
                                Name               = $mockModuleName
                                Repository         = $mockRepositoryName
                                Description        = $mockModule_v1.Description
                                Guid               = $mockModule_v1.Guid
                                ModuleBase         = $mockModule_v1.ModuleBase
                                ModuleType         = $mockModule_v1.ModuleType
                                Author             = $mockModule_v1.Author
                                InstalledVersion   = $mockModule_v1.Version
                                #InstallationPolicy = 'Untrusted'
                            }
                        }
                    }

                    It 'Should return the state as $false' {
                        $testTargetResourceResult = Test-TargetResource -Name $mockModuleName
                        $testTargetResourceResult | Should -Be $false

                        Assert-MockCalled -CommandName Get-TargetResource -Exactly -Times 1 -Scope It
                    }
                }

                Context 'When the configuration should be absent' {
                    BeforeEach {
                        Mock -CommandName Get-TargetResource -MockWith {
                            return @{
                                Ensure             = 'Present'
                                Name               = $mockModuleName
                                Repository         = $null
                                Description        = $null
                                Guid               = $null
                                ModuleBase         = $null
                                ModuleType         = $null
                                Author             = $null
                                InstalledVersion   = $null
                                # = $null
                            }
                        }
                    }

                    It 'Should return the state as $false' {
                        $testTargetResourceResult = Test-TargetResource -Ensure 'Absent' -Name $mockModuleName
                        $testTargetResourceResult | Should -Be $false

                        Assert-MockCalled -CommandName Get-TargetResource -Exactly -Times 1 -Scope It
                    }
                }
            }
        }

        Describe 'MSFT_PSModule\Get-ModuleRepositoryName' -Tag 'Helper' {
            BeforeAll {
                # Mock the file PSGetModuleInfo.xml in the module base folder.
                New-Item -Path $mockModuleBase -ItemType File -Name 'PSGetModuleInfo.xml' -Force

                $mockModule = New-Object -TypeName Object |
                    Add-Member -Name 'ModuleBase' -MemberType NoteProperty -Value $mockModuleBase -PassThru -Force

                Mock -CommandName Import-Clixml -MockWith {
                    return New-Object -TypeName Object |
                        Add-Member -Name 'Repository' -MemberType NoteProperty -Value $mockRepositoryName -PassThru -Force
                }
            }

            It 'Should return the correct repository name of a module' {
                Get-ModuleRepositoryName -Module $mockModule | Should -Be $mockRepositoryName

                Assert-MockCalled -CommandName 'Import-Clixml' -Exactly -Times 1 -Scope It
            }
        }

        Describe 'MSFT_PSModule\Get-RightModule' -Tag 'Helper' {
            BeforeEach {
                Mock -CommandName Get-ModuleRepositoryName -MockWith $mockGetModuleRepositoryName
            }

            Context 'When the module does not exist' {
                BeforeEach {
                    Mock -CommandName Get-Module
                }

                It 'Should return $null' {
                    Get-RightModule -Name 'UnknownModule' | Should -BeNullOrEmpty

                    Assert-MockCalled -CommandName 'Get-Module' -Exactly -Times 1 -Scope It
                }
            }

            Context 'When one version of the module exist' {
                BeforeEach {
                    Mock -CommandName Get-Module -MockWith $mockGetModule_SingleModule
                }

                It 'Should return the correct module information' {
                    $getRightModuleResult = Get-RightModule -Name $mockModuleName
                    $getRightModuleResult.Name | Should -Be $mockModuleName

                    Assert-MockCalled -CommandName 'Get-ModuleRepositoryName' -Exactly -Times 0 -Scope It
                    Assert-MockCalled -CommandName 'Get-Module' -ParameterFilter {
                        $Name -eq $mockModuleName
                    } -Exactly -Times 1 -Scope It
                }
            }

            Context 'When parameter Repository is specified, and one version of the module exist' {
                BeforeEach {
                    Mock -CommandName Get-Module -MockWith $mockGetModule_SingleModule
                }

                It 'Should return the correct module information' {
                    $getRightModuleResult = Get-RightModule -Name $mockModuleName -Repository $mockRepositoryName -Verbose
                    $getRightModuleResult.Name | Should -Be $mockModuleName

                    Assert-MockCalled -CommandName 'Get-ModuleRepositoryName' -Exactly -Times 1 -Scope It
                    Assert-MockCalled -CommandName 'Get-Module' -ParameterFilter {
                        $Name -eq $mockModuleName
                    } -Exactly -Times 1 -Scope It
                }
            }

            Context 'When parameter Repository is specified, and one version of the module exist, but from a different repository' {
                BeforeEach {
                    Mock -CommandName Get-Module -MockWith $mockGetModule_SingleModule
                }

                It 'Should return $null' {
                    $getRightModuleResult = Get-RightModule -Name $mockModuleName -Repository 'OtherRepository' -Verbose
                    $getRightModuleResult.Name | Should -BeNullOrEmpty

                    Assert-MockCalled -CommandName 'Get-ModuleRepositoryName' -Exactly -Times 1 -Scope It
                    Assert-MockCalled -CommandName 'Get-Module' -ParameterFilter {
                        $Name -eq $mockModuleName
                    } -Exactly -Times 1 -Scope It
                }
            }

            Context 'When the module is required to have a specific version, and the specific version is installed' {
                BeforeEach {
                    Mock -CommandName Get-Module -MockWith $mockGetModule_SingleModule2
                }

               # It 'Should return the correct module information' {
               #     $getRightModuleResult = Get-RightModule -Name $mockModuleName -Version '3.0.0.0' -Repository 'PSGalleryTest' -Verbose
               #     $getRightModuleResult.Name  | Should -Be $mockModuleName

               #     Assert-MockCalled -CommandName 'Get-ModuleRepositoryName' -Exactly -Times 0 -Scope It
               #     Assert-MockCalled -CommandName 'Get-Module' -ParameterFilter {
               #         $Name -eq $mockModuleName
               #     } -Exactly -Times 1 -Scope It
               # }
            }

            Context 'When the module is required to have a specific version, and the specific version is not installed' {
                BeforeEach {
                    Mock -CommandName Get-Module -MockWith $mockGetModule_SingleModule
                }

                It 'Should return $null' {
                    $getRightModuleResult = Get-RightModule -Name $mockModuleName -Version '2.0.0.0' -Verbose
                    $getRightModuleResult.Name | Should -BeNullOrEmpty

                    Assert-MockCalled -CommandName 'Get-ModuleRepositoryName' -Exactly -Times 0 -Scope It
                    Assert-MockCalled -CommandName 'Get-Module' -ParameterFilter {
                        $Name -eq $mockModuleName
                    } -Exactly -Times 1 -Scope It
                }
            }

            Context 'When the module is required to have a maximum version, and a suitable module is installed' {
                BeforeEach {
                    Mock -CommandName Get-Module -MockWith $mockGetModule_SingleModule
                }

            }

            Context 'When the module is required to have a minimum version, and a suitable module is installed' {
                BeforeEach {
                    Mock -CommandName Get-Module -MockWith $mockGetModule_SingleModule
                }

            }

            Context 'When the module is required to have a maximum version, and the suitable module is not installed' {
                BeforeEach {
                    Mock -CommandName Get-Module -MockWith $mockGetModule_SingleModule
                }

            }

            Context 'When the module is required to have a minimum version, and the suitable module is not installed' {
                BeforeEach {
                    Mock -CommandName Get-Module -MockWith $mockGetModule_SingleModule
                }

            }

            Context 'When the module is required to have a minimum and maximum version' {
                Context 'When a suitable module is installed' {
                    BeforeEach {
                        Mock -CommandName Get-Module -MockWith $mockGetModule_SingleModule
                    }

                }

                Context 'When there two suitable modules installed' {
                    BeforeEach {
                        Mock -CommandName Get-Module -MockWith $mockGetModule_MultipleModules
                    }

                }


                Context 'When there two installed modules, but only one module is suitable' {
                    BeforeEach {
                        Mock -CommandName Get-Module -MockWith $mockGetModule_MultipleModules
                    }
                }
            }
        }
    }
}
finally {
    Invoke-TestCleanup
}
