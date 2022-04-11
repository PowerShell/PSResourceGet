# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Find-PSResource for Module' {

    BeforeAll{
        $PSGalleryName = Get-PSGalleryName
        $NuGetGalleryName = Get-NuGetGalleryName
        $testModuleName = "test_module"
        $testScriptName = "test_script"
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
        $specItem = Find-PSResource -Name $testModuleName
        $specItem.Name | Should -Be $testModuleName
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
        foreach ($item in $res)
        {
            if ($item.Type -eq "Script")
            {
                $foundScript = $true
            }
        }

        $foundScript | Should -BeTrue
    }

    # TODO: get working with local repo
    # It "should find all resources given Name that equals wildcard, '*'" {
    #     $repoName = "psgettestlocal"
    #     Get-ModuleResourcePublishedToLocalRepoTestDrive "TestLocalModule1" $repoName
    #     Get-ModuleResourcePublishedToLocalRepoTestDrive "TestLocalModule2" $repoName
    #     Get-ModuleResourcePublishedToLocalRepoTestDrive "TestLocalModule3" $repoName

    #     $foundResources = Find-PSResource -Name "TestLocalModule1","TestLocalModule2","TestLocalModule3" -Repository $repoName
    #     # TODO: wildcard search is not supported with local repositories, from NuGet protocol API side- ask about this.
    #     # $foundResources = Find-PSResource -Name "*" -Repository $repoName 
    #     $foundResources.Count | Should -Not -Be 0

    #     # Should find Module and Script resources but no prerelease resources
    #     $foundResources | where-object Name -eq "TestLocalModule1" | Should -Not -BeNullOrEmpty -Because "TestLocalModule1 should exist in local repo"
    #     $foundResources | where-object Name -eq "test_script" | Should -Not -BeNullOrEmpty -Because "TestLocalScript1 should exist in local repo"
    #     $foundResources | where-object IsPrerelease -eq $true | Should -BeNullOrEmpty -Because "No prerelease resources should be returned"
    # }

    # # TODO: get working with local repo
    # It "should find all resources (including prerelease) given Name that equals wildcard, '*' and Prerelease parameter" {
    #     Get-ModuleResourcePublishedToLocalRepoTestDrive "TestLocalModule1" $repoName
    #     Get-ModuleResourcePublishedToLocalRepoTestDrive "TestLocalModule2" $repoName
    #     Get-ModuleResourcePublishedToLocalRepoTestDrive "TestLocalModule3" $repoName
    #     $foundResources = Find-PSResource -Name "*" -Prerelease -Repository $repoName

    #     # Should find Module and Script resources inlcuding prerelease resources
    #     $foundResources | where-object Name -eq "test_module" | Should -Not -BeNullOrEmpty -Because "test_module should exist in local repo"
    #     $foundResources | where-object Name -eq "test_script" | Should -Not -BeNullOrEmpty -Because "test_script should exist in local repo"
    #     $foundResources | where-object IsPrerelease -eq $true | Should -Not -BeNullOrEmpty -Because "Prerelease resources should be returned"
    # }

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

    $testCases2 = @{Version="[5.0.0.0]";           ExpectedVersions=@("5.0.0.0");                                  Reason="validate version, exact match"},
                  @{Version="5.0.0.0";             ExpectedVersions=@("5.0.0.0");                                  Reason="validate version, exact match without bracket syntax"},
                  @{Version="[1.0.0.0, 5.0.0.0]";  ExpectedVersions=@("1.0.0.0", "3.0.0.0", "5.0.0.0");            Reason="validate version, exact range inclusive"},
                  @{Version="(1.0.0.0, 5.0.0.0)";  ExpectedVersions=@("3.0.0.0");                                  Reason="validate version, exact range exclusive"},
                  @{Version="(1.0.0.0,)";          ExpectedVersions=@("3.0.0.0", "5.0.0.0");                       Reason="validate version, minimum version exclusive"},
                  @{Version="[1.0.0.0,)";          ExpectedVersions=@("1.0.0.0", "3.0.0.0", "5.0.0.0");            Reason="validate version, minimum version inclusive"},
                  @{Version="(,3.0.0.0)";          ExpectedVersions=@("1.0.0.0");                                  Reason="validate version, maximum version exclusive"},
                  @{Version="(,3.0.0.0]";          ExpectedVersions=@("1.0.0.0", "3.0.0.0");                       Reason="validate version, maximum version inclusive"},
                  @{Version="[1.0.0.0, 5.0.0.0)";  ExpectedVersions=@("1.0.0.0", "3.0.0.0");                       Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
                  @{Version="(1.0.0.0, 5.0.0.0]";  ExpectedVersions=@("3.0.0.0", "5.0.0.0");                       Reason="validate version, mixed exclusive minimum and inclusive maximum version"}

    It "find resource when given Name to <Reason> <Version>" -TestCases $testCases2{
        param($Version, $ExpectedVersions)
        $res = Find-PSResource -Name $testModuleName -Version $Version -Repository $PSGalleryName
        foreach ($item in $res) {
            $item.Name | Should -Be $testModuleName
            $ExpectedVersions | Should -Contain $item.Version
        }
    }

    It "not find resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(1.0.0.0)';       Description="exclusive version (2.5.0.0)"},
        @{Version='[1-0-0-0]';       Description="version formatted with invalid delimiter"}
    ) {
        param($Version, $Description)

        $res = Find-PSResource -Name $testModuleName -Version $Version -Repository $PSGalleryName
        $res | Should -BeNullOrEmpty
    }

    $testCases = @{Version='[1.*.0.0]';       Description="version with wilcard in middle"},
                 @{Version='[*.0.0.0]';       Description="version with wilcard at start"},
                 @{Version='[1.0.*.0]';       Description="version with wildcard at third digit"}
                 @{Version='[1.0.0.*';        Description="version with wildcard at end"},
                 @{Version='[1..0.0]';        Description="version with missing digit in middle"},
                 @{Version='[1.0.0.]';        Description="version with missing digit at end"},
                 @{Version='[1.0.0.0.0]';     Description="version with more than 4 digits"}

    It "not find resource and throw exception with incorrectly formatted version such as <Description>" -TestCases $testCases {
        param($Version, $Description)

        Find-PSResource -Name $testModuleName -Version $Version -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "IncorrectVersionFormat,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    It "find all versions of resource when given Name, Version not null --> '*'" {
        $res = Find-PSResource -Name $testModuleName -Version "*" -Repository $PSGalleryName
        $res | ForEach-Object {
            $_.Name | Should -Be $testModuleName
        }
        $res.Count | Should -BeGreaterOrEqual 1
    }

    It "find resources when given Name with wildcard, Version not null --> '*'" {
        $res = Find-PSResource -Name "TestModuleWithDependency*" -Version "*" -Repository $PSGalleryName
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
        $res = Find-PSResource -Name "TestModuleWithDependency*" -Version "[1.0.0.0, 5.0.0.0]" -Repository $PSGalleryName
        foreach ($pkg in $res) {
            $pkg.Name | Should -Match "TestModuleWithDependency*"
            [System.Version]$pkg.Version -ge [System.Version]"1.0.0.0" -or [System.Version]$pkg.Version -le [System.Version]"5.0.0.0" | Should -Be $true
        }
    }

    It "find resource when given Name, Version param null" {
        $res = Find-PSResource -Name $testModuleName -Repository $PSGalleryName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0.0"
    }

    It "find resource with latest (including prerelease) version given Prerelease parameter" {
        # test_module resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Find-PSResource -Name $testModuleName -Repository $PSGalleryName
        $res.Version | Should -Be "5.0.0.0"

        $resPrerelease = Find-PSResource -Name $testModuleName -Prerelease -Repository $PSGalleryName
        $resPrerelease.Version | Should -Be "5.2.5.0"
        $resPrerelease.Prerelease | Should -Be "alpha001"
    }

    It "find resources, including Prerelease version resources, when given Prerelease parameter" {
        $resWithoutPrerelease = Find-PSResource -Name $testModuleName -Version "*" -Repository $PSGalleryName
        $resWithPrerelease = Find-PSResource -Name $testModuleName -Version "*" -Repository $PSGalleryName
        $resWithPrerelease.Count | Should -BeGreaterOrEqual $resWithoutPrerelease.Count
    }

    It "find resource and its dependency resources with IncludeDependencies parameter" {
        $resWithoutDependencies = Find-PSResource -Name "TestModuleWithDependencyE" -Repository $PSGalleryName
        $resWithoutDependencies.Count | Should -Be 1
        $resWithoutDependencies.Name | Should -Be "TestModuleWithDependencyE"

        # TestModuleWithDependencyE has the following dependencies:
        # TestModuleWithDependencyC <= 1.0.0.0
        #    TestModuleWithDependencyB >= 1.0.0.0
        #    TestModuleWithDependencyD <= 1.0.0.0

        $resWithDependencies = Find-PSResource -Name "TestModuleWithDependencyE" -IncludeDependencies -Repository $PSGalleryName
        $resWithDependencies.Count | Should -BeGreaterThan $resWithoutDependencies.Count

        $foundParentPkgE = $false
        $foundDepB = $false
        $foundDepBCorrectVersion = $false
        $foundDepC = $false
        $foundDepCCorrectVersion = $false
        $foundDepD = $false
        $foundDepDCorrectVersion = $false
        foreach ($pkg in $resWithDependencies)
        {
            if ($pkg.Name -eq "TestModuleWithDependencyE")
            {
                $foundParentPkgE = $true
            }
            elseif ($pkg.Name -eq "TestModuleWithDependencyC")
            {
                $foundDepC = $true
                $foundDepCCorrectVersion = [System.Version]$pkg.Version -le [System.Version]"1.0.0.0"
            }
            elseif ($pkg.Name -eq "TestModuleWithDependencyB")
            {
                $foundDepB = $true
                $foundDepBCorrectVersion = [System.Version]$pkg.Version -ge [System.Version]"3.0.0.0"
            }
            elseif ($pkg.Name -eq "TestModuleWithDependencyD")
            {
                $foundDepD = $true
                $foundDepDCorrectVersion = [System.Version]$pkg.Version -le [System.Version]"1.0.0.0"
            }
        }

        $foundParentPkgE | Should -Be $true
        $foundDepC | Should -Be $true
        $foundDepCCorrectVersion | Should -Be $true
        $foundDepB | Should -Be $true
        $foundDepBCorrectVersion | Should -Be $true
        $foundDepD | Should -Be $true
        $foundDepDCorrectVersion | Should -Be $true
    }

    It "find resource of Type script or module from PSGallery, when no Type parameter provided" {
        $resScript = Find-PSResource -Name $testScriptName -Repository $PSGalleryName
        $resScript.Name | Should -Be $testScriptName
        $resScriptType = Out-String -InputObject $resScript.Type
        $resScriptType.Replace(",", " ").Split() | Should -Contain "Script"

        $resModule = Find-PSResource -Name $testModuleName -Repository $PSGalleryName
        $resModule.Name | Should -Be $testModuleName
        $resModuleType = Out-String -InputObject $resModule.Type
        $resModuleType.Replace(",", " ").Split() | Should -Contain "Module"
    }

    It "find resource of Type Script from PSGallery, when Type Script specified" {
        $resScript = Find-PSResource -Name $testScriptName -Repository $PSGalleryName -Type "Script"
        $resScript.Name | Should -Be $testScriptName
        $resScript.Repository | Should -Be "PSGalleryScripts"
        $resScriptType = Out-String -InputObject $resScript.Type
        $resScriptType.Replace(",", " ").Split() | Should -Contain "Script"
    }

    It "find resource of Type Command from PSGallery, when Type Command specified" {
        $resources = Find-PSResource -Name "AzureS*" -Repository $PSGalleryName -Type "Command"
        foreach ($item in $resources) {
            $resType = Out-String -InputObject $item.Type
            $resType.Replace(",", " ").Split() | Should -Contain "Command"
        }
    }

    It "find all resources of Type Module when Type parameter set is used" -Skip {
        $foundScript = $False
        $res = Find-PSResource -Name "test*" -Type Module -Repository $PSGalleryName
        $res.Count | Should -BeGreaterThan 1
        foreach ($item in $res) {
            if ($item.Type -eq "Script")
            {
                $foundScript = $True
            }
        }

        $foundScript | Should -Be $False
    }

    It "find resources given Tag parameter" {
        $resWithEitherExpectedTag = @("NetworkingDsc", "DSCR_FileContent", "SS.PowerShell")
        $res = Find-PSResource -Name "NetworkingDsc", "HPCMSL", "DSCR_FileContent", "SS.PowerShell", "PowerShellGet" -Tag "Dsc", "json" -Repository $PSGalleryName
        foreach ($item in $res) {
            $resWithEitherExpectedTag | Should -Contain $item.Name
        }
    }

    It "find all resources with specified tag given Tag property" {
        $foundTestModule = $False
        $foundTestScript = $False
        $tagToFind = "Tag2"
        $res = Find-PSResource -Tag $tagToFind -Repository $PSGalleryName
        foreach ($item in $res) {
            $item.Tags -contains $tagToFind | Should -Be $True

            if ($item.Name -eq $testModuleName)
            {
                $foundTestModule = $True
            }

            if ($item.Name -eq $testScriptName)
            {
                $foundTestScript = $True
            }
        }

        $foundTestModule | Should -Be $True
        $foundTestScript | Should -Be $True
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

    # # Skip test for now because it takes too run (132.24 sec)
    # It "find resource given CommandName (CommandNameParameterSet)" -Skip {
    #     $res = Find-PSResource -CommandName $commandName -Repository $PSGalleryName
    #     foreach ($item in $res) {
    #         $item.Name | Should -Be $commandName
    #         $item.ParentResource.Includes.Command | Should -Contain $commandName
    #     }
    # }

    It "find resource given CommandName and ModuleName (CommandNameParameterSet)" {
        $res = Find-PSResource -CommandName $commandName -ModuleName $parentModuleName -Repository $PSGalleryName
        $res.Name | Should -Be $commandName
        $res.ParentResource.Name | Should -Be $parentModuleName
        $res.ParentResource.Includes.Command | Should -Contain $commandName
    }

    # Skip test for now because it takes too long to run (> 60 sec)
    # It "find resource given DSCResourceName (DSCResourceNameParameterSet)" -Skip {
    #     $res = Find-PSResource -DscResourceName $dscResourceName -Repository $PSGalleryName
    #     foreach ($item in $res) {
    #         $item.Name | Should -Be $dscResourceName
    #         $item.ParentResource.Includes.DscResource | Should -Contain $dscResourceName
    #     }
    # }

    It "find resource given DscResourceName and ModuleName (DSCResourceNameParameterSet)" {
        $res = Find-PSResource -DscResourceName $dscResourceName -ModuleName $parentModuleName -Repository $PSGalleryName
        $res.Name | Should -Be $dscResourceName
        $res.ParentResource.Name | Should -Be $parentModuleName
        $res.ParentResource.Includes.DscResource | Should -Contain $dscResourceName
    }
}