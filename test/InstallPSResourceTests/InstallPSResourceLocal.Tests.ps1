# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"

Describe 'Test Install-PSResource for local repositories' -tags 'CI' {


    BeforeAll {
        $localRepo = "psgettestlocal"
        $testModuleName = "test_local_mod"
        $testModuleName2 = "test_local_mod2"
        $testModuleClobber = "testModuleClobber"
        $testModuleClobber2 = "testModuleClobber2"
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos

        $prereleaseLabel = "alpha001"
        $tags = @()

        New-TestModule -moduleName $testModuleName -repoName $localRepo -packageVersion "1.0.0" -prereleaseLabel "" -tags $tags
        New-TestModule -moduleName $testModuleName -repoName $localRepo -packageVersion "3.0.0" -prereleaseLabel "" -tags $tags
        New-TestModule -moduleName $testModuleName -repoName $localRepo -packageVersion "5.0.0" -prereleaseLabel "" -tags $tags
        New-TestModule -moduleName $testModuleName -repoName $localRepo -packageVersion "5.2.5" -prereleaseLabel $prereleaseLabel -tags $tags

        New-TestModule -moduleName $testModuleName2 -repoName $localRepo -packageVersion "1.0.0" -prereleaseLabel "" -tags $tags
        New-TestModule -moduleName $testModuleName2 -repoName $localRepo -packageVersion "5.0.0" -prereleaseLabel "" -tags $tags

        New-TestModule -moduleName $testModuleClobber -repoName $localRepo -packageVersion "1.0.0" -prereleaseLabel "" -cmdletToExport 'Test-Cmdlet1' -cmdletToExport2 'Test-Cmdlet2'
        New-TestModule -moduleName $testModuleClobber2 -repoName $localRepo -packageVersion "1.0.0" -prereleaseLabel "" -cmdletToExport 'Test-Cmdlet1'
    }

    AfterEach {
        Uninstall-PSResource $testModuleName, $testModuleName2, "RequiredModule*", $testModuleClobber, $testModuleClobber2 -Version "*" -SkipDependencyCheck -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Install resource given Name parameter" {
        Install-PSResource -Name $testModuleName -Repository $localRepo -TrustRepository
        $res = Get-InstalledPSResource -Name $testModuleName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0"
    }

    It "Install resource given Name and Version (specific) parameters" {
        Install-PSResource -Name $testModuleName -Version "3.0.0" -Repository $localRepo -TrustRepository
        $res = Get-InstalledPSResource -Name $testModuleName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "3.0.0"
    }

    It "Install multiple resources by name" {
        $pkgNames = @($testModuleName, $testModuleName2)
        Install-PSResource -Name $pkgNames -Repository $localRepo -TrustRepository  
        $pkg = Get-InstalledPSResource $pkgNames
        $pkg.Name | Should -Be $pkgNames
    }

    It "Should not install resource given nonexistant name" {
        $res = Install-PSResource -Name "NonExistantModule" -Repository $localRepo -TrustRepository -PassThru -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "InstallPackageFailure,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource"
    }

    It "Should install resource given name and exact version with bracket syntax" {
        Install-PSResource -Name $testModuleName -Version "[1.0.0.0]" -Repository $localRepo -TrustRepository  
        $res = Get-InstalledPSResource $testModuleName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "1.0.0"
    }

    It "Should install resource given name and exact range inclusive [1.0.0.0, 5.0.0.0]" {
        Install-PSResource -Name $testModuleName -Version "[1.0.0.0, 5.0.0.0]" -Repository $localRepo -TrustRepository
        $res = Get-InstalledPSResource $testModuleName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0"
    }

    It "Should install resource given name and exact range exclusive (1.0.0.0, 5.0.0.0)" {
        Install-PSResource -Name $testModuleName -Version "(1.0.0.0, 5.0.0.0)" -Repository $localRepo -TrustRepository  
        $res = Get-InstalledPSResource $testModuleName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "3.0.0"
    }

    It "Should not install resource with incorrectly formatted version such as exclusive version (1.0.0.0)" {
        $Version = "(1.0.0.0)"
        try {
            Install-PSResource -Name $testModuleName -Version $Version -Repository $localRepo -TrustRepository -ErrorAction SilentlyContinue
        }
        catch
        {}
        $Error[0].FullyQualifiedErrorId | Should -be "IncorrectVersionFormat,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource"

        $res = Get-InstalledPSResource $testModuleName
        $res | Should -BeNullOrEmpty
    }

    It "Install resource when given Name, Version '*', should install the latest version" {
        Install-PSResource -Name $testModuleName -Version "*" -Repository $localRepo -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    It "Install resource when given Name, Version '3.*', should install the appropriate version" {
        Install-PSResource -Name $testModuleName -Version "3.*" -Repository $localRepo -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "3.0.0"
    }

    It "Install resource with latest (including prerelease) version given Prerelease parameter" {
        Install-PSResource -Name $testModuleName -Prerelease -Repository $localRepo -TrustRepository 
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.2.5"
        $pkg.Prerelease | Should -Be "alpha001"
    }

    It "Install resource with cmdlet names from a module already installed with -NoClobber (should not clobber)" {
        Install-PSResource -Name $testModuleClobber -Repository $localRepo -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleClobber
        $pkg.Name | Should -Be $testModuleClobber
        $pkg.Version | Should -Be "1.0.0"

        Install-PSResource -Name $testModuleClobber2 -Repository $localRepo -TrustRepository -NoClobber -ErrorVariable ev -ErrorAction SilentlyContinue
        $pkg = Get-InstalledPSResource $testModuleClobber2
        $pkg | Should -BeNullOrEmpty
        $ev.Count | Should -Be 1
        $ev[0] | Should -Be "testModuleClobber2 package could not be installed with error: The following commands are already available on this system: 'Test-Cmdlet1, Test-Cmdlet1'. This module 'testModuleClobber2' may override the existing commands. If you still want to install this module 'testModuleClobber2', remove the -NoClobber parameter."
    }

    It "Install resource with cmdlet names from a module already installed (should clobber)" {
        Install-PSResource -Name $testModuleClobber -Repository $localRepo -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleClobber
        $pkg.Name | Should -Be $testModuleClobber
        $pkg.Version | Should -Be "1.0.0"

        Install-PSResource -Name $testModuleClobber2 -Repository $localRepo -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleClobber2
        $pkg.Name | Should -Be $testModuleClobber2
        $pkg.Version | Should -Be "1.0.0"
    }

    It "Install resource with -NoClobber (should install)" {
        Install-PSResource -Name $testModuleClobber -Repository $localRepo -TrustRepository -NoClobber
        $pkg = Get-InstalledPSResource $testModuleClobber
        $pkg.Name | Should -Be $testModuleClobber
        $pkg.Version | Should -Be "1.0.0"
    }

    It "Install resource via InputObject by piping from Find-PSresource" {
        Find-PSResource -Name $testModuleName -Repository $localRepo | Install-PSResource -TrustRepository 
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    It "Install resource via InputObject by piping from Find-PSResource" {
        $modules = Find-PSResource -Name "*" -Repository $localRepo 
        $modules.Count | Should -BeGreaterThan 1

        Install-PSResource -TrustRepository -InputObject $modules

        $pkg = Get-InstalledPSResource $modules.Name
        $pkg.Count | Should -BeGreaterThan 1
    }

    It "Install resource under location specified in PSModulePath" {
        Install-PSResource -Name $testModuleName -Repository $localRepo -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName 
        ($env:PSModulePath).Contains($pkg.InstalledLocation)
    }

    # Windows only
    It "Install resource under CurrentUser scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $testModuleName -Repository $localRepo -TrustRepository -Scope CurrentUser
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("Documents") | Should -Be $true
    }

    # Windows only
    It "Install resource under AllUsers scope - Windows only" -Skip:(!((Get-IsWindows) -and (Test-IsAdmin))) {
        Install-PSResource -Name $testModuleName -Repository $localRepo -TrustRepository -Scope AllUsers -Verbose
        $pkg = Get-Module $testModuleName -ListAvailable
        $pkg.Name | Should -Be $testModuleName
        $pkg.Path.ToString().Contains("Program Files")
    }

    # Windows only
    It "Install resource under no specified scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $testModuleName -Repository $localRepo -TrustRepository 
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("Documents") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install resource under CurrentUser scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name $testModuleName -Repository $localRepo -TrustRepository -Scope CurrentUser
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("$env:HOME/.local") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install resource under no specified scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name $testModuleName -Repository $localRepo -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("$env:HOME/.local") | Should -Be $true
    }

    It "Should not install resource that is already installed" {
        Install-PSResource -Name $testModuleName -Repository $localRepo -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        Install-PSResource -Name $testModuleName -Repository $localRepo -TrustRepository -WarningVariable WarningVar -warningaction SilentlyContinue
        $WarningVar | Should -Not -BeNullOrEmpty
    }

    It "Reinstall resource that is already installed with -Reinstall parameter" {
        Install-PSResource -Name $testModuleName -Repository $localRepo -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
        Install-PSResource -Name $testModuleName -Repository $localRepo -Reinstall -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    It "Install module using -WhatIf, should not install the module" {
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $localRepo -TrustRepository -WhatIf
        $res = Get-InstalledPSResource -Name $testModuleName
        $res | Should -BeNullOrEmpty
    }

    It "Install resource given -Name and -PassThru parameters" {
        $res = Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $localRepo -TrustRepository -PassThru
        $res.Name | Should -Contain $testModuleName
        $res.Version | Should -Be "1.0.0"
    }
}
