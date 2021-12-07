# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Install-PSResource for Module' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        $NuGetGalleryName = Get-NuGetGalleryName
        $testModuleName = "TestModule"
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos
    }

    AfterEach {
        Uninstall-PSResource "TestModule", "TestModule99", "myTestModule", "myTestModule2", "testModulePrerelease", 
            "testModuleWithlicense","PSGetTestModule", "PSGetTestDependency1", "TestFindModule","ClobberTestModule1",
            "ClobberTestModule2" -SkipDependencyCheck -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    $testCases = @{Name="*";                          ErrorId="NameContainsWildcard"},
                 @{Name="TestModule*";                ErrorId="NameContainsWildcard"},
                 @{Name="Test?Module","Test[Module";  ErrorId="ErrorFilteringNamesForUnsupportedWildcards"}

    It "Should not install resource with wildcard in name" -TestCases $testCases {
        param($Name, $ErrorId)
        Install-PSResource -Name $Name -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "$ErrorId,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource"
    }

    It "Install specific module resource by name" {
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName 
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 
        $pkg.Version | Should -Be "1.3.0"
    }

    It "Install specific script resource by name" {
        Install-PSResource -Name "TestTestScript" -Repository $TestGalleryName  
        $pkg = Get-PSResource "TestTestScript"
        $pkg.Name | Should -Be "TestTestScript" 
        $pkg.Version | Should -Be "1.3.1.0"
    }

    It "Install multiple resources by name" {
        $pkgNames = @("TestModule","TestModule99")
        Install-PSResource -Name $pkgNames -Repository $TestGalleryName  
        $pkg = Get-Module $pkgNames -ListAvailable
        $pkg.Name | Should -Be $pkgNames
    }

    It "Should not install resource given nonexistant name" {
        Install-PSResource -Name "NonExistantModule" -Repository $TestGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $pkg = Get-Module "NonExistantModule" -ListAvailable
        $pkg.Name | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ResourceNotFoundError,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource" 
    }

    # Do some version testing, but Find-PSResource should be doing thorough testing
    It "Should install resource given name and exact version" {
        Install-PSResource -Name "TestModule" -Version "1.2.0" -Repository $TestGalleryName  
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule"
        $pkg.Version | Should -Be "1.2.0"
    }

    It "Should install resource given name and exact version with bracket syntax" {
        Install-PSResource -Name "TestModule" -Version "[1.2.0]" -Repository $TestGalleryName  
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule"
        $pkg.Version | Should -Be "1.2.0"
    }

    It "Should install resource given name and exact range inclusive [1.0.0, 1.1.1]" {
        Install-PSResource -Name "TestModule" -Version "[1.0.0, 1.1.1]" -Repository $TestGalleryName  
        $pkg = Get-Module "TestModule" -ListAvailable 
        $pkg.Name | Should -Be "TestModule"
        $pkg.Version | Should -Be "1.1.1"
    }

    It "Should install resource given name and exact range exclusive (1.0.0, 1.1.1)" {
        Install-PSResource -Name "TestModule" -Version "(1.0.0, 1.1.1)" -Repository $TestGalleryName  
        $pkg = Get-Module "TestModule" -ListAvailable 
        $pkg.Name | Should -Be "TestModule"
        $pkg.Version | Should -Be "1.1"
    }

    It "Should not install resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(1.2.0.0)';       Description="exclusive version (2.10.0.0)"},
        @{Version='[1-2-0-0]';       Description="version formatted with invalid delimiter [1-2-0-0]"}
    ) {
        param($Version, $Description)

        Install-PSResource -Name "TestModule" -Version $Version -Repository $TestGalleryName -ErrorAction SilentlyContinue
        $Error[0].FullyQualifiedErrorId | Should -be "ResourceNotFoundError,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource"

        $res = Get-Module "TestModule" -ListAvailable
        $res | Should -BeNullOrEmpty
    }

    It "Install resource when given Name, Version '*', should install the latest version" {
        Install-PSResource -Name "TestModule" -Version "*" -Repository $TestGalleryName
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule"
        $pkg.Version | Should -Be "1.3.0"
    }

    It "Install resource with latest (including prerelease) version given Prerelease parameter" {
        Install-PSResource -Name "TestModulePrerelease" -Prerelease -Repository $TestGalleryName 
        $pkg = Get-Module "TestModulePrerelease" -ListAvailable
        $pkg.Name | Should -Be "TestModulePrerelease"
        $pkg.Version | Should -Be "0.0.1"
        $pkg.PrivateData.PSData.Prerelease | Should -Be "preview"
    }

    It "Install a module with a dependency" {
        Install-PSResource -Name "PSGetTestModule" -Prerelease -Repository $TestGalleryName 
        $pkg = Get-Module "PSGetTestModule" -ListAvailable
        $pkg.Name | Should -Be "PSGetTestModule"
        $pkg.Version | Should -Be "2.0.2"
        $pkg.PrivateData.PSData.Prerelease | Should -Be "-alpha1"

        $pkg = Get-Module "PSGetTestDependency1" -ListAvailable
        $pkg.Name | Should -Be "PSGetTestDependency1"
        $pkg.Version | Should -Be "1.0.0"
    }

    It "Install a module with a dependency and skip installing the dependency" {
        Install-PSResource -Name "PSGetTestModule" -Prerelease -Repository $TestGalleryName -SkipDependencyCheck
        $pkg = Get-Module "PSGetTestModule" -ListAvailable
        $pkg.Name | Should -Be "PSGetTestModule"
        $pkg.Version | Should -Be "2.0.2"
        $pkg.PrivateData.PSData.Prerelease | Should -Be "-alpha1"

        $pkg = Get-Module "PSGetTestDependency1" -ListAvailable
        $pkg | Should -BeNullOrEmpty
    }

    It "Install resource via InputObject by piping from Find-PSresource" {
        Find-PSResource -Name "TestModule" -Repository $TestGalleryName | Install-PSResource
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 
        $pkg.Version | Should -Be "1.3.0"
    }

    # Windows only
    It "Install resource under CurrentUser scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName -Scope CurrentUser
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 
        $pkg.Path.Contains("Documents") | Should -Be $true
    }

    # Windows only
    It "Install resource under AllUsers scope - Windows only" -Skip:(!((Get-IsWindows) -and (Test-IsAdmin))) {
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName -Scope AllUsers
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 
        $pkg.Path.Contains("Program Files") | Should -Be $true
    }

    # Windows only
    It "Install resource under no specified scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 
        $pkg.Path.Contains("Documents") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install resource under CurrentUser scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName -Scope CurrentUser
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 
        $pkg.Path.Contains("$env:HOME/.local") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install resource under no specified scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 
        $pkg.Path.Contains("$env:HOME/.local") | Should -Be $true
    }

    It "Should not install resource that is already installed" {
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName -WarningVariable WarningVar -warningaction SilentlyContinue
        $WarningVar | Should -Not -BeNullOrEmpty
    }

    It "Reinstall resource that is already installed with -Reinstall parameter" {
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule"
        $pkg.Version | Should -Be "1.3.0"
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName -Reinstall
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule"
        $pkg.Version | Should -Be "1.3.0"
    }

    It "Restore resource after reinstall fails" {
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule"
        $pkg.Version | Should -Be "1.3.0"

        $resourcePath = Split-Path -Path $pkg.Path -Parent
        $resourceFiles = Get-ChildItem -Path $resourcePath -Recurse

        # Lock resource file to prevent reinstall from succeeding.
        $fs = [System.IO.File]::Open($resourceFiles[0].FullName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read)
        try
        {
            # Reinstall of resource should fail with one of its files locked.
            Install-PSResource -Name "TestModule" -Repository $TestGalleryName -Reinstall -ErrorVariable ev -ErrorAction Silent
            $ev.FullyQualifiedErrorId | Should -BeExactly 'InstallPackageFailed,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource'
        }
        finally
        {
            $fs.Close()
        }

        # Verify that resource module has been restored.
        (Get-ChildItem -Path $resourcePath -Recurse).Count | Should -BeExactly $resourceFiles.Count
    }

    It "Install resource that requires accept license with -AcceptLicense flag" {
        Install-PSResource -Name "testModuleWithlicense" -Repository $TestGalleryName -AcceptLicense
        $pkg = Get-PSResource "testModuleWithlicense"
        $pkg.Name | Should -Be "testModuleWithlicense" 
        $pkg.Version | Should -Be "0.0.3.0"
    }

    It "Install resource should not prompt 'trust repository' if repository is not trusted but -TrustRepository is used" {
        Set-PSResourceRepository PoshTestGallery -Trusted:$false

        Install-PSResource -Name "TestModule" -Repository $TestGalleryName -TrustRepository
        
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 

        Set-PSResourceRepository PoshTestGallery -Trusted
    }

    It "Install resource with cmdlet names from a module already installed (should clobber)" {
        Install-PSResource -Name "myTestModule" -Repository $TestGalleryName  
        $pkg = Get-PSResource "myTestModule"
        $pkg.Name | Should -Be "myTestModule" 
        $pkg.Version | Should -Be "0.0.3.0"

        Install-PSResource -Name "myTestModule2" -Repository $TestGalleryName  
        $pkg = Get-PSResource "myTestModule2"
        $pkg.Name | Should -Be "myTestModule2" 
        $pkg.Version | Should -Be "0.0.1.0"
    }

    It "Install resource from local repository given Repository parameter" {
        $publishModuleName = "TestFindModule"
        $repoName = "psgettestlocal"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $publishModuleName $repoName
        Set-PSResourceRepository "psgettestlocal" -Trusted:$true

        Install-PSResource -Name $publishModuleName -Repository $repoName
        $pkg = Get-Module $publishModuleName -ListAvailable
        $pkg | Should -Not -BeNullOrEmpty
        $pkg.Name | Should -Be $publishModuleName
    }

    It "Install module using -WhatIf, should not install the module" {
        Install-PSResource -Name "TestModule" -WhatIf
    
        $res = Get-Module "TestModule" -ListAvailable
        $res | Should -BeNullOrEmpty
    }

    It "Validates that a module with module-name script files (like Pester) installs under Modules path" {

        Install-PSResource -Name "testModuleWithScript" -Repository $TestGalleryName
    
        $res = Get-Module "testModuleWithScript" -ListAvailable
        $res.Path.Contains("Modules") | Should -Be $true
    }

    It "Install module using -NoClobber, should throw clobber error and not install the module" {
        Install-PSResource -Name "ClobberTestModule1" -Repository $TestGalleryName  
    
        $res = Get-Module "ClobberTestModule1" -ListAvailable
        $res.Name | Should -Be "ClobberTestModule1"

        Install-PSResource -Name "ClobberTestModule2" -Repository $TestGalleryName -NoClobber -ErrorAction SilentlyContinue
        $Error[0].FullyQualifiedErrorId | Should -be "CommandAlreadyExists,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource"
    }
        It "Install PSResourceInfo object piped in" {
        Find-PSResource -Name $testModuleName -Version "1.1.0.0" -Repository $TestGalleryName | Install-PSResource
        $res = Get-PSResource -Name $testModuleName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "1.1.0.0"
    }

    It "Install module using -PassThru" {
        $res = Install-PSResource -Name "TestModule" -Version "1.3.0" -Repository $TestGalleryName -PassThru
        $res.Name | Should -Be "TestModule"
        $res.Version | Should -Be "1.3.0.0"
    }
}

