# ensure variables were passed in
if ($env:NUPKG_PATH -eq $null)
{
    Write-Verbose -Verbose "NUPKG_PATH variable didn't get set properly"
    return 1
}

if ($env:PSRESOURCE_NAME -eq $null)
{
    Write-Verbose -Verbose "PSRESOURCE_NAME variable didn't get set properly"
    return 1
}

if ($env:PSRESOURCE_VERSION -eq $null)
{
    Write-Verbose -Verbose "PSRESOURCE_VERSION variable didn't get set properly"
    return 1
}

if ($env:DESTINATION_ACR_NAME -eq $null)
{
    Write-Verbose -Verbose "DESTINATION_ACR_NAME variable didn't get passed correctly"
    return 1
}

if ($env:DESTINATION_ACR_URI -eq $null)
{
    Write-Verbose -Verbose "DESTINATION_ACR_URI variable didn't get passed correctly"
    return 1
}

if ($env:MI_CLIENTID -eq $null)
{
    Write-Verbose -Verbose "MI_CLIENTID variable didn't get passed correctly"
    return 1
}

try {
    Write-Verbose -Verbose ".nupkg file path: $env:NUPKG_PATH"
    Write-Verbose -Verbose "psresource name: $env:PSRESOURCE_NAME"
    Write-Verbose -Verbose "psresource version: $env:PSRESOURCE_VERSION"
    Write-Verbose -Verbose "ACR name: $env:DESTINATION_ACR_NAME"
    Write-Verbose -Verbose "ACR uri: $env:DESTINATION_ACR_URI"
    Write-Verbose -Verbose "MI client Id: $env:MI_CLIENTID"

    $nupkgFileName = "$($env:PSRESOURCE_NAME).$($env:PSRESOURCE_VERSION).nupkg"
    Write-Verbose -Verbose "Download file"
    Invoke-WebRequest -Uri $env:NUPKG_PATH -OutFile $nupkgFileName

    # Install PSResourceGet 1.1.0
    Write-Verbose "Download PSResourceGet version 1.1.0"
    Register-PSRepository -Name CFS -SourceLocation "https://pkgs.dev.azure.com/powershell/PowerShell/_packaging/powershell/nuget/v2" -InstallationPolicy Trusted
    Install-Module -Repository CFS -Name Microsoft.PowerShell.PSResourceGet -RequiredVersion '1.1.0' -AllowPrerelease -Verbose
    Import-Module Microsoft.PowerShell.PSResourceGet
    Get-Module

    # Login to Azure CLI using Managed Identity
    Write-Verbose -Verbose "Login cli using managed identity"
    az login --identity --client-id $env:MI_CLIENTID

    # Register the target ACR as a PSResourceGet repository
    Write-Verbose -Verbose "Register ACR as a PSResourceGet reposirory"
    Register-PSResourceRepository -Uri $env:DESTINATION_ACR_URI -Name $env:DESTINATION_ACR_NAME -Trusted -Verbose

    Get-PSResourceRepository

    # Publish module to ACR
    Write-Verbose -Verbose "Publish $env:PSRESOURCE_NAME from file $nupkgFileName to ACR $env:DESTINATION_ACR_NAME"

    # unlisted
    $prefix = "unlisted/psresource"
    Publish-PSResource -NupkgPath $nupkgFileName -Repository $env:DESTINATION_ACR_NAME -ModulePrefix $prefix -Confirm:$false

    # public
    $prefix = "public/psresource"
    
    Publish-PSResource -NupkgPath $nupkgFileName -Repository $env:DESTINATION_ACR_NAME -ModulePrefix $prefix -Confirm:$false 
}
catch {
    $_.Exception | Format-List -Force

    return 1
}

return 0