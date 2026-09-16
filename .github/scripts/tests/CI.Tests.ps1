# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$scriptsPath = Split-Path $PSScriptRoot -Parent
$root = Split-Path (Split-Path $scriptsPath -Parent) -Parent
$testFiles = @(Get-ChildItem (Join-Path $root 'test') -Recurse -Filter '*.Tests.ps1' -File)

Describe 'CI workflow coverage' {
    BeforeAll {
        $workflow = Get-Content (Join-Path $root '.github/workflows/ci.yml') -Raw
        $testWorkflow = Get-Content (Join-Path $root '.github/workflows/ci-tests.yml') -Raw
    }

    It 'invokes the same test workflow without event or branch conditions' {
        $workflow | Should -Match 'uses: \./\.github/workflows/ci-tests\.yml'
        $workflow | Should -Not -Match '(?m)^\s+if:'
        $workflow | Should -Not -Match '(?m)^concurrency:'
        $testWorkflow | Should -Not -Match '(?m)^    if:'
    }

    It 'runs the complete four-platform suite plus the Windows AzAuth suite' {
        [regex]::Matches($testWorkflow, '(?m)^          - name:').Count | Should -Be 5
        [regex]::Matches($testWorkflow, '(?m)^            azAuth: false').Count | Should -Be 4
        [regex]::Matches($testWorkflow, '(?m)^            azAuth: true').Count | Should -Be 1
        $testWorkflow | Should -Match ([regex]::Escape('CI_SUITE: ${{ matrix.azAuth && ''AzAuth'' || ''Full'' }}'))
        $testWorkflow | Should -Match '(?m)^    environment: ci-integration'
    }
}

Describe 'CI test execution' {
    BeforeAll {
        function az {}
        function Set-SecretStoreConfiguration { param($Authentication, $Interaction, $Confirm) }
        function Register-SecretVault { param($Name, $ModuleName, [switch] $DefaultVault) }
        function Set-Secret { param($Name, $Secret, $Vault) }
        $environmentNames = @(
            'TENANTID', 'GITHUB_USERNAME', 'ADO_USERNAME', 'MAPPED_GITHUB_PAT',
            'MAPPED_ADO_PUBLIC_PAT', 'MAPPED_ADO_PRIVATE_PAT', 'MAPPED_ADO_PRIVATE_REPO_URL',
            'USINGAZAUTH', 'VSS_NUGET_EXTERNAL_FEED_ENDPOINTS'
        )
        $savedEnvironment = @{}
        foreach ($name in $environmentNames) {
            $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
        }
        $savedExitCode = $global:LASTEXITCODE
        $state = @{}
    }

    BeforeEach {
        foreach ($name in $environmentNames) {
            [Environment]::SetEnvironmentVariable($name, 'ci-helper-test-value')
        }
        $state.SelectedFiles = @()
        $state.Result = [pscustomobject]@{ TotalCount = 1; FailedCount = 0 }
        Mock Import-Module {}
        Mock Set-Location {}
        Mock New-Item {}
        Mock Test-Path { $true }
        Mock Get-ChildItem { $testFiles }
        Mock Get-Content { '{"TestPath":"test","BuildOutputPath":"out","ModuleName":"Microsoft.PowerShell.PSResourceGet"}' }
        Mock Invoke-Pester {
            $state.SelectedFiles = @($Script)
            $state.Result
        }
        Mock az {
            $global:LASTEXITCODE = 0
            'ci-helper-test-token'
        }
        Mock Set-SecretStoreConfiguration {}
        Mock Register-SecretVault {}
        Mock Set-Secret {}
    }

    AfterAll {
        foreach ($name in $environmentNames) {
            [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name])
        }
        $global:LASTEXITCODE = $savedExitCode
    }

    It 'does not offer a reduced public suite' {
        { & (Join-Path $scriptsPath 'Invoke-CITests.ps1') -Suite Public } | Should -Throw
        Assert-MockCalled Invoke-Pester -Times 0 -Exactly -Scope It
    }

    It 'runs every file with the full authenticated suite' {
        & (Join-Path $scriptsPath 'Invoke-CITests.ps1') -Suite Full
        $state.SelectedFiles.Count | Should -Be $testFiles.Count
        Assert-MockCalled Invoke-Pester -Times 1 -Exactly -Scope It -ParameterFilter {
            $Tag -eq 'CI' -and $ExcludeTag -eq 'ManualValidationOnly' -and $OutputFormat -eq 'NUnitXml' -and $PassThru
        }
        Assert-MockCalled Set-Secret -Times 1 -Exactly -Scope It
        Assert-MockCalled az -Times 2 -Exactly -Scope It
        $endpoint = ($env:VSS_NUGET_EXTERNAL_FEED_ENDPOINTS | ConvertFrom-Json).endpointCredentials[0]
        $endpoint.endpoint | Should -Be 'https://pkgs.dev.azure.com/powershell-rel/PSResourceGet/_packaging/psrg-credprovidertest/nuget/v2'
        $endpoint.password | Should -Be 'ci-helper-test-token'
        $env:USINGAZAUTH | Should -Be 'false'
    }

    It 'runs just the three ACR files with AzAuth and no SecretStore' {
        & (Join-Path $scriptsPath 'Invoke-CITests.ps1') -Suite AzAuth
        $state.SelectedFiles.Count | Should -Be 3
        ($state.SelectedFiles -match 'ContainerRegistryServer').Count | Should -Be 3
        $env:USINGAZAUTH | Should -Be 'true'
        Assert-MockCalled Set-Secret -Times 0 -Exactly -Scope It
        Assert-MockCalled az -Times 0 -Exactly -Scope It
    }

    It 'fails when a required credential is absent' {
        $env:MAPPED_GITHUB_PAT = ''
        { & (Join-Path $scriptsPath 'Invoke-CITests.ps1') -Suite Full } | Should -Throw 'Missing MAPPED_GITHUB_PAT'
        Assert-MockCalled Invoke-Pester -Times 0 -Exactly -Scope It
    }

    It 'fails when Azure token acquisition fails' {
        Mock az { $global:LASTEXITCODE = 1 }
        { & (Join-Path $scriptsPath 'Invoke-CITests.ps1') -Suite Full } | Should -Throw 'Could not acquire'
        Assert-MockCalled Invoke-Pester -Times 0 -Exactly -Scope It
    }

    It 'fails the job when a Pester test fails' {
        $state.Result.FailedCount = 1
        { & (Join-Path $scriptsPath 'Invoke-CITests.ps1') -Suite Full } | Should -Throw '1 Pester tests failed'
    }

    It 'fails the job if no tests ran' {
        $state.Result.TotalCount = 0
        { & (Join-Path $scriptsPath 'Invoke-CITests.ps1') -Suite Full } | Should -Throw 'did not produce test results'
    }

    It 'fails the job if the XML report is missing' {
        Mock Test-Path { $false }
        { & (Join-Path $scriptsPath 'Invoke-CITests.ps1') -Suite Full } | Should -Throw 'did not produce test results'
    }

    It 'does not use an expected native failure as the successful suite exit code' {
        Mock Invoke-Pester {
            $global:LASTEXITCODE = 1
            $state.Result
        }
        & (Join-Path $scriptsPath 'Invoke-CITests.ps1') -Suite Full
        $global:LASTEXITCODE | Should -Be 0
    }
}

