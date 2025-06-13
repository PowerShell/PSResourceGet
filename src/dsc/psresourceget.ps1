[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('repository', 'psresource', 'repositories', 'psresources')]
    [string]$ResourceType,
    [Parameter(Mandatory = $true)]
    [ValidateSet('Get', 'Set', 'Export', 'Test')]
    [string]$Operation,
    [Parameter(ValueFromPipeline)]
    $stdinput
)

# catch any un-caught exception and write it to the error stream
trap {
    Write-Trace -Level Error -message $_.Exception.Message
    exit 1
}

function Write-Trace {
    param(
        [string]$message,
        [string]$level = 'Error'
    )

    $trace = [pscustomobject]@{
        $level.ToLower() = $message
    } | ConvertTo-Json -Compress

    $host.ui.WriteErrorLine($trace)
}
