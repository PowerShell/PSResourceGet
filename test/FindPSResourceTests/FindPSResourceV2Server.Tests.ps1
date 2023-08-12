# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"

Describe 'Test HTTP Find-PSResource for V2 Server Protocol' -tags 'CI' {

    BeforeAll{
        $PSGalleryName = Get-PSGalleryName
        $testModuleName = "test_module"
        $testScriptName = "test_script"
        $commandName = "Get-TargetResource"
        $dscResourceName = "SystemLocale"
        $parentModuleName = "SystemLocaleDsc"
        Get-NewPSResourceRepositoryFile
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "find resource given specific Name, Version null" {
        # FindName()
        $res = Find-PSResource -Name $testModuleName -Repository $PSGalleryName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0.0"
    }

    It "should not find resource given nonexistant Name" {
        $res = Find-PSResource -Name NonExistantModule -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
        $res | Should -BeNullOrEmpty
    }

    It "find resource(s) given wildcard Name" {
        # FindNameGlobbing
        $foundScript = $False
        $res = Find-PSResource -Name "test_*" -Repository $PSGalleryName
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
        # FindVersionGlobbing()
        param($Version, $ExpectedVersions)
        $res = Find-PSResource -Name $testModuleName -Version $Version -Repository $PSGalleryName
        $res | Should -Not -BeNullOrEmpty
        foreach ($item in $res) {
            $item.Name | Should -Be $testModuleName
            $ExpectedVersions | Should -Contain $item.Version
        }
    }

    It "find all versions of resource when given specific Name, Version not null --> '*'" {
        # FindVersionGlobbing()
        $res = Find-PSResource -Name $testModuleName -Version "*" -Repository $PSGalleryName
        $res | ForEach-Object {
            $_.Name | Should -Be $testModuleName
        }

        $res.Count | Should -BeGreaterOrEqual 1
    }

    It "find resource with latest (including prerelease) version given Prerelease parameter" {
        # FindName()
        # test_module resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Find-PSResource -Name $testModuleName -Repository $PSGalleryName
        $res.Version | Should -Be "5.0.0.0"

        $resPrerelease = Find-PSResource -Name $testModuleName -Prerelease -Repository $PSGalleryName
        $resPrerelease.Version | Should -Be "5.2.5"
        $resPrerelease.Prerelease | Should -Be "alpha001"
    }

    It "find resources, including Prerelease version resources, when given Prerelease parameter" {
        # FindVersionGlobbing()
        $resWithoutPrerelease = Find-PSResource -Name $testModuleName -Version "*" -Repository $PSGalleryName
        $resWithPrerelease = Find-PSResource -Name $testModuleName -Version "*" -Repository $PSGalleryName
        $resWithPrerelease.Count | Should -BeGreaterOrEqual $resWithoutPrerelease.Count
    }

    It "find resource and its dependency resources with IncludeDependencies parameter" {
        # FindName() with deps
        $resWithoutDependencies = Find-PSResource -Name "TestModuleWithDependencyE" -Repository $PSGalleryName
        $resWithoutDependencies.Name | Should -Be "TestModuleWithDependencyE"
        $resWithoutDependencies | Should -HaveCount 1

        # TestModuleWithDependencyE has the following dependencies:
        # TestModuleWithDependencyC <= 1.0.0.0
        #    TestModuleWithDependencyB >= 1.0.0.0
        #    TestModuleWithDependencyD <= 1.0.0.0

        $resWithDependencies = Find-PSResource -Name "TestModuleWithDependencyE" -IncludeDependencies -Repository $PSGalleryName
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
        # FindName() Type script
        $resScript = Find-PSResource -Name $testScriptName -Repository $PSGalleryName -Type "Script"
        $resScript.Name | Should -Be $testScriptName
        $resScriptType = Out-String -InputObject $resScript.Type
        $resScriptType.Replace(",", " ").Split() | Should -Contain "Script"
    }

    It "find all resources of Type Module when Type parameter set is used" {
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

    # It "find resources only with Tag parameter" {
    #     $resWithEitherExpectedTag = @("NetworkingDsc", "DSCR_FileContent", "SS.PowerShell")
    #     $res = Find-PSResource -Name "NetworkingDsc", "HPCMSL", "DSCR_FileContent", "SS.PowerShell", "PSResourceGet" -Tag "Dsc", "json" -Repository $PSGalleryName
    #     foreach ($item in $res) {
    #         $resWithEitherExpectedTag | Should -Contain $item.Name
    #     }
    # }

    It "find resource that satisfies given Name and Tag property (single tag)" {
        # FindNameWithTag()
        $requiredTag = "test"
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTag -Repository $PSGalleryName
        $res.Name | Should -Be $testModuleName
        $res.Tags | Should -Contain $requiredTag
    }

    It "should not find resource if Name and Tag are not both satisfied (single tag)" {
        # FindNameWithTag
        $requiredTag = "Windows" # tag "windows" is not present for test_module package
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTag -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "find resource that satisfies given Name and Tag property (multiple tags)" {
        # FindNameWithTag()
        $requiredTags = @("test", "Tag2")
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTags -Repository $PSGalleryName
        $res.Name | Should -Be $testModuleName
        $res.Tags | Should -Contain $requiredTags[0]
        $res.Tags | Should -Contain $requiredTags[1]
    }

    It "should not find resource if Name and Tag are not both satisfied (multiple tag)" {
        # FindNameWithTag
        $requiredTags = @("test", "Windows") # tag "windows" is not present for test_module package
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTags -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "find all resources that satisfy Name pattern and have specified Tag (single tag)" {
        # FindNameGlobbingWithTag()
        $requiredTag = "test"
        $nameWithWildcard = "test_module*"
        $res = Find-PSResource -Name $nameWithWildcard -Tag $requiredTag -Repository $PSGalleryName
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
        $res = Find-PSResource -Name "test_module*" -Tag $requiredTag -Repository $PSGalleryName
        $res | Should -BeNullOrEmpty
    }

    It "find all resources that satisfy Name pattern and have specified Tag (multiple tags)" {
        # FindNameGlobbingWithTag()
        $requiredTags = @("test", "Tag2")
        $nameWithWildcard = "test_module*"
        $res = Find-PSResource -Name $nameWithWildcard -Tag $requiredTags -Repository $PSGalleryName
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
        $res = Find-PSResource -Name "test_module*" -Tag $requiredTags -Repository $PSGalleryName
        $res | Should -BeNullOrEmpty
    }

    It "find resource that satisfies given Name, Version and Tag property (single tag)" {
        # FindVersionWithTag()
        $requiredTag = "test"
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0.0" -Tag $requiredTag -Repository $PSGalleryName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0.0"
        $res.Tags | Should -Contain $requiredTag
    }

    It "should not find resource if Name, Version and Tag property are not all satisfied (single tag)" {
        # FindVersionWithTag()
        $requiredTag = "windows" # tag "windows" is not present for test_module package
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0.0" -Tag $requiredTag -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "find resource that satisfies given Name, Version and Tag property (multiple tags)" {
        # FindVersionWithTag()
        $requiredTags = @("test", "Tag2")
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0.0" -Tag $requiredTags -Repository $PSGalleryName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0.0"
        $res.Tags | Should -Contain $requiredTags[0]
        $res.Tags | Should -Contain $requiredTags[1]

    }

    It "should not find resource if Name, Version and Tag property are not all satisfied (multiple tags)" {
        # FindVersionWithTag()
        $requiredTags = @("test", "windows")
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0.0" -Tag $requiredTags -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    # It "find all resources with specified tag given Tag property" {
    #     # FindTag()
    #     $foundTestModule = $False
    #     $foundTestScript = $False
    #     $tagToFind = "Tag2"
    #     $res = Find-PSResource -Tag $tagToFind -Repository $PSGalleryName
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

    It "find resource given CommandName" {
        $res = Find-PSResource -CommandName $commandName -Repository $PSGalleryName
        $res | Should -Not -BeNullOrEmpty
        foreach ($item in $res) {
            $item.Names | Should -Be $commandName    
            $item.ParentResource.Includes.Command | Should -Contain $commandName
        }
    }

    It "find resource given DscResourceName" {
        $res = Find-PSResource -DscResourceName $dscResourceName -Repository $PSGalleryName
        $res | Should -Not -BeNullOrEmpty
        foreach ($item in $res) {
            $item.Names | Should -Be $dscResourceName    
            $item.ParentResource.Includes.DscResource | Should -Contain $dscResourceName
        }
    }
    
    It "find resource, but only show listed versions" {
        # testmodule99 version 1.0.0-beta1 is unlisted
        $res = Find-PSResource -Name "testmodule99" -Repository $PSGalleryName
        $res | Should -Not -BeNullOrEmpty
        foreach ($item in $res) {
            $item.Version.ToString() + $item.Prerelease | Should -Not -Be "1.0.0-beta1"
        }
    }

    It "find all resources within a version range, including prereleases" {
        $res = Find-PSResource -Name "PSReadLine" -Version "(2.0,2.1)" -Prerelease -Repository $PSGalleryName
        $res | Should -Not -BeNullOrEmpty
        $res.Count | Should -BeGreaterOrEqual 7 
    }

    It "find a specific version using NuGet versioning bracket syntax" {
        $res = Find-PSResource -Name $testModuleName -Version "[3.0.0,3.0.0]" -Repository $PSGalleryName
        $res | Should -Not -BeNullOrEmpty
        $res.Version | Should -Be "3.0.0.0"
    }

    It "not find resource and error handle when repository's ApiVersion is ApiVersion.unknown" {
        Register-PSResourceRepository -Name "UnknownTypeRepo" -Uri "https://org.MyCompany.com/repository/shared-feed/" -Trusted
        $repo = Get-PSResourceRepository -Name "UnknownTypeRepo"
        $repo.ApiVersion | Should -Be "unknown"

        $res = Find-PSResource -Name "MyPackage" -Repository "UnknownTypeRepo" -ErrorAction SilentlyContinue -ErrorVariable err
        $err | Should -Not -BeNullOrEmpty
        $err[0].FullyQualifiedErrorId | Should -BeExactly "RepositoryApiVersionUnknown,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
        $res | Should -BeNullOrEmpty

        Unregister-PSResourceRepository -Name "UnknownTypeRepo"
    }

    It "find a module that does not exist in the highest priority repository, but does exist in a lower priority repository" {
        $res = Find-PSResource -Name "NewpsGetTestModule" -ErrorVariable err
        $res | Should -Not -BeNullOrEmpty
        $res.Version | Should -Be "2.1.0"
        $err.Count | Should -Be 0
    }
}

Describe 'Test HTTP Find-PSResource for V2 Server Protocol' -tags 'ManualValidationOnly' {

    BeforeAll{
        $PSGalleryName = Get-PSGalleryName
        $testModuleName = "MicrosoftPowerBIMgmt"
        Get-NewPSResourceRepositoryFile
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }
    It "find resource given CommandName" {
        $res = Find-PSResource -Name $testModuleName -Repository $PSGalleryName -Type Module

        $res.Name | Should -Be $testModuleName
    }
}
