<#
    .SYNOPSIS
        Automated unit test for helper functions in module PowerShellGet.ResourceHelper.
#>


$script:helperModuleName = 'PowerShellGet.LocalizationHelper'

Describe "$script:helperModuleName Unit Tests" {
    BeforeAll {
        $resourceModuleRoot = Split-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -Parent
        $dscResourcesFolderFilePath = Join-Path -Path (Join-Path -Path $resourceModuleRoot -ChildPath 'Modules') `
            -ChildPath $script:helperModuleName

        Import-Module -Name (Join-Path -Path $dscResourcesFolderFilePath `
                -ChildPath "$script:helperModuleName.psm1") -Force
    }

    InModuleScope $script:helperModuleName {
        Describe 'Get-LocalizedData' {
            $mockTestPath = {
                return $mockTestPathReturnValue
            }

            $mockImportLocalizedData = {
                $BaseDirectory | Should -Be $mockExpectedLanguagePath
            }

            BeforeEach {
                Mock -CommandName Test-Path -MockWith $mockTestPath -Verifiable
                Mock -CommandName Import-LocalizedData -MockWith $mockImportLocalizedData -Verifiable
            }

            Context 'When loading localized data for Swedish' {
                $mockExpectedLanguagePath = 'sv-SE'
                $mockTestPathReturnValue = $true

                It 'Should call Import-LocalizedData with sv-SE language' {
                    Mock -CommandName Join-Path -MockWith {
                        return 'sv-SE'
                    } -Verifiable

                    { Get-LocalizedData -ResourceName 'DummyResource' } | Should -Not -Throw

                    Assert-MockCalled -CommandName Join-Path -Exactly -Times 2 -Scope It
                    Assert-MockCalled -CommandName Test-Path -Exactly -Times 1 -Scope It
                    Assert-MockCalled -CommandName Import-LocalizedData -Exactly -Times 1 -Scope It
                }

                $mockExpectedLanguagePath = 'en-US'
                $mockTestPathReturnValue = $false

                It 'Should call Import-LocalizedData and fallback to en-US if sv-SE language does not exist' {
                    Mock -CommandName Join-Path -MockWith {
                        return $ChildPath
                    } -Verifiable

                    { Get-LocalizedData -ResourceName 'DummyResource' } | Should -Not -Throw

                    Assert-MockCalled -CommandName Join-Path -Exactly -Times 3 -Scope It
                    Assert-MockCalled -CommandName Test-Path -Exactly -Times 1 -Scope It
                    Assert-MockCalled -CommandName Import-LocalizedData -Exactly -Times 1 -Scope It
                }

                Context 'When $ScriptRoot is set to a path' {
                    $mockExpectedLanguagePath = 'sv-SE'
                    $mockTestPathReturnValue = $true

                    It 'Should call Import-LocalizedData with sv-SE language' {
                        Mock -CommandName Join-Path -MockWith {
                            return 'sv-SE'
                        } -Verifiable

                        { Get-LocalizedData -ResourceName 'DummyResource' -ScriptRoot '.' } | Should -Not -Throw

                        Assert-MockCalled -CommandName Join-Path -Exactly -Times 1 -Scope It
                        Assert-MockCalled -CommandName Test-Path -Exactly -Times 1 -Scope It
                        Assert-MockCalled -CommandName Import-LocalizedData -Exactly -Times 1 -Scope It
                    }

                    $mockExpectedLanguagePath = 'en-US'
                    $mockTestPathReturnValue = $false

                    It 'Should call Import-LocalizedData and fallback to en-US if sv-SE language does not exist' {
                        Mock -CommandName Join-Path -MockWith {
                            return $ChildPath
                        } -Verifiable

                        { Get-LocalizedData -ResourceName 'DummyResource' -ScriptRoot '.' } | Should -Not -Throw

                        Assert-MockCalled -CommandName Join-Path -Exactly -Times 2 -Scope It
                        Assert-MockCalled -CommandName Test-Path -Exactly -Times 1 -Scope It
                        Assert-MockCalled -CommandName Import-LocalizedData -Exactly -Times 1 -Scope It
                    }
                }
            }

            Context 'When loading localized data for English' {
                Mock -CommandName Join-Path -MockWith {
                    return 'en-US'
                } -Verifiable

                $mockExpectedLanguagePath = 'en-US'
                $mockTestPathReturnValue = $true

                It 'Should call Import-LocalizedData with en-US language' {
                    { Get-LocalizedData -ResourceName 'DummyResource' } | Should -Not -Throw
                }
            }

            Assert-VerifiableMock
        }

        Describe 'New-InvalidResultException' {
            Context 'When calling with Message parameter only' {
                It 'Should throw the correct error' {
                    $mockErrorMessage = 'Mocked error'

                    { New-InvalidResultException -Message $mockErrorMessage } | Should -Throw $mockErrorMessage
                }
            }

            Context 'When calling with both the Message and ErrorRecord parameter' {
                It 'Should throw the correct error' {
                    $mockErrorMessage = 'Mocked error'
                    $mockExceptionErrorMessage = 'Mocked exception error message'

                    $mockException = New-Object -TypeName System.Exception -ArgumentList $mockExceptionErrorMessage
                    $mockErrorRecord = New-Object -TypeName System.Management.Automation.ErrorRecord -ArgumentList $mockException, $null, 'InvalidResult', $null

                    $exception = { New-InvalidResultException -Message $mockErrorMessage -ErrorRecord $mockErrorRecord -ErrorAction SilentlyContinue}
                    $exception | Should -Throw -PassThru $_ | Should -Match ('System.Exception: {0}' -f $mockErrorMessage) 
                    $exception | Should -Throw -PassThru $_ | Should -Match ('System.Exception: {0}' -f $mockExceptionErrorMessage)
                       }
            }

            Assert-VerifiableMock
        }

        Describe 'New-ObjectNotFoundException' {
            Context 'When calling with Message parameter only' {
                It 'Should throw the correct error' {
                    $mockErrorMessage = 'Mocked error'

                    { New-ObjectNotFoundException -Message $mockErrorMessage } | Should -Throw $mockErrorMessage
                }
            }

            Context 'When calling with both the Message and ErrorRecord parameter' {
                It 'Should throw the correct error' {
                    $mockErrorMessage = 'Mocked error'
                    $mockExceptionErrorMessage = 'Mocked exception error message'

                    $mockException = New-Object -TypeName System.Exception -ArgumentList $mockExceptionErrorMessage
                    $mockErrorRecord = New-Object -TypeName System.Management.Automation.ErrorRecord -ArgumentList $mockException, $null, 'InvalidResult', $null

                    $exception = { New-ObjectNotFoundException -Message $mockErrorMessage -ErrorRecord $mockErrorRecord }
                    $exception | Should -Throw -PassThru $_ | Should -Match ('System.Exception: {0}' -f $mockErrorMessage) 
                    $exception | Should -Throw -PassThru $_ | Should -Match ('System.Exception: {0}' -f $mockExceptionErrorMessage)
                     }
            }

            Assert-VerifiableMock
        }

        Describe 'New-InvalidOperationException' {
            Context 'When calling with Message parameter only' {
                It 'Should throw the correct error' {
                    $mockErrorMessage = 'Mocked error'

                    { New-InvalidOperationException -Message $mockErrorMessage } | Should -Throw $mockErrorMessage
                }
            }

            Context 'When calling with both the Message and ErrorRecord parameter' {
                It 'Should throw the correct error' {
                    $mockErrorMessage = 'Mocked error'
                    $mockExceptionErrorMessage = 'Mocked exception error message'

                    $mockException = New-Object -TypeName System.Exception -ArgumentList $mockExceptionErrorMessage
                    $mockErrorRecord = New-Object -TypeName System.Management.Automation.ErrorRecord -ArgumentList $mockException, $null, 'InvalidResult', $null

                    $exception = {  New-InvalidOperationException -Message $mockErrorMessage -ErrorRecord $mockErrorRecord }
                    $exception | Should -Throw -PassThru $_ | Should -Match ('System.InvalidOperationException: {0}' -f $mockErrorMessage) 
                    $exception | Should -Throw -PassThru $_ | Should -Match ('System.Exception: {0}' -f $mockExceptionErrorMessage)
                }
            }

            Assert-VerifiableMock
        }

        Describe 'New-InvalidArgumentException' {
            Context 'When calling with both the Message and ArgumentName parameter' {
                It 'Should throw the correct error' {
                    $mockErrorMessage = 'Mocked error'
                    $mockArgumentName = 'MockArgument'

                    $exception = { New-InvalidArgumentException -Message $mockErrorMessage -ArgumentName $mockArgumentName }
                    $exception | Should -Throw -PassThru $_ | Should -Match ("Parameter '{0}'" -f $mockArgumentName)
                }
            }

            Assert-VerifiableMock
        }
    }
}
