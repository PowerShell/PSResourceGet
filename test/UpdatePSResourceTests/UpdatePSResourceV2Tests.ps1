# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

Describe 'Test HTTP Update-PSResource for V2 Server Protocol' -tags 'CI' {

    BeforeAll {
        $PSGalleryName = Get-PSGalleryName
        $NuGetGalleryName = Get-NuGetGalleryName
        $testModuleName = "test_module"
        $testModuleName2 = "test_module2"
        $testModuleName3 = "TestModule99"
        $PackageManagement = "PackageManagement"
        Get-NewPSResourceRepositoryFile
        Get-PSResourceRepository
    }

    AfterEach {
       Uninstall-PSResource "test_module", "TestModule99", "TestModuleWithLicense", "test_module2", "test_script" -Version "*" -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Update resource installed given Name parameter" {
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository

        Update-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository
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

    It "Update resources installed given Name (with wildcard) parameter" {
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository
        Install-PSResource -Name $testModuleName2 -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository

        Update-PSResource -Name "test_mod*" -Repository $PSGalleryName -TrustRepository
        $res = Get-InstalledPSResource -Name "test_mod*" -Version "5.0.0.0"

        $inputHashtable = @{test_module = "1.0.0.0"; test_module2 = "1.0.0.0"}
        $isTest_ModuleUpdated = $false
        $isTest_Module2Updated = $false
        foreach ($item in $res)
        {
            if ([System.Version]$item.Version -gt [System.Version]$inputHashtable[$item.Name])
            {
                if ($item.Name -like $testModuleName)
                {
                    $isTest_ModuleUpdated = $true
                }
                elseif ($item.Name -like $testModuleName2)
                {
                    $isTest_Module2Updated = $true
                }
            }
        }

        $isTest_ModuleUpdated | Should -BeTrue
        $isTest_Module2Updated | Should -BeTrue
    }

    It "Update resource installed given Name and Version (specific) parameters" {
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository

        Update-PSResource -Name $testModuleName -Version "5.0.0.0" -Repository $PSGalleryName -TrustRepository
        $res = Get-InstalledPSResource -Name $testModuleName
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

    $testCases2 = @{Version="[3.0.0.0]";           UpdatedVersion="3.0.0.0"; Reason="validate version, exact match"},
                  @{Version="3.0.0.0";             UpdatedVersion="3.0.0.0"; Reason="validate version, exact match without bracket syntax"},
                  @{Version="[3.0.0.0, 5.0.0.0]";  UpdatedVersion="5.0.0.0"; Reason="validate version, exact range inclusive"},
                  @{Version="(3.0.0.0, 6.0.0.0)";  UpdatedVersion="5.0.0.0"; Reason="validate version, exact range exclusive"},
                  @{Version="(3.0.0.0,)";          UpdatedVersion="5.0.0.0"; Reason="validate version, minimum version exclusive"},
                  @{Version="[3.0.0.0,)";          UpdatedVersion="5.0.0.0"; Reason="validate version, minimum version inclusive"},
                  @{Version="(,5.0.0.0)";          UpdatedVersion="3.0.0.0"; Reason="validate version, maximum version exclusive"},
                  @{Version="(,5.0.0.0]";          UpdatedVersion="5.0.0.0"; Reason="validate version, maximum version inclusive"},
                  @{Version="[1.0.0.0, 5.0.0.0)";  UpdatedVersion="3.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
                  @{Version="(1.0.0.0, 3.0.0.0]";  UpdatedVersion="3.0.0.0"; Reason="validate version, mixed exclusive minimum and inclusive maximum version"}

    It "Update resource when given Name to <Reason> <Version>" -TestCases $testCases2{
        param($Version, $UpdatedVersion)

        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository
        $res = Update-PSResource -Name $testModuleName -Version $Version -Repository $PSGalleryName -TrustRepository -PassThru -SkipDependencyCheck

        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be $UpdatedVersion
    }

    $testCases = @(
        @{Version='(3.0.0.0)';       Description="exclusive version (3.0.0.0)"},
        @{Version='[3-0-0-0]';       Description="version formatted with invalid delimiter [3-0-0-0]"}
    )
    It "Should not update resource with incorrectly formatted version such as <Description>" -TestCases $testCases{
        param($Version, $Description)

        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository
        Update-PSResource -Name $testModuleName -Version $Version -Repository $PSGalleryName -TrustRepository 2>$null

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
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository
        Update-PSResource -Name $testModuleName -Prerelease -Repository $PSGalleryName -TrustRepository
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
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository -Scope AllUsers
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository -Scope CurrentUser

        Update-PSResource -Name $testModuleName -Version "3.0.0.0" -Repository $PSGalleryName -TrustRepository -Scope CurrentUser

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
        Install-PSResource -Name "testmodule99" -Version "0.0.91" -Repository $PSGalleryName -TrustRepository -Scope AllUsers
        Install-PSResource -Name "testmodule99" -Version "0.0.91" -Repository $PSGalleryName -TrustRepository -Scope CurrentUser

        Update-PSResource -Name "testmodule99" -Version "0.0.93" -Repository $PSGalleryName -TrustRepository -Scope AllUsers

        $res = Get-Module -Name "testmodule99" -ListAvailable
        $res | Should -Not -BeNullOrEmpty
        $res.Version | Should -Contain "0.0.93"
    }

    # Windows only
    It "Update resource under no specified scope" -skip:(!$IsWindows) {
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository
        Update-PSResource -Name $testModuleName -Version "3.0.0.0" -Repository $PSGalleryName -TrustRepository -verbose

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
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository
        Update-PSResource -Name $testModuleName -WhatIf -Repository $PSGalleryName -TrustRepository

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
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository

        $res = Update-PSResource -Name $testModuleName -Version "3.0.0.0" -Repository $PSGalleryName -TrustRepository -PassThru
        $res.Name | Should -Contain $testModuleName
        $res.Version | Should -Contain "3.0.0.0"
    }

    ## TODO update this -- find module with valid catalog file
    # Update to module 1.4.3 (is authenticode signed and has catalog file)
    # Should update successfully
    It "Update module with catalog file using publisher validation" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $PackageManagement -Version "1.4.2" -Repository $PSGalleryName -TrustRepository -verbose
        Update-PSResource -Name $PackageManagement -Version "1.4.3" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository

        $res1 = Get-InstalledPSResource $PackageManagement -Version "1.4.3"
        $res1.Name | Should -Be $PackageManagement
        $res1.Version | Should -Be "1.4.3"
    }

    # # Update to module 1.4.4.1 (with incorrect catalog file)
    # # Should FAIL to update the module
    # It "Update module with incorrect catalog file" -Skip:(!(Get-IsWindows)) {
    #     Install-PSResource -Name $PackageManagement -Version "1.4.2" -Reinstall -Repository $PSGalleryName -TrustRepository
    #     Update-PSResource -Name $PackageManagement -Version "1.4.4.1" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
    #     $err.Count | Should -Not -Be 0
    #     $err[0].FullyQualifiedErrorId | Should -BeExactly "InstallPackageFailure,Microsoft.PowerShell.PowerShellGet.Cmdlets.UpdatePSResource"
    # }

    # Update to module 1.4.7 (is authenticode signed and has NO catalog file)
    # Should update successfully
    It "Install module with no catalog file" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $PackageManagement -Version "1.4.2" -Repository $PSGalleryName -TrustRepository
        Update-PSResource -Name $PackageManagement -Version "1.4.7" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository

        $res1 = Get-InstalledPSResource $PackageManagement -Version "1.4.7"
        $res1.Name | Should -Be $PackageManagement
        $res1.Version | Should -Be "1.4.7"
    }

    # Update script that is signed
    # Should update successfully
    It "Update script that is authenticode signed" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name "Install-VSCode" -Version "1.4.1" -Repository $PSGalleryName -TrustRepository
        Update-PSResource -Name "Install-VSCode" -Version "1.4.2" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository

        $res1 = Get-InstalledPSResource "Install-VSCode" -Version "1.4.2"
        $res1.Name | Should -Be "Install-VSCode"
        $res1.Version | Should -Be "1.4.2"
    }

    # Update script that is not signed
    # Should throw
    It "Update script that is not signed" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name "TestTestScript" -Version "1.0" -Repository $PSGalleryName -TrustRepository
        Update-PSResource -Name "TestTestScript" -Version "1.3.1.1" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "InstallPackageFailure,Microsoft.PowerShell.PowerShellGet.Cmdlets.UpdatePSResource"
    }
}
