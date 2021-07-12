# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Install-PSResource for Module' {


    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        $NuGetGalleryName = Get-NuGetGalleryName
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos
        Get-PSResourceRepository
    }

    AfterEach {
        Uninstall-PSResource "TestModule"
        Uninstall-PSResource "TestModule99"
        Uninstall-PSResource "TestModuleWithLicense"
        Uninstall-PSResource "PSGetTestModule"

    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "update resource installed given Name parameter" {
        Install-PSResource -Name "TestModule" -Version "1.1.0.0" -Repository $TestGalleryName

        Update-PSResource -Name "TestModule" -Repository $TestGalleryName
        $res = Get-InstalledPSResource -Name "TestModule"
        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.0.0.0")
            {
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -BeTrue
    }

    It "update resources installed given Name (with wildcard) parameter" {
        Install-PSResource -Name "TestModule" -Version "1.1.0.0" -Repository $TestGalleryName
        Install-PSResource -Name "TestModule99" -Version "0.0.4.0" -Repository $TestGalleryName

        Update-PSResource -Name "TestModule*" -Repository $TestGalleryName
        $res = Get-InstalledPSResource -Name "TestModule*"

        $inputHashtable = @{TestModule = "1.1.0.0"; TestModule99 = "0.0.4.0"}
        $isTestModuleUpdated = $false
        $isTestModule99Updated = $false
        foreach ($item in $res)
        {
            if ([System.Version]$item.Version -gt [System.Version]$inputHashtable[$item.Name])
            {
                if ($item.Name -like "TestModule")
                {
                    $isTestModuleUpdated = $true
                }
                elseif ($item.Name -like "TestModule99")
                {
                    $isTestModule99Updated = $true
                }
            }
        }

        $isTestModuleUpdated | Should -BeTrue
        $isTestModule99Updated | Should -BeTrue

    }

    # It "update resources installed given Name (with wildcard) and Version (specific -supported? and with wildcard -supported?) " {
    #     # TODO: determine support!
    #     # Name (wc) + Version (specific) -> unsupported or update which ones have that version but say/write error if that version doesn't exist for that pkg
    #     # Name (wc) + Version ("*") -> is perhaps what's supported by default/happening by default
    #     # NAme (wc) + Version ("1.0.0.*") -> supported?

    # }

    It "update resource installed given Name and Version (specific) parameters" {
        Install-PSResource -Name "TestModule" -Version "1.1.0.0" -Repository $TestGalleryName

        Update-PSResource -Name "TestModule" -Version "1.2.0.0" -Repository $TestGalleryName
        $res = Get-InstalledPSResource -Name "TestModule"
        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -eq [System.Version]"1.2.0.0")
            {
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -BeTrue
    }

    # It "update resource when given Name and incorrectly formatted version" {

    # }

    It "update resource with latest (including prerelease) version given Prerelease parameter" {
        # PSGetTestModule resource's latest version is a prerelease version, before that it has a non-prerelease version

        Install-PSResource -Name "PSGetTestModule" -Version "1.0.2.0" -Repository $TestGalleryName
        Update-PSResource -Name "PSGetTestModule" -Prerelease -Repository $TestGalleryName
        $res = Get-InstalledPSResource "PSGetTestModule"

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.0.2.0")
            {
                Write-Host $pkg.Version" and prerelease data is:"$pkg.PrivateData.PSData.Prerelease"yep that it"
                # $pkg.PrereleaseLabel | Should -Be "-alpha1" (todo: for some reason get-installedpsresource doesn't get this!)
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -BeTrue
    }

    # Windows only
    It "update resource under CurrentUser scope" {
        # TODO: perhaps also install TestModule with the highest version (the one above 1.2.0.0) to the AllUsers path too
        Install-PSResource -Name "TestModule" -Version "1.1.0.0" -Repository $TestGalleryName -Scope CurrentUser
        Update-PSResource -Name "TestModule" -Version "1.2.0.0" -Repository $TestGalleryName -Scope CurrentUser

        # TODO: use Get-InstalledPSResource
        # $res = Get-InstalledPSResource -Name "TestModule"
        # $res.Name | Should -Be "TestModule"
        # $res.InstalledLocation.Contains("Documents") | Should -Be $true
        # TODO: turn foreach loop bool return into a helper function which takes the list output of get, name and version of a package we wish to search for
        $res = Get-Module "TestModule" -ListAvailable

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.1.0.0")
            {
                $pkg.Path.Contains("Documents") | Should -Be $true
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -BeTrue
    }

    # Windows only
    It "update resource under AllUsers scope" {
        # TODO: perhaps also install TestModule with the highest version (the one above 1.2.0.0) to the CurrentUser path too
        Install-PSResource -Name "TestModule" -Version "1.1.0.0" -Repository $TestGalleryName -Scope AllUsers
        Update-PSResource -Name "TestModule" -Version "1.2.0.0" -Repository $TestGalleryName -Scope AllUsers

        # $res = Get-InstalledPSResource -Name "TestModule"
        # $res.Name | Should -Be "TestModule"
        # $res.InstalledLocation.Contains("Program Files") | Should -Be $true
        $res = Get-Module "TestModule" -ListAvailable
        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.1.0.0")
            {
                $pkg.Path.Contains("Program Files") | Should -Be $true
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -BeTrue
    }

    # Windows only
    It "update resource under no specified scope" {
        Install-PSResource -Name "TestModule" -Version "1.1.0.0" -Repository $TestGalleryName
        Update-PSResource -Name "TestModule" -Version "1.2.0.0" -Repository $TestGalleryName

        $res = Get-Module "TestModule" -ListAvailable

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.1.0.0")
            {
                $pkg.Path.Contains("Documents") | Should -Be $true
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -BeTrue
    }

    # TODO: ask Amber if we can publish another version to TestModuleWithLicense
    # It "update resource that requires accept license with -AcceptLicense flag" {
    #     Install-PSResource -Name "TestModuleWithLicense" -Version "0.0.1.0" -Repository $TestGalleryName -AcceptLicense
    #     Update-PSResource -Name "TestModuleWithLicense" -Version "0.0.1.0" -Repository $TestGalleryName -AcceptLicense
    #     $pkg = Get-InstalledPSResource "testModuleWithlicense"

    #     $res = Get-Module "TestModule" -ListAvailable

    #     $isPkgUpdated = $false
    #     foreach ($pkg in $res)
    #     {
    #         if ([System.Version]$pkg.Version -gt [System.Version]"0.0.1.0")
    #         {
    #             $isPkgUpdated = $true
    #         }
    #     }

    #     $isPkgUpdated | Should -BeTrue

    #     # $pkg.Name | Should -Be "testModuleWithlicense"
    #     # $pkg.Version | Should -Be "0.0.1.0"
    # }

    It "Install resource should not prompt 'trust repository' if repository is not trusted but -TrustRepository is used" {
        Install-PSResource -Name "TestModule" -Version "1.1.0.0" -Repository $TestGalleryName

        Set-PSResourceRepository PoshTestGallery -Trusted:$false

        Update-PSResource -Name "TestModule" -Version "1.2.0.0" -Repository $TestGalleryName -TrustRepository
        $res = Get-InstalledPSResource -Name "TestModule"

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.1.0.0")
            {
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -BeTrue
        Set-PSResourceRepository PoshTestGallery -Trusted
    }

    # TODO: slightly confused about NoClobber parameter for Update...if I had already installed
    # myTestModule and wanted to update it would NoClobber not allow it to update (bc this new updated version contains the same cmdlets
    # as the older installed version)?
    # It "update resource with cmdlet names from a module already installed (should clobber)" {
    #     Install-PSResource -Name "myTestModule" -Repository $TestGalleryName
    #     $pkg = Get-InstalledPSResource "myTestModule"
    #     $pkg.Name | Should -Be "myTestModule"
    #     $pkg.Version | Should -Be "0.0.3.0"

    #     Install-PSResource -Name "myTestModule2" -Repository $TestGalleryName
    #     $pkg = Get-InstalledPSResource "myTestModule2"
    #     $pkg.Name | Should -Be "myTestModule2"
    #     $pkg.Version | Should -Be "0.0.1.0"
    # }

    # update script resource
    # Name
    # Version
    # Prerelease
    # Scope
    # AcceptLicense
    # TrustRepository
    # Credential
    # NoClobber
    # InputObject


}