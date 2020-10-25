#
# Script module for module 'PowerShellGet'
#
Set-StrictMode -Version Latest

# Summary: PowerShellGet is supported on Windows PowerShell 5.1 or later and PowerShell 6.0+

$isCore = ($PSVersionTable.Keys -contains "PSEdition") -and ($PSVersionTable.PSEdition -ne 'Desktop')
if ($isCore)
{
    $script:Framework = 'netstandard2.0'
    $script:FrameworkToRemove = 'net472'
    
} else {
    $script:Framework = 'net472'
    $script:FrameworkToRemove = 'netstandard2.0'
}

# Set up some helper variables to make it easier to work with the module
$script:PSModule = $ExecutionContext.SessionState.Module
$script:PSModuleRoot = $script:PSModule.ModuleBase
$script:PSGet = 'PowerShellGet.dll'

# CONSTRUCT A PATH TO THE CORRECT ASSEMBLY
#$pathToAssembly = [io.path]::combine($PSScriptRoot,  $script:Framework, $script:PSGet)
$pathToFramework = Join-Path -Path $script:PSModuleRoot -ChildPath $script:Framework
$pathToAssembly = Join-Path -Path $pathToFramework -ChildPath $script:PSGet

# NOW LOAD THE APPROPRIATE ASSEMBLY
Import-Module -Name $pathToAssembly
