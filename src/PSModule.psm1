#
# Script module for module 'PowerShellGet'
#
Set-StrictMode -Version Latest

# Summary: PowerShellGet is supported on Windows PowerShell 5.0 or later and PowerShellCore

# Set up some helper variables to make it easier to work with the module
$script:PSModule = $ExecutionContext.SessionState.Module
$script:PSModuleRoot = $script:PSModule.ModuleBase

$script:PSGet = 'PowerShellGet.dll'


# Try to import the PowerShellGet assemblies
$PSGetModulePath = Join-Path -Path $script:PSModuleRoot -ChildPath $script:PSGet


if(-not (Test-Path -Path $PSGetModulePath))
{
    # Should not happen
}

$PSGetModule = Import-Module -Name $PSGetModulePath -PassThru


if($PSGetModule)
{
    $script:PSModule.OnRemove = {
        Remove-Module -ModuleInfo $PSGetModule
    }
}
