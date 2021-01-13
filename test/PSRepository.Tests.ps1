# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

#Testing Environment Setup
BeforeAll {
    Import-Module $PSScriptRoot/Shared.psm1 -Verbose
}


BeforeAll {
    $PSGalleryName = 'PSGallery'
    $PSGalleryLocation = 'https://www.powershellgallery.com/api/v2'
    
    $TestRepoName = 'TestRepoName'
    $TestRepoURL = 'https://www.poshtestgallery.com/api/v2'
    
    $TestRepoName2 = "NuGet"
    $TestRepoURL2 = 'https://api.nuget.org/v3/index.json'
    
    $TestRepoLocalName = 'TestLocalRepo'
    $tmpdir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $TestRepoLocalName
    
    if (-not (Test-Path -LiteralPath $tmpdir)) {
        New-Item -Path $tmpdir -ItemType Directory > $null
    }
    $TestRepoLocalURL = $tmpdir
    
    $TestRepoLocalName2 = "TestLocalRepoName2"
    $tmpdir2 = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $TestRepoLocalName2
    if (-not (Test-Path -LiteralPath $tmpdir2)) {
        New-Item -Path $tmpdir2 -ItemType Directory > $null
    }
    
    $TestRepoLocalURL2 = $tmpdir2
    
    # remember to delete these files
    #    Remove-Item -LiteralPath $tmpdir -Force -Recurse
    #}
    
    $ErrorActionPreference = "SilentlyContinue"
}


#####################################
### Register-PSResourceRepository ###
#####################################

