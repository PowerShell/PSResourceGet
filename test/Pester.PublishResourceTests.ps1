# This is a Pester test suite to validate Register-PSResourceRepository, Unregister-PSResourceRepository, Get-PSResourceRepository, and Set-PSResourceRepository.
#
# Copyright (c) Microsoft Corporation, 2019

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -WarningAction SilentlyContinue -force
import-module "C:\code\PowerShellGet\src\bin\Debug\netstandard2.0\publish\PowerShellGet.dll" -force

$PSGalleryName = 'PSGallery'
$PSGalleryLocation = 'https://www.powershellgallery.com/api/v2'

$TestLocalDirectory = 'TestLocalDirectory'
$tmpdir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $TestLocalDirectory



if (-not (Test-Path -LiteralPath $tmpdir)) {
    New-Item -Path $tmpdir -ItemType Directory > $null
}
Write-Host $tmpdir


##########################
### Publish-PSResource ###
##########################


Describe 'Test Publish-PSResource' -tags 'BVT' {

    BeforeAll {

        Register-PSResourceRepository -Name psgettestlocal -URL "c:\code\testdir"

        # Create temp module to be published
        $script:TempModulesPath = Join-Path -Path $tmpdir -ChildPath "PSGet_$(Get-Random)"
        $null = New-Item -Path $script:TempModulesPath -ItemType Directory -Force
        Write-Host("script:TempModulesPath is: " + $script:TempModulesPath)
        $script:PublishModuleName = "TestPublishModule"
        $script:PublishModuleBase = Join-Path $script:TempModulesPath $script:PublishModuleName
        $null = New-Item -Path $script:PublishModuleBase -ItemType Directory -Force
        Write-Host("script:TempModulesPath is: " + $script:PublishModuleBase)

    }
	AfterAll {
        if($tempdir -and (Test-Path $tempdir))
        {
            Remove-Item $tempdir -Force -Recurse -ErrorAction SilentlyContinue
        }
    }

    ### Publish a script
    It 'Should publish a script' {

        $Name = 'TestScriptName'
        $scriptFilePath = Join-Path -Path $script:TempModulesPath -ChildPath "$Name.ps1"
        write-host ("scriptFilePath is: " + $scriptFilePath)
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




    It 'Should publish a module' {
       
        $PublishModuleBase = Join-Path $script:TempModulesPath $script:PublishModuleName

        $version = "1.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"  -NestedModules "$script:PublishModuleName.psm1"
        write-host ($script:PublishModuleBase)

        Publish-PSResource -path  $script:PublishModuleBase -Repository psgettestlocal
    }

}


