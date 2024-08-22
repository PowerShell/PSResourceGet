# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

Describe 'Test Install-PSResource for V3Server scenarios' -tags 'CI' {

    BeforeAll {
        $testModuleName = "test_local_mod"
        $testModuleName2 = "test_local_mod2"
        $testScriptName = "test_ado_script"
        $ADORepoName = "PSGetTestingPublicFeed"
        $ADORepoUri = "https://pkgs.dev.azure.com/powershell/PowerShell/_packaging/psresourceget-public-test-ci/nuget/v3/index.json"
        Get-NewPSResourceRepositoryFile
        Register-PSResourceRepository -Name $ADORepoName -Uri $ADORepoUri
    }

    AfterEach {
        Uninstall-PSResource $testModuleName, $testModuleName2, $testScriptName -Version "*" -SkipDependencyCheck -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    $testCases = @{Name="*";                        ErrorId="NameContainsWildcard"},
                 @{Name="Test_local_m*";            ErrorId="NameContainsWildcard"},
                 @{Name="Test?local","Test[local";  ErrorId="ErrorFilteringNamesForUnsupportedWildcards"}

    It "Should not install resource with wildcard in name" -TestCases $testCases {
        param($Name, $ErrorId)
        Install-PSResource -Name $Name -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "$ErrorId,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"
        $res = Get-InstalledPSResource $testModuleName
        $res | Should -BeNullOrEmpty
    }

    It "Install specific module resource by name" {
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    It "Install specific script resource by name" {
        Install-PSResource -Name $testScriptName -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testScriptName
        $pkg.Name | Should -Be $testScriptName
        $pkg.Version | Should -Be "1.0.0"
    }

    It "Install multiple resources by name" {
        $pkgNames = @($testModuleName, $testModuleName2)
        Install-PSResource -Name $pkgNames -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $pkgNames
        $pkg.Name | Should -Be $pkgNames
    }

    It "Should not install resource given nonexistant name" {
        Install-PSResource -Name "NonExistantModule" -Repository $ADORepoName -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
        $pkg = Get-InstalledPSResource "NonExistantModule"
        $pkg | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "InstallPackageFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"
    }

    # Do some version testing, but Find-PSResource should be doing thorough testing
    It "Should install resource given name and exact version" {
        Install-PSResource -Name $testModuleName -Version "1.0.0" -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "1.0.0"
    }

    It "Should install resource given name and exact version with bracket syntax" {
        Install-PSResource -Name $testModuleName -Version "[1.0.0]" -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "1.0.0"
    }

    It "Should install resource given name and exact range inclusive [1.0.0, 5.0.0]" {
        Install-PSResource -Name $testModuleName -Version "[1.0.0, 5.0.0]" -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    It "Should install resource given name and exact range exclusive (1.0.0, 5.0.0)" {
        Install-PSResource -Name $testModuleName -Version "(1.0.0, 5.0.0)" -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "3.0.0"
    }

    # TODO: Update this test and others like it that use try/catch blocks instead of Should -Throw
    It "Should not install resource with incorrectly formatted version such as exclusive version (1.0.0.0)" {
        $Version = "(1.0.0.0)"
        try {
            Install-PSResource -Name $testModuleName -Version $Version -Repository $ADORepoName -TrustRepository -ErrorAction SilentlyContinue
        }
        catch
        {}
        $Error[0].FullyQualifiedErrorId | Should -be "IncorrectVersionFormat,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"

        $res = Get-InstalledPSResource $testModuleName
        $res | Should -BeNullOrEmpty
    }

    It "Install resource when given Name, Version '*', should install the latest version" {
        Install-PSResource -Name $testModuleName -Version "*" -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    It "Install resource with latest (including prerelease) version given Prerelease parameter" {
        Install-PSResource -Name $testModuleName -Prerelease -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.2.5"
        $pkg.Prerelease | Should -Be "alpha001"
    }

    It "Install resource via InputObject by piping from Find-PSresource" {
        Find-PSResource -Name $testModuleName -Repository $ADORepoName | Install-PSResource -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    It "Install resource with companyname and repository source location and validate properties" {
        Install-PSResource -Name $testModuleName -Version "5.2.5-alpha001" -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Version | Should -Be "5.2.5"
        $pkg.Prerelease | Should -Be "alpha001"

        $pkg.CompanyName | Should -Be "None"
        $pkg.RepositorySourceLocation | Should -Be $ADORepoUri
    }

    # Windows only
    It "Install resource under CurrentUser scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository -Scope CurrentUser
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("Documents") | Should -Be $true
    }

    # Windows only
    It "Install resource under AllUsers scope - Windows only" -Skip:(!((Get-IsWindows) -and (Test-IsAdmin))) {
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository -Scope AllUsers -Verbose
        $pkg = Get-InstalledPSResource $testModuleName -Scope AllUsers
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("Program Files") | Should -Be $true
    }

    # Windows only
    It "Install resource under no specified scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("Documents") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install resource under CurrentUser scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository -Scope CurrentUser
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("$env:HOME/.local") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install resource under no specified scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("$env:HOME/.local") | Should -Be $true
    }

    It "Should not install resource that is already installed" {
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository -WarningVariable WarningVar -warningaction SilentlyContinue
        $WarningVar | Should -Not -BeNullOrEmpty
    }

    It "Reinstall resource that is already installed with -Reinstall parameter" {
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -Reinstall -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    It "Install PSResourceInfo object piped in" {
        Find-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $ADORepoName | Install-PSResource -TrustRepository
        $res = Get-InstalledPSResource -Name $testModuleName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "1.0.0"
    }

    It "Install module using -PassThru" {
        $res = Install-PSResource -Name $testModuleName -Repository $ADORepoName -PassThru -TrustRepository
        $res.Name | Should -Contain $testModuleName
    }
}

Describe 'Test Install-PSResource for V3Server scenarios' -tags 'ManualValidationOnly' {

    BeforeAll {
        $testModuleName = "TestModule"
        $testModuleName2 = "testModuleWithlicense"
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos
    }

    AfterEach {
        Uninstall-PSResource $testModuleName, $testModuleName2 -SkipDependencyCheck -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    # Unix only manual test
    # Expected path should be similar to: '/usr/local/share/powershell/Modules'
    It "Install resource under AllUsers scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name $testModuleName -Repository $TestGalleryName -Scope AllUsers
        $pkg = Get-Module $testModuleName -ListAvailable
        $pkg.Name | Should -Be $testModuleName
        $pkg.Path.Contains("/usr/") | Should -Be $true
    }

    # This needs to be manually tested due to prompt
    It "Install resource that requires accept license without -AcceptLicense flag" {
        Install-PSResource -Name $testModuleName2  -Repository $TestGalleryName
        $pkg = Get-InstalledPSResource $testModuleName2
        $pkg.Name | Should -Be $testModuleName2
        $pkg.Version | Should -Be "0.0.1.0"
    }

    # This needs to be manually tested due to prompt
    It "Install resource should prompt 'trust repository' if repository is not trusted" {
        Set-PSResourceRepository PoshTestGallery -Trusted:$false

        Install-PSResource -Name $testModuleName -Repository $TestGalleryName -confirm:$false

        $pkg = Get-Module $testModuleName -ListAvailable
        $pkg.Name | Should -Be $testModuleName

        Set-PSResourceRepository PoshTestGallery -Trusted
    }
}
