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
                NodeName        = 'localhost'
                CertificateFile = $env:DscPublicCertificatePath

                Module1_Name     = 'PSLogging'
                Module2_Name     = 'SqlServer'

                Module2_RequiredVersion = '21.0.17279'
                Module2_MinimumVersion = '21.0.17199'
                Module2_MaximumVersion = '21.1.18068'
            }
        )
    }
}

<#
    .SYNOPSIS
        Changes the repository (package source) 'PSGallery' to not trusted.

    .NOTES
        Since the module is installed by SYSTEM as default this is done in
        case the PSGallery is already trusted for SYSTEM.
#>
Configuration MSFT_PSModule_SetPackageSourceAsNotTrusted_Config
{
    Import-DscResource -ModuleName 'PowerShellGet'

    node $AllNodes.NodeName
    {
        PSRepository 'Integration_Test'
        {
            Name               = 'PSGallery'
        }
    }
}

<#
    .SYNOPSIS
        Installs a module as trusted.

    .NOTES
        This assumes that the package source 'PSGallery' is not trusted for SYSTEM.
#>
Configuration MSFT_PSModule_InstallWithTrusted_Config
{
    Import-DscResource -ModuleName 'PowerShellGet'

    node $AllNodes.NodeName
    {
        PSModule 'Integration_Test'
        {
            Name               = $Node.Module1_Name
        }
    }
}

<#
    .SYNOPSIS
        Uninstalls a module ($Node.Module1_Name).
#>
Configuration MSFT_PSModule_UninstallModule1_Config
{
    Import-DscResource -ModuleName 'PowerShellGet'

    node $AllNodes.NodeName
    {
        PSModule 'Integration_Test'
        {
            Ensure = 'Absent'
            Name   = $Node.Module1_Name
        }
    }
}

<#
    .SYNOPSIS
        Changes the repository (package source) 'PSGallery' to trusted.

    .NOTES
        Since the module is installed by SYSTEM as default, the package
        source 'PSGallery' must be trusted for SYSTEM for some of the
        tests.
#>
Configuration MSFT_PSModule_SetPackageSourceAsTrusted_Config
{
    Import-DscResource -ModuleName 'PowerShellGet'

    node $AllNodes.NodeName
    {
        PSRepository 'Integration_Test'
        {
            Name               = 'PSGallery'
        }
    }
}

<#
    .SYNOPSIS
        Installs a module with the default parameters.
#>
Configuration MSFT_PSModule_DefaultParameters_Config
{
    Import-DscResource -ModuleName 'PowerShellGet'

    node $AllNodes.NodeName
    {
        PSModule 'Integration_Test'
        {
            Name = $Node.Module1_Name
        }
    }
}

<#
    .SYNOPSIS
        Installed a module using AllowClobber.

    .NOTES
        This test uses SqlServer module that actually needs AllowClobber.
        On the build worker there are other modules (SQLPS) already installed,
        those modules have the same cmdlets in them.
#>
Configuration MSFT_PSModule_UsingAllowClobber_Config
{
    Import-DscResource -ModuleName 'PowerShellGet'

    node $AllNodes.NodeName
    {
        PSModule 'Integration_Test'
        {
            Name         = $Node.Module2_Name
            NoClobber    = $false
        }
    }
}

<#
    .SYNOPSIS
        Uninstalls a module ($Node.Module2_Name).
#>
Configuration MSFT_PSModule_UninstallModule2_Config
{
    Import-DscResource -ModuleName 'PowerShellGet'

    node $AllNodes.NodeName
    {
        PSModule 'Integration_Test'
        {
            Ensure = 'Absent'
            Name   = $Node.Module2_Name
        }
    }
}

<#
    .SYNOPSIS
        Installs a module with the specific version.
#>
Configuration MSFT_PSModule_RequiredVersion_Config
{
    Import-DscResource -ModuleName 'PowerShellGet'

    node $AllNodes.NodeName
    {
        PSModule 'Integration_Test'
        {
            Name            = $Node.Module2_Name
            Version         = $Node.Module2_RequiredVersion
            NoClobber       = $false
       }
    }
}

<#
    .SYNOPSIS
        Installs a module with the specific version.
#>
Configuration MSFT_PSModule_RequiredVersion_Config
{
    Import-DscResource -ModuleName 'PowerShellGet'

    node $AllNodes.NodeName
    {
        PSModule 'Integration_Test'
        {
            Name            = $Node.Module2_Name
            Version         = $Node.Module2_RequiredVersion
            NoClobber       = $false
        }
    }
}

<#
    .SYNOPSIS
        Installs a module within the specific version range.
#>
Configuration MSFT_PSModule_VersionRange_Config
{
    Import-DscResource -ModuleName 'PowerShellGet'

    node $AllNodes.NodeName
    {
        PSModule 'Integration_Test'
        {
            Name           = $Node.Module2_Name
            Version        = "[$($Node.Module2_MinimumVersion), $($Node.Module2_MaximumVersion)]"
            NoClobber      = $false
        }
    }
}
