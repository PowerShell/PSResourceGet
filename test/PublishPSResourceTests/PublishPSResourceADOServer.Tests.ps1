# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$testDir = (get-item $psscriptroot).parent.FullName

function CreateTestModule
{
    param (
        [string] $Path = "$TestDrive",
        [string] $ModuleName = 'TestModule'
    )

    $modulePath = Join-Path -Path $Path -ChildPath $ModuleName
    $moduleMan = Join-Path $modulePath -ChildPath ($ModuleName + '.psd1')
    $moduleSrc = Join-Path $modulePath -ChildPath ($ModuleName + '.psm1')

    if ( Test-Path -Path $modulePath) {
        Remove-Item -Path $modulePath -Recurse -Force
    }

    $null = New-Item -Path $modulePath -ItemType Directory -Force

    @'
    @{{
        RootModule        = "{0}.psm1"
        ModuleVersion     = '1.0.0'
        Author            = 'None'
        Description       = 'None'
        GUID              = '0c2829fc-b165-4d72-9038-ae3a71a755c1'
        FunctionsToExport = @('Test1')
        RequiredModules   = @('NonExistentModule')
    }}
'@ -f $ModuleName | Out-File -FilePath $moduleMan

    @'
    function Test1 {
        Write-Output 'Hello from Test1'
    }
'@ | Out-File -FilePath $moduleSrc
}

Describe "Test Publish-PSResource" -tags 'CI' {
    BeforeAll {
        Get-NewPSResourceRepositoryFile

        $testModuleName = "test_local_mod"
        $ADOPublicRepoName = "PSGetTestingPublicFeed"
        $ADOPublicRepoUri = "https://pkgs.dev.azure.com/powershell/PowerShell/_packaging/psresourceget-public-test-ci/nuget/v3/index.json"
        Register-PSResourceRepository -Name $ADOPublicRepoName -Uri $ADOPublicRepoUri

        $ADOPrivateRepoName = "PSGetTestFeedWithPrivateAccess"
        $ADOPrivateRepoUri = $env:MAPPED_ADO_PRIVATE_REPO_URL
        Register-PSResourceRepository -Name $ADOPrivateRepoName -Uri $ADOPrivateRepoUri

        $secureString = ConvertTo-SecureString $env:MAPPED_ADO_PUBLIC_PAT -AsPlainText -Force
        $correctPublicRepoCred = New-Object pscredential ($env:ADO_USERNAME, $secureString)

        $secureString = ConvertTo-SecureString $env:MAPPED_ADO_PRIVATE_PAT -AsPlainText -Force
        $correctPrivateRepoCred = New-Object pscredential ($env:ADO_USERNAME, $secureString)

        $randomString = ([System.Guid]::NewGuid()).ToString()
        $secureString = ConvertTo-SecureString $randomString -AsPlainText -Force
        $incorrectRepoCred = New-Object pscredential ($env:ADO_USERNAME, $secureString)

        # Create module
        $script:tmpModulesPath = Join-Path -Path $TestDrive -ChildPath "tmpModulesPath"
        $script:PublishModuleName = "PSGetTestModule"
        $script:PublishModuleBase = Join-Path $script:tmpModulesPath -ChildPath $script:PublishModuleName
        if(!(Test-Path $script:PublishModuleBase))
        {
            New-Item -Path $script:PublishModuleBase -ItemType Directory -Force
        }

        # Create temp destination path
        $script:destinationPath = [IO.Path]::GetFullPath((Join-Path -Path $TestDrive -ChildPath "tmpDestinationPath"))
        New-Item $script:destinationPath -ItemType directory -Force
    }
    AfterAll {
       Get-RevertPSResourceRepositoryFile
    }

    It "Should not publish module to ADO repository feed (public) when Credentials are incorrect" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $script:PublishModuleBase -Repository $ADOPublicRepoName -Credential $incorrectRepoCred -ErrorAction SilentlyContinue

        $Error[0].FullyQualifiedErrorId | Should -be "401Error,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"
    }

    It "Should not publish module to ADO repository feed (public) when ApiKey is not provided" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $script:PublishModuleBase -Repository $ADOPublicRepoName -Credential $correctPublicRepoCred -ErrorAction SilentlyContinue

        $Error[0].FullyQualifiedErrorId | Should -be "401Error,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"
    }

    It "Should not publish module to ADO repository feed (private) when Credentials are incorrect" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $script:PublishModuleBase -Repository $ADOPrivateRepoName -Credential $incorrectRepoCred -ErrorAction SilentlyContinue

        $Error[0].FullyQualifiedErrorId | Should -be ("401FatalProtocolError,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource" -or "ProtocolFailError,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource")
    }
}
