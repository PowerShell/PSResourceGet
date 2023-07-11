# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"

Describe 'Test Find-PSResource for local repositories' -tags 'CI' {

    BeforeAll{
        $localRepo = "psgettestlocal"
        $testModuleName = "test_local_mod"
        $testModuleName2 = "test_local_mod2"
        $commandName = "cmd1"
        $dscResourceName = "dsc1"
        $prereleaseLabel = ""
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos

        $localRepoUriAddress = Join-Path -Path $TestDrive -ChildPath "testdir"
        $tagsEscaped = @("'Test'", "'Tag2'", "'$cmdName'", "'$dscName'")
        $prereleaseLabel = "alpha001"

        New-TestModule -moduleName $testModuleName -repoName $localRepo -packageVersion "1.0.0" -prereleaseLabel "" -tags @()
        New-TestModule -moduleName $testModuleName -repoName $localRepo -packageVersion "3.0.0" -prereleaseLabel "" -tags @() -dscResourceToExport $dscResourceName -commandToExport $commandName
        New-TestModule -moduleName $testModuleName -repoName $localRepo -packageVersion "5.0.0" -prereleaseLabel "" -tags $tagsEscaped
        New-TestModule -moduleName $testModuleName -repoName $localRepo -packageVersion "5.2.5" -prereleaseLabel $prereleaseLabel -tags $tagsEscaped

        New-TestModule -moduleName $testModuleName2 -repoName $localRepo -packageVersion "5.0.0" -prereleaseLabel "" -tags $tagsEscaped
        New-TestModule -moduleName $testModuleName2 -repoName $localRepo -packageVersion "5.2.5" -prereleaseLabel $prereleaseLabel -tags $tagsEscaped
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "find resource given specific Name, Version null (module)" {
        # FindName()
        $res = Find-PSResource -Name $testModuleName -Repository $localRepo
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0"
    }

    It "find resource given Name, Version null (package containing nuspec only)" {
        # FindName()
        $pkgName = "test_nonpsresource"
        $requiredTag = "Tag1"
        Save-PSResource -Name $pkgName -Repository "NuGetGallery" -Path $localRepoUriAddress -AsNupkg
        $res = Find-PSResource -Name $pkgName -Repository $localRepo
        $res.Name | Should -Be $pkgName
        $res.Repository | Should -Be $localRepo
        $res.Tags | Should -Contain $requiredTag
    }

    It "should not find resource given nonexistant Name" {
        $res = Find-PSResource -Name NonExistantModule -Repository $localRepo -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
        $res | Should -BeNullOrEmpty
    }

    It "find resource(s) given wildcard Name" {
        # FindNameGlobbing
        $res = Find-PSResource -Name "test_local_*" -Repository $localRepo
        $res.Count | Should -BeGreaterThan 1
    }

    $testCases2 = @{Version="[5.0.0.0]";           ExpectedVersions=@("5.0.0");                              Reason="validate version, exact match"},
                  @{Version="5.0.0.0";             ExpectedVersions=@("5.0.0");                              Reason="validate version, exact match without bracket syntax"},
                  @{Version="[1.0.0.0, 5.0.0.0]";  ExpectedVersions=@("1.0.0", "3.0.0", "5.0.0");            Reason="validate version, exact range inclusive"},
                  @{Version="(1.0.0.0, 5.0.0.0)";  ExpectedVersions=@("3.0.0");                              Reason="validate version, exact range exclusive"},
                  @{Version="(1.0.0.0,)";          ExpectedVersions=@("3.0.0", "5.0.0");                     Reason="validate version, minimum version exclusive"},
                  @{Version="[1.0.0.0,)";          ExpectedVersions=@("1.0.0", "3.0.0", "5.0.0");            Reason="validate version, minimum version inclusive"},
                  @{Version="(,3.0.0.0)";          ExpectedVersions=@("1.0.0");                              Reason="validate version, maximum version exclusive"},
                  @{Version="(,3.0.0.0]";          ExpectedVersions=@("1.0.0", "3.0.0");                     Reason="validate version, maximum version inclusive"},
                  @{Version="[1.0.0.0, 5.0.0.0)";  ExpectedVersions=@("1.0.0", "3.0.0");                     Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
                  @{Version="(1.0.0.0, 5.0.0.0]";  ExpectedVersions=@("3.0.0", "5.0.0");                     Reason="validate version, mixed exclusive minimum and inclusive maximum version"}

    It "find resource when given Name to <Reason> <Version>" -TestCases $testCases2{
        # FindVersionGlobbing()
        param($Version, $ExpectedVersions)
        $res = Find-PSResource -Name $testModuleName -Version $Version -Repository $localRepo
        foreach ($item in $res) {
            $item.Name | Should -Be $testModuleName
            $ExpectedVersions | Should -Contain $item.Version
        }
    }

    It "find all versions of resource when given specific Name, Version not null --> '*'" {
        # FindVersionGlobbing()
        $res = Find-PSResource -Name $testModuleName -Version "*" -Repository $localRepo
        $res | ForEach-Object {
            $_.Name | Should -Be $testModuleName
        }

        $res.Count | Should -BeGreaterOrEqual 1
    }

    It "find resource with latest (including prerelease) version given Prerelease parameter" {
        # FindName()
        # test_module resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Find-PSResource -Name $testModuleName -Repository $localRepo
        $res.Version | Should -Be "5.0.0"

        $resPrerelease = Find-PSResource -Name $testModuleName -Prerelease -Repository $localRepo
        $resPrerelease.Version | Should -Be "5.2.5"
        $resPrerelease.Prerelease | Should -Be "alpha001"
    }

    It "find resources, including Prerelease version resources, when given Prerelease parameter" {
        # FindVersionGlobbing()
        $resWithoutPrerelease = Find-PSResource -Name $testModuleName -Version "*" -Repository $localRepo
        $resWithPrerelease = Find-PSResource -Name $testModuleName -Version "*" -Repository $localRepo
        $resWithPrerelease.Count | Should -BeGreaterOrEqual $resWithoutPrerelease.Count
    }

    It "find resource that satisfies given Name and Tag property (single tag)" {
        # FindNameWithTag()
        $requiredTag = "test"
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTag -Repository $localRepo
        $res.Name | Should -Be $testModuleName
        $res.Tags | Should -Contain $requiredTag
    }

    It "should not find resource if Name and Tag are not both satisfied (single tag)" {
        # FindNameWithTag
        $requiredTag = "Windows" # tag "windows" is not present for test_module package
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTag -Repository $localRepo -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
        $res | Should -BeNullOrEmpty
    }

    It "find resource that satisfies given Name and Tag property (multiple tags)" {
        # FindNameWithTag()
        $requiredTags = @("test", "Tag2")
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTags -Repository $localRepo
        $res.Name | Should -Be $testModuleName
        $res.Tags | Should -Contain $requiredTags[0]
        $res.Tags | Should -Contain $requiredTags[1]
    }

    It "find all resources that satisfy Name pattern and have specified Tag (single tag)" {
        # FindNameGlobbingWithTag()
        $requiredTag = "test"
        $nameWithWildcard = "test_local_mod*"

        $res = Find-PSResource -Name $nameWithWildcard -Tag $requiredTag -Repository $localRepo
        $res.Count | Should -BeGreaterThan 1
        foreach ($pkg in $res)
        {
            $pkg.Name | Should -BeLike $nameWithWildcard
            $pkg.Tags | Should -Contain "$requiredTag"
        }
    }

    It "should not find resources if both Name pattern and Tags are not satisfied (single tag)" {
        # FindNameGlobbingWithTag()
        $requiredTag = "windows" # tag "windows" is not present for test_module package
        $res = Find-PSResource -Name "test_module*" -Tag $requiredTag -Repository $localRepo
        $res | Should -BeNullOrEmpty
    }

    It "find all resources that satisfy Name pattern and have specified Tag (multiple tags)" {
        # FindNameGlobbingWithTag()
        $requiredTags = @("test", "Tag2")
        $nameWithWildcard = "test_local_mod*"

        $res = Find-PSResource -Name $nameWithWildcard -Tag $requiredTags -Repository $localRepo
        $res.Count | Should -BeGreaterThan 1
        foreach ($pkg in $res)
        {
            $pkg.Name | Should -BeLike $nameWithWildcard
            $pkg.Tags | Should -Contain $requiredTags[0]
            $pkg.Tags | Should -Contain $requiredTags[1]
        }
    }

    It "find resource that satisfies given Name, Version and Tag property (single tag)" {
        # FindVersionWithTag()
        $requiredTag = "test"
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0.0" -Tag $requiredTag -Repository $localRepo
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0"
        $res.Tags | Should -Contain $requiredTag
    }

    It "should not find resource if Name, Version and Tag property are not all satisfied (single tag)" {
        # FindVersionWithTag()
        $requiredTag = "windows" # tag "windows" is not present for test_module package
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0.0" -Tag $requiredTag -Repository $localRepo -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindVersionFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
        $res | Should -BeNullOrEmpty
    }

    It "find resource that satisfies given Name, Version and Tag property (multiple tags)" {
        # FindVersionWithTag()
        $requiredTags = @("test", "Tag2")
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0.0" -Tag $requiredTags -Repository $localRepo
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0"
        $res.Tags | Should -Contain $requiredTags[0]
        $res.Tags | Should -Contain $requiredTags[1]
    }

    It "find scripts given -Type parameter" {
        Get-ScriptResourcePublishedToLocalRepoTestDrive

        $res = Find-PSResource -Type Script -Repository $localRepo
        $res | Should -Not -BeNullOrEmpty
        $res.Count | Should -BeGreaterOrEqual 1
        foreach ($script in $res) {
            $item.Type | Should -Be "Script"
        }
    }
    
    It "find modules given -Type parameter" {
        Get-ScriptResourcePublishedToLocalRepoTestDrive

        $res = Find-PSResource -Type Module -Repository $localRepo
        $res | Should -Not -BeNullOrEmpty
        $res.Count | Should -BeGreaterOrEqual 1
        foreach ($script in $res) {
            $item.Type | Should -Be "Module"
        }
    }

    It "find resource given CommandName" -Pending {
        $res = Find-PSResource -CommandName $commandName -Repository $localRepo
        $res | Should -Not -BeNullOrEmpty
        foreach ($item in $res) {
            $item.Names | Should -Be $commandName
            $item.ParentResource.Includes.Command | Should -Contain $commandName
        }
    }

    It "find resource given DscResourceName" -Pending {
        $res = Find-PSResource -DscResourceName $dscResourceName -Repository $localRepo
        $res | Should -Not -BeNullOrEmpty
        foreach ($item in $res) {
            $item.Names | Should -Be $dscResourceName    
            $item.ParentResource.Includes.DscResource | Should -Contain $dscResourceName
        }
    }
}