<# Temporarily commented until -Tag is implemented for this Describe block
Describe 'Test Install-PSResource for interactive and root user scenarios' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        $NuGetGalleryName = Get-NuGetGalleryName
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos
    }

    AfterEach {
        Uninstall-PSResource "TestModule", "testModuleWithlicense" -SkipDependencyCheck -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    # Unix only manual test
    # Expected path should be similar to: '/usr/local/share/powershell/Modules'
    It "Install resource under AllUsers scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name "TestModule" -Repository $TestGalleryName -Scope AllUsers
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 
        $pkg.Path.Contains("/usr/") | Should -Be $true
    }

    # This needs to be manually tested due to prompt
    It "Install resource that requires accept license without -AcceptLicense flag" {
        Install-PSResource -Name "testModuleWithlicense" -Repository $TestGalleryName
        $pkg = Get-PSResource "testModuleWithlicense"
        $pkg.Name | Should -Be "testModuleWithlicense" 
        $pkg.Version | Should -Be "0.0.1.0"
    }

    # This needs to be manually tested due to prompt
    It "Install resource should prompt 'trust repository' if repository is not trusted" {
        Set-PSResourceRepository PoshTestGallery -Trusted:$false

        Install-PSResource -Name "TestModule" -Repository $TestGalleryName -confirm:$false
        
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 

        Set-PSResourceRepository PoshTestGallery -Trusted
    }
}
#>
