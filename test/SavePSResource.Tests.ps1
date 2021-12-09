# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Save-PSResource for PSResources' {

    BeforeAll{
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        $NuGetGalleryName = Get-NuGetGalleryName
        $testModuleName = "test_module"
        $testModuleName2 = "TestModule"
        $testScriptName = "TestTestScript"
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
        Save-PSResource -Name $testModuleName2 -Repository $TestGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName2
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem $pkgDir.FullName).Count | Should -Be 1
    }

    It "Save specific script resource by name" {
        Save-PSResource -Name $testScriptName -Repository $TestGalleryName -Path $SaveDir
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
        Save-PSResource -Name NonExistentModule -Repository $TestGalleryName -Path $SaveDir -ErrorVariable err -ErrorAction SilentlyContinue
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "NonExistentModule"
        $pkgDir.Name | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ResourceNotFoundError,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }

    It "Not Save module with Name containing wildcard" {
        Save-PSResource -Name "TestModule*" -Repository $TestGalleryName -Path $SaveDir -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "NameContainsWildcard,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }

    # Do some version testing, but Find-PSResource should be doing thorough testing
    It "Should save resource given name and exact version" {
        Save-PSResource -Name $testModuleName2 -Version "1.2.0" -Repository $TestGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName2
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.2.0"
    }

    It "Should save resource given name and exact version with bracket syntax" {
        Save-PSResource -Name $testModuleName2 -Version "[1.2.0]" -Repository $TestGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName2
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.2.0"
    }

    It "Should save resource given name and exact range inclusive [1.0.0, 1.1.1]" {
        Save-PSResource -Name $testModuleName2 -Version "[1.0.0, 1.1.1]" -Repository $TestGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName2
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.1.1"
    }

    It "Should save resource given name and exact range exclusive (1.0.0, 1.1.1)" {
        Save-PSResource -Name $testModuleName2 -Version "(1.0.0, 1.1.1)" -Repository $TestGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName2
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.1"
    }

    It "Should not save resource with incorrectly formatted version such as <Description>" -TestCases @(
        @{Version='(1.2.0.0)';       Description="exclusive version (2.10.0.0)"},
        @{Version='[1-2-0-0]';       Description="version formatted with invalid delimiter [1-2-0-0]"}
    ) {
        param($Version, $Description)

        Save-PSResource -Name $testModuleName2 -Version $Version -Repository $TestGalleryName -Path $SaveDir -ErrorVariable err -ErrorAction SilentlyContinue
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName2
        $pkgDir | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ResourceNotFoundError,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }

    It "Save resource when given Name, Version '*', should install the latest version" {
        Save-PSResource -Name $testModuleName2 -Version "*" -Repository $TestGalleryName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName2
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

    It "Save a module with a dependency and skip saving the dependency" {
        Save-PSResource -Name "PSGetTestModule" -Prerelease -Repository $TestGalleryName -Path $SaveDir -SkipDependencyCheck
        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq "PSGetTestModule" -or $_.Name -eq "PSGetTestDependency1" }
        $pkgDirs.Count | Should -Be 1
        (Get-ChildItem $pkgDirs[0].FullName).Count | Should -Be 1
    }

    It "Save resource via InputObject by piping from Find-PSresource" {
        Find-PSResource -Name $testModuleName2 -Repository $TestGalleryName | Save-PSResource -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName2
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.3.0"
    }

    It "Save resource should not prompt 'trust repository' if repository is not trusted but -TrustRepository is used" {
        try {
            Set-PSResourceRepository PoshTestGallery -Trusted:$false
            Save-PSResource -Name $testModuleName2 -Repository $TestGalleryName -TrustRepository -Path $SaveDir
            $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName2
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
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName2
        $pkgDir | Should -Not -BeNullOrEmpty 
        (Get-ChildItem -Path $pkgDir.FullName).Count | Should -Be 1
    }

    It "Save PSResourceInfo object piped in" {
        Find-PSResource -Name $testModuleName2 -Version "1.1.0.0" -Repository $TestGalleryName | Save-PSResource -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName2
        $pkgDir | Should -Not -BeNullOrEmpty 
        (Get-ChildItem -Path $pkgDir.FullName).Count | Should -Be 1   
    }

    It "Save PSResourceInfo object piped in for prerelease version object" {
        Find-PSResource -Name $testModuleName -Version "4.5.2-alpha001" -Repository $TestGalleryName | Save-PSResource -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem -Path $pkgDir.FullName).Count | Should -Be 1   
    }

    It "Save module as a nupkg" {
        Save-PSResource -Name $testModuleName2 -Version "1.3.0" -Repository $TestGalleryName -Path $SaveDir -AsNupkg
        write-host $SaveDir
        write-host 
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "testmodule.1.3.0.nupkg"
        $pkgDir | Should -Not -BeNullOrEmpty
    }

    It "Save script as a nupkg" {
        Save-PSResource -Name $testScriptName -Version "1.3.1" -Repository $TestGalleryName -Path $SaveDir -AsNupkg
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "testtestscript.1.3.1.nupkg"
        $pkgDir | Should -Not -BeNullOrEmpty
    }

    It "Save module and include XML metadata file" {
        Save-PSResource -Name $testModuleName2 -Version "1.3.0" -Repository $TestGalleryName -Path $SaveDir -IncludeXML
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName2
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.3.0"
        $xmlFile = Get-ChildItem -Path $pkgDirVersion.FullName | Where-Object Name -eq "PSGetModuleInfo.xml"
        $xmlFile | Should -Not -BeNullOrEmpty
    }

    It "Save module using -PassThru" {
        $res = Save-PSResource -Name $testModuleName2 -Version "1.3.0" -Repository $TestGalleryName -Path $SaveDir -PassThru
        $res.Name | Should -Be $testModuleName2
        $res.Version | Should -Be "1.3.0.0"
    }
<#
    # Tests should not write to module directory
    It "Save specific module resource by name if no -Path param is specifed" {
        Save-PSResource -Name $testModuleName2 -Repository $TestGalleryName
        $pkgDir = Get-ChildItem -Path . | Where-Object Name -eq $testModuleName2
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

        Install-PSResource -Name $testModuleName2 -Repository $TestGalleryName -confirm:$false
        
        $pkg = Get-Module $testModuleName2 -ListAvailable
        $pkg.Name | Should -Be $testModuleName2

        Set-PSResourceRepository PoshTestGallery -Trusted
    }
#>
}