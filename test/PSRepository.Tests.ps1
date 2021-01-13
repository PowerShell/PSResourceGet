# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

BeforeAll {
    Import-Module $PSScriptRoot/Shared.psm1

    $PSGalleryName = 'PSGallery'
    $PSGalleryLocation = 'https://www.powershellgallery.com/api/v2'
    
    $TestRepoName = 'TestRepoName'
    $TestRepoURL = 'https://www.poshtestgallery.com/api/v2'
    
    $TestRepoName2 = "NuGet"
    $TestRepoURL2 = 'https://api.nuget.org/v3/index.json'
    
    $TestRepoLocalName = 'TestLocalRepo'
    $tmpdir = Join-Path -Path $TestDrive -ChildPath $TestRepoLocalName
    if (-not (Test-Path -LiteralPath $tmpdir)) {
        New-Item -Path $tmpdir -ItemType Directory > $null
    }
    $TestRepoLocalURL = $tmpdir

    $TestRepoLocalName2 = "TestLocalRepoName2"
    $tmpdir2 = Join-Path -Path $TestDrive -ChildPath $TestRepoLocalName2
    if (-not (Test-Path -LiteralPath $tmpdir2)) {
        New-Item -Path $tmpdir2 -ItemType Directory > $null
    }

    $TestRepoLocalURL2 = $tmpdir2
}
AfterAll {
    Remove-Item $tmpdir,$tmpDir2 -Force
}

