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

Describe "Test Publish-PSResource" -tags 'CI' {
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

    It "Publish module with required module not installed on the local machine using -SkipModuleManifestValidate" {
        # Skip the module manifest validation test, which fails from the missing manifest required module.
        $testModulePath = Join-Path -Path $TestDrive -ChildPath ModuleWithMissingRequiredModule
        Publish-PSResource -Path $testModulePath -Repository $testRepository2 -Confirm:$false -SkipDependenciesCheck -SkipModuleManifestValidate

        $expectedPath = Join-Path -Path $script:repositoryPath2  -ChildPath 'ModuleWithMissingRequiredModule.1.0.0.nupkg'
        $publishedModuleFound = Test-Path -Path $expectedPath
        $publishedModuleFound | Should -BeTrue

        if ($publishedModuleFound) {
            Remove-Item $expectedPath -Force -ErrorAction SilentlyContinue
        }
    }

    It "Publish a module with -Path to the highest priority repo" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $script:PublishModuleBase

        $expectedPath = Join-Path -Path $script:repositoryPath  -ChildPath "$script:PublishModuleName.$version.nupkg"
        (Get-ChildItem $script:repositoryPath).FullName | Should -Be $expectedPath
    }

    It "Publish a module using -Path positional parameter" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource $script:PublishModuleBase

        $expectedPath = Join-Path -Path $script:repositoryPath  -ChildPath "$script:PublishModuleName.$version.nupkg"
        (Get-ChildItem $script:repositoryPath).FullName | Should -Be $expectedPath
    }

	#region Local Source Path
    It "Publish a module with -Path and -Repository" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $script:PublishModuleBase -Repository $testRepository2

        $expectedPath = Join-Path -Path $script:repositoryPath2  -ChildPath "$script:PublishModuleName.$version.nupkg"
        (Get-ChildItem $script:repositoryPath2).FullName | Should -Be $expectedPath
    }

    It "Publish a module with -Path pointing to a module directory (parent directory has same name)" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $script:PublishModuleBase -Repository $testRepository2

        $expectedPath = Join-Path -Path $script:repositoryPath2  -ChildPath "$script:PublishModuleName.$version.nupkg"
        (Get-ChildItem $script:repositoryPath2).FullName | Should -Be $expectedPath
    }

    It "Publish a module with -Path pointing to a module directory (parent directory has different name)" {
        $version = "1.0.0"
        $newModuleRoot = Join-Path -Path $script:PublishModuleBase -ChildPath "NewTestParentDirectory"
        New-Item -Path $newModuleRoot -ItemType Directory
        New-ModuleManifest -Path (Join-Path -Path $newModuleRoot -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $newModuleRoot -Repository $testRepository2

        $expectedPath = Join-Path -Path $script:repositoryPath2  -ChildPath "$script:PublishModuleName.$version.nupkg"
        (Get-ChildItem $script:repositoryPath2).FullName | Should -Be $expectedPath
    }

    It "Publish a module with -Path pointing to a .psd1 (parent directory has same name)" {
        $version = "1.0.0"
        $manifestPath = Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1"
        New-ModuleManifest -Path $manifestPath -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $manifestPath -Repository $testRepository2

        $expectedPath = Join-Path -Path $script:repositoryPath2  -ChildPath "$script:PublishModuleName.$version.nupkg"
        (Get-ChildItem $script:repositoryPath2).FullName | Should -Be $expectedPath
    }

    It "Publish a module with -Path pointing to a .psd1 (parent directory has different name)" {
        $version = "1.0.0"
        $newModuleRoot = Join-Path -Path $script:PublishModuleBase -ChildPath "NewTestParentDirectory"
        New-Item -Path $newModuleRoot -ItemType Directory
        $manifestPath = Join-Path -Path $newModuleRoot -ChildPath "$script:PublishModuleName.psd1"
        New-ModuleManifest -Path $manifestPath -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $manifestPath -Repository $testRepository2

        $expectedPath = Join-Path -Path $script:repositoryPath2  -ChildPath "$script:PublishModuleName.$version.nupkg"
        (Get-ChildItem $script:repositoryPath2).FullName | Should -Be $expectedPath
    }
	#endregion Local Source Path

	#region UNC Source Path
    It "Publish a module with -Path and -Repository, with the path pointing to a network share" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBaseUNC -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $script:PublishModuleBaseUNC -Repository $testRepository2

        $expectedPath = Join-Path -Path $script:repositoryPath2  -ChildPath "$script:PublishModuleName.$version.nupkg"
        (Get-ChildItem $script:repositoryPath2).FullName | Should -Be $expectedPath
    }

    It "Publish a module with -Path pointing to a module directory (parent directory has same name) on a network share" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBaseUNC -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $script:PublishModuleBaseUNC -Repository $testRepository2

        $expectedPath = Join-Path -Path $script:repositoryPath2  -ChildPath "$script:PublishModuleName.$version.nupkg"
        (Get-ChildItem $script:repositoryPath2).FullName | Should -Be $expectedPath
    }

    It "Publish a module with -Path pointing to a module directory (parent directory has different name) on a network share" {
        $version = "1.0.0"
        $newModuleRoot = Join-Path -Path $script:PublishModuleBaseUNC -ChildPath "NewTestParentDirectory"
        New-Item -Path $newModuleRoot -ItemType Directory
        New-ModuleManifest -Path (Join-Path -Path $newModuleRoot -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $newModuleRoot -Repository $testRepository2

        $expectedPath = Join-Path -Path $script:repositoryPath2  -ChildPath "$script:PublishModuleName.$version.nupkg"
        (Get-ChildItem $script:repositoryPath2).FullName | Should -Be $expectedPath
    }

    It "Publish a module with -Path pointing to a .psd1 (parent directory has same name) on a network share" {
        $version = "1.0.0"
        $manifestPath = Join-Path -Path $script:PublishModuleBaseUNC -ChildPath "$script:PublishModuleName.psd1"
        New-ModuleManifest -Path $manifestPath -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $manifestPath -Repository $testRepository2

        $expectedPath = Join-Path -Path $script:repositoryPath2  -ChildPath "$script:PublishModuleName.$version.nupkg"
        (Get-ChildItem $script:repositoryPath2).FullName | Should -Be $expectedPath
    }

    It "Publish a module with -Path pointing to a .psd1 (parent directory has different name) on a network share" {
        $version = "1.0.0"
        $newModuleRoot = Join-Path -Path $script:PublishModuleBaseUNC -ChildPath "NewTestParentDirectory"
        New-Item -Path $newModuleRoot -ItemType Directory
        $manifestPath = Join-Path -Path $newModuleRoot -ChildPath "$script:PublishModuleName.psd1"
        New-ModuleManifest -Path $manifestPath -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $manifestPath -Repository $testRepository2

        $expectedPath = Join-Path -Path $script:repositoryPath2  -ChildPath "$script:PublishModuleName.$version.nupkg"
        (Get-ChildItem $script:repositoryPath2).FullName | Should -Be $expectedPath
    }
	#endregion UNC Source Path

    It "Publish a module with dependencies" {
        # Create dependency module
        $dependencyVersion = "2.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:DependencyModuleBase -ChildPath "$script:DependencyModuleName.psd1") -ModuleVersion $dependencyVersion -Description "$script:DependencyModuleName module"

        Publish-PSResource -Path $script:DependencyModuleBase

        # Create module to test
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module" -RequiredModules @(@{ModuleName = 'PackageManagement'; ModuleVersion = '2.0.0' })

        Publish-PSResource -Path $script:PublishModuleBase

        $nupkg = Get-ChildItem $script:repositoryPath | select-object -Last 1
        $nupkg.Name | Should -Be "$script:PublishModuleName.$version.nupkg"
    }

    It "Publish a module with a dependency that is not published, should throw" {
        $version = "1.0.0"
        $dependencyVersion = "2.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module" -RequiredModules @(@{ModuleName = 'PackageManagement'; ModuleVersion = '1.4.4' })

        {Publish-PSResource -Path $script:PublishModuleBase -ErrorAction Stop} | Should -Throw -ErrorId "FindVersionFailure,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"
    }


    It "Publish a module with -SkipDependenciesCheck" {
        $version = "1.0.0"
        $dependencyVersion = "2.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module" -RequiredModules @{ModuleName = "$script:DependencyModuleName"; ModuleVersion = "$dependencyVersion" }

        Publish-PSResource -Path $script:PublishModuleBase -SkipDependenciesCheck

        $expectedPath = Join-Path -Path $script:repositoryPath -ChildPath "$script:PublishModuleName.$version.nupkg"
        (Get-ChildItem $script:repositoryPath).FullName | select-object -Last 1 | Should -Be $expectedPath
    }

    It "Publish a module and preserve file structure" {
        $version = "1.0.0"
        $testFile = Join-Path -Path "TestSubDirectory" -ChildPath "TestSubDirFile.ps1"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"
        New-Item -Path (Join-Path -Path $script:PublishModuleBase -ChildPath $testFile) -Force

        Publish-PSResource -Path $script:PublishModuleBase

        # Must change .nupkg to .zip so that Expand-Archive can work on Windows PowerShell
        $nupkgPath = Join-Path -Path $script:repositoryPath -ChildPath "$script:PublishModuleName.$version.nupkg"
        $zipPath = Join-Path -Path $script:repositoryPath -ChildPath "$script:PublishModuleName.$version.zip"
        Rename-Item -Path $nupkgPath -NewName $zipPath 
        $unzippedPath = Join-Path -Path $TestDrive -ChildPath "$script:PublishModuleName"
        New-Item $unzippedPath -Itemtype directory -Force
        Expand-Archive -Path $zipPath -DestinationPath $unzippedPath

        Test-Path -Path (Join-Path -Path $unzippedPath -ChildPath $testFile) | Should -Be $True
    }

    <# The following tests are related to passing in parameters to customize a nuspec.
     # These parameters are not going in the current release, but is open for discussion to include in the future.
    It "Publish a module with -Nuspec" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"  -NestedModules "$script:PublishModuleName.psm1"
        # Create nuspec
        $nuspec =
@'
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd">
  <metadata>
    <id>PSGetTestModule</id>
    <version>1.0.0</version>
    <authors>americks</authors>
    <owners>americks</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>test</description>
    <releaseNotes></releaseNotes>
    <copyright>(c) 2021 Contoso Corporation. All rights reserved.</copyright>
    <tags>PSModule</tags>
  </metadata>
</package>
'@
        $nuspecPath = Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.nuspec"
        New-Item $nuspecPath -ItemType File -Value $nuspec
        Publish-PSResource -Path $script:PublishModuleBase -Nuspec $nuspecPath
        $expectedPath = Join-Path -Path $script:repositoryPath  -ChildPath "$script:PublishModuleName.$version.nupkg"
        Get-ChildItem $script:repositoryPath | Should -Be $expectedPath
    }
    It "Publish a module with -ReleaseNotes" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"  -NestedModules "$script:PublishModuleName.psm1"
        $releaseNotes = "Test release notes."
        Publish-PSResource -Path $script:PublishModuleBase -ReleaseNotes $releaseNotes
        $expectedNupkgPath = Join-Path -Path $script:repositoryPath  -ChildPath "$script:PublishModuleName.$version.nupkg"
        Get-ChildItem $script:repositoryPath | Should -Be $expectedNupkgPath
        $expectedExpandedPath = Join-Path -Path $script:repositoryPath -ChildPath "ExpandedPackage"
        New-Item -Path $expectedExpandedPath -ItemType directory
        Expand-Archive -Path $expectedNupkgPath -DestinationPath $expectedExpandedPath
        $expectedNuspec = Join-path -Path $expectedExpandedPath -ChildPath "$script:PublishModuleName.nuspec"
        $expectedNuspecContents =  Get-Content -Path $expectedNuspec -Raw
        $expectedNuspecContents.Contains($releaseNotes) | Should Be $true
    }
    It "Publish a module with -LicenseUrl" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"  -NestedModules "$script:PublishModuleName.psm1"
        $licenseUrl = "https://www.fakelicenseurl.com"
        Publish-PSResource -Path $script:PublishModuleBase -LicenseUrl $licenseUrl
        $expectedNupkgPath = Join-Path -Path $script:repositoryPath  -ChildPath "$script:PublishModuleName.$version.nupkg"
        Get-ChildItem $script:repositoryPath | Should -Be $expectedNupkgPath
        $expectedExpandedPath = Join-Path -Path $script:repositoryPath -ChildPath "ExpandedPackage"
        New-Item -Path $expectedExpandedPath -ItemType directory
        Expand-Archive -Path $expectedNupkgPath -DestinationPath $expectedExpandedPath
        $expectedNuspec = Join-path -Path $expectedExpandedPath -ChildPath "$script:PublishModuleName.nuspec"
        $expectedNuspecContents =  Get-Content -Path $expectedNuspec -Raw
        $expectedNuspecContents.Contains($licenseUrl) | Should Be $true
    }
    It "Publish a module with -IconUrl" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"  -NestedModules "$script:PublishModuleName.psm1"
        $iconUrl = "https://www.fakeiconurl.com"
        Publish-PSResource -Path $script:PublishModuleBase -IconUrl $iconUrl
        $expectedNupkgPath = Join-Path -Path $script:repositoryPath  -ChildPath "$script:PublishModuleName.$version.nupkg"
        Get-ChildItem $script:repositoryPath | Should -Be $expectedNupkgPath
        $expectedExpandedPath = Join-Path -Path $script:repositoryPath -ChildPath "ExpandedPackage"
        New-Item -Path $expectedExpandedPath -ItemType directory
        Expand-Archive -Path $expectedNupkgPath -DestinationPath $expectedExpandedPath
        $expectedNuspec = Join-path -Path $expectedExpandedPath -ChildPath "$script:PublishModuleName.nuspec"
        $expectedNuspecContents =  Get-Content -Path $expectedNuspec -Raw
        $expectedNuspecContents.Contains($iconUrl) | Should Be $true
    }
    It "Publish a module with -ProjectUrl" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"  -NestedModules "$script:PublishModuleName.psm1"
        $projectUrl = "https://www.fakeprojectUrl.com"
        Publish-PSResource -Path $script:PublishModuleBase -ProjectUrl $projectUrl
        $expectedNupkgPath = Join-Path -Path $script:repositoryPath  -ChildPath "$script:PublishModuleName.$version.nupkg"
        Get-ChildItem $script:repositoryPath | Should -Be $expectedNupkgPath
        $expectedExpandedPath = Join-Path -Path $script:repositoryPath -ChildPath "ExpandedPackage"
        New-Item -Path $expectedExpandedPath -ItemType directory
        Expand-Archive -Path $expectedNupkgPath -DestinationPath $expectedExpandedPath
        $expectedNuspec = Join-path -Path $expectedExpandedPath -ChildPath "$script:PublishModuleName.nuspec"
        $expectedNuspecContents =  Get-Content -Path $expectedNuspec -Raw
        $expectedNuspecContents.Contains($projectUrl) | Should Be $true
    }
    It "Publish a module with -Tags" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"  -NestedModules "$script:PublishModuleName.psm1"
        $tags = "Tag1"
        Publish-PSResource -Path $script:PublishModuleBase -Tags $tags
        $expectedNupkgPath = Join-Path -Path $script:repositoryPath  -ChildPath "$script:PublishModuleName.$version.nupkg"
        Get-ChildItem $script:repositoryPath | Should -Be $expectedNupkgPath
        $expectedExpandedPath = Join-Path -Path $script:repositoryPath -ChildPath "ExpandedPackage"
        New-Item -Path $expectedExpandedPath -ItemType directory
        Expand-Archive -Path $expectedNupkgPath -DestinationPath $expectedExpandedPath
        $expectedNuspec = Join-path -Path $expectedExpandedPath -ChildPath "$script:PublishModuleName.nuspec"
        $expectedNuspecContents =  Get-Content -Path $expectedNuspec -Raw
        $expectedNuspecContents.Contains($tags) | Should Be $true
    }
