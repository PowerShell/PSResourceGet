# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose
# Explicitly import build module because in CI PowerShell can autoload PSGetv2
# This ensures the build module is always being tested
$buildModule = "$psscriptroot/../../out/PowerShellGet"
Import-Module $buildModule -Force -Verbose
Write-Verbose -Verbose -Message "PowerShellGet version currently loaded: $($(Get-Module powershellget).Version)"

Describe "Test CompatPowerShellGet: Set-PSResourceRepository" -tags 'CI' {
    BeforeEach {
        $PSGalleryName = Get-PSGalleryName
        $PSGalleryUri = Get-PSGalleryLocation
        $TestRepoName1 = "testRepository"
        $TestRepoName2 = "testRepository2"
        $TestRepoName3 = "testRepository3"
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

    It "set repository given Name and SourceLocation parameters" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        Set-PSRepository -Name $TestRepoName1 -SourceLocation $tmpDir2Path
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1  
        $Res.Uri.LocalPath | Should -Contain $tmpDir2Path
        $res.Trusted | Should -Be False
    }

    It "set repository given Name and Trusted parameters" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        Set-PSRepository -Name $TestRepoName1 -InstallationPolicy Trusted 
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be True
    }

    It "set repository given pipeline input ValueFromPipelineByPropertyName passed in" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        Get-PSResourceRepository -Name $TestRepoName1 | Set-PSRepository -InstallationPolicy Trusted
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be True
    }

    It "not set repository and write error given just Name parameter" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        {Set-PSRepository -Name $TestRepoName1 -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"
    }

    It "not set repository and throw error given Name contains * (NameParameterSet)" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        {Set-PSRepository -Name "test*Repository" -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"
    }

    It "not set repository and throw error given Name is null (NameParameterSet)" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        {Set-PSRepository -Name $null -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Set-PSRepository"
    }

    It "not set repository and throw error given Name is whitespace (NameParameterSet)" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        {Set-PSRepository -Name " " -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"
    }

    It "not set and throw error for trying to set PSGallery Uri (NameParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -PSGallery
        {Set-PSRepository -Name $PSGalleryName -SourceLocation $tmpDir1Path -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"
    }

    It "should set repository with relative Uri provided" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        Set-PSRepository -Name $TestRepoName1 -SourceLocation $relativeCurrentPath.ToString()
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1
        $reformattedPath = ($relativeCurrentPath -replace "\\", "/")
        $res.Uri.ToString().Contains($reformattedPath) | Should -Be $true
        $res.Trusted | Should -Be False
    }

    It "should set repository with local file share NuGet based Uri" {
        Register-PSResourceRepository -Name "localFileShareTestRepo" -Uri $tmpDir1Path
        Set-PSRepository -Name "localFileShareTestRepo" -SourceLocation "\\hcgg.rest.of.domain.name\test\ITxx\team\NuGet\"
        $res = Get-PSResourceRepository -Name "localFileShareTestRepo"
        $res.Name | Should -Be "localFileShareTestRepo"
        $Res.Uri | Should -Contain "\\hcgg.rest.of.domain.name\test\ITxx\team\NuGet\"
    }
}

# Ensure that PSGet v2 was not loaded during the test via command discovery
$PSGetVersionsLoaded = (Get-Module powershellget).Version
Write-Host "PowerShellGet versions currently loaded: $PSGetVersionsLoaded"
if ($PSGetVersionsLoaded.Count -gt 1) {
    throw  "There was more than one version of PowerShellGet imported into the current session. `
        Imported versions include: $PSGetVersionsLoaded"
}
