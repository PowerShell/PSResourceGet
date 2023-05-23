# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"

Describe 'Test HTTP Find-PSResource for ADO Server Protocol' -tags 'CI' {

    BeforeAll{
        $testModuleName = "dotnet-format"
        $testModuleWithTagsName = "dotnet-ef" # this package has about 300 versions so best not to use it for all the tests.
        $ADORepoName = "DotnetPublicFeed"
        Get-NewPSResourceRepositoryFile
        Register-PSResourceRepository -Name $ADORepoName -Uri "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet8/nuget/v3/index.json"
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "find resource given specific Name, Version null" {
        # FindName()
        $res = Find-PSResource -Name $testModuleName -Repository $ADORepoName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "8.0.427001"
    }

    It "should not find resource given nonexistant Name" {
        $res = Find-PSResource -Name "NonExistantModule" -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    It "should not find resource(s) given wildcard Name as not supported for ADO feed" {
        # FindNameGlobbing (is not supported for ADO repository)
        $wildcardName = "test_module*"
        $res = Find-PSResource -Name $wildcardName -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameGlobbingFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    $testCases2 = @{Version="[8.0.427001]";              ExpectedVersions=@("8.0.427001");                                  Reason="validate version, exact match"},
                  @{Version="8.0.427001";                ExpectedVersions=@("8.0.427001");                                  Reason="validate version, exact match without bracket syntax"},
                  @{Version="[8.0.426908, 8.0.427001]";  ExpectedVersions=@("8.0.426908", "8.0.426911", "8.0.427001");      Reason="validate version, exact range inclusive"},
                  @{Version="(8.0.426908, 8.0.427001)";  ExpectedVersions=@("8.0.426911");                                  Reason="validate version, exact range exclusive"},
                  @{Version="(8.0.426908,)";             ExpectedVersions=@("8.0.426911", "8.0.427001");                    Reason="validate version, minimum version exclusive"},
                  @{Version="[8.0.426908,)";             ExpectedVersions=@("8.0.426908", "8.0.426911", "8.0.427001");      Reason="validate version, minimum version inclusive"},
                  @{Version="[8.0.426908, 8.0.427001)";  ExpectedVersions=@("8.0.426908", "8.0.426911");                    Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
                  @{Version="(8.0.426908, 8.0.427001]";  ExpectedVersions=@("8.0.426911", "8.0.427001");                    Reason="validate version, mixed exclusive minimum and inclusive maximum version"}

    It "find resource when given Name to <Reason> <Version>" -TestCases $testCases2{
        # Note: VersionRange: "(,8.0.426911)" and "(,8.0.426911]" work but would return about 300 versions so will not explicitly test those scenarios here.
        # FindVersionGlobbing()
        param($Version, $ExpectedVersions)
        $res = Find-PSResource -Name $testModuleName -Version $Version -Repository $ADORepoName
        foreach ($item in $res) {
            $item.Name | Should -Be $testModuleName
            $ExpectedVersions | Should -Contain $item.Version
        }
    }

    It "find all versions of resource when given specific Name, Version not null --> '*'" {
        # FindVersionGlobbing()
        $res = Find-PSResource -Name $testModuleName -Version "*" -Repository $ADORepoName
        $res | ForEach-Object {
            $_.Name | Should -Be $testModuleName
        }

        $res.Count | Should -BeGreaterOrEqual 1
    }

    It "find resource given Name and Tag property (single)" {
        # FindNameWithTag()
        $requiredTag = "Entity"
        $res = Find-PSResource -Name $testModuleWithTagsName -Tag $requiredTag -Repository $ADORepoName -Prerelease
        $res.Name | Should -Be $testModuleWithTagsName
        $res.Tags | Should -Contain $requiredTag
    }

    It "should not find resource if Name and Tag are not both satisfied (single tag)" {
        # FindNameWithTag
        $requiredTag = "Windows" # tag "windows" is not present for this package
        $res = Find-PSResource -Name $testModuleWithTagsName -Tag $requiredTag -Repository $ADORepoName -Prerelease -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    It "find resource that satisfies given Name and Tag property (multiple tags)" {
        # FindNameWithTag()
        $requiredTags = @("Entity", "Data")
        $res = Find-PSResource -Name $testModuleWithTagsName -Tag $requiredTags -Repository $ADORepoName -Prerelease
        $res.Name | Should -Be $testModuleWithTagsName
        $res.Tags | Should -Contain $requiredTags[0]
        $res.Tags | Should -Contain $requiredTags[1]
    }

    It "should not find resource if Name and Tag are not both satisfied (multiple tag)" {
        # FindNameWithTag
        $requiredTags = @("Entity", "Windows") # tag "windows" is not present for this package
        $res = Find-PSResource -Name $testModuleWithTagsName -Tag $requiredTags -Repository $ADORepoName -Prerelease -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    It "should not find resources when given Name pattern and Tag as not supported for ADO repository" {
        # FindNameGlobbingWithTag()
        $requiredTag = "test"
        $nameWithWildcard = "test_module*"
        $res = Find-PSResource -Name $nameWithWildcard -Tag $requiredTag -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameGlobbingFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    It "find resource that satisfies given Name, Version and Tag property (single tag)" {
        # FindVersionWithTag()
        $requiredTag = "Entity"
        $res = Find-PSResource -Name $testModuleWithTagsName -Version "8.0.0-preview.5.23269.2" -Prerelease -Tag $requiredTag -Repository $ADORepoName
        $res.Name | Should -Be $testModuleWithTagsName
        $res.Version | Should -Be "8.0.0"
        $res.Prerelease | Should -Be "preview.5.23269.2"
        $res.Tags | Should -Contain $requiredTag
    }

    It "should not find resource if Name, Version and Tag property are not all satisfied (single tag)" {
        # FindVersionWithTag()
        $requiredTag = "windows" # tag "windows" is not present for this package
        $res = Find-PSResource -Name $testModuleWithTagsName  -Version "8.0.0-preview.5.23269.2" -Prerelease -Tag $requiredTag -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindVersionFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    It "find resource that satisfies given Name, Version and Tag property (multiple tags)" {
        # FindVersionWithTag()
        $requiredTags = @("Entity", "Data")
        $res = Find-PSResource -Name $testModuleWithTagsName -Version "8.0.0-preview.5.23269.2" -Prerelease -Tag $requiredTags -Repository $ADORepoName
        $res.Name | Should -Be $testModuleWithTagsName
        $res.Version | Should -Be "8.0.0"
        $res.Prerelease | Should -Be "preview.5.23269.2"
        $res.Tags | Should -Contain $requiredTags[0]
        $res.Tags | Should -Contain $requiredTags[1]

    }

    It "should not find resource if Name, Version and Tag property are not all satisfied (multiple tags)" {
        # FindVersionWithTag()
        $requiredTags = @("Entity", "windows")
        $res = Find-PSResource -Name $testModuleWithTagsName -Version "8.0.0-preview.5.23269.2" -Tag $requiredTags -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindVersionFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    It "find all resources with specified tag given Tag property" {
        # FindTag()
        $tagToFind = "Tag2"
        $res = Find-PSResource -Tag $tagToFind -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindTagFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    It "should not find resource given CommandName" {
        $res = Find-PSResource -CommandName "command" -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindCommandOrDSCResourceFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    It "should not find resource given DscResourceName" {
        $res = Find-PSResource -DscResourceName "dscResource" -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindCommandOrDSCResourceFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }

    It "should not find all resources given Name '*'" {
        $res = Find-PSResource -Name "*" -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindAllFail,Microsoft.PowerShell.PowerShellGet.Cmdlets.FindPSResource"
    }
}
