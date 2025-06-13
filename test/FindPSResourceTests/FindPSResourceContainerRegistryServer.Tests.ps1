# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

Describe 'Test HTTP Find-PSResource for ACR Server Protocol' -tags 'CI' {

    BeforeAll{
        $testModuleName = "test-module"
        $testModuleWith2DigitVersion = "test-2DigitPkg"
        $testModuleParentName = "test_parent_mod"
        $testModuleDependencyName = "test_dependency_mod"
        $testScriptName = "test-script"
        $ACRRepoName = "ACRRepo"
        $ACRRepoUri = "https://psresourcegettest.azurecr.io"
        Get-NewPSResourceRepositoryFile

        $usingAzAuth = $env:USINGAZAUTH -eq 'true'

        if ($usingAzAuth)
        {
            Write-Verbose -Verbose "Using Az module for authentication"
            Register-PSResourceRepository -Name $ACRRepoName -ApiVersion 'ContainerRegistry' -Uri $ACRRepoUri -Verbose
            Write-Verbose -Verbose "Registering ACR repository with Az authentication completed"
            Get-PSResourceRepository -Name $ACRRepoName -Verbose
            Write-Verbose -Verbose "Get-PSResourceRepository completed"
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
        Write-Verbose -Verbose "Finding resource with Name: $testModuleName"

        try {
            $res = Find-PSResource -Name $testModuleName -Repository $ACRRepoName -Verbose -Debug -ErrorAction Stop
        }
        catch {
            Write-Error "Error occurred while finding resource: $_"
            Get-Error
        }

        Write-Verbose -Verbose "Find-PSResource completed"
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "5.0.0"
    }

    It "Should not find resource given nonexistant Name" {
        # FindName()
        Write-Verbose -Verbose "Moved to the next test case to find non-existant resource"
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

    It "Find resource when version contains different number of digits than the normalized version" {
        # the resource has version "1.0", but querying with any equivalent version should work
        $res1DigitVersion = Find-PSResource -Name $testModuleWith2DigitVersion -Version "1" -Repository $ACRRepoName
        $res1DigitVersion | Should -Not -BeNullOrEmpty
        $res1DigitVersion.Version | Should -Be "1.0"

        $res2DigitVersion = Find-PSResource -Name $testModuleWith2DigitVersion -Version "1.0" -Repository $ACRRepoName
        $res2DigitVersion | Should -Not -BeNullOrEmpty
        $res2DigitVersion.Version | Should -Be "1.0"

        $res3DigitVersion = Find-PSResource -Name $testModuleWith2DigitVersion -Version "1.0.0" -Repository $ACRRepoName
        $res3DigitVersion | Should -Not -BeNullOrEmpty
        $res3DigitVersion.Version | Should -Be "1.0"

        $res4DigitVersion = Find-PSResource -Name $testModuleWith2DigitVersion -Version "1.0.0.0" -Repository $ACRRepoName
        $res4DigitVersion | Should -Not -BeNullOrEmpty
        $res4DigitVersion.Version | Should -Be "1.0"
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
        $res.Prerelease | Should -Be "alpha"
    }

    It "Find resource with latest (including prerelease) version given Prerelease parameter" {
        # FindName()
        # test_local_mod resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Find-PSResource -Name $testModuleName -Repository $ACRRepoName
        $res.Version | Should -Be "5.0.0"

        $resPrerelease = Find-PSResource -Name $testModuleName -Prerelease -Repository $ACRRepoName
        $resPrerelease.Version | Should -Be "5.2.5"
        $resPrerelease.Prerelease | Should -Be "alpha"
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

    It "Should find all resources given Name '*'" {
        # FindAll()
        $res = Find-PSResource -Name "*" -Repository $ACRRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -Not -BeNullOrEmpty
        $res.Count | Should -BeGreaterThan 0
    }

    It "Should find script given Name" {
        # FindName()
        $res = Find-PSResource -Name $testScriptName -Repository $ACRRepoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -BeExactly $testScriptName
        $res.Version | Should -Be "3.0.0"
        $res.Type.ToString() | Should -Be "Script"
    }

    It "Should find script given Name and Prerelease" {
        # latest version is a prerelease version
        $res = Find-PSResource -Name $testScriptName -Prerelease -Repository $ACRRepoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -BeExactly $testScriptName
        $res.Version | Should -Be "5.0.0"
        $res.Prerelease | Should -Be "alpha"
        $res.Type.ToString() | Should -Be "Script"
    }

    It "Should find script given Name and Version" {
        # FindVersion()
        $res = Find-PSResource -Name $testScriptName -Version "1.0.0" -Repository $ACRRepoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -BeExactly $testScriptName
        $res.Version | Should -Be "1.0.0"
        $res.Type.ToString() | Should -Be "Script"
    }

    It "Should find script given Name, Version and Prerelease" {
        # latest version is a prerelease version
        $res = Find-PSResource -Name $testScriptName -Version "5.0.0-alpha" -Prerelease -Repository $ACRRepoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -BeExactly $testScriptName
        $res.Version | Should -Be "5.0.0"
        $res.Prerelease | Should -Be "alpha"
        $res.Type.ToString() | Should -Be "Script"
    }

    It "Should find and return correct resource type - module" {
        $res = Find-PSResource -Name $testModuleName -Repository $ACRRepoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -BeExactly $testModuleName
        $res.Version | Should -Be "5.0.0"
        $res.Type.ToString() | Should -Be "Module"
    }

    It "Should find and return correct resource type - script" {
        $scriptName = "test-script"
        $res = Find-PSResource -Name $scriptName -Repository $ACRRepoName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -BeExactly $scriptName
        $res.Version | Should -Be "3.0.0"
        $res.Type.ToString() | Should -Be "Script"
    }

    It "Should find module with varying case sensitivity" {
        $res = Find-PSResource -Name "test-camelCaseModule" -Repository $ACRRepoName
        $res.Name | Should -BeExactly "test-camelCaseModule"
        $res.Version | Should -Be "1.0.0"
        $res.Type.ToString() | Should -Be "Module"
    }

    It "Should find script with varying case sensitivity" {
        $res = Find-PSResource -Name "test-camelCaseScript" -Repository $ACRRepoName
        $res.Name | Should -BeExactly "test-camelCaseScript"
        $res.Version | Should -Be "1.0.0"
        $res.Type.ToString() | Should -Be "Script"
    }

    It "Should find resource with dependency, given Name and Version" {
        $res = Find-PSResource -Name "Az.Storage" -Version "8.0.0" -Repository $ACRRepoName
        $res.Dependencies.Length | Should -Be 1
        $res.Dependencies[0].Name | Should -Be "Az.Accounts"
    }

    It "Should find resource and its associated author, licenseUri, projectUri, releaseNotes, etc properties" {
        $res = Find-PSResource -Name "Az.Storage" -Version "8.0.0" -Repository $ACRRepoName
        $res.Author | Should -Be "Microsoft Corporation"
        $res.CompanyName | Should -Be "Microsoft Corporation"
        $res.LicenseUri | Should -Be "https://aka.ms/azps-license"
        $res.ProjectUri | Should -Be "https://github.com/Azure/azure-powershell"
        $res.ReleaseNotes.Length | Should -Not -Be 0
        $res.Tags.Length | Should -Be 5
    }
}

Describe 'Test Find-PSResource for MAR Repository' -tags 'CI' {
    BeforeAll {
        Register-PSResourceRepository -Name "MAR" -Uri "https://mcr.microsoft.com" -ApiVersion "ContainerRegistry"
    }

    AfterAll {
        Unregister-PSResourceRepository -Name "MAR"
    }

    It "Should find resource given specific Name, Version null" {
        $res = Find-PSResource -Name "Az.Accounts" -Repository "MAR"
        $res.Name | Should -Be "Az.Accounts"
        $res.Version | Should -BeGreaterThan ([Version]"4.0.0")
    }

    It "Should find resource and its dependency given specific Name and Version" {
        $res = Find-PSResource -Name "Az.Storage" -Version "8.0.0" -Repository "MAR"
        $res.Dependencies.Length | Should -Be 1
        $res.Dependencies[0].Name | Should -Be "Az.Accounts"
    }

    It "Should find Azpreview resource and it's dependency given specific Name and Version" {
        $res = Find-PSResource -Name "Azpreview" -Version "13.2.0" -Repository "MAR"
        $res.Dependencies.Length | Should -Not -Be 0
    }

    It "Should find resource with wildcard in Name" {
        $res = Find-PSResource -Name "Az.App*" -Repository "MAR"
        $res | Should -Not -BeNullOrEmpty
        $res.Count | Should -BeGreaterThan 1
    }

    It "Should find all resource with wildcard in Name" {
        $res = Find-PSResource -Name "*" -Repository "MAR"
        $res | Should -Not -BeNullOrEmpty
        $res.Count | Should -BeGreaterThan 1
    }
}

# Skip this test fo
Describe 'Test Find-PSResource for unauthenticated ACR repository' -tags 'CI' {
    BeforeAll {
        $skipOnWinPS =  $PSVersionTable.PSVersion.Major -eq 5

        if (-not $skipOnWinPS) {
            Register-PSResourceRepository -Name "Unauthenticated" -Uri "https://psresourcegetnoauth.azurecr.io/" -ApiVersion "ContainerRegistry"
        }
    }

    AfterAll {
        if (-not $skipOnWinPS) {
            Unregister-PSResourceRepository -Name "Unauthenticated"
        }
    }

    It "Should find resource given specific Name, Version null" {

        if ($skipOnWinPS) {
            Set-ItResult -Pending -Because "Skipping test on Windows PowerShell"
            return
        }

        $res = Find-PSResource -Name "hello-world" -Repository "Unauthenticated"
        $res.Name | Should -Be "hello-world"
        $res.Version | Should -Be "5.0.0"
    }
}
