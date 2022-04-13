# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Install-PSResource for Module' {

    BeforeAll {
        $PSGalleryName = Get-PSGalleryName
        $NuGetGalleryName = Get-NuGetGalleryName
        $testModuleName = "test_module"
        $testModuleName2 = "TestModule99"
        $testScriptName = "test_script"
        $RequiredResourceJSONFileName = "TestRequiredResourceFile.json"
        $RequiredResourcePSD1FileName = "TestRequiredResourceFile.psd1"
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos
    }

    AfterEach {
        Uninstall-PSResource "test_module", "TestModule", "TestModule99", "myTestModule", "myTestModule2", "testModulePrerelease", 
            "testModuleWithlicense","PSGetTestModule", "PSGetTestDependency1", "TestFindModule","ClobberTestModule1",
            "ClobberTestModule2" -SkipDependencyCheck -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    $testCases = @{Name="*";                          ErrorId="NameContainsWildcard"},
                 @{Name="Test_Module*";               ErrorId="NameContainsWildcard"},
                 @{Name="Test?Module","Test[Module";  ErrorId="ErrorFilteringNamesForUnsupportedWildcards"}

    It "Should not install resource with wildcard in name" -TestCases $testCases {
        param($Name, $ErrorId)
        Install-PSResource -Name $Name -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "$ErrorId,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource"
    }

    It "Install specific module resource by name" {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository
        $pkg = Get-PSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0.0"
    }

    It "Install specific script resource by name" {
        Install-PSResource -Name $testScriptName -Repository $PSGalleryName -TrustRepository
        $pkg = Get-PSResource $testScriptName
        $pkg.Name | Should -Be $testScriptName
        $pkg.Version | Should -Be "3.5.0.0"
    }

    It "Install multiple resources by name" {
        $pkgNames = @($testModuleName,$testModuleName2)
        Install-PSResource -Name $pkgNames -Repository $PSGalleryName -TrustRepository  
        $pkg = Get-PSResource $pkgNames
        $pkg.Name | Should -Be $pkgNames
    }

    It "Should not install resource given nonexistant name" {
        Install-PSResource -Name "NonExistantModule" -Repository $PSGalleryName -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
        $pkg = Get-PSResource "NonExistantModule"
        $pkg.Name | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ResourceNotFoundError,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource" 
    }

    # Do some version testing, but Find-PSResource should be doing thorough testing
    It "Should install resource given name and exact version" {
        Install-PSResource -Name $testModuleName -Version "1.0.0" -Repository $PSGalleryName -TrustRepository
        $pkg = Get-PSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "1.0.0.0"
    }

    It "Should install resource given name and exact version with bracket syntax" {
        Install-PSResource -Name $testModuleName -Version "[1.0.0]" -Repository $PSGalleryName -TrustRepository  
        $pkg = Get-PSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "1.0.0.0"
    }

    It "Should install resource given name and exact range inclusive [1.0.0, 5.0.0]" {
        Install-PSResource -Name $testModuleName -Version "[1.0.0, 5.0.0]" -Repository $PSGalleryName -TrustRepository  
        $pkg = Get-PSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0.0"
    }

    It "Should install resource given name and exact range exclusive (1.0.0, 5.0.0)" {
        Install-PSResource -Name $testModuleName -Version "(1.0.0, 5.0.0)" -Repository $PSGalleryName -TrustRepository  
        $pkg = Get-PSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "3.0.0.0"
    }

    It "Should not install resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(1.0.0.0)';       Description="exclusive version (1.0.0.0)"},
        @{Version='[1-0-0-0]';       Description="version formatted with invalid delimiter [1-0-0-0]"}
    ) {
        param($Version, $Description)

        Install-PSResource -Name $testModuleName -Version $Version -Repository $PSGalleryName -TrustRepository -ErrorAction SilentlyContinue
        $Error[0].FullyQualifiedErrorId | Should -be "ResourceNotFoundError,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource"

        $res = Get-PSResource $testModuleName
        $res | Should -BeNullOrEmpty
    }

    It "Install resource when given Name, Version '*', should install the latest version" {
        Install-PSResource -Name $testModuleName -Version "*" -Repository $PSGalleryName -TrustRepository
        $pkg = Get-PSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0.0"
    }

    It "Install resource with latest (including prerelease) version given Prerelease parameter" {
        Install-PSResource -Name $testModuleName -Prerelease -Repository $PSGalleryName -TrustRepository 
        $pkg = Get-PSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.2.5"
        $pkg.Prerelease | Should -Be "alpha001"
    }

    It "Install a module with a dependency" {
        Uninstall-PSResource -Name "TestModuleWithDependency*" -Version "*" -SkipDependencyCheck
        Install-PSResource -Name "TestModuleWithDependencyC" -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository 

        $pkg = Get-PSResource "TestModuleWithDependencyC"
        $pkg.Name | Should -Be "TestModuleWithDependencyC"
        $pkg.Version | Should -Be "1.0.0.0"

        $pkg = Get-PSResource "TestModuleWithDependencyB"
        $pkg.Name | Should -Be "TestModuleWithDependencyB"
        $pkg.Version | Should -Be "3.0.0.0"

        $pkg = Get-PSResource "TestModuleWithDependencyD"
        $pkg.Name | Should -Be "TestModuleWithDependencyD"
        $pkg.Version | Should -Be "1.0.0.0"
    }

    It "Install a module with a dependency and skip installing the dependency" {
        Uninstall-PSResource -Name "TestModuleWithDependency*" -Version "*" -SkipDependencyCheck
        Install-PSResource -Name "TestModuleWithDependencyC" -Version "1.0.0.0" -SkipDependencyCheck -Repository $PSGalleryName -TrustRepository
        $pkg = Get-PSResource "TestModuleWithDependencyC"
        $pkg.Name | Should -Be "TestModuleWithDependencyC"
        $pkg.Version | Should -Be "1.0.0.0"

        $pkg = Get-PSResource "TestModuleWithDependencyB", "TestModuleWithDependencyD"
        $pkg | Should -BeNullOrEmpty
    }

    It "Install resource via InputObject by piping from Find-PSresource" {
        Find-PSResource -Name $testModuleName -Repository $PSGalleryName | Install-PSResource -TrustRepository 
        $pkg = Get-PSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName 
        $pkg.Version | Should -Be "5.0.0.0"
    }

    # Windows only
    It "Install resource under CurrentUser scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -Scope CurrentUser
        $pkg = Get-Module $testModuleName -ListAvailable
        $pkg.Name | Should -Be $testModuleName
        $pkg.Path.Contains("Documents") | Should -Be $true
    }

    # Windows only
    It "Install resource under AllUsers scope - Windows only" -Skip:(!((Get-IsWindows) -and (Test-IsAdmin))) {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -Scope AllUsers 
        $pkg = Get-PSResource $testModuleName -ListAvailable
        $pkg.Name | Should -Be $testModuleName
        $pkg.Path.Contains("Program Files") | Should -Be $true
    }

    # Windows only
    It "Install resource under no specified scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository 
        $pkg = Get-PSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("Documents") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install resource under CurrentUser scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -Scope CurrentUser
        $pkg = Get-PSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("$env:HOME/.local") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install resource under no specified scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository
        $pkg = Get-PSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("$env:HOME/.local") | Should -Be $true
    }

    It "Should not install resource that is already installed" {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository
        $pkg = Get-PSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -WarningVariable WarningVar -warningaction SilentlyContinue
        $WarningVar | Should -Not -BeNullOrEmpty
    }

    It "Reinstall resource that is already installed with -Reinstall parameter" {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository
        $pkg = Get-PSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0.0"
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -Reinstall -TrustRepository
        $pkg = Get-PSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0.0"
    }

    It "Restore resource after reinstall fails" {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository
        $pkg = Get-Module $testModuleName -ListAvailable
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "5.0.0.0"

        $resourcePath = Split-Path -Path $pkg.Path -Parent
        $resourceFiles = Get-ChildItem -Path $resourcePath -Recurse

        # Lock resource file to prevent reinstall from succeeding.
        $fs = [System.IO.File]::Open($resourceFiles[0].FullName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read)
        try
        {
            # Reinstall of resource should fail with one of its files locked.
            Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -Reinstall -ErrorVariable ev -ErrorAction Silent
            $ev.FullyQualifiedErrorId | Should -BeExactly 'InstallPackageFailed,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource'
        }
        finally
        {
            $fs.Close()
        }

        # Verify that resource module has been restored.
        (Get-ChildItem -Path $resourcePath -Recurse).Count | Should -BeExactly $resourceFiles.Count
    }

    # It "Install resource that requires accept license with -AcceptLicense flag" {
    #     Install-PSResource -Name "testModuleWithlicense" -Repository $TestGalleryName -AcceptLicense
    #     $pkg = Get-PSResource "testModuleWithlicense"
    #     $pkg.Name | Should -Be "testModuleWithlicense" 
    #     $pkg.Version | Should -Be "0.0.3.0"
    # }


    # It "Install resource with cmdlet names from a module already installed (should clobber)" {
    #     Install-PSResource -Name "myTestModule" -Repository $TestGalleryName  
    #     $pkg = Get-PSResource "myTestModule"
    #     $pkg.Name | Should -Be "myTestModule" 
    #     $pkg.Version | Should -Be "0.0.3.0"

    #     Install-PSResource -Name "myTestModule2" -Repository $TestGalleryName  
    #     $pkg = Get-PSResource "myTestModule2"
    #     $pkg.Name | Should -Be "myTestModule2" 
    #     $pkg.Version | Should -Be "0.0.1.0"
    # }

    It "Install resource from local repository given Repository parameter" {
        $publishModuleName = "TestFindModule"
        $repoName = "psgettestlocal"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $publishModuleName $repoName
        Set-PSResourceRepository "psgettestlocal" -Trusted:$true

        Install-PSResource -Name $publishModuleName -Repository $repoName
        $pkg = Get-PSResource $publishModuleName
        $pkg | Should -Not -BeNullOrEmpty
        $pkg.Name | Should -Be $publishModuleName
    }

    It "Install module using -WhatIf, should not install the module" {
        Install-PSResource -Name $testModuleName -WhatIf
    
        $res = Get-PSResource $testModuleName
        $res | Should -BeNullOrEmpty
    }

    # It "Validates that a module with module-name script files (like Pester) installs under Modules path" {

    #     Install-PSResource -Name "testModuleWithScript" -Repository $TestGalleryName
    
    #     $res = Get-Module "testModuleWithScript" -ListAvailable
    #     $res.Path.Contains("Modules") | Should -Be $true
    # }

    It "Install module using -NoClobber, should throw clobber error and not install the module" {
        Install-PSResource -Name "ClobberTestModule1" -Repository $PSGalleryName -TrustRepository
    
        $res = Get-PSResource "ClobberTestModule1"
        $res.Name | Should -Be "ClobberTestModule1"

        Install-PSResource -Name "ClobberTestModule2" -Repository $PSGalleryName -TrustRepository -NoClobber -ErrorAction SilentlyContinue
        $Error[0].FullyQualifiedErrorId | Should -be "CommandAlreadyExists,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource"
    }

    It "Install PSResourceInfo object piped in" {
        Find-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName | Install-PSResource -TrustRepository
        $res = Get-PSResource -Name $testModuleName
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
          
             TestModule99 = @{}
          }

          Install-PSResource -RequiredResource $rrHash -TrustRepository
    
          $res1 = Get-PSResource $testModuleName
          $res1.Name | Should -Be $testModuleName
          $res1.Version | Should -Be "3.0.0.0"

          $res2 = Get-PSResource "test_module2" -Version "2.5.0-beta"
          $res2.Name | Should -Be "test_module2"
          $res2.Version | Should -Be "2.5.0"
          $res2.Prerelease | Should -Be "beta"

          $res3 = Get-PSResource $testModuleName2
          $res3.Name | Should -Be $testModuleName2
          $res3.Version | Should -Be "0.0.93.0"
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
    
          $res1 = Get-PSResource $testModuleName
          $res1.Name | Should -Be $testModuleName
          $res1.Version | Should -Be "3.0.0.0"

          $res2 = Get-PSResource "test_module2" -Version "2.5.0-beta"
          $res2.Name | Should -Be "test_module2"
          $res2.Version | Should -Be "2.5.0"
          $res2.Prerelease | Should -Be "beta"

          $res3 = Get-PSResource $testModuleName2
          $res3.Name | Should -Be $testModuleName2
          $res3.Version | Should -Be "0.0.93.0"
    }

    It "Install modules using -RequiredResourceFile with PSD1 file" {
        $rrFilePSD1 = Join-Path -Path $psscriptroot -ChildPath $RequiredResourcePSD1FileName

        Install-PSResource -RequiredResourceFile $rrFilePSD1 -TrustRepository

        $res1 = Get-PSResource $testModuleName
        $res1.Name | Should -Be $testModuleName
        $res1.Version | Should -Be "3.0.0.0"

        $res2 = Get-PSResource "test_module2" -Version "2.5.0-beta"
        $res2.Name | Should -Be "test_module2"
        $res2.Version | Should -Be "2.5.0"
        $res2.Prerelease | Should -Be "beta"

        $res3 = Get-PSResource $testModuleName2
        $res3.Name | Should -Be $testModuleName2
        $res3.Version | Should -Be "0.0.93.0"
    }

    It "Install modules using -RequiredResourceFile with JSON file" {
        $rrFileJSON = Join-Path -Path $psscriptroot -ChildPath $RequiredResourceJSONFileName

        Install-PSResource -RequiredResourceFile $rrFileJSON -TrustRepository

        $res1 = Get-PSResource $testModuleName
        $res1.Name | Should -Be $testModuleName
        $res1.Version | Should -Be "3.0.0.0"
 
        $res2 = Get-PSResource "test_module2" -Version "2.5.0-beta"
        $res2.Name | Should -Be "test_module2"
        $res2.Version | Should -Be "2.5.0"
        $res2.Prerelease | Should -Be "beta"
 
        $res3 = Get-PSResource $testModuleName2
        $res3.Name | Should -Be $testModuleName2
        $res3.Version | Should -Be "0.0.93.0"
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
