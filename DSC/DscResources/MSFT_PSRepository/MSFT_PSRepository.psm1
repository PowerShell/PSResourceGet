$resourceModuleRoot = Split-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -Parent

# Import localization helper functions.
$helperName = 'PowerShellGet.LocalizationHelper'
$dscResourcesFolderFilePath = Join-Path -Path $resourceModuleRoot -ChildPath "Modules\$helperName\$helperName.psm1"
Import-Module -Name $dscResourcesFolderFilePath

$script:localizedData = Get-LocalizedData -ResourceName 'MSFT_PSRepository' -ScriptRoot $PSScriptRoot

# Import resource helper functions
$helperName = 'PowerShellGet.ResourceHelper'
$dscResourcesFolderFilePath = Join-Path -Path $resourceModuleRoot -ChildPath "Modules\$helperName\$helperName.psm1"
Import-Module -Name $dscResourcesFolderFilePath -force

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
        URL                       = $null
        Priority                  = $null
        Trusted                   = $false
        Registered                = $false
    }

    Write-Verbose -Message ($localizedData.GetTargetResourceMessage -f $Name)

    $repository = Get-PSResourceRepository -Name $Name -ErrorAction 'SilentlyContinue'

    if ($repository) {
        $returnValue.Ensure = 'Present'
        $returnValue.URL = $repository.URL
        $returnValue.Priority = $repository.Priority
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

    .PARAMETER URL
        Specifies the URI for discovering and installing modules from
        this repository. A URI can be a NuGet server feed, HTTP, HTTPS,
        FTP or file location.

    .PARAMETER Trusted
        Specifies the installation policy. Valid values are  'Trusted'
        or 'Untrusted'. The default value is 'Untrusted'.
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
        $URL,

        [Parameter()]
        [System.Int32]
        $Priority,

        [Parameter()]
        [System.Boolean]
        $Trusted = $false
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
                'URL'
                'Priority'
                'Trusted'
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

    .PARAMETER URL
        Specifies the URI for discovering and installing modules from
        this repository. A URI can be a NuGet server feed, HTTP, HTTPS,
        FTP or file location.

    .PARAMETER Priority
        Specifies the priority for the URI for the script source location.

    .PARAMETER Trusted
        Specifies the installation policy. Valid values are  'Trusted'
        or 'Untrusted'. The default value is 'Untrusted'.
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
        $URL,

        [Parameter()]
        [System.String]
        $Priority = '50',

        [Parameter()]
        [System.Boolean]
        $TrustRepository = $false
    )

    $getTargetResourceResult = Get-TargetResource -Name $Name

    # Determine if the repository should be present or absent.
    if ($Ensure -eq 'Present') {
        $repositoryParameters = New-SplatParameterHashTable `
            -FunctionBoundParameters $PSBoundParameters `
            -ArgumentNames @(
            'Name'
            'URL'
            'Priority'
            'TrustRepository'
        )

        # Determine if the repository is already present.
        if ($getTargetResourceResult.Ensure -eq 'Present') {
            Write-Verbose -Message ($localizedData.RepositoryExist -f $Name)

            # Repository exist, update the properties.
            Set-PSResourceRepository @repositoryParameters -ErrorAction 'Stop'
        }
        else {
            Write-Verbose -Message ($localizedData.RepositoryDoesNotExist -f $Name)

            # Repository did not exist, create the repository.
            Register-PSResourceRepository @repositoryParameters -ErrorAction 'Stop'
        }
    }
    else {
        if ($getTargetResourceResult.Ensure -eq 'Present') {
            Write-Verbose -Message ($localizedData.RemoveExistingRepository -f $Name)

            # Repository did exist, remove the repository.
            Unregister-PSResourceRepository -Name $Name -ErrorAction 'Stop'
        }
    }
}
