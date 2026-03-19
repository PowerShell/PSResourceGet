# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"


Describe 'Test Find-PSResource for MAR Repository' -tags 'Release' {
    BeforeAll {
        Register-PSResourceRepository -Name "MAR" -Uri "https://mcr.microsoft.com" -ApiVersion "ContainerRegistry"
    }

    AfterAll {
        Unregister-PSResourceRepository -Name "MAR"
    }

    It "Should find resource with wildcard in Name" {
        $res = Find-PSResource -Name "Az.App*" -Repository "MAR"
        $res | Should -Not -BeNullOrEmpty
        $res.Count | Should -BeGreaterThan 1
    }
    
    It "Should find all resource with wildcard in Name" {
        $res = Find-PSResource -Name "*" -Repository "MAR"
        $res | Should -Not -BeNullOrEmpty
        $res.Count | Should -BeGreaterThan 1
    }

}


Describe 'Test Find-PSResource for searching and looping through repositories' -tags 'Release' {

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

}



Describe 'Test HTTP Find-PSResource for V2 Server Protocol' -tags 'Release' {

    BeforeAll{
        $PSGalleryName = Get-PSGalleryName
        $testModuleName = "test_module"
        $testScriptName = "test_script"
        $commandName = "Get-TargetResource"
        $dscResourceName = "SystemLocale"
        $parentModuleName = "SystemLocaleDsc"
        Get-NewPSResourceRepositoryFile
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "find resource given CommandName" {
        $res = Find-PSResource -CommandName $commandName -Repository $PSGalleryName
        $res | Should -Not -BeNullOrEmpty
        foreach ($item in $res) {
            $item.Names | Should -Be $commandName
            $item.ParentResource.Includes.Command | Should -Contain $commandName
        }
    }
    
    It "find resource given DscResourceName" {
        $res = Find-PSResource -DscResourceName $dscResourceName -Repository $PSGalleryName
        $res | Should -Not -BeNullOrEmpty
        foreach ($item in $res) {
            $item.Names | Should -Be $dscResourceName
            $item.ParentResource.Includes.DscResource | Should -Contain $dscResourceName
        }
    }
}
