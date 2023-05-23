# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"

Describe 'Test Install-PSResource for V3Server scenarios' -tags 'CI' {

    BeforeAll {
        $testModuleName = "dotnet-format"
        $testModuleWithTagsName = "dotnet-ef" # this package has about 300 versions so best not to use it for all the tests.
        $ADORepoName = "DotnetPublicFeed"
        $ADORepoUri = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet8/nuget/v3/index.json"
        Get-NewPSResourceRepositoryFile
        Register-PSResourceRepository -Name $ADORepoName -Uri $ADORepoUri
    }

    AfterEach {
        Uninstall-PSResource $testModuleName, $testModuleWithTagsName -SkipDependencyCheck -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    $testCases = @{Name="*";                          ErrorId="NameContainsWildcard"},
                 @{Name="Test_Module*";               ErrorId="NameContainsWildcard"},
                 @{Name="Test?Module","Test[Module";  ErrorId="ErrorFilteringNamesForUnsupportedWildcards"}

    It "Should not install resource with wildcard in name" -TestCases $testCases {
        param($Name, $ErrorId)
        Install-PSResource -Name $Name -Repository $ADORepoName -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "$ErrorId,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource"
    }

    It "Install specific resource by name" {
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "8.0.427001"
    }

    # It "Install specific script resource by name" {
    #     Install-PSResource -Name $testScriptName -Repository $ADORepoName -TrustRepository
    #     $pkg = Get-InstalledPSResource $testScriptName
    #     $pkg.Name | Should -Be $testScriptName
    #     $pkg.Version | Should -Be "3.5.0"
    # }

    It "Install multiple resources by name" {
        $pkgNames = @($testModuleName, $testModuleWithTagsName)
        Install-PSResource -Name $pkgNames -Repository $ADORepoName -Prerelease -TrustRepository
        $pkg = Get-InstalledPSResource $pkgNames
        $pkg.Name | Should -Be $pkgNames
    }

    It "Should not install resource given nonexistant name" {
        Install-PSResource -Name "NonExistantModule" -Repository $ADORepoName -TrustRepository -ErrorVariable err -ErrorAction SilentlyContinue
        $pkg = Get-InstalledPSResource "NonExistantModule"
        $pkg.Name | Should -BeNullOrEmpty
        $err.Count | Should -BeGreaterThan 0
        $err[0].FullyQualifiedErrorId | Should -BeExactly "InstallPackageFailure,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource" 
    }

    # Do some version testing, but Find-PSResource should be doing thorough testing
    It "Should install resource given name and exact version" {
        Install-PSResource -Name $testModuleName -Version "8.0.426911" -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "8.0.426911"
    }

    It "Should install resource given name and exact version with bracket syntax" {
        Install-PSResource -Name $testModuleName -Version "[8.0.426911]" -Repository $ADORepoName -TrustRepository  
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "8.0.426911"
    }

    It "Should install resource given name and exact range inclusive [8.0.426908, 8.0.427001]" {
        Install-PSResource -Name $testModuleName -Version "[8.0.426908, 8.0.427001]" -Repository $ADORepoName -TrustRepository  
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "8.0.427001"
    }

    It "Should install resource given name and exact range exclusive (8.0.426908, 8.0.427001)" {
        Install-PSResource -Name $testModuleName -Version "(8.0.426908, 8.0.427001)" -Repository $ADORepoName -TrustRepository  
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "8.0.426911"
    }

    # TODO: Update this test and others like it that use try/catch blocks instead of Should -Throw
    It "Should not install resource with incorrectly formatted version such as exclusive version (8.0.427001)" {
        $Version = "(8.0.427001)"
        try {
            Install-PSResource -Name $testModuleName -Version $Version -Repository $ADORepoName -TrustRepository -ErrorAction SilentlyContinue
        }
        catch
        {}
        $Error[0].FullyQualifiedErrorId | Should -be "IncorrectVersionFormat,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource"

        $res = Get-InstalledPSResource $testModuleName
        $res | Should -BeNullOrEmpty
    }

    It "Install resource when given Name, Version '*', should install the latest version" {
        Install-PSResource -Name $testModuleName -Version "*" -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "8.0.427001"
    }

    It "Install resource with latest (including prerelease) version given Prerelease parameter" {
        Install-PSResource -Name $testModuleWithTagsName -Prerelease -Repository $ADORepoName -TrustRepository 
        $pkg = Get-InstalledPSResource $testModuleWithTagsName
        $pkg.Name | Should -Be $testModuleWithTagsName
        $pkg.Version | Should -Be "8.0.0"
        $pkg.Prerelease | Should -Be "preview.5.23272.5"
    }

    It "Install resource via InputObject by piping from Find-PSresource" {
        Find-PSResource -Name $testModuleName -Repository $ADORepoName | Install-PSResource -TrustRepository 
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "8.0.427001"
    }

    It "Install resource under specified in PSModulePath" {
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        ($env:PSModulePath).Contains($pkg.InstalledLocation)
    }

    It "Install resource with companyname, copyright and repository source location and validate properties" {
        Install-PSResource -Name $testModuleName -Version "8.0.427001" -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Version | Should -Be "8.0.427001"

        $pkg.CompanyName | Should -Be "Microsoft"
        $pkg.Copyright | Should -Be ""
        $pkg.RepositorySourceLocation | Should -Be $ADORepoUri
    }

    # Windows only
    It "Install resource under CurrentUser scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository -Scope CurrentUser
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("Documents") | Should -Be $true
    }

    # Windows only
    It "Install resource under AllUsers scope - Windows only" -Skip:(!((Get-IsWindows) -and (Test-IsAdmin))) {
        Install-PSResource -Name "testmodule99" -Repository $ADORepoName -TrustRepository -Scope AllUsers -Verbose
        $pkg = Get-Module "testmodule99" -ListAvailable
        $pkg.Name | Should -Be "testmodule99"
        $pkg.Path.ToString().Contains("Program Files")
    }

    # Windows only
    It "Install resource under no specified scope - Windows only" -Skip:(!(Get-IsWindows)) {
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("Documents") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install resource under CurrentUser scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository -Scope CurrentUser
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("$env:HOME/.local") | Should -Be $true
    }

    # Unix only
    # Expected path should be similar to: '/home/janelane/.local/share/powershell/Modules'
    It "Install resource under no specified scope - Unix only" -Skip:(Get-IsWindows) {
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.InstalledLocation.ToString().Contains("$env:HOME/.local") | Should -Be $true
    }

    It "Should not install resource that is already installed" {
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository -WarningVariable WarningVar -warningaction SilentlyContinue
        $WarningVar | Should -Not -BeNullOrEmpty
    }

    It "Reinstall resource that is already installed with -Reinstall parameter" {
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "8.0.427001"
        Install-PSResource -Name $testModuleName -Repository $ADORepoName -Reinstall -TrustRepository
        $pkg = Get-InstalledPSResource $testModuleName
        $pkg.Name | Should -Be $testModuleName
        $pkg.Version | Should -Be "8.0.427001"
    }

    # It "Restore resource after reinstall fails" {
    #     Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository
    #     $pkg = Get-InstalledPSResource $testModuleName
    #     $pkg.Name | Should -Contain $testModuleName
    #     $pkg.Version | Should -Contain "5.0.0"

    #     $resourcePath = Split-Path -Path $pkg.InstalledLocation -Parent
    #     $resourceFiles = Get-ChildItem -Path $resourcePath -Recurse

    #     # Lock resource file to prevent reinstall from succeeding.
    #     $fs = [System.IO.File]::Open($resourceFiles[0].FullName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read)
    #     try
    #     {
    #         # Reinstall of resource should fail with one of its files locked.
    #         Install-PSResource -Name $testModuleName -Repository $ADORepoName -TrustRepository -Reinstall -ErrorVariable ev -ErrorAction Silent
    #         $ev.FullyQualifiedErrorId | Should -BeExactly 'InstallPackageFailed,Microsoft.PowerShell.PowerShellGet.Cmdlets.InstallPSResource'
    #     }
    #     finally
    #     {
    #         $fs.Close()
    #     }

    #     # Verify that resource module has been restored.
    #     (Get-ChildItem -Path $resourcePath -Recurse).Count | Should -BeExactly $resourceFiles.Count
    # }

    # It "Install resource that requires accept license with -AcceptLicense flag" {
    #     Install-PSResource -Name "testModuleWithlicense" -Repository $TestGalleryName -AcceptLicense
    #     $pkg = Get-InstalledPSResource "testModuleWithlicense"
    #     $pkg.Name | Should -Be "testModuleWithlicense"
    #     $pkg.Version | Should -Be "0.0.3.0"
    # }

    It "Install PSResourceInfo object piped in" {
        Find-PSResource -Name $testModuleName -Version "8.0.426911" -Repository $ADORepoName | Install-PSResource -TrustRepository
        $res = Get-InstalledPSResource -Name $testModuleName
        $res.Name | Should -Be $testModuleName
        $res.Version | Should -Be "8.0.426911"
    }

    It "Install module using -PassThru" {
        $res = Install-PSResource -Name $testModuleName -Repository $ADORepoName -PassThru -TrustRepository
        $res.Name | Should -Be $testModuleName
    }

    It "Install modules using -RequiredResource with hashtable" {
        $rrHash = @{
            "dotnet-format" = @{
               version = "(8.0.426908, 8.0.427001)"
               repository = $ADORepoName
            }

            "dotnet-ef" = @{
               version = "[8.0.0-preview.5.23269.2,8.0.0-preview.5.23272.5]"
               repository = $ADORepoName
               prerelease = "true"
            }

            "dotnet-sql-cache" = @{
                repository = $ADORepoName
                prerelease = "true"
            }
          }

        Install-PSResource -RequiredResource $rrHash -TrustRepository

        $res1 = Get-InstalledPSResource $testModuleName
        $res1.Name | Should -Be $testModuleName
        $res1.Version | Should -Be "8.0.426911"

        $res2 = Get-InstalledPSResource $testModuleWithTagsName
        $res2.Name | Should -Be $testModuleWithTagsName
        $res2.Version | Should -Be "8.0.0"
        $res2.Prerelease | Should -Be "preview.5.23272.5"

        $res3 = Get-InstalledPSResource "dotnet-sql-cache"
        $res3.Name | Should -Be "dotnet-sql-cache"
        $res3.Version | Should -Be "8.0.0"
        $res3.Prerelease | Should -Be "preview.5.23272.6"
    }

    It "Install modules using -RequiredResource with JSON string" {
        $rrJSON = "{
           'dotnet-format': {
             'version': '(8.0.426908, 8.0.427001)',
             'repository': 'DotnetPublicFeed'
           },
           'dotnet-ef': {
             'version': '[8.0.0-preview.5.23269.2,8.0.0-preview.5.23272.5]',
             'repository': 'DotnetPublicFeed',
             'prerelease': 'true'
           },
           'dotnet-sql-cache': {
             'repository': 'DotnetPublicFeed',
             'prerelease': 'true'
           }
         }"

        Install-PSResource -RequiredResource $rrJSON -TrustRepository

        $res1 = Get-InstalledPSResource $testModuleName
        $res1.Name | Should -Be $testModuleName
        $res1.Version | Should -Be "8.0.426911"

        $res2 = Get-InstalledPSResource $testModuleWithTagsName
        $res2.Name | Should -Be $testModuleWithTagsName
        $res2.Version | Should -Be "8.0.0"
        $res2.Prerelease | Should -Be "preview.5.23272.5"

        $res3 = Get-InstalledPSResource "dotnet-sql-cache"
        $res3.Name | Should -Be "dotnet-sql-cache"
        $res3.Version | Should -Be "8.0.0"
        $res3.Prerelease | Should -Be "preview.5.23272.6"
    }

