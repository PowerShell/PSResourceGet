# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$testDir = (get-item $psscriptroot).parent.FullName

function CreateTestModule
{
    param (
        [string] $Path = "$TestDrive",
        [string] $ModuleName = 'TestModule'
    )

    $modulePath = Join-Path -Path $Path -ChildPath $ModuleName
    $moduleMan = Join-Path $modulePath -ChildPath ($ModuleName + '.psd1')
    $moduleSrc = Join-Path $modulePath -ChildPath ($ModuleName + '.psm1')

    if ( Test-Path -Path $modulePath) {
        Remove-Item -Path $modulePath -Recurse -Force
    }

    $null = New-Item -Path $modulePath -ItemType Directory -Force

    @'
    @{{
        RootModule        = "{0}.psm1"
        ModuleVersion     = '1.0.0'
        Author            = 'None'
        Description       = 'None'
        GUID              = '0c2829fc-b165-4d72-9038-ae3a71a755c1'
        FunctionsToExport = @('Test1')
        RequiredModules   = @('NonExistentModule')
    }}
'@ -f $ModuleName | Out-File -FilePath $moduleMan

    @'
    function Test1 {
        Write-Output 'Hello from Test1'
    }
'@ | Out-File -FilePath $moduleSrc
}

Describe "Test Compress-PSResource" -tags 'CI' {
    BeforeAll {
        Get-NewPSResourceRepositoryFile

        # Register temporary repositories
        $tmpRepoPath = Join-Path -Path $TestDrive -ChildPath "tmpRepoPath"
        New-Item $tmpRepoPath -Itemtype directory -Force
        $testRepository = "testRepository"
        Register-PSResourceRepository -Name $testRepository -Uri $tmpRepoPath -Priority 1 -ErrorAction SilentlyContinue
        $script:repositoryPath = [IO.Path]::GetFullPath((get-psresourcerepository "testRepository").Uri.AbsolutePath)

        $tmpRepoPath2 = Join-Path -Path $TestDrive -ChildPath "tmpRepoPath2"
        New-Item $tmpRepoPath2 -Itemtype directory -Force
        $testRepository2 = "testRepository2"
        Register-PSResourceRepository -Name $testRepository2 -Uri $tmpRepoPath2 -ErrorAction SilentlyContinue
        $script:repositoryPath2 = [IO.Path]::GetFullPath((get-psresourcerepository "testRepository2").Uri.AbsolutePath)

        # Create module
        $script:tmpModulesPath = Join-Path -Path $TestDrive -ChildPath "tmpModulesPath"
        $script:PublishModuleName = "PSGetTestModule"
        $script:PublishModuleBase = Join-Path $script:tmpModulesPath -ChildPath $script:PublishModuleName
        if(!(Test-Path $script:PublishModuleBase))
        {
            New-Item -Path $script:PublishModuleBase -ItemType Directory -Force
        }
		$script:PublishModuleBaseUNC = $script:PublishModuleBase -Replace '^(.):', '\\localhost\$1$'

        #Create dependency module
        $script:DependencyModuleName = "PackageManagement"
        $script:DependencyModuleBase = Join-Path $script:tmpModulesPath -ChildPath $script:DependencyModuleName
        if(!(Test-Path $script:DependencyModuleBase))
        {
            New-Item -Path $script:DependencyModuleBase -ItemType Directory -Force
        }

        # Create temp destination path
        $script:destinationPath = [IO.Path]::GetFullPath((Join-Path -Path $TestDrive -ChildPath "tmpDestinationPath"))
        New-Item $script:destinationPath -ItemType directory -Force

        #Create folder where we shall place all script files to be published for these tests
        $script:tmpScriptsFolderPath = Join-Path -Path $TestDrive -ChildPath "tmpScriptsPath"
        if(!(Test-Path $script:tmpScriptsFolderPath))
        {
            New-Item -Path $script:tmpScriptsFolderPath -ItemType Directory -Force
        }

        # Path to folder, within our test folder, where we store invalid module and script files used for testing
        $script:testFilesFolderPath = Join-Path $testDir -ChildPath "testFiles"

        # Path to specifically to that invalid test modules folder
        $script:testModulesFolderPath = Join-Path $script:testFilesFolderPath -ChildPath "testModules"

        # Path to specifically to that invalid test scripts folder
        $script:testScriptsFolderPath = Join-Path $script:testFilesFolderPath -ChildPath "testScripts"

        # Create test module with missing required module
        CreateTestModule -Path $TestDrive -ModuleName 'ModuleWithMissingRequiredModule'
    }
    AfterAll {
       Get-RevertPSResourceRepositoryFile
    }
    AfterEach {
        # Delete all contents of the repository without deleting the repository directory itself
        $pkgsToDelete = Join-Path -Path "$script:repositoryPath" -ChildPath "*"
        Remove-Item $pkgsToDelete -Recurse

        $pkgsToDelete = Join-Path -Path "$script:repositoryPath2" -ChildPath "*"
        Remove-Item $pkgsToDelete -Recurse

        $pkgsToDelete = Join-Path -Path $script:PublishModuleBase  -ChildPath "*"
        Remove-Item $pkgsToDelete -Recurse -ErrorAction SilentlyContinue
    }

    It "Compress-PSResource compresses a module into a nupkg and saves it to the DestinationPath" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"
        
        Compress-PSResource -Path $script:PublishModuleBase -DestinationPath $script:repositoryPath
        
        $expectedPath = Join-Path -Path $script:repositoryPath -ChildPath "$script:PublishModuleName.$version.nupkg"
        (Get-ChildItem $script:repositoryPath).FullName | Should -Be $expectedPath
    }

    It "Compress a module using -Path positional parameter and -Destination positional parameter" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Compress-PSResource $script:PublishModuleBase $script:repositoryPath

        $expectedPath = Join-Path -Path $script:repositoryPath -ChildPath "$script:PublishModuleName.$version.nupkg"
        (Get-ChildItem $script:repositoryPath).FullName | Should -Be $expectedPath
    }

    It "Compress-PSResource compresses a module and preserves file structure" {
        $version = "1.0.0"
        $testFile = Join-Path -Path "TestSubDirectory" -ChildPath "TestSubDirFile.ps1"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"
        New-Item -Path (Join-Path -Path $script:PublishModuleBase -ChildPath $testFile) -Force

        Compress-PSResource -Path $script:PublishModuleBase -DestinationPath $script:repositoryPath

        # Must change .nupkg to .zip so that Expand-Archive can work on Windows PowerShell
        $nupkgPath = Join-Path -Path $script:repositoryPath -ChildPath "$script:PublishModuleName.$version.nupkg"
        $zipPath = Join-Path -Path $script:repositoryPath -ChildPath "$script:PublishModuleName.$version.zip"
        Rename-Item -Path $nupkgPath -NewName $zipPath 
        $unzippedPath = Join-Path -Path $TestDrive -ChildPath "$script:PublishModuleName"
        New-Item $unzippedPath -Itemtype directory -Force
        Expand-Archive -Path $zipPath -DestinationPath $unzippedPath

        Test-Path -Path (Join-Path -Path $unzippedPath -ChildPath $testFile) | Should -Be $True
    }

    It "Compress-PSResource -PassThru returns the path to the nupkg" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        $nupkgPath = Compress-PSResource -Path $script:PublishModuleBase -DestinationPath $script:repositoryPath -PassThru

        $expectedPath = Join-Path -Path $script:repositoryPath -ChildPath "$script:PublishModuleName.$version.nupkg"
        $nupkgPath | Should -Be $expectedPath
    }