Describe 'CI ACR cleanup' {
    BeforeAll {
        function az {}
        $savedExitCode = $global:LASTEXITCODE
        $state = @{}
    }
    BeforeEach {
        $state.PackageName = 'temp-testmodule12345678-1234-1234-1234-123456789abc'
        Mock Test-Path { $true }
        Mock Get-Content { $state.PackageName }
        Mock az {
            $global:LASTEXITCODE = 0
            ConvertTo-Json -InputObject @($state.PackageName, 'fixture-package')
        }
    }
    AfterAll {
        $global:LASTEXITCODE = $savedExitCode
    }

    It 'deletes only a generated repository recorded by this job' {
        & (Join-Path $scriptsPath 'Remove-CITestRepositories.ps1')
        Assert-MockCalled az -Times 1 -Exactly -Scope It -ParameterFilter {
            $args[2] -eq 'delete' -and $args[6] -eq $state.PackageName
        }
    }

    It 'refuses to delete fixture repositories' {
        $state.PackageName = 'fixture-package'
        { & (Join-Path $scriptsPath 'Remove-CITestRepositories.ps1') } | Should -Throw 'Refusing to delete'
        Assert-MockCalled az -Times 0 -Exactly -Scope It -ParameterFilter { $args[2] -eq 'delete' }
    }

    It 'does nothing when publishing never produced a repository' {
        Mock Get-Content { @() }
        & (Join-Path $scriptsPath 'Remove-CITestRepositories.ps1')
        Assert-MockCalled az -Times 0 -Exactly -Scope It
    }

    It 'does not delete a recorded package that was never published' {
        Mock az {
            $global:LASTEXITCODE = 0
            '["fixture-package"]'
        }
        & (Join-Path $scriptsPath 'Remove-CITestRepositories.ps1')
        Assert-MockCalled az -Times 0 -Exactly -Scope It -ParameterFilter { $args[2] -eq 'delete' }
    }

    It 'fails explicitly if cleanup cannot access ACR' {
        Mock az { $global:LASTEXITCODE = 1 }
        { & (Join-Path $scriptsPath 'Remove-CITestRepositories.ps1') } | Should -Throw 'Could not list ACR'
    }
}
