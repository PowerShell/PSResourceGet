# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

param(
    [Parameter(Mandatory)]
    [string]$Token
)

Write-Host "Install and import PowerShell modules"
#Set-PSRepository -Name PSGallery -InstallationPolicy Trusted | Out-Null
#Install-Module -Name PowerShellForGitHub -Scope CurrentUser -Force
Import-Module $PSScriptRoot/releaseTools.psm1

Write-Host "Setup authentication"
Set-GitHubConfiguration -SuppressTelemetryReminder
$password = ConvertTo-SecureString -String $Token -AsPlainText -Force
Set-GitHubAuthentication -Credential (New-Object System.Management.Automation.PSCredential ("token", $password))