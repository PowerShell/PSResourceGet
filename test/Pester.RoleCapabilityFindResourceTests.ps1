# This is a Pester test suite to validate Find-PSResource.
#
# Copyright (c) Microsoft Corporation, 2020

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -WarningAction SilentlyContinue -force
# Import-Module "C:\code\PowerShellGet\src\bin\Debug\netstandard2.0\publish\PowerShellGet.dll" -force


Import-Module "C:\Users\annavied\Documents\PowerShellGet\src\bin\Debug\netstandard2.0\publish\PowerShellGet.dll" -force


$PSGalleryName = 'PSGallery'
$PSGalleryLocation = 'https://www.powershellgallery.com/api/v2'

$PoshTestGalleryName = 'PoshTestGallery'
$PostTestGalleryLocation = 'https://www.poshtestgallery.com/api/v2'


# Register-PSResourceRepository -PSGallery

$TestLocalDirectory = 'TestLocalDirectory'
$tmpdir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $TestLocalDirectory

if (-not (Test-Path -LiteralPath $tmpdir)){
    New-Item -Path $tmpdir -ItemType Directory > $null
}

##########################
### Find-PSResource ###
##########################
Describe 'Test Find-PSResource for Command' {
    
    # Purpose: to check if v3 installs the PSGallery repo by default
    #
    # Action: Get-PSResourceRepository PSGallery
    #
    # Expected Result: Should find that the PSGallery resource repo is already registered in v3
    It 'Find the Default Registered PSGallery' {

        $repo = Get-PSResourceRepository $PSGalleryName
        $repo | Should -Not -BeNullOrEmpty
        $repo.URL | Should be $PSGalleryLocation
        $repo.Trusted | Should be false
        $repo.Priority | Should be 50
    }

    # Purpose: to register PoshTestGallery resource repo and check it registered successfully
    #
    # Action: Register-PSResourceRepository PoshTestGallery -URL https://www.poshtestgallery.com/api/v2 -Trusted
    #
    # Expected Result: PoshTestGallery resource repo has registered successfully
    It 'Register the Poshtest Repository When -URL is a Website and Installation Policy is Trusted' {
        # Register-PSResourceRepository $PoshTestGalleryName -URL $PostTestGalleryLocation -Trusted

        $repo = Get-PSResourceRepository $PoshTestGalleryName
        $repo.Name | should be $PoshTestGalleryName
        $repo.URL | should be $PostTestGalleryLocation
        $repo.Trusted | should be true
    }

    
}