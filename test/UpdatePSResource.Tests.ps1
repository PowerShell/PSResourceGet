# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Update-PSResource' {


    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $NuGetGalleryName = Get-NuGetGalleryName
        $testModuleName = "TestModule"
        Get-NewPSResourceRepositoryFile
        Get-PSResourceRepository
    }

    AfterEach {
        Uninstall-PSResource "TestModule", "TestModule99", "TestModuleWithLicense", "PSGetTestModule"
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "update resource installed given Name parameter" {
        Install-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName

        Update-PSResource -Name $testModuleName -Repository $TestGalleryName
        $res = Get-PSResource -Name $testModuleName

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.1.0.0")
            {
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -Be $true
    }

    It "update resources installed given Name (with wildcard) parameter" {
        Install-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName
        Install-PSResource -Name "TestModule99" -Version "0.0.4.0" -Repository $TestGalleryName

        Update-PSResource -Name "TestModule*" -Repository $TestGalleryName
        $res = Get-PSResource -Name "TestModule*"

        $inputHashtable = @{TestModule = "1.1.0.0"; TestModule99 = "0.0.4.0"}
        $isTestModuleUpdated = $false
        $isTestModule99Updated = $false
        foreach ($item in $res)
        {
            if ([System.Version]$item.Version -gt [System.Version]$inputHashtable[$item.Name])
            {
                if ($item.Name -like $testModuleName)
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

    It "update resource installed given Name and Version (specific) parameters" {
        Install-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName

        Update-PSResource -Name $testModuleName -Version "1.2.0.0" -Repository $TestGalleryName
        $res = Get-PSResource -Name $testModuleName
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

    $testCases2 = @{Version="[1.3.0.0]";           ExpectedVersions=@("1.1.0.0", "1.3.0.0"); Reason="validate version, exact match"},
                  @{Version="1.3.0.0";             ExpectedVersions=@("1.1.0.0", "1.3.0.0"); Reason="validate version, exact match without bracket syntax"},
                  @{Version="[1.1.1.0, 1.3.0.0]";  ExpectedVersions=@("1.1.0.0", "1.3.0.0"); Reason="validate version, exact range inclusive"},
                  @{Version="(1.1.1.0, 1.3.0.0)";  ExpectedVersions=@("1.1.0.0", "1.2.0.0"); Reason="validate version, exact range exclusive"},
                  @{Version="(1.1.1.0,)";          ExpectedVersions=@("1.1.0.0", "1.3.0.0"); Reason="validate version, minimum version exclusive"},
                  @{Version="[1.1.1.0,)";          ExpectedVersions=@("1.1.0.0", "1.3.0.0"); Reason="validate version, minimum version inclusive"},
                  @{Version="(,1.3.0.0)";          ExpectedVersions=@("1.1.0.0", "1.2.0.0"); Reason="validate version, maximum version exclusive"},
                  @{Version="(,1.3.0.0]";          ExpectedVersions=@("1.1.0.0", "1.3.0.0"); Reason="validate version, maximum version inclusive"},
                  @{Version="[1.1.1.0, 1.3.0.0)";  ExpectedVersions=@("1.1.0.0", "1.2.0.0"); Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
                  @{Version="(1.1.1.0, 1.3.0.0]";  ExpectedVersions=@("1.1.0.0", "1.2.0.0"); Reason="validate version, mixed exclusive minimum and inclusive maximum version"}

    It "update resource when given Name to <Reason> <Version>" -TestCases $testCases2{
        param($Version, $ExpectedVersions)

        Install-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName
        Update-PSResource -Name $testModuleName -Version $Version -Repository $TestGalleryName

        $res = Get-PSResource -Name $testModuleName

        foreach ($item in $res) {
            $item.Name | Should -Be $testModuleName
            $ExpectedVersions | Should -Contain $item.Version
        }
    }

    $testCases = @(
        @{Version='(1.2.0.0)';       Description="exclusive version (2.10.0.0)"},
        @{Version='[1-2-0-0]';       Description="version formatted with invalid delimiter [1-2-0-0]"}
    )
    It "Should not update resource with incorrectly formatted version such as <Description>" -TestCases $testCases{
        param($Version, $Description)

        Install-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName
        Update-PSResource -Name $testModuleName -Version $Version -Repository $TestGalleryName

        $res = Get-PSResource -Name $testModuleName
        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.1.0.0")
            {
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -Be $false
    }

    It "update resource with latest (including prerelease) version given Prerelease parameter" {
        # PSGetTestModule resource's latest version is a prerelease version, before that it has a non-prerelease version

        Install-PSResource -Name "PSGetTestModule" -Version "1.0.2.0" -Repository $TestGalleryName
        Update-PSResource -Name "PSGetTestModule" -Prerelease -Repository $TestGalleryName
        $res = Get-PSResource -Name "PSGetTestModule"

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.0.2.0")
            {
                $pkg.PrereleaseLabel | Should -Be "alpha1"
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -Be $true
    }

    # Windows only
    It "update resource under CurrentUser scope" -skip:(!$IsWindows) {
        # TODO: perhaps also install TestModule with the highest version (the one above 1.2.0.0) to the AllUsers path too
        Install-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName -Scope AllUsers
        Install-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName -Scope CurrentUser

        Update-PSResource -Name $testModuleName -Version "1.2.0.0" -Repository $TestGalleryName -Scope CurrentUser

        $res = Get-PSResource -Name $testModuleName

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.1.0.0")
            {
                $pkg.InstalledLocation.Contains("Documents") | Should -Be $true
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -Be $true
    }

    # Windows only
    It "update resource under AllUsers scope" -skip:(!($IsWindows -and (Test-IsAdmin))) {
        Install-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName -Scope AllUsers
        Install-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName -Scope CurrentUser

        Update-PSResource -Name $testModuleName -Version "1.2.0.0" -Repository $TestGalleryName -Scope AllUsers

        $res = Get-PSResource -Name $testModuleName
        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.1.0.0")
            {
                $pkg.InstalledLocation.Contains("Program Files") | Should -Be $true
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -Be $true
    }

    # Windows only
    It "update resource under no specified scope" -skip:(!$IsWindows) {
        Install-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName
        Update-PSResource -Name $testModuleName -Version "1.2.0.0" -Repository $TestGalleryName

        $res = Get-PSResource -Name $testModuleName

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.1.0.0")
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
        Install-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName -Scope CurrentUser

        Update-PSResource -Name $testModuleName -Repository $TestGalleryName -Scope CurrentUser

        $res = Get-PSResource -Name $testModuleName

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.1.0.0")
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
        Install-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName -Scope AllUsers
        Install-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName -Scope CurrentUser

        Update-PSResource -Name $testModuleName -Repository $TestGalleryName -Scope AllUsers

        $res = Get-PSResource -Name $testModuleName

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.1.0.0")
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
        Install-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName -Scope CurrentUser

        Update-PSResource -Name $testModuleName -Repository $TestGalleryName

        $res = Get-PSResource -Name $testModuleName

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.1.0.0")
            {
                $pkg.InstalledLocation.Contains("$env:HOME/.local") | Should -Be $true
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -Be $true
    }

    It "update resource that requires accept license with -AcceptLicense flag" {
        Install-PSResource -Name "TestModuleWithLicense" -Version "0.0.1.0" -Repository $TestGalleryName -AcceptLicense
        Update-PSResource -Name "TestModuleWithLicense" -Repository $TestGalleryName -AcceptLicense
        $res = Get-PSResource "TestModuleWithLicense"

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"0.0.1.0")
            {
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -Be $true
    }

    It "update resource should not prompt 'trust repository' if repository is not trusted but -TrustRepository is used" {
        Install-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName

        Set-PSResourceRepository PoshTestGallery -Trusted:$false

        Update-PSResource -Name $testModuleName -Version "1.2.0.0" -Repository $TestGalleryName -TrustRepository
        $res = Get-PSResource -Name $testModuleName

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

    It "Update module using -WhatIf, should not update the module" {
        Install-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName
        Update-PSResource -Name $testModuleName -WhatIf

        $res = Get-PSResource -Name $testModuleName

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"1.1.0.0")
            {
                $isPkgUpdated = $true
            }
        }

        $isPkgUpdated | Should -Be $false
    }

    It "update resource installed given -Name and -PassThru parameters" {
        Install-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName

        $res = Update-PSResource -Name $testModuleName -Version "1.3.0.0" -Repository $TestGalleryName -PassThru
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "1.3.0.0"
    }
}
