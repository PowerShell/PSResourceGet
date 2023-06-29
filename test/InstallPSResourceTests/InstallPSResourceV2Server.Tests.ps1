# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"

Describe 'Test Install-PSResource for V2 Server scenarios' -tags 'CI' {

    BeforeAll {
        $PSGalleryName = Get-PSGalleryName
        $PSGalleryUri = Get-PSGalleryLocation
        $NuGetGalleryName = Get-NuGetGalleryName
        $testModuleName = "test_module"
        $testModuleName2 = "TestModule99"
        $testScriptName = "test_script"
        $PackageManagement = "PackageManagement"
        $clobberTestModule = "ClobberTestModule1"
        $clobberTestModule2 = "ClobberTestModule2"
        $RequiredResourceJSONFileName = "TestRequiredResourceFile.json"
        $RequiredResourcePSD1FileName = "TestRequiredResourceFile.psd1"
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos
    }

    AfterEach {
        Uninstall-PSResource "test_module", "test_module2", "test_script", "TestModule99", "testModuleWithlicense", "TestFindModule","ClobberTestModule1", "ClobberTestModule2", "PackageManagement", "TestTestScript" -SkipDependencyCheck -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    $testCases = @{Name="*";                          ErrorId="NameContainsWildcard"},
                 @{Name="Test_Module*";               ErrorId="NameContainsWildcard"},
                 @{Name="Test?Module","Test[Module";  ErrorId="ErrorFilteringNamesForUnsupportedWildcards"}

    It "Should not install resource with wildcard in name" -TestCases $testCases {
        param($Name, $ErrorId)
        Install-PSResource -Name $Name -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "$ErrorId,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"
    }

    It "Install specific module resource by name" {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0.0"
    }

    It "Install specific script resource by name" {
        Install-PSResource -Name $testScriptName -Repository $PSGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testScriptName
        $pkg.Name | Should -Be $testScriptName
        $pkg.Version | Should -Be "3.5.0.0"
    }

    It "Install multiple resources by name" {
        $pkgNames = @($testModuleName, $testModuleName2)
        Install-PSResource -Name $pkgNames -Repository $PSGalleryName -TrustRepository  
        $pkg = Get-InstalledPSResource $pkgNames
        $pkg.Name | Should -Be $pkgNames
    }

    It "Should not install resource given nonexistant name" {
        Install-PSResource -Name "NonExistantModule" -Repository $PSGalleryName -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
        $pkg = Get-InstalledPSResource "NonExistantModule"
        $pkg.Name | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "InstallPackageFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource" 
    }

    # Do some version testing, but Find-PSResource should be doing thorough testing
    It "Should install resource given name and exact version" {
        Install-PSResource -Name $testModuleName -Version "1.0.0" -Repository $PSGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "1.0.0.0"
    }
    
    It "Should install resource given name and exact version with bracket syntax" {
        Install-PSResource -Name $testModuleName -Version "3.*" -Repository $PSGalleryName -TrustRepository  
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "3.0.0.0"
    }

    It "Should install resource given name and exact version with bracket syntax" {
        Install-PSResource -Name $testModuleName -Version "[1.0.0]" -Repository $PSGalleryName -TrustRepository  
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "1.0.0.0"
    }

    It "Should install resource given name and exact range inclusive [1.0.0, 5.0.0]" {
        Install-PSResource -Name $testModuleName -Version "[1.0.0, 5.0.0]" -Repository $PSGalleryName -TrustRepository  
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0.0"
    }

    It "Should install resource given name and exact range exclusive (1.0.0, 5.0.0)" {
        Install-PSResource -Name $testModuleName -Version "(1.0.0, 5.0.0)" -Repository $PSGalleryName -TrustRepository  
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "3.0.0.0"
    }

    # TODO: Update this test and others like it that use try/catch blocks instead of Should -Throw
    It "Should not install resource with incorrectly formatted version such as exclusive version (1.0.0.0)" {
        $Version = "(1.0.0.0)"
        try {
            Install-PSResource -Name $testModuleName -Version $Version -Repository $PSGalleryName -TrustRepository -ErrorAction SilentlyContinue
        }
        catch
        {}
        $Error[0].FullyQualifiedErrorId | Should -be "IncorrectVersionFormat,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"

        $res = Get-InstalledPSResource $testModuleName
        $res | Should -BeNullOrEmpty
    }

    It "Install resource when given Name, Version '*', should install the latest version" {
        Install-PSResource -Name $testModuleName -Version "*" -Repository $PSGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0.0"
    }

    It "Install resource with latest (including prerelease) version given Prerelease parameter" {
        Install-PSResource -Name $testModuleName -Prerelease -Repository $PSGalleryName -TrustRepository 
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.2.5"
        $pkg.Prerelease | Should -Be "alpha001"
    }

    It "Install a module with a dependency" {
        Uninstall-PSResource -Name "TestModuleWithDependency*" -Version "*" -SkipDependencyCheck -ErrorAction SilentlyContinue
        Install-PSResource -Name "TestModuleWithDependencyC" -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository 

        $pkg = Get-InstalledPSResource "TestModuleWithDependencyC"
        $pkg.Name | Should -Be "TestModuleWithDependencyC"
        $pkg.Version | Should -Be "1.0"

        $pkg = Get-InstalledPSResource "TestModuleWithDependencyB"
        $pkg.Name | Should -Be "TestModuleWithDependencyB"
        $pkg.Version | Should -Be "3.0"

        $pkg = Get-InstalledPSResource "TestModuleWithDependencyD"
        $pkg.Name | Should -Be "TestModuleWithDependencyD"
        $pkg.Version | Should -Be "1.0"
    }

    It "Install a module with a dependency and skip installing the dependency" {
        Uninstall-PSResource -Name "TestModuleWithDependency*" -Version "*" -SkipDependencyCheck
        Install-PSResource -Name "TestModuleWithDependencyC" -Version "1.0.0.0" -SkipDependencyCheck -Repository $PSGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource "TestModuleWithDependencyC"
        $pkg.Name | Should -Be "TestModuleWithDependencyC"
        $pkg.Version | Should -Be "1.0"

        $pkg = Get-InstalledPSResource "TestModuleWithDependencyB", "TestModuleWithDependencyD"
        $pkg | Should -BeNullOrEmpty
    }

    It "Install resource via InputObject by piping from Find-PSresource" {
        Find-PSResource -Name $testModuleName -Repository $PSGalleryName | Install-PSResource -TrustRepository 
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName 
        $pkg.Version | Should -Be "5.0.0.0"
    }

    It "Install resource under specified in PSModulePath" {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName 
        ($env:PSModulePath).Contains($pkg.InstalledLocation)
    }

    It "Install resource with companyname, copyright and repository source location and validate" {
        Install-PSResource -Name $testModuleName -Version "5.2.5-alpha001" -Repository PSGallery -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Version | Should -Be "5.2.5"
        $pkg.Prerelease | Should -Be "alpha001"

        $pkg.CompanyName | Should -Be "Anam"
        $pkg.Copyright | Should -Be "(c) Anam Navied. All rights reserved."
        $pkg.RepositorySourceLocation | Should -Be $PSGalleryUri
    }


    It "Install script with companyname, copyright, and repository source location and validate" {
        Install-PSResource -Name "Install-VSCode" -Version "1.4.2" -Repository $PSGalleryName -TrustRepository

        $res = Get-InstalledPSResource "Install-VSCode" -Version "1.4.2"
        $res.Name | Should -Be "Install-VSCode"
        $res.Version | Should -Be "1.4.2"
        $res.CompanyName | Should -Be "Microsoft Corporation"
        $res.Copyright | Should -Be "(c) Microsoft Corporation"
        $res.RepositorySourceLocation | Should -Be $PSGalleryUri
    }

    # Windows only
    It "Install resource under CurrentUser scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -Scope CurrentUser
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("Documents") | Should -Be $true
    }

    # Windows only
    It "Install resource under AllUsers scope - Windows only" -Skip:(!((Get-IsWindows) -and (Test-IsAdmin))) {
        Install-PSResource -Name "testmodule99" -Repository $PSGalleryName -TrustRepository -Scope AllUsers -Verbose
        $pkg = Get-Module "testmodule99" -ListAvailable
        $pkg.Name | Should -Be "testmodule99"
        $pkg.Path.ToString().Contains("Program Files")
    }

    # Windows only
    It "Install resource under no specified scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository 
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("Documents") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install resource under CurrentUser scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -Scope CurrentUser
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("$env:HOME/.local") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install resource under no specified scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("$env:HOME/.local") | Should -Be $true
    }

    It "Should not install resource that is already installed" {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -WarningVariable WarningVar -warningaction SilentlyContinue
        $WarningVar | Should -Not -BeNullOrEmpty
    }

    It "Reinstall resource that is already installed with -Reinstall parameter" {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0.0"
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -Reinstall -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0.0"
    }

    # It "Restore resource after reinstall fails" {
    #     Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository
    #     $pkg = Get-InstalledPSResource $testModuleName
    #     $pkg.Name | Should -Contain $testModuleName
    #     $pkg.Version | Should -Contain "5.0.0.0"

    #     $resourcePath = Split-Path -Path $pkg.InstalledLocation -Parent
    #     $resourceFiles = Get-ChildItem -Path $resourcePath -Recurse

    #     # Lock resource file to prevent reinstall from succeeding.
    #     $fs = [System.IO.File]::Open($resourceFiles[0].FullName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read)
    #     try
    #     {
    #         # Reinstall of resource should fail with one of its files locked.
    #         Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -Reinstall -ErrorVariable ev -ErrorAction Silent
    #         $ev.FullyQualifiedErrorId | Should -BeExactly 'InstallPackageFailed,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource'
    #     }
    #     finally
    #     {
    #         $fs.Close()
    #     }

    #     # Verify that resource module has been restored.
    #     (Get-ChildItem -Path $resourcePath -Recurse).Count | Should -BeExactly $resourceFiles.Count
    # }

    # It "Install resource that requires accept license with -AcceptLicense flag" {
    #     Install-PSResource -Name "testModuleWithlicense" -Repository $TestGalleryName -AcceptLicense
    #     $pkg = Get-InstalledPSResource "testModuleWithlicense"
    #     $pkg.Name | Should -Be "testModuleWithlicense" 
    #     $pkg.Version | Should -Be "0.0.3.0"
    # }

    It "Install resource with cmdlet names from a module already installed with -NoClobber (should not clobber)" {
        Install-PSResource -Name $clobberTestModule -Repository $PSGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $clobberTestModule
        $pkg.Name | Should -Be $clobberTestModule
        $pkg.Version | Should -Be "0.0.1"

        Install-PSResource -Name $clobberTestModule2 -Repository $PSGalleryName -TrustRepository -NoClobber -ErrorVariable ev -ErrorAction SilentlyContinue
        $pkg = Get-InstalledPSResource $clobberTestModule2
        $pkg | Should -BeNullOrEmpty
        $ev.Count | Should -Be 1
        $ev[0] | Should -Be "ClobberTestModule2 package could not be installed with error: The following commands are already available on this system: 'Test-Command2, Test-Command2'. This module 'ClobberTestModule2' may override the existing commands. If you still want to install this module 'ClobberTestModule2', remove the -NoClobber parameter."
    }

    It "Install resource with cmdlet names from a module already installed (should clobber)" {
        Install-PSResource -Name $clobberTestModule -Repository $PSGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $clobberTestModule
        $pkg.Name | Should -Be $clobberTestModule
        $pkg.Version | Should -Be "0.0.1"

        Install-PSResource -Name $clobberTestModule2 -Repository $PSGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $clobberTestModule2
        $pkg.Name | Should -Be $clobberTestModule2
        $pkg.Version | Should -Be "0.0.1"
    }

    It "Install resource with -NoClobber (should install)" {
        Install-PSResource -Name $clobberTestModule -Repository $PSGalleryName -TrustRepository -NoClobber
        $pkg = Get-InstalledPSResource $clobberTestModule
        $pkg.Name | Should -Be $clobberTestModule
        $pkg.Version | Should -Be "0.0.1"
    }

    It "Install module using -WhatIf, should not install the module" {
        Install-PSResource -Name $testModuleName -WhatIf

        $res = Get-InstalledPSResource $testModuleName
        $res | Should -BeNullOrEmpty
    }

    It "Validates that a module with module-name script files (like Pester) installs under Modules path" {

        Install-PSResource -Name "testModuleWithScript" -Repository $PSGalleryName -TrustRepository

        $res = Get-InstalledPSResource "testModuleWithScript"
        $res.InstalledLocation.ToString().Contains("Modules") | Should -Be $true
    }

    # It "Install module using -NoClobber, should throw clobber error and not install the module" {
    #     Install-PSResource -Name "ClobberTestModule1" -Repository $PSGalleryName -TrustRepository

    #     $res = Get-InstalledPSResource "ClobberTestModule1"
    #     $res.Name | Should -Be "ClobberTestModule1"

    #     Install-PSResource -Name "ClobberTestModule2" -Repository $PSGalleryName -TrustRepository -NoClobber -ErrorAction SilentlyContinue
    #     $Error[0].FullyQualifiedErrorId | Should -be "CommandAlreadyExists,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"
    # }

    It "Install PSResourceInfo object piped in" {
        Find-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName | Install-PSResource -TrustRepository
        $res = Get-InstalledPSResource -Name $testModuleName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "1.0.0.0"
    }

    It "Install module using -PassThru" {
        $res = Install-PSResource -Name $testModuleName -Repository $PSGalleryName -PassThru -TrustRepository
        $res.Name | Should -Contain $testModuleName
    }

    It "Install modules using -RequiredResource with hashtable" {
        $rrHash = @{
            test_module = @{
               version = "[1.0.0,5.0.0)"
               repository = $PSGalleryName
            }

             test_module2 = @{
               version = "[1.0.0,3.0.0)"
               repository = $PSGalleryName
               prerelease = "true"
            }

             TestModule99 = @{
                repository = $PSGalleryName
            }
          }

          Install-PSResource -RequiredResource $rrHash -TrustRepository

          $res1 = Get-InstalledPSResource $testModuleName
          $res1.Name | Should -Be $testModuleName
          $res1.Version | Should -Be "3.0.0.0"

          $res2 = Get-InstalledPSResource "test_module2" -Version "2.5.0-beta"
          $res2.Name | Should -Be "test_module2"
          $res2.Version | Should -Be "2.5.0"
          $res2.Prerelease | Should -Be "beta"

          $res3 = Get-InstalledPSResource $testModuleName2
          $res3.Name | Should -Be $testModuleName2
          $res3.Version | Should -Be "0.0.93"
    }

    It "Install modules using -RequiredResource with JSON string" {
        $rrJSON = "{
           'test_module': {
             'version': '[1.0.0,5.0.0)',
             'repository': 'PSGallery'
           },
           'test_module2': {
             'version': '[1.0.0,3.0.0)',
             'repository': 'PSGallery',
             'prerelease': 'true'
           },
           'TestModule99': {
             'repository': 'PSGallery'
           }
         }"

          Install-PSResource -RequiredResource $rrJSON -TrustRepository

          $res1 = Get-InstalledPSResource $testModuleName
          $res1.Name | Should -Be $testModuleName
          $res1.Version | Should -Be "3.0.0.0"

          $res2 = Get-InstalledPSResource "test_module2" -Version "2.5.0-beta"
          $res2.Name | Should -Be "test_module2"
          $res2.Version | Should -Be "2.5.0"
          $res2.Prerelease | Should -Be "beta"

          $res3 = Get-InstalledPSResource $testModuleName2
          $res3.Name | Should -Be $testModuleName2
          $res3.Version | Should -Be "0.0.93"
    }

    It "Install modules using -RequiredResourceFile with PSD1 file" {
        $rrFilePSD1 = "$psscriptroot/../$RequiredResourcePSD1FileName"

        Install-PSResource -RequiredResourceFile $rrFilePSD1 -TrustRepository

        $res1 = Get-InstalledPSResource $testModuleName
        $res1.Name | Should -Be $testModuleName
        $res1.Version | Should -Be "3.0.0.0"

        $res2 = Get-InstalledPSResource "test_module2" -Version "2.5.0-beta"
        $res2.Name | Should -Be "test_module2"
        $res2.Version | Should -Be "2.5.0"
        $res2.Prerelease | Should -Be "beta"

        $res3 = Get-InstalledPSResource $testModuleName2
        $res3.Name | Should -Be $testModuleName2
        $res3.Version | Should -Be "0.0.93"
    }

    It "Install modules using -RequiredResourceFile with JSON file" {
        $rrFileJSON = "$psscriptroot/../$RequiredResourceJSONFileName"

        Install-PSResource -RequiredResourceFile $rrFileJSON -TrustRepository

        $res1 = Get-InstalledPSResource $testModuleName
        $res1.Name | Should -Be $testModuleName
        $res1.Version | Should -Be "3.0.0.0"

        $res2 = Get-InstalledPSResource "test_module2" -Version "2.5.0-beta"
        $res2.Name | Should -Be "test_module2"
        $res2.Version | Should -Be "2.5.0"
        $res2.Prerelease | Should -Be "beta"

        $res3 = Get-InstalledPSResource $testModuleName2
        $res3.Name | Should -Be $testModuleName2
        $res3.Version | Should -Be "0.0.93"
    }

    # Install module 1.4.3 (is authenticode signed and has catalog file)
    # Should install successfully 
    It "Install modules with catalog file using publisher validation" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $PackageManagement -Version "1.4.3" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository

        $res1 = Get-InstalledPSResource $PackageManagement -Version "1.4.3"
        $res1.Name | Should -Be $PackageManagement
        $res1.Version | Should -Be "1.4.3"
    }

    #TODO: update this test to use something other than PackageManagement.
    # Install module 1.4.7 (is authenticode signed and has no catalog file)
    # Should not install successfully
    It "Install module with no catalog file" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $PackageManagement -Version "1.4.7" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository
        $res1 = Get-InstalledPSResource $PackageManagement -Version "1.4.7"
        $res1.Name | Should -Be $PackageManagement
        $res1.Version | Should -Be "1.4.7"
    }

    # Install module that is not authenticode signed
    # Should FAIL to install the  module
    It "Install module that is not authenticode signed" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $testModuleName -Version "5.0.0" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "GetAuthenticodeSignatureError,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource" 
    }

    # # Install 1.4.4.1 (with incorrect catalog file)
    # # Should FAIL to install the  module
    # It "Install module with incorrect catalog file" -Skip:(!(Get-IsWindows)) {
    #     { Install-PSResource -Name $PackageManagement -Version "1.4.4.1" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository } | Should -Throw -ErrorId "TestFileCatalogError,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"
    # }

    # Install script that is signed
    # Should install successfully
    It "Install script that is authenticode signed" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name "Install-VSCode" -Version "1.4.2" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository

        $res1 = Get-InstalledPSResource "Install-VSCode" -Version "1.4.2"
        $res1.Name | Should -Be "Install-VSCode"
        $res1.Version | Should -Be "1.4.2"
    }

    # Install script that is not signed
    # Should throw and fail to install
    It "Install script that is not signed" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name "TestTestScript" -Version "1.3.1.1" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
        write-host $err.Count
        $err.Count | Should -HaveCount 1
        write-Host $err
        write-Host $err[0]
        $err[0].FullyQualifiedErrorId | Should -BeExactly "GetAuthenticodeSignatureError,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource" 
    }
}

Describe 'Test Install-PSResource for V2 Server scenarios' -tags 'ManualValidationOnly' {

    BeforeAll {
        $PSGalleryName = Get-PSGalleryName
        $PSGalleryUri = Get-PSGalleryLocation
        $NuGetGalleryName = Get-NuGetGalleryName
        $testModuleName = "TestModule"
        $testModuleName2 = "testModuleWithlicense"
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos
    }

    AfterEach {
        Uninstall-PSResource $testModuleName, $testModuleName2 -ErrorAction SilentlyContinue
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
        Install-PSResource -Name $testModuleName2 -Repository $TestGalleryName
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
