$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

function SetupDsc {
    $script:DSC_ROOT = $env:DSC_ROOT
    if (-not (Test-Path -Path $script:DSC_ROOT)) {
        throw "DSC_ROOT environment variable is not set or path does not exist."
    }

    $pathSeparator = [System.IO.Path]::PathSeparator

    $env:PATH += "$pathSeparator$script:DSC_ROOT"

    # Ensure DSC v3 is available
    if (-not (Get-Command -name dsc -CommandType Application -ErrorAction SilentlyContinue)) {
        throw "DSC v3 is not installed"
    }

    $script:dscExe = Get-Command -name dsc -CommandType Application | Select-Object -First 1

    $expectedModulePath = Join-Path $env:BUILD_SOURCESDIRECTORY 'out'
    $resources = Get-ChildItem $expectedModulePath/*resource.json -ErrorAction SilentlyContinue -Recurse

    if (-not $script:dscExe) {
        throw "Could not find dsc executable in PATH after setup."
    }

    if (-not $resources.Count -ge 2) {
        throw "Expected at least 2 resource schema files in PSResourceGet module directory, found $($resources.Count)."
    }

    $resourcePath = Split-Path $resources[0].FullName -Parent

    Write-Verbose -Verbose "Adding DSC resource path to PATH environment variable: $resourcePath"

    $env:PATH += "$pathSeparator$resourcePath"
}

function SetupTestRepos {
    $script:localRepo = "psgettestlocal"
    $script:testModuleName = "test_local_mod"
    $script:testModuleName2 = "test_local_mod2"
    $script:testModuleName3 = "Test_Local_Mod3"
    Get-NewPSResourceRepositoryFile
    Register-LocalRepos

    New-TestModule -moduleName $script:testModuleName -repoName $script:localRepo -packageVersion "1.0.0" -prereleaseLabel "" -tags @()
    New-TestModule -moduleName $script:testModuleName -repoName $script:localRepo -packageVersion "5.0.0" -prereleaseLabel "" -tags @()
    New-TestModule -moduleName $script:testModuleName2 -repoName $script:localRepo -packageVersion "5.0.0" -prereleaseLabel "" -tags @()
    New-TestModule -moduleName $script:testModuleName3 -repoName $script:localRepo -packageVersion "1.0.0" -prereleaseLabel "" -tags @()
}

Describe "DSC resource schema tests" -tags 'CI' {
    BeforeAll {
        SetupDsc
    }

    It 'DSC v3 resources can be found' {
        $repoResource = & $script:dscExe resource list Microsoft.PowerShell.PSResourceGet/Repository -o json | convertfrom-json  | select-object -ExpandProperty type
        $repoResource | Should -BeExactly 'Microsoft.PowerShell.PSResourceGet/Repository'

        $pkgResource = & $script:dscExe resource list Microsoft.PowerShell.PSResourceGet/PSResourceList -o json | convertfrom-json  | select-object -ExpandProperty type
        $pkgResource | Should -BeExactly 'Microsoft.PowerShell.PSResourceGet/PSResourceList'
    }

    It 'Repository resource has expected properties' {
        $repoResource = & $script:dscExe resource schema --resource Microsoft.PowerShell.PSResourceGet/Repository -o json | convertfrom-json | select-object -first 1
        $repoResource.properties.name.title | Should -BeExactly 'Name'
        $repoResource.properties.uri.title | Should -BeExactly 'URI'
        $repoResource.properties.trusted.title | Should -BeExactly 'Trusted'
        $repoResource.properties.priority.title | Should -BeExactly 'Priority'
        $repoResource.properties.repositoryType.title | Should -BeExactly 'Repository Type'
        $repoResource.properties._exist.'$ref' | Should -Not -BeNullOrEmpty
    }

    It 'PSResourceList resource has expected properties' {
        $psresourceListResource = & $script:dscExe resource schema --resource Microsoft.PowerShell.PSResourceGet/PSResourceList -o json | convertfrom-json | select-object -first 1
        $psresourceListResource.properties.repositoryName.title | Should -BeExactly 'Repository Name'
        $psresourceListResource.properties.trustedRepository.title | Should -BeExactly 'Trusted Repository'
        $psresourceListResource.properties.resources.title | Should -BeExactly 'Resources'
        $psresourceListResource.properties.resources.type | Should -BeExactly 'array'
        $psresourceListResource.properties.resources.minItems | Should -Be 0
        $psresourceListResource.properties.resources.items.'$ref' | Should -BeExactly '#/$defs/PSResource'
    }
}

Describe 'Repository Resource Tests' -Tags 'CI' {
    BeforeAll {
        # Register a test repository to ensure DSC can access repositories
        Register-PSResourceRepository -Name 'TestRepo' -uri 'https://www.doesnotexist.com' -ErrorAction SilentlyContinue -APIVersion Local
    }
    AfterAll {
        # Clean up the test repository
        Unregister-PSResourceRepository -Name 'TestRepo' -ErrorAction SilentlyContinue
    }

    It 'Can get a Repository resource instance' {
        $repoParams = @{
            name   = 'TestRepo'
            uri    = 'https://www.doesnotexist.com'
            _exist = $true
        }

        $resourceInput = $repoParams | ConvertTo-Json -Depth 5

        $getResult = & $script:dscExe resource get --resource Microsoft.PowerShell.PSResourceGet/Repository --input $resourceInput -o json | ConvertFrom-Json

        $getResult.actualState.name | Should -BeExactly 'TestRepo'
        $getResult.actualState.uri | Should -BeExactly 'https://www.doesnotexist.com/'
        $getResult.actualState._exist | Should -Be $true
        $getResult.actualState.trusted | Should -Be $false
        $getResult.actualState.priority | Should -Be 50
        $getResult.actualState.repositoryType | Should -Be 'Local'
    }

    It 'Can set a Repository resource instance' {
        $repoParams = @{
            name           = 'TestRepo2'
            uri            = 'https://www.doesnotexist.com'
            _exist         = $true
            repositoryType = 'Local'
            priority       = 51
        }

        $resourceInput = $repoParams | ConvertTo-Json -Depth 5

        try {
            & $script:dscExe resource set --resource Microsoft.PowerShell.PSResourceGet/Repository --input $resourceInput

            $repo = Get-PSResourceRepository -Name 'TestRepo2' -ErrorAction SilentlyContinue
            $repo.Name | Should -BeExactly 'TestRepo2'
            $repo.Uri.AbsoluteUri | Should -BeExactly 'https://www.doesnotexist.com/'
            $repo.APIVersion | Should -Be 'Local'
            $repo.Priority | Should -Be 51
        }
        finally {
            if ($repo) {
                Unregister-PSResourceRepository -Name 'TestRepo2' -ErrorAction SilentlyContinue
            }
        }
    }

    It 'Can delete a Repository resource instance' {
        # First, create a repository to delete
        Register-PSResourceRepository -Name 'TestRepoToDelete' -uri 'https://www.doesnotexist.com' -ErrorAction SilentlyContinue -APIVersion Local

        $repoParams = @{
            name   = 'TestRepoToDelete'
            uri    = 'https://www.doesnotexist.com'
            _exist = $false
        }

        $resourceInput = $repoParams | ConvertTo-Json -Depth 5

        & $script:dscExe resource delete --resource Microsoft.PowerShell.PSResourceGet/Repository --input $resourceInput

        $repo = Get-PSResourceRepository -Name 'TestRepoToDelete' -ErrorAction SilentlyContinue
        $repo | Should -BeNullOrEmpty
    }
}

Describe "PSResourceList Resource Tests" -Tags 'CI' {
    BeforeAll {
        SetupDsc
        SetupTestRepos
    }
    AfterAll {
        # Clean up the test repository
        Get-RevertPSResourceRepositoryFile
    }

    It 'Can get a PSResourceList resource instance' {
        $psResourceListParams = @{
            repositoryName = $localRepo
            resources      = @()
        }

        $resourceInput = $psResourceListParams | ConvertTo-Json -Depth 5

        $getResult = & $script:dscExe resource get --resource Microsoft.PowerShell.PSResourceGet/PSResourceList --input $resourceInput -o json | ConvertFrom-Json

        $getResult.actualState.repositoryName | Should -BeExactly $localRepo
        $getResult.actualState.resources.Count | Should -Be 0
    }

    It 'Can get a PSResourceList resource instance with resources' {
        $psResourceListParams = @{
            repositoryName = $localRepo
            resources      = @(
                @{
                    name    = $testModuleName
                    version = '1.0.0'
                },
                @{
                    name    = $testModuleName2
                    version = '5.0.0'
                }
            )
        }

        ## Setup expected state by ensuring the modules are not installed
        Uninstall-PSResource -name $script:testModuleName -ErrorAction SilentlyContinue
        Uninstall-PSResource -name $script:testModuleName2 -ErrorAction SilentlyContinue

        $resourceInput = $psResourceListParams | ConvertTo-Json -Depth 5
        $getResult = & $script:dscExe resource get --resource Microsoft.PowerShell.PSResourceGet/PSResourceList --input $resourceInput -o json | ConvertFrom-Json
        $getResult.actualState.repositoryName | Should -BeExactly $script:localRepo
        $getResult.actualState.resources.Count | Should -Be 2
        $getResult.actualState.resources[0].name | Should -BeExactly $script:testModuleName
        $getResult.actualState.resources[0]._exist | Should -BeFalse
        $getResult.actualState.resources[1].name | Should -BeExactly $script:testModuleName2
        $getResult.actualState.resources[1]._exist | Should -BeFalse
    }

    It 'Can set a PSResourceList resource instance with resources' {
        $psResourceListParams = @{
            repositoryName = $script:localRepo
            trustedRepository = $true
            resources      = @(
                @{
                    name    = $script:testModuleName
                    version = '5.0.0'
                },
                @{
                    name    = $script:testModuleName2
                    version = '5.0.0'
                }
            )
        }

        Uninstall-PSResource -name $script:testModuleName -ErrorAction SilentlyContinue
        Uninstall-PSResource -name $script:testModuleName2 -ErrorAction SilentlyContinue

        $resourceInput = $psResourceListParams | ConvertTo-Json -Depth 5
        $setResult = & $script:dscExe resource set --resource Microsoft.PowerShell.PSResourceGet/PSResourceList --input $resourceInput -o json | ConvertFrom-Json
        $setResult.afterState.repositoryName | Should -BeExactly $script:localRepo
        $setResult.afterState.resources.Count | Should -Be 2
        $setResult.afterState.resources[0].name | Should -BeExactly $script:testModuleName
        $setResult.afterState.resources[0].version | Should -BeExactly '5.0.0'
        $setResult.afterState.resources[1].name | Should -BeExactly $script:testModuleName2
        $setResult.afterState.resources[1].version | Should -BeExactly '5.0.0'
    }

    It 'Can test a PSResourceList resource instance with resources' {
        $psResourceListParams = @{
            repositoryName = $script:localRepo
            resources      = @(
                @{
                    name    = $script:testModuleName
                    version = '5.0.0'
                },
                @{
                    name    = $script:testModuleName2
                    version = '5.0.0'
                }
            )
        }

        Install-PSResource -name $script:testModuleName -version '5.0.0' -Repository $script:localRepo -ErrorAction SilentlyContinue -Reinstall -TrustRepository
        Install-PSResource -name $script:testModuleName2 -version '5.0.0' -Repository $script:localRepo -ErrorAction SilentlyContinue -Reinstall -TrustRepository

        $resourceInput = $psResourceListParams | ConvertTo-Json -Depth 5
        $testResult = & $script:dscExe resource test --resource Microsoft.PowerShell.PSResourceGet/PSResourceList --input $resourceInput -o json | ConvertFrom-Json
        $testResult.inDesiredState | Should -BeTrue
    }

    It 'Can test a PSResourceList resource instance that is not in desired state' {
        $psResourceListParams = @{
            repositoryName = $script:localRepo
            resources      = @(
                @{
                    name    = $script:testModuleName
                    version = '9.0.0'
                },
                @{
                    name    = $script:testModuleName2
                    version = '9.0.0'
                }
            )
        }

        $resourceInput = $psResourceListParams | ConvertTo-Json -Depth 5
        $testResult = & $script:dscExe resource test --resource Microsoft.PowerShell.PSResourceGet/PSResourceList --input $resourceInput -o json | ConvertFrom-Json
        $testResult.inDesiredState | Should -BeFalse
    }
}

Describe 'E2E tests for Repository resource' -Tags 'CI' {
    BeforeAll {
        Get-PSResourceRepository -Name 'TestRepository' -ErrorAction SilentlyContinue | Unregister-PSResourceRepository -ErrorAction SilentlyContinue
    }

    It 'Register test repository via DSC configuration' {
        $configPath = Join-Path -Path $PSScriptRoot -ChildPath 'configs/repository.get.dsc.yaml'
        & $dscExe config set -f $configPath
        $repo = Get-PSResourceRepository -Name 'TestRepository' -ErrorAction SilentlyContinue
        $repo.Name | Should -BeExactly 'TestRepository'
        $repo.Uri.AbsoluteUri | Should -BeExactly 'https://www.powershellgallery.com/api/v2'
        $repo.Priority | Should -Be 55
        $repo.Trusted | Should -Be $true
        $repo.ApiVersion | Should -Be 'V2'
    }

    It 'Get test repository via DSC configuration' {
        $configPath = Join-Path -Path $PSScriptRoot -ChildPath 'configs/repository.get.dsc.yaml'
        $out = & $dscExe config set -f $configPath -o json | ConvertFrom-Json
        $out.results.result.afterState.name | Should -BeExactly 'TestRepository'
        $out.results.result.afterState.uri | Should -BeExactly 'https://www.powershellgallery.com/api/v2'
        $out.results.result.afterState._exist | Should -Be $true
        $out.results.result.afterState.trusted | Should -Be $true
        $out.results.result.afterState.priority | Should -Be 55
        $out.results.result.afterState.repositoryType | Should -Be 'V2'
    }

    It 'Export test repository via DSC configuration' {
        $configPath = Join-Path -Path $PSScriptRoot -ChildPath 'configs/repository.export.dsc.yaml'
        $out = & $dscExe config export -f $configPath -o json | ConvertFrom-Json

        # Verify exported file content
        $testRepo = $out.resources.properties | Where-Object { $_.name -eq 'TestRepository' }
        $testRepo | Should -Not -BeNullOrEmpty
        $testRepo.name | Should -BeExactly 'TestRepository'
        $testRepo.uri | Should -BeExactly 'https://www.powershellgallery.com/api/v2'
        $testRepo._exist | Should -Be $true
    }

    It 'Unregister test repository via DSC configuration' {
        $configPath = Join-Path -Path $PSScriptRoot -ChildPath 'configs/repository.unregister.dsc.yaml'
        & $dscExe config set -f $configPath
        $repo = Get-PSResourceRepository -Name 'TestRepository' -ErrorAction SilentlyContinue
        $repo | Should -BeNullOrEmpty
    }
}

Describe 'E2E tests for PSResourceList resource' -Tags 'CI' {
    BeforeAll {
        SetupDsc
    }

    It 'Can Install testmodule99' {
        $mod = Get-PSResource -Name 'testmodule99' -ErrorAction SilentlyContinue -Version '0.0.93'
        if ($mod) {
           $mod | Uninstall-PSResource -ErrorAction SilentlyContinue
        }

        $configPath = Join-Path -Path $PSScriptRoot -ChildPath 'configs/psresourcegetlist.install.dsc.yaml'
        & $script:dscExe config set -f $configPath

        $psresource = Get-PSResource -Name 'testmodule99' -ErrorAction SilentlyContinue -Version '0.0.93'
        $psresource.Name | Should -Be 'testmodule99'
        $psresource.Version | Should -Be '0.0.93'
    }

    It 'Can Uninstall testmodule99' {
        Install-PSResource -Name 'testmodule99' -ErrorAction SilentlyContinue -Repository PSGallery -Reinstall -TrustRepository -Version '0.0.93'

        $configPath = Join-Path -Path $PSScriptRoot -ChildPath 'configs/psresourcegetlist.uninstall.dsc.yaml'
        & $script:dscExe config set -f $configPath

        $psresource = Get-PSResource -Name 'testmodule99' -ErrorAction SilentlyContinue -Version '0.0.93'
        $psresource | Should -BeNullOrEmpty
    }

    It 'Can export PSResourceList with testmodule99' {
        Install-PSResource -Name 'testmodule99' -ErrorAction SilentlyContinue -Repository PSGallery -Reinstall -TrustRepository -Version '0.0.93'

        $configPath = Join-Path -Path $PSScriptRoot -ChildPath 'configs/psresourcegetlist.export.dsc.yaml'
        $out = & $script:dscExe config export -f $configPath -o json | ConvertFrom-Json

        $psResourceList = $out.resources.properties | Where-Object { $_.repositoryName -eq 'PSGallery' }
        $psResourceList | Should -Not -BeNullOrEmpty
        $psResourceList.repositoryName | Should -BeExactly 'PSGallery'
        $psResourceList.resources.Count | Should -BeGreaterThan 0
        $psResourceList.resources.name | Should -Contain 'testmodule99'
    }

    It 'Can Install module with dependency via PSResourceList' {
        $modulelist = @('TestModuleWithDependencyA', 'TestModuleWithDependencyB', 'TestModuleWithDependencyC', 'TestModuleWithDependencyD', 'TestModuleWithDependencyE')
        $mods = Get-PSResource -Name $modulelist -ErrorAction SilentlyContinue
        if ($mods) {
            $mods | Uninstall-PSResource -ErrorAction SilentlyContinue
        }

        $configPath = Join-Path -Path $PSScriptRoot -ChildPath 'configs/psresourcegetlist.moddeps.install.dsc.yaml'
        & $script:dscExe config set -f $configPath

        $psresource = Get-PSResource -Name $modulelist -ErrorAction SilentlyContinue
        $psresource | Should -HaveCount 5
    }

    It 'Can install modules with prerelease versions via PSResourceList' {
        $mod = Get-PSResource -Name 'testmodule99' -ErrorAction SilentlyContinue
        if ($mod) {
           $mod | Uninstall-PSResource -ErrorAction SilentlyContinue
        }

        $configPath = Join-Path -Path $PSScriptRoot -ChildPath 'configs/psresourcegetlist.prerelease.install.dsc.yaml'
        & $script:dscExe config set -f $configPath

        $psresource = Get-PSResource -Name 'testmodule99' -ErrorAction SilentlyContinue
        $psresource | Should -HaveCount 2

        $psresource | ForEach-Object {
            $version = if ($_.prerelease) { "$($_.Version)" + '.' + "$($_.PreRelease)" } else { $_.Version.ToString() }

            $version | Should -BeIn @("101.0.99.beta1", "0.0.93")
        }
    }

    It 'Can install modules with one existing other not' {
        $mod = Get-PSResource -Name 'testmodule99' -ErrorAction SilentlyContinue
        if ($mod) {
           $mod | Uninstall-PSResource -ErrorAction SilentlyContinue
        }

        Install-PSResource -Name 'testmodule99' -ErrorAction SilentlyContinue -Repository PSGallery -Reinstall -TrustRepository -Version '0.0.93'

        $configPath = Join-Path -Path $PSScriptRoot -ChildPath 'configs/psresourcegetlist.oneexisting.install.dsc.yaml'
        & $script:dscExe config set -f $configPath

        $psresource = Get-PSResource -Name 'testmodule99' -ErrorAction SilentlyContinue
        $psresource | Should -HaveCount 2

        $psresource | ForEach-Object {
            $version = if ($_.prerelease) { "$($_.Version)" + '.' + "$($_.PreRelease)" } else { $_.Version.ToString() }

            $version | Should -BeIn @("101.0.99.beta1", "0.0.93")
        }
    }
}

Describe "Error code tests" -Tags 'CI' {

    BeforeAll {
        SetupDsc

        $mod = Get-PSResource -Name 'testmodule99' -ErrorAction SilentlyContinue
        if ($mod) {
           $mod | Uninstall-PSResource -ErrorAction SilentlyContinue
        }
    }

    It 'Repository not found should return error code 2' {
        $out = & $script:dscExe config set -f (Join-Path -Path $PSScriptRoot -ChildPath 'configs/psresourcegetlist.error.norepo.dsc.yaml') 2>&1
        $out[-1] | Should -BeLike "*Repository not found (during set operation)*"
    }

    It 'Repository not trusted should return error code 3' {
        $out = & $script:dscExe config set -f (Join-Path -Path $PSScriptRoot -ChildPath 'configs/psresourcegetlist.error.install.untrustedrepo.dsc.yaml') 2>&1
        $out[-1] | Should -BeLike "*Repository not trusted (during set operation)*"
    }

    It 'Resource not found should return error code 4' {
        $out = & $script:dscExe config set -f (Join-Path -Path $PSScriptRoot -ChildPath 'configs/psresourcegetlist.error.noresource.dsc.yaml') 2>&1
        $out[-1] | Should -BeLike '*Could not install one or more resources (during set operation)*'
    }
}