<# Test for Signing the nupkg. Signing doesn't work
    It "Compressed Module is able to be signed with a certificate" {
		$version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

		Compress-PSResource -Path $script:PublishModuleBase -DestinationPath $script:repositoryPath2
		
		$expectedPath = Join-Path -Path $script:repositoryPath2 -ChildPath "$script:PublishModuleName.$version.nupkg"
		(Get-ChildItem $script:repositoryPath2).FullName | Should -Be $expectedPath

        # create test cert
        # Create a self-signed certificate for code signing
        $testCert = New-SelfSignedCertificate -Subject "CN=NuGet Test Developer, OU=Use for testing purposes ONLY" -FriendlyName "NuGetTestDeveloper" -Type CodeSigning -KeyUsage DigitalSignature -KeyLength 2048 -KeyAlgorithm RSA -HashAlgorithm SHA256 -Provider "Microsoft Enhanced RSA and AES Cryptographic Provider" -CertStoreLocation "Cert:\CurrentUser\My"
        
        # sign the nupkg
        $nupkgPath = Join-Path -Path $script:repositoryPath2 -ChildPath "$script:PublishModuleName.$version.nupkg"
        Set-AuthenticodeSignature -FilePath $nupkgPath -Certificate $testCert

        # Verify the file was signed
        $signature = Get-AuthenticodeSignature -FilePath $nupkgPath
        $signature.Status | Should -Be 'Valid'
	}
    #>
}
