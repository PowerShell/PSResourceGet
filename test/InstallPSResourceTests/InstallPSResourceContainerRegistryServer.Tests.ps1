# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

Describe 'Test Install-PSResource for ACR scenarios' -tags 'CI' {

    BeforeAll {
        $testModuleName = "test-module"
        $testModuleName2 = "test-module2"
        $testCamelCaseModuleName = "test-camelCaseModule"
        $testCamelCaseScriptName = "test-camelCaseScript"
        $testModuleParentName = "test_parent_mod"
        $testModuleDependencyName = "test_dependency_mod"
        $testScriptName = "test-script"
        $ACRRepoName = "ACRRepo"
        $ACRRepoUri = "https://psresourcegettest.azurecr.io/"
        Get-NewPSResourceRepositoryFile

        $usingAzAuth = $env:USINGAZAUTH -eq 'true'

        if ($usingAzAuth)
        {
            Register-PSResourceRepository -Name $ACRRepoName -ApiVersion 'ContainerRegistry' -Uri $ACRRepoUri -Verbose
        }
        else
        {
            $psCredInfo = New-Object Microsoft.PowerShell.PSResourceGet.UtilClasses.PSCredentialInfo ("SecretStore", "$env:TENANTID")
            Register-PSResourceRepository -Name $ACRRepoName -ApiVersion 'ContainerRegistry' -Uri $ACRRepoUri -CredentialInfo $psCredInfo -Verbose
        }
    }

    AfterEach {
        Uninstall-PSResource $testModuleName, $testModuleName2, $testCamelCaseModuleName, $testScriptName, $testCamelCaseScriptName -Version "*" -SkipDependencyCheck -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    $testCases = @{Name="*";                        ErrorId="NameContainsWildcard"},
                 @{Name="Test-mod*";                ErrorId="NameContainsWildcard"},
                 @{Name="Test?modu","Test[module";  ErrorId="ErrorFilteringNamesForUnsupportedWildcards"}

    It "Should not install resource with wildcard in name" -TestCases $testCases {
        param($Name, $ErrorId)
        Install-PSResource -Name $Name -Repository $ACRRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "$ErrorId,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"
        $res = Get-InstalledPSResource $testModuleName
        $res | Should -BeNullOrEmpty
    }

    It "Install specific module resource by name" {
        Install-PSResource -Name $testModuleName -Repository $ACRRepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    It "Install specific script resource by name" {
        Install-PSResource -Name $testScriptName -Repository $ACRRepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testScriptName
        $pkg.Name | Should -BeExactly $testScriptName
        $pkg.Version | Should -Be "3.0.0"
        $pkg.Type.ToString() | Should -Be "Script"
    }

    It "Install script resource by name and version" {
        Install-PSResource -Name $testScriptName -Version "1.0.0" -Repository $ACRRepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testScriptName
        $pkg.Name | Should -Be $testScriptName
        $pkg.Version | Should -BeExactly "1.0.0"
    }

    It "Install multiple resources by name" {
        $pkgNames = @($testModuleName, $testModuleName2)
        Install-PSResource -Name $pkgNames -Repository $ACRRepoName -TrustRepository
        $pkg = Get-InstalledPSResource $pkgNames
        $pkg.Name | Should -Be $pkgNames
    }

    It "Should not install resource given nonexistant name" {
        Install-PSResource -Name "NonExistantModule" -Repository $ACRRepoName -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
        $pkg = Get-InstalledPSResource "NonExistantModule"
        $pkg | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ResourceNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"
    }

    # Do some version testing, but Find-PSResource should be doing thorough testing
    It "Should install resource given name and exact version" {
        Install-PSResource -Name $testModuleName -Version "1.0.0" -Repository $ACRRepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "1.0.0"
    }

    It "Should install resource given name and exact version with bracket syntax" {
        Install-PSResource -Name $testModuleName -Version "[1.0.0]" -Repository $ACRRepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "1.0.0"
    }

    It "Should install resource given name and exact range inclusive [1.0.0, 5.0.0]" {
        Install-PSResource -Name $testModuleName -Version "[1.0.0, 5.0.0]" -Repository $ACRRepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    It "Should install resource given name and exact range exclusive (1.0.0, 5.0.0)" {
        Install-PSResource -Name $testModuleName -Version "(1.0.0, 5.0.0)" -Repository $ACRRepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "3.0.0"
    }

    # TODO: Update this test and others like it that use try/catch blocks instead of Should -Throw
    It "Should not install resource with incorrectly formatted version such as exclusive version (1.0.0.0)" {
        $Version = "(1.0.0.0)"
        { Install-PSResource -Name $testModuleName -Version $Version -Repository $ACRRepoName -TrustRepository -ErrorAction SilentlyContinue } | Should -Throw -ErrorId "IncorrectVersionFormat,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"

        $res = Get-InstalledPSResource $testModuleName
        $res | Should -BeNullOrEmpty
    }

    It "Install resource when given Name, Version '*', should install the latest version" {
        Install-PSResource -Name $testModuleName -Version "*" -Repository $ACRRepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    It "Install resource with a dependency (should install both parent and dependency)" {
        Install-PSResource -Name $testModuleParentName -Repository $ACRRepoName -TrustRepository
        
        $parentPkg = Get-InstalledPSResource $testModuleParentName
        $parentPkg.Name | Should -Be $testModuleParentName
        $parentPkg.Version | Should -Be "1.0.0"
        $childPkg = Get-InstalledPSResource $testModuleDependencyName
        $childPkg.Name | Should -Be $testModuleDependencyName
        $childPkg.Version | Should -Be "1.0.0"
    }

    It "Install resource with latest (including prerelease) version given Prerelease parameter" {
        Install-PSResource -Name $testModuleName -Prerelease -Repository $ACRRepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.2.5"
        $pkg.Prerelease | Should -Be "alpha"
    }

    It "Install resource via InputObject by piping from Find-PSresource" {
        Find-PSResource -Name $testModuleName -Repository $ACRRepoName | Install-PSResource -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    It "Install resource with copyright, description and repository source location and validate properties" {
        Install-PSResource -Name $testModuleName -Version "3.0.0" -Repository $ACRRepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "3.0.0"
        $pkg.Copyright | Should -Be "(c) Anam Navied. All rights reserved."
        $pkg.Description | Should -Be "This is a test module, for PSGallery team internal testing. Do not take a dependency on this package. This version contains tags for the package."
        $pkg.RepositorySourceLocation | Should -Be $ACRRepoUri
    }

    # Windows only
    It "Install resource under CurrentUser scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $testModuleName -Repository $ACRRepoName -TrustRepository -Scope CurrentUser
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("Documents") | Should -Be $true
    }

    # Windows only
    It "Install resource under AllUsers scope - Windows only" -Skip:(!((Get-IsWindows) -and (Test-IsAdmin))) {
        Install-PSResource -Name $testModuleName -Repository $ACRRepoName -TrustRepository -Scope AllUsers -Verbose
        $pkg = Get-InstalledPSResource $testModuleName -Scope AllUsers
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("Program Files") | Should -Be $true
    }

    # Windows only
    It "Install resource under no specified scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $testModuleName -Repository $ACRRepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("Documents") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install resource under CurrentUser scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name $testModuleName -Repository $ACRRepoName -TrustRepository -Scope CurrentUser
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("$env:HOME/.local") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install resource under no specified scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name $testModuleName -Repository $ACRRepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("$env:HOME/.local") | Should -Be $true
    }

    It "Should not install resource that is already installed" {
        Install-PSResource -Name $testModuleName -Repository $ACRRepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        Install-PSResource -Name $testModuleName -Repository $ACRRepoName -TrustRepository -WarningVariable WarningVar -warningaction SilentlyContinue
        $WarningVar | Should -Not -BeNullOrEmpty
    }

    It "Reinstall resource that is already installed with -Reinstall parameter" {
        Install-PSResource -Name $testModuleName -Repository $ACRRepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
        Install-PSResource -Name $testModuleName -Repository $ACRRepoName -Reinstall -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    It "Install PSResourceInfo object piped in" {
        Find-PSResource -Name $testModuleName -Version "1.0.0" -Repository $ACRRepoName | Install-PSResource -TrustRepository
        $res = Get-InstalledPSResource -Name $testModuleName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "1.0.0"
    }

    It "Install module using -PassThru" {
        $res = Install-PSResource -Name $testModuleName -Repository $ACRRepoName -PassThru -TrustRepository
        $res.Name | Should -Contain $testModuleName
    }

    It "Install module with varying case sensitivity" {
        Install-PSResource -Name $testCamelCaseModuleName -Repository $ACRRepoName -TrustRepository
        $res = Get-InstalledPSResource -Name $testCamelCaseModuleName
        $res.Name | Should -BeExactly $testCamelCaseModuleName
        $res.Version | Should -Be "1.0.0"
        $res.Type.ToString() | Should -Be "Module"
    }

    It "Install script with varying case sensitivity" {
        Install-PSResource -Name $testCamelCaseScriptName -Repository $ACRRepoName -TrustRepository
        $res = Get-InstalledPSResource -Name $testCamelCaseScriptName
        $res.Name | Should -BeExactly $testCamelCaseScriptName
        $res.Version | Should -Be "1.0.0"
        $res.Type.ToString() | Should -Be "Script"
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
