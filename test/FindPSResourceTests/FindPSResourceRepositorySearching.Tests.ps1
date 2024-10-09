# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"

Describe 'Test Find-PSResource for searching and looping through repositories' -tags 'CI' {

    BeforeAll{
        $testModuleName = "test_module"
        $testModuleName2 = "test_module2"
        $testCmdDSCParentPkg = "myCmdDSCModule"
        $testScriptName = "test_script"

        $tag1 = "CommandsAndResource"
        $tag2 = "Tag-Required-Script1-2.5"

        $cmdName = "Get-TargetResource"
        $dscName = "SystemLocale"
        $tagsEscaped = @("'$tag1'", "'PSCommand_$cmdName'", "'PSDscResource_$dscName'")

        $cmdName2 = "Get-MyCommand"
        $dscName2 = "MyDSCResource"
        $tagsEscaped2 = @("'PSCommand_$cmdName2'", "'PSDscResource_$dscName2'")

        $PSGalleryName = "PSGallery"
        $NuGetGalleryName = "NuGetGallery"
        $localRepoName = "localRepo"

        Get-NewPSResourceRepositoryFile

        $localRepoUriAddress = Join-Path -Path $TestDrive -ChildPath "testdir"
        $null = New-Item $localRepoUriAddress -ItemType Directory -Force
        Register-PSResourceRepository -Name $localRepoName -Uri $localRepoUriAddress

        New-TestModule -moduleName $testModuleName -repoName localRepo -packageVersion "1.0.0" -prereleaseLabel "" -tags $tagsEscaped
        New-TestModule -moduleName $testCmdDSCParentPkg -repoName localRepo -packageVersion "1.0.0" -prereleaseLabel "" -tags $tagsEscaped2
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    # For -Name based search
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
        $res = Find-PSResource -Name $testModuleName,$testModuleName2 -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0
        $res | Should -HaveCount 5

        $pkg1 = $res[0]
        $pkg1.Name | Should -Be $testModuleName
        $pkg1.Repository | Should -Be $localRepoName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be $testModuleName
        $pkg2.Repository | Should -Be $PSGalleryName

        $pkg3 = $res[2]
        $pkg3.Name | Should -Be $testModuleName2
        $pkg3.Repository | Should -Be $PSGalleryName

        $pkg4 = $res[3]
        $pkg4.Name | Should -Be $testModuleName
        $pkg4.Repository | Should -Be $NuGetGalleryName

        $pkg5 = $res[4]
        $pkg5.Name | Should -Be $testModuleName2
        $pkg5.Repository | Should -Be $NuGetGalleryName
    }

    It "find multiple resources from all repositories where it exists where package Name contains wildcard (without -Repository specified)" {
        $res = Find-PSResource -Name "test_module*" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 10
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
        {Find-PSResource -Name "test_module" -Repository "*Gallery",$localRepoName} | Should -Throw -ErrorId "RepositoryNamesWithWildcardsAndNonWildcardUnsupported,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
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
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
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

        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "should not find resource from repositories where it does not exist and not write error since package Name contains wilcard" -Pending {
        $res = Find-PSResource -Name "NonExistantPkg*" -Repository $PSGalleryName,$NuGetGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 0
        $err | Should -HaveCount 0
    }

    # For Tag based search
    It "find resources from all repositories where it exists (without -Repository specified)" {
        # Package with Tag "" exists in the following repositories: PSGallery, NuGetGallery, and localRepo
        $res = Find-PSResource -Tag $tag1 -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0
        $res.Count | Should -BeGreaterOrEqual 5

        $pkg1 = $res[0]
        $pkg1.Name | Should -Be $testModuleName
        $pkg1.Repository | Should -Be $localRepoName

        # Note  Find-PSResource -Tag returns package Ids in desc order
        $pkg2 = $res[1]
        $pkg2.Name | Should -Be $testModuleName2
        $pkg2.Repository | Should -Be $PSGalleryName

        $pkg3 = $res[2]
        $pkg3.Name | Should -Be $testModuleName
        $pkg3.Repository | Should -Be $PSGalleryName

        # Note  Find-PSResource -Tag returns package Ids in desc order
        $pkg4 = $res[3]
        $pkg4.Name | Should -Be $testModuleName
        $pkg4.Repository | Should -Be $NuGetGalleryName

        $pkg5 = $res[4]
        $pkg5.Name | Should -Be $testModuleName2
        $pkg5.Repository | Should -Be $NuGetGalleryName
    }

    It "find resources from all repositories where it exists and not write errors for repositories where it does not exist (without -Repository specified)" {
        # Package with tag "Tag-Required-Script1-2.5" exists in the following repositories: PSGallery, NuGetGallery
        $res = Find-PSResource -Tag $tag2 -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0
        $res.Count | Should -BeGreaterOrEqual 3

        $pkg1 = $res[0]
        $pkg1.Name | Should -Be "test_script"
        $pkg1.Repository | Should -Be $PSGalleryName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be "Required-Script1"
        $pkg2.Repository | Should -Be $PSGalleryName

        $pkg3 = $res[2]
        $pkg3.Name | Should -Be "test_script"
        $pkg3.Repository | Should -Be $NuGetGalleryName
    }

    It "not find resource when the tag specified is not found for any package and report error (without -Repository specified)" {
        $res = Find-PSResource -Tag "NonExistantTag" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 0
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageWithTagsNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "not find resource when it has one tag specified but not other and report error (without -Repository specified)" {
        $res = Find-PSResource -Tag $tag2,"NonExistantTag" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 0
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageWithTagsNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "not find resources when Tag entry contains wildcard (without -Repository specified)" {
        $res = Find-PSResource -Tag "myTag*" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 0
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "WildcardsUnsupportedForTag,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "not find resource and discard Tag entry containing wildcard, but search for other non-wildcard Tag entries (without -Repository specified)" {
        $res = Find-PSResource -Tag $tag2,"myTag*" -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "WildcardsUnsupportedForTag,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"

        $res.Count | Should -BeGreaterOrEqual 3
        $pkg1 = $res[0]
        $pkg1.Name | Should -Be "test_script"
        $pkg1.Repository | Should -Be $PSGalleryName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be "Required-Script1"
        $pkg2.Repository | Should -Be $PSGalleryName

        $pkg3 = $res[2]
        $pkg3.Name | Should -Be "test_script"
        $pkg3.Repository | Should -Be $NuGetGalleryName
    }

    It "find resources from all pattern matching repositories where it exists (-Repository with wildcard)" {
        # Package with Tag "CommandsAndResource" exists in the following repositories: PSGallery, NuGetGallery, localRepo
        $res = Find-PSResource -Tag $tag1 -Repository "*Gallery" -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0
        $res.Count | Should -BeGreaterOrEqual 4

        # Note  Find-PSResource -Tag returns package Ids in desc order
        $pkg1 = $res[0]
        $pkg1.Name | Should -Be $testModuleName2
        $pkg1.Repository | Should -Be $PSGalleryName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be $testModuleName
        $pkg2.Repository | Should -Be $PSGalleryName

        # Note  Find-PSResource -Tag returns package Ids in desc order
        $pkg3 = $res[2]
        $pkg3.Name | Should -Be $testModuleName
        $pkg3.Repository | Should -Be $NuGetGalleryName

        $pkg4 = $res[3]
        $pkg4.Name | Should -Be $testModuleName2
        $pkg4.Repository | Should -Be $NuGetGalleryName
    }

    It "should not allow for repository name with wildcard and non-wildcard name specified in same command run" {
        {Find-PSResource -Tag $tag1 -Repository "*Gallery",$localRepoName} | Should -Throw -ErrorId "RepositoryNamesWithWildcardsAndNonWildcardUnsupported,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "not find resource and write error if tag does not exist for resources in any pattern matching repositories (-Repository with wildcard)" {
        $res = Find-PSResource -Tag "NonExistantTag" -Repository "*Gallery" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageWithTagsNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "find resource from single specific repository (-Repository with single non-wildcard value)" {
        $res = Find-PSResource -Tag $tag2 -Repository $PSGalleryName
        $res.Count | Should -BeGreaterOrEqual 2
        $pkg1 = $res[0]
        $pkg1.Name | Should -Be "test_script"
        $pkg1.Repository | Should -Be $PSGalleryName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be "Required-Script1"
        $pkg2.Repository | Should -Be $PSGalleryName
    }

    It "not find resource if it does not exist in repository and write error (-Repository with single non-wildcard value)" {
        $res = Find-PSResource -Tag "NonExistantTag" -Repository $PSGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageWithSpecifiedTagsNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "find resource from all repositories where it exists (-Repository with multiple non-wildcard values)" {
        $res = Find-PSResource -Tag $tag2 -Repository $PSGalleryName,$NuGetGalleryName
        $res.Count | Should -BeGreaterOrEqual 3

        $pkg1 = $res[0]
        $pkg1.Name | Should -Be "test_script"
        $pkg1.Repository | Should -Be $PSGalleryName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be "Required-Script1"
        $pkg2.Repository | Should -Be $PSGalleryName

        $pkg3 = $res[2]
        $pkg3.Name | Should -Be "test_script"
        $pkg3.Repository | Should -Be $NuGetGalleryName
    }

    It "find resource from all repositories where it exists and write errors for those it does not exist from (-Repository with multiple non-wildcard values)" {
        # Package eith Tag "Tag-TestMyLocalScript-1.0.0.0" exists in the following repositories: PSGallery
        $tagForPkgOnPSGallery = "Tag-TestMyLocalScript-1.0.0.0"
        $res = Find-PSResource -Tag $tagForPkgOnPSGallery -Repository $PSGalleryName,$NuGetGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res.Count | Should -BeGreaterOrEqual 2
        $err | Should -HaveCount 1

        $pkg1 = $res[0]
        $pkg1.Name | Should -Be "TestLocalScript"
        $pkg1.Repository | Should -Be $PSGalleryName

        $pkg2 = $res[1]
        $pkg2.Name | Should -Be "anam_script"
        $pkg2.Repository | Should -Be $PSGalleryName

        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageWithSpecifiedTagsNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    # For CommandName Name based search
    It "find resource that has CommandName specified from all repositories where it exists and not write errors where it does not exist (without -Repository specified)" {
        # $cmdNameToSearch = "Get-TargetResource"
        $res = Find-PSResource -CommandName $cmdName -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0
        $res.Count | Should -BeGreaterOrEqual 9
        $pkgFoundFromLocalRepo = $false
        $pkgFoundFromPSGallery = $false

        foreach ($pkg in $res)
        {
            if ($pkg.ParentResource.Repository -eq $localRepoName)
            {
                $pkgFoundFromLocalRepo = $true
            }
            elseif ($pkg.ParentResource.Repository -eq $PSGalleryName)
            {
                $pkgFoundFromPSGallery = $true
            }
        }

        $pkg.Names | Should -Be $cmdName
        $pkg.ParentResource.Includes.Command | Should -Contain $cmdName
        $pkgFoundFromLocalRepo | Should -BeTrue
        $pkgFoundFromPSGallery | Should -BeTrue
    }

    It "not find resource when the CommandName specified is not found for any package and report error (without -Repository specified)" {
        $res = Find-PSResource -Command "NonExistantCommandName" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 0
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageWithCmdOrDscNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "not find resource when it has one CommandName specified but not other and report error (without -Repository specified)" {
        $res = Find-PSResource -CommandName $cmdName,"NonExistantCommandName" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 0
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageWithCmdOrDscNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "not find resources when CommandName entry contains wildcard (without -Repository specified)" {
        $res = Find-PSResource -CommandName "myCommand*" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 0
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "WildcardsUnsupportedForCommandNameorDSCResourceName,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "not find resource and discard CommandName entry containing wildcard, but search for other non-wildcard CommandName entries (without -Repository specified)" {
        $res = Find-PSResource -CommandName $cmdName,"myCommandName*" -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "WildcardsUnsupportedForCommandNameorDSCResourceName,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"

        $res.Count | Should -BeGreaterOrEqual 9
        $pkgFoundFromLocalRepo = $false
        $pkgFoundFromPSGallery = $false

        foreach ($pkg in $res)
        {
            if ($pkg.ParentResource.Repository -eq $localRepoName)
            {
                $pkgFoundFromLocalRepo = $true
            }
            elseif ($pkg.ParentResource.Repository -eq $PSGalleryName)
            {
                $pkgFoundFromPSGallery = $true
            }
        }

        $pkg.Names | Should -Be $cmdName
        $pkg.ParentResource.Includes.Command | Should -Contain $cmdName
        $pkgFoundFromLocalRepo | Should -BeTrue
        $pkgFoundFromPSGallery | Should -BeTrue
    }

    It "find resources from all pattern matching repositories where it exists (-Repository with wildcard)" {
        # Package with CommandName "Get-TargetResource" exists in the following repositories: PSGallery, localRepo
        $res = Find-PSResource -CommandName $cmdName -Repository "*Gallery" -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0
        $res.Count | Should -BeGreaterOrEqual 9

        $pkgFoundFromLocalRepo = $false
        $pkgFoundFromPSGallery = $false

        foreach ($pkg in $res)
        {
            if ($pkg.ParentResource.Repository -eq $localRepoName)
            {
                $pkgFoundFromLocalRepo = $true
            }
            elseif ($pkg.ParentResource.Repository -eq $PSGalleryName)
            {
                $pkgFoundFromPSGallery = $true
            }
        }

        $pkg.Names | Should -Be $cmdName
        $pkg.ParentResource.Includes.Command | Should -Contain $cmdName
        $pkgFoundFromLocalRepo | Should -BeFalse
        $pkgFoundFromPSGallery | Should -BeTrue
    }

    It "should not allow for repository name with wildcard and non-wildcard command name specified in same command run" {
        {Find-PSResource -CommandName $cmdName -Repository "*Gallery",$localRepoName} | Should -Throw -ErrorId "RepositoryNamesWithWildcardsAndNonWildcardUnsupported,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "not find resource and write error if tag does not exist for resources in any pattern matching repositories (-Repository with wildcard)" {
        $res = Find-PSResource -CommandName "NonExistantCommand" -Repository "*Gallery" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageWithCmdOrDscNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "find resource given CommandName from unsupported single specific repository (-Repository with single non-wildcard value)" {
        $res = Find-PSResource -CommandName $cmdName -Repository $NuGetGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindCommandOrDscResourceFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "not find resource with given CommandName if it does not exist in repository and write error (-Repository with single non-wildcard value)" {
        $res = Find-PSResource -CommandName "NonExistantCommand" -Repository $localRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindCmdOrDSCNamesPackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "not find resource with given CommandName from NuGetGallery (V3 server) as it is not supported and write error" {
        $res = Find-PSResource -CommandName "NonExistantCommand" -Repository $localRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindCmdOrDSCNamesPackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "find resource given CommandName from all repositories where it exists (-Repository with multiple non-wildcard values)" {
        $res = Find-PSResource -CommandName $cmdName -Repository $PSGalleryName,$localRepoName
        $res.Count | Should -BeGreaterOrEqual 9

        $pkgFoundFromLocalRepo = $false
        $pkgFoundFromPSGallery = $false

        foreach ($pkg in $res)
        {
            if ($pkg.ParentResource.Repository -eq $localRepoName)
            {
                $pkgFoundFromLocalRepo = $true
            }
            elseif ($pkg.ParentResource.Repository -eq $PSGalleryName)
            {
                $pkgFoundFromPSGallery = $true
            }
        }

        $pkg.Names | Should -Be $cmdName
        $pkg.ParentResource.Includes.Command | Should -Contain $cmdName
        $pkgFoundFromLocalRepo | Should -BeTrue
        $pkgFoundFromPSGallery | Should -BeTrue
    }

    It "find resource given CommandName from all repositories where it exists and write errors for those it does not exist from (-Repository with multiple non-wildcard values)" {
        # Package with Command "Get-MyCommand" exists in the following repositories: localRepo
        $res = Find-PSResource -CommandName $cmdName2 -Repository $PSGalleryName,$localRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 1
        $err | Should -HaveCount 1

        $pkgFoundFromLocalRepo = $false
        $pkgFoundFromPSGallery = $false

        foreach ($pkg in $res)
        {
            if ($pkg.ParentResource.Repository -eq $localRepoName)
            {
                $pkgFoundFromLocalRepo = $true
            }
            elseif ($pkg.ParentResource.Repository -eq $PSGalleryName)
            {
                $pkgFoundFromPSGallery = $true
            }
        }

        $pkg.Names | Should -Be $cmdName2
        $pkg.ParentResource.Includes.Command | Should -Contain $cmdName2
        $pkgFoundFromLocalRepo | Should -BeTrue
        $pkgFoundFromPSGallery | Should -BeFalse

        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageWithSpecifiedCmdOrDSCNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "find resource given CommandName from repository where it exists and not find and write error for unsupported single specific repository (-Repository with single non-wildcard value)" {
        $res = Find-PSResource -CommandName $cmdName -Repository $localRepoName,$NuGetGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 1
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindCommandOrDscResourceFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"

        $pkgFoundFromLocalRepo = $false
        $pkgFoundFromPSGallery = $false
        $pkgFoundFromNuGetGallery = $false

        foreach ($pkg in $res)
        {
            if ($pkg.ParentResource.Repository -eq $localRepoName)
            {
                $pkgFoundFromLocalRepo = $true
            }
            elseif ($pkg.ParentResource.Repository -eq $PSGalleryName)
            {
                $pkgFoundFromPSGallery = $true
            }
            elseif($pkg.ParentResource.Repository -eq $NuGetGalleryName)
            {
                $pkgFoundFromNuGetGallery = $true
            }
        }

        $pkg.Names | Should -Be $cmdName
        $pkg.ParentResource.Includes.Command | Should -Contain $cmdName
        $pkgFoundFromLocalRepo | Should -BeTrue
        $pkgFoundFromPSGallery | Should -BeFalse
        $pkgFoundFromNuGetGallery | Should -BeFalse
    }

    # For DSCResource Name based search
    It "find resource that has DSCResourceName specified from all repositories where it exists and not write errors where it does not exist (without -Repository specified)" {
        $res = Find-PSResource -DscResourceName $dscName -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0
        $res.Count | Should -BeGreaterOrEqual 2
        $pkgFoundFromLocalRepo = $false
        $pkgFoundFromPSGallery = $false

        foreach ($pkg in $res)
        {
            if ($pkg.ParentResource.Repository -eq $localRepoName)
            {
                $pkgFoundFromLocalRepo = $true
            }
            elseif ($pkg.ParentResource.Repository -eq $PSGalleryName)
            {
                $pkgFoundFromPSGallery = $true
            }
        }

        $pkg.Names | Should -Be $dscName
        $pkg.ParentResource.Includes.DscResource | Should -Contain $dscName
        $pkgFoundFromLocalRepo | Should -BeTrue
        $pkgFoundFromPSGallery | Should -BeTrue
    }

    It "not find resource when the DSCResourceName specified is not found for any package and report error (without -Repository specified)" {
        $res = Find-PSResource -DscResourceName "NonExistantDSCResourceName" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 0
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageWithCmdOrDscNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "not find resource when it has one DSCResourceName specified but not other and report error (without -Repository specified)" {
        $res = Find-PSResource -DscResourceName $dscName,"NonExistantDSCResourceName" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 0
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageWithCmdOrDscNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "not find resources when DSCResourceName entry contains wildcard (without -Repository specified)" {
        $res = Find-PSResource -CommandName "myDSCResourceName*" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 0
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "WildcardsUnsupportedForCommandNameorDSCResourceName,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "not find resource and discard DSCREsource entry containing wildcard, but search for other non-wildcard DSCResourceName entries (without -Repository specified)" {
        $res = Find-PSResource -DscResourceName $dscName,"myDSCResourceName*" -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "WildcardsUnsupportedForCommandNameorDSCResourceName,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"

        $res.Count | Should -BeGreaterOrEqual 2
        $pkgFoundFromLocalRepo = $false
        $pkgFoundFromPSGallery = $false

        foreach ($pkg in $res)
        {
            if ($pkg.ParentResource.Repository -eq $localRepoName)
            {
                $pkgFoundFromLocalRepo = $true
            }
            elseif ($pkg.ParentResource.Repository -eq $PSGalleryName)
            {
                $pkgFoundFromPSGallery = $true
            }
        }

        $pkg.Names | Should -Be $dscName
        $pkg.ParentResource.Includes.DscResource | Should -Contain $dscName
        $pkgFoundFromLocalRepo | Should -BeTrue
        $pkgFoundFromPSGallery | Should -BeTrue
    }

    It "find resources from all pattern matching repositories where it exists (-Repository with wildcard)" {
        # Package with DSCResourceName "SystemLocale" exists in the following repositories: PSGallery, localRepo
        $res = Find-PSResource -DscResourceName $dscName -Repository "*Gallery" -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -HaveCount 0
        $res.Count | Should -BeGreaterOrEqual 2

        $pkgFoundFromLocalRepo = $false
        $pkgFoundFromPSGallery = $false

        foreach ($pkg in $res)
        {
            if ($pkg.ParentResource.Repository -eq $localRepoName)
            {
                $pkgFoundFromLocalRepo = $true
            }
            elseif ($pkg.ParentResource.Repository -eq $PSGalleryName)
            {
                $pkgFoundFromPSGallery = $true
            }
        }

        $pkg.Names | Should -Be $dscName
        $pkg.ParentResource.Includes.DscResource | Should -Contain $dscName
        $pkgFoundFromLocalRepo | Should -BeFalse
        $pkgFoundFromPSGallery | Should -BeTrue
    }

    It "should not allow for repository name with wildcard and non-wildcard command name specified in same command run" {
        {Find-PSResource -DscResourceName $dscName -Repository "*Gallery",$localRepoName} | Should -Throw -ErrorId "RepositoryNamesWithWildcardsAndNonWildcardUnsupported,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "not find resource and write error if tag does not exist for resources in any pattern matching repositories (-Repository with wildcard)" {
        $res = Find-PSResource -DscResourceName "NonExistantDSCResource" -Repository "*Gallery" -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageWithCmdOrDscNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "find resource given DSCResourceName from single specific repository (-Repository with single non-wildcard value)" {
        $res = Find-PSResource -DscResourceName $dscName -Repository $localRepoName
        $res | Should -HaveCount 1
        $res.Names | Should -Be $dscName
        $res.ParentResource.Includes.DscResource | Should -Contain $dscName
        $res.ParentResource.Repository | Should -Be $localRepoName
    }

    It "not find resource with given DSCResourceName if it does not exist in repository and write error (-Repository with single non-wildcard value)" {
        $res = Find-PSResource -DscResourceName "NonExistantDSCResource" -Repository $localRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindCmdOrDSCNamesPackageNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "find resource given CommandName from unsupported single specific repository (-Repository with single non-wildcard value)" {
        $res = Find-PSResource -DscResourceName $dscName -Repository $NuGetGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -BeNullOrEmpty
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindCommandOrDscResourceFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "find resource given DSCResourceName from all repositories where it exists (-Repository with multiple non-wildcard values)" {
        $res = Find-PSResource -DscResourceName $dscName -Repository $PSGalleryName,$localRepoName
        $res.Count | Should -BeGreaterOrEqual 3

        $pkgFoundFromLocalRepo = $false
        $pkgFoundFromPSGallery = $false

        foreach ($pkg in $res)
        {
            if ($pkg.ParentResource.Repository -eq $localRepoName)
            {
                $pkgFoundFromLocalRepo = $true
            }
            elseif ($pkg.ParentResource.Repository -eq $PSGalleryName)
            {
                $pkgFoundFromPSGallery = $true
            }
        }

        $pkg.Names | Should -Be $dscName
        $pkg.ParentResource.Includes.DscResource | Should -Contain $dscName
        $pkgFoundFromLocalRepo | Should -BeTrue
        $pkgFoundFromPSGallery | Should -BeTrue
    }

    It "find resource given DSCResourceName from all repositories where it exists and write errors for those it does not exist from (-Repository with multiple non-wildcard values)" {
        # Package with DSCResourceName "MyDSCResource" exists in the following repositories: localRepo
        $res = Find-PSResource -DscResourceName $dscName2 -Repository $PSGalleryName,$localRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 1
        $err | Should -HaveCount 1

        $pkgFoundFromLocalRepo = $false
        $pkgFoundFromPSGallery = $false

        foreach ($pkg in $res)
        {
            if ($pkg.ParentResource.Repository -eq $localRepoName)
            {
                $pkgFoundFromLocalRepo = $true
            }
            elseif ($pkg.ParentResource.Repository -eq $PSGalleryName)
            {
                $pkgFoundFromPSGallery = $true
            }
        }

        $pkg.Names | Should -Be $dscName2
        $pkg.ParentResource.Includes.DscResource | Should -Contain $dscName2
        $pkgFoundFromLocalRepo | Should -BeTrue
        $pkgFoundFromPSGallery | Should -BeFalse

        $err[0].FullyQualifiedErrorId | Should -BeExactly "PackageWithSpecifiedCmdOrDSCNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "find resource given DSCResourceName from repository where it exists and not find and write error for unsupported specific repository (-Repository with single non-wildcard value)" {
        $res = Find-PSResource -DscResourceName $dscName -Repository $localRepoName,$NuGetGalleryName -ErrorVariable err -ErrorAction SilentlyContinue
        $res | Should -HaveCount 1
        $err | Should -HaveCount 1
        $err[0].FullyQualifiedErrorId | Should -BeExactly "FindCommandOrDscResourceFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"

        $pkgFoundFromLocalRepo = $false
        $pkgFoundFromPSGallery = $false
        $pkgFoundFromNuGetGallery = $false

        foreach ($pkg in $res)
        {
            if ($pkg.ParentResource.Repository -eq $localRepoName)
            {
                $pkgFoundFromLocalRepo = $true
            }
            elseif ($pkg.ParentResource.Repository -eq $PSGalleryName)
            {
                $pkgFoundFromPSGallery = $true
            }
            elseif($pkg.ParentResource.Repository -eq $NuGetGalleryName)
            {
                $pkgFoundFromNuGetGallery = $true
            }
        }

        $pkg.Names | Should -Be $dscName
        $pkg.ParentResource.Includes.DscResource | Should -Contain $dscName
        $pkgFoundFromLocalRepo | Should -BeTrue
        $pkgFoundFromPSGallery | Should -BeFalse
        $pkgFoundFromNuGetGallery | Should -BeFalse
    }
}
