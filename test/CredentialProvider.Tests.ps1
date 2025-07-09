# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe 'Test Azure Artifacts Credential Provider' -tags 'CI' {

    BeforeAll{
        $TestModuleName = "TestModule99"
        $ADORepoName = "psrg-credprovidertest"
        $ADORepoUri = "https://pkgs.dev.azure.com/powershell-rel/PSResourceGet/_packaging/psrg-credprovidertest/nuget/v2"
        #https://pkgs.dev.azure.com/powershell-rel/PSResourceGet/_packaging/psrg-credprovidertest/nuget/v3/index.json
        $LocalRepoName = "LocalRepository"
        $LocalRepoUri = Join-Path -Path $TestDrive -ChildPath "testdir"
        $null = New-Item $LocalRepoUri -ItemType Directory -Force

        Get-NewPSResourceRepositoryFile
        Register-PSResourceRepository -Name $ADORepoName -Uri $ADORepoUri -Trusted
    }

    AfterAll {
        Uninstall-PSResource $TestModuleName -SkipDependencyCheck -ErrorAction SilentlyContinue

        Get-RevertPSResourceRepositoryFile
    }

    It "Find resource given specific Name and Repository" {
        Write-Host "Var: $env:VSS_NUGET_EXTERNAL_FEED_ENDPOINTS"

        $res = Find-PSResource -Name $TestModuleName -Repository $ADORepoName -Verbose
        $res.Name | Should -Be $TestModuleName
    }
    
    It "Install resource given specific Name and Repository" {
        Install-PSResource -Name $TestModuleName -Repository $ADORepoName
        
        Get-InstalledPSResource -Name $TestModuleName | Should -Not -BeNullOrEmpty
    }

    It "Register repository with local path (CredentialProvider should be set to 'None')" {
        Register-PSResourceRepository -Name $LocalRepoName -Uri $LocalRepoUri -Force
        $repo = Get-PSResourceRepository -Name $LocalRepoName
        $repo.CredentialProvider | Should -Be "None"
    }
    
    It "Set CredentialProvider for local path repository" {
        Register-PSResourceRepository -Name $LocalRepoName -Uri $LocalRepoUri -Trusted -Force
        $repo = Get-PSResourceRepository -Name $LocalRepoName
        $repo.CredentialProvider | Should -Be "None"

        Set-PSResourceRepository -Name $LocalRepoName -CredentialProvider AzArtifacts
        $repo2 = Get-PSResourceRepository -Name $LocalRepoName
        $repo2.CredentialProvider | Should -Be "AzArtifacts"
    }

    It "Register repository with ADO Uri (CredentialProvider should be set to 'AzArtifacts')" {
        Register-PSResourceRepository -Name $ADORepoName -Uri $ADORepoUri -Force
        $repo = Get-PSResourceRepository -Name $ADORepoName
        $repo.CredentialProvider | Should -Be "AzArtifacts"
    }

    It "Set CredentialProvider for ADO repository" {
        Register-PSResourceRepository -Name $ADORepoName -Uri $ADORepoUri -Trusted -Force
        $repo = Get-PSResourceRepository -Name $ADORepoName
        $repo.CredentialProvider | Should -Be "AzArtifacts"

        Set-PSResourceRepository -Name $ADORepoName -CredentialProvider None
        $repo2 = Get-PSResourceRepository -Name $ADORepoName
        $repo2.CredentialProvider | Should -Be "None"
    }

    It "Register repository with ADO Uri (CredentialProvider should be set to 'AzArtifacts')" {
        Register-PSResourceRepository -Name $ADORepoName -Uri $ADORepoUri -CredentialProvider None -Force
        $repo = Get-PSResourceRepository -Name $ADORepoName
        $repo.CredentialProvider | Should -Be "None"
    }
}
