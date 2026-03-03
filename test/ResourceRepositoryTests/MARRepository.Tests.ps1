# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$modPath = "$psscriptroot/../PSGetTestUtils.psm1"
Write-Verbose -Verbose -Message "PSGetTestUtils path: $modPath"
Import-Module $modPath -Force -Verbose

Describe "Test MAR Repository Registration" -tags 'CI' {
    BeforeEach {
        $MARName = Get-MARName
        $MARUri = Get-MARLocation
        $PSGalleryName = Get-PSGalleryName
        $PSGalleryUri = Get-PSGalleryLocation
        Get-NewPSResourceRepositoryFile
    }
    AfterEach {
        Get-RevertPSResourceRepositoryFile
    }

    Context "MAR is auto-registered with expected values" {
        It "MAR repository should be present with expected default values" {
            $res = Get-PSResourceRepository -Name $MARName
            $res | Should -Not -BeNullOrEmpty
            $res.Name | Should -Be $MARName
            $res.Uri | Should -Be "$MARUri/"
            $res.Trusted | Should -Be True
            $res.Priority | Should -Be 40
            $res.ApiVersion | Should -Be 'ContainerRegistry'
        }

        It "MAR repository should have lower priority number (higher priority) than PSGallery" {
            $mar = Get-PSResourceRepository -Name $MARName
            $psGallery = Get-PSResourceRepository -Name $PSGalleryName
            $mar.Priority | Should -BeLessThan $psGallery.Priority
        }
    }

    Context "MAR name protection" {
        It "should not allow registering MAR with -Name parameter" {
            { Register-PSResourceRepository -Name "MAR" -Uri "https://mcr.microsoft.com" -ErrorAction Stop } | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PSResourceGet.Cmdlets.RegisterPSResourceRepository"
        }

        It "should not allow registering MAR (case insensitive) with -Name parameter" {
            { Register-PSResourceRepository -Name "mar" -Uri "https://mcr.microsoft.com" -ErrorAction Stop } | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PSResourceGet.Cmdlets.RegisterPSResourceRepository"
        }

        It "should not allow registering MAR with -Name parameter in hashtable" {
            Unregister-PSResourceRepository -Name $MARName
            $hashtable = @{Name = "MAR"; Uri = "https://mcr.microsoft.com"}
            Register-PSResourceRepository -Repository $hashtable -ErrorVariable err -ErrorAction SilentlyContinue
            $err.Count | Should -BeGreaterThan 0
            $err[0].FullyQualifiedErrorId | Should -BeExactly "MARProvidedAsNameRepoPSet,Microsoft.PowerShell.PSResourceGet.Cmdlets.RegisterPSResourceRepository"
        }

        It "should not allow setting Uri for MAR repository" {
            { Set-PSResourceRepository -Name $MARName -Uri "https://example.com" -ErrorAction Stop } | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PSResourceGet.Cmdlets.SetPSResourceRepository"
        }

        It "should not allow setting CredentialInfo for MAR repository" {
            $randomSecret = [System.IO.Path]::GetRandomFileName()
            $credentialInfo = New-Object Microsoft.PowerShell.PSResourceGet.UtilClasses.PSCredentialInfo ("testvault", $randomSecret)
            { Set-PSResourceRepository -Name $MARName -CredentialInfo $credentialInfo -ErrorAction Stop } | Should -Throw -ErrorId "ErrorInNameParameterSet,Microsoft.PowerShell.PSResourceGet.Cmdlets.SetPSResourceRepository"
        }

        It "should allow setting Trusted for MAR repository" {
            Set-PSResourceRepository -Name $MARName -Trusted:$false
            $res = Get-PSResourceRepository -Name $MARName
            $res.Trusted | Should -Be False
        }

        It "should allow setting Priority for MAR repository" {
            Set-PSResourceRepository -Name $MARName -Priority 10
            $res = Get-PSResourceRepository -Name $MARName
            $res.Priority | Should -Be 10
        }
    }

    Context "Reset repository store includes MAR" {
        It "Reset-PSResourceRepository should register MAR alongside PSGallery" {
            Reset-PSResourceRepository -Force
            $res = Get-PSResourceRepository -Name $MARName
            $res | Should -Not -BeNullOrEmpty
            $res.Name | Should -Be $MARName
            $res.Uri | Should -Be "$MARUri/"
            $res.Trusted | Should -Be True
            $res.Priority | Should -Be 40
            $res.ApiVersion | Should -Be 'ContainerRegistry'

            $psGallery = Get-PSResourceRepository -Name $PSGalleryName
            $psGallery | Should -Not -BeNullOrEmpty
        }

        It "Reset-PSResourceRepository should restore MAR after unregistration" {
            Unregister-PSResourceRepository -Name $MARName
            $res = Get-PSResourceRepository -Name $MARName -ErrorAction SilentlyContinue
            $res | Should -BeNullOrEmpty

            Reset-PSResourceRepository -Force
            $res = Get-PSResourceRepository -Name $MARName
            $res | Should -Not -BeNullOrEmpty
            $res.Name | Should -Be $MARName
            $res.Uri | Should -Be "$MARUri/"
            $res.Trusted | Should -Be True
            $res.Priority | Should -Be 40
        }

        It "Reset-PSResourceRepository should restore both PSGallery and MAR" {
            Unregister-PSResourceRepository -Name $MARName
            Unregister-PSResourceRepository -Name $PSGalleryName
            Reset-PSResourceRepository -Force

            $mar = Get-PSResourceRepository -Name $MARName
            $mar | Should -Not -BeNullOrEmpty
            $mar.Priority | Should -Be 40
            $mar.Trusted | Should -Be True
            $mar.ApiVersion | Should -Be 'ContainerRegistry'

            $psGallery = Get-PSResourceRepository -Name $PSGalleryName
            $psGallery | Should -Not -BeNullOrEmpty
            $psGallery.Priority | Should -Be 50
        }
    }

    Context "MAR first due to higher priority" {
        It "Find-PSResource Az.Accounts module from MAR" {
            $res = Find-PSResource -Name "Az.Accounts"
            $res | Should -Not -BeNullOrEmpty
            $res.Name | Should -Be "Az.Accounts"
            $res.Repository | Should -Be $MARName
        }

        It 'Find-PSResource fallback to PSGallery if module not in MAR' {
            $res = Find-PSResource -Name "Pscx"
            $res | Should -Not -BeNullOrEmpty
            $res.Name | Should -Be "Pscx"
            $res.Repository | Should -Be $PSGalleryName
        }
    }
}
