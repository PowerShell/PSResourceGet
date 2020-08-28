<#PSScriptInfo
.VERSION 1.0.0
.GUID cbed3f85-50cf-49ca-bde4-1b3ef0e33687
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
        Configuration that installs a module with a specific version.

    .DESCRIPTION
        Configuration that installs a module with a specific version.

    .PARAMETER NodeName
        The names of one or more nodes to compile a configuration for.
        Defaults to 'localhost'.

    .PARAMETER ModuleName
        The name of the module to be downloaded and installed.

    .PARAMETER Version
        The version of the module to download and install.

    .EXAMPLE
        PSModule_InstallModuleSpecificVersionConfig -ModuleName 'SqlServer' -RequiredVersion '21.1.18068'

        Compiles a configuration that downloads and installs the module 'SqlServer'.

    .EXAMPLE
        $configurationParameters = @{
            ModuleName = 'SqlServer'
            RequiredVersion = '21.1.18068'
        }
        Start-AzureRmAutomationDscCompilationJob -ResourceGroupName '<resource-group>' -AutomationAccountName '<automation-account>' -ConfigurationName 'PSModule_InstallModuleSpecificVersionConfig' -Parameters $configurationParameters

        Compiles a configuration in Azure Automation that downloads and installs
        the module 'SqlServer'.

        Replace the <resource-group> and <automation-account> with correct values.
#>
configuration PSModule_InstallModuleSpecificVersionConfig
{
    param
    (
        [Parameter()]
        [System.String[]]
        $NodeName = 'localhost',

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [System.String]
        $ModuleName,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [System.String]
        $Version
    )

    Import-DscResource -ModuleName 'PowerShellGet'

    Node $nodeName
    {
        PSModule 'InstallModuleAndAllowClobber'
        {
            Name            = $ModuleName
            Version         = $Version
        }
    }
}
