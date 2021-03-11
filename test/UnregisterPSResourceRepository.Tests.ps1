# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test Register-PSResourceRepository" {
    BeforeEach {
        $TestGalleryName = Get-PoshTestGalleryName
        $PSGalleryName = Get-PSGalleryName
        Get-NewPSResourceRepositoryFile
        $tmpDir1Path = Join-Path -Path (Get-TempPath) -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path (Get-TempPath) -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path (Get-TempPath) -ChildPath "tmpDir3"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path)
        Create-TempDirs($tmpDirPaths)
    }
    AfterEach {
        Get-RevertPSResourceRepositoryFile
        $tmpDir1Path = Join-Path -Path (Get-TempPath) -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path (Get-TempPath) -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path (Get-TempPath) -ChildPath "tmpDir3"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path)
        Remove-TempDirs($tmpDirPaths)
    }

    # It "unregister with empty string for repoName" {

    # }

    # It "unregister one repo name when one specified" {

    # }
}