Describe 'Test Register-PSResourceRepository' -tags 'BVT' { 
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
    
        It 'Should fail to reregister the repository when the fileshare -URL is already registered' {
            Register-PSResourceRepository $TestRepoLocalName -URL $TestRepoLocalURL
            Register-PSResourceRepository 'NewTestName' -URL $TestRepoLocalURL2 -ErrorVariable ev -ErrorAction SilentlyContinue
    
            $ev[0].FullyQualifiedErrorId | Should -Be "The repository could not be registered because there exists a registered repository with Name '$($TestRepoName)' and URL '$($TestRepoURL)'. To register another repository with Name '$($TestRepoName2)', please unregister the existing repository using the Unregister-PSResourceRepository cmdlet."
        }
    
        It 'Register PSResourceRepository File system location with special chars' { Set-ItResult -Pending
            $tmpdir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath 'ps repo testing [$!@^&test(;)]'
            if (-not (Test-Path -LiteralPath $tmpdir)) {
                New-Item -Path $tmpdir -ItemType Directory > $null
            }
            try {
                Register-PSResourceRepository -Name 'Test Repository' -URL $tmpdir
                try {
                    Write-Host $tmpdir

                    $repo = Get-PSResourceRepository -Name 'Test Repository'
                    $repo.Name | Should -Be 'Test Repository'

                    $repoModifiedURL = $repo.URL.replace("/","\")
                    $repoModifiedURL | Should -Be ("file:\\\" + $tmpdir)
                    #$repo.URL | Should -Be $tmpdir
                }
                finally {
                    Unregister-PSResourceRepository -Name 'Test Repository' -ErrorAction SilentlyContinue
                }
            }
            finally {
                Remove-Item -LiteralPath $tmpdir -Force -Recurse
            }
        }
    }

}



Describe 'Registering Repositories with Hashtable Parameters' -tags 'BVT', 'InnerLoop' {

    It 'Should register a repository with parameters as a hashtable' { Set-ItResult -Pending
        Unregister-PSResourceRepository -Name $TestRepoName -ErrorAction SilentlyContinue
		
		$paramRegisterPSResourceRepository = @{
            Name     = $TestRepoName
            URL      = $TestRepoURL
            Trusted  = $False
            Priority = 1
        }

        { Register-PSResourceRepository @paramRegisterPSResourceRepository } | Should -Not Throw

        $repo = Get-PSResourceRepository -Name $TestRepoName
        $repo.URL | Should -Be $TestRepoURL
        $repo.Trusted | Should -Be $True
        $repo.Priority | Should -Be 1
    }

    It 'Should register multiple repositories' { Set-ItResult -Pending
	    Unregister-PSResourceRepository -Name $TestRepoName -ErrorAction SilentlyContinue
		Unregister-PSResourceRepository -Name $TestRepoLocalName -ErrorAction SilentlyContinue
		Unregister-PSResourceRepository -Name $PSGalleryName

        Register-PSResourceRepository -Repositories @(
            @{ Name = $TestRepoName; URL = $TestRepoURL; Priority = 15 }
            @{ Name = $TestRepoLocalName; URL = $TestRepoLocalURL }
            @{ PSGallery = $true; Trusted = $true }
        )

        $repos = Get-PSResourceRepository
        $repos.Count | Should -Be 3
        $repo1 = Get-PSResourceRepository $TestRepoName
        $repo1.URL | Should -Be $TestRepoURL
        $repo1.Priority | Should -Be 15


        $repo2 = Get-PSResourceRepository $TestRepoLocalName

		Write-Host $repo2.URL
		$repo2ModifiedURL = $repo2.URL.replace("/","\")

		Write-Host $repo2.URL
		Write-Host $repo2ModifiedURL
		WRite-Host $TestRepoLocalURL
		$repo2ModifiedURL | Should -Be ("file:\\\" + $TestRepoLocalURL)
        $repo2.Priority | Should -Be 50

        $repo3 = Get-PSResourceRepository $PSGalleryName
        $repo3.URL | Should -Be $PSGalleryLocation
        $repo3.Priority | Should -Be 50
    }
}





	
################################
### Set-PSResourceRepository ###
################################
Describe 'Test Set-PSResourceRepository' -tags 'BVT', 'InnerLoop' {

    It 'Should set PSGallery to a trusted installation policy and a non-zero priority' { Set-ItResult -Pending
		Unregister-PSResourceRepository -Name $PSGalleryName
		Register-PSResourceRepository -PSGallery -Trusted:$False -Priority 0
        Set-PSResourceRepository $PSGalleryName -Trusted -Priority 2

        $repo = Get-PSResourceRepository $PSGalleryName
		$repo.URL | Should -Be $PSGalleryLocation
        $repo.Trusted | Should -Be $true
        $repo.Priority | Should -Be 2
    }

    It 'Should set PSGallery to an untrusted installation policy' { Set-ItResult -Pending
		Unregister-PSResourceRepository -Name $PSGalleryName
		Register-PSResourceRepository -PSGallery -Trusted
        Set-PSResourceRepository -Name $PSGalleryName -Trusted:$False

        $repo = Get-PSResourceRepository $PSGalleryName
        $repo.Trusted | Should -Be false
        $repo.Priority | Should -Be 50
    }

    It 'Should fail to set PSGallery to a different URL' { Set-ItResult -Pending
        Set-PSResourceRepository $PSGalleryName -URL $TestRepoURL -ErrorVariable ev -ErrorAction SilentlyContinue

        $ev[0].FullyQualifiedErrorId | Should -Be "The PSGallery repository has pre-defined locations. Setting the 'URL' parameter is not allowed, try again after removing the 'URL' parameter."
    }
}



Describe 'Test Set-PSResourceRepository with hashtable parameters' -tags 'BVT', 'InnerLoop' {

    AfterAll {
    }
    BeforeAll {
    }

    BeforeEach {
   
    }


	# if repo isn't registered it shouldnt be set'
    It 'Should set repository with given hashtable parameters' { Set-ItResult -Pending
        Unregister-PSResourceRepository -Name $TestRepoName
		Unregister-PSResourceRepository -Name $TestRepoLocalName
		Unregister-PSResourceRepository -Name $PSGalleryName

		Register-PSResourceRepository $TestRepoName -URL $TestRepoURL

		$paramSetPSResourceRepository = @{
            Name     = $TestRepoName
            URL      = $TestRepoURL2
            Trusted  = $False
            Priority = 1
        }

        { Set-PSResourceRepository -Repositories $paramSetPSResourceRepository } | Should not Throw

        $repo = Get-PSResourceRepository -Name $TestRepoName
        $repo.URL | Should -Be $TestRepoURL2
        $repo.Trusted | Should -Be false
        $repo.Priority | Should -Be 1
    }

    It 'Should set multiple repositories' { Set-ItResult -Pending
        Unregister-PSResourceRepository -Name $TestRepoName

		Register-PSResourceRepository $TestRepoName -URL $TestRepoURL
		Register-PSResourceRepository $TestRepoLocalName -URL $TestRepoLocalURL -Priority 0
		Register-PSResourceRepository -PSGallery

		Write-Host $TestRepoURL
		$repositories = @(
            @{ Name = $TestRepoName; URL = $TestRepoURL2; Priority = 9 },
            @{ Name = $TestRepoLocalName; URL = $TestRepoLocalURL2; Trusted =$True }
            #@{ Name = $PSGalleryName; Trusted = $True     /*****************************/
        )

        { Set-PSResourceRepository -Repositories $repositories } | Should not Throw

        $repos = Get-PSResourceRepository
        $repos.Count | Should -Be 3

        $repo1 = Get-PSResourceRepository $TestRepoName
        $repo1.URL | Should -Be $TestRepoURL2
        $repo1.Trusted | Should -Be false
        $repo1.Priority | Should -Be 9

        $repo2 = Get-PSResourceRepository $TestRepoLocalName
		$repo2ModifiedURL = $repo2.URL.replace("/","\")
		$repo2ModifiedURL | Should -Be ("file:\\\" + $TestRepoLocalURL2)
        #$repo2.URL | Should -Be $TestRepoLocalURL2
        $repo2.Trusted | Should -Be true
        $repo2.Priority | Should -Be 50

        $repo3 = Get-PSResourceRepository $PSGalleryName
        $repo3.URL | Should -Be $PSGalleryLocation
       # $repo3.Trusted | Should -Be true
        $repo3.Priority | Should -Be 50
    }

}






################################
### Get-PSResourceRepository ###
################################
Describe 'Test Get-PSResourceRepository' -tags 'BVT', 'InnerLoop' {

    BeforeAll {

	    #Register-PSResourceRepository -PSGallery -Trusted -ErrorAction SilentlyContinue
        #Register-PSResourceRepository -Name $TestRepoName -URL $TestRepoURL -Trusted -Priority 2 -ErrorAction SilentlyContinue
        #Register-PSResourceRepository -Name $TestRepoName2 -URL $TestRepoURL2 -Priority 15 -ErrorAction SilentlyContinue
        ##Register-PSResourceRepository -Name $TestRepoLocalName -URL $TestRepoLocalURL -ErrorAction SilentlyContinue
        ##Register-PSResourceRepository -Name $TestRepoLocalName2 -URL $TestRepoLocalURL2 -ErrorAction SilentlyContinue
    }

    AfterAll {
		#Unregister-PSResourceRepository -Name $PSGalleryName -ErrorAction SilentlyContinue
        #Unregister-PSResourceRepository -Name $TestRepoName -ErrorAction SilentlyContinue
        #Unregister-PSResourceRepository -Name $TestRepoName2 -ErrorAction SilentlyContinue
       # Unregister-PSResourceRepository -Name $TestRepoLocalName -ErrorAction SilentlyContinue
       # Unregister-PSResourceRepository -Name $TestRepoLocalName2 -ErrorAction SilentlyContinue
    }

    BeforeEach {
 
    }

    It 'Should get PSGallery repository' { Set-ItResult -Pending
		Unregister-PSResourceRepository -Name $PSGalleryName 
		Register-PSResourceRepository -PSGallery -Trusted -ErrorAction SilentlyContinue

        $repo = Get-PSResourceRepository $PSGalleryName
        $repo.URL | Should -Be $PSGalleryLocation
        $repo.Trusted | Should -Be $true
        $repo.Priority | Should -Be 50
    }

    It 'Should get test repository' { Set-ItResult -Pending
		Unregister-PSResourceRepository -Name $TestRepoName 
		Register-PSResourceRepository -Name $TestRepoName -URL $TestRepoURL -Trusted -Priority 2 -ErrorAction SilentlyContinue

        $repo = Get-PSResourceRepository $TestRepoName
        $repo.URL | Should -Be $TestRepoURL
        $repo.Trusted | Should -Be $true
        $repo.Priority | Should -Be 2
    }



    It 'Should get multiple repositories' { Set-ItResult -Pending
		get-PSResourceRepository
		Unregister-PSResourceRepository -Name $TestRepoLocalName
        Register-PSResourceRepository -Name $TestRepoName2 -URL $TestRepoURL2 -Priority 15 -ErrorAction SilentlyContinue
		Register-PSResourceRepository -Name $TestRepoLocalName2 -URL $TestRepoLocalURL2 -ErrorAction SilentlyContinue

        $repos = Get-PSResourceRepository $PSGalleryName, $TestRepoName2, $TestRepoLocalName2

        $repos.Count | Should -Be 3

		$repos.Name | should contain $PSGalleryName
        $repos.Name | should contain $TestRepoName2
        $repos.Name | should contain $TestRepoLocalName2

		
        $repos.URL | should contain $PSGalleryLocation
        $repos.URL | should contain $TestRepoURL2

		$repoModifiedURL = "file:///" + ($TestRepoLocalURL2.replace("\", "/"));
		$repos.URL | should contain $repoModifiedURL

		$repos.Priority | should contain 15
		$repos.Priority | should contain 50

   }

    It 'Should get all repositories' { Set-ItResult -Pending
		Register-PSResourceRepository $TestRepoLocalName -URL $TestRepoLocalURL

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






#######################################
### Unregister-PSResourceRepository ###
#######################################

Describe 'Test Unregister-PSResourceRepository' -tags 'BVT' {

    BeforeAll {

    }
    AfterAll {
       # Unregister-PSResourceRepository -Name $PSGalleryName -ErrorAction SilentlyContinue
       # Unregister-PSResourceRepository -Name $TestRepoName -ErrorAction SilentlyContinue
       # Unregister-PSResourceRepository -Name $TestRepoName2 -ErrorAction SilentlyContinue
      #  Unregister-PSResourceRepository -Name $TestRepoLocalName -ErrorAction SilentlyContinue
      #  Unregister-PSResourceRepository -Name $TestRepoLocalName2 -ErrorAction SilentlyContinue
    }

    BeforeEach {
      
    }

    ### Unregistering the PowerShell Gallery
    It 'Should unregister the default PSGallery' { Set-ItResult -Pending 
        Unregister-PSResourceRepository $PSGalleryName -ErrorVariable ev -ErrorAction SilentlyContinue

        $repo = Get-PSResourceRepository $PSGalleryName
        $repo | Should -BeNullOrEmpty
    }

	### Unregistering any repository
    It 'Should unregister a given repository' { Set-ItResult -Pending 
        Unregister-PSResourceRepository $TestRepoName -ErrorVariable ev -ErrorAction SilentlyContinue

        $repo = Get-PSResourceRepository $TestRepoName
        $repo | Should -BeNullOrEmpty
    }
	
    It 'Should unregister multiple repositories' { Set-ItResult -Pending
        Unregister-PSResourceRepository $TestRepoName, $TestRepoName2, $TestRepoLocalName

        $repos = Get-PSResourceRepository $TestRepoName, $TestRepoName2, $TestRepoLocalName -ErrorVariable ev -ErrorAction SilentlyContinue
        $repos | Should -BeNullOrEmpty
        $ev[0].FullyQualifiedErrorId | Should -Be "Unable to find repository 'PSGallery'. Use Get-PSResourceRepository to see all available repositories."

    }

}

#>


