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
        Install-PSResource -Name $testModuleName -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0

        $res = Get-InstalledPSResource $testModuleName
        $res | Should -Not -BeNullOrEmpty
        $res.Repository | Should -Be $localRepoName
    }

    It "install resources from hightest priority repository where it exists and not write errors for repositories where it does not exist (without -Repository specified)" -Pending {
        # Package "test_script" exists in the following repositories: PSGallery, NuGetGallery
        $find = find-psresource -Name $testScriptName -Repository PSGallery
        write-host $($find.Name)
        Install-PSResource -Name $testScriptName -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0

        $res = Get-InstalledPSResource $testScriptName
        $res | Should -Not -BeNullOrEmpty
        $res.Repository | Should -Be $PSGalleryName
    }

    It "should install resources that exist and not find ones that do not exist while reporting error (without -Repository specified)" -Pending {
        Install-PSResource -Name $testScriptName,"NonExistantModule" -ErrorVariable err -ErrorAction SilentlyContinue
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"

        $res = Get-InstalledPSResource $testScriptName
        $res | Should -Not -BeNullOrEmpty
        $res.Repository | Should -Be $PSGalleryName
    }

    It "should not find resource given nonexistant Name (without -Repository specified)" -Pending {
        Install-PSResource -Name "NonExistantModule" -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "install multiple resources from highest priority repository where it exists (without -Repository specified)" -Pending {
        Install-PSResource -Name "test_module","test_module2" -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0

        $res = Get-InstalledPSResource $testModuleName, $testModule2Name
        $res | Should -Not -BeNullOrEmpty

        $pkg1 = $res[0]
        $pkg1.Name | Should -Be $testModuleName
        $pkg1.Repository | Should -Be $PSGalleryName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be $testModule2Name
        $pkg2.Repository | Should -Be $PSGalleryName
    }

    It "install resources from highest pattern matching repository where it exists (-Repository with wildcard)" {
        # Package "test_script" exists in the following repositories: PSGallery, NuGetGallery
        Install-PSResource -Name $testScriptName -Repository "*Gallery" -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0

        $res = Get-InstalledPSResource $testScriptName
        $res | Should -Not -BeNullOrEmpty
        $res.Repository | Should -Be $PSGalleryName
    }

    It "should not allow for repository name with wildcard and non-wildcard name specified in same command run" -Pending {
        { Install-PSResource -Name $testModuleName -Repository "*Gallery",$localRepoName } | Should -Throw -ErrorId "ErrorFilteringNamesForUnsupportedWildcards,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"
    }
    
    It "not install resource and write error if resource does not exist in any pattern matching repositories (-Repository with wildcard)" -Pending {
        Install-PSResource -Name "nonExistantModule" -Repository "*Gallery" -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "install resource from single specific repository (-Repository with single non-wildcard value)" {
        Install-PSResource -Name $testModuleName -Repository $PSGalleryName -TrustRepository

        $res = Get-InstalledPSResource $testModuleName
        $res | Should -HaveCount 1
        $res.Name | Should -Be $testModuleName
        $res.Repository | Should -Be $PSGalleryName
    }

    It "not install resource if it does not exist in repository and write error (-Repository with single non-wildcard value)" -Pending {
        Install-PSResource -Name $nonExistantModule -Repository $PSGalleryName -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "install resource from highest priority repository where it exists (-Repository with multiple non-wildcard values)" {
        $res = Find-PSResource -Name $testModuleName -Repository $PSGalleryName,$NuGetGalleryName
        $res | Should -HaveCount 2

        $pkg1 = $res[0]
        $pkg1.Name | Should -Be $testModuleName
        $pkg1.Repository | Should -Be $PSGalleryName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be $testModuleName
        $pkg2.Repository | Should -Be $NuGetGalleryName
    }

    It "should not allow for repository name with wildcard and non-wildcard name specified in same command run" {
        {Install-PSResource -Name $testModuleName -Repository "*Gallery",$localRepoName} | Should -Throw -ErrorId "RepositoryNamesWithWildcardsAndNonWildcardUnsupported,Microsoft.PowerShell.PSResourceGet.Cmdlets.InstallPSResource"
    }
}
