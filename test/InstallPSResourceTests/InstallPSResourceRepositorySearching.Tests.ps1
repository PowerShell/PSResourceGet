# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"

Describe 'Test Install-PSResource for searching and looping through repositories' -tags 'CI' {

    BeforeAll{
        $testModuleName = "test_module"
        $testModule2Name = "test_module2"
        $testLocalModuleName = "test_local_mod"
        $nonExistantModule = "NonExistantModule"
        $testScriptName = "test_script"
        $PSGalleryName = "PSGallery"
        $NuGetGalleryName = "NuGetGallery"
        $localRepoName = "localRepo"

        Get-NewPSResourceRepositoryFile

        $localRepoUriAddress = Join-Path -Path $TestDrive -ChildPath "testdir"
        $null = New-Item $localRepoUriAddress -ItemType Directory -Force
        Register-PSResourceRepository -Name $localRepoName -Uri $localRepoUriAddress

        New-TestModule -moduleName $testModuleName -repoName localRepo -packageVersion "1.0.0" -prereleaseLabel "" -tags @()
    }
    AfterEach {
        Uninstall-PSResource $testModuleName, $testModule2Name, $testLocalModuleName, $testScriptName, "RequiredModule1", "RequiredModule2", "RequiredModule3", "RequiredModule4", "RequiredModule5" -Version "*" -SkipDependencyCheck -ErrorAction SilentlyContinue
    }
    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "install resources from higest priority repository where it exists (without -Repository specified)" {
        # Package "test_module" exists in the following repositories (in this order): localRepo, PSGallery, NuGetGallery
        $res = Install-PSResource -Name $testModuleName -TrustRepository -SkipDependencyCheck -ErrorVariable err -ErrorAction SilentlyContinue -PassThru
        $err | Should -HaveCount 0

        $res | Should -Not -BeNullOrEmpty
        $res.Repository | Should -Be $localRepoName
    }

    It "install resources from hightest priority repository where it exists and not write errors for repositories where it does not exist (without -Repository specified)" {
        # Package "test_script" exists in the following repositories: PSGallery, NuGetGallery
        Install-PSResource -Name $testScriptName -TrustRepository -SkipDependencyCheck -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0

        $res = Get-InstalledPSResource $testScriptName
        $res | Should -Not -BeNullOrEmpty
        $res.Repository | Should -Be $PSGalleryName
    }

    It "should install resources that exist and not install ones that do not exist while reporting error (without -Repository specified)" {
        Install-PSResource -Name $testScriptName,"NonExistantModule" -TrustRepository -SkipDependencyCheck -ErrorVariable err -ErrorAction SilentlyContinue
        $err[0].FullyQualifiedErrorId | Should -BeExactly "InstallPackageFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"

        $res = Get-InstalledPSResource $testScriptName
        $res | Should -Not -BeNullOrEmpty
        $res.Repository | Should -Be $PSGalleryName
    }

    It "should not install resource given nonexistant Name (without -Repository specified)" {
        Install-PSResource -Name "NonExistantModule" -TrustRepository -SkipDependencyCheck -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "InstallPackageFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"
    }

    It "install multiple resources from highest priority repository where it exists (without -Repository specified)" {
        $res = Install-PSResource -Name "test_module","test_module2" -TrustRepository -SkipDependencyCheck -ErrorVariable err -ErrorAction SilentlyContinue -PassThru
        $err | Should -HaveCount 0
        $res | Should -Not -BeNullOrEmpty

        $pkg1 = $res[0]
        $pkg1.Name | Should -Be $testModuleName
        $pkg1.Repository | Should -Be $localRepoName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be $testModule2Name
        $pkg2.Repository | Should -Be $PSGalleryName
    }

    It "install resources from highest pattern matching repository where it exists (-Repository with wildcard)" {
        # Package "test_script" exists in the following repositories: PSGallery, NuGetGallery
        Install-PSResource -Name $testScriptName -Repository "*Gallery" -TrustRepository -SkipDependencyCheck -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0

        $res = Get-InstalledPSResource $testScriptName
        $res | Should -Not -BeNullOrEmpty
        $res.Repository | Should -Be $PSGalleryName
    }

    It "should not allow for repository name with wildcard and non-wildcard name specified in same command run" {
        { Install-PSResource -Name $testModuleName -Repository "*Gallery",$localRepoName } | Should -Throw -ErrorId "RepositoryNamesWithWildcardsAndNonWildcardUnsupported,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"
    }
    
    It "not install resource and write error if resource does not exist in any pattern matching repositories (-Repository with wildcard)" {
        Install-PSResource -Name "nonExistantModule" -Repository "*Gallery" -TrustRepository -SkipDependencyCheck -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "InstallPackageFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"
    }

    It "install resource from single specific repository (-Repository with single non-wildcard value)" {
        $res = Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -SkipDependencyCheck -PassThru
        $res | Should -HaveCount 1
        $res.Name | Should -Be $testModuleName
        $res.Repository | Should -Be $PSGalleryName
    }

    It "not install resource if it does not exist in repository and write error (-Repository with single non-wildcard value)" {
        Install-PSResource -Name $nonExistantModule -Repository $PSGalleryName -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "InstallPackageFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"
    }

    It "install resource from highest priority repository where it exists (-Repository with multiple non-wildcard values)" {
        $res = Install-PSResource -Name $testModuleName -Repository $PSGalleryName,$NuGetGalleryName -SkipDependencyCheck -TrustRepository -PassThru
        $res | Should -HaveCount 1

        $res.Name | Should -Be $testModuleName
        $res.Repository | Should -Be $PSGalleryName
    }

    It "should not allow for repository name with wildcard and non-wildcard name specified in same command run" {
        {Install-PSResource -Name $testModuleName -Repository "*Gallery",$localRepoName} | Should -Throw -ErrorId "RepositoryNamesWithWildcardsAndNonWildcardUnsupported,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"
    }

    It "should not write error when package to install is already installed and -reinstall is not provided" {
        $res = Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository -SkipDependencyCheck -PassThru
        $res | Should -HaveCount 1

        $resReinstall = Install-PSResource -Name $testModuleName -Repository $PSGalleryName -SkipDependencyCheck -TrustRepository -PassThru -ErrorVariable err -ErrorAction SilentlyContinue -WarningVariable warningVar -WarningAction SilentlyContinue
        $resReinstall | Should -BeNullOrEmpty

        $err | Should -HaveCount 0
        $warningVar | Should -Not -BeNullOrEmpty
    }
}
