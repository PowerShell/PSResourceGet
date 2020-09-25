# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -force

Describe "Test Find-PSResource for Script" {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        Get-NewPSResourceRepositoryFile
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    # Purpose: find a script resource with specified Name parameter
    #
    # Action: Find-PSResource -Name Fabrikam-ServerScript
    #
    # Expected Result: should return a resource with specified name Fabrikam-ServerScript
    It "find resource given Name parameter" {
        $res = Find-PSResource -Name Fabrikam-ServerScript
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "Fabrikam-ServerScript"
    }

    # Purpose: successfully not find a resource that doesn't have a valid name
    #
    # Action: Find-PSResource -Name NonExistantScript -Repository PoshTestGallery
    #
    # Expected Result: should not return a resource, as none with specified name exists
    It "not find a resource that doesn't have a valid name" {
        $res = Find-PSResource -Name NonExistantScript -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find resources with multiple values provided for Name parameter
    #
    # Action: Find-PSResource -Name Fabrikam-ServerScript,Fabrikam-ClientScript -Repository PoshTestGallery
    #
    # Expected Result: should return the multiple resources specified
    It "find resources with multiple values provided for Name parameter" {
        $res = Find-PSResource -Name Fabrikam-ServerScript,Fabrikam-ClientScript -Repository $TestGalleryName
        $res | Should -Not -BeNullOrEmpty
        $res.Count | Should -Be 2
    }

    # Purpose: find resource when given Name, Version param not null
    #
    # Action: Find-PSResource -Name ContosoServer -Repository PoshTestGallery
    #
    # Expected Results: should return resource with appropriate version
    It "find resource when given Name to <Reason>" -TestCases @(
        @{Version="[2.0.0.0]";          ExpectedVersion="2.0.0.0"; Reason="validate version, exact match"},
        @{Version="2.0.0.0";            ExpectedVersion="2.0.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.0.0.0, 2.5.0.0]"; ExpectedVersion="2.5.0.0"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, exact range exclusive"},
        @{Version="(1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version exclusive"},
        @{Version="[1.0.0.0,)";         ExpectedVersion="2.5.0.0"; Reason="validate version, minimum version inclusive"},
        @{Version="(,1.5.0.0)";         ExpectedVersion="1.2.0.0"; Reason="validate version, maximum version exclusive"},
        @{Version="(,1.5.0.0]";         ExpectedVersion="1.5.0.0"; Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0, 2.5.0.0)"; ExpectedVersion="2.0.0.0"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -Name "Fabrikam-ServerScript" -Version $Version -Repository $TestGalleryName
        $res.Name | Should -Be "Fabrikam-ServerScript"
        $res.Version | Should -Be $ExpectedVersion
    }

    # Purpose: not find resources with invalid version
    #
    # Action: Find-PSResource -Name "Fabrikam-ServerScript" -Version "(1.5.0.0)"
    #
    # Expected Result: should not return a resource
    It "not find resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version="(1.5.0.0)";       Description="exlcusive version (2.5.0.0)"},
        @{Version="[1-5-0-0]";       Description="version formatted with invalid delimiter"},
        @{Version="[1.*.0]";         Description="version with wilcard in middle"},
        @{Version="[*.5.0.0]";       Description="version with wilcard at start"},
        @{Version="[1.*.0.0]";       Description="version with wildcard at second digit"},
        @{Version="[1.5.*.0]";       Description="version with wildcard at third digit"}
        @{Version="[1.5.0.*";        Description="version with wildcard at end"},
        @{Version="[1..0.0]";        Description="version with missing digit in middle"},
        @{Version="[1.5.0.]";        Description="version with missing digit at end"},
        @{Version="[1.5.0.0.0]";     Description="version with more than 4 digits"}
    )
    {
        param($Version, $Description)
        $res = Find-PSResource -Name "Fabrikam-ServerScript" -Version $Version -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find resources when given Name, Version not null --> '*'
    #
    # Action: Find-PSResource -Name ContosoServer -Version "*" -Repository PoshTestGallery
    #
    # Expected Result: returns 4 ContosoServer resources (of all versions in descending order)
    It "find resources when given Name, Version not null --> '*'" {
        $res = Find-PSResource -Name "Fabrikam-ServerScript" -Version "*" -Repository $TestGalleryName
        $res.Count | Should -BeGreaterOrEqual 5
    }

    # Purpose: not find script resource when given ModuleName and no Version parameter
    #
    # Action: Find-PSResource -ModuleName "Fabrikam-ServerScript" -Repository PoshTestGallery
    #
    # Expected Result: not find a script resource when given ModuleName
    It "not find script resource when given ModuleName" {
        $res = Find-PSResource -ModuleName "Fabrikam-ServerScript" -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }

    # Purpose: not find script resource when given ModuleName and any Version parameter
    #
    # Action: Find-PSResource -Name "Fabrikam-ServerScript" -Version [2.0.0.0] -Repository "PoshTestGallery"
    #
    # Expected Result: should not find a script resource when given ModuleName and any version value
    It "not find script resource when given ModuleName to <Reason>" -TestCases @(
        @{Version="[2.0.0.0]";          Reason="validate version, exact match"},
        @{Version="2.0.0.0";            ExpectedVersion="2.0.0.0"; Reason="validate version, exact match without bracket syntax"},
        @{Version="[1.0.0.0, 2.5.0.0]"; Reason="validate version, exact range inclusive"},
        @{Version="(1.0.0.0, 2.5.0.0)"; Reason="validate version, exact range exclusive"},
        @{Version="(1.0.0.0,)";         Reason="validate version, minimum version exclusive"},
        @{Version="[1.0.0.0,)";         Reason="validate version, minimum version inclusive"},
        @{Version="(,1.5.0.0)";         Reason="validate version, maximum version exclusive"},
        @{Version="(,1.5.0.0]";         Reason="validate version, maximum version inclusive"},
        @{Version="[1.0.0.0, 2.5.0.0)"; Reason="validate version, mixed inclusive minimum and exclusive maximum version"}
    ) {
        param($Version, $ExpectedVersion)
        $res = Find-PSResource -ModuleName "Fabrikam-ServerScript" -Version $Version -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }

    # Purpose: not find resource with given ModuleName and wildcard Version -> '*'
    #
    # Action: Find-PSResource -ModuleName Fabrikam-ServerScript -Version "*" -Repository PoshTestGallery
    #
    # Expected Result: not return a resource
    It "not find script resource with specified ModuleName and range Version parameter -> '*' " {
        $res = Find-PSResource -ModuleName Fabrikam-ServerScript -Version "*" -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }

    # Purpose: find resource with latest version (including prerelease version) given Prerelease parameter
    #
    # Action: Find-PSResource -Name "test_script" -Prerelease
    #
    # Expected Result: should return latest version (may be a prerelease version)
    It "find resource with latest (including prerelease) version given Prerelease parameter" {
        # test_script resource's latest version is a prerelease version, before that it has a non-prerelease version
        $res = Find-PSResource -Name "test_script"
        $res.Version | Should -Be "2.5.0.0"

        $resPrerelease = Find-PSResource -Name "test_script" -Prerelease
        $resPrerelease.Version | Should -Be "3.0.0.0"        
    }
    
    # Purpose: not find un-available resource from specified repository, when given Repository parameter
    #
    # Action: Find-PSResource -Name Get-WindowsAutoPilotInfo -Repository PoshTestGallery
    #
    # Expected Result: should not find resource from specified PSGallery repository becuase resource doesn't exist there
    It "find resource from specified repository, when given Repository parameter" {
        $res = Find-PSResource -Name Get-WindowsAutoPilotInfo -Repository $TestGalleryName
        $res | Should -BeNullOrEmpty
    }
    
    # Purpose: find resource from specified repository, when given Repository parameter
    #
    # Action: Find-PSResource -Name Get-WindowsAutoPilotInfo -Repository PSGallery
    #
    # Expected Result: should return resource from specified repository where it exists
    It "find resource from specified repository, when given Repository parameter" {
        $res = Find-PSResource -Name Get-WindowsAutoPilotInfo -Repository $PSGalleryName
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "Get-WindowsAutoPilotInfo"
        $res.Repository | Should -Be "PSGallery"
    }

    # Purpose: find resource that exists in test and psgallery, given Repository parameter
    #
    # Action: Find-PSResource -Name "Connect-AzureVM" -Repository PSGallery
    #
    # Expected Result: find resource quicker when repository is specified
    It "find resource existing in multiple repositories given Repository parameter" {
        $repeat = 100
        $timeWithoutRepoSpecified = Measure-Command -Expression {
            for ($i = 0; $i -lt $repeat; $i++) {
                $res = Find-PSResource -Name "Connect-AzureVM"
                $res.Repository | Should -Be "PoshTestGallery"                
            }
        }

        $timeWithRepoSpecified = Measure-Command -Expression {
            for ($i = 0; $i -lt $repeat; $i++) {
                $res = Find-PSResource -Name "Connect-AzureVM" -Repository $TestGalleryName
                $res.Repository | Should -Be "PoshTestGallery" 
            }
        }

        $timeWithRepoSpecified | Should -BeLessOrEqual $timeWithoutRepoSpecified
    }

    # Purpose: find resource in first repository where it exists given Repository parameter
    #
    # Action: Find-PSResource "Connect-AzureVM"
    #         Find-PSResource "Connect-AzureVM" -Repository PSGallery
    #
    # Expected Result: Returns resource from first avaiable or specfied repository
    It "find Resource given repository parameter, where resource exists in multiple repos" {
        # first availability found in PoshTestGallery
        $res = Find-PSResource "Connect-AzureVM"
        $res.Repository | Should -Be "PoshTestGallery"

        # check that same resource can be returned from non-first-availability/non-default repo
        $resNonDefault = Find-PSResource "Connect-AzureVM" -Repository $PSGalleryName
        $resNonDefault.Repository | Should -Be "PSGallery"
    }

    # Purpose: find a resource but not its dependency resource(s)
    #
    # Action: Find-PSResources -Name Script-WithDependencies1
    #
    # Expected Result: should return resource but none of its dependency resources
    It "find resource and its dependency resource(s) with IncludeDependencies parameter" {
        $res = Find-PSResource -Name Script-WithDependencies1
        $res | Should -Not -BeNullOrEmpty
        $res.Name | Should -Be "Script-WithDependencies1"
    }

    # Purpose: find a resource and its dependency resource(s) with IncludeDependencies parameter
    #
    # Action: Find-PSResources -Name Script-WithDependencies1 -IncludeDependencies
    #
    # Expected Result: should return resource and all of its dependency resources
    It "find resource and its dependency resource(s) with IncludeDependencies parameter" {
        $res = Find-PSResource -Name Script-WithDependencies1 -IncludeDependencies
        $res | Should -Not -BeNullOrEmpty
        $res.Count | Should -BeGreaterOrEqual 9
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

        $Name = 'TestScriptName'
        $scriptFilePath = Join-Path -Path $script:TempModulesPath -ChildPath "$Name.ps1"
        $null = New-Item -Path $scriptFilePath -ItemType File -Force

        $version = "1.0.0"
        $params = @{
                    #Path = $scriptFilePath
                    Version = $version
                    #GUID = 
                    Author = 'Jane'
                    CompanyName = 'Microsoft Corporation'
                    Copyright = '(c) 2020 Microsoft Corporation. All rights reserved.'
                    Description = "Description for the $Name script"
                    LicenseUri = "https://$Name.com/license"
                    IconUri = "https://$Name.com/icon"
                    ProjectUri = "https://$Name.com"
                    Tags = @('Tag1','Tag2', "Tag-$Name-$version")
                    ReleaseNotes = "$Name release notes"
                    }

        $scriptMetadata = Create-PSScriptMetadata @params
        Set-Content -Path $scriptFilePath -Value $scriptMetadata

        Publish-PSResource -path $scriptFilePath -Repository psgettestlocal

        $res = Find-PSResource -Name $Name -Repository "psgettestlocal"
        $res | Should -Not -BeNullOrEmpty

        if($tempdir -and (Test-Path $tempdir))
        {
            Remove-Item $tempdir -Force -Recurse -ErrorAction SilentlyContinue
        }
    }
}
