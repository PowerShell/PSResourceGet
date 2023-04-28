# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$((Get-Item $psscriptroot).parent)\PSGetTestUtils.psm1" -Force

Describe 'Test HTTP Update-PSResource for V3 Server Protocol' -tags 'CI' {

    BeforeAll{
        $NuGetGalleryName = Get-NuGetGalleryName
        $testModuleName = "test_module"
        Get-NewPSResourceRepositoryFile
    }

    AfterEach {
        Uninstall-PSResource $testModuleName -Version "*"
    }
    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Update resource installed given Name parameter" {
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $NuGetGalleryName -TrustRepository

        Update-PSResource -Name $testModuleName -Repository $NuGetGalleryName -TrustRepository
        $res = Get-InstalledPSResource -Name $testModuleName

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.0.0.0")
            {
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -Be $true
    }

    It "Update resource installed given Name and Version (specific) parameters" {
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $NuGetGalleryName -TrustRepository

        Update-PSResource -Name $testModuleName -Version "5.0.0.0" -Repository $NuGetGalleryName -TrustRepository
        $res = Get-InstalledPSResource -Name $testModuleName
        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -eq [System.Version]"5.0.0")
            {
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -BeTrue
    }

    $testCases2 = @{Version="[3.0.0.0]";           UpdatedVersion="3.0.0"; Reason="validate version, exact match"},
                  @{Version="3.0.0.0";             UpdatedVersion="3.0.0"; Reason="validate version, exact match without bracket syntax"},
                  @{Version="[3.0.0.0, 5.0.0.0]";  UpdatedVersion="5.0.0"; Reason="validate version, exact range inclusive"},
                  @{Version="(3.0.0.0, 6.0.0.0)";  UpdatedVersion="5.0.0"; Reason="validate version, exact range exclusive"},
                  @{Version="(3.0.0.0,)";          UpdatedVersion="5.0.0"; Reason="validate version, minimum version exclusive"},
                  @{Version="[3.0.0.0,)";          UpdatedVersion="5.0.0"; Reason="validate version, minimum version inclusive"},
                  @{Version="(,5.0.0.0)";          UpdatedVersion="3.0.0"; Reason="validate version, maximum version exclusive"},
                  @{Version="(,5.0.0.0]";          UpdatedVersion="5.0.0"; Reason="validate version, maximum version inclusive"},
                  @{Version="[1.0.0.0, 5.0.0.0)";  UpdatedVersion="3.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
                  @{Version="(1.0.0.0, 3.0.0.0]";  UpdatedVersion="3.0.0"; Reason="validate version, mixed exclusive minimum and inclusive maximum version"}

    It "Update resource when given Name to <Reason> <Version>" -TestCases $testCases2{
        param($Version, $UpdatedVersion)

        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $NuGetGalleryName -TrustRepository
        $res = Update-PSResource -Name $testModuleName -Version $Version -Repository $NuGetGalleryName -TrustRepository -PassThru -SkipDependencyCheck

        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be $UpdatedVersion
    }

    $testCases = @(
        @{Version='(3.0.0.0)';       Description="exclusive version (3.0.0.0)"},
        @{Version='[3-0-0-0]';       Description="version formatted with invalid delimiter [3-0-0-0]"}
    )
    It "Should not update resource with incorrectly formatted version such as <Description>" -TestCases $testCases{
        param($Version, $Description)

        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $NuGetGalleryName -TrustRepository
        Update-PSResource -Name $testModuleName -Version $Version -Repository $NuGetGalleryName -TrustRepository 2>$null

        $res = Get-InstalledPSResource -Name $testModuleName
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

    It "Update resource with latest (including prerelease) version given Prerelease parameter" {
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $NuGetGalleryName -TrustRepository
        Update-PSResource -Name $testModuleName -Prerelease -Repository $NuGetGalleryName -TrustRepository
        $res = Get-InstalledPSResource -Name $testModuleName

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -ge [System.Version]"5.2.5")
            {
                $pkg.Prerelease | Should -Be "alpha001"
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -Be $true
    }

    # Windows only
    It "update resource under CurrentUser scope" -skip:(!($IsWindows -and (Test-IsAdmin))) {
        # TODO: perhaps also install TestModule with the highest version (the one above 1.2.0.0) to the AllUsers path too
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $NuGetGalleryName -TrustRepository -Scope AllUsers
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $NuGetGalleryName -TrustRepository -Scope CurrentUser

        Update-PSResource -Name $testModuleName -Version "3.0.0.0" -Repository $NuGetGalleryName -TrustRepository -Scope CurrentUser

        $res = Get-InstalledPSResource -Name $testModuleName

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
        Install-PSResource -Name "testmodule99" -Version "0.0.91" -Repository $NuGetGalleryName -TrustRepository -Scope AllUsers
        Install-PSResource -Name "testmodule99" -Version "0.0.91" -Repository $NuGetGalleryName -TrustRepository -Scope CurrentUser

        Update-PSResource -Name "testmodule99" -Version "0.0.93" -Repository $NuGetGalleryName -TrustRepository -Scope AllUsers

        $res = Get-Module -Name "testmodule99" -ListAvailable
        $res | Should -Not -BeNullOrEmpty
        $res.Version | Should -Contain "0.0.93"
    }

    # Windows only
    It "Update resource under no specified scope" -skip:(!$IsWindows) {
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $NuGetGalleryName -TrustRepository
        Update-PSResource -Name $testModuleName -Version "3.0.0.0" -Repository $NuGetGalleryName -TrustRepository -verbose

        $res = Get-InstalledPSResource -Name $testModuleName

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
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository -Scope CurrentUser

        Update-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -Scope CurrentUser

        $res = Get-InstalledPSResource -Name $testModuleName

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
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository -Scope AllUsers
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository -Scope CurrentUser

        Update-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -Scope AllUsers

        $res = Get-InstalledPSResource -Name $testModuleName

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
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository -Scope CurrentUser

        Update-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository

        $res = Get-InstalledPSResource -Name $testModuleName

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
    #     $res = Get-InstalledPSResource "TestModuleWithLicense"

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
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $NuGetGalleryName -TrustRepository
        Update-PSResource -Name $testModuleName -WhatIf -Repository $NuGetGalleryName -TrustRepository

        $res = Get-InstalledPSResource -Name $testModuleName

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
        Install-PSResource -Name $testModuleName -Version "1.0.0" -Repository $NuGetGalleryName -TrustRepository

        $res = Update-PSResource -Name $testModuleName -Version "3.0.0" -Repository $NuGetGalleryName -TrustRepository -PassThru
        $res.Name | Should -Contain $testModuleName
        $res.Version | Should -Contain "3.0.0"
    }
}
