Describe "DSC resource tests" -tags 'CI' {
    BeforeAll {
        $DSC_ROOT = $env:DSC_ROOT
        if (-not (Test-Path -Path $DSC_ROOT)) {
            throw "DSC_ROOT environment variable is not set or path does not exist."
        }

        $env:PATH += ";$DSC_ROOT"

        # Ensure DSC v3 is available
        if (-not (Get-Command -name dsc -CommandType Application -ErrorAction SilentlyContinue)) {
            throw "DSC v3 is not installed"
        }

        $dscExe = Get-Command -name dsc -CommandType Application | Select-Object -First 1
    }

    It 'DSC v3 resources can be found' {
        $repoResource = & $dscExe resource list Microsoft.PowerShell.PSResourceGet/Repository -o json | convertfrom-json  | select-object -ExpandProperty type
        $repoResource | Should -BeExactly 'Microsoft.PowerShell.PSResourceGet/Repository'

        $pkgResource = & $dscExe resource list Microsoft.PowerShell.PSResourceGet/PSResourceList -o json | convertfrom-json  | select-object -ExpandProperty type
        $pkgResource | Should -BeExactly 'Microsoft.PowerShell.PSResourceGet/PSResourceList'
    }

    It 'Repository resource has expected properties' {
        $repoResource = & $dscExe resource schema --resource Microsoft.PowerShell.PSResourceGet/Repository -o json | convertfrom-json | select-object -first 1
        $repoResource.properties.name.title | Should -BeExactly 'Name'
        $repoResource.properties.uri.title | Should -BeExactly 'URI'
        $repoResource.properties.trusted.title | Should -BeExactly 'Trusted'
        $repoResource.properties.priority.title | Should -BeExactly 'Priority'
        $repoResource.properties.repositoryType.title | Should -BeExactly 'Repository Type'
        $repoResource.properties._exist.{$ref} | Should -Not -BeNullOrEmpty
    }

    It 'PSResourceList resource has expected properties' {
        $psresourceListResource = & $dscExe resource schema --resource Microsoft.PowerShell.PSResourceGet/PSResourceList -o json | convertfrom-json | select-object -first 1
        $psresourceListResource.properties.repositoryName.title | Should -BeExactly 'Repository Name'
        $psresourceListResource.properties.resources.title | Should -BeExactly 'Resources'
        $psresourceListResource.properties.resources.type| Should -BeExactly 'array'
        $psresourceListResource.properties.resources.minItems | Should -Be 0
        $psresourceListResource.properties.resources.items.{$ref} | Should -BeExactly '#/$defs/PSResource'
    }
}

Describe 'Repository Resource Tests' -Tags 'CI' {
    BeforeAll {
        $DSC_ROOT = $env:DSC_ROOT
        if (-not (Test-Path -Path $DSC_ROOT)) {
            throw "DSC_ROOT environment variable is not set or path does not exist."
        }

        $env:PATH += ";$DSC_ROOT"

        # Ensure DSC v3 is available
        if (-not (Get-Command -name dsc -CommandType Application -ErrorAction SilentlyContinue)) {
            throw "DSC v3 is not installed"
        }

        $dscExe = Get-Command -name dsc -CommandType Application | Select-Object -First 1

         # Register a test repository to ensure DSC can access repositories
        Register-PSResourceRepository -Name 'TestRepo' -uri 'https://www.doesnotexist.com' -ErrorAction SilentlyContinue -APIVersion Local
    }
    AfterAll {
        # Clean up the test repository
        Unregister-PSResourceRepository -Name 'TestRepo' -ErrorAction SilentlyContinue
    }

    It 'Can get a Repository resource instance' {
        $repoParams = @{
            name           = 'TestRepo'
            uri            = 'https://www.doesnotexist.com'
            _exist        = $true
        }

        $resourceInput = $repoParams | ConvertTo-Json -Depth 5

        $getResult = & $dscExe resource get --resource Microsoft.PowerShell.PSResourceGet/Repository --input $resourceInput -o json | ConvertFrom-Json

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
            _exist        = $true
            repositoryType = 'Local'
            priority      = 51
        }

        $resourceInput = $repoParams | ConvertTo-Json -Depth 5

        try {
            & $dscExe resource set --resource Microsoft.PowerShell.PSResourceGet/Repository --input $resourceInput

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
            name           = 'TestRepoToDelete'
            uri            = 'https://www.doesnotexist.com'
            _exist        = $false
        }

        $resourceInput = $repoParams | ConvertTo-Json -Depth 5

        & $dscExe resource delete --resource Microsoft.PowerShell.PSResourceGet/Repository --input $resourceInput

        $repo = Get-PSResourceRepository -Name 'TestRepoToDelete' -ErrorAction SilentlyContinue
        $repo | Should -BeNullOrEmpty
    }
}

