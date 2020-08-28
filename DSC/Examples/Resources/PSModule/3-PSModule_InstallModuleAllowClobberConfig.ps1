<#PSScriptInfo
.VERSION 1.0.0
.GUID 47eb256b-9c81-437d-9148-890bc94f15ed
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
        Configuration that installs a module and allows clobber.

    .DESCRIPTION
        Configuration that installs a module and allows clobber.

    .PARAMETER NodeName
        The names of one or more nodes to compile a configuration for.
        Defaults to 'localhost'.

    .PARAMETER ModuleName
        The name of the module to be downloaded and installed.

    .EXAMPLE
        PSModule_InstallModuleAllowClobberConfig -ModuleName 'SqlServer'

        Compiles a configuration that downloads and installs the module 'SqlServer'.

    .EXAMPLE
        $configurationParameters = @{
            ModuleName = 'SqlServer'
        }
        Start-AzureRmAutomationDscCompilationJob -ResourceGroupName '<resource-group>' -AutomationAccountName '<automation-account>' -ConfigurationName 'PSModule_InstallModuleAllowClobberConfig' -Parameters $configurationParameters

        Compiles a configuration in Azure Automation that downloads and installs
        the module 'SqlServer'.

        Replace the <resource-group> and <automation-account> with correct values.
#>
configuration PSModule_InstallModuleAllowClobberConfig
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
        PSModule 'InstallModuleAndAllowClobber'
        {
            Name         = $ModuleName
            NoClobber    = $false
        }
    }
}
