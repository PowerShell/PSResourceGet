# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Write-Verbose -Verbose -Message "PSGetTestUtils path: $modPath"
Import-Module $modPath -Force -Verbose

Describe "Test Reset-PSResourceRepository" -tags 'CI' {
    BeforeEach {
        $PSGalleryName = Get-PSGalleryName
        $PSGalleryUri = Get-PSGalleryLocation
        Get-NewPSResourceRepositoryFile
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path)
        Get-NewTestDirs($tmpDirPaths)
    }
    AfterEach {
        Get-RevertPSResourceRepositoryFile
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path)
        Get-RemoveTestDirs($tmpDirPaths)
    }

    It "reset repository store with multiple repositories registered" {
        # Register multiple repositories
        Register-PSResourceRepository -Name "testRepository1" -Uri $tmpDir1Path
        Register-PSResourceRepository -Name "testRepository2" -Uri $tmpDir2Path
        
        # Verify multiple repositories exist
        $repos = Get-PSResourceRepository
        $repos.Count | Should -BeGreaterThan 2

        # Reset repository store
        Reset-PSResourceRepository -Confirm:$false
        
        # Verify only PSGallery exists
        $repos = Get-PSResourceRepository
        $repos.Count | Should -Be 1
        $repos.Name | Should -Be $PSGalleryName
        $repos.Uri | Should -Be $PSGalleryUri
        $repos.Priority | Should -Be 50
        $repos.Trusted | Should -Be $false
    }

    It "reset repository store when only PSGallery is registered" {
        # Reset repository store
        Reset-PSResourceRepository -Confirm:$false
        
        # Verify only PSGallery exists
        $repos = Get-PSResourceRepository
        $repos.Count | Should -Be 1
        $repos.Name | Should -Be $PSGalleryName
    }

    It "reset repository store with -PassThru returns PSGallery" {
        # Register a test repository
        Register-PSResourceRepository -Name "testRepository1" -Uri $tmpDir1Path
        
        # Reset repository store with PassThru
        $result = Reset-PSResourceRepository -Confirm:$false -PassThru
        
        # Verify PassThru returns PSGallery
        $result.Name | Should -Be $PSGalleryName
        $result.Uri | Should -Be $PSGalleryUri
        $result.Priority | Should -Be 50
        $result.Trusted | Should -Be $false
        
        # Verify only PSGallery exists
        $repos = Get-PSResourceRepository
        $repos.Count | Should -Be 1
    }

    It "reset repository store respects -WhatIf" {
        # Register test repositories
        Register-PSResourceRepository -Name "testRepository1" -Uri $tmpDir1Path
        Register-PSResourceRepository -Name "testRepository2" -Uri $tmpDir2Path
        
        # Get count before reset
        $reposBefore = Get-PSResourceRepository
        $countBefore = $reposBefore.Count

        # Reset with WhatIf
        Reset-PSResourceRepository -WhatIf
        
        # Verify repositories still exist (WhatIf should not make changes)
        $reposAfter = Get-PSResourceRepository
        $reposAfter.Count | Should -Be $countBefore
        $reposAfter.Name | Should -Contain "testRepository1"
        $reposAfter.Name | Should -Contain "testRepository2"
    }

    It "reset repository store when PSGallery was unregistered" {
        # Unregister PSGallery
        Unregister-PSResourceRepository -Name $PSGalleryName
        
        # Verify PSGallery is not registered
        $repos = Get-PSResourceRepository -ErrorAction SilentlyContinue
        $repos.Name | Should -Not -Contain $PSGalleryName
        
        # Reset repository store
        Reset-PSResourceRepository -Confirm:$false
        
        # Verify PSGallery is back
        $repos = Get-PSResourceRepository
        $repos.Count | Should -Be 1
        $repos.Name | Should -Be $PSGalleryName
    }

    It "reset repository store with custom PSGallery settings" {
        # Unregister default PSGallery and register with custom settings
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -PSGallery -Priority 10 -Trusted
        
        # Verify custom settings
        $repo = Get-PSResourceRepository -Name $PSGalleryName
        $repo.Priority | Should -Be 10
        $repo.Trusted | Should -Be $true
        
        # Reset repository store
        Reset-PSResourceRepository -Confirm:$false
        
        # Verify PSGallery is reset to default settings
        $repo = Get-PSResourceRepository -Name $PSGalleryName
        $repo.Priority | Should -Be 50
        $repo.Trusted | Should -Be $false
    }
}
