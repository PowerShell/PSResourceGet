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
    }

    AfterEach {
        Get-RevertPSResourceRepositoryFile
    }

    It "Reset repository store without PassThru parameter" {
        # Arrange: Add a test repository
        $TestRepoName = "testRepository"
        $tmpDirPath = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        New-Item -ItemType Directory -Path $tmpDirPath -Force | Out-Null
        Register-PSResourceRepository -Name $TestRepoName -Uri $tmpDirPath
        
        # Verify repository was added
        $repos = Get-PSResourceRepository
        $repos.Count | Should -BeGreaterThan 1
        
        # Act: Reset repository store
        Reset-PSResourceRepository -Confirm:$false
        
        # Assert: Only PSGallery should exist
        $repos = Get-PSResourceRepository
        $repos.Count | Should -Be 1
        $repos.Name | Should -Be $PSGalleryName
        $repos.Uri | Should -Be $PSGalleryUri
    }

    It "Reset repository store with PassThru parameter returns PSGallery" {
        # Arrange: Add a test repository
        $TestRepoName = "testRepository"
        $tmpDirPath = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        New-Item -ItemType Directory -Path $tmpDirPath -Force | Out-Null
        Register-PSResourceRepository -Name $TestRepoName -Uri $tmpDirPath
        
        # Act: Reset repository store with PassThru
        $result = Reset-PSResourceRepository -Confirm:$false -PassThru
        
        # Assert: Result should be PSGallery repository info
        $result | Should -Not -BeNullOrEmpty
        $result.Name | Should -Be $PSGalleryName
        $result.Uri | Should -Be $PSGalleryUri
        $result.Trusted | Should -Be $false
        $result.Priority | Should -Be 50
        
        # Verify only PSGallery exists
        $repos = Get-PSResourceRepository
        $repos.Count | Should -Be 1
    }

    It "Reset repository store should support -WhatIf" {
        # Arrange: Add a test repository
        $TestRepoName = "testRepository"
        $tmpDirPath = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        New-Item -ItemType Directory -Path $tmpDirPath -Force | Out-Null
        Register-PSResourceRepository -Name $TestRepoName -Uri $tmpDirPath
        
        # Capture repository count before WhatIf
        $reposBefore = Get-PSResourceRepository
        $countBefore = $reposBefore.Count
        
        # Act: Run with WhatIf
        Reset-PSResourceRepository -WhatIf
        
        # Assert: Repositories should not have changed
        $reposAfter = Get-PSResourceRepository
        $reposAfter.Count | Should -Be $countBefore
    }

    It "Reset repository store when corrupted should succeed" {
        # Arrange: Corrupt the repository file
        $powerShellGetPath = Join-Path -Path ([Environment]::GetFolderPath([System.Environment+SpecialFolder]::LocalApplicationData)) -ChildPath "PSResourceGet"
        $repoFilePath = Join-Path -Path $powerShellGetPath -ChildPath "PSResourceRepository.xml"
        
        # Write invalid XML to corrupt the file
        "This is not valid XML" | Set-Content -Path $repoFilePath -Force
        
        # Act: Reset the repository store
        $result = Reset-PSResourceRepository -Confirm:$false -PassThru
        
        # Assert: Should successfully reset and return PSGallery
        $result | Should -Not -BeNullOrEmpty
        $result.Name | Should -Be $PSGalleryName
        
        # Verify we can now read repositories
        $repos = Get-PSResourceRepository
        $repos.Count | Should -Be 1
        $repos.Name | Should -Be $PSGalleryName
    }

    It "Reset repository store when file doesn't exist should succeed" {
        # Arrange: Delete the repository file
        $powerShellGetPath = Join-Path -Path ([Environment]::GetFolderPath([System.Environment+SpecialFolder]::LocalApplicationData)) -ChildPath "PSResourceGet"
        $repoFilePath = Join-Path -Path $powerShellGetPath -ChildPath "PSResourceRepository.xml"
        
        if (Test-Path -Path $repoFilePath) {
            Remove-Item -Path $repoFilePath -Force
        }
        
        # Act: Reset the repository store
        $result = Reset-PSResourceRepository -Confirm:$false -PassThru
        
        # Assert: Should successfully reset and return PSGallery
        $result | Should -Not -BeNullOrEmpty
        $result.Name | Should -Be $PSGalleryName
        
        # Verify PSGallery is registered
        $repos = Get-PSResourceRepository
        $repos.Count | Should -Be 1
        $repos.Name | Should -Be $PSGalleryName
    }

    It "Reset repository store with multiple repositories should only keep PSGallery" {
        # Arrange: Register multiple repositories
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path $TestDrive -ChildPath "tmpDir3"
        New-Item -ItemType Directory -Path $tmpDir1Path -Force | Out-Null
        New-Item -ItemType Directory -Path $tmpDir2Path -Force | Out-Null
        New-Item -ItemType Directory -Path $tmpDir3Path -Force | Out-Null
        
        Register-PSResourceRepository -Name "testRepo1" -Uri $tmpDir1Path
        Register-PSResourceRepository -Name "testRepo2" -Uri $tmpDir2Path
        Register-PSResourceRepository -Name "testRepo3" -Uri $tmpDir3Path
        
        # Verify multiple repositories exist
        $reposBefore = Get-PSResourceRepository
        $reposBefore.Count | Should -BeGreaterThan 1
        
        # Act: Reset repository store
        Reset-PSResourceRepository -Confirm:$false
        
        # Assert: Only PSGallery should remain
        $reposAfter = Get-PSResourceRepository
        $reposAfter.Count | Should -Be 1
        $reposAfter.Name | Should -Be $PSGalleryName
    }
}
