# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ProgressPreference = "SilentlyContinue"
$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Import-Module $modPath -Force -Verbose

$psmodulePaths = $env:PSModulePath -split ';'
Write-Verbose -Verbose "Current module search paths: $psmodulePaths"

Describe 'Test Install-PSResource for the Az module' -tags 'CI' {

    BeforeAll {
        $PSGalleryName = Get-PSGalleryName
        $azName = "Az"
        $azDepWildCard = "Az.*"

        Get-NewPSResourceRepositoryFile
    }

    AfterEach {
        Uninstall-PSResource $azName, $azDepWildCard -ErrorAction SilentlyContinue
    }

    AfterAll {
        Get-RevertPSResourceRepositoryFile
    }

    It "Install Az module and all dependencies" {
        Install-PSResource -Name $azName -Repository $PSGalleryName -TrustRepository -Reinstall

        $pkg = Get-InstalledPSResource $azName
        $pkg | Should -Not -BeNullOrEmpty
        $pkg.Name | Should -Be $azName

        $dep = Get-InstalledPSResource $azDepWildCard
        $dep | Should -Not -BeNullOrEmpty
        $dep.Count | Should -BeGreaterThan 70
    }
}
