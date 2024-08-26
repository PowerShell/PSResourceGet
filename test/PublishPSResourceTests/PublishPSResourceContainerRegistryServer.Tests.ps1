# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

function CreateTestModule
{
    param (
        [string] $Path = "$TestDrive",
        [string] $ModuleName = 'temp-testmodule'
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
        $script:testDir = (get-item $psscriptroot).parent.FullName
        Get-NewPSResourceRepositoryFile

        # Register repositories
        $ACRRepoName = "ACRRepo"
        $ACRRepoUri = "https://psresourcegettest.azurecr.io"

        $usingAzAuth = $env:USINGAZAUTH -eq 'true'

        if ($usingAzAuth)
        {
            Register-PSResourceRepository -Name $ACRRepoName -ApiVersion 'ContainerRegistry' -Uri $ACRRepoUri -Verbose
        }
        else
        {
            $psCredInfo = New-Object Microsoft.PowerShell.PSResourceGet.UtilClasses.PSCredentialInfo ("SecretStore", "$env:TENANTID")
            Register-PSResourceRepository -Name $ACRRepoName -ApiVersion 'ContainerRegistry' -Uri $ACRRepoUri -CredentialInfo $psCredInfo -Verbose
        }

        # Create module
        $script:tmpModulesPath = Join-Path -Path $TestDrive -ChildPath "tmpModulesPath"
        $script:PublishModuleName = "temp-testmodule" + [System.Guid]::NewGuid();
        $script:PublishModuleBase = Join-Path $script:tmpModulesPath -ChildPath $script:PublishModuleName
        if(!(Test-Path $script:PublishModuleBase))
        {
            New-Item -Path $script:PublishModuleBase -ItemType Directory -Force
        }
		$script:PublishModuleBaseUNC = $script:PublishModuleBase -Replace '^(.):', '\\localhost\$1$'

        # create names of other modules and scripts that will be referenced in test
        $script:ModuleWithoutRequiredModuleName = "temp-testmodulewithoutrequiredmodule-" + [System.Guid]::NewGuid()
        $script:ScriptName = "temp-testscript" + [System.Guid]::NewGuid()
        $script:ScriptWithExternalDeps = "temp-testscriptwithexternaldeps" + [System.Guid]::NewGuid()
        $script:ScriptWithoutEmptyLinesInMetadata = "temp-scriptwithoutemptylinesinmetadata" + [System.Guid]::NewGuid()
        $script:ScriptWithoutEmptyLinesBetweenCommentBlocks = "temp-scriptwithoutemptylinesbetweencommentblocks" + [System.Guid]::NewGuid()

        # Create temp destination path
        $script:destinationPath = [IO.Path]::GetFullPath((Join-Path -Path $TestDrive -ChildPath "tmpDestinationPath"))
        $null = New-Item $script:destinationPath -ItemType directory -Force

        #Create folder where we shall place all script files to be published for these tests
        $script:tmpScriptsFolderPath = Join-Path -Path $TestDrive -ChildPath "tmpScriptsPath"
        if(!(Test-Path $script:tmpScriptsFolderPath))
        {
            $null = New-Item -Path $script:tmpScriptsFolderPath -ItemType Directory -Force
        }

        # Path to folder, within our test folder, where we store invalid module and script files used for testing
        $script:testFilesFolderPath = Join-Path $script:testDir -ChildPath "testFiles"

        # Path to specifically to that invalid test modules folder
        $script:testModulesFolderPath = Join-Path $script:testFilesFolderPath -ChildPath "testModules"

        # Path to specifically to that invalid test scripts folder
        $script:testScriptsFolderPath = Join-Path $script:testFilesFolderPath -ChildPath "testScripts"
    }
    AfterEach {
        if(!(Test-Path $script:PublishModuleBase))
        {
            Remove-Item -Path $script:PublishModuleBase -Recurse -Force
        }
    }
    AfterAll {
        Get-RevertPSResourceRepositoryFile

        # Note: all repository names provided as test packages for ACR, must have lower cased names, otherwise the Az cmdlets will not be able to properly find and delete it.
        $acrRepositoryNames = @($script:PublishModuleName, $script:ModuleWithoutRequiredModuleName, $script:ScriptName, $script:ScriptWithExternalDeps, $script:ScriptWithoutEmptyLinesInMetadata, $script:ScriptWithoutEmptyLinesBetweenCommentBlocks)
        Set-TestACRRepositories $acrRepositoryNames
    }

    It "Publish module with required module not installed on the local machine using -SkipModuleManifestValidate" {
        CreateTestModule -Path $TestDrive -ModuleName $script:ModuleWithoutRequiredModuleName

        # Skip the module manifest validation test, which fails from the missing manifest required module.
        $testModulePath = Join-Path -Path $TestDrive -ChildPath $script:ModuleWithoutRequiredModuleName
        Publish-PSResource -Path $testModulePath -Repository $ACRRepoName -Confirm:$false -SkipDependenciesCheck -SkipModuleManifestValidate

        $results = Find-PSResource -Name $script:ModuleWithoutRequiredModuleName -Repository $ACRRepoName
        $results | Should -Not -BeNullOrEmpty
        $results[0].Name | Should -Be $script:ModuleWithoutRequiredModuleName
        $results[0].Version | Should -Be "1.0.0"
    }

    It "Publish a module with -Path pointing to a module directory (parent directory has same name)" {
        $version = "1.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $script:PublishModuleBase -Repository $ACRRepoName

        $results = Find-PSResource -Name $script:PublishModuleName -Repository $ACRRepoName
        $results | Should -Not -BeNullOrEmpty
        $results[0].Name | Should -Be $script:PublishModuleName
        $results[0].Version | Should -Be $version
    }

    It "Publish a module with -Path pointing to a module directory (parent directory has different name)" {
        $version = "2.0.0"
        $newModuleRoot = Join-Path -Path $script:PublishModuleBase -ChildPath "NewTestParentDirectory"
        New-Item -Path $newModuleRoot -ItemType Directory -Force
        New-ModuleManifest -Path (Join-Path -Path $newModuleRoot -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $newModuleRoot -Repository $ACRRepoName

        $results = Find-PSResource -Name $script:PublishModuleName -Repository $ACRRepoName
        $results | Should -Not -BeNullOrEmpty
        $results[0].Name | Should -Be $script:PublishModuleName
        $results[0].Version | Should -Be $version
    }

    It "Publish a module with -Path pointing to a .psd1 (parent directory has same name)" {
        $version = "3.0.0"
        $manifestPath = Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1"
        New-ModuleManifest -Path $manifestPath -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $manifestPath -Repository $ACRRepoName

        $results = Find-PSResource -Name  $script:PublishModuleName -Repository $ACRRepoName
        $results | Should -Not -BeNullOrEmpty
        $results[0].Name | Should -Be $script:PublishModuleName
        $results[0].Version | Should -Be $version
    }

    It "Publish a module with -Path pointing to a .psd1 (parent directory has different name)" {
        $version = "4.0.0"
        $newModuleRoot = Join-Path -Path $script:PublishModuleBase -ChildPath "NewTestParentDirectory"
        New-Item -Path $newModuleRoot -ItemType Directory -Force
        $manifestPath = Join-Path -Path $newModuleRoot -ChildPath "$script:PublishModuleName.psd1"
        New-ModuleManifest -Path $manifestPath -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $manifestPath -Repository $ACRRepoName

        $results = Find-PSResource -Name $script:PublishModuleName -Repository $ACRRepoName
        $results | Should -Not -BeNullOrEmpty
        $results[0].Name | Should -Be $script:PublishModuleName
        $results[0].Version | Should -Be $version
    }

    It "Publish a module with -Path pointing to a module directory (parent directory has same name) on a network share" {
        $version = "5.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBaseUNC -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $script:PublishModuleBaseUNC -Repository $ACRRepoName

        $results = Find-PSResource -Name $script:PublishModuleName -Repository $ACRRepoName
        $results | Should -Not -BeNullOrEmpty
        $results[0].Name | Should -Be $script:PublishModuleName
        $results[0].Version | Should -Be $version
    }

    It "Publish a module with -Path pointing to a module directory (parent directory has different name) on a network share" {
        $version = "6.0.0"
        $newModuleRoot = Join-Path -Path $script:PublishModuleBaseUNC -ChildPath "NewTestParentDirectory"
        New-Item -Path $newModuleRoot -ItemType Directory -Force
        New-ModuleManifest -Path (Join-Path -Path $newModuleRoot -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $newModuleRoot -Repository $ACRRepoName

        $results = Find-PSResource -Name $script:PublishModuleName -Repository $ACRRepoName
        $results | Should -Not -BeNullOrEmpty
        $results[0].Name | Should -Be $script:PublishModuleName
        $results[0].Version | Should -Be $version
    }

    It "Publish a module with -Path pointing to a .psd1 (parent directory has same name) on a network share" {
        $version = "7.0.0"
        $manifestPath = Join-Path -Path $script:PublishModuleBaseUNC -ChildPath "$script:PublishModuleName.psd1"
        New-ModuleManifest -Path $manifestPath -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $manifestPath -Repository $ACRRepoName

        $results = Find-PSResource -Name $script:PublishModuleName -Repository $ACRRepoName
        $results | Should -Not -BeNullOrEmpty
        $results[0].Name | Should -Be $script:PublishModuleName
        $results[0].Version | Should -Be $version
    }

    It "Publish a module with -Path pointing to a .psd1 (parent directory has different name) on a network share" {
        $version = "8.0.0"
        $newModuleRoot = Join-Path -Path $script:PublishModuleBaseUNC -ChildPath "NewTestParentDirectory"
        New-Item -Path $newModuleRoot -ItemType Directory -Force
        $manifestPath = Join-Path -Path $newModuleRoot -ChildPath "$script:PublishModuleName.psd1"
        New-ModuleManifest -Path $manifestPath -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $manifestPath -Repository $ACRRepoName

        $results = Find-PSResource -Name $script:PublishModuleName -Repository $ACRRepoName
        $results | Should -Not -BeNullOrEmpty
        $results[0].Name | Should -Be $script:PublishModuleName
        $results[0].Version | Should -Be $version
    }

    It "Publish a module and preserve file structure" {
        $version = "9.0.0"
        $testFile = Join-Path -Path "TestSubDirectory" -ChildPath "TestSubDirFile.ps1"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"
        New-Item -Path (Join-Path -Path $script:PublishModuleBase -ChildPath $testFile) -Force

        Publish-PSResource -Path $script:PublishModuleBase -Repository $ACRRepoName -ErrorAction Stop

        Save-PSResource -Name $script:PublishModuleName -Repository $ACRRepoName -AsNupkg -Path $TestDrive -TrustRepository
        # Must change .nupkg to .zip so that Expand-Archive can work on Windows PowerShell
        $nupkgPath = Join-Path -Path $TestDrive -ChildPath "$script:PublishModuleName.$version.nupkg"
        $zipPath = Join-Path -Path $TestDrive -ChildPath "$script:PublishModuleName.$version.zip"
        Rename-Item -Path $nupkgPath -NewName $zipPath
        $unzippedPath = Join-Path -Path $TestDrive -ChildPath "$script:PublishModuleName"
        New-Item $unzippedPath -Itemtype directory -Force
        Expand-Archive -Path $zipPath -DestinationPath $unzippedPath

        Test-Path -Path (Join-Path -Path $unzippedPath -ChildPath $testFile) | Should -Be $True
    }

    It "Publish a module with -Path -Repository and -DestinationPath" {
        $version = "10.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $script:PublishModuleBase -Repository $ACRRepoName -DestinationPath $script:destinationPath

        $results = Find-PSResource -Name $script:PublishModuleName -Repository $ACRRepoName -Version $version
        $results | Should -Not -BeNullOrEmpty
        $results[0].Name | Should -Be $script:PublishModuleName
        $results[0].Version | Should -Be $version

        $expectedPath = Join-Path -Path $script:destinationPath -ChildPath "$script:PublishModuleName.$version.nupkg"
        Test-Path $expectedPath | Should -Be $true
    }

    It "Publish a module with one dependency" {
        $version = "11.0.0"
        $dependencyName = 'test_dependency_mod'
        $dependencyVersion = '1.0.0'

        # New-ModuleManifest requires that the module be installed before it can be added as a dependency
        Install-PSResource -Name $dependencyName -Version $dependencyVersion -Repository $ACRRepoName -TrustRepository
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module" -RequiredModules @(@{ ModuleName = $dependencyName; ModuleVersion = $dependencyVersion })

        Publish-PSResource -Path $script:PublishModuleBase -Repository $ACRRepoName

        $results = Find-PSResource -Name $script:PublishModuleName -Repository $ACRRepoName -Version $version
        $results | Should -Not -BeNullOrEmpty
        $results[0].Name | Should -Be $script:PublishModuleName
        $results[0].Version | Should -Be $version
        $results[0].Dependencies.Name | Should -Be $dependencyName
        $results[0].Dependencies.VersionRange.MinVersion.ToString() | Should -Be $dependencyVersion
    }

    It "Publish a module with multiple dependencies" {
        $version = "12.0.0"
        $dependency1Name = 'test_dependency_mod2'
        $dependency2Name = 'test_dependency_mod'
        $dependency2Version = '1.0.0'

        # New-ModuleManifest requires that the module be installed before it can be added as a dependency
        Install-PSResource -Name $dependency1Name -Repository $ACRRepoName -TrustRepository -Verbose -Reinstall
        Install-PSResource -Name $dependency2Name -Version $dependency2Version -Repository $ACRRepoName -TrustRepository -Reinstall
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module" -RequiredModules @( $dependency1Name , @{ ModuleName = $dependency2Name; ModuleVersion = $dependency2Version })

        Publish-PSResource -Path $script:PublishModuleBase -Repository $ACRRepoName

        $results = Find-PSResource -Name $script:PublishModuleName -Repository $ACRRepoName -Version $version
        $results | Should -Not -BeNullOrEmpty
        $results[0].Name | Should -Be $script:PublishModuleName
        $results[0].Version | Should -Be $version
        $results[0].Dependencies.Name | Should -Be $dependency1Name, $dependency2Name
        $results[0].Dependencies.VersionRange.MinVersion.OriginalVersion.ToString() | Should -Be $dependency2Version
    }

    It "Publish a module and clean up properly when file in module is readonly" {
        $version = "13.0.0"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        # Create a readonly file that will throw access denied error if deletion is attempted
        $file = Join-Path -Path $script:PublishModuleBase -ChildPath "inaccessiblefile.txt"
        New-Item $file -Itemtype file -Force
        Set-ItemProperty -Path $file -Name IsReadOnly -Value $true

        Publish-PSResource -Path $script:PublishModuleBase -Repository $ACRRepoName

        $results = Find-PSResource -Name $script:PublishModuleName -Repository $ACRRepoName -Version $version
        $results | Should -Not -BeNullOrEmpty
        $results[0].Name | Should -Be $script:PublishModuleName
        $results[0].Version | Should -Be $version
    }

    It "Publish a module when the .psd1 version and the path version are different" {
        $incorrectVersion = "15.2.4"
        $correctVersion = "14.0.0"
        $versionBase = (Join-Path -Path $script:PublishModuleBase  -ChildPath $incorrectVersion)
        New-Item -Path $versionBase -ItemType Directory -Force
        $modManifestPath = (Join-Path -Path $versionBase -ChildPath "$script:PublishModuleName.psd1")
        New-ModuleManifest -Path $modManifestPath -ModuleVersion $correctVersion -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $modManifestPath -Repository $ACRRepoName

        $results = Find-PSResource -Name $script:PublishModuleName -Repository $ACRRepoName -Version $correctVersion
        $results | Should -Not -BeNullOrEmpty
        $results[0].Name | Should -Be $script:PublishModuleName
        $results[0].Version | Should -Be $correctVersion
    }

    It "Publish a script"{
        $scriptVersion = "1.0.0"
        $params = @{
            Version = $scriptVersion
            GUID = [guid]::NewGuid()
            Author = 'Jane'
            CompanyName = 'Microsoft Corporation'
            Copyright = '(c) 2024 Microsoft Corporation. All rights reserved.'
            Description = "Description for the $script:ScriptName script"
            LicenseUri = "https://$script:ScriptName.com/license"
            IconUri = "https://$script:ScriptName.com/icon"
            ProjectUri = "https://$script:ScriptName.com"
            Tags = @('Tag1','Tag2', "Tag-$script:ScriptName-$scriptVersion")
            ReleaseNotes = "$script:ScriptName release notes"
            }

        $scriptPath = (Join-Path -Path $script:tmpScriptsFolderPath -ChildPath "$script:ScriptName.ps1")
        New-PSScriptFileInfo @params -Path $scriptPath

        Publish-PSResource -Path $scriptPath -Repository $ACRRepoName

        $result = Find-PSResource -Name $script:ScriptName -Repository $ACRRepoName
        $result | Should -Not -BeNullOrEmpty
        $result.Name | Should -Be $script:ScriptName
        $result.Version | Should -Be $scriptVersion
    }

    It "Should publish a script without lines in between comment blocks locally" {
        $scriptName = "ScriptWithoutEmptyLinesBetweenCommentBlocks"
        $scriptVersion = "1.0"
        $scriptSrcPath = Join-Path -Path $script:testScriptsFolderPath -ChildPath "$scriptName.ps1"
        $scriptDestPath = Join-Path -Path $script:tmpScriptsFolderPath -ChildPath "$script:ScriptWithoutEmptyLinesBetweenCommentBlocks.ps1"
        Copy-Item -Path $scriptSrcPath -Destination $scriptDestPath

        Publish-PSResource -Path $scriptDestPath -Repository $ACRRepoName

        $result = Find-PSResource -Name $script:ScriptWithoutEmptyLinesBetweenCommentBlocks -Repository $ACRRepoName
        $result | Should -Not -BeNullOrEmpty
        $result.Name | Should -Be $script:ScriptWithoutEmptyLinesBetweenCommentBlocks
        $result.Version | Should -Be $scriptVersion
    }

    It "Should publish a script without lines in help block locally" {
        $scriptName = "ScriptWithoutEmptyLinesInMetadata"
        $scriptVersion = "1.0"
        $scriptSrcPath = Join-Path -Path $script:testScriptsFolderPath -ChildPath "$scriptName.ps1"
        $scriptDestPath = Join-Path -Path $script:tmpScriptsFolderPath -ChildPath "$script:ScriptWithoutEmptyLinesInMetadata.ps1"
        Copy-Item -Path $scriptSrcPath -Destination $scriptDestPath

        Publish-PSResource -Path $scriptDestPath -Repository $ACRRepoName

        $result = Find-PSResource -Name $script:ScriptWithoutEmptyLinesInMetadata -Repository $ACRRepoName
        $result | Should -Not -BeNullOrEmpty
        $result.Name | Should -Be $script:ScriptWithoutEmptyLinesInMetadata
        $result.Version | Should -Be $scriptVersion
    }

    It "Should publish a script with ExternalModuleDependencies that are not published" {
        $scriptVersion = "1.0.0"
        $scriptPath = Join-Path -Path $script:tmpScriptsFolderPath -ChildPath "$script:ScriptWithExternalDeps.ps1"
        New-PSScriptFileInfo -Description 'test' -Version $scriptVersion -RequiredModules @{ModuleName='testModule'} -ExternalModuleDependencies 'testModule' -Path $scriptPath -Force

        Publish-PSResource -Path $scriptPath -Repository $ACRRepoName

        $results = Find-PSResource -Name $script:ScriptWithExternalDeps -Repository $ACRRepoName
        $results | Should -Not -BeNullOrEmpty
        $results[0].Name | Should -Be $script:ScriptWithExternalDeps
        $results[0].Version | Should -Be $scriptVersion
    }

    It "Should write error and not publish script when Author property is missing" {
        $scriptName = "InvalidScriptMissingAuthor.ps1"

        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName
        Publish-PSResource -Path $scriptFilePath -Repository $ACRRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "psScriptMissingAuthor,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"

        Find-PSResource -Name $scriptName -Repository $ACRRepoName -ErrorVariable findErr -ErrorAction SilentlyContinue
        $findErr.Count | Should -BeGreaterThan 0
        $findErr[0].FullyQualifiedErrorId | Should -BeExactly "ResourceNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "Should write error and not publish script when Version property is missing" {
        $scriptName = "InvalidScriptMissingVersion.ps1"

        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName
        Publish-PSResource -Path $scriptFilePath -Repository $ACRRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "psScriptMissingVersion,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"

        Find-PSResource -Name $scriptName -Repository $ACRRepoName -ErrorVariable findErr -ErrorAction SilentlyContinue
        $findErr.Count | Should -BeGreaterThan 0
        $findErr[0].FullyQualifiedErrorId | Should -BeExactly "ResourceNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "Should write error and not publish script when Guid property is missing" {
        $scriptName = "InvalidScriptMissingGuid.ps1"

        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName
        Publish-PSResource -Path $scriptFilePath -Repository $ACRRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "psScriptMissingGuid,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"

        Find-PSResource -Name $scriptName -Repository $ACRRepoName -ErrorVariable findErr -ErrorAction SilentlyContinue
        $findErr.Count | Should -BeGreaterThan 0
        $findErr[0].FullyQualifiedErrorId | Should -BeExactly "ResourceNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "Should write error and not publish script when Description property is missing" {
        $scriptName = "InvalidScriptMissingDescription.ps1"

        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName
        Publish-PSResource -Path $scriptFilePath -Repository $ACRRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "PSScriptInfoMissingDescription,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"

        Find-PSResource -Name $scriptName -Repository $ACRRepoName -ErrorVariable findErr -ErrorAction SilentlyContinue
        $findErr.Count | Should -BeGreaterThan 0
        $findErr[0].FullyQualifiedErrorId | Should -BeExactly "ResourceNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "Should write error and not publish script when Description block altogether is missing" {
        # we expect .ps1 files to have a separate comment block for .DESCRIPTION property, not to be included in the PSScriptInfo commment block
        $scriptName = "InvalidScriptMissingDescriptionCommentBlock.ps1"

        $scriptFilePath = Join-Path $script:testScriptsFolderPath -ChildPath $scriptName
        Publish-PSResource -Path $scriptFilePath -Repository $ACRRepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "missingHelpInfoCommentError,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"

        Find-PSResource -Name $scriptName -Repository $ACRRepoName -ErrorVariable findErr -ErrorAction SilentlyContinue
        $findErr.Count | Should -BeGreaterThan 0
        $findErr[0].FullyQualifiedErrorId | Should -BeExactly "ResourceNotFound,Microsoft.PowerShell.PSResourceGet.Cmdlets.FindPSResource"
    }

    It "Publish a module with that has an invalid version format, should throw" {
        $moduleName = "incorrectmoduleversion"
        $incorrectmoduleversion = Join-Path -Path $script:testModulesFolderPath -ChildPath $moduleName

        { Publish-PSResource -Path $incorrectmoduleversion -Repository $ACRRepoName -ErrorAction Stop } | Should -Throw -ErrorId "InvalidModuleManifest,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"
    }

    It "Publish a module with a dependency that has an invalid version format, should throw" {
        $moduleName = "incorrectdepmoduleversion"
        $incorrectdepmoduleversion = Join-Path -Path $script:testModulesFolderPath -ChildPath $moduleName

        { Publish-PSResource -Path $incorrectdepmoduleversion -Repository $ACRRepoName -ErrorAction Stop } | Should -Throw -ErrorId "InvalidModuleManifest,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"
    }

    It "Publish a module with using an invalid file path (path to .psm1), should throw" {
        $fileName = "$script:PublishModuleName.psm1"
        $psm1Path = Join-Path -Path $script:PublishModuleBase -ChildPath $fileName
        $null = New-Item -Path $psm1Path -ItemType File -Force

        {Publish-PSResource -Path $psm1Path -Repository $ACRRepoName -ErrorAction Stop} | Should -Throw -ErrorId "InvalidPublishPath,Microsoft.PowerShell.PSResourceGet.Cmdlets.PublishPSResource"
    }

    It "Publish a module with -ModulePrefix" {
        $version = "1.0.0"
        $modulePrefix = "unlisted"
        New-ModuleManifest -Path (Join-Path -Path $script:PublishModuleBase -ChildPath "$script:PublishModuleName.psd1") -ModuleVersion $version -Description "$script:PublishModuleName module"

        Publish-PSResource -Path $script:PublishModuleBase -Repository $ACRRepoName -ModulePrefix $modulePrefix

        $results = Find-PSResource -Name "$modulePrefix/$script:PublishModuleName" -Repository $ACRRepoName
        $results | Should -Not -BeNullOrEmpty
        $results[0].Name | Should -Be $script:PublishModuleName
        $results[0].Version | Should -Be $version
    }
}
