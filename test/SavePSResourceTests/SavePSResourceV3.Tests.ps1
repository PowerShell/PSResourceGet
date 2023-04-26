# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

Describe 'Test HTTP Save-PSResource for V3 Server Protocol' -tags 'CI' {

    BeforeAll {
        $NuGetGalleryName = Get-NuGetGalleryName
        $testModuleName = "test_module"
        $testModuleName2 = "test_module2"
        Get-NewPSResourceRepositoryFile

        $SaveDir = Join-Path $TestDrive 'SavedResources'
        New-Item -Item Directory $SaveDir -Force
    }

    AfterEach {
        # Delete contents of save directory
        Remove-Item -Path (Join-Path $SaveDir '*') -Recurse -Force -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Save specific module resource by name" {
        Save-PSResource -Name $testModuleName -Repository $NuGetGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem $pkgDir.FullName) | Should -HaveCount 1
    }

    It "Save multiple resources by name" {
        $pkgNames = @($testModuleName, $testModuleName2)
        Save-PSResource -Name $pkgNames -Repository $NuGetGalleryName -Path $SaveDir -TrustRepository
        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq $testModuleName -or $_.Name -eq $testModuleName2 }
        $pkgDirs | Should -HaveCount 2
        (Get-ChildItem $pkgDirs[0].FullName) | Should -HaveCount 1
        (Get-ChildItem $pkgDirs[1].FullName) | Should -HaveCount 1
    }

    It "Should not save resource given nonexistant name" {
        Save-PSResource -Name NonExistentModule -Repository $NuGetGalleryName -Path $SaveDir -ErrorVariable err -ErrorAction SilentlyContinue -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "NonExistentModule"
        $pkgDir.Name | Should -BeNullOrEmpty
    }

    It "Not Save module with Name containing wildcard" {
        Save-PSResource -Name "TestModule*" -Repository $NuGetGalleryName -Path $SaveDir -ErrorVariable err -ErrorAction SilentlyContinue -TrustRepository
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "NameContainsWildcard,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }

    It "Should save resource given name and exact version" {
        Save-PSResource -Name $testModuleName -Version "1.0.0" -Repository $NuGetGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.0.0"
    }

    It "Should save resource given name and exact version with bracket syntax" {
        Save-PSResource -Name $testModuleName -Version "[1.0.0]" -Repository $NuGetGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.0.0"
    }

    It "Should save resource given name and exact range inclusive [1.0.0, 3.0.0]" {
        Save-PSResource -Name $testModuleName -Version "[1.0.0, 3.0.0]" -Repository $NuGetGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "3.0.0"
    }

    It "Should save resource given name and exact range exclusive (1.0.0, 5.0.0)" {
        Save-PSResource -Name $testModuleName -Version "(1.0.0, 5.0.0)" -Repository $NuGetGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "3.0.0"
    }

    It "Should not save resource with incorrectly formatted version such as exclusive version (1.0.0.0)" {
        $Version="(1.0.0.0)"
        try {
            Save-PSResource -Name $testModuleName -Version $Version -Repository $NuGetGalleryName -Path $SaveDir -ErrorAction SilentlyContinue -TrustRepository
        }
        catch
        {}

        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -BeNullOrEmpty
        $Error.Count | Should -BeGreaterThan 0
        $Error[0].FullyQualifiedErrorId  | Should -Be "IncorrectVersionFormat,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }

    It "Save resource with latest (including prerelease) version given Prerelease parameter" {
        Save-PSResource -Name $testModuleName -Prerelease -Repository $NuGetGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "5.2.5"
    }

### TODO:  this is broken because the "Prerelease" parameter is a boolean, but the type from
### the input object is of type string (ie "true").
<#
    It "Save PSResourceInfo object piped in for prerelease version object" {
        $test = Find-PSResource -Name $testModuleName -Version "5.2.5-alpha001" -Repository $NuGetGalleryName 
        Write-Host "Test Output V3: $($test.Name)"
        Write-Host "Test Output V3: $($test.Version.ToString())"
        Write-Host "Test Output V3: $($test.Prerelease)"


        $test | Save-PSResource -Path $SaveDir -TrustRepository -Verbose
        $saveOutput = Get-ChildItem -Path $SaveDir
        Write-Host "Save Output V3: $saveOutput"

        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem -Path $pkgDir.FullName) | Should -HaveCount 1   
    }
#>
    It "Save module as a nupkg" {
        Save-PSResource -Name $testModuleName -Version "1.0.0" -Repository $NuGetGalleryName -Path $SaveDir -AsNupkg -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "test_module.1.0.0.nupkg"
        $pkgDir | Should -Not -BeNullOrEmpty
    }

    It "Save module and include XML metadata file" {
        Save-PSResource -Name $testModuleName -Version "1.0.0" -Repository $NuGetGalleryName -Path $SaveDir -IncludeXml -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.0.0"
        $xmlFile = Get-ChildItem -Path $pkgDirVersion.FullName | Where-Object Name -eq "PSGetModuleInfo.xml"
        $xmlFile | Should -Not -BeNullOrEmpty
    }

    It "Save module using -PassThru" {
        $res = Save-PSResource -Name $testModuleName -Version "1.0.0" -Repository $NuGetGalleryName -Path $SaveDir -PassThru -TrustRepository
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "1.0.0"
    }

    # Save module that is not authenticode signed
    # Should FAIL to save the module
    It "Save module that is not authenticode signed" -Skip:(!(Get-IsWindows)) {
        Save-PSResource -Name $testModuleName -Version "5.0.0" -AuthenticodeCheck -Repository $NuGetGalleryName -TrustRepository -Path $SaveDir -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "InstallPackageFailure,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }
}
