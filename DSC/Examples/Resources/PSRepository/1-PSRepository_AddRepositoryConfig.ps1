<#PSScriptInfo
.VERSION 1.0.0
.GUID a1f8ee59-31b6-49be-9175-f7a49b5e03f1
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
        PSRepository_AddRepositoryConfig -RepositoryName 'PSTestGallery'

        Compiles a configuration that downloads and installs the module 'PSLogging'.

    .EXAMPLE
        $configurationParameters = @{
            RepositoryName = 'PSTestGallery'
        }
        Start-AzureRmAutomationDscCompilationJob -ResourceGroupName '<resource-group>' -AutomationAccountName '<automation-account>' -ConfigurationName 'PSRepository_AddRepositoryConfig' -Parameters $configurationParameters

        Compiles a configuration in Azure Automation that downloads and installs
        the module 'PSLogging'.

        Replace the <resource-group> and <automation-account> with correct values.
#>
configuration PSRepository_AddRepositoryConfig
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
            Name                  = $RepositoryName
            URL                   = 'https://www.poshtestgallery.com/api/v2'
            Priority              = 1
            Trusted               = 'True'
        }
    }
}
