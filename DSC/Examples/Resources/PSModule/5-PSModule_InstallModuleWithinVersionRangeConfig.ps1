<#PSScriptInfo
.VERSION 1.0.0
.GUID b3a8515b-9164-4fc5-9df0-e883f6420a83
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
        Configuration that installs a module withing a specific version range.

    .DESCRIPTION
        Configuration that installs a module withing a specific version range.

    .PARAMETER NodeName
        The names of one or more nodes to compile a configuration for.
        Defaults to 'localhost'.

    .PARAMETER ModuleName
        The name of the module to be downloaded and installed.

    .PARAMETER MinimumVersion
        The minimum version of the module to download and install.

    .PARAMETER MaximumVersion
        The maximum version of the module to download and install.

    .EXAMPLE
        PSModule_InstallModuleWithinVersionRangeConfig -ModuleName 'SqlServer' -MinimumVersion '21.0.17199' -MaximumVersion '21.1.18068'

        Compiles a configuration that downloads and installs the module 'SqlServer'.

    .EXAMPLE
        $configurationParameters = @{
            ModuleName = 'SqlServer'
            MinimumVersion = '21.0.17199'
            MaximumVersion = '21.1.18068'
        }
        Start-AzureRmAutomationDscCompilationJob -ResourceGroupName '<resource-group>' -AutomationAccountName '<automation-account>' -ConfigurationName 'PSModule_InstallModuleWithinVersionRangeConfig' -Parameters $configurationParameters

        Compiles a configuration in Azure Automation that downloads and installs
        the module 'SqlServer'.

        Replace the <resource-group> and <automation-account> with correct values.
#>
configuration PSModule_InstallModuleWithinVersionRangeConfig
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
        $MinimumVersion,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [System.String]
        $MaximumVersion
    )

    Import-DscResource -ModuleName 'PowerShellGet'

    Node $nodeName
    {
        PSModule 'InstallModuleAndAllowClobber'
        {
            Name           = $ModuleName
            Version        = "[$MinimumVersion, $MaximumVersion]"

        }
    }
}
