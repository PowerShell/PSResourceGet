# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"

$tempModuleDir = Get-ChildItem "D:\a\_temp\TempModules" -Recurse
Write-Verbose -Verbose "Get-ChildItem on tempModules path: $tempModuleDir"

$gmo = Get-Module powershellget 
Write-Verbose -Verbose "Get-InstalledModule on PowerShellGet: $gmo"

$getcmd = (Get-Command Find-Module).Module.ModuleBase
Write-Verbose -Verbose "Get-Command on Find-Module: $getcmd"

Describe 'Test CompatPowerShellGet: Install-PSResource' -tags 'CI' {

    BeforeAll {
        $PSGalleryName = Get-PSGalleryName
        $PSGalleryUri = Get-PSGalleryLocation
        $NuGetGalleryName = Get-NuGetGalleryName
        $testModuleName = "test_module"
        $testModuleName2 = "TestModule99"
        $testScriptName = "test_script"
        $RequiredResourceJSONFileName = "TestRequiredResourceFile.json"
        $RequiredResourcePSD1FileName = "TestRequiredResourceFile.psd1"
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos

        Set-PSResourceRepository -Name PSGallery -Trusted
    }

    AfterEach {
        Uninstall-PSResource "test_module", "TestModule99", "TestModuleWithDependencyB", "TestModuleWithDependencyD", "newTestModule", "test_script", `
          "RequiredModule1", "RequiredModule2", "RequiredModule3", "RequiredModule4", "RequiredModule5" -SkipDependencyCheck -ErrorAction SilentlyContinue -Version "*"
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Install-Module testmodule99 should return" {
        Install-Module -Name $testModuleName2 -Repository PSGallery

        $res = Get-PSResource -Name $testModuleName2
        $res.Name | Should be $testModuleName2
    }

    It "Install-Module testmodule99 -PassThru should return output" {
        Install-Module -Name $testModuleName2 -Repository PSGallery -PassThru

        $res = Get-PSResource -Name $testModuleName2
        $res.Name | Should be $testModuleName2
    }

    It "Install-Module should not install with wildcard" {
        Install-Module -Name "testmodule9*" -Repository PSGallery -ErrorAction SilentlyContinue

        $res = Get-PSResource -Name $testModuleName2
        $res.Count -eq 0 | Should -Be $true
    }

    ### Broken
#    It "Install-Module with version params" {
#        Install-Module $testModuleName2 -MinimumVersion 0.0.1 -MaximumVersion 0.0.9 -Repository PSGallery
        
#        $res = Get-PSResource -Name $testModuleName2
#        $res.Name | Should be $testModuleName2
#        $res.Version | Should be "0.0.9"
#    }

    It "Install-Module multiple names" {
        Install-Module "TestModuleWithDependencyB", "testmodule99" -Repository PSGallery

        $res = Get-PSResource "TestModuleWithDependencyB", "testmodule99"
        $res.Count -ge 2 | Should -Be $true
    }

    It "Install-Module multiple names with RequiredVersion" {
        Install-Module "newTestModule", "testmodule99" -RequiredVersion 0.0.1 -Repository PSGallery

        $res = Get-PSResource "newTestModule", "testmodule99"
        $res.Count -eq 2 | Should -Be $true
    }

    It "Install-Multiple names with MinimumVersion" {
        Install-Module "TestModuleWithDependencyB", "TestModuleWithDependencyD" -MinimumVersion 1.0 -Repository PSGallery

        $res = Get-PSResource "TestModuleWithDependencyB", "TestModuleWithDependencyD"
        $res.Count -eq 2 | Should -Be $true
    }

    It "Install-Module with MinimumVersion" {
        Install-Module $testModuleName2 -MinimumVersion 0.0.3 -Repository PSGallery
        
        $res = Get-PSResource $testModuleName2
        $res.Count -eq 1 | Should -Be $true    
    }

    It "Install-Module with RequiredVersion" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.3 -Repository PSGallery

        $res = Get-PSResource $testModuleName2
        $res.Count -eq 1 | Should -Be $true 
    }

    It "Install-Module should fail if RequiredVersion is already installed" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.93 -Repository PSGallery

        Install-Module $testModuleName2 -RequiredVersion 0.0.93 -WarningVariable wv -Repository PSGallery
        $wv[1] | Should -Be "Resource 'testmodule99' with version '0.0.93' is already installed.  If you would like to reinstall, please run the cmdlet again with the -Reinstall parameter"
       }

    It "Install-Module should fail if MinimumVersion is already installed" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.93 -Repository PSGallery

        Install-Module $testModuleName2 -MinimumVersion 2.0 -WarningVariable wv -Repository PSGallery
        $wv[1] | Should -Be "Resource 'testmodule99' with version '0.0.93' is already installed.  If you would like to reinstall, please run the cmdlet again with the -Reinstall parameter"
    }

    It "Install-Module with -Force" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.91 -Repository PSGallery
        Install-Module $testModuleName2 -RequiredVersion 0.0.93 -Force -Repository PSGallery -WarningVariable wv
        $wv.Count | Should -Be 1
    }

    It "Install-Module same version with -Force" {
        Install-Module $testModuleName2 -RequiredVersion 0.0.91 -Repository PSGallery
        Install-Module $testModuleName2 -RequiredVersion 0.0.93 -Force -Repository PSGallery -WarningVariable wv
        $wv.Count | Should -Be 1
    }

    It "Install-Module with nonexistent module" {
        Install-Module NonExistentModule -Repository PSGallery -ErrorVariable ev -ErrorAction SilentlyContinue

        $ev.Count | Should -Be 1
    }
<#
    ### broken
    It "Install-Module with PipelineInput" {
        Find-Module $testModuleName2 -Repository PSGallery | Install-Module -Repository PSGallery
        $res = Get-PSResource $testModuleName2
        $res.Count -eq 1 | Should -Be $true   
    }

    ### BROKEN
    It "Install-Module multiple modules with PipelineInput" {
        Find-Module $testModuleName2, "newTestModule" -Repository PSGallery | Install-Module -Repository PSGallery
        $res = Get-PSResource $testModuleName2 , "newTestModule"
        $res.Count -eq 2 | Should -Be $true   
    }
#>
    ### BROKEN
#    It "Install-Module multiple module using InputObjectParam" {
#        $items = Find-Module $testModuleName2 , "newTestModule"
#        Install-Module -InputObject $items
#        $res = Get-PSResource $testModuleName2 , "newTestModule"
#        $res.Count -eq 2 | Should -Be $true   
#    }

    It "InstallToCurrentUserScope" {
        Install-Module $testModuleName2 -Scope CurrentUser -Repository PSGallery

        $mod = Get-Module $testModuleName2 -ListAvailable
        $mod.ModuleBase.StartsWith($script:MyDocumentsModulesPath, [System.StringComparison]::OrdinalIgnoreCase)
    }

    ### broken
#    It "Install-Module using Find-DscResource output" {
#        $moduleName = "SystemLocaleDsc"
#        Find-DscResource -Name "SystemLocale" -Repository PSGallery | Install-Module
#        $res = Get-Module $moduleName -ListAvailable
#        $res.Name | Should -Be $moduleName
#    }

    ### broken
#    It "Install-Module using Find-Command Output" {
#        $cmd = "Get-WUJob"
#        $module = "PSWindowsUpdate"
#        Find-Command -Name $cmd | Install-Module

#        $res = Get-Module $module -ListAvailable
#        $res.Name | Should -Be $module
#    }

    ### Broken
#    It "Install-Module with Dependencies" {
#        $parentModule = "TestModuleWithDependencyC"
#        $childModule1 = "TestModuleWithDependencyB"
#        $childModule2 = "TestModuleWithDependencyD"
#        $childModule3 = "TestModuleWithDependencyF"
#        Install-Module $parentModule

#        $res = Get-PSResource $parentModule, $childModule1, $childModule2, $childModule3
#        $res.Count -ge 4 | Should -Be $true
#    }


    $testCases = @{Name="*";                          ErrorId="NameContainsWildcard"},
                 @{Name="Test_Module*";               ErrorId="NameContainsWildcard"},
                 @{Name="Test?Module","Test[Module";  ErrorId="ErrorFilteringNamesForUnsupportedWildcards"}

    It "Should not install resource with wildcard in name" -TestCases $testCases {
        param($Name, $ErrorId)
        Install-Module -Name $Name -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "$ErrorId,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource"
    }

    It "Install specific module resource by name" {
        Install-Module -Name $testModuleName2 -Repository $PSGalleryName
        $pkg = Get-PSResource $testModuleName2
        $pkg.Name | Should -Be $testModuleName2
        $pkg.Version | Should -Be "0.0.93"
    }

    It "Install specific script resource by name" {
        Install-Script -Name $testScriptName -Repository $PSGalleryName
        $pkg = Get-PSResource $testScriptName
        $pkg.Name | Should -Be $testScriptName
        $pkg.Version | Should -Be "3.5.0.0"
    }

    <#
### Something strange going on here
    It "Install multiple resources by name" {
        $pkgNames = @($testModuleName, $testModuleName2)
        Install-Module -Name $pkgNames -Repository $PSGalleryName  
        $pkg = Get-PSResource $pkgNames
        $pkg.Name | Should -Be $pkgNames
    }
#>
    It "Should not install module given nonexistant name" {
        Install-Module -Name "NonExistantModule" -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $pkg = Get-PSResource "NonExistantModule"
        $pkg.Name | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "InstallPackageFailure,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource" 
    }

    It "Should install module given name and exact version" {
        Install-Module -Name $testModuleName2 -RequiredVersion "0.0.2" -Repository $PSGalleryName
        $pkg = Get-PSResource $testModuleName2
        $pkg.Name | Should -Be $testModuleName2
        $pkg.Version | Should -Be "0.0.2"
    }

    It "Install module with latest (including prerelease) version given Prerelease parameter" {
        Install-Module -Name $testModuleName2 -AllowPrerelease -Repository $PSGalleryName 
        $pkg = Get-PSResource $testModuleName2
        $pkg.Name | Should -Be $testModuleName2
        $pkg.Version | Should -Be "1.0.0"
        $pkg.Prerelease | Should -Be "beta2"
    }

    It "Install module via InputObject by piping from Find-PSresource" {
        Find-PSResource -Name $testModuleName2 -Repository $PSGalleryName | Install-Module 
        $pkg = Get-PSResource $testModuleName2
        $pkg.Name | Should -Be $testModuleName2 
        $pkg.Version | Should -Be "0.0.93"
    }

    It "Install module under specified in PSModulePath" {
        Install-Module -Name $testModuleName2 -Repository $PSGalleryName
        $pkg = Get-PSResource $testModuleName2
        $pkg.Name | Should -Be $testModuleName2 
        ($env:PSModulePath).Contains($pkg.InstalledLocation)
    }

    # Windows only
    It "Install module under CurrentUser scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-Module -Name $testModuleName2 -Repository $PSGalleryName -Scope CurrentUser
        $pkg = Get-PSResource $testModuleName2
        $pkg.Name | Should -Be $testModuleName2
        $pkg.InstalledLocation.ToString().Contains("Documents") | Should -Be $true
    }

    # Windows only
    It "Install module under AllUsers scope - Windows only" -Skip:(!((Get-IsWindows) -and (Test-IsAdmin))) {
        Install-Module -Name "testmodule99" -Repository $PSGalleryName -Scope AllUsers
        $pkg = Get-Module "testmodule99" -ListAvailable
        $pkg.Name | Should -Be "testmodule99"
        $pkg.Path.ToString().Contains("Program Files")
    }

    # Windows only
    It "Install module under no specified scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-Module -Name $testModuleName2 -Repository $PSGalleryName  
        $pkg = Get-PSResource $testModuleName2
        $pkg.Name | Should -Be $testModuleName2
        $pkg.InstalledLocation.ToString().Contains("Documents") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install module under CurrentUser scope - Unix only" -Skip:(Get-IsWindows) {
        Install-Module -Name $testModuleName2 -Repository $PSGalleryName -Scope CurrentUser
        $pkg = Get-PSResource $testModuleName2
        $pkg.Name | Should -Be $testModuleName2
        $pkg.InstalledLocation.ToString().Contains("$env:HOME/.local") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install module under no specified scope - Unix only" -Skip:(Get-IsWindows) {
        Install-Module -Name $testModuleName2 -Repository $PSGalleryName
        $pkg = Get-PSResource $testModuleName2
        $pkg.Name | Should -Be $testModuleName2
        $pkg.InstalledLocation.ToString().Contains("$env:HOME/.local") | Should -Be $true
    }

    It "Should not install module that is already installed" {
        Install-Module -Name $testModuleName2 -Repository $PSGalleryName
        $pkg = Get-PSResource $testModuleName2
        $pkg.Name | Should -Be $testModuleName2
        Install-Module -Name $testModuleName2 -Repository $PSGalleryName -WarningVariable WarningVar -warningaction SilentlyContinue
        $WarningVar | Should -Not -BeNullOrEmpty
    }

<#
    It "Install resource that requires accept license with -AcceptLicense flag" {
        Install-PSResource -Name "testModuleWithlicense" -Repository $PSGalleryName -AcceptLicense
        $pkg = Get-PSResource "testModuleWithlicense"
        $pkg.Name | Should -Be "testModuleWithlicense" 
        $pkg.Version | Should -Be "0.0.3.0"
    }
#>

    It "Install resource with cmdlet names from a module already installed (should clobber)" {
        Install-Module -Name "CLobberTestModule1" -Repository $PSGalleryName
        $pkg = Get-PSResource "ClobberTestModule1"
        $pkg.Name | Should -Be "ClobberTestModule1" 
        $pkg.Version | Should -Be "0.0.1"

        Install-Module -Name "ClobberTestModule2" -Repository $PSGalleryName -AllowClobber
        $pkg = Get-PSResource "ClobberTestModule2"
        $pkg.Name | Should -Be "ClobberTestModule2" 
        $pkg.Version | Should -Be "0.0.1"
    }

    It "Install module using -WhatIf, should not install the module" {
        Install-Module -Name $testModuleName2 -WhatIf

        $res = Get-PSResource $testModuleName2
        $res | Should -BeNullOrEmpty
    }

    It "Validates that a module with module-name script files (like Pester) installs under Modules path" {
        Install-Module -Name "testModuleWithScript" -Repository $PSGalleryName

        $res = Get-PSResource "testModuleWithScript"
        $res.InstalledLocation.ToString().Contains("Modules") | Should -Be $true
    }
<#
    It "Install module using -NoClobber, should throw clobber error and not install the module" {
        Install-Module -Name "ClobberTestModule1" -Repository $PSGalleryName -TrustRepository

        $res = Get-PSResource "ClobberTestModule1"
        $res.Name | Should -Be "ClobberTestModule1"

        Install-PSResource -Name "ClobberTestModule2" -Repository $PSGalleryName -TrustRepository -NoClobber -ErrorAction SilentlyContinue
        $Error[0].FullyQualifiedErrorId | Should -be "CommandAlreadyExists,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource"
    }
#>

    It "Install PSResourceInfo object piped in" {
        Find-PSResource -Name $testModuleName2 -Version "0.0.2" -Repository $PSGalleryName | Install-Module
        $res = Get-PSResource -Name $testModuleName2
        $res.Name | Should -Be $testModuleName2
        $res.Version | Should -Be "0.0.2"
    }

    # Install module 1.4.3 (is authenticode signed and has catalog file)
    # Should install successfully 
    It "Install modules with catalog file using publisher validation" -Skip:(!(Get-IsWindows)) {
        $PackageManagement = "PackageManagement"
        Install-Module -Name $PackageManagement -RequiredVersion "1.4.3" -Repository $PSGalleryName

        $res1 = Get-PSResource $PackageManagement -Version "1.4.3"
        $res1.Name | Should -Be $PackageManagement
        $res1.Version | Should -Be "1.4.3"
    }

    # Install module 1.4.7 (is authenticode signed and has no catalog file)
    # Should not install successfully 
    It "Install module with no catalog file" -Skip:(!(Get-IsWindows)) {
        $PackageManagement = "PackageManagement"
        Install-Module -Name $PackageManagement -RequiredVersion "1.4.7" -Repository $PSGalleryName

        $res1 = Get-PSResource $PackageManagement -Version "1.4.7"
        $res1.Name | Should -Be $PackageManagement
        $res1.Version | Should -Be "1.4.7"    
    }

    # Install module that is not authenticode signed
    # Should FAIL to install the  module
    It "Install module that is not authenticode signed" -Skip:(!(Get-IsWindows)) {
        Install-Module -Name $testModuleName -RequiredVersion "5.0.0" -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -Be "InstallDependencyPackageFailure,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource" 
    }

    # Install 1.4.4.1 (with incorrect catalog file)
    # Should FAIL to install the  module
    It "Install module with incorrect catalog file" -Skip:(!(Get-IsWindows)) {
        { Install-Module -Name $PackageManagement -RequiredVersion "1.4.4.1" -Repository $PSGalleryName } | Should -Throw -ErrorId "ParameterArgumentValidationError,Install-Module"
    }

    # Install script that is signed
    # Should install successfully 
    It "Install script that is authenticode signed" -Skip:(!(Get-IsWindows)) {
        Install-Script -Name "Install-VSCode" -RequiredVersion "1.4.2" -Repository $PSGalleryName

        $res1 = Get-PSResource "Install-VSCode" -Version "1.4.2"
        $res1.Name | Should -Be "Install-VSCode"
        $res1.Version | Should -Be "1.4.2"
    }

    <#
    # Install script that is not signed
    # Should throw
    It "Install script that is not signed" -Skip:(!(Get-IsWindows)) {
        Install-Script -Name "TestTestScript" -RequiredVersion "1.3.1.1" -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "InstallPackageFailure,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource" 
    }
    #>
        
    # Unix only manual test
    # Expected path should be similar to: '/usr/local/share/powershell/Modules'
    It "Install module under AllUsers scope - Unix only" -Skip:(Get-IsWindows) {
        Install-Module -Name "TestModule99" -Repository $PSGalleryName -Scope AllUsers
        $pkg = Get-Module "TestModule99" -ListAvailable
        $pkg.Name | Should -Be "TestModule99" 
        $pkg.Path.Contains("/usr/") | Should -Be $true
    }

<#
    # This needs to be manually tested due to prompt
    It "Install module that requires accept license without -AcceptLicense flag" {
        Install-Module -Name "testModuleWithlicense" -Repository $TestGalleryName
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

    }
#>
}