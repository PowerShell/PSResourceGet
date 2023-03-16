# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
Import-Module "$((Get-Item $psscriptroot).parent)\PSGetTestUtils.psm1" -Force

Describe 'Test Install-PSResource for local repositories' {


    BeforeAll {
        $localRepo = "psgettestlocal"
        $moduleName = "test_local_mod"
        $moduleName2 = "test_local_mod2"
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos

        Get-ModuleResourcePublishedToLocalRepoTestDrive $moduleName $localRepo "1.0.0"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $moduleName $localRepo "3.0.0"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $moduleName $localRepo "5.0.0"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $moduleName2 $localRepo "1.0.0"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $moduleName2 $localRepo "5.0.0"
    }

    AfterEach {
        Uninstall-PSResource $moduleName, $moduleName2 -Version "*"
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Update resource installed given Name parameter" {
        Install-PSResource -Name $moduleName -Version "1.0.0" -Repository $localRepo -TrustRepository
        
        Update-PSResource -Name $moduleName -Repository $localRepo -TrustRepository
        $res = Get-PSResource -Name $moduleName

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.0.0")
            {
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -Be $true
    }

    It "Update resources installed given Name (with wildcard) parameter" {
        Install-PSResource -Name $moduleName -Version "1.0.0" -Repository $localRepo -TrustRepository
        Install-PSResource -Name $moduleName2 -Version "1.0.0" -Repository $localRepo -TrustRepository

        Update-PSResource -Name "test_local*" -Repository $localRepo -TrustRepository
        $res = Get-PSResource -Name "test_local*" -Version "5.0.0"

        $inputHashtable = @{test_module = "1.0.0"; test_module2 = "1.0.0"}
        $isTest_ModuleUpdated = $false
        $isTest_Module2Updated = $false
        foreach ($item in $res)
        {
            if ([System.Version]$item.Version -gt [System.Version]$inputHashtable[$item.Name])
            {
                if ($item.Name -like $moduleName)
                {
                    $isTest_ModuleUpdated = $true
                }
                elseif ($item.Name -like $moduleName2)
                {
                    $isTest_Module2Updated = $true
                }
            }
        }

        $isTest_ModuleUpdated | Should -BeTrue
        $isTest_Module2Updated | Should -BeTrue
    }

    It "Update resource installed given Name and Version (specific) parameters" {
        Install-PSResource -Name $moduleName -Version "1.0.0" -Repository $localRepo -TrustRepository

        Update-PSResource -Name $moduleName -Version "5.0.0" -Repository $localRepo -TrustRepository
        $res = Get-PSResource -Name $moduleName
        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -eq [System.Version]"5.0.0.0")
            {
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -BeTrue
    }

    # Windows only 
    It "update resource under CurrentUser scope" -skip:(!($IsWindows -and (Test-IsAdmin))) {
        # TODO: perhaps also install TestModule with the highest version (the one above 1.2.0.0) to the AllUsers path too
        Install-PSResource -Name $moduleName -Version "1.0.0.0" -Repository $localRepo -TrustRepository -Scope AllUsers
        Install-PSResource -Name $moduleName -Version "1.0.0.0" -Repository $localRepo -TrustRepository -Scope CurrentUser

        Update-PSResource -Name $moduleName -Version "3.0.0.0" -Repository $localRepo -TrustRepository -Scope CurrentUser

        $res = Get-PSResource -Name $moduleName

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.0.0.0")
            {
                $pkg.InstalledLocation.Contains("Documents") | Should -Be $true
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -Be $true
    }
 
    # Windows only
    It "update resource under AllUsers scope" -skip:(!($IsWindows -and (Test-IsAdmin))) {
        Install-PSResource -Name $moduleName -Version "1.0.0" -Repository $localRepo -TrustRepository -Scope AllUsers

        Update-PSResource -Name $moduleName -Repository $localRepo -TrustRepository -Scope AllUsers

        $res = Get-Module -Name $moduleName -ListAvailable
        $res | Should -Not -BeNullOrEmpty
        $res.Version | Should -Contain "5.0.0"

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.0.0.0")
            {
                $pkg.ModuleBase.Contains("Program") | Should -Be $true
                $isPkgUpdated = $true
            }
        }
        $isPkgUpdated | Should -Be $true

    }

    # Windows only
    It "Update resource under no specified scope" -skip:(!$IsWindows) {
        Install-PSResource -Name $moduleName -Version "1.0.0.0" -Repository $localRepo -TrustRepository
        Update-PSResource -Name $moduleName -Version "5.0.0.0" -Repository $localRepo -TrustRepository

        $res = Get-PSResource -Name $moduleName

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.0.0.0")
            {
                $pkg.InstalledLocation.Contains("Documents") | Should -Be $true
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Update resource under CurrentUser scope - Unix only" -Skip:(Get-IsWindows) {
        # this line is commented out because AllUsers scope requires sudo and that isn't supported in CI yet
        # Install-PSResource -Name "TestModule" -Version "1.1.0.0" -Repository $TestGalleryName -Scope AllUsers
        Install-PSResource -Name $moduleName -Version "1.0.0.0" -Repository $localRepo -TrustRepository -Scope CurrentUser

        Update-PSResource -Name $moduleName -Repository $localRepo -TrustRepository -Scope CurrentUser

        $res = Get-PSResource -Name $moduleName

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.0.0.0")
            {
                $pkg.InstalledLocation.Contains("$env:HOME/.local") | Should -Be $true
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/usr/local/share/powershell/Modules'
    # this test is skipped because it requires sudo to run and has yet to be resolved in CI
    It "Update resource under AllUsers scope - Unix only" -Skip:($true) {
        Install-PSResource -Name $moduleName -Version "1.0.0.0" -Repository $localRepo -TrustRepository -Scope AllUsers

        Update-PSResource -Name $moduleName -Repository $PSGalleryName -TrustRepository -Scope AllUsers

        $res = Get-PSResource -Name $moduleName

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.0.0.0")
            {
                $pkg.InstalledLocation.Contains("usr") | Should -Be $true
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Update resource under no specified scope - Unix only" -Skip:(Get-IsWindows) {
        # this is commented out because it requires sudo to run with AllUsers scope and this hasn't been resolved in CI yet
        # Install-PSResource -Name "TestModule" -Version "1.1.0.0" -Repository $TestGalleryName -Scope AllUsers
        Install-PSResource -Name $moduleName -Version "1.0.0.0" -Repository $localRepo -TrustRepository -Scope CurrentUser

        Update-PSResource -Name $moduleName -Repository $localRepo -TrustRepository

        $res = Get-PSResource -Name $moduleName

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.0.0.0")
            {
                $pkg.InstalledLocation.Contains("$env:HOME/.local") | Should -Be $true
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -Be $true
    }

    # It "update resource that requires accept license with -AcceptLicense flag" {
    #     Install-PSResource -Name "TestModuleWithLicense" -Version "0.0.1.0" -Repository $TestGalleryName -AcceptLicense
    #     Update-PSResource -Name "TestModuleWithLicense" -Repository $TestGalleryName -AcceptLicense
    #     $res = Get-PSResource "TestModuleWithLicense"

    #     $isPkgUpdated = $false
    #     foreach ($pkg in $res)
    #     {
    #         if ([System.Version]$pkg.Version -gt [System.Version]"0.0.1.0")
    #         {
    #             $isPkgUpdated = $true
    #         }
    #     }

    #     $isPkgUpdated | Should -Be $true
    # }

    It "Update module using -WhatIf, should not update the module" {
        Install-PSResource -Name $moduleName -Version "1.0.0.0" -Repository $localRepo -TrustRepository
        Update-PSResource -Name $moduleName -WhatIf -Repository $localRepo -TrustRepository

        $res = Get-PSResource -Name $moduleName

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.0.0.0")
            {
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -Be $false
    }

    It "Update resource installed given -Name and -PassThru parameters" {
        Install-PSResource -Name $moduleName -Version "1.0.0.0" -Repository $localRepo -TrustRepository

        $res = Update-PSResource -Name $moduleName -Version "5.0.0.0" -Repository $localRepo -TrustRepository -PassThru
        $res.Name | Should -Contain $moduleName
        $res.Version | Should -Be "5.0.0.0"
    }
}
