# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -force

Describe 'Test Find-PSResource for Role Capability' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        Get-NewPSResourceRepositoryFile
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    # Purpose: find Role Capability resource, given Name parameter
    #
    # Action: Find-PSResource -Name TestRoleCapModule
    #
    # Expected Result: return TestRoleCapModule resource
    It "find Role Capability resource, given Name parameter" {
        $res = Find-PSResource -Name TestRoleCapModule
        $res.Name | Should -Be "TestRoleCapModule"
    }

    # Purpose: not find non-existant Role Capability resource, given Name parameter
    #
    # Action: Find-PSResource -Name NonExistantRoleCap
    #
    # Expected Result: should not return any resource
    It "not find non-existant Role Capability resource, given Name parameter" {
        $res = Find-PSResource -Name NonExistantRoleCap
        $res | Should -BeNullOrEmpty
    }


    # Purpose: find a RoleCapability resource given Name, to validate version parameter values
    #
    # Action: Find-PSResource -Name DscTestModule -Version [2.0.0.0]
    #
    # Expected Result: return resource meeting version criteria
    It "find DSC resource when given Name to <Reason>" -TestCases @(
        @{Version="[2.0.0.0]";          ExpectedVersion="2.0.0.0"; Reason="validate version, exact match"},
        @{Version="2.0.0.0";            ExpectedVersion="2.0.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.0.0.0, 2.5.0.0]"; ExpectedVersion="2.5.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="(1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="[1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(,2.5.0.0)";         ExpectedVersion="2.0.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,2.5.0.0]";         ExpectedVersion="2.5.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -Name "DscTestModule" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "DscTestModule"
        $res.Version | Should -Be $ExpectedVersion
    }

    # Purpose: not find resources with invalid version
    #
    # Action: Find-PSResource -Name "DscTestModule" -Version "(2.5.0.0)"
    #
    # Expected Result: should not return a resource
    It "not find resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version="(2.5.0.0)";       Description="exlcusive version (2.5.0.0)"},
        @{Version="[2-5-0-0]";       Description="version formatted with invalid delimiter"},
        @{Version="[2.*.0]";         Description="version with wilcard in middle"},
        @{Version="[*.5.0.0]";       Description="version with wilcard at start"},
        @{Version="[2.*.0.0]";       Description="version with wildcard at second digit"},
        @{Version="[2.5.*.0]";       Description="version with wildcard at third digit"}
        @{Version="[2.5.0.*";        Description="version with wildcard at end"},
        @{Version="[2..0.0]";        Description="version with missing digit in middle"},
        @{Version="[2.5.0.]";        Description="version with missing digit at end"},
        @{Version="[2.5.0.0.0]";     Description="version with more than 4 digits"}
    )
    {
        param($Version, $Description)
        $res = Find-PSResource -Name "DscTestModule" -Version $Version -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find Role Capability resource with wildcard version, given Version parameter
    #
    # Action: Find-PSResource -Name "TestRoleCapModule" -Version "*"
    #
    # Expected Result: returns all versions of DSCTestModule (versions in descending order)
    It "find Role Capability resource with wildcard version, given Version parameter -> '*' " {
        $res = Find-PSResource -Name "DscTestModule" -Version "*" -Repository $TestGalleryName
        $res.Count | Should -BeGreaterOrEqual 1
    }
    
    # Purpose: find Role Capability resource, given ModuleName parameter
    #
    # Action: Find-PSResource -ModuleName JeaExamples -Repository PSGallery
    #
    # Expected Result: should return JeaExamples resource
    It "find Role Capability resource, given ModuleName parameter" {
        $res = Find-PSResource -ModuleName "DscTestModule" -Repository $TestGalleryName
        $res.Name | Should -Be "DscTestModule"
    }


    # Purpose: find a RoleCapability resource given ModuleName, to validate version parameter values
    #
    # Action: Find-PSResource -ModuleName DscTestModule -Version [2.0.0.0]
    #
    # Expected Result: return resource meeting version criteria
    It "find DSC resource when given Name to <Reason>" -TestCases @(
        @{Version="[2.0.0.0]";          ExpectedVersion="2.0.0.0"; Reason="validate version, exact match"},
        @{Version="2.0.0.0";            ExpectedVersion="2.0.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.0.0.0, 2.5.0.0]"; ExpectedVersion="2.5.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="(1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="[1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(,2.5.0.0)";         ExpectedVersion="2.0.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,2.5.0.0]";         ExpectedVersion="2.5.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -ModuleName "DscTestModule" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "DscTestModule"
        $res.Version | Should -Be $ExpectedVersion
    }

    # Purpose: find Role Capability resource with given Tags parameter
    #
    # Action: Find-PSResource -Tags "CommandsAndResource" -Repository PoshTestGallery | Where-Object { $_.Name -eq "PSGETTEST-TestPackageMetadata" }
    #
    # Expected Result: should return PSGETTEST-TestPackageMetadata resource
    It "find Role Capability resource with given Tags parameter" {
        $res = Find-PSResource -Tags "CommandsAndResource" -Repository $TestGalleryName | Where-Object { $_.Name -eq "PSGETTEST-TestPackageMetadata" }
        $res.Name | Should -Be "PSGETTEST-TestPackageMetadata"
    }

    # Purpose: find Role Capability resource with multiple given Tags parameter
    #
    # Action: Find-PSResource -Tags "CommandsAndResource","Tag2", "PSGet" -Repository PoshTestGallery | Where-Object { $_.Name -eq "PSGETTEST-TestPackageMetadata" }
    #
    # Expected Result: should return PSGETTEST-TestPackageMetadata resource
    It "find Role Capability resource with given Tags parameter" {
        $res = Find-PSResource -Tags "CommandsAndResource","Tag2", "PSGet" -Repository $TestGalleryName | Where-Object { $_.Name -eq "PSGETTEST-TestPackageMetadata" }
        $res.Name | Should -Be "PSGETTEST-TestPackageMetadata"
    }

    # Purpose: not find Role Capability resource that doesn't exist in specified repository, given Repository parameter
    #
    # Action: Find-PSResource -Name JeaExamples -Repository PoshTestGallery
    #
    # Expected Result: should not find JeaExamples resource
    It "not find Role Capability resource that doesn't exist in specified repository, given Repository parameter" {
        $res = Find-PSResource -Name JeaExamples -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find Role Capability resource that exists only in specified repository, given Repository parameter
    #
    # Action: Find-PSResource -Name JeaExamples -Repository $PSGalleryName
    #
    # Expected Result: should find JeaExamples resource
    It "find resource that exists only in a specific repository, given Repository parameter" {
        $resRightRepo = Find-PSResource -Name JeaExamples -Repository $PSGalleryName
        $resRightRepo.Name | Should -Be "JeaExamples"
    }

    # Purpose: find resource and check performance opitmization, given and not given Repository parameter
    #
    # Action: Find-PSResource -Name JeaExamples" -Repository PSGallery
    #
    # Expected Result: find resource quicker when repository is specified
    It "find resource existing in multiple repositories given Repository parameter" {
        $repeat = 100
        $timeWithoutRepoSpecified = Measure-Command -Expression {
            for ($i = 0; $i -lt $repeat; $i++) {
                $res = Find-PSResource -Name "JeaExamples"
                $res.Repository | Should -Be "PSGallery"                
            }
        }

        $timeWithRepoSpecified = Measure-Command -Expression {
            for ($i = 0; $i -lt $repeat; $i++) {
                $res = Find-PSResource -Name "JeaExamples" -Repository $PSGalleryName
                $res.Repository | Should -Be "PSGallery" 
            }
        }

        $timeWithRepoSpecified | Should -BeLessOrEqual $timeWithoutRepoSpecified
    }

    It "find resource in local repository given Repository parameter" {

        # create path and make sure testdir there exists, otherwise will cause problems in .xml file URL
        $repoURLAddress = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "testdir"
        $null = New-Item -Path $repoURLAddress -ItemType Directory -Force 

        Set-PSResourceRepository -Name "psgettestlocal" -URL $repoURLAddress

        # register module to that repository
        $TestLocalDirectory = 'TestLocalDirectory'
        $tmpdir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $TestLocalDirectory

        $script:TempModulesPath = Join-Path -Path $tmpdir -ChildPath "PSGet_$(Get-Random)"
        $null = New-Item -Path $script:TempModulesPath -ItemType Directory -Force

        $script:PublishModuleName = "TestFindRoleCapModule"
        $script:PublishModuleBase = Join-Path $script:TempModulesPath $script:PublishModuleName
        $null = New-Item -Path $script:PublishModuleBase -ItemType Directory -Force

        $PublishModuleBase = Join-Path $script:TempModulesPath $script:PublishModuleName
        $version = "1.0"

        New-PSRoleCapabilityFile -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psrc")
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"  -NestedModules "$script:PublishModuleName.psm1"

        Publish-PSResource -path  $script:PublishModuleBase -Repository psgettestlocal

        # test find
        $res = Find-PSResource -Name $script:PublishModuleName -Repository "psgettestlocal"
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be $script:PublishModuleName

        if($tempdir -and (Test-Path $tempdir))
        {
            Remove-Item $tempdir -Force -Recurse -ErrorAction SilentlyContinue
        }

    }
}
