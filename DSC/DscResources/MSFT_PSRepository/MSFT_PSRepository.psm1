$resourceModuleRoot = Split-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -Parent

# Import localization helper functions.
$helperName = 'PowerShellGet.LocalizationHelper'
$dscResourcesFolderFilePath = Join-Path -Path $resourceModuleRoot -ChildPath "Modules\$helperName\$helperName.psm1"
Import-Module -Name $dscResourcesFolderFilePath

$script:localizedData = Get-LocalizedData -ResourceName 'MSFT_PSRepository' -ScriptRoot $PSScriptRoot

# Import resource helper functions.
$helperName = 'PowerShellGet.ResourceHelper'
$dscResourcesFolderFilePath = Join-Path -Path $resourceModuleRoot -ChildPath "Modules\$helperName\$helperName.psm1"
Import-Module -Name $dscResourcesFolderFilePath

<#
    .SYNOPSIS
        Returns the current state of the repository.

    .PARAMETER Name
        Specifies the name of the repository to manage.
#>
function Get-TargetResource {
    <#
        These suppressions are added because this repository have other Visual Studio Code workspace
        settings than those in DscResource.Tests DSC test framework.
        Only those suppression that contradict this repository guideline is added here.
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('DscResource.AnalyzerRules\Measure-FunctionBlockBraces', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('DscResource.AnalyzerRules\Measure-IfStatement', '')]
    [CmdletBinding()]
    [OutputType([System.Collections.Hashtable])]
    param
    (
        [Parameter(Mandatory = $true)]
        [System.String]
        $Name
    )

    $returnValue = @{
        Ensure                    = 'Absent'
        Name                      = $Name
        SourceLocation            = $null
        ScriptSourceLocation      = $null
        PublishLocation           = $null
        ScriptPublishLocation     = $null
        InstallationPolicy        = $null
        PackageManagementProvider = $null
        Trusted                   = $false
        Registered                = $false
    }

    Write-Verbose -Message ($localizedData.GetTargetResourceMessage -f $Name)

    $repository = Get-PSRepository -Name $Name -ErrorAction 'SilentlyContinue'

    if ($repository) {
        $returnValue.Ensure = 'Present'
        $returnValue.SourceLocation = $repository.SourceLocation
        $returnValue.ScriptSourceLocation = $repository.ScriptSourceLocation
        $returnValue.PublishLocation = $repository.PublishLocation
        $returnValue.ScriptPublishLocation = $repository.ScriptPublishLocation
        $returnValue.InstallationPolicy = $repository.InstallationPolicy
        $returnValue.PackageManagementProvider = $repository.PackageManagementProvider
        $returnValue.Trusted = $repository.Trusted
        $returnValue.Registered = $repository.Registered
    }
    else {
        Write-Verbose -Message ($localizedData.RepositoryNotFound -f $Name)
    }

    return $returnValue
}

<#
    .SYNOPSIS
        Determines if the repository is in the desired state.

    .PARAMETER Ensure
        If the repository should be present or absent on the server
        being configured. Default values is 'Present'.

    .PARAMETER Name
        Specifies the name of the repository to manage.

    .PARAMETER SourceLocation
        Specifies the URI for discovering and installing modules from
        this repository. A URI can be a NuGet server feed, HTTP, HTTPS,
        FTP or file location.

    .PARAMETER ScriptSourceLocation
        Specifies the URI for the script source location.

    .PARAMETER PublishLocation
        Specifies the URI of the publish location. For example, for
        NuGet-based repositories, the publish location is similar
        to http://someNuGetUrl.com/api/v2/Packages.

    .PARAMETER ScriptPublishLocation
        Specifies the URI for the script publish location.

    .PARAMETER InstallationPolicy
        Specifies the installation policy. Valid values are  'Trusted'
        or 'Untrusted'. The default value is 'Untrusted'.

    .PARAMETER PackageManagementProvider
        Specifies a OneGet package provider. Default value is 'NuGet'.
