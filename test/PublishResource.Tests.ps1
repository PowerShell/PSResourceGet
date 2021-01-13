# This is a Pester test suite to validate Register-PSResourceRepository, Unregister-PSResourceRepository, Get-PSResourceRepository, and Set-PSResourceRepository.
#
# Copyright (c) Microsoft Corporation, 2019

#Testing Environment Setup
BeforeAll {
    Import-Module $PSScriptRoot/Shared.psm1

    $PSGalleryName = 'PSGallery'
    $PSGalleryLocation = 'https://www.powershellgallery.com/api/v2'
    $TestLocalDirectory = 'TestLocalDirectory'
    $tmpdir = Join-Path -Path $TestDrive -ChildPath $TestLocalDirectory
    
    if (-not (Test-Path -LiteralPath $tmpdir)) {
        New-Item -Path $tmpdir -ItemType Directory > $null
    }
}

Describe 'Publish-PSResource' -tags 'BVT' {
    BeforeAll {
        try {
            Unregister-PSResourceRepository -Name psgettestlocal
        } catch {}
        Register-PSResourceRepository -Name psgettestlocal -URL $(Join-Path $testdrive 'psgettestlocal')

        # Create temp module to be published
        $script:TempModulesPath = Join-Path -Path $tmpdir -ChildPath "PSGet_$(Get-Random)"
        $null = New-Item -Path $TempModulesPath -ItemType Directory -Force

        $PublishModuleName = "TestPublishModule"
        $PublishModuleBase = Join-Path $TempModulesPath $PublishModuleName
        $null = New-Item -Path $PublishModuleBase -ItemType Directory -Force
    }
	AfterAll { 
        if($tmpdir -and (Test-Path $tmpdir))
        {
            Remove-Item $tmpdir -Force -Recurse
        }
    }

    ### Publish a script
    It 'Should publish a script' { Set-ItResult -Pending -Because 'WIP'

        $Name = 'TestScriptName'
        $scriptFilePath = Join-Path -Path $TempModulesPath -ChildPath "$Name.ps1"
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

        $scriptMetadata = New-PSScriptMetadata @params
        Set-Content -Path $scriptFilePath -Value $scriptMetadata

        Publish-PSResource -path $scriptFilePath -Repository psgettestlocal
    }


    It 'Should publish a module' { Set-ItResult -Pending -Because 'WIP'
        $PublishModuleBase = Join-Path $TempModulesPath $PublishModuleName

        $version = "1.0"
        New-ModuleManifest -Path (Join-Path -Path $PublishModuleBase -ChildPath "$PublishModuleName.psd1") -ModuleVersion $version -Description "$PublishModuleName module"  -NestedModules "$PublishModuleName.psm1"

        Publish-PSResource -path $PublishModuleBase -Repository psgettestlocal
    }
}


