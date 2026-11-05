<#
.SYNOPSIS
    Simulates a PowerShell Gallery outage locally to verify the aka.ms/psgallery hint in PSResourceGet errors.
.DESCRIPTION
    1. Maps a fake gallery host (test.powershellgallery.com) to 127.0.0.1 in the hosts file (requires elevation).
    2. Starts a tiny local HTTP server on that host that returns -StatusCode:
         -Mode All          every request fails (search, download, and publish).
         -Mode DownloadOnly search requests are forwarded to the real PowerShell Gallery (with download links
                            rewritten to the fake host) and only package downloads and publish fail.
    3. Registers a temporary V2 repository pointing at the fake host and runs Find, Install, Save, Update,
       and Publish against it, printing every error each command reports.
    4. Cleans everything up.

    Every error from the fake gallery should end with:
        The PowerShell Gallery may be experiencing an issue. Check https://aka.ms/psgallery for the current service status.

    This is a manual test (not run by CI). Build the module first, then run from an elevated PowerShell 7 session:
        ./build.ps1 -Clean -Build -BuildConfiguration Debug -BuildFramework net472
        ./test/Test-PSGalleryOutage.ps1 -StatusCode 503
        ./test/Test-PSGalleryOutage.ps1 -StatusCode 404 -Mode DownloadOnly -UpdateName <outdated module you have installed>
.PARAMETER UpdateName
    Name of a module installed for the current user that has a newer version on the PowerShell Gallery. Update-PSResource
    only reaches the download step for such a module. If omitted, the Update case is skipped. Downloads always fail, so the
    installed module is never changed.
#>
param(
    [int] $StatusCode = 503,
    [ValidateSet('All', 'DownloadOnly')]
    [string] $Mode = 'All',
    [string] $UpdateName,
    [int] $Port = 8080,
    [string] $FakeHost = 'test.powershellgallery.com',
    [string] $ModulePath = (Join-Path $PSScriptRoot '..\out\Microsoft.PowerShell.PSResourceGet\Microsoft.PowerShell.PSResourceGet.psd1'),
    [switch] $SkipHostsFile
)

$ErrorActionPreference = 'Stop'
$hostsFile = "$env:SystemRoot\System32\drivers\etc\hosts"
$hostsLine = "127.0.0.1`t$FakeHost # PSResourceGet outage test"
$repoName = 'FakePSGalleryOutage'
$workDir = Join-Path ([System.IO.Path]::GetTempPath()) 'PSGalleryOutageTest'

# Bind the port here (not inside the job) so a port that's already in use fails fast instead of
# silently sending requests to whatever else is listening on it.
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
try { $listener.Start() }
catch { throw "Port $Port is already in use. Stop the process using it or pass a different -Port. $_" }

$server = Start-ThreadJob -ArgumentList $listener, $StatusCode, $Mode, "http://${FakeHost}:$Port/api/v2" -ScriptBlock {
    param($listener, $StatusCode, $Mode, $fakeRepoUri)
    $http = [System.Net.Http.HttpClient]::new()
    # Poll instead of blocking on AcceptTcpClient so Stop-Job can end the thread.
    while ($true) {
        if (-not $listener.Pending()) { Start-Sleep -Milliseconds 50; continue }
        $client = $listener.AcceptTcpClient()
        try {
            $stream = $client.GetStream()
            $reader = [System.IO.StreamReader]::new($stream)
            $requestLine = $reader.ReadLine()
            while (($line = $reader.ReadLine()) -ne '' -and $null -ne $line) { }
            $method, $path = ($requestLine -split ' ')[0, 1]

            if ($Mode -eq 'DownloadOnly' -and $method -eq 'GET' -and $path -notmatch '/package/') {
                # Forward search/metadata queries to the real gallery, pointing download links back at this server.
                $body = $http.GetStringAsync("https://www.powershellgallery.com$path").GetAwaiter().GetResult()
                $bytes = [System.Text.Encoding]::UTF8.GetBytes($body.Replace('https://www.powershellgallery.com/api/v2', $fakeRepoUri))
                $header = "HTTP/1.1 200 OK`r`nContent-Type: application/atom+xml;charset=utf-8`r`nContent-Length: $($bytes.Length)`r`nConnection: close`r`n`r`n"
            }
            else {
                $bytes = [System.Text.Encoding]::ASCII.GetBytes("Simulated $StatusCode")
                $header = "HTTP/1.1 $StatusCode Simulated`r`nContent-Type: text/plain`r`nContent-Length: $($bytes.Length)`r`nConnection: close`r`n`r`n"
            }

            $headerBytes = [System.Text.Encoding]::ASCII.GetBytes($header)
            $stream.Write($headerBytes, 0, $headerBytes.Length)
            $stream.Write($bytes, 0, $bytes.Length)
        }
        finally { $client.Close() }
    }
}

