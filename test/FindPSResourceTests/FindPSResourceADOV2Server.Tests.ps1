# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"

Describe 'Test HTTP Find-PSResource for ADO V2 Server Protocol' -tags 'CI' {

    BeforeAll{
        $testModuleName = "test_local_mod"
        $ADOV2RepoName = "PSGetTestingPublicFeed"
        $ADOV2RepoUri = "https://pkgs.dev.azure.com/powershell/PowerShell/_packaging/psresourceget-public-test-ci/nuget/v2"
        Get-NewPSResourceRepositoryFile
        Register-PSResourceRepository -Name $ADOV2RepoName -Uri $ADOV2RepoUri
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Find resource given specific Name, Version null" {
        $res = Find-PSResource -Name $testModuleName -Repository $ADOV2RepoName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0"
    }

    It "Should not find resource given nonexistant Name" {
        $res = Find-PSResource -Name NonExistantModule -Repository $ADOV2RepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ResourceNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
        $res | Should -BeNullOrEmpty
    }

    It "Find resource(s) given wildcard Name" {
        # FindNameGlobbing
        $foundScript = $False
        $res = Find-PSResource -Name "test_*" -Repository $ADOV2RepoName
        $res.Count | Should -BeGreaterThan 1
    }

    $testCases2 = @{Version="[5.0.0]";           ExpectedVersions=@("5.0.0");                                  Reason="validate version, exact match"},
                  @{Version="5.0.0";             ExpectedVersions=@("5.0.0");                                  Reason="validate version, exact match without bracket syntax"},
                  @{Version="[1.0.0, 5.0.0]";    ExpectedVersions=@("1.0.0", "3.0.0", "5.0.0");            Reason="validate version, exact range inclusive"},
                  @{Version="(1.0.0, 5.0.0)";    ExpectedVersions=@("3.0.0");                                  Reason="validate version, exact range exclusive"},
                  @{Version="(1.0.0,)";          ExpectedVersions=@("3.0.0", "5.0.0");                       Reason="validate version, minimum version exclusive"},
                  @{Version="[1.0.0,)";          ExpectedVersions=@("1.0.0", "3.0.0", "5.0.0");            Reason="validate version, minimum version inclusive"},
                  @{Version="(,3.0.0)";          ExpectedVersions=@("1.0.0");                                  Reason="validate version, maximum version exclusive"},
                  @{Version="(,3.0.0]";          ExpectedVersions=@("1.0.0", "3.0.0");                       Reason="validate version, maximum version inclusive"},
                  @{Version="[1.0.0, 5.0.0)";    ExpectedVersions=@("1.0.0", "3.0.0");                       Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
                  @{Version="(1.0.0, 5.0.0]";    ExpectedVersions=@("3.0.0", "5.0.0");                       Reason="validate version, mixed exclusive minimum and inclusive maximum version"}

    It "Find resource when given Name to <Reason> <Version>" -TestCases $testCases2{
        # FindVersionGlobbing()
        param($Version, $ExpectedVersions)
        $res = Find-PSResource -Name $testModuleName -Version $Version -Repository $ADOV2RepoName
        $res | Should -Not -BeNullOrEmpty
        foreach ($item in $res) {
            $item.Name | Should -Be $testModuleName
            $ExpectedVersions | Should -Contain $item.Version
        }
    }

    It "Find all versions of resource when given specific Name, Version not null --> '*'" {
        # FindVersionGlobbing()
        $res = Find-PSResource -Name $testModuleName -Version "*" -Repository $ADOV2RepoName
        $res | ForEach-Object {
            $_.Name | Should -Be $testModuleName
        }

        $res.Count | Should -BeGreaterOrEqual 1
    }

    It "Find resource with latest (including prerelease) version given Prerelease parameter" {
        # FindName()
        # test_module resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Find-PSResource -Name $testModuleName -Repository $ADOV2RepoName
        $res.Version | Should -Be "5.0.0"

        $resPrerelease = Find-PSResource -Name $testModuleName -Prerelease -Repository $ADOV2RepoName
        $resPrerelease.Version | Should -Be "5.2.5"
        $resPrerelease.Prerelease | Should -Be "alpha001"
    }

    It "Find resources, including Prerelease version resources, when given Prerelease parameter" {
        # FindVersionGlobbing()
        $resWithoutPrerelease = Find-PSResource -Name $testModuleName -Version "*" -Repository $ADOV2RepoName
        $resWithPrerelease = Find-PSResource -Name $testModuleName -Version "*" -Repository $ADOV2RepoName
        $resWithPrerelease.Count | Should -BeGreaterOrEqual $resWithoutPrerelease.Count
    }

<# LATER
    It "Find resource and its dependency resources with IncludeDependencies parameter" {
        # FindName() with deps
        $resWithoutDependencies = Find-PSResource -Name "TestModuleWithDependencyE" -Repository $ADOV2RepoName
        $resWithoutDependencies.Name | Should -Be "TestModuleWithDependencyE"
        $resWithoutDependencies | Should -HaveCount 1

        # TestModuleWithDependencyE has the following dependencies:
        # TestModuleWithDependencyC <= 1.0.0.0
        #    TestModuleWithDependencyB >= 1.0.0.0
        #    TestModuleWithDependencyD <= 1.0.0.0

        $resWithDependencies = Find-PSResource -Name "TestModuleWithDependencyE" -IncludeDependencies -Repository $ADOV2RepoName
        $resWithDependencies | Should -HaveCount 4

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
                $foundDepCCorrectVersion = [System.Version]$pkg.Version -le [System.Version]"1.0"
            }
            elseif ($pkg.Name -eq "TestModuleWithDependencyB")
            {
                $foundDepB = $true
                $foundDepBCorrectVersion = [System.Version]$pkg.Version -ge [System.Version]"1.0"
            }
            elseif ($pkg.Name -eq "TestModuleWithDependencyD")
            {
                $foundDepD = $true
                $foundDepDCorrectVersion = [System.Version]$pkg.Version -le [System.Version]"1.0"
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
        # FindName() script
        $resScript = Find-PSResource -Name $testScriptName -Repository $ADOV2RepoName
        $resScript.Name | Should -Be $testScriptName
        $resScriptType = Out-String -InputObject $resScript.Type
        $resScriptType.Replace(",", " ").Split() | Should -Contain "Script"

        $resModule = Find-PSResource -Name $testModuleName -Repository $ADOV2RepoName
        $resModule.Name | Should -Be $testModuleName
        $resModuleType = Out-String -InputObject $resModule.Type
        $resModuleType.Replace(",", " ").Split() | Should -Contain "Module"
    }

    It "find resource of Type Script from PSGallery, when Type Script specified" {
        # FindName() Type script
        $resScript = Find-PSResource -Name $testScriptName -Repository $ADOV2RepoName -Type "Script"
        $resScript.Name | Should -Be $testScriptName
        $resScriptType = Out-String -InputObject $resScript.Type
        $resScriptType.Replace(",", " ").Split() | Should -Contain "Script"
    }
#>


    It "Find all resources of Type Module when Type parameter set is used" {
        $res = Find-PSResource -Name "test*" -Type Module -Repository $ADOV2RepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameGlobbingNotSupportedForRepo,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "Find resource that satisfies given Name and Tag property (single tag)" {
        # FindNameWithTag()
        $requiredTag = "Test"
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTag -Repository $ADOV2RepoName
        $res.Name | Should -Be $testModuleName
        $res.Tags | Should -Contain $requiredTag
    }

    It "Should not find resource if Name and Tag are not both satisfied (single tag)" {
        # FindNameWithTag
        $requiredTag = "Windows" # tag "windows" is not present for test_module package
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTag -Repository $ADOV2RepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "Find resource that satisfies given Name and Tag property (multiple tags)" {
        # FindNameWithTag()
        $requiredTags = @("Test", "Tag2")
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTags -Repository $ADOV2RepoName
        $res.Name | Should -Be $testModuleName
        $res.Tags | Should -Contain $requiredTags[0]
        $res.Tags | Should -Contain $requiredTags[1]
    }

    It "Should not find resource if Name and Tag are not both satisfied (multiple tag)" {
        # FindNameWithTag
        $requiredTags = @("test", "Windows") # tag "windows" is not present for test_module package
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTags -Repository $ADOV2RepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "Find all resources that satisfy Name pattern and have specified Tag (single tag)" {
        # FindNameGlobbingWithTag()
        $requiredTag = "test"
        $nameWithWildcard = "test_module*"
        $res = Find-PSResource -Name $nameWithWildcard -Tag $requiredTag -Repository $ADOV2RepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameGlobbingAndTagFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"

    }

    It "Should not find resources if both Name pattern and Tags are not satisfied (multiple tags)" {
        # FindNameGlobbingWithTag() # tag "windows" is not present for test_module package
        $requiredTags = @("Test", "windows")
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTags -Repository $ADOV2RepoName -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
    }

    It "Find resource that satisfies given Name, Version and Tag property (single tag)" {
        # FindVersionWithTag()
        $requiredTag = "Test"
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0" -Tag $requiredTag -Repository $ADOV2RepoName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0"
        $res.Tags | Should -Contain $requiredTag
    }

    It "Should not find resource if Name, Version and Tag property are not all satisfied (single tag)" {
        # FindVersionWithTag()
        $requiredTag = "windows" # tag "windows" is not present for test_module package
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0" -Tag $requiredTag -Repository $ADOV2RepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "Find resource that satisfies given Name, Version and Tag property (multiple tags)" {
        # FindVersionWithTag()
        $requiredTags = @("Test", "Tag2")
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0" -Tag $requiredTags -Repository $ADOV2RepoName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0"
        $res.Tags | Should -Contain $requiredTags[0]
        $res.Tags | Should -Contain $requiredTags[1]

    }

    It "Should not find resource if Name, Version and Tag property are not all satisfied (multiple tags)" {
        # FindVersionWithTag()
        $requiredTags = @("test", "windows")
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0" -Tag $requiredTags -Repository $ADOV2RepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }
}
