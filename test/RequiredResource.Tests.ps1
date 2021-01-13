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

Describe 'Test Install-PSResource using the RequiredResource parameter set' -tags 'BVT' {

	AfterEach {
        #WIP
        #$null = uninstall-psresource 'CertificateDsc' -ErrorAction SilentlyContinue
    }

    ### Installing using -RequiredResource and a json argument
    It 'Should install the resource specified in the json (with specified parameters)' { Set-ItResult -Pending -Because 'WIP'
        $json = 
        "{
            'CertificateDsc': {
              'version': '[4.0.0,4.2.0]',
              'repository': 'PSGallery',
              'Prerelease': true
            }
        }"

        $ret = Install-PSResource -RequiredResource $json
        $ret | Should -BeNullOrEmpty

        $pkg = get-psresource 'CertificateDsc' -Version '[4.0.0.0,4.2.0.0]'
        $pkg.Name | Should -Be 'CertificateDsc'
        $pkg.Version.ToString() | Should -BeGreaterOrEqual 4.0.0.0
        $pkg.Version.ToString() | Should -BeLessOrEqual 4.2.0.0
    }

    It 'Should install multiple resources specified in the json' { Set-ItResult -Pending -Because 'WIP'
    $json = 
        $json = 
        "{
            'CertificateDsc': {
              'version': '[4.0.0,4.2.0]',
              'repository': 'PSGallery',
              'Prerelease': true
            },
            'WSManDsc': {
              'repository': 'PSGallery',
              'Prerelease': true
            }
        }"

        $ret = Install-PSResource -RequiredResource $json
        $ret | Should -BeNullOrEmpty

        $pkg = get-psresource 'CertificateDsc' -Version '[4.0.0.0,4.2.0.0]'
        $pkg.Name | Should -Be 'CertificateDsc'
        $pkg.Version.ToString() | Should -BeGreaterOrEqual 4.0.0.0
        $pkg.Version.ToString() | Should -BeLessOrEqual 4.2.0.0

        $pkg2 = get-psresource 'WSManDsc' -Version '3.1.2-preview0001'
        $pkg2.Name | Should -Be 'WSManDsc'
        $pkg2.Version.ToString() | Should -Be '3.1.2-preview0001'
    }


    ### Installing using -RequiredResource and a hashtable argument
    It 'Should install the resource specified in the hashtable' { Set-ItResult -Pending -Because 'WIP'
    $json = 
        $hash = 
        @{
            name = "CertificateDsc"
            trustrepository = "true"
            version = "[4.0.0,4.2.0]"
            Prerelease = "true"
        }

        $ret = Install-PSResource -RequiredResource $hash
        $ret | Should -BeNullOrEmpty

        $pkg = get-psresource 'CertificateDsc' -Version '[4.0.0.0,4.2.0.0]'
        $pkg.Name | Should -Be 'CertificateDsc'
        $pkg.Version.ToString() | Should -BeGreaterOrEqual 4.0.0.0
        $pkg.Version.ToString() | Should -BeLessOrEqual 4.2.0.0
    }
}

Describe 'Test Install-PSResource using the RequiredResource parameter set' -tags 'BVT' { 

    ### Installing using -RequiredResource and a json file
    It 'Should install the resource specified in the json file' { Set-ItResult -Pending -Because 'WIP'
        $json = 
        "{
            'CertificateDsc': {
              'version': '[4.0.0,4.2.0]',
              'repository': 'PSGallery',
              'Prerelease': true
            }
        }"

        $tmpJsonFile = Join-Path -Path $tmpdir -ChildPath "TestJsonFile.json"

        New-Item -Path $tmpJsonFile -ItemType File
        $json | Write-File $tmpJsonFile

        if ($tmpJsonFile -ne $null)
        {
            $ret = Install-PSResource -RequiredResourceFile $tmpJsonFile
        }
        Remove-Item $tmpJsonFile
        
        $ret | Should -BeNullOrEmpty

        $pkg = get-psresource 'CertificateDsc' -Version '[4.0.0.0,4.2.0.0]'
        $pkg.Name | Should -Be 'CertificateDsc'
        $pkg.Version.ToString() | Should -BeGreaterOrEqual 4.0.0.0
        $pkg.Version.ToString() | Should -BeLessOrEqual 4.2.0.0
    }
}
