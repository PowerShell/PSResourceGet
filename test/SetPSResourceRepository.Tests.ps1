# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test Set-PSResourceRepository" {
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

        $credentialInfo1 = New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ("testvault", "testsecret")
        $secureString = ConvertTo-SecureString "testpassword" -AsPlainText -Force
        $credential = New-Object pscredential ("testusername", $secureString)
        $credentialInfo2 = New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ("testvault", "testsecret", $credential)
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

    It "set repository given Name and Uri parameters" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        Set-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir2Path
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir2Path
        $res.Priority | Should -Be 50
        $res.Trusted | Should -Be False
        $res.CredentialInfo | Should -BeNullOrEmpty
    }

    It "set repository given Name and Priority parameters" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        Set-PSResourceRepository -Name $TestRepoName1 -Priority 25
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir1Path
        $res.Priority | Should -Be 25
        $res.Trusted | Should -Be False
        $res.CredentialInfo | Should -BeNullOrEmpty
    }

    It "set repository given Name and Trusted parameters" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        Set-PSResourceRepository -Name $TestRepoName1 -Trusted
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir1Path
        $res.Priority | Should -Be 50
        $res.Trusted | Should -Be True
        $res.CredentialInfo | Should -BeNullOrEmpty
    }

    It "set repository given pipeline input ValueFromPipelineByPropertyName passed in" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        Get-PSResourceRepository -Name $TestRepoName1 | Set-PSResourceRepository -Trusted
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir1Path
        $res.Priority | Should -Be 50
        $res.Trusted | Should -Be True
        $res.CredentialInfo | Should -BeNullOrEmpty
    }

    It "set repository given Name and CredentialInfo parameters" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        Set-PSResourceRepository -Name $TestRepoName1 -CredentialInfo $credentialInfo1
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir1Path
        $res.Priority | Should -Be 50
        $res.Trusted | Should -Be False
        $res.CredentialInfo.VaultName | Should -Be "testvault"
        $res.CredentialInfo.SecretName | Should -Be "testsecret"
        $res.CredentialInfo.Credential | Should -BeNullOrEmpty
    }

    It "not set repository and write error given just Name parameter" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        {Set-PSResourceRepository -Name $TestRepoName1 -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"
    }

    $testCases = @{Type = "contains *";     Name = "test*Repository"; ErrorId = "ErrorInNameParameterSet"},
                 @{Type = "is whitespace";  Name = " ";               ErrorId = "ErrorInNameParameterSet"},
                 @{Type = "is null";        Name = $null;             ErrorId = "ParameterArgumentValidationError"}

    It "not set repository and throw error given Name <Type> (NameParameterSet)" -TestCases $testCases {
        param($Type, $Name)

        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        {Set-PSResourceRepository -Name $Name -Priority 25 -ErrorAction Stop} | Should -Throw -ErrorId "$ErrorId,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"
    }

    $testCases2 = @{Type = "contains *";     Name = "test*Repository2";  ErrorId = "ErrorSettingIndividualRepoFromRepositories"},
                  @{Type = "is whitespace";  Name = " ";                 ErrorId = "ErrorSettingIndividualRepoFromRepositories"},
                  @{Type = "is null";        Name = $null;               ErrorId = "NullNameForRepositoriesParameterSetRepo"}
    It "not set repository and write error given Name <Type> (RepositoriesParameterSet)" -TestCases $testCases2 {
        param($Type, $Name, $ErrorId)

        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        Register-PSResourceRepository -Name $TestRepoName2 -Uri $tmpDir2Path

        $hashtable1 = @{Name = $TestRepoName1; Uri = $tmpDir3Path}
        $hashtable2 = @{Name = $TestRepoName2; Priority = 25}
        $incorrectHashTable = @{Name = $Name; Trusted = $True}
        $arrayOfHashtables = $hashtable1, $incorrectHashTable, $hashtable2

        Set-PSResourceRepository -Repository $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "$ErrorId,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"

        $res = Get-PSResourceRepository -Name $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir3Path
        $res.Trusted | Should -Be False

        $res2 = Get-PSResourceRepository -Name $TestRepoName2
        $res2.Priority | Should -Be 25
        $res2.Trusted | Should -Be False
    }

    It "set repositories with Repositories parameter" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        Register-PSResourceRepository -Name $TestRepoName2 -Uri $tmpDir2Path
        Register-PSResourceRepository -Name $TestRepoName3 -Uri $tmpDir3Path
        Register-PSResourceRepository -PSGallery

        $hashtable1 = @{Name = $TestRepoName1; Uri = $tmpDir2Path};
        $hashtable2 = @{Name = $TestRepoName2; Priority = 25};
        $hashtable3 = @{Name = $TestRepoName3; CredentialInfo = [PSCustomObject] @{ VaultName = "testvault"; SecretName = "testsecret" }};
        $hashtable4 = @{Name = $PSGalleryName; Trusted = $True};
        $arrayOfHashtables = $hashtable1, $hashtable2, $hashtable3, $hashtable4

        Set-PSResourceRepository -Repository $arrayOfHashtables
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir2Path
        $res.Priority | Should -Be 50
        $res.Trusted | Should -Be False
        $res.CredentialInfo | Should -BeNullOrEmpty

        $res2 = Get-PSResourceRepository -Name $TestRepoName2
        $res2.Name | Should -Be $TestRepoName2
        $res2.Uri.LocalPath | Should -Contain $tmpDir2Path
        $res2.Priority | Should -Be 25
        $res2.Trusted | Should -Be False
        $res2.CredentialInfo | Should -BeNullOrEmpty

        $res3 = Get-PSResourceRepository -Name $TestRepoName3
        $res3.Name | Should -Be $TestRepoName3
        $res3.Uri.LocalPath | Should -Contain $tmpDir3Path
        $res3.Priority | Should -Be 50
        $res3.Trusted | Should -Be False
        $res3.CredentialInfo.VaultName | Should -Be "testvault"
        $res3.CredentialInfo.SecretName | Should -Be "testsecret"
        $res3.CredentialInfo.Credential | Should -BeNullOrEmpty

        $res4 = Get-PSResourceRepository -Name $PSGalleryName
        $res4.Name | Should -Be $PSGalleryName
        $res4.Uri | Should -Contain $PSGalleryUri
        $res4.Priority | Should -Be 50
        $res4.Trusted | Should -Be True
        $res4.CredentialInfo | Should -BeNullOrEmpty
    }

    It "not set and throw error for trying to set PSGallery Uri (NameParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -PSGallery
        {Set-PSResourceRepository -Name $PSGalleryName -Uri $tmpDir1Path -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"
    }

    It "not set and throw error for trying to set PSGallery CredentialInfo (NameParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -PSGallery
        {Set-PSResourceRepository -Name $PSGalleryName -CredentialInfo $credentialInfo1 -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"
    }

    It "not set repository and throw error for trying to set PSGallery Uri (RepositoriesParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -PSGallery

        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path

        $hashtable1 = @{Name = $PSGalleryName; Uri = $tmpDir1Path}
        $hashtable2 = @{Name = $TestRepoName1; Priority = 25}
        $arrayOfHashtables = $hashtable1, $hashtable2

        Set-PSResourceRepository -Repository $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ErrorSettingIndividualRepoFromRepositories,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"

        $res = Get-PSResourceRepository -Name $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir1Path
        $res.Priority | Should -Be 25
        $res.Trusted | Should -Be False
    }

    It "should set repository with relative Uri provided" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        Set-PSResourceRepository -Name $TestRepoName1 -Uri $relativeCurrentPath
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $relativeCurrentPath
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "not set repository and throw error for trying to set PSGallery CredentialInfo (RepositoriesParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -PSGallery

        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path

        $hashtable1 = @{Name = $PSGalleryName; CredentialInfo = $credentialInfo1}
        $hashtable2 = @{Name = $TestRepoName1; Priority = 25}
        $arrayOfHashtables = $hashtable1, $hashtable2

        Set-PSResourceRepository -Repository $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "ErrorSettingIndividualRepoFromRepositories,Microsoft.PowerShell.PowerShellGet.Cmdlets.SetPSResourceRepository"

        $res = Get-PSResourceRepository -Name $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir1Path
        $res.Priority | Should -Be 25
        $res.Trusted | Should -Be False
        $res.CredentialInfo | Should -BeNullOrEmpty
    }

    It "should set repository with local file share NuGet based Uri" {
        Register-PSResourceRepository -Name "localFileShareTestRepo" -Uri $tmpDir1Path
        Set-PSResourceRepository -Name "localFileShareTestRepo" -Uri "\\hcgg.rest.of.domain.name\test\ITxx\team\NuGet\"
        $res = Get-PSResourceRepository -Name "localFileShareTestRepo"
        $res.Name | Should -Be "localFileShareTestRepo"
        $Res.Uri.LocalPath | Should -Contain "\\hcgg.rest.of.domain.name\test\ITxx\team\NuGet\"
    }

    It "set repository and see updated repository with -PassThru" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        $res = Set-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir2Path -PassThru
        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir2Path
        $res.Priority | Should -Be 50
        $res.Trusted | Should -Be False
    }

    It "prints a warning if CredentialInfo is passed in without SecretManagement module setup" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
        $output = Set-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path -CredentialInfo $credentialInfo1 3>&1
        $output | Should -Match "Microsoft.PowerShell.SecretManagement module cannot be found"

        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res | Should -Not -BeNullOrEmpty
    }

    It "throws error if CredentialInfo is passed in with Credential property without SecretManagement module setup" {
        {
            Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path
            Set-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path -CredentialInfo $credentialInfo2
        } | Should -Throw -ErrorId "RepositoryCredentialSecretManagementUnavailableModule"

        $res = Get-PSResourceRepository -Name $TestRepoName1 -ErrorAction Ignore
        $res.CredentialInfo | Should -BeNullOrEmpty
    }
}
