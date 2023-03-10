# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test HTTP Find-PSResource for V2 Server Protocol' {

    BeforeAll{
        $NuGetGalleryName = Get-NuGetGalleryName
        $testModuleName = "test_module"
        Get-NewPSResourceRepositoryFile
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "find resource given specific Name, Version null" {
        # FindName()
        $res = Find-PSResource -Name $testModuleName -Repository $NuGetGalleryName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0"
    }

    It "should not find resource given nonexistant Name" {
        $res = Find-PSResource -Name NonExistantModule -Repository $NuGetGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
        $res | Should -BeNullOrEmpty
    }

    It "find resource(s) given wildcard Name" {
        # FindNameGlobbing
        $wildcardName = "test_module*"
        $res = Find-PSResource -Name $wildcardName -Repository $NuGetGalleryName
        $res.Count | Should -BeGreaterThan 1
        foreach ($item in $res)
        {
            $item.Name | Should -BeLike $wildcardName
        }
    }

    $testCases2 = @{Version="[5.0.0.0]";           ExpectedVersions=@("5.0.0");                                  Reason="validate version, exact match"},
                  @{Version="5.0.0.0";             ExpectedVersions=@("5.0.0");                                  Reason="validate version, exact match without bracket syntax"},
                  @{Version="[1.0.0.0, 5.0.0.0]";  ExpectedVersions=@("1.0.0", "3.0.0", "5.0.0");            Reason="validate version, exact range inclusive"},
                  @{Version="(1.0.0.0, 5.0.0.0)";  ExpectedVersions=@("3.0.0");                                  Reason="validate version, exact range exclusive"},
                  @{Version="(1.0.0.0,)";          ExpectedVersions=@("3.0.0", "5.0.0");                       Reason="validate version, minimum version exclusive"},
                  @{Version="[1.0.0.0,)";          ExpectedVersions=@("1.0.0", "3.0.0", "5.0.0");            Reason="validate version, minimum version inclusive"},
                  @{Version="(,3.0.0.0)";          ExpectedVersions=@("1.0.0");                                  Reason="validate version, maximum version exclusive"},
                  @{Version="(,3.0.0.0]";          ExpectedVersions=@("1.0.0", "3.0.0");                       Reason="validate version, maximum version inclusive"},
                  @{Version="[1.0.0.0, 5.0.0.0)";  ExpectedVersions=@("1.0.0", "3.0.0");                       Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
                  @{Version="(1.0.0.0, 5.0.0.0]";  ExpectedVersions=@("3.0.0", "5.0.0");                       Reason="validate version, mixed exclusive minimum and inclusive maximum version"}

    It "find resource when given Name to <Reason> <Version>" -TestCases $testCases2{
        # FindVersionGlobbing()
        param($Version, $ExpectedVersions)
        $res = Find-PSResource -Name $testModuleName -Version $Version -Repository $NuGetGalleryName
        foreach ($item in $res) {
            $item.Name | Should -Be $testModuleName
            $ExpectedVersions | Should -Contain $item.Version
        }
    }

    It "find all versions of resource when given specific Name, Version not null --> '*'" {
        # FindVersionGlobbing()
        $res = Find-PSResource -Name $testModuleName -Version "*" -Repository $NuGetGalleryName
        $res | ForEach-Object {
            $_.Name | Should -Be $testModuleName
        }

        $res.Count | Should -BeGreaterOrEqual 1
    }

    It "find resource with latest (including prerelease) version given Prerelease parameter" {
        # FindName()
        # test_module resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Find-PSResource -Name $testModuleName -Repository $NuGetGalleryName
        $res.Version | Should -Be "5.0.0"

        $resPrerelease = Find-PSResource -Name $testModuleName -Prerelease -Repository $NuGetGalleryName
        $resPrerelease.Version | Should -Be "5.2.5"
        $resPrerelease.Prerelease | Should -Be "alpha001"
    }

    It "find resources, including Prerelease version resources, when given Prerelease parameter" {
        # FindVersionGlobbing()
        $resWithoutPrerelease = Find-PSResource -Name $testModuleName -Version "*" -Repository $NuGetGalleryName
        $resWithPrerelease = Find-PSResource -Name $testModuleName -Version "*" -Repository $NuGetGalleryName
        $resWithPrerelease.Count | Should -BeGreaterOrEqual $resWithoutPrerelease.Count
    }

    # It "find resource and its dependency resources with IncludeDependencies parameter" {
    #     # FindName() with deps
    #     $resWithoutDependencies = Find-PSResource -Name "TestModuleWithDependencyE" -Repository $NuGetGalleryName
    #     $resWithoutDependencies.Count | Should -Be 1
    #     $resWithoutDependencies.Name | Should -Be "TestModuleWithDependencyE"

    #     # TestModuleWithDependencyE has the following dependencies:
    #     # TestModuleWithDependencyC <= 1.0.0.0
    #     #    TestModuleWithDependencyB >= 1.0.0.0
    #     #    TestModuleWithDependencyD <= 1.0.0.0

    #     $resWithDependencies = Find-PSResource -Name "TestModuleWithDependencyE" -IncludeDependencies -Repository $NuGetGalleryName
    #     $resWithDependencies.Count | Should -BeGreaterThan $resWithoutDependencies.Count

    #     $foundParentPkgE = $false
    #     $foundDepB = $false
    #     $foundDepBCorrectVersion = $false
    #     $foundDepC = $false
    #     $foundDepCCorrectVersion = $false
    #     $foundDepD = $false
    #     $foundDepDCorrectVersion = $false
    #     foreach ($pkg in $resWithDependencies)
    #     {
    #         if ($pkg.Name -eq "TestModuleWithDependencyE")
    #         {
    #             $foundParentPkgE = $true
    #         }
    #         elseif ($pkg.Name -eq "TestModuleWithDependencyC")
    #         {
    #             $foundDepC = $true
    #             $foundDepCCorrectVersion = [System.Version]$pkg.Version -le [System.Version]"1.0.0.0"
    #         }
    #         elseif ($pkg.Name -eq "TestModuleWithDependencyB")
    #         {
    #             $foundDepB = $true
    #             $foundDepBCorrectVersion = [System.Version]$pkg.Version -ge [System.Version]"3.0.0.0"
    #         }
    #         elseif ($pkg.Name -eq "TestModuleWithDependencyD")
    #         {
    #             $foundDepD = $true
    #             $foundDepDCorrectVersion = [System.Version]$pkg.Version -le [System.Version]"1.0.0.0"
    #         }
    #     }

    #     $foundParentPkgE | Should -Be $true
    #     $foundDepC | Should -Be $true
    #     $foundDepCCorrectVersion | Should -Be $true
    #     $foundDepB | Should -Be $true
    #     $foundDepBCorrectVersion | Should -Be $true
    #     $foundDepD | Should -Be $true
    #     $foundDepDCorrectVersion | Should -Be $true
    # }

    # It "find resources only with Tag parameter" {
    #     $resWithEitherExpectedTag = @("NetworkingDsc", "DSCR_FileContent", "SS.PowerShell")
    #     $res = Find-PSResource -Name "NetworkingDsc", "HPCMSL", "DSCR_FileContent", "SS.PowerShell", "PowerShellGet" -Tag "Dsc", "json" -Repository $NuGetGalleryName
    #     foreach ($item in $res) {
    #         $resWithEitherExpectedTag | Should -Contain $item.Name
    #     }
    # }

    It "find resource that satisfies given Name and Tag property (single tag)" {
        # FindNameWithTag()
        $requiredTag = "test"
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTag -Repository $NuGetGalleryName
        $res.Name | Should -Be $testModuleName
        $res.Tags | Should -Contain $requiredTag
    }

    It "should not find resource if Name and Tag are not both satisfied (single tag)" {
        # FindNameWithTag
        $requiredTag = "Windows" # tag "windows" is not present for test_module package
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTag -Repository $NuGetGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    It "find resource that satisfies given Name and Tag property (multiple tags)" {
        # FindNameWithTag()
        $requiredTags = @("test", "Tag2")
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTags -Repository $NuGetGalleryName
        $res.Name | Should -Be $testModuleName
        $res.Tags | Should -Contain $requiredTags[0]
        $res.Tags | Should -Contain $requiredTags[1]
    }

    It "should not find resource if Name and Tag are not both satisfied (multiple tag)" {
        # FindNameWithTag
        $requiredTags = @("test", "Windows") # tag "windows" is not present for test_module package
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTags -Repository $NuGetGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    It "find all resources that satisfy Name pattern and have specified Tag (single tag)" {
        # FindNameGlobbingWithTag()
        $requiredTag = "test"
        $nameWithWildcard = "test_module*"
        $res = Find-PSResource -Name $nameWithWildcard -Tag $requiredTag -Repository $NuGetGalleryName
        $res.Count | Should -BeGreaterThan 1
        foreach ($pkg in $res)
        {
            $pkg.Name | Should -BeLike $nameWithWildcard
            $pkg.Tags | Should -Contain $requiredTag
        }
    }

    It "should not find resources if both Name pattern and Tags are not satisfied (single tag)" {
        # FindNameGlobbingWithTag()
        $requiredTag = "windows" # tag "windows" is not present for test_module package
        $res = Find-PSResource -Name "test_module*" -Tag $requiredTag -Repository $NuGetGalleryName
        $res | Should -BeNullOrEmpty
    }

    It "find all resources that satisfy Name pattern and have specified Tag (multiple tags)" {
        # FindNameGlobbingWithTag()
        $requiredTags = @("test", "Tag2")
        $nameWithWildcard = "test_module*"
        $res = Find-PSResource -Name $nameWithWildcard -Tag $requiredTags -Repository $NuGetGalleryName
        $res.Count | Should -BeGreaterThan 1
        foreach ($pkg in $res)
        {
            $pkg.Name | Should -BeLike $nameWithWildcard
            $pkg.Tags | Should -Contain $requiredTags[0]
            $pkg.Tags | Should -Contain $requiredTags[1]
        }
    }

    It "should not find resources if both Name pattern and Tags are not satisfied (multiple tags)" {
        # FindNameGlobbingWithTag() # tag "windows" is not present for test_module package
        $requiredTags = @("test", "windows")
        $res = Find-PSResource -Name "test_module*" -Tag $requiredTags -Repository $NuGetGalleryName
        $res | Should -BeNullOrEmpty
    }

    It "find resource that satisfies given Name, Version and Tag property (single tag)" {
        # FindVersionWithTag()
        $requiredTag = "test"
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0.0" -Tag $requiredTag -Repository $NuGetGalleryName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0"
        $res.Tags | Should -Contain $requiredTag
    }

    It "should not find resource if Name, Version and Tag property are not all satisfied (single tag)" {
        # FindVersionWithTag()
        $requiredTag = "windows" # tag "windows" is not present for test_module package
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0.0" -Tag $requiredTag -Repository $NuGetGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindVersionFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    It "find resource that satisfies given Name, Version and Tag property (multiple tags)" {
        # FindVersionWithTag()
        $requiredTags = @("test", "Tag2")
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0.0" -Tag $requiredTags -Repository $NuGetGalleryName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0"
        $res.Tags | Should -Contain $requiredTags[0]
        $res.Tags | Should -Contain $requiredTags[1]

    }

    It "should not find resource if Name, Version and Tag property are not all satisfied (multiple tags)" {
        # FindVersionWithTag()
        $requiredTags = @("test", "windows")
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0.0" -Tag $requiredTags -Repository $NuGetGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindVersionFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    # It "find all resources with specified tag given Tag property" {
    #     # FindTag()
    #     $foundTestModule = $False
    #     $foundTestScript = $False
    #     $tagToFind = "Tag2"
    #     $res = Find-PSResource -Tag $tagToFind -Repository $NuGetGalleryName
    #     foreach ($item in $res) {
    #         $item.Tags -contains $tagToFind | Should -Be $True

    #         if ($item.Name -eq $testModuleName)
    #         {
    #             $foundTestModule = $True
    #         }

    #         if ($item.Name -eq $testScriptName)
    #         {
    #             $foundTestScript = $True
    #         }
    #     }

    #     $foundTestModule | Should -Be $True
    #     $foundTestScript | Should -Be $True
    # }

    It "should not find resource given CommandName" {
        $res = Find-PSResource -CommandName "command" -Repository $NuGetGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindCommandOrDSCResourceFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    It "should not find resource given DscResourceName" {
        $res = Find-PSResource -DscResourceName "dscResource" -Repository $NuGetGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindCommandOrDSCResourceFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    It "should not find all resources given Name '*'" {
        $res = Find-PSResource -Name "*" -Repository $NuGetGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindAllFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"

    }
}