function Invoke-OutageCase([string] $Name, [scriptblock] $Command) {
    Write-Host "`n--- $Name ---" -ForegroundColor Cyan
    $errors = @()
    try { $errors = @(& $Command 2>&1 | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] }) }
    catch { $errors += $_ }

    if (-not $errors) { Write-Host '(no error)' -ForegroundColor Green }
    foreach ($err in $errors) {
        $color = if ($err.Exception.Message -like '*aka.ms/psgallery*') { 'Yellow' } else { 'DarkGray' }
        Write-Host $err.Exception.Message -ForegroundColor $color
    }
}

try {
    if (-not $SkipHostsFile) {
        Add-Content -Path $hostsFile -Value $hostsLine
        Clear-DnsClientCache
    }

    Remove-Item $workDir -Recurse -Force -ErrorAction SilentlyContinue
    $publishDir = New-Item -ItemType Directory -Path (Join-Path $workDir 'OutageTestModule') -Force
    New-ModuleManifest -Path (Join-Path $publishDir 'OutageTestModule.psd1') -ModuleVersion 1.0.0 -Author 'test' -Description 'Outage test module'

    Import-Module $ModulePath -Force
    Register-PSResourceRepository -Name $repoName -Uri "http://${FakeHost}:$Port/api/v2" -ApiVersion V2 -Trusted -Force

    Write-Host "Fake gallery: http://${FakeHost}:$Port/api/v2  Mode: $Mode  Status: $StatusCode" -ForegroundColor Cyan
    Write-Host 'Errors that include the aka.ms/psgallery hint are shown in yellow; other errors in gray.' -ForegroundColor Cyan

    $ErrorActionPreference = 'Continue'
    Invoke-OutageCase 'Find-PSResource -Name' { Find-PSResource -Name PowerShellGet -Repository $repoName }
    Invoke-OutageCase 'Find-PSResource -Name -Version' { Find-PSResource -Name PowerShellGet -Version 2.2.5 -Repository $repoName }
    Invoke-OutageCase 'Find-PSResource -Name -Version (range)' { Find-PSResource -Name PowerShellGet -Version '[2.0.0, 3.0.0]' -Repository $repoName }
    # -Reinstall forces the download step even if the module is already installed; the download always fails.
    Invoke-OutageCase 'Install-PSResource' { Install-PSResource -Name PowerShellGet -Version 2.2.5 -Repository $repoName -Scope CurrentUser -Reinstall }
    Invoke-OutageCase 'Save-PSResource' { Save-PSResource -Name PowerShellGet -Version 2.2.5 -Repository $repoName -Path $workDir }
    if ($UpdateName) {
        Invoke-OutageCase "Update-PSResource -Name $UpdateName" { Update-PSResource -Name $UpdateName -Repository $repoName -Scope CurrentUser }
    }
    else {
        Write-Host "`n--- Update-PSResource --- skipped (pass -UpdateName <outdated installed module> to test it)" -ForegroundColor DarkGray
    }
    Invoke-OutageCase 'Publish-PSResource' { Publish-PSResource -Path $publishDir -Repository $repoName -ApiKey 'fake' -SkipDependenciesCheck -AllowInsecureConnection $true }
}
finally {
    $ErrorActionPreference = 'Continue'
    Unregister-PSResourceRepository -Name $repoName -ErrorAction SilentlyContinue
    if (-not $SkipHostsFile) {
        (Get-Content $hostsFile) | Where-Object { $_ -ne $hostsLine } | Set-Content $hostsFile
        Clear-DnsClientCache
    }
    Stop-Job $server; Remove-Job $server -Force
    $listener.Stop()
    Remove-Item $workDir -Recurse -Force -ErrorAction SilentlyContinue
}