#>
function Test-TargetResource {
    <#
        These suppressions are added because this repository have other Visual Studio Code workspace
        settings than those in DscResource.Tests DSC test framework.
        Only those suppression that contradict this repository guideline is added here.
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('DscResource.AnalyzerRules\Measure-FunctionBlockBraces', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('DscResource.AnalyzerRules\Measure-IfStatement', '')]
    [CmdletBinding()]
    [OutputType([System.Boolean])]
    param
    (
        [Parameter()]
        [ValidateSet('Present', 'Absent')]
        [System.String]
        $Ensure = 'Present',

        [Parameter(Mandatory = $true)]
        [System.String]
        $Name,

        [Parameter()]
        [System.String]
        $SourceLocation,

        [Parameter()]
        [System.String]
        $ScriptSourceLocation,

        [Parameter()]
        [System.String]
        $PublishLocation,

        [Parameter()]
        [System.String]
        $ScriptPublishLocation,

        [Parameter()]
        [ValidateSet('Trusted', 'Untrusted')]
        [System.String]
        $InstallationPolicy = 'Untrusted',

        [Parameter()]
        [System.String]
        $PackageManagementProvider = 'NuGet'
    )

    Write-Verbose -Message ($localizedData.TestTargetResourceMessage -f $Name)

    $returnValue = $false

    $getTargetResourceResult = Get-TargetResource -Name $Name

    if ($Ensure -eq $getTargetResourceResult.Ensure) {
        if ($getTargetResourceResult.Ensure -eq 'Present' ) {
            $returnValue = Test-DscParameterState `
                -CurrentValues $getTargetResourceResult `
                -DesiredValues $PSBoundParameters `
                -ValuesToCheck @(
                'SourceLocation'
                'ScriptSourceLocation'
                'PublishLocation'
                'ScriptPublishLocation'
                'InstallationPolicy'
                'PackageManagementProvider'
            )
        }
        else {
            $returnValue = $true
        }
    }

    if ($returnValue) {
        Write-Verbose -Message ($localizedData.InDesiredState -f $Name)
    }
    else {
        Write-Verbose -Message ($localizedData.NotInDesiredState -f $Name)
    }

    return $returnValue
}

<#
    .SYNOPSIS
        Creates, removes or updates the repository.

    .PARAMETER Ensure
        If the repository should be present or absent on the server
        being configured. Default values is 'Present'.

    .PARAMETER Name
        Specifies the name of the repository to manage.

    .PARAMETER SourceLocation
        Specifies the URI for discovering and installing modules from
        this repository. A URI can be a NuGet server feed, HTTP, HTTPS,
        FTP or file location.

    .PARAMETER ScriptSourceLocation
        Specifies the URI for the script source location.

    .PARAMETER PublishLocation
        Specifies the URI of the publish location. For example, for
        NuGet-based repositories, the publish location is similar
        to http://someNuGetUrl.com/api/v2/Packages.

    .PARAMETER ScriptPublishLocation
        Specifies the URI for the script publish location.

    .PARAMETER InstallationPolicy
        Specifies the installation policy. Valid values are  'Trusted'
        or 'Untrusted'. The default value is 'Untrusted'.

    .PARAMETER PackageManagementProvider
        Specifies a OneGet package provider. Default value is 'NuGet'.
#>
function Set-TargetResource {
    <#
        These suppressions are added because this repository have other Visual Studio Code workspace
        settings than those in DscResource.Tests DSC test framework.
        Only those suppression that contradict this repository guideline is added here.
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('DscResource.AnalyzerRules\Measure-FunctionBlockBraces', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('DscResource.AnalyzerRules\Measure-IfStatement', '')]
    [CmdletBinding()]
    param
    (
        [Parameter()]
        [ValidateSet('Present', 'Absent')]
        [System.String]
        $Ensure = 'Present',

        [Parameter(Mandatory = $true)]
        [System.String]
        $Name,

        [Parameter()]
        [System.String]
        $SourceLocation,

        [Parameter()]
        [System.String]
        $ScriptSourceLocation,

        [Parameter()]
        [System.String]
        $PublishLocation,

        [Parameter()]
        [System.String]
        $ScriptPublishLocation,

        [Parameter()]
        [ValidateSet('Trusted', 'Untrusted')]
        [System.String]
        $InstallationPolicy = 'Untrusted',

        [Parameter()]
        [System.String]
        $PackageManagementProvider = 'NuGet'
    )

    $getTargetResourceResult = Get-TargetResource -Name $Name

    # Determine if the repository should be present or absent.
    if ($Ensure -eq 'Present') {
        $repositoryParameters = New-SplatParameterHashTable `
            -FunctionBoundParameters $PSBoundParameters `
            -ArgumentNames @(
            'Name'
            'SourceLocation'
            'ScriptSourceLocation'
            'PublishLocation'
            'ScriptPublishLocation'
            'InstallationPolicy'
            'PackageManagementProvider'
        )

        # Determine if the repository is already present.
        if ($getTargetResourceResult.Ensure -eq 'Present') {
            Write-Verbose -Message ($localizedData.RepositoryExist -f $Name)

            # Repository exist, update the properties.
            Set-PSRepository @repositoryParameters -ErrorAction 'Stop'
        }
        else {
            Write-Verbose -Message ($localizedData.RepositoryDoesNotExist -f $Name)

            # Repository did not exist, create the repository.
            Register-PSRepository @repositoryParameters -ErrorAction 'Stop'
        }
    }
    else {
        if ($getTargetResourceResult.Ensure -eq 'Present') {
            Write-Verbose -Message ($localizedData.RemoveExistingRepository -f $Name)

            # Repository did exist, remove the repository.
            Unregister-PSRepository -Name $Name -ErrorAction 'Stop'
        }
    }
}
