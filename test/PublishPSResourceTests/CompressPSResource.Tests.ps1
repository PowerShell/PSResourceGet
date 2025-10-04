# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$testDir = (get-item $psscriptroot).parent.FullName

function CreateTestModule {
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

function CompressExpandRetrieveNuspec {
    param(
        [string]$PublishModuleBase,
        [string]$PublishModuleName,
        [string]$ModuleVersion,
        [string]$RepositoryPath,
        [string]$ModuleBasePath,
        [string]$TestDrive,
        [object[]]$RequiredModules,
        [switch]$SkipModuleManifestValidate
    )

    $testFile = Join-Path -Path "TestSubDirectory" -ChildPath "TestSubDirFile.ps1"
    $null = New-ModuleManifest -Path (Join-Path -Path $PublishModuleBase -ChildPath "$PublishModuleName.psd1") -ModuleVersion $version -Description "$PublishModuleName module" -RequiredModules $RequiredModules
    $null = New-Item -Path (Join-Path -Path $PublishModuleBase -ChildPath $testFile) -Force

    $null = Compress-PSResource -Path $PublishModuleBase -DestinationPath $repositoryPath -SkipModuleManifestValidate:$SkipModuleManifestValidate

    # Must change .nupkg to .zip so that Expand-Archive can work on Windows PowerShell
    $nupkgPath = Join-Path -Path $RepositoryPath -ChildPath "$PublishModuleName.$version.nupkg"
    $zipPath = Join-Path -Path $RepositoryPath -ChildPath "$PublishModuleName.$version.zip"
    Rename-Item -Path $nupkgPath -NewName $zipPath
    $unzippedPath = Join-Path -Path $TestDrive -ChildPath "$PublishModuleName"
    $null = New-Item $unzippedPath -Itemtype directory -Force
    $null = Expand-Archive -Path $zipPath -DestinationPath $unzippedPath

    $nuspecPath = Join-Path -Path $unzippedPath -ChildPath "$PublishModuleName.nuspec"
    $nuspecxml = [xml](Get-Content $nuspecPath)
    $null = Remove-Item $unzippedPath -Force -Recurse
    return $nuspecxml
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
        if (!(Test-Path $script:PublishModuleBase)) {
            New-Item -Path $script:PublishModuleBase -ItemType Directory -Force
        }
        $script:PublishModuleBaseUNC = $script:PublishModuleBase -Replace '^(.):', '\\localhost\$1$'

        #Create dependency module
        $script:DependencyModuleName = "PackageManagement"
        $script:DependencyModuleBase = Join-Path $script:tmpModulesPath -ChildPath $script:DependencyModuleName
        if (!(Test-Path $script:DependencyModuleBase)) {
            New-Item -Path $script:DependencyModuleBase -ItemType Directory -Force
        }

        # Create temp destination path
        $script:destinationPath = [IO.Path]::GetFullPath((Join-Path -Path $TestDrive -ChildPath "tmpDestinationPath"))
        New-Item $script:destinationPath -ItemType directory -Force

        #Create folder where we shall place all script files to be published for these tests
        $script:tmpScriptsFolderPath = Join-Path -Path $TestDrive -ChildPath "tmpScriptsPath"
        if (!(Test-Path $script:tmpScriptsFolderPath)) {
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
        $null = Remove-Item $unzippedPath -Force -Recurse
    }

    It "Compresses a script" {
        $scriptName = "PSGetTestScript"
        $scriptVersion = "1.0.0"

        $params = @{
            Version      = $scriptVersion
            GUID         = [guid]::NewGuid()
            Author       = 'Jane'
            CompanyName  = 'Microsoft Corporation'
            Copyright    = '(c) 2020 Microsoft Corporation. All rights reserved.'
            Description  = "Description for the $scriptName script"
            LicenseUri   = "https://$scriptName.com/license"
            IconUri      = "https://$scriptName.com/icon"
            ProjectUri   = "https://$scriptName.com"
            Tags         = @('Tag1', 'Tag2', "Tag-$scriptName-$scriptVersion")
            ReleaseNotes = "$scriptName release notes"
        }

        $scriptPath = (Join-Path -Path $script:tmpScriptsFolderPath -ChildPath "$scriptName.ps1")
        New-PSScriptFileInfo @params -Path $scriptPath

        Compress-PSResource -Path $scriptPath -DestinationPath $script:repositoryPath

        $expectedPath = Join-Path -Path $script:repositoryPath  -ChildPath "$scriptName.$scriptVersion.nupkg"
        (Get-ChildItem $script:repositoryPath).FullName | Should -Be $expectedPath
    }

    It "Compress-PSResource -DestinationPath works for relative paths" {
        $version = "1.0.0"
        $relativePath = ".\RelativeTestModule"
        $relativeDestination = ".\RelativeDestination"

        # Create relative paths
        New-Item -Path $relativePath -ItemType Directory -Force
        New-Item -Path $relativeDestination -ItemType Directory -Force

        # Create module manifest in the relative path
        New-ModuleManifest -Path (Join-Path -Path $relativePath -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        # Compress using relative paths
        Compress-PSResource -Path $relativePath -DestinationPath $relativeDestination

        $expectedPath = Join-Path -Path $relativeDestination -ChildPath "$script:PublishModuleName.$version.nupkg"
        $fileExists = Test-Path -Path $expectedPath
        $fileExists | Should -Be $True

        # Cleanup
        Remove-Item -Path $relativePath -Recurse -Force
        Remove-Item -Path $relativeDestination -Recurse -Force
    }

    It "Compress-PSResource -PassThru returns a FileInfo object with the correct path" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        $fileInfoObject = Compress-PSResource -Path $script:PublishModuleBase -DestinationPath $script:repositoryPath -PassThru

        $expectedPath = Join-Path -Path $script:repositoryPath -ChildPath "$script:PublishModuleName.$version.nupkg"
        $fileInfoObject | Should -BeOfType 'System.IO.FileSystemInfo'
        $fileInfoObject.FullName | Should -Be $expectedPath
        $fileInfoObject.Extension | Should -Be '.nupkg'
        $fileInfoObject.Name | Should -Be "$script:PublishModuleName.$version.nupkg"
    }

    It "Compress-PSResource creates nuspec dependency version range when RequiredVersion is in RequiredModules section" {
        $version = "1.0.0"
        $requiredModules = @(
            @{
                'ModuleName'      = 'PSGetTestRequiredModule'
                'GUID'            = (New-Guid).Guid
                'RequiredVersion' = '2.0.0'
            }
        )
        $compressParams = @{
            'PublishModuleBase'          = $script:PublishModuleBase
            'PublishModuleName'          = $script:PublishModuleName
            'ModuleVersion'              = $version
            'RepositoryPath'             = $script:repositoryPath
            'TestDrive'                  = $TestDrive
            'RequiredModules'            = $requiredModules
            'SkipModuleManifestValidate' = $true
        }
        $nuspecxml = CompressExpandRetrieveNuspec @compressParams
        # removing spaces as the nuget packaging is formatting the version range and adding spaces even when the original nuspec file doesn't have spaces.
        # e.g (,2.0.0] is being formatted to (, 2.0.0]
        $nuspecxml.package.metadata.dependencies.dependency.version.replace(' ', '') | Should -BeExactly '[2.0.0]'
    }

    It "Compress-PSResource creates nuspec dependency version range when ModuleVersion is in RequiredModules section" {
        $version = "1.0.0"
        $requiredModules = @(
            @{
                'ModuleName'    = 'PSGetTestRequiredModule'
                'GUID'          = (New-Guid).Guid
                'ModuleVersion' = '2.0.0'
            }
        )
        $compressParams = @{
            'PublishModuleBase'          = $script:PublishModuleBase
            'PublishModuleName'          = $script:PublishModuleName
            'ModuleVersion'              = $version
            'RepositoryPath'             = $script:repositoryPath
            'TestDrive'                  = $TestDrive
            'RequiredModules'            = $requiredModules
            'SkipModuleManifestValidate' = $true
        }
        $nuspecxml = CompressExpandRetrieveNuspec @compressParams
        $nuspecxml.package.metadata.dependencies.dependency.version.replace(' ', '') | Should -BeExactly '2.0.0'
    }

    It "Compress-PSResource creates nuspec dependency version range when MaximumVersion is in RequiredModules section" {
        $version = "1.0.0"
        $requiredModules = @(
            @{
                'ModuleName'     = 'PSGetTestRequiredModule'
                'GUID'           = (New-Guid).Guid
                'MaximumVersion' = '2.0.0'
            }
        )
        $compressParams = @{
            'PublishModuleBase'          = $script:PublishModuleBase
            'PublishModuleName'          = $script:PublishModuleName
            'ModuleVersion'              = $version
            'RepositoryPath'             = $script:repositoryPath
            'TestDrive'                  = $TestDrive
            'RequiredModules'            = $requiredModules
            'SkipModuleManifestValidate' = $true
        }
        $nuspecxml = CompressExpandRetrieveNuspec @compressParams
        $nuspecxml.package.metadata.dependencies.dependency.version.replace(' ', '') | Should -BeExactly '(,2.0.0]'
    }

    It "Compress-PSResource creates nuspec dependency version range when ModuleVersion and MaximumVersion are in RequiredModules section" {
        $version = "1.0.0"
        $requiredModules = @(
            @{
                'ModuleName'     = 'PSGetTestRequiredModule'
                'GUID'           = (New-Guid).Guid
                'ModuleVersion'  = '1.0.0'
                'MaximumVersion' = '2.0.0'
            }
        )
        $compressParams = @{
            'PublishModuleBase'          = $script:PublishModuleBase
            'PublishModuleName'          = $script:PublishModuleName
            'ModuleVersion'              = $version
            'RepositoryPath'             = $script:repositoryPath
            'TestDrive'                  = $TestDrive
            'RequiredModules'            = $requiredModules
            'SkipModuleManifestValidate' = $true
        }
        $nuspecxml = CompressExpandRetrieveNuspec @compressParams
        $nuspecxml.package.metadata.dependencies.dependency.version.replace(' ', '') | Should -BeExactly '[1.0.0,2.0.0]'
    }

    It "Compress-PSResource creates nuspec dependency version range when there are multiple modules in RequiredModules section" {
        $version = "1.0.0"
        $requiredModules = @(
            @{
                'ModuleName'      = 'PSGetTestRequiredModuleRequiredVersion'
                'GUID'            = (New-Guid).Guid
                'RequiredVersion' = '1.0.0'
            },
            @{
                'ModuleName'    = 'PSGetTestRequiredModuleModuleVersion'
                'GUID'          = (New-Guid).Guid
                'ModuleVersion' = '2.0.0'
            },
            @{
                'ModuleName'     = 'PSGetTestRequiredModuleMaximumVersion'
                'GUID'           = (New-Guid).Guid
                'MaximumVersion' = '3.0.0'
            },
            @{
                'ModuleName'     = 'PSGetTestRequiredModuleModuleAndMaximumVersion'
                'GUID'           = (New-Guid).Guid
                'ModuleVersion'  = '4.0.0'
                'MaximumVersion' = '5.0.0'
            }
        )
        $compressParams = @{
            'PublishModuleBase'          = $script:PublishModuleBase
            'PublishModuleName'          = $script:PublishModuleName
            'ModuleVersion'              = $version
            'RepositoryPath'             = $script:repositoryPath
            'TestDrive'                  = $TestDrive
            'RequiredModules'            = $requiredModules
            'SkipModuleManifestValidate' = $true
        }
        $nuspecxml = CompressExpandRetrieveNuspec @compressParams
        foreach ($dependency in $nuspecxml.package.metadata.dependencies.dependency) {
            switch ($dependency.id) {
                "PSGetTestRequiredModuleRequiredVersion" {
                    $dependency.version.replace(' ', '') | Should -BeExactly '[1.0.0]'
                }
                "PSGetTestRequiredModuleModuleVersion" {
                    $dependency.version.replace(' ', '') | Should -BeExactly '2.0.0'
                }
                "PSGetTestRequiredModuleMaximumVersion" {
                    $dependency.version.replace(' ', '') | Should -BeExactly '(,3.0.0]'
                }
                "PSGetTestRequiredModuleModuleAndMaximumVersion" {
                    $dependency.version.replace(' ', '') | Should -BeExactly '[4.0.0,5.0.0]'
                }
            }
        }
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
