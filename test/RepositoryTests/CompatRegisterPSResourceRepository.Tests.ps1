# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"

$tempModuleDir = Get-ChildItem "D:\a\_temp\TempModules" -Recurse
Write-Verbose -Verbose "Get-ChildItem on tempModules path: $tempModuleDir"

$gmo = Get-Module powershellget 
Write-Verbose -Verbose "Get-InstalledModule on PowerShellGet: $gmo"

$getcmd = (Get-Command Find-Module).Module.ModuleBase
Write-Verbose -Verbose "Get-Command on Find-Module: $getcmd"

Describe "Test CompatPowerShellGet: Register-PSResourceRepository" -Tags 'CI' {
    BeforeEach {
        $PSGalleryName = Get-PSGalleryName
        $PSGalleryUri = Get-PSGalleryLocation
        $TestRepoName1 = "testRepository"
        $TestRepoName2 = "testRepository2"
        $TestRepoName3 = "testRepository3"
        $TestRepoName4 = "testRepository4"
        $relativeCurrentPath = Get-Location
        Get-NewPSResourceRepositoryFile
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path $TestDrive -ChildPath "tmpDir3"
        $tmpDir4Path = Join-Path -Path $TestDrive -ChildPath "tmpDir4"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path, $tmpDir4Path)
        Get-NewTestDirs($tmpDirPaths)

        $relativeCurrentPath = Get-Location

        $randomSecret = [System.IO.Path]::GetRandomFileName()
        $randomPassword = [System.IO.Path]::GetRandomFileName()
        
        $credentialInfo1 = New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ("testvault", $randomSecret)
        $secureString = ConvertTo-SecureString $randomPassword -AsPlainText -Force
        $credential = New-Object pscredential ("testusername", $secureString)
        $credentialInfo2 = New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ("testvault", $randomSecret, $credential)
    }
    AfterEach {
        Get-RevertPSResourceRepositoryFile
        $tmpDir1Path = Join-Path -Path $TestDrive -ChildPath "tmpDir1"
        $tmpDir2Path = Join-Path -Path $TestDrive -ChildPath "tmpDir2"
        $tmpDir3Path = Join-Path -Path $TestDrive -ChildPath "tmpDir3"
        $tmpDir4Path = Join-Path -Path $TestDrive -ChildPath "tmpDir4"
        $tmpDirPaths = @($tmpDir1Path, $tmpDir2Path, $tmpDir3Path, $tmpDir4Path)
        Get-RemoveTestDirs($tmpDirPaths)
    }

    It "register repository given Name, SourceLocation (bare minimum for NameParmaterSet)" {
        Register-PSRepository -Name $TestRepoName1 -SourceLocation $tmpDir1Path
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be False
    }

    It "register repository with Name, Uri, Trusted (NameParameterSet)" {
        Register-PSRepository -Name $TestRepoName1 -SourceLocation $tmpDir1Path -InstallationPolicy Trusted
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be True
    }

    It "register repository with PSGallery parameter (PSGalleryParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSRepository -Default
        $res = Get-PSResourceRepository -Name $PSGalleryName
        $res.Name | Should -Be $PSGalleryName
        $res.Uri | Should -Be $PSGalleryUri
        $res.Trusted | Should -Be False
    }

    It "register repository with PSGallery, InstallationPolicy parameters (PSGalleryParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSRepository -Default -InstallationPolicy Trusted
        $res = Get-PSResourceRepository -Name $PSGalleryName
        $res.Name | Should -Be $PSGalleryName
        $res.Uri | Should -Be $PSGalleryUri
        $res.Trusted | Should -Be True
    }

    It "not register repository when Name is provided but -SourceLocation is not" {
        {Register-PSRepository -Name $TestRepoName1 -SourceLocation "" -ErrorAction Stop} | Should -Throw -ErrorId "Cannot convert '' to System.Uri"
    }

    It "not register repository when Name is empty but -SourceLocation is provided" {
        {Register-PSRepository -Name "" -SourceLocation $tmpDir1Path -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Register-PSRepository"
    }

    It "not register repository when Name is null but -SourceLocation is provided" {
        {Register-PSRepository -Name $null -SourceLocation $tmpDir1Path -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Register-PSRepository"
    }

    It "not register repository when Name is just whitespace but -SourceLocation is provided" {
        {Register-PSRepository -Name " " -SourceLocation $tmpDir1Path -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    It "not register PSGallery with NameParameterSet" {
        {Register-PSRepository -Name $PSGalleryName -SourceLocation $PSGalleryUri -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    # this error message comes from the parameter cmdlet tags (earliest point of detection)
    It "not register PSGallery when PSGallery parameter provided with Name, Uri or CredentialInfo" {
        {Register-PSRepository -PSGallery -Name $PSGalleryName -ErrorAction Stop} | Should -Throw -ErrorId "NamedParameterNotFound,Register-PSRepository"
        {Register-PSRepository -PSGallery -SourceLocation $PSGalleryUri -ErrorAction Stop} | Should -Throw -ErrorId "NamedParameterNotFound,Register-PSRepository"
    }

    ### Broken
<#    It "should register repository with relative location provided as Uri" {
        Register-PSRepository -Name $TestRepoName1 -SourceLocation ".\"
        $res = Get-PSResourceRepository -Name $TestRepoName1

        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $relativeCurrentPath
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }
#>

    It "should register local file share NuGet based repository" {
        Register-PSRepository -Name "localFileShareTestRepo" -SourceLocation "\\hcgg.rest.of.domain.name\test\ITxx\team\NuGet\"
        $res = Get-PSResourceRepository -Name "localFileShareTestRepo"

        $res.Name | Should -Be "localFileShareTestRepo"
        $res.Uri.LocalPath | Should -Contain "\\hcgg.rest.of.domain.name\test\ITxx\team\NuGet\"
    }

    It 'Register-PSRepository File system location with special chars' {
        $tmpdir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath 'ps repo testing [$!@^&test(;)]'
        if (-not (Test-Path -LiteralPath $tmpdir)) {
            New-Item -Path $tmpdir -ItemType Directory > $null
        }
        try {
            Register-PSRepository -Name 'Test Repo' -SourceLocation $tmpdir
            try {
                $repo = Get-PSRepository -Name 'Test Repo'
                $repo.Name | Should -Be 'Test Repo'
                $repo.Uri.ToString().EndsWith('ps repo testing [$!@^&test(;)]') | should be $true
            }
            finally {
                Unregister-PSRepository -Name 'Test Repo' -ErrorAction SilentlyContinue
            }
        }
        finally {
            Remove-Item -LiteralPath $tmpdir -Force -Recurse
        }
    }

    It 'Reregister PSGallery again: Should fail' {
        { Register-PSRepository -Default -ErrorVariable ev -ErrorAction SilentlyContinue } | Should Throw 'Adding to repository store failed: The PSResource Repository 'PSGallery' already exists'
    } 

    It 'Register-PSRepository -Name PSGallery -SourceLocation $SourceLocation : Should fail' {
        { Register-PSRepository $RepositoryName $SourceLocation -ErrorVariable ev -ErrorAction SilentlyContinue } | Should Throw "Cannot validate argument on parameter 'Name'. The argument is null or empty. Provide an argument that is not null or empty, and then try the command again."
    }

    It 'Register-PSRepository -Name PSGallery -SourceLocation $SourceLocation -PublishLocation $PublishLocation : Should fail' {
        { Register-PSRepository $RepositoryName $SourceLocation -PublishLocation $PublishLocation -ErrorVariable ev  -ErrorAction SilentlyContinue } | Should Throw "Cannot validate argument on parameter 'PublishLocation'. The argument is null or empty. Provide an argument that is not null or empty, and then try the command again."
    }

    It 'Register-PSRepository -Name PSGallery -SourceLocation $SourceLocation -ScriptPublishLocation $ScriptPublishLocation : Should fail' {
        { Register-PSRepository -Name $RepositoryName $SourceLocation -ScriptPublishLocation $ScriptPublishLocation -ErrorVariable ev  -ErrorAction SilentlyContinue } | Should Throw "Cannot validate argument on parameter 'Name'. The argument is null or empty. Provide an argument that is not null or empty, and then try the command again."
    }

    It 'Register-PSRepository -Name PSGallery -SourceLocation $SourceLocation -ScriptSourceLocation $ScriptSourceLocation : Should fail' {
        { Register-PSRepository $RepositoryName -SourceLocation $SourceLocation -ScriptSourceLocation $ScriptSourceLocation -ErrorVariable ev  -ErrorAction SilentlyContinue } | Should Throw "Cannot validate argument on parameter 'SourceLocation'. The argument is null or empty. Provide an argument that is not null or empty, and then try the command again."
    }
}
