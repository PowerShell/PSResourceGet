# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
<##
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

Describe 'Test HTTP Find-PSResource for ADO Server Protocol' -tags 'CI' {

    BeforeAll{
        $testModuleName = "test_local_mod"
        $ADORepoName = "PSGetTestingPublicFeed"
        $ADORepoUri = "https://pkgs.dev.azure.com/powershell/PowerShell/_packaging/psresourceget-public-test-ci/nuget/v3/index.json"
        Get-NewPSResourceRepositoryFile
        Register-PSResourceRepository -Name $ADORepoName -Uri $ADORepoUri
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "find resource given specific Name, Version null" {
        # FindName()
        $res = Find-PSResource -Name $testModuleName -Repository $ADORepoName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0"
    }

    It "should not find resource given nonexistant Name" {
        # FindName()
        $res = Find-PSResource -Name NonExistantModule -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
        $res | Should -BeNullOrEmpty
    }

    It "find resource(s) given wildcard Name" {
        # FindNameGlobbing
        $wildcardName = "test_local_m*"
        $res = Find-PSResource -Name $wildcardName -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameGlobbingFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
        $res | Should -BeNullOrEmpty
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
        $res = Find-PSResource -Name $testModuleName -Version $Version -Repository $ADORepoName
        $res | Should -Not -BeNullOrEmpty
        foreach ($item in $res) {
            $item.Name | Should -Be $testModuleName
            $ExpectedVersions | Should -Contain $item.Version
        }
    }

    It "find all versions of resource when given specific Name, Version not null --> '*'" {
        # FindVersionGlobbing()
        $res = Find-PSResource -Name $testModuleName -Version "*" -Repository $ADORepoName
        $res | Should -Not -BeNullOrEmpty
        $res | ForEach-Object {
            $_.Name | Should -Be $testModuleName
        }

        $res.Count | Should -BeGreaterOrEqual 1
    }

    It "find resource with latest (including prerelease) version given Prerelease parameter" {
        # FindName()
        # test_local_mod resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Find-PSResource -Name $testModuleName -Repository $ADORepoName
        $res.Version | Should -Be "5.0.0"

        $resPrerelease = Find-PSResource -Name $testModuleName -Prerelease -Repository $ADORepoName
        $resPrerelease.Version | Should -Be "5.2.5"
        $resPrerelease.Prerelease | Should -Be "alpha001"
    }

    It "find resources, including Prerelease version resources, when given Prerelease parameter" {
        # FindVersionGlobbing()
        $resWithoutPrerelease = Find-PSResource -Name $testModuleName -Version "*" -Repository $ADORepoName
        $resWithPrerelease = Find-PSResource -Name $testModuleName -Version "*" -Repository $ADORepoName
        $resWithPrerelease.Count | Should -BeGreaterOrEqual $resWithoutPrerelease.Count
    }

    It "find resource that satisfies given Name and Tag property (single tag)" {
        # FindNameWithTag()
        $requiredTag = "test"
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTag -Repository $ADORepoName
        $res.Name | Should -Be $testModuleName
        $res.Tags | Should -Contain $requiredTag
    }

    It "should not find resource if Name and Tag are not both satisfied (single tag)" {
        # FindNameWithTag
        $requiredTag = "Windows" # tag "windows" is not present for test_local_mod package
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTag -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "find resource that satisfies given Name and Tag property (multiple tags)" {
        # FindNameWithTag()
        $requiredTags = @("test", "Tag2")
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTags -Repository $ADORepoName
        $res.Name | Should -Be $testModuleName
        $res.Tags | Should -Contain $requiredTags[0]
        $res.Tags | Should -Contain $requiredTags[1]
    }

    It "should not find resource if Name and Tag are not both satisfied (multiple tag)" {
        # FindNameWithTag
        $requiredTags = @("test", "Windows") # tag "windows" is not present for test_local_mod package
        $res = Find-PSResource -Name $testModuleName -Tag $requiredTags -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "should not find resources when given Name with wildcard and Tag proprties" {
        # FindNameGlobbingWithTag()
        $requiredTag = "test"
        $nameWithWildcard = "test_local_m*"
        $res = Find-PSResource -Name $nameWithWildcard -Tag $requiredTag -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameGlobbingWithTagFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "find resource that satisfies given Name, Version and Tag property (single tag)" {
        # FindVersionWithTag()
        $requiredTag = "test"
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0.0" -Tag $requiredTag -Repository $ADORepoName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0"
        $res.Tags | Should -Contain $requiredTag
    }

    It "should not find resource if Name, Version and Tag property are not all satisfied (single tag)" {
        # FindVersionWithTag()
        $requiredTag = "windows" # tag "windows" is not present for test_local_mod package
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0.0" -Tag $requiredTag -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "find resource that satisfies given Name, Version and Tag property (multiple tags)" {
        # FindVersionWithTag()
        $requiredTags = @("test", "Tag2")
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0.0" -Tag $requiredTags -Repository $ADORepoName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0"
        $res.Tags | Should -Contain $requiredTags[0]
        $res.Tags | Should -Contain $requiredTags[1]

    }

    It "should not find resource if Name, Version and Tag property are not all satisfied (multiple tags)" {
        # FindVersionWithTag()
        $requiredTags = @("test", "windows")
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0.0" -Tag $requiredTags -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "should not find resources given Tag property" {
        # FindTag()
        $tagToFind = "Tag2"
        $res = Find-PSResource -Tag $tagToFind -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindTagsFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "should not find resource given CommandName" {
        # FindCommandOrDSCResource()
        $res = Find-PSResource -CommandName "command" -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        write-Host $($err[0].FullyQualifiedErrorId)
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindCommandOrDscResourceFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "should not find resource given DscResourceName" {
        # FindCommandOrDSCResource()
        $res = Find-PSResource -DscResourceName "dscResource" -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindCommandOrDscResourceFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "should not find all resources given Name '*'" {
        # FindAll()
        $res = Find-PSResource -Name "*" -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindAllFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }
}
##>