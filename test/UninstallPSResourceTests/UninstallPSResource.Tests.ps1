# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

Describe 'Test Uninstall-PSResource for Modules' -tags 'CI' {

    BeforeAll{
        $PSGalleryName = Get-PSGalleryName
        $testModuleName = "test_module2"
        $testScriptName = "test_script"
        Get-NewPSResourceRepositoryFile
        Uninstall-PSResource -Name $testModuleName -Version "*" -ErrorAction SilentlyContinue -SkipDependencyCheck
        Uninstall-PSResource -Name $testScriptName -Version "*" -ErrorAction SilentlyContinue -SkipDependencyCheck
    }

    BeforeEach {
        $null = Install-PSResource $testModuleName -Repository $PSGalleryName -TrustRepository -WarningAction SilentlyContinue
    }

    AfterEach {
        Uninstall-PSResource -Name $testModuleName -Version "*" -ErrorAction SilentlyContinue -SkipDependencyCheck
        Uninstall-PSResource -Name "test_module" -Version "*" -ErrorAction SilentlyContinue -SkipDependencyCheck
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Uninstall a specific module by name" {
        Uninstall-PSResource -name $testModuleName -SkipDependencyCheck
        Get-InstalledPSResource $testModuleName | Should -BeNullOrEmpty
    }

    $testCases = @{Name="Test?Module";      ErrorId="ErrorFilteringNamesForUnsupportedWildcards"},
                 @{Name="Test[Module";      ErrorId="ErrorFilteringNamesForUnsupportedWildcards"}

    It "not uninstall module given Name with invalid wildcard characters" -TestCases $testCases {
        param($Name, $ErrorId)
        Uninstall-PSResource -Name $Name -ErrorVariable err -ErrorAction SilentlyContinue -SkipDependencyCheck
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "$ErrorId,Microsoft.PowerShell.PSResourceGet.Cmdlets.UninstallPSResource"
    }

    It "Uninstall a list of modules by name" {
        $null = Install-PSResource "testmodule99" -Repository $PSGalleryName -TrustRepository -WarningAction SilentlyContinue -SkipDependencyCheck

        Uninstall-PSResource -Name $testModuleName, "testmodule99" -SkipDependencyCheck
        Get-InstalledPSResource $testModuleName, "testmodule99" | Should -BeNullOrEmpty
    }

    It "Uninstall a specific script by name" {
        $null = Install-PSResource $testScriptName -Repository $PSGalleryName -TrustRepository
        $res = Get-InstalledPSResource -Name $testScriptName
        $res.Name | Should -Be $testScriptName

        Uninstall-PSResource -Name $testScriptName -SkipDependencyCheck
        $res = Get-InstalledPSResource -Name $testScriptName
        $res | Should -BeNullOrEmpty
    }

    It "Uninstall a list of scripts by name" {
        $null = Install-PSResource $testScriptName, "Required-Script1" -Repository $PSGalleryName -TrustRepository
        $res = Get-InstalledPSResource -Name $testScriptName
        $res.Name | Should -Be $testScriptName
        $res2 = Get-InstalledPSResource -Name "Required-Script1"
        $res2.Name | Should -Be "Required-Script1"

        Uninstall-PSResource -Name $testScriptName, "Required-Script1" -SkipDependencyCheck
        $res = Get-InstalledPSResource -Name $testScriptName
        $res | Should -BeNullOrEmpty
        $res2 = Get-InstalledPSResource -Name "Required-Script1"
        $res2 | Should -BeNullOrEmpty
    }

    It "Uninstall a module when given name and specifying all versions" {
        $null = Install-PSResource $testModuleName -Repository $PSGalleryName -Version "1.0.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource $testModuleName -Repository $PSGalleryName -Version "3.0.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource $testModuleName -Repository $PSGalleryName -Version "5.0.0" -TrustRepository -WarningAction SilentlyContinue

        Uninstall-PSResource -Name $testModuleName -version "*" -SkipDependencyCheck
        $pkgs = Get-InstalledPSResource $testModuleName
        $pkgs.Version | Should -Not -Contain "1.0.0"
        $pkgs.Version | Should -Not -Contain "3.0.0"
        $pkgs.Version | Should -Not -Contain "5.0.0"
    }

    It "Uninstall a module when given name and using the default version (ie all versions, not explicitly specified)" {
        $null = Install-PSResource $testModuleName -Repository $PSGalleryName -Version "1.0.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource $testModuleName -Repository $PSGalleryName -Version "3.0.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource $testModuleName -Repository $PSGalleryName -Version "5.0.0" -TrustRepository -WarningAction SilentlyContinue

        Uninstall-PSResource -Name $testModuleName -SkipDependencyCheck
        $pkgs = Get-InstalledPSResource $testModuleName
        $pkgs.Version | Should -Not -Contain "1.0.0"
        $pkgs.Version | Should -Not -Contain "3.0.0"
        $pkgs.Version | Should -Not -Contain "5.0.0"
    }

    It "Uninstall module when given Name and specifying exact version" {
        $null = Install-PSResource $testModuleName -Repository $PSGalleryName -Version "1.0.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource $testModuleName -Repository $PSGalleryName -Version "3.0.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource $testModuleName -Repository $PSGalleryName -Version "5.0.0" -TrustRepository -WarningAction SilentlyContinue

        Uninstall-PSResource -Name $testModuleName -Version "3.0.0" -SkipDependencyCheck
        $pkgs = Get-InstalledPSResource -Name $testModuleName
        $pkgs.Version | Should -Not -Contain "1.0.0"
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
        Uninstall-PSResource -Name $testModuleName -Version "*" -SkipDependencyCheck
        $null = Install-PSResource $testModuleName -Repository $PSGalleryName -Version "1.0.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource $testModuleName -Repository $PSGalleryName -Version "3.0.0" -TrustRepository -WarningAction SilentlyContinue
        $null = Install-PSResource $testModuleName -Repository $PSGalleryName -Version "5.0.0" -TrustRepository -WarningAction SilentlyContinue

        Uninstall-PSResource -Name $testModuleName -Version $Version -SkipDependencyCheck
        $pkgs = Get-InstalledPSResource $testModuleName
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

        {Uninstall-PSResource -Name $testModuleName -Version $Version -SkipDependencyCheck} | Should -Throw "Argument for -Version parameter is not in the proper format."
    }

    $testCases3 =  @{Version='(1.0.0.0)';       Description="exclusive version (1.0.0.0)"},
                @{Version='[1-0-0-0]';       Description="version formatted with invalid delimiter"}

    It "Do not uninstall module with incorrectly formatted version such as <Description>" -TestCases $testCases3 {
        param($Version, $Description)

        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository

        try {
            Uninstall-PSResource -Name $testModuleName -Version $Version -ErrorAction SilentlyContinue -SkipDependencyCheck
        }
        catch
        {}
        $pkg = Get-InstalledPSResource $testModuleName -Version "1.0.0.0"
        $pkg.Version | Should -Be "1.0.0.0"
    }

    It "Uninstall prerelease version module when prerelease version specified" {
        Install-PSResource -Name $testModuleName -Version "5.2.5-alpha001" -Repository $PSGalleryName -TrustRepository
        Uninstall-PSResource -Name $testModuleName -Version "5.2.5-alpha001" -SkipDependencyCheck
        $res = Get-InstalledPSResource $testModuleName -Version "5.2.5-alpha001"
        $res | Should -BeNullOrEmpty
    }

    It "Not uninstall non-prerelease version module when similar prerelease version is specified" {
        # test_module has a version 5.0.0.0, but no version 5.0.0-preview.
        # despite the core version part being the same this uninstall on a nonexistant prerelease version should not be successful
        Install-PSResource -Name $testModuleName -Version "5.0.0.0" -Repository $PSGalleryName -TrustRepository
        Uninstall-PSResource -Name $testModuleName -Version "5.0.0-preview" -ErrorAction SilentlyContinue -SkipDependencyCheck
        $res = Get-InstalledPSResource -Name $testModuleName -Version "5.0.0.0"
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0.0"
    }

    It "Uninstall prerelease version script when prerelease version specified" {
        Install-PSResource -Name $testScriptName -Version "3.0.0-alpha" -Repository $PSGalleryName -TrustRepository
        Uninstall-PSResource -Name $testScriptName -Version "3.0.0-alpha" -SkipDependencyCheck
        $res = Get-InstalledPSResource -Name $testScriptName
        $res | Should -BeNullOrEmpty
    }

    It "Not uninstall non-prerelease version module when prerelease version specified" {
        Install-PSResource -Name $testScriptName -Version "2.5.0.0" -Repository $PSGalleryName -TrustRepository
        Uninstall-PSResource -Name $testScriptName -Version "2.5.0-alpha001" -ErrorAction SilentlyContinue -SkipDependencyCheck
        $res = Get-InstalledPSResource -Name $testScriptName -Version "2.5.0.0"
        $res.Name | Should -Be $testScriptName
        $res.Version | Should -Be "2.5"
    }

    It "uninstall all prerelease versions (which satisfy the range) when -Version '*' and -Prerelease parameter is specified" {
        Uninstall-PSResource -Name $testModuleName -Version "*" -SkipDependencyCheck
        Install-PSResource -Name $testModuleName -Version "2.5.0-beta" -Repository $PSGalleryName -TrustRepository
        Install-PSResource -Name $testModuleName -Version "3.0.0" -Repository $PSGalleryName -TrustRepository
        Install-PSResource -Name $testModuleName -Version "5.0.0" -Repository $PSGalleryName -TrustRepository
        Install-PSResource -Name $testModuleName -Version "5.2.5-alpha001" -Repository $PSGalleryName -TrustRepository
        $res = Get-InstalledPSResource -Name $testModuleName
        $prereleaseVersionPkgs = $res | Where-Object {$_.IsPrerelease -eq $true}
        $prereleaseVersionPkgs.Count | Should -Be 2

        Uninstall-PSResource -Name $testModuleName -Version "*" -Prerelease -SkipDependencyCheck
        $res = Get-InstalledPSResource -Name $testModuleName
        $prereleaseVersionPkgs = $res | Where-Object {$_.IsPrerelease -eq $true}
        $prereleaseVersionPkgs.Count | Should -Be 0
        $stableVersionPkgs = $res | Where-Object {$_.IsPrerelease -ne $true}
        $stableVersionPkgs.Count | Should -Be 2
    }

    It "uninstall all prerelease versions (which satisfy the range) when -Version range and -Prerelease parameter is specified" {
        Uninstall-PSResource -Name $testModuleName -Version "*" -SkipDependencyCheck
        Install-PSResource -Name $testModuleName -Version "2.5.0-beta" -Repository $PSGalleryName -TrustRepository
        Install-PSResource -Name $testModuleName -Version "3.0.0" -Repository $PSGalleryName -TrustRepository
        Install-PSResource -Name $testModuleName -Version "5.0.0" -Repository $PSGalleryName -TrustRepository
        Install-PSResource -Name $testModuleName -Version "5.2.5-alpha001" -Repository $PSGalleryName -TrustRepository

        $res = Get-InstalledPSResource -Name $testModuleName
        $prereleaseVersionPkgs = $res | Where-Object {$_.IsPrerelease -eq $true}
        $prereleaseVersionPkgs.Count | Should -Be 2

        Uninstall-PSResource -Name $testModuleName -Version "[2.0.0, 5.0.0]" -Prerelease -SkipDependencyCheck
        $res = Get-InstalledPSResource -Name $testModuleName
        # should only uninstall 2.5.0-beta, 5.2.5-alpha001 is out of range and should remain installed
        $prereleaseVersionPkgs = $res | Where-Object {$_.IsPrerelease -eq $true}
        $prereleaseVersionPkgs.Count | Should -Be 1
        $stableVersionPkgs = $res | Where-Object {$_.IsPrerelease -ne $true}
        # versions 3.0.0 falls in range but should not be uninstalled as Prerelease parameter only selects prerelease versions to uninstall
        $stableVersionPkgs.Count | Should -Be 2
    }

    It "Uninstall module using -WhatIf, should not uninstall the module" {
        Start-Transcript .\testUninstallWhatIf.txt
        Uninstall-PSResource -Name $testModuleName -WhatIf -SkipDependencyCheck
        Stop-Transcript

        $pkg = Get-InstalledPSResource $testModuleName -Version "5.0.0.0"
        $pkg.Version | Should -Be "5.0.0.0"

        $match = Get-Content .\testUninstallWhatIf.txt | 
            select-string -pattern "What if: Performing the operation ""Uninstall-PSResource"" on target ""Uninstall resource 'test_module2', version '5.0.0.0', from path '" -SimpleMatch

        $match -ne $null

        RemoveItem -path .\testUninstallWhatIf.txt
    }

    It "Do not Uninstall module that is a dependency for another module" {
        Install-PSResource "test_module" -Repository $PSGalleryName -TrustRepository -Version "3.0.0" -Reinstall

        $pkg = Get-InstalledPSResource "test_module"
        write-host "$pkg"

        $pkg = Get-InstalledPSResource "RequiredModule1"
        $pkg | Should -Not -Be $null
        write-host "$pkg"

        Uninstall-PSResource -Name "RequiredModule1" -ErrorVariable ev -ErrorAction SilentlyContinue

        $pkg = Get-InstalledPSResource "RequiredModule1"
        $pkg | Should -Not -Be $null

        $ev.FullyQualifiedErrorId | Should -BeExactly 'UninstallPSResourcePackageIsADependency,Microsoft.PowerShell.PSResourceGet.Cmdlets.UninstallPSResource'
    }

    It "Do not uninstall specific module version that is dependency for another module" {
        $null = Install-PSResource "test_module" -Repository $PSGalleryName -TrustRepository -SkipDependencyCheck -Version "1.0.0.0"
        $null = Install-PSResource "test_module" -Repository $PSGalleryName -TrustRepository -SkipDependencyCheck -Version "3.0.0.0"
        $null = Install-PSResource "test_module" -Repository $PSGalleryName -TrustRepository -SkipDependencyCheck -Version "5.0.0.0"
        $null = Install-PSResource "ModuleWithDependency" -Repository $PSGalleryName -TrustRepository -SkipDependencyCheck
       
        $pkg = Get-InstalledPSResource "test_module"
        $pkg | Should -Not -Be $null
        $pkg.Count | Should -BeGreaterOrEqual 3
        Uninstall-PSResource -Name "test_module" -Version "*" -ErrorVariable ev -ErrorAction SilentlyContinue

        $pkg = Get-InstalledPSResource "test_module"
        $pkg | Should -Not -Be $null
        $pkg.Count | Should -Be 1
        $ev.FullyQualifiedErrorId | Should -BeExactly 'UninstallPSResourcePackageIsADependency,Microsoft.PowerShell.PSResourceGet.Cmdlets.UninstallPSResource'
    }

    It "Uninstall module that is a dependency for another module using -SkipDependencyCheck" {
        $null = Install-PSResource $testModuleName -Repository $PSGalleryName -TrustRepository -WarningAction SilentlyContinue

        Uninstall-PSResource -Name "RequiredModule1" -SkipDependencyCheck

        $pkg = Get-InstalledPSResource "RequiredModule1"
        $pkg | Should -BeNullOrEmpty
    }

    It "Uninstall PSResourceInfo object piped in" {
        Install-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName -TrustRepository
        Get-InstalledPSResource -Name $testModuleName -Version "1.0.0.0" | Uninstall-PSResource -SkipDependencyCheck
        $res = Get-InstalledPSResource -Name "ContosoServer" -Version "1.0.0.0"
        $res | Should -BeNullOrEmpty
    }

    It "Uninstall PSResourceInfo object piped in for prerelease version object" {
        Install-PSResource -Name $testModuleName -Version "2.5.0-beta" -Repository $PSGalleryName -TrustRepository
        Get-InstalledPSResource -Name $testModuleName -Version "2.5.0-beta" | Uninstall-PSResource -SkipDependencyCheck
        $res = Get-InstalledPSResource -Name $testModuleName -Version "2.5.0-beta"
        $res | Should -BeNullOrEmpty
    }

    It "Install resource via InputObject by piping from Find-PSResource" {
        Install-PSResource -Name $testModuleName, $testScriptName -Repository $PSGalleryName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName, $testScriptName
        $pkg.Count | Should -BeGreaterThan 1

        Uninstall-PSResource -InputObject $pkg -SkipDependencyCheck

        $foundPkgs = Get-InstalledPSResource $pkg.Name
        $foundPkgs.Count | Should -Be 0
    }

    It "Uninstall module that is not installed should throw error" {
        Uninstall-PSResource -Name "NonInstalledModule" -ErrorVariable ev -ErrorAction SilentlyContinue -SkipDependencyCheck
        $ev.FullyQualifiedErrorId | Should -BeExactly 'ResourceNotInstalled,Microsoft.PowerShell.PSResourceGet.Cmdlets.UninstallPSResource'
    }

    # Windows only
    It "Uninstall resource under CurrentUser scope only- Windows only" -Skip:(!((Get-IsWindows) -and (Test-IsAdmin))) {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -Scope AllUsers -Reinstall
        Uninstall-PSResource -Name $testModuleName -Scope CurrentUser -SkipDependencyCheck

        $pkg = Get-Module $testModuleName -ListAvailable
        $pkg.Name | Should -Be $testModuleName
        $pkg.Path.ToString().Contains("Program Files") | Should -Be $true
    }

    # Windows only
    It "Uninstall resource under AllUsers scope only- Windows only" -Skip:(!((Get-IsWindows) -and (Test-IsAdmin))) {
        Install-PSResource $testModuleName -Repository $PSGalleryName -TrustRepository -Scope AllUsers -Reinstall
        Uninstall-PSResource -Name $testModuleName -Scope AllUsers -SkipDependencyCheck

        $pkg = Get-Module $testModuleName -ListAvailable
        $pkg.Name | Should -Be $testModuleName
        $pkg.Path.ToString().Contains("Documents") | Should -Be $true
    }
}
