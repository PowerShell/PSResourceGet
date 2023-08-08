# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"

Describe 'Test Find-PSResource for local repositories' -tags 'CI' {

    BeforeAll{
        $testModuleName = "test_module"
        $testLocalModuleName = "test_local_mod"
        $testScriptName = "test_script"
        $PSGalleryName = "PSGallery"
        $NuGetGalleryName = "NuGetGallery"
        $localRepoName = "localRepo"

        # $testModuleName2 = "test_local_mod2"
        # $commandName = "cmd1"
        # $dscResourceName = "dsc1"
        # $prereleaseLabel = ""
        Get-NewPSResourceRepositoryFile

        $localRepoUriAddress = Join-Path -Path $TestDrive -ChildPath "testdir"
        $null = New-Item $localRepoUriAddress -ItemType Directory -Force
        Register-PSResourceRepository -Name $localRepoName -Uri $localRepoUriAddress

        

        # Register-LocalRepos

        # $localRepoUriAddress = Join-Path -Path $TestDrive -ChildPath "testdir"
        # $tagsEscaped = @("'Test'", "'Tag2'", "'$cmdName'", "'$dscName'")
        # $prereleaseLabel = "alpha001"

        New-TestModule -moduleName $testModuleName -repoName localRepo -packageVersion "1.0.0" -prereleaseLabel "" -tags @()
        # New-TestModule -moduleName $testLocalModuleName -repoName $localRepo -packageVersion "1.0.0" -prereleaseLabel "" -tags @()

        # New-TestModule -moduleName $testModuleName -repoName $localRepo -packageVersion "3.0.0" -prereleaseLabel "" -tags @() -dscResourceToExport $dscResourceName -commandToExport $commandName
        # New-TestModule -moduleName $testModuleName -repoName $localRepo -packageVersion "5.0.0" -prereleaseLabel "" -tags $tagsEscaped
        # New-TestModule -moduleName $testModuleName -repoName $localRepo -packageVersion "5.2.5" -prereleaseLabel $prereleaseLabel -tags $tagsEscaped

        # New-TestModule -moduleName $testModuleName2 -repoName $localRepo -packageVersion "5.0.0" -prereleaseLabel "" -tags $tagsEscaped
        # New-TestModule -moduleName $testModuleName2 -repoName $localRepo -packageVersion "5.2.5" -prereleaseLabel $prereleaseLabel -tags $tagsEscaped
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "find resources from all repositories where it exists (without -Repository specified)" {
        # Package "test_module" exists in the following repositories: PSGallery, NuGetGallery, and localRepo
        $res = Find-PSResource -Name $testModuleName -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0
        $res | Should -HaveCount 3
        $pkg1 = $res[0]
        $pkg1.Name | Should -Be $testModuleName
        $pkg1.Repository | Should -Be $localRepoName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be $testModuleName
        $pkg2.Repository | Should -Be $PSGalleryName
        
        $pkg3 = $res[2]
        $pkg3.Name | Should -Be $testModuleName
        $pkg3.Repository | Should -Be $NuGetGalleryName
    }

    It "find resources from all repositories where it exists and not write errors for repositories where it does not exist (without -Repository specified)" {
        # Package "test_script" exists in the following repositories: PSGallery, NuGetGallery
        $res = Find-PSResource -Name $testScriptName -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0
        $res | Should -HaveCount 2
        $pkg1 = $res[0]
        $pkg1.Name | Should -Be $testScriptName
        $pkg1.Repository | Should -Be $PSGalleryName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be $testScriptName
        $pkg2.Repository | Should -Be $NuGetGalleryName
    }

    It "find resources from all pattern matching repositories where it exists (-Repository with wildcard)" {
        # Package "test_script" exists in the following repositories: PSGallery, NuGetGallery
        $res = Find-PSResource -Name $testScriptName -Repository "*Gallery" -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0
        $res | Should -HaveCount 2
        $pkg1 = $res[0]
        $pkg1.Name | Should -Be $testScriptName
        $pkg1.Repository | Should -Be $PSGalleryName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be $testScriptName
        $pkg2.Repository | Should -Be $NuGetGalleryName
    }

    # It "find resources from pattern matching repositories where it exists and error report for specific repositories (-Repository with wildcard and specific repositories)" {
    #     # Package "test_script" exists in the following repositories: PSGallery, NuGetGallery
    #     $res = Find-PSResource -Name $testScriptName -Repository "*Gallery",$localRepoName -ErrorVariable err -ErrorAction SilentlyContinue
    #     $err | Should -HaveCount 1
    #     $res | Should -HaveCount 2
    #     $pkg1 = $res[0]
    #     $pkg1.Name | Should -Be $testScriptName
    #     $pkg1.Repository | Should -Be $PSGalleryName

    #     $pkg2 = $res[1]
    #     $pkg2.Name | Should -Be $testScriptName
    #     $pkg2.Repository | Should -Be $NuGetGalleryName

    #     $err.Count | Should -Be 1
    #     $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    # }

    # It "not find resources from pattern matching repositories if it doesn't exist and only write for for specific repositories (-Repository with wildcard and specific repositories)" {
    #     # Package "nonExistantPkg" does not exist in any repo
    #     # TODO: determine behavior
    #     $res = Find-PSResource -Name "nonExistantPkg" -Repository "*Gallery",$localRepoName -ErrorVariable err -ErrorAction SilentlyContinue
    #     $err | Should -HaveCount 1
    #     $res | Should -HaveCount 2
    #     $pkg1 = $res[0]
    #     $pkg1.Name | Should -Be $testScriptName
    #     $pkg1.Repository | Should -Be $PSGalleryName

    #     $pkg2 = $res[1]
    #     $pkg2.Name | Should -Be $testScriptName
    #     $pkg2.Repository | Should -Be $NuGetGalleryName

    #     $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    # }

    It "not find resource and write error if resource does not exist in any pattern matching repositories (-Repository with wildcard)" {
        $res = Find-PSResource -Name "nonExistantPkg" -Repository "*Gallery" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "find resource from single specific repository (-Repository with single non-wildcard value)" {
        $res = Find-PSResource -Name $testModuleName -Repository $PSGalleryName
        $res | Should -HaveCount 1
        $res.Name | Should -Be $testModuleName
        $res.Repository | Should -Be $PSGalleryName
    }

    It "not find resource if it does not exist in repository and write error (-Repository with single non-wildcard value)" {
        $res = Find-PSResource -Name "nonExistantPkg" -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "find resource from all repositories where it exists (-Repository with multiple non-wildcard values)" {
        $res = Find-PSResource -Name $testModuleName -Repository $PSGalleryName,$NuGetGalleryName
        $res | Should -HaveCount 2

        $pkg1 = $res[0]
        $pkg1.Name | Should -Be $testModuleName
        $pkg1.Repository | Should -Be $PSGalleryName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be $testModuleName
        $pkg2.Repository | Should -Be $NuGetGalleryName
    }

    It "find resource from all repositories where it exists and write errors for those it does not exist from (-Repository with multiple non-wildcard values)" {
        # Package "test_module3" exists in the following repositories: NuGetGalleryName
        $pkgOnNuGetGallery = "test_module3"
        $res = Find-PSResource -Name $pkgOnNuGetGallery -Repository $PSGalleryName,$NuGetGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 1
        $err | Should -HaveCount 1

        $pkg1 = $res[0]
        $pkg1.Name | Should -Be $pkgOnNuGetGallery
        $pkg1.Repository | Should -Be $NuGetGalleryName

        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }
}
