<#PSScriptInfo
.VERSION 1.0.0
.GUID d16da19d-439a-4730-8e02-6928d6a8ed28
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
        Configuration that installs a module, and overrides the
        current trust level for the package source.

    .DESCRIPTION
        Configuration that installs a module, and overrides the
        current trust level for the package source.

    .PARAMETER NodeName
        The names of one or more nodes to compile a configuration for.
        Defaults to 'localhost'.

    .PARAMETER ModuleName
        The name of the module to be downloaded and installed.

    .EXAMPLE
        PSModule_InstallModuleTrustedConfig -ModuleName 'PSLogging'

        Compiles a configuration that downloads and installs the module 'PSLogging'.

    .EXAMPLE
        $configurationParameters = @{
            ModuleName = 'PSLogging'
        }
        Start-AzureRmAutomationDscCompilationJob -ResourceGroupName '<resource-group>' -AutomationAccountName '<automation-account>' -ConfigurationName 'PSModule_InstallModuleTrustedConfig' -Parameters $configurationParameters

        Compiles a configuration in Azure Automation that downloads and installs
        the module 'PSLogging'.

        Replace the <resource-group> and <automation-account> with correct values.
#>
configuration PSModule_InstallModuleTrustedConfig
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
        PSModule 'InstallModuleAsTrusted'
        {
            Name               = $ModuleName
            InstallationPolicy = 'Trusted'
        }
    }
}
