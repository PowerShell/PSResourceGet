# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

Describe 'Test HTTP Find-PSResource for ACR Server Protocol' -tags 'CI' {

    BeforeAll{
        $testModuleName = "test_local_mod"
        $testModuleParentName = "test_parent_mod"
        $testModuleDependencyName = "test_dependency_mod"
        $testScript = "testscript"
        $ACRRepoName = "ACRRepo"
        $ACRRepoUri = "https://psresourcegettest.azurecr.io"
        Get-NewPSResourceRepositoryFile

        $usingAzAuth = $env:USINGAZAUTH -eq 'true'

        if ($usingAzAuth)
        {
            Write-Verbose -Verbose "Using Az module for authentication"
            Register-PSResourceRepository -Name $ACRRepoName -ApiVersion 'ContainerRegistry' -Uri $ACRRepoUri -Verbose
        }
        else
        {
            $psCredInfo = New-Object Microsoft.PowerShell.PSResourceGet.UtilClasses.PSCredentialInfo ("SecretStore", "$env:TENANTID")
            Register-PSResourceRepository -Name $ACRRepoName -ApiVersion 'ContainerRegistry' -Uri $ACRRepoUri -CredentialInfo $psCredInfo -Verbose
        }
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Find resource given specific Name, Version null" {
        # FindName()
        $res = Find-PSResource -Name $testModuleName -Repository $ACRRepoName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0"
    }

    It "Should not find resource given nonexistant Name" {
        # FindName()
        $res = Find-PSResource -Name NonExistantModule -Repository $ACRRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ResourceNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
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

    It "Find resource when given Name to <Reason> <Version>" -TestCases $testCases2{
        # FindVersionGlobbing()
        param($Version, $ExpectedVersions)
        $res = Find-PSResource -Name $testModuleName -Version $Version -Repository $ACRRepoName
        $res | Should -Not -BeNullOrEmpty
        foreach ($item in $res) {
            $item.Name | Should -Be $testModuleName
            $ExpectedVersions | Should -Contain $item.Version
        }
    }

    It "Find all versions of resource when given specific Name, Version not null --> '*'" {
        # FindVersionGlobbing()
        $res = Find-PSResource -Name $testModuleName -Version "*" -Repository $ACRRepoName
        $res | Should -Not -BeNullOrEmpty
        $res | ForEach-Object {
            $_.Name | Should -Be $testModuleName
        }

        $res.Count | Should -BeGreaterOrEqual 1
    }

    It "Find module and dependencies when -IncludeDependencies is specified" {
        $res = Find-PSResource -Name $testModuleParentName -Repository $ACRRepoName -IncludeDependencies
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be @($testModuleParentName, $testModuleDependencyName)
        $res.Version[0].ToString() | Should -Be "1.0.0"
        $res.Version[1].ToString() | Should -Be "1.0.0"
    }

    It "Find resource given specific Name, Version null but allowing Prerelease" {
        # FindName()
        $res = Find-PSResource -Name $testModuleName -Repository $ACRRepoName -Prerelease
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.2.5"
        $res.Prerelease | Should -Be "alpha001"
    }

    It "Find resource with latest (including prerelease) version given Prerelease parameter" {
        # FindName()
        # test_local_mod resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Find-PSResource -Name $testModuleName -Repository $ACRRepoName
        $res.Version | Should -Be "5.0.0"

        $resPrerelease = Find-PSResource -Name $testModuleName -Prerelease -Repository $ACRRepoName
        $resPrerelease.Version | Should -Be "5.2.5"
        $resPrerelease.Prerelease | Should -Be "alpha001"
    }

    It "Find resources, including Prerelease version resources, when given Prerelease parameter" {
        # FindVersionGlobbing()
        $resWithoutPrerelease = Find-PSResource -Name $testModuleName -Version "*" -Repository $ACRRepoName
        $resWithPrerelease = Find-PSResource -Name $testModuleName -Version "*" -Repository $ACRRepoName -Prerelease
        $resWithPrerelease.Count | Should -BeGreaterOrEqual $resWithoutPrerelease.Count
    }

    It "Should not find resource if Name, Version and Tag property are not all satisfied (single tag)" {
        # FindVersionWithTag()
        $requiredTag = "windows" # tag "windows" is not present for test_local_mod package
        $res = Find-PSResource -Name $testModuleName -Version "5.0.0.0" -Tag $requiredTag -Repository $ACRRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindVersionWithTagFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "Should not find resources given Tag property" {
        # FindTag()
        $tagToFind = "Tag2"
        $res = Find-PSResource -Tag $tagToFind -Repository $ACRRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindTagsFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "Should not find resource given CommandName" {
        # FindCommandOrDSCResource()
        $res = Find-PSResource -CommandName "command" -Repository $ACRRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        write-Host $($err[0].FullyQualifiedErrorId)
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindCommandOrDscResourceFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "Should not find resource given DscResourceName" {
        # FindCommandOrDSCResource()
        $res = Find-PSResource -DscResourceName "dscResource" -Repository $ACRRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindCommandOrDscResourceFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "Should not find all resources given Name '*'" {
        # FindAll()
        $res = Find-PSResource -Name "*" -Repository $ACRRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindAllFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "Should find script given Name" {
        # FindName()
        $res = Find-PSResource -Name $testScript -Repository $ACRRepoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -BeExactly $testScript
        $res.Version | Should -Be "2.0.0"
    }

    It "Should find script given Name and Prerelease" {
        # latest version is a prerelease version
        $res = Find-PSResource -Name $testScript -Prerelease -Repository $ACRRepoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -BeExactly $testScript
        $res.Version | Should -Be "3.5.0"
        $res.Prerelease | Should -Be "alpha"
    }

    It "Should find script given Name and Version" {
        # FindVersion()
        $res = Find-PSResource -Name $testScript -Version "1.0.0" -Repository $ACRRepoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -BeExactly $testScript
        $res.Version | Should -Be "1.0.0"
    }

    It "Should find script given Name, Version and Prerelease" {
        # latest version is a prerelease version
        $res = Find-PSResource -Name $testScript -Version "3.5.0-alpha" -Prerelease -Repository $ACRRepoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -BeExactly $testScript
        $res.Version | Should -Be "3.5.0"
        $res.Prerelease | Should -Be "alpha"
    }

    It "Should find and return correct resource type - module" {
        $moduleName = "test_dependency_mod"
        $res = Find-PSResource -Name $moduleName -Repository $ACRRepoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -BeExactly $moduleName
        $res.Version | Should -Be "1.0.0"
        $res.Type.ToString() | Should -Be "Module"
    }

    It "Should find and return correct resource type - script" {
        $scriptName = "test-script"
        $res = Find-PSResource -Name $scriptName -Repository $ACRRepoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -BeExactly $scriptName
        $res.Version | Should -Be "1.0.0"
        $res.Type.ToString() | Should -Be "Script"
    }
}
