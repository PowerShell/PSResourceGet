# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Save-PSResource for PSResources' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        $NuGetGalleryName = Get-NuGetGalleryName
        Get-NewPSResourceRepositoryFile
        Register-LocalRepos

       $SaveDir = Join-Path $TestDrive 'SavedResources'
       New-Item -Item Directory $SaveDir -Force
    }

    AfterEach {
        # Delte contents of save directory
        Remove-Item -Path (Join-Path $SaveDir '*') -Recurse -Force -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Save specific module resource by name" {
        Save-PSResource -Name "TestModule" -Repository $TestGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "TestModule"
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem $pkgDir.FullName).Count | Should -Be 1
    }

    It "Save specific script resource by name" {
        Save-PSResource -Name "TestTestScript" -Repository $TestGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "TestTestScript.ps1"
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem $pkgDir.FullName).Count | Should -Be 1
    }

    It "Save multiple resources by name" {
        $pkgNames = @("TestModule","TestModule99")
        Save-PSResource -Name $pkgNames -Repository $TestGalleryName -Path $SaveDir
        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq "TestModule" -or $_.Name -eq "TestModule99" }
        $pkgDirs.Count | Should -Be 2
        (Get-ChildItem $pkgDirs[0].FullName).Count | Should -Be 1
        (Get-ChildItem $pkgDirs[1].FullName).Count | Should -Be 1
    }

    It "Should not save resource given nonexistant name" {
        Save-PSResource -Name NonExistentModule -Repository $TestGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "NonExistentModule"
        $pkgDir.Name | Should -BeNullOrEmpty
    }

    It "Not Save module with Name containing wildcard" {
        Save-PSResource -Name "TestModule*" -Repository $TestGalleryName -Path $SaveDir -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "NameContainsWildcard,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }

    # Do some version testing, but Find-PSResource should be doing thorough testing
    It "Should save resource given name and exact version" {
        Save-PSResource -Name "TestModule" -Version "1.2.0" -Repository $TestGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "TestModule"
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.2.0"
    }

    It "Should save resource given name and exact version with bracket syntax" {
        Save-PSResource -Name "TestModule" -Version "[1.2.0]" -Repository $TestGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "TestModule"
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.2.0"
    }

    It "Should save resource given name and exact range inclusive [1.0.0, 1.1.1]" {
        Save-PSResource -Name "TestModule" -Version "[1.0.0, 1.1.1]" -Repository $TestGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "TestModule"
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.1.1"
    }

    It "Should save resource given name and exact range exclusive (1.0.0, 1.1.1)" {
        Save-PSResource -Name "TestModule" -Version "(1.0.0, 1.1.1)" -Repository $TestGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "TestModule"
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.1"
    }

    It "Should not save resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(1.2.0.0)';       Description="exclusive version (2.10.0.0)"},
        @{Version='[1-2-0-0]';       Description="version formatted with invalid delimiter [1-2-0-0]"}
    ) {
        param($Version, $Description)

        Save-PSResource -Name "TestModule" -Version $Version -Repository $TestGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "TestModule"
        $pkgDir | Should -BeNullOrEmpty
    }

    It "Save resource when given Name, Version '*', should install the latest version" {
        Save-PSResource -Name "TestModule" -Version "*" -Repository $TestGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "TestModule"
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.3.0"
    }

    It "Save resource with latest (including prerelease) version given Prerelease parameter" {
        Save-PSResource -Name "TestModulePrerelease" -Prerelease -Repository $TestGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "TestModulePrerelease"
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "0.0.1"
    }

    It "Save a module with a dependency" {
        Save-PSResource -Name "PSGetTestModule" -Prerelease -Repository $TestGalleryName -Path $SaveDir
        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq "PSGetTestModule" -or $_.Name -eq "PSGetTestDependency1" }
        $pkgDirs.Count | Should -Be 2
        (Get-ChildItem $pkgDirs[0].FullName).Count | Should -Be 1
        (Get-ChildItem $pkgDirs[1].FullName).Count | Should -Be 1
    }

    It "Save resource via InputObject by piping from Find-PSresource" {
        Find-PSResource -Name "TestModule" -Repository $TestGalleryName | Save-PSResource -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "TestModule"
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.3.0"
    }

    It "Save resource should not prompt 'trust repository' if repository is not trusted but -TrustRepository is used" {
        try {
            Set-PSResourceRepository PoshTestGallery -Trusted:$false
            Save-PSResource -Name "TestModule" -Repository $TestGalleryName -TrustRepository -Path $SaveDir
            $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "TestModule"
            $pkgDir | Should -Not -BeNullOrEmpty
            $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
            $pkgDirVersion.Name | Should -Be "1.3.0"
        }
        finally {
            Set-PSResourceRepository PoshTestGallery -Trusted
        }
    }

    It "Save resource from local repository given Repository parameter" {
        $publishModuleName = "TestFindModule"
        $repoName = "psgettestlocal"
        Get-ModuleResourcePublishedToLocalRepoTestDrive $publishModuleName $repoName
        Set-PSResourceRepository "psgettestlocal" -Trusted:$true

        Save-PSResource -Name $publishModuleName -Repository $repoName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $publishModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem -Path $pkgDir.FullName).Count | Should -Be 1
    }

    It "Save specific module resource by name when no repository is specified" {
        Set-PSResourceRepository "PoshTestGallery" -Trusted:$True
        Set-PSResourceRepository "PSGallery" -Trusted:$True
        Set-PSResourceRepository "psgettestlocal2" -Trusted:$True

        Save-PSResource -Name "TestModule" -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "TestModule"
        $pkgDir | Should -Not -BeNullOrEmpty 
        (Get-ChildItem -Path $pkgDir.FullName).Count | Should -Be 1
    }

<#
    # Tests should not write to module directory
    It "Save specific module resource by name if no -Path param is specifed" {
        Save-PSResource -Name "TestModule" -Repository $TestGalleryName
        $pkgDir = Get-ChildItem -Path . | Where-Object Name -eq "TestModule"
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem $pkgDir.FullName).Count | Should -Be 1

        # Delete all files and subdirectories in the current , but keep the directory $SaveDir
        if (Test-Path -Path $pkgDir.FullName) {
            Remove-Item -Path $pkgDir.FullName -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
#>

<#
    # This needs to be manually tested due to prompt
    It "Install resource should prompt 'trust repository' if repository is not trusted" {
        Set-PSResourceRepository PoshTestGallery -Trusted:$false

        Install-PSResource -Name "TestModule" -Repository $TestGalleryName -confirm:$false
        
        $pkg = Get-Module "TestModule" -ListAvailable
        $pkg.Name | Should -Be "TestModule" 

        Set-PSResourceRepository PoshTestGallery -Trusted
    }
#>
}