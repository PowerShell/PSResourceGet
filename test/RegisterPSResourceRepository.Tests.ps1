# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Import-Module "$psscriptroot\PSGetTestUtils.psm1" -Force

Describe "Test Register-PSResourceRepository" {
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

    It "register repository given Name, Uri (bare minimum for NameParmaterSet)" {
        $res = Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path -PassThru
        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "register repository with Name, Uri, Trusted (NameParameterSet)" {
        $res = Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path -Trusted -PassThru
        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 50
    }

    It "register repository given Name, Uri, Trusted, Priority (NameParameterSet)" {
        $res = Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path -Trusted -Priority 20 -PassThru
        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 20
    }

    It "register repository given Name, Uri, Trusted, Priority, CredentialInfo (NameParameterSet)" {
        $res = Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path -Trusted -Priority 20 -CredentialInfo $credentialInfo1 -PassThru
        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 20
        $res.CredentialInfo.VaultName | Should -Be "testvault"
        $res.CredentialInfo.SecretName | Should -Be $randomSecret
    }

    It "register repository with PSGallery parameter (PSGalleryParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $res = Register-PSResourceRepository -PSGallery -PassThru
        $res.Name | Should -Be $PSGalleryName
        $res.Uri | Should -Be $PSGalleryUri
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "register repository with PSGallery, Trusted parameters (PSGalleryParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $res = Register-PSResourceRepository -PSGallery -Trusted -PassThru
        $res.Name | Should -Be $PSGalleryName
        $res.Uri | Should -Be $PSGalleryUri
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 50
    }

    It "register repository with PSGallery, Trusted, Priority parameters (PSGalleryParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $res = Register-PSResourceRepository -PSGallery -Trusted -Priority 20 -PassThru
        $res.Name | Should -Be $PSGalleryName
        $res.Uri | Should -Be $PSGalleryUri
        $res.Trusted | Should -Be True
        $res.Priority | Should -Be 20
    }

    It "register repositories with -Repository parameter, all name parameter style repositories (RepositoriesParameterSet)" {
        $hashtable1 = @{Name = $TestRepoName1; Uri = $tmpDir1Path}
        $hashtable2 = @{Name = $TestRepoName2; Uri = $tmpDir2Path; Trusted = $True}
        $hashtable3 = @{Name = $TestRepoName3; Uri = $tmpDir3Path; Trusted = $True; Priority = 20}
        $hashtable4 = @{Name = $TestRepoName4; Uri = $tmpDir4Path; Trusted = $True; Priority = 30; CredentialInfo = (New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ("testvault", $randomSecret))}
        $arrayOfHashtables = $hashtable1, $hashtable2, $hashtable3, $hashtable4

        Register-PSResourceRepository -Repository $arrayOfHashtables
        $res = Get-PSResourceRepository -Name $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $tmpDir1Path
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50

        $res2 = Get-PSResourceRepository -Name $TestRepoName2
        $res2.Uri.LocalPath | Should -Contain $tmpDir2Path
        $res2.Trusted | Should -Be True
        $res2.Priority | Should -Be 50

        $res3 = Get-PSResourceRepository -Name $TestRepoName3
        $res3.Uri.LocalPath | Should -Contain $tmpDir3Path
        $res3.Trusted | Should -Be True
        $res3.Priority | Should -Be 20

        $res4 = Get-PSResourceRepository -Name $TestRepoName4
        $res4.Uri.LocalPath | Should -Contain $tmpDir4Path
        $res4.Trusted | Should -Be True
        $res4.Priority | Should -Be 30
        $res4.CredentialInfo.VaultName | Should -Be "testvault"
        $res4.CredentialInfo.SecretName | Should -Be $randomSecret
        $res4.CredentialInfo.Credential | Should -BeNullOrEmpty
    }

    It "register repositories with -Repository parameter, psgallery style repository (RepositoriesParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $hashtable1 = @{PSGallery = $True}
        Register-PSResourceRepository -Repository $hashtable1
        $res = Get-PSResourceRepository -Name $PSGalleryName
        $res.Uri | Should -Be $PSGalleryUri
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "register repositories with -Repository parameter, name and psgallery parameter styles (RepositoriesParameterSet)" {
        Unregister-PSResourceRepository -Name $PSGalleryName
        $hashtable1 = @{PSGallery = $True}
        $hashtable2 = @{Name = $TestRepoName1; Uri = $tmpDir1Path}
        $hashtable3 = @{Name = $TestRepoName2; Uri = $tmpDir2Path; Trusted = $True}
        $hashtable4 = @{Name = $TestRepoName3; Uri = $tmpDir3Path; Trusted = $True; Priority = 20}
        $hashtable5 = @{Name = $TestRepoName4; Uri = $tmpDir4Path; Trusted = $True; Priority = 30; CredentialInfo = (New-Object Microsoft.PowerShell.PowerShellGet.UtilClasses.PSCredentialInfo ("testvault", $randomSecret))}
        $arrayOfHashtables = $hashtable1, $hashtable2, $hashtable3, $hashtable4, $hashtable5

        Register-PSResourceRepository -Repository $arrayOfHashtables

        $res1 = Get-PSResourceRepository -Name $PSGalleryName
        $res1.Uri | Should -Be $PSGalleryUri
        $res1.Trusted | Should -Be False
        $res1.Priority | Should -Be 50

        $res2 = Get-PSResourceRepository -Name $TestRepoName1
        $res2.Uri.LocalPath | Should -Contain $tmpDir1Path
        $res2.Trusted | Should -Be False
        $res2.Priority | Should -Be 50

        $res3 = Get-PSResourceRepository -Name $TestRepoName2
        $res3.Uri.LocalPath | Should -Contain $tmpDir2Path
        $res3.Trusted | Should -Be True
        $res3.Priority | Should -Be 50

        $res4 = Get-PSResourceRepository -Name $TestRepoName3
        $res4.Uri.LocalPath | Should -Contain $tmpDir3Path
        $res4.Trusted | Should -Be True
        $res4.Priority | Should -Be 20

        $res5 = Get-PSResourceRepository -Name $TestRepoName4
        $res5.Uri.LocalPath | Should -Contain $tmpDir4Path
        $res5.Trusted | Should -Be True
        $res5.Priority | Should -Be 30
        $res5.CredentialInfo.VaultName | Should -Be "testvault"
        $res5.CredentialInfo.SecretName | Should -Be $randomSecret
        $res5.CredentialInfo.Credential | Should -BeNullOrEmpty
    }

    It "not register repository when Name is provided but Uri is not" {
        {Register-PSResourceRepository -Name $TestRepoName1 -Uri "" -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    It "not register repository when Name is empty but Uri is provided" {
        {Register-PSResourceRepository -Name "" -Uri $tmpDir1Path -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    It "not register repository when Name is null but Uri is provided" {
        {Register-PSResourceRepository -Name $null -Uri $tmpDir1Path -ErrorAction Stop} | Should -Throw -ErrorId "ParameterArgumentValidationError,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    It "not register repository when Name is just whitespace but Uri is provided" {
        {Register-PSResourceRepository -Name " " -Uri $tmpDir1Path -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    It "not register PSGallery with NameParameterSet" {
        {Register-PSResourceRepository -Name $PSGalleryName -Uri $PSGalleryUri -ErrorAction Stop} | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    # this error message comes from the parameter cmdlet tags (earliest point of detection)
    It "not register PSGallery when PSGallery parameter provided with Name, Uri or CredentialInfo" {
        {Register-PSResourceRepository -PSGallery -Name $PSGalleryName -ErrorAction Stop} | Should -Throw -ErrorId "AmbiguousParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
        {Register-PSResourceRepository -PSGallery -Uri $PSGalleryUri -ErrorAction Stop} | Should -Throw -ErrorId "AmbiguousParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
        {Register-PSResourceRepository -PSGallery -CredentialInfo $credentialInfo1 -ErrorAction Stop} | Should -Throw -ErrorId "AmbiguousParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
    }

    $testCases = @{Type = "Name key specified with PSGallery key"; IncorrectHashTable = @{PSGallery = $True; Name=$PSGalleryName}},
                 @{Type = "Uri key specified with PSGallery key";  IncorrectHashTable = @{PSGallery = $True; Uri=$PSGalleryUri}},
                 @{Type = "CredentialInfo key specified with PSGallery key";  IncorrectHashTable = @{PSGallery = $True; CredentialInfo = $credentialInfo1}}

    It "not register incorrectly formatted PSGallery type repo among correct ones when incorrect type is <Type>" -TestCases $testCases {
        param($Type, $IncorrectHashTable)

        $correctHashtable1 = @{Name = $TestRepoName1; Uri = $tmpDir1Path}
        $correctHashtable2 = @{Name = $TestRepoName2; Uri = $tmpDir2Path; Trusted = $True}
        $correctHashtable3 = @{Name = $TestRepoName3; Uri = $tmpDir3Path; Trusted = $True; Priority = 20}
        $arrayOfHashtables = $correctHashtable1, $correctHashtable2, $IncorrectHashTable, $correctHashtable3

        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -Repository $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "NotProvideNameUriCredentialInfoForPSGalleryRepositoriesParameterSetRegistration,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"

        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res.Name | Should -Be $TestRepoName1

        $res2 = Get-PSResourceRepository -Name $TestRepoName2
        $res2.Name | Should -Be $TestRepoName2

        $res3 = Get-PSResourceRepository -Name $TestRepoName3
        $res3.Name | Should -Be $TestRepoName3
    }

    It "not register incorrectly formatted -Name type repo among correct ones, where incorrect one is missing -Name" {
        $correctHashtable1 = @{Name = $TestRepoName2; Uri = $tmpDir2Path; Trusted = $True}
        $correctHashtable2 = @{Name = $TestRepoName3; Uri = $tmpDir3Path; Trusted = $True; Priority = 20}
        $correctHashtable3 = @{PSGallery = $True; Priority = 30};
        $IncorrectHashTable = @{Uri = $tmpDir1Path};

        $arrayOfHashtables = $correctHashtable1, $correctHashtable2, $IncorrectHashTable, $correctHashtable3
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -Repository $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue

        $ErrorId = "NullNameForRepositoriesParameterSetRegistration,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -Be $ErrorId

        $res = Get-PSResourceRepository -Name $TestRepoName2
        $res.Name | Should -Be $TestRepoName2

        $res2 = Get-PSResourceRepository -Name $TestRepoName3
        $res2.Name | Should -Be $TestRepoName3

        $res3 = Get-PSResourceRepository -Name $PSGalleryName
        $res3.Name | Should -Be $PSGalleryName
        $res3.Priority | Should -Be 30
    }

    It "not register incorrectly formatted -Name type repo among correct ones, where incorrect type has -Name of PSGallery" {
        $correctHashtable1 = @{Name = $TestRepoName2; Uri = $tmpDir2Path; Trusted = $True}
        $correctHashtable2 = @{Name = $TestRepoName3; Uri = $tmpDir3Path; Trusted = $True; Priority = 20}
        $correctHashtable3 = @{PSGallery = $True; Priority = 30};
        $IncorrectHashTable = @{Name = $PSGalleryName; Uri = $tmpDir1Path};  

        $arrayOfHashtables = $correctHashtable1, $correctHashtable2, $IncorrectHashTable, $correctHashtable3
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -Repository $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue

        $ErrorId = "PSGalleryProvidedAsNameRepoPSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -Be $ErrorId

        $res = Get-PSResourceRepository -Name $TestRepoName2
        $res.Name | Should -Be $TestRepoName2

        $res2 = Get-PSResourceRepository -Name $TestRepoName3
        $res2.Name | Should -Be $TestRepoName3

        $res3 = Get-PSResourceRepository -Name $PSGalleryName
        $res3.Name | Should -Be $PSGalleryName
        $res3.Priority | Should -Be 30
    }

    It "not register incorrectly formatted Name type repo among correct ones when incorrect type is -Uri not specified" {
        $correctHashtable1 = @{Name = $TestRepoName2; Uri = $tmpDir2Path; Trusted = $True}
        $correctHashtable2 = @{Name = $TestRepoName3; Uri = $tmpDir3Path; Trusted = $True; Priority = 20}
        $correctHashtable3 = @{PSGallery = $True; Priority = 30};
        $IncorrectHashTable = @{Name = $TestRepoName1};

        $arrayOfHashtables = $correctHashtable1, $correctHashtable2, $IncorrectHashTable, $correctHashtable3
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -Repository $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue

        $ErrorId = "NullUriForRepositoriesParameterSetRegistration,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -Be $ErrorId

        $res = Get-PSResourceRepository -Name $TestRepoName2
        $res.Name | Should -Be $TestRepoName2

        $res2 = Get-PSResourceRepository -Name $TestRepoName3
        $res2.Name | Should -Be $TestRepoName3

        $res3 = Get-PSResourceRepository -Name $PSGalleryName
        $res3.Name | Should -Be $PSGalleryName
        $res3.Priority | Should -Be 30
    }

    It "not register incorrectly formatted Name type repo among correct ones when incorrect type is -Uri is not valid scheme" {
        $correctHashtable1 = @{Name = $TestRepoName2; Uri = $tmpDir2Path; Trusted = $True}
        $correctHashtable2 = @{Name = $TestRepoName3; Uri = $tmpDir3Path; Trusted = $True; Priority = 20}
        $correctHashtable3 = @{PSGallery = $True; Priority = 30};
        $IncorrectHashTable = @{Name = $TestRepoName1; Uri="www.google.com"};

        $arrayOfHashtables = $correctHashtable1, $correctHashtable2, $IncorrectHashTable, $correctHashtable3
        Unregister-PSResourceRepository -Name $PSGalleryName
        Register-PSResourceRepository -Repository $arrayOfHashtables -ErrorVariable err -ErrorAction SilentlyContinue

        $ErrorId = "InvalidUri,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"
        $err.Count | Should -Not -Be 0
        $err[0].FullyQualifiedErrorId | Should -Be $ErrorId

        $res = Get-PSResourceRepository -Name $TestRepoName2
        $res.Name | Should -Be $TestRepoName2

        $res2 = Get-PSResourceRepository -Name $TestRepoName3
        $res2.Name | Should -Be $TestRepoName3

        $res3 = Get-PSResourceRepository -Name $PSGalleryName
        $res3.Name | Should -Be $PSGalleryName
        $res3.Priority | Should -Be 30
    }

    It "should register repository with relative location provided as Uri" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri "./"
        $res = Get-PSResourceRepository -Name $TestRepoName1

        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $relativeCurrentPath
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 50
    }

    It "should update a repository if -Force is used" {
        Register-PSResourceRepository -Name $TestRepoName1 -Uri "./"
        Register-PSResourceRepository -Name $TestRepoName1 -Uri "./" -Priority 3 -Force
        $res = Get-PSResourceRepository -Name $TestRepoName1

        $res.Name | Should -Be $TestRepoName1
        $Res.Uri.LocalPath | Should -Contain $relativeCurrentPath
        $res.Trusted | Should -Be False
        $res.Priority | Should -Be 3
    }

    It "should register local file share NuGet based repository" {
        Register-PSResourceRepository -Name "localFileShareTestRepo" -Uri "\\hcgg.rest.of.domain.name\test\ITxx\team\NuGet\"
        $res = Get-PSResourceRepository -Name "localFileShareTestRepo"

        $res.Name | Should -Be "localFileShareTestRepo"
        $res.Uri.LocalPath | Should -Contain "\\hcgg.rest.of.domain.name\test\ITxx\team\NuGet\"
    }

    It "prints a warning if CredentialInfo is passed in without SecretManagement module setup" {
        $output = Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path -Trusted -Priority 20 -CredentialInfo $credentialInfo1 3>&1
        $output | Should -Match "Microsoft.PowerShell.SecretManagement module cannot be found"

        $res = Get-PSResourceRepository -Name $TestRepoName1
        $res | Should -Not -BeNullOrEmpty
    }

    It "throws error if CredentialInfo is passed in with Credential property without SecretManagement module setup" {
        { Register-PSResourceRepository -Name $TestRepoName1 -Uri $tmpDir1Path -Trusted -Priority 20 -CredentialInfo $credentialInfo2 } | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PowerShellGet.Cmdlets.RegisterPSResourceRepository"

        $res = Get-PSResourceRepository -Name $TestRepoName1 -ErrorAction Ignore
        $res | Should -BeNullOrEmpty
    }
}
