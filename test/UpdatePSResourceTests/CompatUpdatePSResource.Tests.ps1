# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
<#
$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

Describe 'Test CompatPowerShellGet: Update-PSResource' -tags 'CI' {

    BeforeAll {
        $PSGalleryName = Get-PSGalleryName
        $NuGetGalleryName = Get-NuGetGalleryName
        $testModuleName = "test_module"
        $testModuleName2 = "TestModule99"
        Get-NewPSResourceRepositoryFile
        Get-PSResourceRepository
        Set-PSResourceRepository -Name PSGallery -Trusted
    }

    AfterEach {
       Uninstall-PSResource "test_module", "TestModule99", "TestModuleWithLicense", "TestModuleWithDependencyB", "TestModuleWithDependencyD" -Version "*" -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Update-Module with -Force" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.91 -Repository PSGallery
        Update-Module $testModuleName2 -Force

        $res = Get-PSResourcde $testModuleName2
        $res.version | Should -Contain "0.0.91" 
    }

    It "Update-Module with -RequiredVersion" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.91 -Repository PSGallery
        Update-Module $testModuleName2 -RequiredVersion 0.0.93 -WarningVariable wv
        $wv.Count | Should -Be 1
    }

    It "Update-Module multiple modules" {
        Install-Module "TestModuleWithDependencyB" -Repository PSGallery -RequiredVersion "2.0"
        Install-Module "TestModuleWithDependencyD" -Repository PSGallery -RequiredVersion "1.0"

        Update-Module "TestModuleWithDependencyB", "TestModuleWithDependencyD"
        $res = Get-PSResource "TestModuleWithDependencyB", "TestModuleWithDependencyD" 
        $res.count -ge 4 | Should -Be $true
        $res.version | Should -Contain "2.0"
        $res.version | Should -Contain "3.0"
    }

    It "Update-Module with RequiredVersion and wildcard" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.91 -Repository PSGallery
        Update-Module "testModule9*"
        $res = Get-PSResource $testModuleName2
        $res.count -ge 2 | Should -Be $true
        $res.version | Should -Contain "3.0"
    }

    It "Update-Module with -PassThru should return output" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.91 -Repository PSGallery
        $res = Update-Module $testModuleName2 -PassThru
        $res | Should -Not -BeNullOrEmpty
    }

    It "Update-Module with lower -RequiredVersion should not update" {
        Install-Module $testModuleName2 -Repository PSGallery
        Update-Module $testModuleName2 -RequiredVersion 0.0.1
        $res = Get-PSResource $testModuleName2
        $res.Count -eq 1 | Should -Be $true
        $res.version | Should -Be "0.0.93"
    }

    It "UpdateAllModules" {
        Install-Module $testModuleName2 -Repository PSGallery -RequiredVersion 0.0.7
        Install-Module "TestModuleWithDependencyB" -Repository PSGallery -RequiredVersion 2.0
        Install-Module "TestModuleWithDependencyD" -Repository PSGallery -RequiredVersion 1.0

        Update-Module $testModuleName2, "TestModuleWithDependencyB", "TestModuleWithDependencyD"
        $res = Get-PSResource $testModuleName2, "TestModuleWithDependencyB", "TestModuleWithDependencyD" 
        $res.Count -ge 3 | Should -Be $true
        $res.version | Should -Be "0.0.93"
    }

    It "Update-Module with not available -RequiredVersion" {
        Install-Module $testModuleName2 -RequiredVersion 3.0 -Repository PSGallery

        Update-Module $testModuleName2 -RequiredVersion 10.0

        $res = Get-PSResource $testModuleName2
        $res.Count -ge 0 | Should -Be $true
        $res.Version | Should -Not -Contain "10.0"
    }

    ### Broken?
    It "Update-Module with Dependencies" {
        $parentModule = "TestModuleWithDependencyC"
        $childModule1 = "TestModuleWithDependencyB"
        $childModule2 = "TestModuleWithDependencyD"
        $childModule3 = "TestModuleWithDependencyF"
        Install-Module $parentModule -Repository PSGallery -RequiredVersion "3.0"
        Update-Module $parentModule

        $res = Get-PSResource $parentModule, $childModule1, $childModule2, $childModule3
        $res.Count -ge 4 | Should -Be $true
        $res.Version | Should -Contain "5.0"
    }

    It "Update resource installed given Name parameter" {
        Install-PSResource -Name $testModuleName -Version "0.0.1" -Repository $PSGalleryName -TrustRepository
        
        Update-Module -Name $testModuleName -Repository $PSGalleryName -TrustRepository
        $res = Get-PSResource -Name $testModuleName

        $isPkgUpdated = $false
        foreach ($pkg in $res)
        {
            if ([System.Version]$pkg.Version -gt [System.Version]"0.0.1")
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
        $res = Get-PSResource -Name "test_mod*" -Version "5.0.0.0"

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
        $res = Get-PSResource -Name $testModuleName
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

    $testCases2 = @{Version="[3.0.0.0]";           ExpectedVersions=@("1.0.0.0", "3.0.0.0"); Reason="validate version, exact match"},
                  @{Version="3.0.0.0";             ExpectedVersions=@("1.0.0.0", "3.0.0.0"); Reason="validate version, exact match without bracket syntax"},
                  @{Version="[3.0.0.0, 5.0.0.0]";  ExpectedVersions=@("1.0.0.0", "3.0.0.0", "5.0.0.0"); Reason="validate version, exact range inclusive"},
                  @{Version="(3.0.0.0, 6.0.0.0)";  ExpectedVersions=@("1.0.0.0", "3.0.0.0", "5.0.0.0"); Reason="validate version, exact range exclusive"},
                  @{Version="(3.0.0.0,)";          ExpectedVersions=@("1.0.0.0", "5.0.0.0"); Reason="validate version, minimum version exclusive"},
                  @{Version="[3.0.0.0,)";          ExpectedVersions=@("1.0.0.0", "3.0.0.0", "5.0.0.0"); Reason="validate version, minimum version inclusive"},
                  @{Version="(,5.0.0.0)";          ExpectedVersions=@("1.0.0.0", "3.0.0.0"); Reason="validate version, maximum version exclusive"},
                  @{Version="(,5.0.0.0]";          ExpectedVersions=@("1.0.0.0", "3.0.0.0", "5.0.0.0"); Reason="validate version, maximum version inclusive"},
                  @{Version="[1.0.0.0, 5.0.0.0)";  ExpectedVersions=@("1.0.0.0", "3.0.0.0"); Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
                  @{Version="(1.0.0.0, 3.0.0.0]";  ExpectedVersions=@("1.0.0.0", "3.0.0.0"); Reason="validate version, mixed exclusive minimum and inclusive maximum version"}

    It "Update resource when given Name to <Reason> <Version>" -TestCases $testCases2{
        param($Version, $ExpectedVersions)

        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository
        Update-PSResource -Name $testModuleName -Version $Version -Repository $PSGalleryName -TrustRepository

        $res = Get-PSResource -Name $testModuleName

        foreach ($item in $res) {
            $item.Name | Should -Be $testModuleName
            $ExpectedVersions | Should -Contain $item.Version
        }
    }

    $testCases = @(
        @{Version='(3.0.0.0)';       Description="exclusive version (3.0.0.0)"},
        @{Version='[3-0-0-0]';       Description="version formatted with invalid delimiter [3-0-0-0]"}
    )
    It "Should not update resource with incorrectly formatted version such as <Description>" -TestCases $testCases{
        param($Version, $Description)

        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository
        Update-PSResource -Name $testModuleName -Version $Version -Repository $PSGalleryName -TrustRepository 2>$null

        $res = Get-PSResource -Name $testModuleName
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
        $res = Get-PSResource -Name $testModuleName

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

        $res = Get-PSResource -Name $testModuleName

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

        $res = Get-PSResource -Name $testModuleName

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

        $res = Get-PSResource -Name $testModuleName

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

        $res = Get-PSResource -Name $testModuleName

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

        $res = Get-PSResource -Name $testModuleName

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

    It "Update module using -WhatIf, should not update the module" {
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository
        Update-PSResource -Name $testModuleName -WhatIf -Repository $PSGalleryName -TrustRepository

        $res = Get-PSResource -Name $testModuleName

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
}

#>
