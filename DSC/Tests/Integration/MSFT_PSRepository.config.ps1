#region HEADER
# Integration Test Config Template Version: 1.2.0
#endregion

$configFile = [System.IO.Path]::ChangeExtension($MyInvocation.MyCommand.Path, 'json')
if (Test-Path -Path $configFile)
{
    <#
        Allows reading the configuration data from a JSON file
        for real testing scenarios outside of the CI.
    #>
    $ConfigurationData = Get-Content -Path $configFile | ConvertFrom-Json
}
else
{
    $ConfigurationData = @{
        AllNodes = @(
            @{
                NodeName                  = 'localhost'
                CertificateFile           = $env:DscPublicCertificatePath

                Name                      = 'PSTestGallery'

                URL                       = 'https://www.poshtestgallery.com/api/v2'

  

                TestModuleName            = 'ContosoServer'
            }
        )
    }
}

<#
    .SYNOPSIS
        Adds a repository.
#>
Configuration MSFT_PSRepository_AddRepository_Config
{
    Import-DscResource -ModuleName 'PowerShellGet'

    node $AllNodes.NodeName
    {
        PSRepository 'Integration_Test'
        {
            Name                  = $Node.Name
            URL                   = $Node.TestURL
            Priority              = $Node.TestPriority
            Trusted               = $false
        }
    }
}

<#
    .SYNOPSIS
        Installs a module with default parameters from the new repository.
#>
Configuration MSFT_PSRepository_InstallTestModule_Config
{
    Import-DscResource -ModuleName 'PowerShellGet'

    node $AllNodes.NodeName
    {
        PSModule 'Integration_Test'
        {
            Name       = $Node.TestModuleName
            Repository = $Node.Name
        }
    }
}

<#
    .SYNOPSIS
        Changes the properties of the repository.
#>
Configuration MSFT_PSRepository_ChangeRepository_Config
{
    Import-DscResource -ModuleName 'PowerShellGet'

    node $AllNodes.NodeName
    {
        PSRepository 'Integration_Test'
        {
            Name                      = $Node.Name
            URL                       = $Node.URL
            Priority                  = $Node.Priority
            Trusted                   = $false
        }
    }
}

<#
    .SYNOPSIS
        Removes the repository.
#>
Configuration MSFT_PSRepository_RemoveRepository_Config
{
    Import-DscResource -ModuleName 'PowerShellGet'

    node $AllNodes.NodeName
    {
        PSRepository 'Integration_Test'
        {
            Ensure = 'Absent'
            Name   = $Node.Name
        }
    }
}
