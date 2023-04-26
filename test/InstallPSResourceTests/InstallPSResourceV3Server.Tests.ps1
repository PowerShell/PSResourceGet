# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Write-Verbose -Verbose -Message "PSGetTestUtils path: $modPath"
Import-Module $modPath -Force -Verbose

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"

Describe 'Test Install-PSResource for V3Server scenarios' -tags 'CI' {

    BeforeAll {
        $NuGetGalleryName = Get-NuGetGalleryName
        $NuGetGalleryUri = Get-NuGetGalleryLocation
        $testModuleName = "test_module"
        $testModuleName2 = "test_module2"
        $testScriptName = "test_script"
        $PackageManagement = "PackageManagement"
        $RequiredResourceJSONFileName = "TestRequiredResourceFile.json"
        $RequiredResourcePSD1FileName = "TestRequiredResourceFile.psd1"
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos
    }

    AfterEach {
        Uninstall-PSResource "test_module", "test_module2", "test_script", "TestModule99", "testModuleWithlicense", "TestFindModule", "PackageManagement" -SkipDependencyCheck -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    $testCases = @{Name="*";                          ErrorId="NameContainsWildcard"},
                 @{Name="Test_Module*";               ErrorId="NameContainsWildcard"},
                 @{Name="Test?Module","Test[Module";  ErrorId="ErrorFilteringNamesForUnsupportedWildcards"}

    It "Should not install resource with wildcard in name" -TestCases $testCases {
        param($Name, $ErrorId)
        Install-PSResource -Name $Name -Repository $NuGetGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "$ErrorId,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource"
    }

    It "Install specific module resource by name" {
        Install-PSResource -Name $testModuleName -Repository $NuGetGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    It "Install specific script resource by name" {
        Install-PSResource -Name $testScriptName -Repository $NuGetGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testScriptName
        $pkg.Name | Should -Be $testScriptName
        $pkg.Version | Should -Be "3.5.0"
    }

    It "Install multiple resources by name" {
        $pkgNames = @($testModuleName, $testModuleName2)
        Install-PSResource -Name $pkgNames -Repository $NuGetGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $pkgNames
        $pkg.Name | Should -Be $pkgNames
    }

    It "Should not install resource given nonexistant name" {
        Install-PSResource -Name "NonExistantModule" -Repository $NuGetGalleryName -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
        $pkg = Get-InstalledPSResource "NonExistantModule"
        $pkg.Name | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "InstallPackageFailure,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource"
    }

    # Do some version testing, but Find-PSResource should be doing thorough testing
    It "Should install resource given name and exact version" {
        Install-PSResource -Name $testModuleName -Version "1.0.0" -Repository $NuGetGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "1.0.0"
    }

    It "Should install resource given name and exact version with bracket syntax" {
        Install-PSResource -Name $testModuleName -Version "[1.0.0]" -Repository $NuGetGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "1.0.0"
    }

    It "Should install resource given name and exact range inclusive [1.0.0, 5.0.0]" {
        Install-PSResource -Name $testModuleName -Version "[1.0.0, 5.0.0]" -Repository $NuGetGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    It "Should install resource given name and exact range exclusive (1.0.0, 5.0.0)" {
        Install-PSResource -Name $testModuleName -Version "(1.0.0, 5.0.0)" -Repository $NuGetGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "3.0.0"
    }

    # TODO: Update this test and others like it that use try/catch blocks instead of Should -Throw
    It "Should not install resource with incorrectly formatted version such as exclusive version (1.0.0.0)" {
        $Version = "(1.0.0.0)"
        try {
            Install-PSResource -Name $testModuleName -Version $Version -Repository $NuGetGalleryName -TrustRepository -ErrorAction SilentlyContinue
        }
        catch
        {}
        $Error[0].FullyQualifiedErrorId | Should -be "IncorrectVersionFormat,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource"

        $res = Get-InstalledPSResource $testModuleName
        $res | Should -BeNullOrEmpty
    }

    It "Install resource when given Name, Version '*', should install the latest version" {
        Install-PSResource -Name $testModuleName -Version "*" -Repository $NuGetGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    It "Install resource with latest (including prerelease) version given Prerelease parameter" {
        Install-PSResource -Name $testModuleName -Prerelease -Repository $NuGetGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.2.5"
        $pkg.Prerelease | Should -Be "alpha001"
    }

    It "Install resource via InputObject by piping from Find-PSresource" {
        Find-PSResource -Name $testModuleName -Repository $NuGetGalleryName | Install-PSResource -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    It "Install resource under specified in PSModulePath" {
        Install-PSResource -Name $testModuleName -Repository $NuGetGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        ($env:PSModulePath).Contains($pkg.InstalledLocation)
    }

    It "Install resource with companyname, copyright and repository source location and validate properties" {
        Install-PSResource -Name $testModuleName -Version "5.2.5-alpha001" -Repository $NuGetGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Version | Should -Be "5.2.5"
        $pkg.Prerelease | Should -Be "alpha001"

        $pkg.CompanyName | Should -Be "Anam Navied"
        $pkg.Copyright | Should -Be "(c) Anam Navied. All rights reserved."
        $pkg.RepositorySourceLocation | Should -Be $NuGetGalleryUri
    }

    # Windows only
    It "Install resource under CurrentUser scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $testModuleName -Repository $NuGetGalleryName -TrustRepository -Scope CurrentUser
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("Documents") | Should -Be $true
    }

    # Windows only
    It "Install resource under AllUsers scope - Windows only" -Skip:(!((Get-IsWindows) -and (Test-IsAdmin))) {
        Install-PSResource -Name "testmodule99" -Repository $NuGetGalleryName -TrustRepository -Scope AllUsers -Verbose
        $pkg = Get-Module "testmodule99" -ListAvailable
        $pkg.Name | Should -Be "testmodule99"
        $pkg.Path.ToString().Contains("Program Files")
    }

    # Windows only
    It "Install resource under no specified scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $testModuleName -Repository $NuGetGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("Documents") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install resource under CurrentUser scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name $testModuleName -Repository $NuGetGalleryName -TrustRepository -Scope CurrentUser
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("$env:HOME/.local") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install resource under no specified scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name $testModuleName -Repository $NuGetGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("$env:HOME/.local") | Should -Be $true
    }

    It "Should not install resource that is already installed" {
        Install-PSResource -Name $testModuleName -Repository $NuGetGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        Install-PSResource -Name $testModuleName -Repository $NuGetGalleryName -TrustRepository -WarningVariable WarningVar -warningaction SilentlyContinue
        $WarningVar | Should -Not -BeNullOrEmpty
    }

    It "Reinstall resource that is already installed with -Reinstall parameter" {
        Install-PSResource -Name $testModuleName -Repository $NuGetGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
        Install-PSResource -Name $testModuleName -Repository $NuGetGalleryName -Reinstall -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0"
    }

    # It "Restore resource after reinstall fails" {
    #     Install-PSResource -Name $testModuleName -Repository $NuGetGalleryName -TrustRepository
    #     $pkg = Get-InstalledPSResource $testModuleName
    #     $pkg.Name | Should -Contain $testModuleName
    #     $pkg.Version | Should -Contain "5.0.0"

    #     $resourcePath = Split-Path -Path $pkg.InstalledLocation -Parent
    #     $resourceFiles = Get-ChildItem -Path $resourcePath -Recurse

    #     # Lock resource file to prevent reinstall from succeeding.
    #     $fs = [System.IO.File]::Open($resourceFiles[0].FullName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read)
    #     try
    #     {
    #         # Reinstall of resource should fail with one of its files locked.
    #         Install-PSResource -Name $testModuleName -Repository $NuGetGalleryName -TrustRepository -Reinstall -ErrorVariable ev -ErrorAction Silent
    #         $ev.FullyQualifiedErrorId | Should -BeExactly 'InstallPackageFailed,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource'
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

    It "Install PSResourceInfo object piped in" {
        Find-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $NuGetGalleryName | Install-PSResource -TrustRepository
        $res = Get-InstalledPSResource -Name $testModuleName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "1.0.0"
    }

    It "Install module using -PassThru" {
        $res = Install-PSResource -Name $testModuleName -Repository $NuGetGalleryName -PassThru -TrustRepository
        $res.Name | Should -Contain $testModuleName
    }

    It "Install modules using -RequiredResource with hashtable" {
        $rrHash = @{
            test_module = @{
               version = "[1.0.0,5.0.0)"
               repository = $NuGetGalleryName
            }

             test_module2 = @{
               version = "[1.0.0,5.0.0]"
               repository = $NuGetGalleryName
               prerelease = "true"
            }

             TestModule99 = @{
                repository = $NuGetGalleryName
            }
          }

          Install-PSResource -RequiredResource $rrHash -TrustRepository

          $res1 = Get-InstalledPSResource $testModuleName
          $res1.Name | Should -Be $testModuleName
          $res1.Version | Should -Be "3.0.0"

          $res2 = Get-InstalledPSResource $testModuleName2
          $res2.Name | Should -Be $testModuleName2
          $res2.Version | Should -Be "5.0.0"

          $res3 = Get-InstalledPSResource "TestModule99"
          $res3.Name | Should -Be "TestModule99"
          $res3.Version | Should -Be "0.0.93"
    }

    It "Install modules using -RequiredResource with JSON string" {
        $rrJSON = "{
           'test_module': {
             'version': '[1.0.0,5.0.0)',
             'repository': 'NuGetGallery'
           },
           'test_module2': {
             'version': '[1.0.0,5.0.0]',
             'repository': 'PSGallery',
             'prerelease': 'true'
           },
           'TestModule99': {
             'repository': 'NuGetGallery'
           }
         }"

          Install-PSResource -RequiredResource $rrJSON -TrustRepository

          $res1 = Get-InstalledPSResource $testModuleName
          $res1.Name | Should -Be $testModuleName
          $res1.Version | Should -Be "3.0.0"

          $res2 = Get-InstalledPSResource $testModuleName2
          $res2.Name | Should -Be $testModuleName2
          $res2.Version | Should -Be "5.0.0.0"

          $res3 = Get-InstalledPSResource "testModule99"
          $res3.Name | Should -Be "testModule99"
          $res3.Version | Should -Be "0.0.93"
    }

    It "Install modules using -RequiredResourceFile with PSD1 file" {
        $rrFilePSD1 = "$psscriptroot/../$RequiredResourcePSD1FileName"

        Install-PSResource -RequiredResourceFile $rrFilePSD1 -TrustRepository

        $res1 = Get-InstalledPSResource $testModuleName
        $res1.Name | Should -Be $testModuleName
        $res1.Version | Should -Be "3.0.0.0"

        $res2 = Get-InstalledPSResource $testModuleName2 -Version "2.5.0-beta"
        $res2.Name | Should -Be $testModuleName2
        $res2.Version | Should -Be "2.5.0"
        $res2.Prerelease | Should -Be "beta"

        $res3 = Get-InstalledPSResource "testModule99"
        $res3.Name | Should -Be "testModule99"
        $res3.Version | Should -Be "0.0.93"
    }

    It "Install modules using -RequiredResourceFile with JSON file" {
        $rrFileJSON = "$psscriptroot/../$RequiredResourceJSONFileName"

        Install-PSResource -RequiredResourceFile $rrFileJSON -TrustRepository

        $res1 = Get-InstalledPSResource $testModuleName
        $res1.Name | Should -Be $testModuleName
        $res1.Version | Should -Be "3.0.0.0"

        $res2 = Get-InstalledPSResource $testModuleName2 -Version "2.5.0-beta"
        $res2.Name | Should -Be $testModuleName2
        $res2.Version | Should -Be "2.5.0"
        $res2.Prerelease | Should -Be "beta"

        $res3 = Get-InstalledPSResource "testModule99"
        $res3.Name | Should -Be "testModule99"
        $res3.Version | Should -Be "0.0.93"
    }
}
<# Temporarily commented until -Tag is implemented for this Describe block
Describe 'Test Install-PSResource for interactive and root user scenarios' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $NuGetGalleryName = Get-PSGalleryName
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
        $pkg = Get-InstalledPSResource "testModuleWithlicense"
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
