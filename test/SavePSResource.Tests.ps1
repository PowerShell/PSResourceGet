# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Save-PSResource for PSResources' {

    BeforeAll {
        $PSGalleryName = Get-PSGalleryName
        $NuGetGalleryName = Get-NuGetGalleryName
        $testModuleName = "test_module"
        $testScriptName = "test_script"
        $testModuleName2 = "testmodule99"
        $PackageManagement = "PackageManagement"
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
        Save-PSResource -Name $testModuleName -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem $pkgDir.FullName).Count | Should -Be 1
    }

    It "Save specific script resource by name" {
        Save-PSResource -Name $testScriptName -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "test_script.ps1"
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem $pkgDir.FullName).Count | Should -Be 1
    }

    It "Save multiple resources by name" {
        $pkgNames = @($testModuleName, $testModuleName2)
        Save-PSResource -Name $pkgNames -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq $testModuleName -or $_.Name -eq $testModuleName2 }
        $pkgDirs.Count | Should -Be 2
        (Get-ChildItem $pkgDirs[0].FullName).Count | Should -Be 1
        (Get-ChildItem $pkgDirs[1].FullName).Count | Should -Be 1
    }

    It "Should not save resource given nonexistant name" {
        Save-PSResource -Name NonExistentModule -Repository $PSGalleryName -Path $SaveDir -ErrorVariable err -ErrorAction SilentlyContinue -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "NonExistentModule"
        $pkgDir.Name | Should -BeNullOrEmpty
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ResourceNotFoundError,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }

    It "Not Save module with Name containing wildcard" {
        Save-PSResource -Name "TestModule*" -Repository $PSGalleryName -Path $SaveDir -ErrorVariable err -ErrorAction SilentlyContinue -TrustRepository
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "NameContainsWildcard,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }

    # Do some version testing, but Find-PSResource should be doing thorough testing
    It "Should save resource given name and exact version" {
        Save-PSResource -Name $testModuleName -Version "1.0.0" -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.0.0.0"
    }

    It "Should save resource given name and exact version with bracket syntax" {
        Save-PSResource -Name $testModuleName -Version "[1.0.0]" -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.0.0.0"
    }

    It "Should save resource given name and exact range inclusive [1.0.0, 3.0.0]" {
        Save-PSResource -Name $testModuleName -Version "[1.0.0, 3.0.0]" -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "3.0.0.0"
    }

    It "Should save resource given name and exact range exclusive (1.0.0, 5.0.0)" {
        Save-PSResource -Name $testModuleName -Version "(1.0.0, 5.0.0)" -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "3.0.0.0"
    }

    It "Should not save resource with incorrectly formatted version such as exclusive version (1.0.0.0)" {
        $Version="(1.0.0.0)"
        try {
            Save-PSResource -Name $testModuleName -Version $Version -Repository $PSGalleryName -Path $SaveDir -ErrorAction SilentlyContinue -TrustRepository
        }
        catch
        {}
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -BeNullOrEmpty
        $Error.Count | Should -Not -Be 0
        $Error[0].FullyQualifiedErrorId  | Should -Be "IncorrectVersionFormat,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }

    It "Should not save resource with incorrectly formatted version such as version formatted with invalid delimiter [1-0-0-0]"{
        $Version = "[1-0-0-0]"
        try {
            Save-PSResource -Name $testModuleName -Version $Version -Repository $PSGalleryName -Path $SaveDir -ErrorAction SilentlyContinue -TrustRepository
        }
        catch
        {}
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -BeNullOrEmpty
        $Error.Count | Should -Not -Be 0
        $Error[0].FullyQualifiedErrorId | Should -BeExactly "ResourceNotFoundError,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }

    It "Save resource when given Name, Version '*', should install the latest version" {
        Save-PSResource -Name $testModuleName -Version "*" -Repository $PSGalleryName -Path $SaveDir -ErrorAction SilentlyContinue -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "5.0.0.0"
    }

    It "Save resource with latest (including prerelease) version given Prerelease parameter" {
        Save-PSResource -Name $testModuleName -Prerelease -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "5.2.5"
    }

    It "Save a module with a dependency" {
        Save-PSResource -Name "TestModuleWithDependencyE" -Version "1.0.0.0" -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq "TestModuleWithDependencyE" -or $_.Name -eq "TestModuleWithDependencyC" -or $_.Name -eq "TestModuleWithDependencyB" -or $_.Name -eq "TestModuleWithDependencyD"}
        $pkgDirs.Count | Should -Be 4
        (Get-ChildItem $pkgDirs[0].FullName).Count | Should -Be 1
        (Get-ChildItem $pkgDirs[1].FullName).Count | Should -Be 1
        (Get-ChildItem $pkgDirs[2].FullName).Count | Should -Be 1
        (Get-ChildItem $pkgDirs[3].FullName).Count | Should -Be 1
    }

    It "Save a module with a dependency and skip saving the dependency" {
        Save-PSResource -Name "TestModuleWithDependencyE" -Version "1.0.0.0" -SkipDependencyCheck -Repository $PSGalleryName -Path $SaveDir -TrustRepository
        $pkgDirs = Get-ChildItem -Path $SaveDir | Where-Object { $_.Name -eq "TestModuleWithDependencyE"}
        $pkgDirs.Count | Should -Be 1
        (Get-ChildItem $pkgDirs[0].FullName).Count | Should -Be 1
    }

    It "Save resource via InputObject by piping from Find-PSresource" {
        Find-PSResource -Name $testModuleName -Repository $PSGalleryName | Save-PSResource -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "5.0.0.0"
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
        Set-PSResourceRepository "PSGallery" -Trusted:$True
        Set-PSResourceRepository "psgettestlocal2" -Trusted:$True

        Save-PSResource -Name $testModuleName -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty 
        (Get-ChildItem -Path $pkgDir.FullName).Count | Should -Be 1
    }

    It "Save PSResourceInfo object piped in" {
        Find-PSResource -Name $testModuleName -Version "1.0.0.0" -Repository $PSGalleryName | Save-PSResource -Path $SaveDir -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty 
        (Get-ChildItem -Path $pkgDir.FullName).Count | Should -Be 1   
    }

    It "Save PSResourceInfo object piped in for prerelease version object" {
        Find-PSResource -Name $testModuleName -Version "5.2.5-alpha001" -Repository $PSGalleryName | Save-PSResource -Path $SaveDir
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        (Get-ChildItem -Path $pkgDir.FullName).Count | Should -Be 1   
    }

    It "Save module as a nupkg" {
        Save-PSResource -Name $testModuleName -Version "1.0.0" -Repository $PSGalleryName -Path $SaveDir -AsNupkg -TrustRepository
        write-host $SaveDir
        write-host 
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "test_module.1.0.0.nupkg"
        $pkgDir | Should -Not -BeNullOrEmpty
    }

    It "Save script as a nupkg" {
        Save-PSResource -Name $testScriptName -Version "2.5.0" -Repository $PSGalleryName -Path $SaveDir -AsNupkg -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "test_script.2.5.0.nupkg"
        $pkgDir | Should -Not -BeNullOrEmpty
    }

    It "Save module and include XML metadata file" {
        Save-PSResource -Name $testModuleName -Version "1.0.0" -Repository $PSGalleryName -Path $SaveDir -IncludeXml -TrustRepository
        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $testModuleName
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.0.0.0"
        $xmlFile = Get-ChildItem -Path $pkgDirVersion.FullName | Where-Object Name -eq "PSGetModuleInfo.xml"
        $xmlFile | Should -Not -BeNullOrEmpty
    }

    It "Save module using -PassThru" {
        $res = Save-PSResource -Name $testModuleName -Version "1.0.0" -Repository $PSGalleryName -Path $SaveDir -PassThru -TrustRepository
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "1.0.0.0"
    }

    # Save module 1.4.3 (is authenticode signed and has catalog file)
    # Should save successfully 
    It "Save modules with catalog file using publisher validation" -Skip:(!(Get-IsWindows)) {
        Save-PSResource -Name $PackageManagement -Version "1.4.3" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository -Path $SaveDir

        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $PackageManagement
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.4.3" 
    }

    # Save module 1.4.7 (is authenticode signed and has NO catalog file)
    # Should save successfully 
    It "Save module with no catalog file" -Skip:(!(Get-IsWindows)) {
        Save-PSResource -Name $PackageManagement -Version "1.4.7" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository -Path $SaveDir

        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq $PackageManagement
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgDirVersion = Get-ChildItem -Path $pkgDir.FullName
        $pkgDirVersion.Name | Should -Be "1.4.7" 
    }

    # Save module that is not authenticode signed
    # Should FAIL to save the module
    It "Save module that is not authenticode signed" -Skip:(!(Get-IsWindows)) {
        { Save-PSResource -Name $testModuleName -Version "5.0.0" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository -Path $SaveDir } | Should -Throw -ErrorId "GetAuthenticodeSignatureError,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }

    # Save 1.4.4.1 (with incorrect catalog file)
    # Should FAIL to save the module
    It "Save module with incorrect catalog file" -Skip:(!(Get-IsWindows)) {
        { Save-PSResource -Name $PackageManagement -Version "1.4.4.1" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository -Path $SaveDir } | Should -Throw -ErrorId "TestFileCatalogError,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
    }

    # Save script that is signed
    # Should save successfully 
    It "Save script that is authenticode signed" -Skip:(!(Get-IsWindows)) {
        Save-PSResource -Name "Install-VSCode" -Version "1.4.2" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository -Path $SaveDir

        $pkgDir = Get-ChildItem -Path $SaveDir | Where-Object Name -eq "Install-VSCode.ps1" 
        $pkgDir | Should -Not -BeNullOrEmpty
        $pkgName = Get-ChildItem -Path $pkgDir.FullName
        $pkgName.Name | Should -Be "Install-VSCode.ps1" 
    }

    # Save script that is not signed
    # Should throw
    It "Save script that is not signed" -Skip:(!(Get-IsWindows)) {
        { Save-PSResource -Name "TestTestScript" -Version "1.3.1.1" -AuthenticodeCheck -Repository $PSGalleryName -TrustRepository } | Should -Throw -ErrorId "GetAuthenticodeSignatureError,Microsoft.PowerShell.PowerShellGet.Cmdlets.SavePSResource"
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