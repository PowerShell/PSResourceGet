# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

Describe 'PSGetMigration Module' {

    BeforeAll {
        $modulePath = Join-Path $PSScriptRoot '..' 'PSGetMigration.psd1'
        Import-Module $modulePath -Force

        # Helper to create temp script files
        function New-TempScript {
            param([string]$Content)
            $tempFile = [System.IO.Path]::GetTempFileName() -replace '\.tmp$', '.ps1'
            Set-Content -Path $tempFile -Value $Content -Encoding UTF8
            return $tempFile
        }
    }

    Context 'Get-PSGetCommandMapping' {
        It 'Returns a hashtable with expected cmdlet mappings' {
            $mapping = Get-PSGetCommandMapping
            $mapping | Should -BeOfType [hashtable]
            $mapping.Keys.Count | Should -BeGreaterThan 20
        }

        It 'Maps Install-Module to Install-PSResource' {
            $mapping = Get-PSGetCommandMapping
            $mapping['Install-Module'].Command | Should -Be 'Install-PSResource'
        }

        It 'Maps Find-DscResource to Find-PSResource with Type parameter' {
            $mapping = Get-PSGetCommandMapping
            $mapping['Find-DscResource'].Command | Should -Be 'Find-PSResource'
            $mapping['Find-DscResource'].ExtraParams.Type | Should -Be 'DscResource'
        }

        It 'Marks Find-RoleCapability as having no equivalent' {
            $mapping = Get-PSGetCommandMapping
            $mapping['Find-RoleCapability'].Command | Should -BeNullOrEmpty
            $mapping['Find-RoleCapability'].Warning | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Get-PSGetParameterMapping' {
        It 'Returns parameter rules' {
            $params = Get-PSGetParameterMapping
            $params | Should -BeOfType [hashtable]
        }

        It 'Maps AllowPrerelease to Prerelease (Rename)' {
            $params = Get-PSGetParameterMapping
            $params['AllowPrerelease'].Type | Should -Be 'Rename'
            $params['AllowPrerelease'].NewName | Should -Be 'Prerelease'
        }

        It 'Maps AllowClobber as Remove with warning' {
            $params = Get-PSGetParameterMapping
            $params['AllowClobber'].Type | Should -Be 'Remove'
            $params['AllowClobber'].Warning | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Find-PSGetCommand' {
        It 'Finds Install-Module in a script' {
            $file = New-TempScript -Content 'Install-Module -Name Pester -Force'
            try {
                $results = @(Find-PSGetCommand -Path $file)
                $results.Count | Should -Be 1
                $results[0].CommandName | Should -Be 'Install-Module'
                $results[0].Line | Should -Be 1
            }
            finally {
                Remove-Item $file -Force
            }
        }

        It 'Finds multiple PSGet commands' {
            $content = @'
Install-Module -Name Pester
Find-Module -Name Az
Get-InstalledModule
'@
            $file = New-TempScript -Content $content
            try {
                $results = @(Find-PSGetCommand -Path $file)
                $results.Count | Should -Be 3
                $results[0].CommandName | Should -Be 'Install-Module'
                $results[1].CommandName | Should -Be 'Find-Module'
                $results[2].CommandName | Should -Be 'Get-InstalledModule'
            }
            finally {
                Remove-Item $file -Force
            }
        }

        It 'Does not match non-PSGet commands' {
            $file = New-TempScript -Content 'Get-Module -Name Pester'
            try {
                $results = @(Find-PSGetCommand -Path $file)
                $results.Count | Should -Be 0
            }
            finally {
                Remove-Item $file -Force
            }
        }

        It 'Does not match commands in comments' {
            $file = New-TempScript -Content '# Install-Module -Name Pester'
            try {
                $results = @(Find-PSGetCommand -Path $file)
                $results.Count | Should -Be 0
            }
            finally {
                Remove-Item $file -Force
            }
        }
    }

    Context 'Convert-PSGetCommand' {
        BeforeAll {
            function Invoke-ConvertFromScript {
                param([string]$Script)
                $file = New-TempScript -Content $Script
                try {
                    $cmd = @(Find-PSGetCommand -Path $file)[0]
                    return Convert-PSGetCommand -CommandInfo $cmd
                }
                finally {
                    Remove-Item $file -Force
                }
            }
        }

        It 'Converts Install-Module to Install-PSResource' {
            $result = Invoke-ConvertFromScript -Script 'Install-Module -Name Pester'
            $result.ConvertedText | Should -Be 'Install-PSResource -Name Pester'
            $result.Status | Should -Be 'Converted'
        }

        It 'Converts Find-Module to Find-PSResource' {
            $result = Invoke-ConvertFromScript -Script 'Find-Module -Name Az'
            $result.ConvertedText | Should -Be 'Find-PSResource -Name Az'
        }

        It 'Converts Find-DscResource and adds -Type DscResource' {
            $result = Invoke-ConvertFromScript -Script 'Find-DscResource -Name MyDsc'
            $result.ConvertedText | Should -Match 'Find-PSResource'
            $result.ConvertedText | Should -Match '-Type DscResource'
            $result.ConvertedText | Should -Match '-Name MyDsc'
        }

        It 'Converts -RequiredVersion to -Version' {
            $result = Invoke-ConvertFromScript -Script "Install-Module -Name Pester -RequiredVersion '5.0.0'"
            $result.ConvertedText | Should -Match "-Version '5.0.0'"
            $result.ConvertedText | Should -Not -Match '-RequiredVersion'
        }

        It 'Converts -MinimumVersion to -Version range' {
            $result = Invoke-ConvertFromScript -Script "Install-Module -Name Pester -MinimumVersion '4.0'"
            $result.ConvertedText | Should -Match "-Version '\[4\.0,\)'"
        }

        It 'Converts -MaximumVersion to -Version range' {
            $result = Invoke-ConvertFromScript -Script "Install-Module -Name Pester -MaximumVersion '5.0'"
            $result.ConvertedText | Should -Match "-Version '\(,5\.0\]'"
        }

        It 'Merges -MinimumVersion and -MaximumVersion into a single range' {
            $result = Invoke-ConvertFromScript -Script "Install-Module -Name Pester -MinimumVersion '4.0' -MaximumVersion '5.0'"
            $result.ConvertedText | Should -Match "-Version '\[4\.0,5\.0\]'"
        }

        It 'Converts -AllVersions to -Version wildcard' {
            $result = Invoke-ConvertFromScript -Script 'Find-Module -Name Az -AllVersions'
            $result.ConvertedText | Should -Match "-Version '\*'"
        }

        It 'Renames -AllowPrerelease to -Prerelease' {
            $result = Invoke-ConvertFromScript -Script 'Find-Module -Name Az -AllowPrerelease'
            $result.ConvertedText | Should -Match '-Prerelease'
            $result.ConvertedText | Should -Not -Match '-AllowPrerelease'
        }

        It 'Renames -NuGetApiKey to -ApiKey' {
            $result = Invoke-ConvertFromScript -Script "Publish-Module -Path ./MyModule -NuGetApiKey 'abc123'"
            $result.ConvertedText | Should -Match "-ApiKey 'abc123'"
            $result.ConvertedText | Should -Not -Match '-NuGetApiKey'
        }

        It 'Removes -AllowClobber with warning' {
            $result = Invoke-ConvertFromScript -Script 'Install-Module -Name Pester -AllowClobber'
            $result.ConvertedText | Should -Not -Match '-AllowClobber'
            $result.Warnings | Should -Not -BeNullOrEmpty
            $result.Warnings | Where-Object { $_ -match 'AllowClobber' } | Should -Not -BeNullOrEmpty
        }

        It 'Replaces -Force with -Reinstall for Install-PSResource' {
            $result = Invoke-ConvertFromScript -Script 'Install-Module -Name Pester -Force'
            $result.ConvertedText | Should -Match '-Reinstall'
            $result.ConvertedText | Should -Not -Match '-Force'
        }

        It 'Returns NoEquivalent for Find-RoleCapability' {
            $result = Invoke-ConvertFromScript -Script 'Find-RoleCapability -Name MyRole'
            $result.Status | Should -Be 'NoEquivalent'
            $result.ConvertedText | Should -BeNullOrEmpty
            $result.Warnings | Should -Not -BeNullOrEmpty
        }

        It 'Preserves unmapped parameters' {
            $result = Invoke-ConvertFromScript -Script 'Install-Module -Name Pester -Scope CurrentUser -Repository PSGallery'
            $result.ConvertedText | Should -Match '-Scope CurrentUser'
            $result.ConvertedText | Should -Match '-Repository PSGallery'
        }

        It 'Converts repository cmdlets' {
            $result = Invoke-ConvertFromScript -Script "Register-PSRepository -Name MyRepo -SourceLocation 'https://example.com'"
            $result.ConvertedText | Should -Match 'Register-PSResourceRepository'
        }
    }

    Context 'ConvertTo-PSResourceGetScript' {
        It 'Returns conversion results for a file' {
            $content = @'
Install-Module -Name Pester -Force
Find-Module -Name Az -AllowPrerelease
'@
            $file = New-TempScript -Content $content
            try {
                $results = @(ConvertTo-PSResourceGetScript -Path $file)
                $results.Count | Should -Be 2
                $results[0].Status | Should -Be 'Converted'
                $results[1].Status | Should -Be 'Converted'
            }
            finally {
                Remove-Item $file -Force
            }
        }

        It 'Returns nothing for files without PSGet commands' {
            $file = New-TempScript -Content 'Get-Module -Name Pester'
            try {
                $results = @(ConvertTo-PSResourceGetScript -Path $file)
                $results.Count | Should -Be 0
            }
            finally {
                Remove-Item $file -Force
            }
        }

        It 'Applies changes in-place when -Apply is specified' {
            $content = 'Install-Module -Name Pester'
            $file = New-TempScript -Content $content
            try {
                ConvertTo-PSResourceGetScript -Path $file -Apply
                $newContent = Get-Content -Path $file -Raw
                $newContent | Should -Match 'Install-PSResource'
                $newContent | Should -Not -Match 'Install-Module'

                # Verify backup was created
                "$file.bak" | Should -Exist
            }
            finally {
                Remove-Item $file -Force -ErrorAction SilentlyContinue
                Remove-Item "$file.bak" -Force -ErrorAction SilentlyContinue
            }
        }

        It 'Applies changes to a custom backup path' {
            $content = 'Install-Module -Name Pester'
            $file = New-TempScript -Content $content
            $backupDir = Join-Path ([System.IO.Path]::GetTempPath()) "psget-migration-test-$(Get-Random)"
            try {
                ConvertTo-PSResourceGetScript -Path $file -Apply -BackupPath $backupDir
                $backupDir | Should -Exist
                $newContent = Get-Content -Path $file -Raw
                $newContent | Should -Match 'Install-PSResource'
            }
            finally {
                Remove-Item $file -Force -ErrorAction SilentlyContinue
                Remove-Item $backupDir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
}
