# This is a Pester test suite to validate Register-PSResourceRepository, Unregister-PSResourceRepository, Get-PSResourceRepository, and Set-PSResourceRepository.
#
# Copyright (c) Microsoft Corporation, 2019

# TODO:
Write-Warning "PSRepository.Tests.ps1 is current disabled."
return

$psGetMod = Get-Module -Name PowerShellGet
if ((! $psGetMod) -or (($psGetMod | Select-Object Version) -lt 3.0.0))
{
    Write-Verbose -Verbose "Importing PowerShellGet 3.0.0 for test"
    Import-Module -Name PowerShellGet -MinimumVersion 3.0.0 -Force
}

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -WarningAction SilentlyContinue -force

$PSGalleryName = 'PSGallery'
$PSGalleryLocation = 'https://www.powershellgallery.com/api/v2'

$TestLocalDirectory = 'TestLocalDirectory'
$tmpdir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $TestLocalDirectory



if (-not (Test-Path -LiteralPath $tmpdir)) {
    New-Item -Path $tmpdir -ItemType Directory > $null
}


##########################
### Publish-PSResource ###
##########################
Describe 'Test Publish-PSResource' -tags 'BVT' {

    BeforeAll {

        Register-PSResourceRepository -Name psgettestlocal -URL "c:\code\testdir"

        # Create temp module to be published
        $script:TempModulesPath = Join-Path -Path $tmpdir -ChildPath "PSGet_$(Get-Random)"
        $null = New-Item -Path $script:TempModulesPath -ItemType Directory -Force

        $script:PublishModuleName = "TestPublishModule"
        $script:PublishModuleBase = Join-Path $script:TempModulesPath $script:PublishModuleName
        $null = New-Item -Path $script:PublishModuleBase -ItemType Directory -Force

    }
	AfterAll {
        if($tempdir -and (Test-Path $tempdir))
        {
            Remove-Item $tempdir -Force -Recurse -ErrorAction SilentlyContinue
        }
    }

    ### Publish a script
    It 'Should publish a script'
    {

        $Name = 'TestScriptName'
        $scriptFilePath = Join-Path -Path $script:TempModulesPath -ChildPath "$Name.ps1"
        $null = New-Item -Path $scriptFilePath -ItemType File -Force

        $version = "1.0.0"
        $params = @{
                    #Path = $scriptFilePath
                    Version = $version
                    #GUID = 
                    Author = 'Jane'
                    CompanyName = 'Microsoft Corporation'
                    Copyright = '(c) 2020 Microsoft Corporation. All rights reserved.'
                    Description = "Description for the $Name script"
                    LicenseUri = "https://$Name.com/license"
                    IconUri = "https://$Name.com/icon"
                    ProjectUri = "https://$Name.com"
                    Tags = @('Tag1','Tag2', "Tag-$Name-$version")
                    ReleaseNotes = "$Name release notes"
                    }

        $scriptMetadata = Create-PSScriptMetadata @params
        Set-Content -Path $scriptFilePath -Value $scriptMetadata

        Publish-PSResource -path $scriptFilePath -Repository psgettestlocal
    }


    It 'Should publish a module'
    {
        $PublishModuleBase = Join-Path $script:TempModulesPath $script:PublishModuleName

        $version = "1.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"  -NestedModules "$script:PublishModuleName.psm1"

        Publish-PSResource -path  $script:PublishModuleBase -Repository psgettestlocal
    }
}


