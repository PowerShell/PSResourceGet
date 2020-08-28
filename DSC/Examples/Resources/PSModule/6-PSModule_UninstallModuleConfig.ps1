<#PSScriptInfo
.VERSION 1.0.0
.GUID 83a844ed-4e23-427d-94c9-72bdcae0e1bb
.AUTHOR Microsoft Corporation
.COMPANYNAME Microsoft Corporation
.COPYRIGHT
.TAGS DSCConfiguration
.LICENSEURI https://github.com/PowerShell/PowerShellGet/blob/master/LICENSE
.PROJECTURI https://github.com/PowerShell/PowerShellGet
.ICONURI
.EXTERNALMODULEDEPENDENCIES
.REQUIREDSCRIPTS
.EXTERNALSCRIPTDEPENDENCIES
.RELEASENOTES First version.
.PRIVATEDATA 2016-Datacenter,2016-Datacenter-Server-Core
#>

#Requires -module PowerShellGet

<#
    .SYNOPSIS
        Configuration that uninstalls an installed module.

    .DESCRIPTION
        Configuration that uninstalls an installed module.

    .PARAMETER NodeName
        The names of one or more nodes to compile a configuration for.
        Defaults to 'localhost'.

    .PARAMETER ModuleName
        The name of the module to be uninstalled.

    .EXAMPLE
        PSModule_UninstallModuleConfig -ModuleName 'PSLogging'

        Compiles a configuration that downloads and installs the module 'PSLogging'.

    .EXAMPLE
        $configurationParameters = @{
            ModuleName = 'PSLogging'
        }
        Start-AzureRmAutomationDscCompilationJob -ResourceGroupName '<resource-group>' -AutomationAccountName '<automation-account>' -ConfigurationName 'PSModule_UninstallModuleConfig' -Parameters $configurationParameters

        Compiles a configuration in Azure Automation that downloads and installs
        the module 'PSLogging'.

        Replace the <resource-group> and <automation-account> with correct values.
#>
configuration PSModule_UninstallModuleConfig
{
    param
    (
        [Parameter()]
        [System.String[]]
        $NodeName = 'localhost',

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [System.String]
        $ModuleName
    )

    Import-DscResource -ModuleName 'PowerShellGet'

    Node $nodeName
    {
        PSModule 'InstallModule'
        {
            Ensure = 'Absent'
            Name   = $ModuleName
        }
    }
}
