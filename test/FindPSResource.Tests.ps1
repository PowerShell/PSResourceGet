# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Find-PSResource for Module' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        $NuGetGalleryName = Get-NuGetGalleryName
        $commandName = "Get-TargetResource"
        $dscResourceName = "SystemLocale"
        $parentModuleName = "SystemLocaleDsc"
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "find Specific Module Resource by Name" {
        $specItem = Find-PSResource -Name "Carbon"
        $specItem.Name | Should -Be "Carbon"
    }

    It "should not find resource given nonexistant name" {
        $res = Find-PSResource -Name NonExistantModule
        $res | Should -BeNullOrEmpty
    }

    It "should not find any resources given names with invalid wildcard characters" {
        Find-PSResource -Name "Invalid?PkgName", "Invalid[PkgName" -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ErrorFilteringNamesForUnsupportedWildcards,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource" 
    }

    It "find resources when Name contains * from V2 endpoint repository (PowerShellGallery))" {
        $foundScript = $False
        $res = Find-PSResource -Name "AzureS*" -Repository $PSGalleryName
        $res.Count | Should -BeGreaterThan 1
        # should find Module and Script resources
        foreach ($item in $res) {
            if ($item.Type -eq "Script")
            {
                $foundScript = $true
            }
        }

        $foundScript | Should -BeTrue
    }

    It "should not find resources given Name that equals wildcard, '*'" {
        Find-PSResource -Name "*" -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "NameEqualsWildcardIsNotSupported,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    It "find resource given Name from V3 endpoint repository (NuGetGallery)" {
        $res = Find-PSResource -Name "Serilog" -Repository $NuGetGalleryName
        $res.Count | Should -Be 1
        $res.Name | Should -Be "Serilog"
        $res.Repository | Should -Be $NuGetGalleryName
    }

    It "find resources when Name contains wildcard * from V3 endpoint repository" {
        $res = Find-PSResource -Name "Serilog*" -Repository $NuGetGalleryName
        $res.Count | Should -BeGreaterThan 1
    }

    $testCases2 = @{Version="[2.10.0.0]";          ExpectedVersions=@("2.10.0.0"); Reason="validate version, exact match"},
                  @{Version="2.10.0.0";            ExpectedVersions=@("2.10.0.0"); Reason="validate version, exact match without bracket syntax"},
                  @{Version="[2.5.0.0, 2.8.0.0]";  ExpectedVersions=@("2.5.0.0", "2.5.1.0", "2.5.2.0", "2.5.3.0", "2.5.4.0", "2.6.0.0", "2.7.0.0", "2.8.0.0"); Reason="validate version, exact range inclusive"},
                  @{Version="(2.5.0.0, 2.8.0.0)";  ExpectedVersions=@("2.5.1.0", "2.5.2.0", "2.5.3.0", "2.5.4.0", "2.6.0.0", "2.7.0.0"); Reason="validate version, exact range exclusive"},
                  @{Version="(2.9.4.0,)";          ExpectedVersions=@("2.10.0.0", "2.10.1.0", "2.10.2.0"); Reason="validate version, minimum version exclusive"},
                  @{Version="[2.9.4.0,)";          ExpectedVersions=@("2.9.4.0", "2.10.0.0", "2.10.1.0", "2.10.2.0"); Reason="validate version, minimum version inclusive"},
                  @{Version="(,2.0.0.0)";          ExpectedVersions=@("1.9.0.0"); Reason="validate version, maximum version exclusive"},
                  @{Version="(,2.0.0.0]";          ExpectedVersions=@("1.9.0.0", "2.0.0.0"); Reason="validate version, maximum version inclusive"},
                  @{Version="[2.5.0.0, 2.8.0.0)";  ExpectedVersions=@("2.5.0.0", "2.5.1.0", "2.5.2.0", "2.5.3.0", "2.5.4.0", "2.6.0.0", "2.7.0.0", "2.8.0.0"); Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
                  @{Version="(2.5.0.0, 2.8.0.0]";  ExpectedVersions=@("2.5.1.0", "2.5.2.0", "2.5.3.0", "2.5.4.0", "2.6.0.0", "2.7.0.0", "2.8.0.0"); Reason="validate version, mixed exclusive minimum and inclusive maximum version"}

    It "find resource when given Name to <Reason> <Version>" -TestCases $testCases2{
        param($Version, $ExpectedVersions)
        $res = Find-PSResource -Name "Carbon" -Version $Version -Repository $PSGalleryName
        foreach ($item in $res) {
            $item.Name | Should -Be "Carbon"
            $ExpectedVersions | Should -Contain $item.Version
        }
    }

    It "not find resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(2.10.0.0)';       Description="exclusive version (2.10.0.0)"},
        @{Version='[2-10-0-0]';       Description="version formatted with invalid delimiter"}
    ) {
        param($Version, $Description)

        $res = Find-PSResource -Name "Carbon" -Version $Version -Repository $PSGalleryName
        $res | Should -BeNullOrEmpty
    }

    $testCases = @{Version='[2.*.0.0]';       Description="version with wilcard in middle"},
                 @{Version='[*.10.0.0]';      Description="version with wilcard at start"},
                 @{Version='[2.10.*.0]';      Description="version with wildcard at third digit"}
                 @{Version='[1.5.0.*';        Description="version with wildcard at end"},
                 @{Version='[1..0.0]';        Description="version with missing digit in middle"},
                 @{Version='[1.5.0.]';        Description="version with missing digit at end"},
                 @{Version='[1.5.0.0.0]';     Description="version with more than 4 digits"}

    It "not find resource and throw exception with incorrectly formatted version such as <Description>" -TestCases $testCases {
        param($Version, $Description)

        Find-PSResource -Name "Carbon" -Version $Version -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "IncorrectVersionFormat,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    It "find resources when given Name, Version not null --> '*'" {
        $res = Find-PSResource -Name "Carbon" -Version "*" -Repository $PSGalleryName
        $res | ForEach-Object {
            $_.Name | Should -Be "Carbon"
        }
        $res.Count | Should -BeGreaterOrEqual 1
    }

    It "find resources when given Name with wildcard, Version not null --> '*'" {
        $res = Find-PSResource -Name "TestModuleWithDependency*" -Version "*" -Repository $TestGalleryName
        $moduleA = $res | Where-Object {$_.Name -eq "TestModuleWithDependencyA"}
        $moduleA.Count | Should -BeGreaterOrEqual 3
        $moduleB = $res | Where-Object {$_.Name -eq "TestModuleWithDependencyB"}
        $moduleB.Count | Should -BeGreaterOrEqual 2
        $moduleC = $res | Where-Object {$_.Name -eq "TestModuleWithDependencyC"}
        $moduleC.Count | Should -BeGreaterOrEqual 3
        $moduleD = $res | Where-Object {$_.Name -eq "TestModuleWithDependencyD"}
        $moduleD.Count | Should -BeGreaterOrEqual 2
        $moduleE = $res | Where-Object {$_.Name -eq "TestModuleWithDependencyE"}
        $moduleE.Count | Should -BeGreaterOrEqual 1        
        $moduleF = $res | Where-Object {$_.Name -eq "TestModuleWithDependencyF"}
        $moduleF.Count | Should -BeGreaterOrEqual 1
    }

    It "find resources when given Name with wildcard, Version range" {
        $res = Find-PSResource -Name "TestModuleWithDependency*" -Version "[1.0.0.0, 2.0.0.0]" -Repository $TestGalleryName
        foreach ($pkg in $res) {
            $pkg.Name | Should -Match "TestModuleWithDependency*"
            [System.Version]$pkg.Version -ge [System.Version]"1.0.0.0" -or [System.Version]$pkg.Version -le [System.Version]"2.0.0.0" | Should -Be $true
        }
    }

    It "find resource when given Name, Version param null" {
        $res = Find-PSResource -Name "Carbon" -Repository $PSGalleryName
        $res.Name | Should -Be "Carbon"
        $res.Version | Should -Be "2.10.2.0"
    }

    It "find resource with latest (including prerelease) version given Prerelease parameter" {
        # test_module resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Find-PSResource -Name "test_module" -Repository $TestGalleryName
        $res.Version | Should -Be "5.0.0.0"

        $resPrerelease = Find-PSResource -Name "test_module" -Prerelease -Repository $TestGalleryName
        $resPrerelease.Version | Should -Be "5.2.5.0"
    }

    It "find resources, including Prerelease version resources, when given Prerelease parameter" {
        $resWithoutPrerelease = Find-PSResource -Name "Carbon" -Version "*" -Repository $PSGalleryName
        $resWithPrerelease = Find-PSResource -Name "Carbon" -Version "*" -Repository $PSGalleryName
        $resWithPrerelease.Count | Should -BeGreaterOrEqual $resWithoutPrerelease.Count
    }

    It "find resource of Type script or module from PSGallery, when no Type parameter provided" {
        $resScript = Find-PSResource -Name "AzureSqlScale" -Repository $PSGalleryName
        $resScript.Name | Should -Be "AzureSqlScale"
        $resScript.Type | Should -Be "Script"

        $resModule = Find-PSResource -Name "Carbon" -Repository $PSGalleryName
        $resModule.Name | Should -Be "Carbon"
        $resModuleType = Out-String -InputObject $resModule.Type
        $resModuleType.Replace(",", " ").Split() | Should -Contain "Module"
    }

    It "find resource of Type Script from PSGallery, when Type Script specified" {
        $resScript = Find-PSResource -Name "AzureSqlScale" -Repository $PSGalleryName -Type "Script"
        $resScript.Name | Should -Be "AzureSqlScale"
        $resScript.Repository | Should -Be "PSGalleryScripts"
        $resScript.Type | Should -Be "Script"
    }

    It "find resource of Type Command from PSGallery, when Type Command specified" {
        $resources = Find-PSResource -Name "AzureS*" -Repository $PSGalleryName -Type "Command"
        foreach ($item in $resources) {
            $resType = Out-String -InputObject $item.Type
            $resType.Replace(",", " ").Split() | Should -Contain "Command"
        }
    }

    It "find resuources given Tag parameter" {
        $resWithEitherExpectedTag = @("NetworkingDsc", "DSCR_FileContent", "SS.PowerShell")
        $res = Find-PSResource -Name "NetworkingDsc", "HPCMSL", "DSCR_FileContent", "SS.PowerShell", "PowerShellGet" -Tag "Dsc", "json" -Repository $PSGalleryName
        foreach ($item in $res) {
            $resWithEitherExpectedTag | Should -Contain $item.Name
        }
    }

    It "find resource with IncludeDependencies parameter" {
        $res = Find-PSResource -Name "Az.Compute" -IncludeDependencies -Repository $PSGalleryName
        $isDependencyNamePresent = $False
        $isDependencyVersionCorrect = $False
        foreach ($item in $res) {
            if ($item.Name -eq "Az.Accounts")
            {
                $isDependencyNamePresent = $True
                $isDependencyVersionCorrect = [System.Version]$item.Version -ge [System.Version]"2.2.8.0"
            }
        }
        $isDependencyNamePresent | Should -BeTrue
        $isDependencyVersionCorrect | Should -BeTrue
    }

    It "find resource in local repository given Repository parameter" {
        $publishModuleName = "TestFindModule"
        $repoName = "psgettestlocal"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $publishModuleName $repoName

        $res = Find-PSResource -Name $publishModuleName -Repository $repoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be $publishModuleName
        $res.Repository | Should -Be $repoName
    }

    It "find Resource given repository parameter, where resource exists in multiple local repos" {
        $moduleName = "test_local_mod"
        $repoHigherPriorityRanking = "psgettestlocal"
        $repoLowerPriorityRanking = "psgettestlocal2"

        Get-ModuleResourcePublishedToLocalRepoTestDrive $moduleName $repoHigherPriorityRanking
        Get-ModuleResourcePublishedToLocalRepoTestDrive $moduleName $repoLowerPriorityRanking

        $res = Find-PSResource -Name $moduleName
        $res.Repository | Should -Be $repoHigherPriorityRanking

        $resNonDefault = Find-PSResource -Name $moduleName -Repository $repoLowerPriorityRanking
        $resNonDefault.Repository | Should -Be $repoLowerPriorityRanking
    }

    It "find resource given CommandName (CommandNameParameterSet)" {
        $res = Find-PSResource -CommandName $commandName -Repository $PSGalleryName
        foreach ($item in $res) {
            $item.Name | Should -Be $commandName
            $item.ParentResource.Includes.Command | Should -Contain $commandName
        }
    }

    It "find resource given CommandName and ModuleName (CommandNameParameterSet)" {
        $res = Find-PSResource -CommandName $commandName -ModuleName $parentModuleName -Repository $PSGalleryName
        $res.Name | Should -Be $commandName
        $res.ParentResource.Name | Should -Be $parentModuleName
        $res.ParentResource.Includes.Command | Should -Contain $commandName
    }

    It "find resource given DSCResourceName (DSCResourceNameParameterSet)" {
        $res = Find-PSResource -DscResourceName $dscResourceName -Repository $PSGalleryName
        foreach ($item in $res) {
            $item.Name | Should -Be $dscResourceName
            $item.ParentResource.Includes.DscResource | Should -Contain $dscResourceName
        }
    }

    It "find resource given DscResourceName and ModuleName (DSCResourceNameParameterSet)" {
        $res = Find-PSResource -DscResourceName $dscResourceName -ModuleName $parentModuleName -Repository $PSGalleryName
        $res.Name | Should -Be $dscResourceName
        $res.ParentResource.Name | Should -Be $parentModuleName
        $res.ParentResource.Includes.DscResource | Should -Contain $dscResourceName
    }
}
