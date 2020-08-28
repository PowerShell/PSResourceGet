<#PSScriptInfo
.VERSION 1.0.0
.GUID 45d8677b-817f-4b2a-8d47-a802c4f758b1
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
        Configuration that installs a module.

    .DESCRIPTION
        Configuration that installs a module.

    .PARAMETER NodeName
        The names of one or more nodes to compile a configuration for.
        Defaults to 'localhost'.

    .PARAMETER ModuleName
        The name of the module to be downloaded and installed.

    .EXAMPLE
        PSModule_InstallModuleConfig -ModuleName 'PSLogging'

        Compiles a configuration that downloads and installs the module 'PSLogging'.

    .EXAMPLE
        $configurationParameters = @{
            ModuleName = 'PSLogging'
        }
        Start-AzureRmAutomationDscCompilationJob -ResourceGroupName '<resource-group>' -AutomationAccountName '<automation-account>' -ConfigurationName 'PSModule_InstallModuleConfig' -Parameters $configurationParameters

        Compiles a configuration in Azure Automation that downloads and installs
        the module 'PSLogging'.

        Replace the <resource-group> and <automation-account> with correct values.
#>
configuration PSModule_InstallModuleConfig
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
            Name = $ModuleName
        }
    }
}