#>
    It "Publish a module to PSGallery without -APIKey, should throw" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $script:PublishModuleBase -Repository PSGallery -ErrorAction SilentlyContinue

        $Error[0].FullyQualifiedErrorId | Should -be "401ApiKeyError,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"
    }

    It "Publish a module to PSGallery using incorrect API key, should throw" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $script:PublishModuleBase -Repository PSGallery -APIKey "123456789" -ErrorAction SilentlyContinue

        $Error[0].FullyQualifiedErrorId | Should -be "403Error,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"
    }

    It "Publish a module with -Path -Repository and -DestinationPath" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $script:PublishModuleBase -Repository $testRepository2 -DestinationPath $script:destinationPath

        $expectedPath = Join-Path -Path $script:repositoryPath2 -ChildPath "$script:PublishModuleName.$version.nupkg"

        (Get-ChildItem $script:repositoryPath2).FullName | Should -Be $expectedPath

        $expectedPath = Join-Path -Path $script:destinationPath -ChildPath "$script:PublishModuleName.$version.nupkg"
        (Get-ChildItem $script:destinationPath).FullName | Should -Be $expectedPath
    }

    It "Publish a module with -Path -Repository and -DestinationPath" {
        $moduleName = "Pester"
        $moduleVersion = "5.5.0"
        Save-PSResource -Name $moduleName -Path $tmpRepoPath -Version $moduleVersion -Repository PSGallery -TrustRepository
        $modulePath = Join-Path -Path $tmpRepoPath -ChildPath $moduleName 
        $moduleVersionPath = Join-Path -Path $modulePath -ChildPath $moduleVersion
        $moduleManifestPath = Join-path -Path $moduleVersionPath -ChildPath "$moduleName.psd1"
        Publish-PSResource -Path $moduleManifestPath -Repository $testRepository2
        $expectedPath = Join-Path -Path $script:repositoryPath2 -ChildPath "$moduleName.$moduleVersion.nupkg"
        (Get-ChildItem $script:repositoryPath2).FullName | Should -Be $expectedPath
    }

    It "Publish a module and clean up properly when file in module is readonly" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        # Create a readonly file that will throw access denied error if deletion is attempted
        $file = Join-Path -Path $script:PublishModuleBase -ChildPath "inaccessiblefile.txt"
        New-Item $file -Itemtype file -Force
        Set-ItemProperty -Path $file -Name IsReadOnly -Value $true

        Publish-PSResource -Path $script:PublishModuleBase -Repository $testRepository2

        $expectedPath = Join-Path -Path $script:repositoryPath2 -ChildPath "$script:PublishModuleName.$version.nupkg"
        (Get-ChildItem $script:repositoryPath2).FullName | Should -Be $expectedPath
    }

    It "publish a script locally"{
        $scriptName = "PSGetTestScript"
        $scriptVersion = "1.0.0"

        $params = @{
            Version = $scriptVersion
            GUID = [guid]::NewGuid()
            Author = 'Jane'
            CompanyName = 'Microsoft Corporation'
            Copyright = '(c) 2020 Microsoft Corporation. All rights reserved.'
            Description = "Description for the $scriptName script"
            LicenseUri = "https://$scriptName.com/license"
            IconUri = "https://$scriptName.com/icon"
            ProjectUri = "https://$scriptName.com"
            Tags = @('Tag1','Tag2', "Tag-$scriptName-$scriptVersion")
            ReleaseNotes = "$scriptName release notes"
            }

        $scriptPath = (Join-Path -Path $script:tmpScriptsFolderPath -ChildPath "$scriptName.ps1")
        New-PSScriptFileInfo @params -Path $scriptPath

        Publish-PSResource -Path $scriptPath

        $expectedPath = Join-Path -Path $script:repositoryPath  -ChildPath "$scriptName.$scriptVersion.nupkg"
        (Get-ChildItem $script:repositoryPath).FullName | Should -Be $expectedPath
    }

    It "should publish a script without lines in between comment blocks locally" {
        $scriptName = "ScriptWithoutEmptyLinesBetweenCommentBlocks"
        $scriptVersion = "1.0.0"
        $scriptPath = (Join-Path -Path $script:testScriptsFolderPath -ChildPath "$scriptName.ps1")

        Publish-PSResource -Path $scriptPath

        $expectedPath = Join-Path -Path $script:repositoryPath  -ChildPath "$scriptName.$scriptVersion.nupkg"
        (Get-ChildItem $script:repositoryPath).FullName | Should -Be $expectedPath
    }

    It "should publish a script without lines in help block locally" {
        $scriptName = "ScriptWithoutEmptyLinesInMetadata"
        $scriptVersion = "1.0.0"
        $scriptPath = (Join-Path -Path $script:testScriptsFolderPath -ChildPath "$scriptName.ps1")

        Publish-PSResource -Path $scriptPath

        $expectedPath = Join-Path -Path $script:repositoryPath  -ChildPath "$scriptName.$scriptVersion.nupkg"
        (Get-ChildItem $script:repositoryPath).FullName | Should -Be $expectedPath
    }

    It "should write error and not publish script when Author property is missing" {
        $scriptName = "InvalidScriptMissingAuthor.ps1"
        $scriptVersion = "1.0.0"

        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName
        Publish-PSResource -Path $scriptFilePath -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "psScriptMissingAuthor,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"

        $publishedPath = Join-Path -Path $script:repositoryPath  -ChildPath "$scriptName.$scriptVersion.nupkg"
        Test-Path -Path $publishedPath | Should -Be $false
    }

    It "should write error and not publish script when Version property is missing" {
        $scriptName = "InvalidScriptMissingVersion.ps1"

        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName
        Publish-PSResource -Path $scriptFilePath -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "psScriptMissingVersion,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"

        $publishedPkgs = Get-ChildItem -Path $script:repositoryPath -Filter *.nupkg
        $publishedPkgs | Should -HaveCount 0
    }

    It "should write error and not publish script when Guid property is missing" {
        $scriptName = "InvalidScriptMissingGuid.ps1"
        $scriptVersion = "1.0.0"

        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName
        Publish-PSResource -Path $scriptFilePath -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "psScriptMissingGuid,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"

        $publishedPath = Join-Path -Path $script:repositoryPath  -ChildPath "$scriptName.$scriptVersion.nupkg"
        Test-Path -Path $publishedPath | Should -Be $false
    }

    It "should write error and not publish script when Description property is missing" {
        $scriptName = "InvalidScriptMissingDescription.ps1"
        $scriptVersion = "1.0.0"

        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName
        Publish-PSResource -Path $scriptFilePath -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PSScriptInfoMissingDescription,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"

        $publishedPath = Join-Path -Path $script:repositoryPath  -ChildPath "$scriptName.$scriptVersion.nupkg"
        Test-Path -Path $publishedPath | Should -Be $false
    }

    It "should write error and not publish script when Description block altogether is missing" {
        # we expect .ps1 files to have a separate comment block for .DESCRIPTION property, not to be included in the PSScriptInfo commment block
        $scriptName = "InvalidScriptMissingDescriptionCommentBlock.ps1"
        $scriptVersion = "1.0.0"

        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName
        Publish-PSResource -Path $scriptFilePath -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "missingHelpInfoCommentError,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"

        $publishedPath = Join-Path -Path $script:repositoryPath  -ChildPath "$scriptName.$scriptVersion.nupkg"
        Test-Path -Path $publishedPath | Should -Be $false
    }

    It "Publish a module with that has an invalid version format, should throw" {
        $moduleName = "incorrectmoduleversion"
        $incorrectmoduleversion = Join-Path -Path $script:testModulesFolderPath -ChildPath $moduleName

        {Publish-PSResource -Path $incorrectmoduleversion -ErrorAction Stop} | Should -Throw -ErrorId "InvalidModuleManifest,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"
    }

    It "Publish a module with a dependency that has an invalid version format, should throw" {
        $moduleName = "incorrectdepmoduleversion"
        $incorrectdepmoduleversion = Join-Path -Path $script:testModulesFolderPath -ChildPath $moduleName

        {Publish-PSResource -Path $incorrectdepmoduleversion -ErrorAction Stop} | Should -Throw -ErrorId "InvalidModuleManifest,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"
    }

    It "Publish a module with using an invalid file path (path to .psm1), should throw" {
        $fileName = "$script:PublishModuleName.psm1"
        $psm1Path = Join-Path -Path $script:PublishModuleBase -ChildPath $fileName
        $null = New-Item -Path $psm1Path -ItemType File -Force

        {Publish-PSResource -Path $psm1Path -Repository $testRepository2 -ErrorAction Stop} | Should -Throw -ErrorId "InvalidPublishPath,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"
    }

    It "Get definition for alias 'pups'" {
        (Get-Alias pups).Definition | Should -BeExactly 'Publish-PSResource'
    }
}