# Describe 'Test Install-PSResource for V3Server scenarios' -tags 'ManualValidationOnly' {

#     BeforeAll {
#         $testModuleName = "TestModule"
#         $testModuleName2 = "testModuleWithlicense"
#         Get-NewPSResourceRepositoryFile
#         Register-LocalRepos
#     }

#     AfterEach {
#         Uninstall-PSResource $testModuleName, $testModuleName2 -SkipDependencyCheck -ErrorAction SilentlyContinue
#     }

#     AfterAll {
#         Get-RevertPSResourceRepositoryFile
#     }

#     # Unix only manual test
#     # Expected path should be similar to: '/usr/local/share/powershell/Modules'
#     It "Install resource under AllUsers scope - Unix only" -Skip:(Get-IsWindows) {
#         Install-PSResource -Name $testModuleName -Repository $TestGalleryName -Scope AllUsers
#         $pkg = Get-Module $testModuleName -ListAvailable
#         $pkg.Name | Should -Be $testModuleName 
#         $pkg.Path.Contains("/usr/") | Should -Be $true
#     }

#     # This needs to be manually tested due to prompt
#     It "Install resource that requires accept license without -AcceptLicense flag" {
#         Install-PSResource -Name $testModuleName2  -Repository $TestGalleryName
#         $pkg = Get-InstalledPSResource $testModuleName2 
#         $pkg.Name | Should -Be $testModuleName2 
#         $pkg.Version | Should -Be "0.0.1.0"
#     }

#     # This needs to be manually tested due to prompt
#     It "Install resource should prompt 'trust repository' if repository is not trusted" {
#         Set-PSResourceRepository PoshTestGallery -Trusted:$false

#         Install-PSResource -Name $testModuleName -Repository $TestGalleryName -confirm:$false
    
#         $pkg = Get-Module $testModuleName -ListAvailable
#         $pkg.Name | Should -Be $testModuleName

#         Set-PSResourceRepository PoshTestGallery -Trusted
#     }
}