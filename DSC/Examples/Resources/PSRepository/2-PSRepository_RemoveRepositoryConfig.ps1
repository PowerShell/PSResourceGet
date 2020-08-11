<#PSScriptInfo
.VERSION 1.0.0
.GUID c9a8b46f-12a6-46e1-8c6b-946ac9995aad
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

    .PARAMETER RepositoryName
        The name of the repository that will be added.

    .EXAMPLE
        PSRepository_RemoveRepositoryConfig -RepositoryName 'PSTestGallery'

        Compiles a configuration that downloads and installs the module 'PSLogging'.

    .EXAMPLE
        $configurationParameters = @{
            RepositoryName = 'PSTestGallery'
        }
        Start-AzureRmAutomationDscCompilationJob -ResourceGroupName '<resource-group>' -AutomationAccountName '<automation-account>' -ConfigurationName 'PSRepository_RemoveRepositoryConfig' -Parameters $configurationParameters

        Compiles a configuration in Azure Automation that downloads and installs
        the module 'PSLogging'.

        Replace the <resource-group> and <automation-account> with correct values.
#>
configuration PSRepository_RemoveRepositoryConfig
{
    param
    (
        [Parameter()]
        [System.String[]]
        $NodeName = 'localhost',

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [System.String]
        $RepositoryName
    )

    Import-DscResource -ModuleName 'PowerShellGet'

    Node $nodeName
    {
        PSRepository 'AddRepository'
        {
            Ensure = 'Absent'
            Name   = $RepositoryName
        }
    }
}
