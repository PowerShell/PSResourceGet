# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Uninstall-PSResource for Modules' {

    BeforeAll{
        $testModuleName = "test_module2"
        $testScriptName = "test_script"

        Get-NewPSResourceRepositoryFile
        Uninstall-PSResource -Name $testModuleName -Version "*"
        Uninstall-PSResource -Name $testScriptName -Version "*"
        Register-LocalRepos

        $testLocalModuleName = "TestMyLocalModule"
        $testLocalScriptName = "TestMyLocalScripts"
        $PSGalleryName = Get-PSGalleryName
        $localRepoName = "psgettestlocal"
        $emptyPrereleaseLabel = ""
        Get-ModuleResourcePublishedToLocalRepoTestDrive $testLocalModuleName $localRepoName "1.0.0.0" $emptyPrereleaseLabel
        Get-ModuleResourcePublishedToLocalRepoTestDrive $testLocalModuleName $localRepoName "3.0.0.0" $emptyPrereleaseLabel
        Get-ModuleResourcePublishedToLocalRepoTestDrive $testLocalModuleName $localRepoName "4.0.0.0" "alpha"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $testLocalModuleName $localRepoName "5.0.0.0" $emptyPrereleaseLabel

        Get-ScriptResourcePublishedToLocalRepoTestDrive $testLocalScriptName $localRepoName "1.0.0.0"
        Get-ScriptResourcePublishedToLocalRepoTestDrive $testLocalScriptName $localRepoName "3.0.0.0"
        Get-ScriptResourcePublishedToLocalRepoTestDrive $testLocalScriptName $localRepoName "4.0.0.0" "alpha"
        Get-ScriptResourcePublishedToLocalRepoTestDrive $testLocalScriptName $localRepoName "4.3.0.0" "beta"
        Get-ScriptResourcePublishedToLocalRepoTestDrive $testLocalScriptName $localRepoName "5.0.0.0"
    }

    BeforeEach {
        $null = Install-PSResource $testLocalModuleName -Version "1.0.0.0" -Repository $localRepoName -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource $testLocalScriptName -Version "1.0.0.0" -Repository $localRepoName -TrustRepository -WarningAction SilentlyContinue
    }

    AfterEach {
        Uninstall-PSResource -Name "TestMyLocalModule" -Version "1.0.0.0"
        Uninstall-PSResource -Name "TestMyLocalScript" -Version "1.0.0.0"
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Uninstall a specific module by name" {
        Uninstall-PSResource -name $testLocalModuleName -Version "1.0.0.0"
        Get-PSResource $testModuleName | Should -BeNullOrEmpty
    }

    $testCases = @{Name="Test?Module";      ErrorId="ErrorFilteringNamesForUnsupportedWildcards"},
                 @{Name="Test[Module";      ErrorId="ErrorFilteringNamesForUnsupportedWildcards"}

    It "not uninstall module given Name with invalid wildcard characters" -TestCases $testCases {
        param($Name, $ErrorId)
        Uninstall-PSResource -Name $Name -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "$ErrorId,Microsoft.PowerShell.PowerShellGet.Cmdlets.UninstallPSResource"
    }

    It "Uninstall a list of modules by name" {
        $null = Install-PSResource $testModuleName -Repository $PSGalleryName -TrustRepository -WarningAction SilentlyContinue -SkipDependencyCheck

        Uninstall-PSResource -Name $testLocalModuleName, $testModuleName
        Get-PSResource $testLocalModuleName, $testModuleName | Should -BeNullOrEmpty
    }

    It "Uninstall a specific script by name" {
        Uninstall-PSResource -Name $testLocalScriptName
        $res = Get-PSResource -Name $testLocalScriptName
        $res | Should -BeNullOrEmpty
    }

    It "Uninstall a list of scripts by name" {
        $null = Install-PSResource $testScriptName -Repository $PSGalleryName -TrustRepository

        Uninstall-PSResource -Name $testScriptName, $testLocalScriptName
        Get-PSResource -Name $testScriptName, $testLocalScriptName | Should -BeNullOrEmpty
    }

    It "Uninstall a module when given name and specifying all versions" {
        # Module TestMyLocalModule (stored in variable $testLocalModuleName) version 1.0.0 is already installed locally in BeforeEach       
        $null = Install-PSResource $testLocalModuleName -Version "3.0.0.0" -Repository "psgettestlocal" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource $testLocalModuleName -Version "5.0.0.0" -Repository "psgettestlocal" -TrustRepository -WarningAction SilentlyContinue

        Uninstall-PSResource -Name $testLocalModuleName -version "*"
        $pkgs = Get-PSResource $testLocalModuleName
        $pkgs.Version | Should -Not -Contain "1.0.0"
        $pkgs.Version | Should -Not -Contain "3.0.0"
        $pkgs.Version | Should -Not -Contain "5.0.0"
    }

    It "Uninstall a module when given name and using the default version (ie all versions, not explicitly specified)" {
        # Module TestMyLocalModule (stored in variable $testLocalModuleName) version 1.0.0 is already installed locally in BeforeEach       
        $null = Install-PSResource $testLocalModuleName -Version "3.0.0.0" -Repository "psgettestlocal" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource $testLocalModuleName -Version "5.0.0.0" -Repository "psgettestlocal" -TrustRepository -WarningAction SilentlyContinue

        Uninstall-PSResource -Name $testLocalModuleName -version "*"
        $pkgs = Get-PSResource $testLocalModuleName
        $pkgs.Version | Should -Not -Contain "1.0.0"
        $pkgs.Version | Should -Not -Contain "3.0.0"
        $pkgs.Version | Should -Not -Contain "5.0.0"
    }

    It "Uninstall module when given Name and specifying exact version" {
        # Module TestMyLocalModule (stored in variable $testLocalModuleName) version 1.0.0 is already installed locally in BeforeEach       
        $null = Install-PSResource $testLocalModuleName -Version "3.0.0.0" -Repository "psgettestlocal" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource $testLocalModuleName -Version "5.0.0.0" -Repository "psgettestlocal" -TrustRepository -WarningAction SilentlyContinue

        Uninstall-PSResource -Name $testLocalModuleName -Version "3.0.0"
        $pkgs = Get-PSResource -Name $testModuleName
        $pkgs.Version | Should -Not -Contain "3.0.0"
    }

    $testCases = @{Version="[1.0.0.0]";          ExpectedVersion="1.0.0.0"; Reason="validate version, exact match"},
                 @{Version="1.0.0.0";            ExpectedVersion="1.0.0.0"; Reason="validate version, exact match without bracket syntax"},
                 @{Version="[1.0.0.0, 5.0.0.0]"; ExpectedVersion="5.0.0.0"; Reason="validate version, exact range inclusive"},
                 @{Version="(1.0.0.0, 5.0.0.0)"; ExpectedVersion="3.0.0.0"; Reason="validate version, exact range exclusive"},
                 @{Version="(1.0.0.0,)";         ExpectedVersion="5.0.0.0"; Reason="validate version, minimum version exclusive"},
                 @{Version="[1.0.0.0,)";         ExpectedVersion="5.0.0.0"; Reason="validate version, minimum version inclusive"},
                 @{Version="(,3.0.0.0)";         ExpectedVersion="1.0.0.0"; Reason="validate version, maximum version exclusive"},
                 @{Version="(,3.0.0.0]";         ExpectedVersion="1.0.0.0"; Reason="validate version, maximum version inclusive"},
                 @{Version="[1.0.0.0, 5.0.0.0)"; ExpectedVersion="3.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
                
    It "Uninstall module when given Name to <Reason> <Version>" -TestCases $testCases {
        param($Version, $ExpectedVersion)
        Uninstall-PSResource -Name $testModuleName -Version "*"

        # Module TestMyLocalModule (stored in variable $testLocalModuleName) version 1.0.0 is already installed locally in BeforeEach       
        $null = Install-PSResource $testLocalModuleName -Version "3.0.0.0" -Repository "psgettestlocal" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource $testLocalModuleName -Version "5.0.0.0" -Repository "psgettestlocal" -TrustRepository -WarningAction SilentlyContinue

        Uninstall-PSResource -Name $testLocalModuleName -Version $Version
        $pkgs = Get-PSResource $testLocalModuleName
        $pkgs.Version | Should -Not -Contain $Version
    }

    $testCases2 =  @{Version='[1.*.0]';         Description="version with wilcard in middle"},
                   @{Version='[*.0.0.0]';       Description="version with wilcard at start"},
                   @{Version='[1.*.0.0]';       Description="version with wildcard at second digit"},
                   @{Version='[1.0.*.0]';       Description="version with wildcard at third digit"}
                   @{Version='[1.0.0.*]';       Description="version with wildcard at end"},
                   @{Version='[1..0.0]';        Description="version with missing digit in middle"},
                   @{Version='[1.0.0.]';        Description="version with missing digit at end"},
                   @{Version='[1.0.0.0.0]';     Description="version with more than 4 digits"}

    It "Do not uninstall module with incorrectly formatted version such as <Description>" -TestCases $testCases2 {
        param($Version, $Description)

        {Uninstall-PSResource -Name $testLocalModuleName -Version $Version} | Should -Throw "Argument for -Version parameter is not in the proper format."
    }

    $testCases3 =  @{Version='(1.0.0.0)';       Description="exclusive version (1.0.0.0)"},
                   @{Version='[1-0-0-0]';       Description="version formatted with invalid delimiter"}

    It "Do not uninstall module with incorrectly formatted version such as <Description>" -TestCases $testCases3 {
        param($Version, $Description)

        Uninstall-PSResource -Name $testLocalModuleName -Version $Version
        $pkg = Get-PSResource $testLocalModuleName -Version "1.0.0.0"
        $pkg.Version | Should -Be "1.0.0.0"
    }

    It "Uninstall prerelease version module when prerelease version specified" {
        Install-PSResource -Name $testModuleName -Version "4.0.0-alpha" -Repository $localRepoName -TrustRepository
        Uninstall-PSResource -Name $testModuleName -Version "4.0.0-alpha"
        Get-PSResource $testModuleName -Version "4.0.0-alpha" | Should -BeNullOrEmpty
    }

    It "Not uninstall non-prerelease version module when similar prerelease version is specified" {
        # test_module has a version 5.0.0.0, but no version 5.0.0-preview.
        # despite the core version part being the same this uninstall on a nonexistant prerelease version should not be successful
        Install-PSResource $testLocalModuleName -Version "5.0.0.0" -Repository $localRepoName -TrustRepository -WarningAction SilentlyContinue
        Uninstall-PSResource -Name $testLocalModuleName -Version "5.0.0-preview"
        $res = Get-PSResource -Name $testLocalModuleName -Version "5.0.0.0"
        $res.Name | Should -Be $testLocalModuleName
        $res.Version | Should -Be "5.0.0.0"
    }

    It "Uninstall prerelease version script when prerelease version specified" {
        Install-PSResource $testLocalScriptName -Version "4.0.0-alpha" -Repository $localRepoName -TrustRepository
        Uninstall-PSResource -Name $testLocalScriptName -Version "4.0.0-alpha"
        $res = Get-PSResource -Name $testLocalScriptName
        $res | Should -BeNullOrEmpty
    }

    It "Not uninstall non-prerelease version module when prerelease version specified" {
        Install-PSResource -Name $testLocalScriptName -Version "3.0.0.0" -Repository $localRepoName -TrustRepository
        Uninstall-PSResource -Name $testLocalScriptName -Version "3.0.0-alpha"
        $res = Get-PSResource -Name $testScriptName -Version "3.0.0.0"
        $res.Name | Should -Be $testScriptName
        $res.Version | Should -Be "3.0.0.0"
    }

    It "uninstall all prerelease versions (which satisfy the range) when -Version '*' and -Prerelease parameter is specified" {
        Uninstall-PSResource -Name $testLocalModuleName -Version "*"
        Install-PSResource -Name $testLocalModuleName -Version "1.0.0.0" -Repository $localRepoName -TrustRepository
        Install-PSResource -Name $testLocalModuleName -Version "3.0.0.0" -Repository $localRepoName -TrustRepository
        Install-PSResource -Name $testLocalModuleName -Version "4.0.0-alpha" -Repository $localRepoName -TrustRepository
        Install-PSResource -Name $testLocalModuleName -Version "5.0.0.0" -Repository $localRepoName -TrustRepository
        $res = Get-PSResource -Name $testLocalModuleName
        $prereleaseVersionPkgs = $res | Where-Object {$_.IsPrerelease -eq $true}
        $prereleaseVersionPkgs.Count | Should -Be 1

        Uninstall-PSResource -Name $testLocalModuleName -Version "*" -Prerelease
        $res = Get-PSResource -Name $testModuleName
        $prereleaseVersionPkgs = $res | Where-Object {$_.IsPrerelease -eq $true}
        $prereleaseVersionPkgs.Count | Should -Be 0
        $stableVersionPkgs = $res | Where-Object {$_.IsPrerelease -ne $true}
        $stableVersionPkgs.Count | Should -Be 3
    }

    It "uninstall all prerelease versions (which satisfy the range) when -Version range and -Prerelease parameter is specified" {
        Uninstall-PSResource -Name $testLocalModuleName -Version "*"
        Install-PSResource -Name $testLocalModuleName -Version "3.0.0.0" -Repository $localRepoName -TrustRepository
        Install-PSResource -Name $testLocalModuleName -Version "4.0.0-alpha" -Repository $localRepoName -TrustRepository
        Install-PSResource -Name $testLocalModuleName -Version "4.3.0-beta" -Repository $localRepoName -TrustRepository
        Install-PSResource -Name $testLocalModuleName -Version "5.0.0.0" -Repository $localRepoName -TrustRepository

        $res = Get-PSResource -Name $testLocalModuleName
        $prereleaseVersionPkgs = $res | Where-Object {$_.IsPrerelease -eq $true}
        $prereleaseVersionPkgs.Count | Should -Be 2

        Uninstall-PSResource -Name $testLocalModuleName -Version "[3.0.0, 4.2.0]" -Prerelease
        $res = Get-PSResource -Name $testLocalModuleName
        # should only uninstall 4.0.0-alpha, not 4.3.0-beta which isn't in the range specified so should remain installed
        $prereleaseVersionPkgs = $res | Where-Object {$_.IsPrerelease -eq $true}
        $prereleaseVersionPkgs.Count | Should -Be 1
        $stableVersionPkgs = $res | Where-Object {$_.IsPrerelease -ne $true}
        # versions 3.0.0 falls in range but should not be uninstalled as Prerelease parameter only selects prerelease versions to uninstall
        $stableVersionPkgs.Count | Should -Be 2
    }

    It "Uninstall module using -WhatIf, should not uninstall the module" {
        Uninstall-PSResource -Name $testLocalModuleName -WhatIf
        $pkg = Get-PSResource $testLocalModuleName -Version "5.0.0.0"
        $pkg.Version | Should -Be "5.0.0.0"
    }

    It "Do not Uninstall module that is a dependency for another module" {
        $null = Install-PSResource "test_module" -Repository $PSGalleryName -TrustRepository -WarningAction SilentlyContinue
    
        Uninstall-PSResource -Name "RequiredModule1" -ErrorVariable ev -ErrorAction SilentlyContinue

        $pkg = Get-PSResource "RequiredModule1"
        $pkg | Should -Not -Be $null

        $ev.FullyQualifiedErrorId | Should -BeExactly 'UninstallPSResourcePackageIsaDependency,Microsoft.PowerShell.PowerShellGet.Cmdlets.UninstallPSResource'
    }

    It "Uninstall module that is a dependency for another module using -SkipDependencyCheck" {
        $null = Install-PSResource $testModuleName -Repository $PSGalleryName -TrustRepository -WarningAction SilentlyContinue

        Uninstall-PSResource -Name "RequiredModule1" -SkipDependencyCheck
        
        $pkg = Get-PSResource "RequiredModule1"
        $pkg | Should -BeNullOrEmpty
    }

    It "Uninstall PSResourceInfo object piped in" {
        Install-PSResource -Name $testLocalModuleName -Version "3.0.0.0" -Repository $localRepoName -TrustRepository
        Get-PSResource -Name $testLocalModuleName -Version "3.0.0.0" | Uninstall-PSResource
        $res = Get-PSResource -Name $testLocalModuleName -Version "3.0.0.0"
        $res | Should -BeNullOrEmpty
    }

    It "Uninstall PSResourceInfo object piped in for prerelease version object" {
        Install-PSResource -Name $testLocalModuleName -Version "4.0.0-alpha" -Repository $localRepoName -TrustRepository
        Get-PSResource -Name $testLocalModuleName -Version "4.0.0-alpha" | Uninstall-PSResource
        $res = Get-PSResource -Name $testLocalModuleName -Version "4.0.0-alpha"
        $res | Should -BeNullOrEmpty
    }

    # Windows only
    It "Uninstall resource under CurrentUser scope only- Windows only" -Skip:(!((Get-IsWindows) -and (Test-IsAdmin))) {
        Install-PSResource -Name $testLocalModuleName -Repository $localRepoName -Version "5.0.0.0" -TrustRepository -Scope AllUsers -Reinstall
        Uninstall-PSResource -Name $testLocalModuleName -Scope CurrentUser 
        
        $pkg = Get-PSResource $testLocalModuleName
        $pkg.Name | Should -Be $testLocalModuleName
        # $pkg.Path.ToString().Contains("Program Files") | Should -Be $true
        $pkg.InstalledLocation.Contains("Program Files") | Should -Be $true
    }

    # Windows only
    It "Uninstall resource under AllUsers scope only- Windows only" -Skip:(!((Get-IsWindows) -and (Test-IsAdmin))) {
        Install-PSResource $testLocalModuleName -Repository $localRepoName -TrustRepository -Scope AllUsers -Reinstall
        Uninstall-PSResource -Name $testLocalModuleName -Scope AllUsers 

        $pkg = Get-PSResource $testLocalModuleName
        $pkg.Name | Should -Be $testModuleName
        # $pkg.Path.ToString().Contains("Documents") | Should -Be $true
        $pkg.InstalledLocation.Contains("Documents") | Should -Be $trur
    }
}