Describe 'Register-PSResourceRepository' -tags 'BVT' { 
    ### Registering the PowerShell Gallery
    Context 'PSGallery' {
        BeforeAll {
            $PSGalleryName = Get-PSGalleryName
            $ExistingPSGallery = Get-PSResourceRepository -Name $PSGalleryName -ErrorAction SilentlyContinue
        }
        AfterAll {
            #BUG: "Already exists" exception doesn't respect EA silentlycontinue
            try {
                if ($ExistingPSGallery) {Register-PSResourceRepository -PSGallery -ErrorAction Stop}
            } catch {
                if ([String]$PSItem -ne "The PSResource Repository 'PSGallery' already exists.") {throw}
            }
        }
        BeforeEach {
            Unregister-PSResourceRepository $PSGalleryName -ErrorAction SilentlyContinue
        }
        It 'Should register the default PSGallery' {
            Register-PSResourceRepository -PSGallery
    
            $repo = Get-PSResourceRepository $PSGalleryName
            $repo | Should -Not -BeNullOrEmpty
            $repo.URL | Should -Be $PSGalleryLocation
            $repo.Trusted | Should -Be false
            $repo.Priority | Should -Be 50
        }
        
        It 'Should register PSGallery with installation policy trusted' {
            Register-PSResourceRepository -PSGallery -Trusted
    
            $repo = Get-PSResourceRepository $PSGalleryName
            $repo.Name | Should -Be $PSGalleryName
            $repo.Trusted | Should -Be true
        }
        
        It 'Should fail to re-register PSGallery' {
            Register-PSResourceRepository -PSGallery
            {Register-PSResourceRepository -PSGallery -ErrorAction Stop} | 
                Should -Throw "The PSResource Repository 'PSGallery' already exists."
        }

        It 'Should fail to register PSGallery when manually providing URL' { Set-ItResult -Pending -Because 'Currently does not throw exception as expected but may be WIP'
            {Register-PSResourceRepository $PSGalleryName -URL $PSGalleryLocation -ErrorAction Stop} | 
                Should -Throw "Use 'Register-PSResourceRepository -Default' to register the PSGallery repository."
        }
    }

    ### Registering an online URL
    Context 'Online URL' {
        BeforeEach {
            #BUG: Unregister-PSResourceRepository does not respect erroraction silentlycontinue
            try {
                Unregister-PSResourceRepository $TestRepoName -ErrorAction Stop
            } catch {
                if ([String]$PSItem -notmatch 'Unable to successfully unregister repository: Unable to find repository') {throw}
            }
            
        }
        AfterEach {
            try {
                Unregister-PSResourceRepository $TestRepoName -ErrorAction Stop
            } catch {
                if ([String]$PSItem -notmatch 'Unable to successfully unregister repository: Unable to find repository') {throw}
            }
        }

        It 'Should register the test repository with online -URL' {
            Register-PSResourceRepository $TestRepoName -URL $TestRepoURL
    
            $repo = Get-PSResourceRepository $TestRepoName
            $repo.Name | Should -Be $TestRepoName
            $repo.URL | Should -Be $TestRepoURL
            $repo.Trusted | Should -Be false    
        }
    
        It 'Should register the test repository when -URL is a website and installation policy is trusted' {
            Register-PSResourceRepository $TestRepoName -URL $TestRepoURL -Trusted
    
            $repo = Get-PSResourceRepository $TestRepoName
            $repo.Name | Should -Be $TestRepoName
            $repo.URL | Should -Be $TestRepoURL
            $repo.Trusted | Should -Be true
        }
    
        It 'Should register the test repository when -URL is a website and priority is set' {
            Register-PSResourceRepository $TestRepoName -URL $TestRepoURL -Priority 2
    
            $repo = Get-PSResourceRepository $TestRepoName
            $repo.Name | Should -Be $TestRepoName
            $repo.URL | Should -Be $TestRepoURL
            $repo.Trusted | Should -Be $true
            $repo.Priority | Should -Be 2
        }
    
        It 'Should fail to reregister the repository when the -Name is already registered' {
            Register-PSResourceRepository $TestRepoName -URL $TestRepoURL
            {Register-PSResourceRepository $TestRepoName -URL $TestRepoURL2} | 
                Should -Throw "The PSResource Repository '$($TestRepoName)' already exists."
        }

        It 'Should fail to reregister the repository when the -URL is already registered' {
            Register-PSResourceRepository $TestRepoName -URL $TestRepoURL
            {Register-PSResourceRepository $TestRepoName2 -URL $TestRepoURL -ErrorAction Stop} |
                Should -Throw "The PSResource Repository '$($TestRepoName2)' already exists."
        }
    }

	### Registering a fileshare URL
    Context 'Fileshare URL' {
        BeforeEach {
            #BUG: Unregister-PSResourceRepository does not respect erroraction silentlycontinue
            try {
                Unregister-PSResourceRepository $TestRepoLocalName -ErrorAction Stop
            } catch {
                if ([String]$PSItem -notmatch 'Unable to successfully unregister repository: Unable to find repository') {throw}
            }
            
        }
        AfterEach {
            try {
                Unregister-PSResourceRepository $TestRepoLocalName -ErrorAction Stop
            } catch {
                if ([String]$PSItem -notmatch 'Unable to successfully unregister repository: Unable to find repository') {throw}
            }
        }

        It 'Should register the test repository when -URL is a fileshare' {
            Register-PSResourceRepository $TestRepoLocalName -URL $TestRepoLocalURL
    
            $repo = Get-PSResourceRepository $TestRepoLocalName
            $repo.Name | Should -Be $TestRepoLocalName
    
            $repoModifiedURL = $repo.URL.replace("/","\")
            $repoModifiedURL | Should -Be ("file:\\\" + $TestRepoLocalURL)
            $repo.Trusted | Should -Be false
        }

        It 'Should register the test repository when -URL is a fileshare and installation policy is trusted and priority is set' {
            Register-PSResourceRepository $TestRepoLocalName -URL $TestRepoLocalURL -Trusted -Priority 2

            $repo = Get-PSResourceRepository $TestRepoLocalName
            $repo.Name | Should -Be $TestRepoLocalName

            $repoModifiedURL = $repo.URL.replace("/","\")
            $repoModifiedURL | Should -Be ("file:\\\" + $TestRepoLocalURL)
            $repo.Trusted | Should -Be true
            $repo.Priority | Should -Be 2
        }
    
        It 'Should fail to reregister the repository when the -Name is already registered' {
            Register-PSResourceRepository $TestRepoLocalName -URL $TestRepoLocalURL
            {Register-PSResourceRepository $TestRepoLocalName -URL $TestRepoLocalURL2} |
                Should -Throw "The PSResource Repository '$($TestRepoLocalName)' already exists."
        }
    
        It 'Should fail to reregister the repository when the fileshare -URL is already registered' { Set-ItResult -Pending -Because "Throws correctly but error message isn't as detailed as the test, may be WIP"
            Register-PSResourceRepository $TestRepoLocalName -URL $TestRepoLocalURL
            {Register-PSResourceRepository 'NewTestName' -URL $TestRepoLocalURL2} |
                Should -Throw "The repository could not be registered because there exists a registered repository with Name '$($TestRepoName)' and URL '$($TestRepoURL)'. To register another repository with Name '$($TestRepoName2)', please unregister the existing repository using the Unregister-PSResourceRepository cmdlet."
        }
    
        It 'Register PSResourceRepository File system location with special chars' {
            $tmpdir = Join-Path -Path $TestDrive -ChildPath 'ps repo testing [$!@^&test(;)]'
            if (-not (Test-Path -LiteralPath $tmpdir)) {
                New-Item -Path $tmpdir -ItemType Directory > $null
            }
            try {
                Register-PSResourceRepository -Name $TestRepoLocalName -URL $tmpdir

                $repo = Get-PSResourceRepository -Name $TestRepoLocalName
                $repo.Name | Should -Be $TestRepoLocalName
    
                $repoModifiedURL = $repo.URL.replace("/","\")
                $repoModifiedURL | Should -Be ("file:\\\" + $tmpdir)
            } finally {
                Remove-Item $tmpdir -Force
            }
        }
    }

    Context "Splatting" {
        BeforeEach {
            #BUG: Unregister-PSResourceRepository does not respect erroraction silentlycontinue
            try {
                Unregister-PSResourceRepository $TestRepoName -ErrorAction Stop
            } catch {
                if ([String]$PSItem -notmatch 'Unable to successfully unregister repository: Unable to find repository') {throw}
            }
            
        }
        AfterEach {
            try {
                Unregister-PSResourceRepository $TestRepoName -ErrorAction Stop
            } catch {
                if ([String]$PSItem -notmatch 'Unable to successfully unregister repository: Unable to find repository') {throw}
            }
        }

        It 'Should register a repository' {            
            $paramRegisterPSResourceRepository = @{
                Name     = $TestRepoName
                URL      = $TestRepoURL
                Trusted  = $False
                Priority = 1
            }
            Register-PSResourceRepository @paramRegisterPSResourceRepository 

            $repo = Get-PSResourceRepository -Name $TestRepoName
            $repo.URL | Should -Be $TestRepoURL
            $repo.Trusted | Should -Be $True
            $repo.Priority | Should -Be 1
        }
    
        It 'Should register multiple repositories' {

            #FIXME: Blackhole errors because ErrorAction isn't respected by Unregister-PSResourceRepository correctly
            try {
                Unregister-PSResourceRepository -Name $TestRepoName
            } catch {}
            try {
                Unregister-PSResourceRepository -Name $TestRepoLocalName
            } catch {}
            try {
                Unregister-PSResourceRepository -Name $PSGalleryName
            } catch {}
            
            $errorActionPreference = $lastErrorActionPreference

            Register-PSResourceRepository -Repositories @(
                @{ Name = $TestRepoName; URL = $TestRepoURL; Priority = 15 }
                @{ Name = $TestRepoLocalName; URL = $TestRepoLocalURL }
                @{ PSGallery = $true; Trusted = $true }
            )
    
            $repo1 = Get-PSResourceRepository $TestRepoName
            $repo1.URL | Should -Be $TestRepoURL
            $repo1.Priority | Should -Be 15

            $repo2 = Get-PSResourceRepository $TestRepoLocalName
            $repo2ModifiedURL = $repo2.URL.replace("/","\")
            $repo2ModifiedURL | Should -Be ("file:\\\" + $TestRepoLocalURL)
            $repo2.Priority | Should -Be 50

            $repo3 = Get-PSResourceRepository $PSGalleryName
            $repo3.URL | Should -Be $PSGalleryLocation
            $repo3.Priority | Should -Be 50
        }
    }
}

Describe 'Unregister-PSResourceRepository' -tags 'BVT' {
    BeforeEach {
        $PSGalleryName,$TestRepoName,$TestRepoName2,$TestRepoLocalName | Foreach {
            try {
                Unregister-PSResourceRepository -Name $PSItem
            } catch {}
        }
		Register-PSResourceRepository $TestRepoName -URL $TestRepoURL
        Register-PSResourceRepository $TestRepoName2 -URL $TestRepoURL2 -Priority 50
		Register-PSResourceRepository $TestRepoLocalName -URL $TestRepoLocalURL -Priority 0
		Register-PSResourceRepository -PSGallery
    }
    ### Unregistering the PowerShell Gallery
    It 'Should unregister the default PSGallery' {
        Unregister-PSResourceRepository $PSGalleryName

        $repo = Get-PSResourceRepository $PSGalleryName
        $repo | Should -BeNullOrEmpty
    }

	### Unregistering any repository
    It 'Should unregister a given repository' {
        Unregister-PSResourceRepository $TestRepoName

        $repo = Get-PSResourceRepository $TestRepoName
        $repo | Should -BeNullOrEmpty
    }
	
    It 'Should unregister multiple repositories' {
        Unregister-PSResourceRepository $TestRepoName, $TestRepoName2, $TestRepoLocalName

        $repos = Get-PSResourceRepository $TestRepoName, $TestRepoName2, $TestRepoLocalName
        $repos | Should -BeNullOrEmpty
    }
}

Describe 'Set-PSResourceRepository' -tags 'BVT', 'InnerLoop' {
    Context 'PSGallery' {
        BeforeEach {
            try {
                Unregister-PSResourceRepository $PSGalleryName
            } catch {}
        }

        It 'Should set PSGallery to a trusted installation policy and a non-zero priority' {
            Register-PSResourceRepository -PSGallery -Trusted:$False -Priority 0
            Set-PSResourceRepository $PSGalleryName -Trusted -Priority 2
    
            $repo = Get-PSResourceRepository $PSGalleryName
            $repo.URL | Should -Be $PSGalleryLocation
            $repo.Trusted | Should -Be $true
            $repo.Priority | Should -Be 2
        }
    
        It 'Should set PSGallery to an untrusted installation policy' {
            Register-PSResourceRepository -PSGallery -Trusted
            Set-PSResourceRepository -Name $PSGalleryName -Trusted:$False
    
            $repo = Get-PSResourceRepository $PSGalleryName
            $repo.Trusted | Should -Be false
            $repo.Priority | Should -Be 50
        }

        It 'Should fail to set PSGallery to a different URL' { Set-ItResult -Pending -Because 'Throws but error message does not match, may be WIP'
            {Set-PSResourceRepository $PSGalleryName -URL $TestRepoURL} | 
                Should -Throw "The PSGallery repository has pre-defined locations. Setting the 'URL' parameter is not allowed, try again after removing the 'URL' parameter."
        }
    }

    Context 'Splatting' -tags 'BVT', 'InnerLoop' {
        BeforeEach {
            #Establish the repositories to run set commands on
            try {
                Unregister-PSResourceRepository -Name $TestRepoName
            } catch {}
            try {
                Unregister-PSResourceRepository -Name $TestRepoLocalName
            } catch {}
            try {
                Unregister-PSResourceRepository -Name $PSGalleryName
            } catch {}
    
            Register-PSResourceRepository $TestRepoName -URL $TestRepoURL
            Register-PSResourceRepository $TestRepoLocalName -URL $TestRepoLocalURL -Priority 0
            Register-PSResourceRepository -PSGallery
        }
        It 'Should fail to set on nonexistent repository' {
            Set-ItResult -Pending -Because WIP
        }
        It 'Should fail when trying to modify PSGallery Url' {
            Set-ItResult -Pending -Because WIP
        }
        It 'Should set repository with given hashtable parameters' {
    
            $paramSetPSResourceRepository = @{
                Name     = $TestRepoName
                URL      = $TestRepoURL2
                Trusted  = $False
                Priority = 1
            }
            Set-PSResourceRepository @paramSetPSResourceRepository 
    
            $repo = Get-PSResourceRepository -Name $TestRepoName
            $repo.URL | Should -Be $TestRepoURL2
            $repo.Trusted | Should -Be false
            $repo.Priority | Should -Be 1
        }
        It 'Should set multiple repositories' {
            $repositories = @(
                @{ Name = $TestRepoName; URL = $TestRepoURL2; Priority = 9 },
                @{ Name = $TestRepoLocalName; URL = $TestRepoLocalURL2; Trusted = $True }
                @{ Name = $PSGalleryName; URL = $PSGalleryLocation; Trusted = $True; Priority = 55 }
            )
    
            Set-PSResourceRepository -Repositories $repositories
    
            $repo1 = Get-PSResourceRepository $TestRepoName
            $repo1.URL | Should -Be $TestRepoURL2
            $repo1.Trusted | Should -Be false
            $repo1.Priority | Should -Be 9
    
            $repo2 = Get-PSResourceRepository $TestRepoLocalName
            $repo2ModifiedURL = $repo2.URL.replace("/","\")
            $repo2ModifiedURL | Should -Be ("file:\\\" + $TestRepoLocalURL2)
            $repo2.Trusted | Should -Be true
            $repo2.Priority | Should -Be 50
    
            $repo3 = Get-PSResourceRepository $PSGalleryName
            $repo3.Trusted | Should -Be true
            $repo3.Priority | Should -Be 55
        }
    }
}

Describe 'Get-PSResourceRepository' -tags 'BVT', 'InnerLoop' {

    BeforeAll {
        $PSGalleryName,$TestRepoName,$TestRepoName2,$TestRepoLocalName2 | Foreach-Object {
            try {
                Unregister-PSResourceRepository -Name $PSItem
            } catch {}
        }
		Register-PSResourceRepository -PSGallery -Trusted -ErrorAction SilentlyContinue
        Register-PSResourceRepository -Name $TestRepoName -URL $TestRepoURL -Trusted -Priority 2
        Register-PSResourceRepository -Name $TestRepoName2 -URL $TestRepoURL2 -Priority 15 -ErrorAction SilentlyContinue
		Register-PSResourceRepository -Name $TestRepoLocalName2 -URL $TestRepoLocalURL2 -ErrorAction SilentlyContinue
    }

    It 'Should get PSGallery repository' {
        $repo = Get-PSResourceRepository $PSGalleryName
        $repo.URL | Should -Be $PSGalleryLocation
        $repo.Trusted | Should -Be $true
        $repo.Priority | Should -Be 50
    }

    It 'Should get test repository' {
        $repo = Get-PSResourceRepository $TestRepoName
        $repo.URL | Should -Be $TestRepoURL
        $repo.Trusted | Should -Be $true
        $repo.Priority | Should -Be 2
    }
    It 'Should get multiple repositories' {
        $repos = Get-PSResourceRepository $PSGalleryName, $TestRepoName2, $TestRepoLocalName2

        $repos.Count | Should -Be 3

		$repos.Name | Should -Contain $PSGalleryName
        $repos.Name | Should -Contain $TestRepoName2
        $repos.Name | Should -Contain $TestRepoLocalName2

        $repos.URL | Should -Contain $PSGalleryLocation
        $repos.URL | Should -Contain $TestRepoURL2

		$repoModifiedURL = "file:///" + ($TestRepoLocalURL2.replace("\", "/"));
		$repos.URL | Should -Contain $repoModifiedURL

		$repos.Priority | Should -Contain 15
		$repos.Priority | Should -Contain 50
    }

    It 'Should get all repositories' { Set-ItResult -Pending -Because 'This is a bad test because you have to wipe the test system of all repositories, its a bad idea'

		$repos = Get-PSResourceRepository

        $repos.Count | Should -Be 5		

        $repos.Name | should contain $PSGalleryName
        $repos.Name | should contain $TestRepoName
        $repos.Name | should contain $TestRepoName2
        $repos.Name | should contain $TestRepoLocalName
        $repos.Name | should contain $TestRepoLocalName2

        $repos.URL | should contain $PSGalleryLocation
        $repos.URL | should contain $TestRepoURL
        $repos.URL | should contain $TestRepoURL2

		$repoModifiedURL = "file:///" + ($TestRepoLocalURL.replace("\", "/"));
		$repos.URL | should contain $repoModifiedURL

	    $repoModifiedURL2 = "file:///" + ($TestRepoLocalURL2.replace("\", "/"));
	    $repos.URL | should contain $repoModifiedURL2

        $repos.Priority | should contain 2
        $repos.Priority | should contain 50
        $repos.Priority | should contain 15

    }
}

