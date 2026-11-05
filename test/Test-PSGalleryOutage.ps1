<#
.SYNOPSIS
    Simulates a PowerShell Gallery outage locally to verify the aka.ms/psgallery hint in PSResourceGet errors.
.DESCRIPTION
    1. Maps a fake gallery host (test.powershellgallery.com) to 127.0.0.1 in the hosts file (requires elevation).
    2. Starts a tiny local HTTP server that returns the given status code for every request.
    3. Registers a temporary V2 repository pointing at the fake host and runs Find-PSResource against it.
    4. Cleans everything up.

    This is a manual test (not run by CI). Build the module first, then run from an elevated PowerShell 7 session:
        ./build.ps1 -Clean -Build -BuildConfiguration Debug -BuildFramework net472
        ./test/FindPSResourceTests/Test-PSGalleryOutage.ps1 -StatusCode 503
#>
param(
    [int] $StatusCode = 503,
    [int] $Port = 8080,
    [string] $FakeHost = 'test.powershellgallery.com',
    [string] $ModulePath = (Join-Path $PSScriptRoot '..\..\out\Microsoft.PowerShell.PSResourceGet\Microsoft.PowerShell.PSResourceGet.psd1'),
    [switch] $SkipHostsFile
)

$ErrorActionPreference = 'Stop'
$hostsFile = "$env:SystemRoot\System32\drivers\etc\hosts"
$hostsLine = "127.0.0.1`t$FakeHost # PSResourceGet outage test"
$repoName = 'FakePSGalleryOutage'

$server = Start-ThreadJob -ArgumentList $Port, $StatusCode -ScriptBlock {
    param($Port, $StatusCode)
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
    $listener.Start()
    try {
        # Poll instead of blocking on AcceptTcpClient so Stop-Job can end the thread.
        while ($true) {
            if (-not $listener.Pending()) { Start-Sleep -Milliseconds 100; continue }
            $client = $listener.AcceptTcpClient()
            $stream = $client.GetStream()
            $reader = [System.IO.StreamReader]::new($stream)
            while (($line = $reader.ReadLine()) -ne '' -and $null -ne $line) { }
            $body = "Simulated $StatusCode"
            $response = "HTTP/1.1 $StatusCode Simulated`r`nContent-Type: text/plain`r`nContent-Length: $($body.Length)`r`nConnection: close`r`n`r`n$body"
            $bytes = [System.Text.Encoding]::ASCII.GetBytes($response)
            $stream.Write($bytes, 0, $bytes.Length)
            $client.Close()
        }
    }
    finally { $listener.Stop() }
}

try {
    if (-not $SkipHostsFile) {
        Add-Content -Path $hostsFile -Value $hostsLine
        Clear-DnsClientCache
    }

    Import-Module $ModulePath -Force
    Register-PSResourceRepository -Name $repoName -Uri "http://${FakeHost}:$Port/api/v2" -ApiVersion V2 -Trusted -Force
    Start-Sleep -Seconds 1

    Write-Host "`n--- Find-PSResource -Name against fake gallery returning HTTP $StatusCode ---" -ForegroundColor Cyan
    Find-PSResource -Name PowerShellGet -Repository $repoName -ErrorAction Continue 2>&1 |
        ForEach-Object { Write-Host $_ -ForegroundColor Yellow }

    Write-Host "`n--- Find-PSResource -Name -Version against fake gallery returning HTTP $StatusCode ---" -ForegroundColor Cyan
    Find-PSResource -Name PowerShellGet -Version 2.2.5 -Repository $repoName -ErrorAction Continue 2>&1 |
        ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
}
finally {
    Unregister-PSResourceRepository -Name $repoName -ErrorAction SilentlyContinue
    if (-not $SkipHostsFile) {
        (Get-Content $hostsFile) | Where-Object { $_ -ne $hostsLine } | Set-Content $hostsFile
        Clear-DnsClientCache
    }
    Stop-Job $server; Remove-Job $server -Force
}