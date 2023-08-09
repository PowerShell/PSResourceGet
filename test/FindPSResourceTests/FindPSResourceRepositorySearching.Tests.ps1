# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"

Describe 'Test Find-PSResource for searching and looping through repositories' -tags 'CI' {

    BeforeAll{
        $testModuleName = "test_module"
        $testLocalModuleName = "test_local_mod"
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

    It "should find resources that exist and not find ones that do not exist while reporting error (without -Repository specified)" {
        $res = Find-PSResource -Name $testScriptName,"NonExistantModule" -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 1
        $res | Should -HaveCount 2
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"

        $pkg1 = $res[0]
        $pkg1.Name | Should -Be $testScriptName
        $pkg1.Repository | Should -Be $PSGalleryName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be $testScriptName
        $pkg2.Repository | Should -Be $NuGetGalleryName
    }

    It "should not find resource given nonexistant Name (without -Repository specified)" {
        $res = Find-PSResource -Name "NonExistantModule" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
        $res | Should -BeNullOrEmpty
    }

    It "find multiple resources from all repositories where it exists (without -Repository specified)" {
        $res = Find-PSResource -Name "test_module","test_module2" -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0
        $res | Should -HaveCount 5

        $pkg1 = $res[0]
        $pkg1.Name | Should -Be "test_module"
        $pkg1.Repository | Should -Be $localRepoName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be "test_module"
        $pkg2.Repository | Should -Be $PSGalleryName

        $pkg3 = $res[2]
        $pkg3.Name | Should -Be "test_module2"
        $pkg3.Repository | Should -Be $PSGalleryName

        $pkg4 = $res[3]
        $pkg4.Name | Should -Be "test_module"
        $pkg4.Repository | Should -Be $NuGetGalleryName

        $pkg5 = $res[4]
        $pkg5.Name | Should -Be "test_module2"
        $pkg5.Repository | Should -Be $NuGetGalleryName
    }

    It "find multiple resources from all repositories where it exists where package Name contains wildcard (without -Repository specified)" {
        $res = Find-PSResource -Name "test_module*" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 9
        $err | Should -HaveCount 0

        $pkgFoundinLocalRepo = $false
        $pkgFoundinPSGallery = $false
        $pkgFoundinNuGetGallery = $false
        foreach ($pkg in $res)
        {
            if ($pkg.Repository -eq $localRepoName)
            {
                $pkgFoundinLocalRepo = $true
            }
            elseif ($pkg.Repository -eq $PSGalleryName) {
                $pkgFoundinPSGallery = $true
            }
            elseif ($pkg.Repository -eq $NuGetGalleryName)
            {
                $pkgFoundinNuGetGallery = $true
            }
        }

        $pkgFoundinLocalRepo | Should -BeTrue
        $pkgFoundinPSGallery | Should -BeTrue
        $pkgFoundinNuGetGallery | Should -BeTrue
    }

    It "should not find resources if they do not exist in any repository and not write error given package Name contains wildcard (without -Repository specified)" {
        $res = Find-PSResource -Name "NonExistantPkg*" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 0
        $err | Should -HaveCount 0
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

    It "find resources from pattern matching repositories where it exists and error report for specific repositories (-Repository with wildcard and specific repositories)" -Pending {
        # Package "test_script" exists in the following repositories: PSGallery, NuGetGallery
        $res = Find-PSResource -Name $testScriptName -Repository "*Gallery",$localRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 1
        $res | Should -HaveCount 2
        $pkg1 = $res[0]
        $pkg1.Name | Should -Be $testScriptName
        $pkg1.Repository | Should -Be $PSGalleryName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be $testScriptName
        $pkg2.Repository | Should -Be $NuGetGalleryName

        $err.Count | Should -Be 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "not find resources from pattern matching repositories if it doesn't exist and only write for for specific repositories (-Repository with wildcard and specific repositories)" -Pending {
        # Package "nonExistantPkg" does not exist in any repo
        $res = Find-PSResource -Name "nonExistantPkg" -Repository "*Gallery",$localRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 1
        $res | Should -HaveCount 2
        $pkg1 = $res[0]
        $pkg1.Name | Should -Be $testScriptName
        $pkg1.Repository | Should -Be $PSGalleryName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be $testScriptName
        $pkg2.Repository | Should -Be $NuGetGalleryName

        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "should not allow for repository name with wildcard and non-wildcard name specified in same command run" {
        {Find-PSResource -Name "test_module" -Repository "*Gallery",$localRepoName} | Should -Throw -ErrorId "ErrorFilteringNamesForUnsupportedWildcards,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }
    
    It "not find resource and write error if resource does not exist in any pattern matching repositories (-Repository with wildcard)" {
        $res = Find-PSResource -Name "nonExistantPkg" -Repository "*Gallery" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "not find resource that does not exist in any repository and not write error given package Name with wildcards (-Repository with wildcard)" {
        $res = Find-PSResource -Name "NonExistantPkg*" -Repository "*Gallery" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 0
        $err | Should -HaveCount 0
    }

    It "find resource from single specific repository (-Repository with single non-wildcard value)" {
        $res = Find-PSResource -Name $testModuleName -Repository $PSGalleryName
        $res | Should -HaveCount 1
        $res.Name | Should -Be $testModuleName
        $res.Repository | Should -Be $PSGalleryName
    }

    It "not find resource if it does not exist in repository and write error (-Repository with single non-wildcard value)" {
        $res = Find-PSResource -Name "NonExistantPkg" -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameConvertToPSResourceFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "not find resource if it does not exist in repository and not write error given package Name with wildcard (-Repository with single non-wildcard value)" -Pending {
        $res = Find-PSResource -Name "NonExistantPkg*" -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 0
        $err | Should -HaveCount 0
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

        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindNameConvertToPSResourceFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "should not find resource from repositories where it does not exist and not write error since package Name contains wilcard" -Pending {
        $res = Find-PSResource -Name "NonExistantPkg*" -Repository $PSGalleryName,$NuGetGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 0
        $err | Should -HaveCount 0
    }
}
