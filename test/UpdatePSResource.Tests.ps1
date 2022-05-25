# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Update-PSResource' {


    BeforeAll {
        $PSGalleryName = Get-PSGalleryName
        $localRepoName = "psgettestlocal"
        $testModuleName = "test_module"
        $testModuleName2 = "test_module2"
        $testScriptName = "test_script"
        $testLocalModuleName = "TestMyLocalModule"
        $testLocalModuleName2 = "TestMyLocalModule2"
        $testLocalScriptName = "TestMyLocalScripts"

        Get-NewPSResourceRepositoryFile
        Register-LocalRepos

        Get-ModuleResourcePublishedToLocalRepoTestDrive $testLocalModuleName $localRepoName "1.0.0.0"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $testLocalModuleName $localRepoName "3.0.0.0"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $testLocalModuleName $localRepoName "5.0.0.0"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $testLocalModuleName $localRepoName "5.2.5.0" "alpha"
        
        Get-ModuleResourcePublishedToLocalRepoTestDrive $testLocalModuleName2 $localRepoName "1.0.0.0"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $testLocalModuleName2 $localRepoName "3.0.0.0"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $testLocalModuleName2 $localRepoName "5.0.0.0"

        Get-ScriptResourcePublishedToLocalRepoTestDrive $testLocalScriptName $localRepoName "1.0.0.0"
        Get-ScriptResourcePublishedToLocalRepoTestDrive $testLocalScriptName $localRepoName "3.0.0.0"
    }

    BeforeEach {
        $null = Install-PSResource $testLocalModuleName -Version "1.0.0.0" -Repository $localRepoName -TrustRepository
        $null = Install-PSResource $testLocalScriptName -Version "1.0.0.0" -Repository $localRepoName -TrustRepository
    }

    AfterEach {
        # Uninstall-PSResource "test_module", "TestModule99", "TestModuleWithLicense", "test_module2", "test_script"
        Uninstall-PSResource -Name $testLocalModuleName -Version "*"
        Uninstall-PSResource -Name $testLocalModuleName2 -Version "*"
        Uninstall-PSResource -Name $testLocalScriptName -Version "*"
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    # BeforeAll{
    #     $testModuleName = "test_module2"
    #     $testScriptName = "test_script"
    #     $testLocalModuleName = "TestMyLocalModule"
    #     $testLocalScriptName = "TestMyLocalScripts"

    #     Get-NewPSResourceRepositoryFile
    #     Uninstall-PSResource -Name $testModuleName -Version "*"
    #     Uninstall-PSResource -Name $testScriptName -Version "*"
    #     Uninstall-PSResource -Name $testLocalModuleName -Version "*"
    #     Uninstall-PSResource -Name $testLocalScriptName -Version "*"
    #     Register-LocalRepos


    #     $PSGalleryName = Get-PSGalleryName
    #     $localRepoName = "psgettestlocal"
    #     Get-ModuleResourcePublishedToLocalRepoTestDrive $testLocalModuleName $localRepoName "1.0.0.0"
    #     Get-ModuleResourcePublishedToLocalRepoTestDrive $testLocalModuleName $localRepoName "3.0.0.0"
    #     Get-ModuleResourcePublishedToLocalRepoTestDrive $testLocalModuleName $localRepoName "4.0.0.0" "alpha"
    #     Get-ModuleResourcePublishedToLocalRepoTestDrive $testLocalModuleName $localRepoName "4.3.0.0" "beta"
    #     Get-ModuleResourcePublishedToLocalRepoTestDrive $testLocalModuleName $localRepoName "5.0.0.0"

    #     Get-ScriptResourcePublishedToLocalRepoTestDrive $testLocalScriptName $localRepoName "1.0.0.0"
    #     Get-ScriptResourcePublishedToLocalRepoTestDrive $testLocalScriptName $localRepoName "3.0.0.0"
    #     Get-ScriptResourcePublishedToLocalRepoTestDrive $testLocalScriptName $localRepoName "4.0.0.0" "alpha"
    #     Get-ScriptResourcePublishedToLocalRepoTestDrive $testLocalScriptName $localRepoName "4.3.0.0" "beta"
    #     Get-ScriptResourcePublishedToLocalRepoTestDrive $testLocalScriptName $localRepoName "5.0.0.0"
    # }

    # BeforeEach {
    #     $null = Install-PSResource $testLocalModuleName -Version "1.0.0.0" -Repository $localRepoName -TrustRepository
    #     $null = Install-PSResource $testLocalScriptName -Version "1.0.0.0" -Repository $localRepoName -TrustRepository
    # }

    # AfterEach {
    #     Uninstall-PSResource -Name $testLocalModuleName -Version "1.0.0.0"
    #     Uninstall-PSResource -Name $testLocalScriptName -Version "1.0.0.0"
    # }

    # AfterAll {
    #     Get-RevertPSResourceRepositoryFile
    # }

    It "update resource installed given Name parameter" {
        Update-PSResource -Name $testLocalModuleName -Repository $localRepoName -TrustRepository
        $res = Get-PSResource -Name $testLocalModuleName

        # originally installed version should still be installed
        $res.Version | Should -Contain "1.0.0.0"
        # version updated to should also be installed
        $res.Version | Should -Contain "5.0.0.0"

    }

    It "update resources installed given Name (with wildcard) parameter" {
        Install-PSResource $testLocalModuleName2 -Version "1.0.0.0" -Repository $localRepoName -TrustRepository

        # this should update TestMyLocalModule and TestMyLocalModule2 each to version 5.0.0.0 (highest version of both modules published to psgettestlocal repo)
        Update-PSResource -Name "TestMyLocalMod*" -Repository $localRepoName -TrustRepository

        Get-PSResource -Name $testLocalModuleName -Version "5.0.0.0" | Should -Not -BeNullOrEmpty
        Get-PSResource -Name $testLocalModuleName2 -Version "5.0.0.0" | Should -Not -BeNullOrEmpty
    }

    It "update resource installed given Name and Version (specific) parameters" {
        Update-PSResource -Name $testLocalModuleName -Version "3.0.0.0" -Repository $localRepoName -TrustRepository
        Get-PSResource -Name $testLocalModuleName -Version "3.0.0.0" | Should -Not -BeNullOrEmpty
    }

    $testCases2 = @{Version="[3.0.0.0]";           ExpectedVersions=@("1.0.0.0", "3.0.0.0"); Reason="validate version, exact match"},
                  @{Version="3.0.0.0";             ExpectedVersions=@("1.0.0.0", "3.0.0.0"); Reason="validate version, exact match without bracket syntax"},
                  @{Version="[3.0.0.0, 5.0.0.0]";  ExpectedVersions=@("1.0.0.0", "3.0.0.0", "5.0.0.0"); Reason="validate version, exact range inclusive"},
                  @{Version="(3.0.0.0, 5.0.0.0)";  ExpectedVersions=@("1.0.0.0", "3.0.0.0"); Reason="validate version, exact range exclusive"},
                  @{Version="(3.0.0.0,)";          ExpectedVersions=@("1.0.0.0", "5.0.0.0"); Reason="validate version, minimum version exclusive"},
                  @{Version="[3.0.0.0,)";          ExpectedVersions=@("1.0.0.0", "3.0.0.0", "5.0.0.0"); Reason="validate version, minimum version inclusive"},
                  @{Version="(,5.0.0.0)";          ExpectedVersions=@("1.0.0.0", "3.0.0.0"); Reason="validate version, maximum version exclusive"},
                  @{Version="(,5.0.0.0]";          ExpectedVersions=@("1.0.0.0", "3.0.0.0", "5.0.0.0"); Reason="validate version, maximum version inclusive"},
                  @{Version="[1.0.0.0, 5.0.0.0)";  ExpectedVersions=@("1.0.0.0", "3.0.0.0"); Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
                  @{Version="(1.0.0.0, 3.0.0.0]";  ExpectedVersions=@("1.0.0.0", "3.0.0.0"); Reason="validate version, mixed exclusive minimum and inclusive maximum version"}

    It "update resource when given Name to <Reason> <Version>" -TestCases $testCases2{
        param($Version, $ExpectedVersions)

        Update-PSResource -Name $testLocalModuleName -Version $Version -Repository $localRepoName -TrustRepository
        $res = Get-PSResource -Name $testLocalModuleName

        foreach ($item in $res) {
            $ExpectedVersions | Should -Contain $item.Version
        }
    }

    $testCases = @(
        @{Version='(3.0.0.0)';       Description="exclusive version (3.0.0.0)"},
        @{Version='[3-0-0-0]';       Description="version formatted with invalid delimiter [3-0-0-0]"}
    )
    It "Should not update resource with incorrectly formatted version such as <Description>" -TestCases $testCases{
        param($Version, $Description)

        Update-PSResource -Name $testLocalModuleName -Version $Version -Repository $localRepoName -TrustRepository
        Get-PSResource -Name $testLocalModuleName -Version "3.0.0.0" | Should -BeNullOrEmpty
    }

    It "update resource with latest (including prerelease) version given Prerelease parameter" {
        Update-PSResource -Name $testLocalModuleName -Prerelease -Repository $localRepoName -TrustRepository
        Get-PSResource -Name $testLocalModuleName -Version "5.2.5-alpha" | Should -Not -BeNullOrEmpty
    }

    # Windows only
    It "update resource under CurrentUser scope" -skip:(!$IsWindows) {
        Install-PSResource -Name $testLocalModuleName2 -Version "1.0.0.0" -Repository $localRepoName -TrustRepository -Scope AllUsers
        Install-PSResource -Name $testLocalModuleName2 -Version "1.0.0.0" -Repository $localRepoName -TrustRepository -Scope CurrentUser

        Update-PSResource -Name $testLocalModuleName2 -Version "3.0.0.0" -Repository $localRepoName -TrustRepository -Scope CurrentUser

        $res = Get-PSResource -Name $testLocalModuleName2 -Version "3.0.0.0"
        $res.InstalledLocation.Contains("Documents") | Should -Be $true
    }

    # Windows only
    It "update resource under AllUsers scope" -skip:(!($IsWindows -and (Test-IsAdmin))) {
        Install-PSResource -Name $testLocalModuleName2 -Version "1.0.0.0" -Repository $localRepoName -TrustRepository -Scope AllUsers -Verbose
        Install-PSResource -Name $testLocalModuleName2 -Version "1.0.0.0" -Repository $localRepoName -TrustRepository -Scope CurrentUser -Verbose

        Update-PSResource -Name $testLocalModuleName2 -Version "3.0.0.0" -Repository $localRepoName -TrustRepository -Scope AllUsers -Verbose

        $res = Get-PSResource -Name $testLocalModuleName2 -Version "3.0.0.0"
        $res.InstalledLocation.Contains("Program Files") | Should -Be $true
    }

    # Windows only
    It "update resource under no specified scope" -skip:(!$IsWindows) {
        Update-PSResource -Name $testLocalModuleName -Version "3.0.0.0" -Repository $localRepoName -TrustRepository

        $res = Get-PSResource -Name $testModuleName -Version "3.0.0.0"
        $res | Should -Not -BeNullOrEmpty
        $res.InstalledLocation.Contains("Documents") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Update resource under CurrentUser scope - Unix only" -Skip:(Get-IsWindows) {
        # this line is commented out because AllUsers scope requires sudo and that isn't supported in CI yet
        # Install-PSResource -Name $testLocalModuleName2 -Version "1.0.0.0" -Repository $localRepoName -Scope AllUsers
        Install-PSResource -Name $testLocalModuleName2 -Version "1.0.0.0" -Repository $localRepoName -TrustRepository -Scope CurrentUser

        Update-PSResource -Name $testLocalModuleName2 -Version "3.0.0.0" -Repository $localRepoName -TrustRepository -Scope CurrentUser

        $res = Get-PSResource -Name $testLocalModuleName2 -Version "3.0.0.0"
        $res | Should -BeNullOrEmpty
        $res.InstalledLocation.Contains("$env:HOME/.local") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/usr/local/share/powershell/Modules'
    # this test is skipped because it requires sudo to run and has yet to be resolved in CI
    It "Update resource under AllUsers scope - Unix only" -Skip:($true) {
        Install-PSResource -Name $testLocalModuleName2 -Version "1.0.0.0" -Repository $localRepoName -TrustRepository -Scope AllUsers
        Install-PSResource -Name $testLocalModuleName2 -Version "1.0.0.0" -Repository $localRepoName -TrustRepository -Scope CurrentUser

        Update-PSResource -Name $testLocalModuleName2 -Version "3.0.0.0" -Repository $localRepoName -TrustRepository -Scope AllUsers

        $res = Get-PSResource -Name $testLocalModuleName2 -Version "3.0.0.0"
        $res.InstalledLocation.Contains("usr") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Update resource under no specified scope - Unix only" -Skip:(Get-IsWindows) {
        # this is commented out because it requires sudo to run with AllUsers scope and this hasn't been resolved in CI yet
        # Install-PSResource -Name $testLocalModuleName2 -Version "1.0.0.0" -Repository $localRepoName -Scope AllUsers
        Install-PSResource -Name $testLocalModuleName2 -Version "1.0.0.0" -Repository $localRepoName -TrustRepository -Scope CurrentUser

        Update-PSResource -Name $testLocalModuleName2 -Version "3.0.0.0" -Repository $localRepoName -TrustRepository

        $res = Get-PSResource -Name $testLocalModuleName2 -Version "3.0.0.0"
        $res | Should -Not -BeNullOrEmpty
        $res.InstalledLocation.Contains("$env:HOME/.local") | Should -Be $true
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
        Update-PSResource -Name $testLocalModuleName -WhatIf -Repository $localRepoName -TrustRepository

        $res = Get-PSResource -Name $testLocalModuleName -Version "5.0.0.0"
        $res | Should -BeNullOrEmpty
    }

    It "update resource installed given -Name and -PassThru parameters" {
        $res = Update-PSResource -Name $testLocalModuleName -Version "3.0.0.0" -Repository $localRepoName -TrustRepository -PassThru
        $res.Name | Should -Contain $testLocalModuleName
        $res.Version | Should -Contain "3.0.0.0"
    }
}
