# PROXY HELPER
# used to determine if we have a semantic version
$semVerRegex = '^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$'

# try to convert the string into a version (semantic or system)
function Get-VersionType
{
    param ( $versionString )

    # if this can be converted into a version, simple return it
    $version = $versionString -as [version]
    if ( $version ) {
        return $version
    }

    # if the string matches a semantic version, return it, but also return a lossy conversion to system.version
    if ( $versionString -match $semVerRegex ) {
        return [pscustomobject]@{
            Major = [int]$Matches['major']
            Minor = [int]$Matches['Minor']
            Patch = [int]$Matches['patch']
            PreReleaseLabel = [string]$matches['prerelease']
            BuildLabel = [string]$matches['buildmetadata']
            originalString = $versionString
            Version = [version]("{0}.{1}.{2}" -f $Matches['major'],$Matches['minor'],$Matches['patch'])
            }
    }
    return $null
}

# this handles comparison of version with semantic versions
# this is all needed as semantic version exists only in core
function Compare-Version
{
    param ([string]$minimum, [string]$maximum)

    # this is done so we can use version to do our comparison
    $reference = Get-VersionType $minimum
    if ( ! $reference ) {
        throw "Cannot convert '$minimum' to version type"
    }
    $difference= Get-VersionType $maximum
    if ( ! $difference ) {
        throw "Cannot convert '$maximum' to version type"
    }

    if ( $reference -is [version] -and $difference -is [version] ) {
        if ( $reference -gt $difference ) {
            return 1
        }
        elseif ( $reference -lt $difference ) {
            return -1
        }
    }
    elseif ( $reference.version -is [version] -and $difference.version -is [version] ) {
        # two semantic versions
        if ( $reference.version -gt $difference.version ) {
            return 1
        }
        elseif ( $reference.version -lt $difference.version ) {
            return -1
        }
    }
    elseif ( $reference -is [version] -and $difference.version -is [version] ) {
        # one semantic version
        if ( $reference -gt $difference.version ) {
            return 1
        }
        elseif ( $reference -lt $difference.version ) {
            return -1
        }
        elseif ( $reference -eq $difference.version ) {
            # 1.0.0 is greater than 1.0.0-preview
            return 1
        }
    }
    elseif ( $reference.version -is [version] -and $difference -is [version] ) {
        # one semantic version
        if ( $reference.version -gt $difference ) {
            return 1
        }
        elseif ( $reference.version -lt $difference ) {
            return -1
        }
        elseif ( $reference.version -eq $difference ) {
            # 1.0.0 is greater than 1.0.0-preview
            return -1
        }
    }
    # Fall through

    if ( $reference.PreReleaseLabel -gt $difference.PreReleaseLabel ) {
        return 1
    }
    if ( $reference.PreReleaseLabel -lt $difference.PreReleaseLabel ) {
        return -1
    }
    # Fall through

    if ( $reference.BuildLabel -gt $difference.BuildLabel ) {
        return 1
    }
    if ( $reference.BuildLabel -lt $difference.BuildLabel ) {
        return -1
    }

    # Fall through, they are equivalent
    return 0
}


# Convert-VersionParamaters -RequiredVersion $RequiredVersion  -MinimumVersion $MinimumVersion -MaximumVersion $MaximumVersion
# this tries to figure out whether we have an improper use of version parameters
# such as RequiredVersion with MinimumVersion or MaximumVersion
function Convert-VersionParamaters
{
    param ( $RequiredVersion, $MinimumVersion, $MaximumVersion )

    # validate that required is not used with minimum or maximum version
    if ( $RequiredVersion -and ($MinimumVersion -or $MaximumVersion) ) {
        throw "RequiredVersion may not be used with MinimumVersion or MaximumVersion"
    }
    elseif ( ! $RequiredVersion -and ! $MinimumVersion -and ! $MaximumVersion ) {
        return $null
    }
    elseif ( $RequiredVersion -and ! $MinimumVersion -and ! $MaximumVersion ) { 
        return "$RequiredVersion" }

    # now return the appropriate string
    if ( $MinimumVersion -and ! $MaximumVersion ) {
        return "[$MinimumVersion,)"
    }
    elseif ( ! $MinimumVersion -and $MaximumVersion ) {
        # no minimum version
        return "(,${MaximumVersion}]"
    }
    else {
        $result = Compare-Version $MinimumVersion $MaximumVersion
        if ( $result -ge 0 ) {
            throw "'$MaximumVersion' must be greater than '$MinimumVersion'"
        }

        return "[${MinimumVersion},${MaximumVersion}]"
    }
}

####
####
# Proxy functions
# This is where we map the parameters from v2 to v3
# In some cases we have the same parameters
# In some cases we have parameters which are not used in v3 - these we will silently ignore
#  the goal in ignoring them is to provide ways for automation to succeed without error rather than provide exact
#  semantic behavior between v2 and v3
# In some cases we have a way to map a v2 parameter into a v3 parameter
#  In those cases, we need to remove the parameter from the bound parameters and apply the value to the newly mapped parameter
# In some cases we have a completely new parameter which we need to set.
####
####
function Find-Command {
[CmdletBinding(HelpUri='https://go.microsoft.com/fwlink/?LinkId=733636')]
param(
    [Parameter(Position=0)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Name},

    [ValidateNotNullOrEmpty()]
    [string]
    ${ModuleName},

    [ValidateNotNull()]
    [string]
    ${MinimumVersion},

    [ValidateNotNull()]
    [string]
    ${MaximumVersion},

    [ValidateNotNull()]
    [string]
    ${RequiredVersion},

    [switch]
    ${AllVersions},

    [switch]
    ${AllowPrerelease},

    [ValidateNotNull()]
    [string[]]
    ${Tag},

    [ValidateNotNull()]
    [string]
    ${Filter},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [uri]
    ${Proxy},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${ProxyCredential},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Repository})

begin
{
    Write-Warning -Message "The cmdlet 'Find-Command' is deprecated, please use 'Find-PSResource'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # add new specifier 
    if ( $PSBoundParameters['Name'] )               { $null = $PSBoundParameters.Remove('Name'); $PSBoundParameters['CommandName'] = $Name }
    # Parameter translations
    $verArgs = @{}
    if ( $PSBoundParameters['MinimumVersion'] )      { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MinimumVersion }
    if ( $PSBoundParameters['MaximumVersion'] )      { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )     { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    $ver = Convert-VersionParamaters @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }
    if ( $PSBoundParameters['AllVersions'] )         { $null = $PSBoundParameters.Remove('AllVersions'); $PSBoundParameters['Version'] = '*' }
    if ( $PSBoundParameters['AllowPrerelease'] )     { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }
    if ( $PSBoundParameters['Tag'] )                 { $null = $PSBoundParameters.Remove('Tag'); $PSBoundParameters['Tag'] = $Tag }
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Includes'] )            { $null = $PSBoundParameters.Remove('Includes') }
    if ( $PSBoundParameters['Proxy'] )               { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] )     { $null = $PSBoundParameters.Remove('ProxyCredential') }

    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Find-PSResource', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Find-Command
.ForwardHelpCategory Function

#>

}


function Find-DscResource {
[CmdletBinding(HelpUri='https://go.microsoft.com/fwlink/?LinkId=517196')]
param(
    [Parameter(Position=0)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Name},

    [ValidateNotNullOrEmpty()]
    [string]
    ${ModuleName},

    [ValidateNotNull()]
    [string]
    ${MinimumVersion},

    [ValidateNotNull()]
    [string]
    ${MaximumVersion},

    [ValidateNotNull()]
    [string]
    ${RequiredVersion},

    [switch]
    ${AllVersions},

    [switch]
    ${AllowPrerelease},

    [ValidateNotNull()]
    [string[]]
    ${Tag},

    [ValidateNotNull()]
    [string]
    ${Filter},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [uri]
    ${Proxy},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${ProxyCredential},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Repository})

begin
{
    Write-Warning -Message "The cmdlet 'Find-DscResource' is deprecated, please use 'Find-PSResource'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

        # PARAMETER MAP
        # add new specifier 
        if ( $PSBoundParameters['Name'] )               { $null = $PSBoundParameters.Remove('Name'); $PSBoundParameters['DscResourceName'] = $Name }
        # Parameter translations
        $verArgs = @{}
        if ( $PSBoundParameters['MinimumVersion'] )      { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MinimumVersion }
        if ( $PSBoundParameters['MaximumVersion'] )      { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
        if ( $PSBoundParameters['RequiredVersion'] )     { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
        $ver = Convert-VersionParamaters @verArgs
        if ( $ver ) {
            $PSBoundParameters['Version'] = $ver
        }
        if ( $PSBoundParameters['AllVersions'] )         { $null = $PSBoundParameters.Remove('AllVersions'); $PSBoundParameters['Version'] = '*' }

        # Parameter Deletions (unsupported in v3)
        if ( $PSBoundParameters['AllowPrerelease'] )     { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }
        if ( $PSBoundParameters['Tag'] )                 { $null = $PSBoundParameters.Remove('Tag'); $PSBoundParameters['Tag'] = $Tag }
        if ( $PSBoundParameters['Filter'] )              { $null = $PSBoundParameters.Remove('Filter') }
        if ( $PSBoundParameters['Proxy'] )               { $null = $PSBoundParameters.Remove('Proxy') }
        if ( $PSBoundParameters['ProxyCredential'] )     { $null = $PSBoundParameters.Remove('ProxyCredential') }
        # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Find-PSResource', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Find-DscResource
.ForwardHelpCategory Function

#>

}

function Find-Module {
[CmdletBinding(HelpUri='https://go.microsoft.com/fwlink/?LinkID=398574')]
param(
    [Parameter(Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Name},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MinimumVersion},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MaximumVersion},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${RequiredVersion},

    [switch]
    ${AllVersions},

    [switch]
    ${IncludeDependencies},

    [ValidateNotNull()]
    [string]
    ${Filter},

    [ValidateNotNull()]
    [string[]]
    ${Tag},

    [ValidateSet('DscResource','Cmdlet','Function','RoleCapability')]
    [ValidateNotNull()]
    [string[]]
    ${Includes},

    [ValidateNotNull()]
    [string[]]
    ${DscResource},

    [ValidateNotNull()]
    [string[]]
    ${RoleCapability},

    [ValidateNotNull()]
    [string[]]
    ${Command},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [uri]
    ${Proxy},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${ProxyCredential},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Repository},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${Credential},

    [switch]
    ${AllowPrerelease})

begin
{
    Write-Warning -Message "The cmdlet 'Find-Module' is deprecated, please use 'Find-PSResource'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # add new specifier 
    $PSBoundParameters['Type'] = 'module'
    # Parameter translations
    $verArgs = @{}
    if ( $PSBoundParameters['MinimumVersion'] )      { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MinimumVersion }
    if ( $PSBoundParameters['MaximumVersion'] )      { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )     { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    $ver = Convert-VersionParamaters @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }
    if ( $PSBoundParameters['AllVersions'] )         { $null = $PSBoundParameters.Remove('AllVersions'); $PSBoundParameters['Version'] = '*' }
    if ( $PSBoundParameters['Tag'] )                 { $null = $PSBoundParameters.Remove('Tag'); $PSBoundParameters['Tag'] = $Tag }
    if ( $PSBoundParameters['AllowPrerelease'] )     { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }
    if ( $PSBoundParameters['DscResource'] )         { $null = $PSBoundParameters.Remove('DscResource'); }
    if ( $PSBoundParameters['RoleCapability'] )      { $null = $PSBoundParameters.Remove('RoleCapability'); }
    if ( $PSBoundParameters['Command'] )             { $null = $PSBoundParameters.Remove('Command'); }
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Includes'] )            { $null = $PSBoundParameters.Remove('Includes') }
    if ( $PSBoundParameters['Proxy'] )               { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] )     { $null = $PSBoundParameters.Remove('ProxyCredential') }
    # END PARAMETER MAP
        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Find-PSResource', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Find-Module
.ForwardHelpCategory Function

#>

}

function Find-RoleCapability {
[CmdletBinding(HelpUri='https://go.microsoft.com/fwlink/?LinkId=718029')]
param(
    [Parameter(Position=0)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Name},

    [ValidateNotNullOrEmpty()]
    [string]
    ${ModuleName},

    [ValidateNotNull()]
    [string]
    ${MinimumVersion},

    [ValidateNotNull()]
    [string]
    ${MaximumVersion},

    [ValidateNotNull()]
    [string]
    ${RequiredVersion},

    [switch]
    ${AllVersions},

    [switch]
    ${AllowPrerelease},

    [ValidateNotNull()]
    [string[]]
    ${Tag},

    [ValidateNotNull()]
    [string]
    ${Filter},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [uri]
    ${Proxy},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${ProxyCredential},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Repository})

begin
{
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # add new specifier 
    $PSBoundParameters['Type'] = 'RoleCapability'
    # Parameter translations
    $verArgs = @{}
    if ( $PSBoundParameters['MinimumVersion'] )      { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MinimumVersion }
    if ( $PSBoundParameters['MaximumVersion'] )      { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )     { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    $ver = Convert-VersionParamaters @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }
    if ( $PSBoundParameters['AllVersions'] )         { $null = $PSBoundParameters.Remove('AllVersions'); $PSBoundParameters['Version'] = '*' }
    if ( $PSBoundParameters['Tag'] )                 { $null = $PSBoundParameters.Remove('Tag'); $PSBoundParameters['Tag'] = $Tag }
    if ( $PSBoundParameters['AllowPrerelease'] )     { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Filter'] )     { $null = $PSBoundParameters.Remove('Filter') }
    if ( $PSBoundParameters['Proxy'] )     { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] )     { $null = $PSBoundParameters.Remove('ProxyCredential') }
    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Find-PSResource', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Find-RoleCapability
.ForwardHelpCategory Function

#>

}

function Find-Script {
[CmdletBinding(HelpUri='https://go.microsoft.com/fwlink/?LinkId=619785')]
param(
    [Parameter(Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Name},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MinimumVersion},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MaximumVersion},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${RequiredVersion},

    [switch]
    ${AllVersions},

    [switch]
    ${IncludeDependencies},

    [ValidateNotNull()]
    [string]
    ${Filter},

    [ValidateNotNull()]
    [string[]]
    ${Tag},

    [ValidateSet('Function','Workflow')]
    [ValidateNotNull()]
    [string[]]
    ${Includes},

    [ValidateNotNull()]
    [string[]]
    ${Command},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [uri]
    ${Proxy},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${ProxyCredential},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Repository},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${Credential},

    [switch]
    ${AllowPrerelease})

begin
{
    Write-Warning -Message "The cmdlet 'Find-Script' is deprecated, please use 'Find-PSResource'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # add new specifier 
    $PSBoundParameters['Type'] = 'script'
    # Parameter translations
    $verArgs = @{}
    if ( $PSBoundParameters['MinimumVersion'] )      { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MinimumVersion }
    if ( $PSBoundParameters['MaximumVersion'] )      { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )     { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    $ver = Convert-VersionParamaters @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }
    if ( $PSBoundParameters['AllVersions'] )         { $null = $PSBoundParameters.Remove('AllVersions'); $PSBoundParameters['Version'] = '*' }
    if ( $PSBoundParameters['Tag'] )                 { $null = $PSBoundParameters.Remove('Tag'); $PSBoundParameters['Tag'] = $Tag }
    if ( $PSBoundParameters['AllowPrerelease'] )     { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }

    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Filter'] )              { $null = $PSBoundParameters.Remove('Filter') }
    if ( $PSBoundParameters['Includes'] )            { $null = $PSBoundParameters.Remove('Includes') }
    if ( $PSBoundParameters['Command'] )             { $null = $PSBoundParameters.Remove('Command') }
    if ( $PSBoundParameters['Proxy'] )               { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] )     { $null = $PSBoundParameters.Remove('ProxyCredential') }
    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Find-PSResource', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Find-Script
.ForwardHelpCategory Function

#>

}

function Get-InstalledModule {
[CmdletBinding(HelpUri='https://go.microsoft.com/fwlink/?LinkId=526863')]
param(
    [Parameter(Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Name},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MinimumVersion},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${RequiredVersion},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MaximumVersion},

    [switch]
    ${AllVersions},

    [switch]
    ${AllowPrerelease})

begin
{
    Write-Warning -Message "The cmdlet 'Get-InstalledModule' is deprecated, please use 'Get-PSResource'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    $verArgs = @{}
    if ( $PSBoundParameters['MinimumVersion'] )     { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MinimumVersion }
    if ( $PSBoundParameters['MaximumVersion'] )     { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )    { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    $ver = Convert-VersionParamaters @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }
    if ( $PSBoundParameters['AllVersions'] )        { $null = $PSBoundParameters.Remove('AllVersions'); $PSBoundParameters['Version'] = '*' }
    if ( $PSBoundParameters['AllowPrerelease'] )    { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }
    if ( $PSBoundParameters['Name'] )               { $null = $PSBoundParameters.Remove('Name'); $PSBoundParameters['Name'] = $Name }
    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Get-PSResource', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Get-InstalledModule
.ForwardHelpCategory Function

#>

}

function Get-InstalledScript {
[CmdletBinding(HelpUri='https://go.microsoft.com/fwlink/?LinkId=619790')]
param(
    [Parameter(Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Name},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MinimumVersion},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${RequiredVersion},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MaximumVersion},

    [switch]
    ${AllowPrerelease})

begin
{
    Write-Warning -Message "The cmdlet 'Get-InstalledScript' is deprecated, please use 'Get-PSResource'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # add new specifier 
    # Parameter translations
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['MinimumVersion'] )     { $null = $PSBoundParameters.Remove('MinimumVersion') }
    if ( $PSBoundParameters['RequiredVersion'] )     { $null = $PSBoundParameters.Remove('RequiredVersion') }
    if ( $PSBoundParameters['MaximumVersion'] )     { $null = $PSBoundParameters.Remove('MaximumVersion') }
    if ( $PSBoundParameters['AllowPrerelease'] )     { $null = $PSBoundParameters.Remove('AllowPrerelease') }
    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Get-PSResource', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Get-InstalledScript
.ForwardHelpCategory Function

#>

}

function Get-PSRepository {
[CmdletBinding(HelpUri='https://go.microsoft.com/fwlink/?LinkID=517127')]
param(
    [Parameter(Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Name})

begin
{
    Write-Warning -Message "The cmdlet 'Get-PSRepository' is deprecated, please use 'Get-PSResourceRepository'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Get-PSResourceRepository', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Get-PSRepository
.ForwardHelpCategory Function

#>

}

function Install-Module {
[CmdletBinding(DefaultParameterSetName='NameParameterSet', SupportsShouldProcess=$true, ConfirmImpact='Medium', HelpUri='https://go.microsoft.com/fwlink/?LinkID=398573')]
param(
    [Parameter(ParameterSetName='NameParameterSet', Mandatory=$true, Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Name},

    [Parameter(ParameterSetName='InputObject', Mandatory=$true, Position=0, ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [psobject[]]
    ${InputObject},

    [Parameter(ParameterSetName='NameParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MinimumVersion},

    [Parameter(ParameterSetName='NameParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MaximumVersion},

    [Parameter(ParameterSetName='NameParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${RequiredVersion},

    [Parameter(ParameterSetName='NameParameterSet')]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Repository},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${Credential},

    [ValidateSet('CurrentUser','AllUsers')]
    [string]
    ${Scope},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [uri]
    ${Proxy},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${ProxyCredential},

    [switch]
    ${AllowClobber},

    [switch]
    ${SkipPublisherCheck},

    [switch]
    ${Force},

    [Parameter(ParameterSetName='NameParameterSet')]
    [switch]
    ${AllowPrerelease},

    [switch]
    ${AcceptLicense},

    [switch]
    ${PassThru})

begin
{
    Write-Warning -Message "The cmdlet 'Install-Module' is deprecated, please use 'Install-PSResource'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # add new specifier 
    # handle version changes
    $verArgs = @{}
    if ( $PSBoundParameters['MinimumVersion'] )     { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MinimumVersion }
    if ( $PSBoundParameters['MaximumVersion'] )     { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )    { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    $ver = Convert-VersionParamaters @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }

    # Parameter translations
    if ( $PSBoundParameters['AllowClobber'] )       { $null = $PSBoundParameters.Remove('AllowClobber') }
    $PSBoundParameters['NoClobber'] = ! $AllowClobber
    if ( $PSBoundParameters['AllowPrerelease'] )    { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }

    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Proxy'] )              { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] )    { $null = $PSBoundParameters.Remove('ProxyCredential') }
    if ( $PSBoundParameters['SkipPublisherCheck'] ) { $null = $PSBoundParameters.Remove('SkipPublisherCheck') }
    if ( $PSBoundParameters['InputObject'] )        { $null = $PSBoundParameters.Remove('InputObject') }
    if ( $PSBoundParameters['PassThru'] )           { $null = $PSBoundParameters.Remove('PassThru') }
    if ( $PSBoundParameters['Force'] )              { $null = $PSBoundParameters.Remove('Force') }

    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Install-PSResource', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Install-Module
.ForwardHelpCategory Function

#>

}

function Install-Script {
[CmdletBinding(DefaultParameterSetName='NameParameterSet', SupportsShouldProcess=$true, ConfirmImpact='Medium', HelpUri='https://go.microsoft.com/fwlink/?LinkId=619784')]
param(
    [Parameter(ParameterSetName='NameParameterSet', Mandatory=$true, Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Name},

    [Parameter(ParameterSetName='InputObject', Mandatory=$true, Position=0, ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [psobject[]]
    ${InputObject},

    [Parameter(ParameterSetName='NameParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MinimumVersion},

    [Parameter(ParameterSetName='NameParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MaximumVersion},

    [Parameter(ParameterSetName='NameParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${RequiredVersion},

    [Parameter(ParameterSetName='NameParameterSet')]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Repository},

    [ValidateSet('CurrentUser','AllUsers')]
    [string]
    ${Scope},

    [switch]
    ${NoPathUpdate},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [uri]
    ${Proxy},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${ProxyCredential},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${Credential},

    [switch]
    ${Force},

    [Parameter(ParameterSetName='NameParameterSet')]
    [switch]
    ${AllowPrerelease},

    [switch]
    ${AcceptLicense},

    [switch]
    ${PassThru})

begin
{
    Write-Warning -Message "The cmdlet 'Install-Script' is deprecated, please use 'Install-PSResource'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # add new specifier 
    # handle version changes
    $verArgs = @{}
    if ( $PSBoundParameters['MinimumVersion'] )     { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MinimumVersion }
    if ( $PSBoundParameters['MaximumVersion'] )     { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )    { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    $ver = Convert-VersionParamaters @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }
    # Parameter translations
    if ( $PSBoundParameters['AllowPrerelease'] )    { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['InputObject'] )      { $null = $PSBoundParameters.Remove('InputObject') }
    if ( $PSBoundParameters['NoPathUpdate'] )     { $null = $PSBoundParameters.Remove('NoPathUpdate') }
    if ( $PSBoundParameters['Proxy'] )            { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] )  { $null = $PSBoundParameters.Remove('ProxyCredential') }
    if ( $PSBoundParameters['PassThru'] )         { $null = $PSBoundParameters.Remove('PassThru') }
    if ( $PSBoundParameters['Force'] )            { $null = $PSBoundParameters.Remove('Force') }

    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Install-PSResource', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Install-Script
.ForwardHelpCategory Function

#>

}

function Publish-Module {
[CmdletBinding(DefaultParameterSetName='ModuleNameParameterSet', SupportsShouldProcess=$true, ConfirmImpact='Medium', PositionalBinding=$false, HelpUri='https://go.microsoft.com/fwlink/?LinkID=398575')]
param(
    [Parameter(ParameterSetName='ModuleNameParameterSet', Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string]
    ${Name},

    [Parameter(ParameterSetName='ModulePathParameterSet', Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string]
    ${Path},

    [Parameter(ParameterSetName='ModuleNameParameterSet')]
    [ValidateNotNullOrEmpty()]
    [string]
    ${RequiredVersion},

    [ValidateNotNullOrEmpty()]
    [string]
    ${NuGetApiKey},

    [ValidateNotNullOrEmpty()]
    [string]
    ${Repository},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${Credential},

    [ValidateSet('2.0')]
    [version]
    ${FormatVersion},

    [string[]]
    ${ReleaseNotes},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Tags},

    [ValidateNotNullOrEmpty()]
    [uri]
    ${LicenseUri},

    [ValidateNotNullOrEmpty()]
    [uri]
    ${IconUri},

    [ValidateNotNullOrEmpty()]
    [uri]
    ${ProjectUri},

    [Parameter(ParameterSetName='ModuleNameParameterSet')]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Exclude},

    [switch]
    ${Force},

    [Parameter(ParameterSetName='ModuleNameParameterSet')]
    [switch]
    ${AllowPrerelease},

    [switch]
    ${SkipAutomaticTags})

begin
{
    Write-Warning -Message "The cmdlet 'Publish-Module' is deprecated, please use 'Publish-PSResource'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # Parameter translations
    if ( $PSBoundParameters['NuGetApiKey'] )       { $null = $PSBoundParameters.Remove('NuGetApiKey'); $PSBoundParameters['APIKey'] = $NuGetApiKey }
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Name'] )              { $null = $PSBoundParameters.Remove('Name') }
    if ( $PSBoundParameters['RequiredVersion'] )   { $null = $PSBoundParameters.Remove('RequiredVersion') }
    if ( $PSBoundParameters['Repository'] )        { $null = $PSBoundParameters.Remove('Repository') }
    if ( $PSBoundParameters['FormatVersion'] )     { $null = $PSBoundParameters.Remove('FormatVersion') }
    if ( $PSBoundParameters['Force'] )             { $null = $PSBoundParameters.Remove('Force') }
    if ( $PSBoundParameters['AllowPrerelease'] )   { $null = $PSBoundParameters.Remove('AllowPrerelease') }
    if ( $PSBoundParameters['SkipAutomaticTags'] ) { $null = $PSBoundParameters.Remove('SkipAutomaticTags') }
    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Publish-PSResource', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Publish-Module
.ForwardHelpCategory Function

#>

}

function Publish-Script {
[CmdletBinding(DefaultParameterSetName='PathParameterSet', SupportsShouldProcess=$true, ConfirmImpact='Medium', PositionalBinding=$false, HelpUri='https://go.microsoft.com/fwlink/?LinkId=619788')]
param(
    [Parameter(ParameterSetName='PathParameterSet', Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string]
    ${Path},

    [Parameter(ParameterSetName='LiteralPathParameterSet', Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
    [Alias('PSPath')]
    [ValidateNotNullOrEmpty()]
    [string]
    ${LiteralPath},

    [ValidateNotNullOrEmpty()]
    [string]
    ${NuGetApiKey},

    [ValidateNotNullOrEmpty()]
    [string]
    ${Repository},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${Credential},

    [switch]
    ${Force})

begin
{
    Write-Warning -Message "The cmdlet 'Publish-Script' is deprecated, please use 'Publish-PSResource'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # add new specifier 
    # Parameter translations
    if ( $PSBoundParameters['NuGetApiKey'] )  { $null = $PSBoundParameters.Remove('NuGetApiKey'); $PSBoundParameters['APIKey'] = $NuGetApiKey }
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Force'] )        { $null = $PSBoundParameters.Remove('Force') }
    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Publish-PSResource', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Publish-Script
.ForwardHelpCategory Function

#>

}

function Register-PSRepository {
[CmdletBinding(DefaultParameterSetName='NameParameterSet', HelpUri='https://go.microsoft.com/fwlink/?LinkID=517129')]
param(
    [Parameter(ParameterSetName='NameParameterSet', Mandatory=$true, Position=0)]
    [ValidateNotNullOrEmpty()]
    [string]
    ${Name},

    [Parameter(ParameterSetName='NameParameterSet', Mandatory=$true, Position=1)]
    [ValidateNotNullOrEmpty()]
    [string]
    ${SourceLocation},

    [Parameter(ParameterSetName='NameParameterSet')]
    [ValidateNotNullOrEmpty()]
    [uri]
    ${PublishLocation},

    [Parameter(ParameterSetName='NameParameterSet')]
    [ValidateNotNullOrEmpty()]
    [uri]
    ${ScriptSourceLocation},

    [Parameter(ParameterSetName='NameParameterSet')]
    [ValidateNotNullOrEmpty()]
    [uri]
    ${ScriptPublishLocation},

    [Parameter(ParameterSetName='NameParameterSet', ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${Credential},

    [Parameter(ParameterSetName='PSGalleryParameterSet', Mandatory=$true)]
    [switch]
    ${Default},

    [ValidateSet('Trusted','Untrusted')]
    [string]
    ${InstallationPolicy},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [uri]
    ${Proxy},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${ProxyCredential},

    [Parameter(ParameterSetName='NameParameterSet')]
    [ValidateNotNullOrEmpty()]
    [string]
    ${PackageManagementProvider})

begin
{
    Write-Warning -Message "The cmdlet 'Register-PSRepository' is deprecated, please use 'Register-PSResourceRepository'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # Parameter translations
    if ( $PSBoundParameters['InstallationPolicy'] ) {
        $null = $PSBoundParameters.Remove('InstallationPolicy')
        if  ( $InstallationPolicy -eq "Trusted" ) {
            $PSBoundParameters['Trusted'] = $true
        }
    }
    if ( $PSBoundParameters['Name'] )                      { $null = $PSBoundParameters.Remove('Name'); $PSBoundParameters['Name'] = $Name }
    if ( $PSBoundParameters['SourceLocation'] )            { $null = $PSBoundParameters.Remove('SourceLocation'); $PSBoundParameters['Uri'] = $SourceLocation }
    if ( $PSBoundParameters['Default'] )                   { $null = $PSBoundParameters.Remove('Default'); $PSBoundParameters['PSGallery'] = $Default }
    
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['PublishLocation'] )           { $null = $PSBoundParameters.Remove('PublishLocation') }
    if ( $PSBoundParameters['ScriptSourceLocation'] )      { $null = $PSBoundParameters.Remove('ScriptSourceLocation') }
    if ( $PSBoundParameters['ScriptPublishLocation'] )     { $null = $PSBoundParameters.Remove('ScriptPublishLocation') }
    if ( $PSBoundParameters['Proxy'] )                     { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] )           { $null = $PSBoundParameters.Remove('ProxyCredential') }
    if ( $PSBoundParameters['PackageManagementProvider'] ) { $null = $PSBoundParameters.Remove('PackageManagementProvider') }
    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Register-PSResourceRepository', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Register-PSRepository
.ForwardHelpCategory Function

#>

}

function Save-Module {
[CmdletBinding(DefaultParameterSetName='NameAndPathParameterSet', SupportsShouldProcess=$true, ConfirmImpact='Medium', HelpUri='https://go.microsoft.com/fwlink/?LinkId=531351')]
param(
    [Parameter(ParameterSetName='NameAndLiteralPathParameterSet', Mandatory=$true, Position=0, ValueFromPipelineByPropertyName=$true)]
    [Parameter(ParameterSetName='NameAndPathParameterSet', Mandatory=$true, Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Name},

    [Parameter(ParameterSetName='InputObjectAndLiteralPathParameterSet', Mandatory=$true, Position=0, ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true)]
    [Parameter(ParameterSetName='InputObjectAndPathParameterSet', Mandatory=$true, Position=0, ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [psobject[]]
    ${InputObject},

    [Parameter(ParameterSetName='NameAndLiteralPathParameterSet', ValueFromPipelineByPropertyName=$true)]
    [Parameter(ParameterSetName='NameAndPathParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MinimumVersion},

    [Parameter(ParameterSetName='NameAndLiteralPathParameterSet', ValueFromPipelineByPropertyName=$true)]
    [Parameter(ParameterSetName='NameAndPathParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MaximumVersion},

    [Parameter(ParameterSetName='NameAndLiteralPathParameterSet', ValueFromPipelineByPropertyName=$true)]
    [Parameter(ParameterSetName='NameAndPathParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${RequiredVersion},

    [Parameter(ParameterSetName='NameAndLiteralPathParameterSet', ValueFromPipelineByPropertyName=$true)]
    [Parameter(ParameterSetName='NameAndPathParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Repository},

    [Parameter(ParameterSetName='InputObjectAndPathParameterSet', Mandatory=$true, Position=1, ValueFromPipelineByPropertyName=$true)]
    [Parameter(ParameterSetName='NameAndPathParameterSet', Mandatory=$true, Position=1, ValueFromPipelineByPropertyName=$true)]
    [string]
    ${Path},

    [Parameter(ParameterSetName='InputObjectAndLiteralPathParameterSet', Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
    [Parameter(ParameterSetName='NameAndLiteralPathParameterSet', Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
    [Alias('PSPath')]
    [string]
    ${LiteralPath},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [uri]
    ${Proxy},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${ProxyCredential},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${Credential},

    [switch]
    ${Force},

    [Parameter(ParameterSetName='NameAndLiteralPathParameterSet')]
    [Parameter(ParameterSetName='NameAndPathParameterSet')]
    [switch]
    ${AllowPrerelease},

    [switch]
    ${AcceptLicense})

begin
{
    Write-Warning -Message "The cmdlet 'Save-Module' is deprecated, please use 'Save-PSResource'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # add new specifier 
    # remove until available
    if ( $PSBoundParameters['InputObject'] )     { $null = $PSBoundParameters.Remove('InputObject') }

    # handle version changes
    $verArgs = @{}
    if ( $PSBoundParameters['MinimumVersion'] )   { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['MaximumVersion'] )   { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )  { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    $ver = Convert-VersionParamaters @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }

    # Parameter translations
    if ( $PSBoundParameters['AllowPrerelease'] )  { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }
    if ( $PSBoundParameters['LiteralPath'] )      { $null = $PSBoundParameters.Remove('LiteralPath'); $PSBoundParameters['Path'] = $LiteralPath }

    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Proxy'] )            { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] )  { $null = $PSBoundParameters.Remove('ProxyCredential') }
    if ( $PSBoundParameters['Force'] )            { $null = $PSBoundParameters.Remove('Force') }
    if ( $PSBoundParameters['AcceptLicense'] )    { $null = $PSBoundParameters.Remove('AcceptLicense') }
    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Save-PSResource', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Save-Module
.ForwardHelpCategory Function

#>

}

function Save-Script {
[CmdletBinding(DefaultParameterSetName='NameAndPathParameterSet', SupportsShouldProcess=$true, ConfirmImpact='Medium', HelpUri='https://go.microsoft.com/fwlink/?LinkId=619786')]
param(
    [Parameter(ParameterSetName='NameAndLiteralPathParameterSet', Mandatory=$true, Position=0, ValueFromPipelineByPropertyName=$true)]
    [Parameter(ParameterSetName='NameAndPathParameterSet', Mandatory=$true, Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Name},

    [Parameter(ParameterSetName='InputObjectAndLiteralPathParameterSet', Mandatory=$true, Position=0, ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true)]
    [Parameter(ParameterSetName='InputObjectAndPathParameterSet', Mandatory=$true, Position=0, ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [psobject[]]
    ${InputObject},

    [Parameter(ParameterSetName='NameAndLiteralPathParameterSet', ValueFromPipelineByPropertyName=$true)]
    [Parameter(ParameterSetName='NameAndPathParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MinimumVersion},

    [Parameter(ParameterSetName='NameAndLiteralPathParameterSet', ValueFromPipelineByPropertyName=$true)]
    [Parameter(ParameterSetName='NameAndPathParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MaximumVersion},

    [Parameter(ParameterSetName='NameAndLiteralPathParameterSet', ValueFromPipelineByPropertyName=$true)]
    [Parameter(ParameterSetName='NameAndPathParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${RequiredVersion},

    [Parameter(ParameterSetName='NameAndLiteralPathParameterSet', ValueFromPipelineByPropertyName=$true)]
    [Parameter(ParameterSetName='NameAndPathParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Repository},

    [Parameter(ParameterSetName='InputObjectAndPathParameterSet', Mandatory=$true, Position=1, ValueFromPipelineByPropertyName=$true)]
    [Parameter(ParameterSetName='NameAndPathParameterSet', Mandatory=$true, Position=1, ValueFromPipelineByPropertyName=$true)]
    [string]
    ${Path},

    [Parameter(ParameterSetName='InputObjectAndLiteralPathParameterSet', Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
    [Parameter(ParameterSetName='NameAndLiteralPathParameterSet', Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
    [Alias('PSPath')]
    [string]
    ${LiteralPath},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [uri]
    ${Proxy},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${ProxyCredential},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${Credential},

    [switch]
    ${Force},

    [Parameter(ParameterSetName='NameAndLiteralPathParameterSet')]
    [Parameter(ParameterSetName='NameAndPathParameterSet')]
    [switch]
    ${AllowPrerelease},

    [switch]
    ${AcceptLicense})

begin
{
    Write-Warning -Message "The cmdlet 'Save-Script' is deprecated, please use 'Save-PSResource'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # add new specifiers

    # handle version changes
    $verArgs = @{}
    if ( $PSBoundParameters['MinimumVersion'] )     { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['MaximumVersion'] )     { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )    { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    $ver = Convert-VersionParamaters @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }

    # Parameter translations
    # LiteralPath needs to be converted to Path - we know they won't be used together because they're in different parameter sets 
    if ( $PSBoundParameters['LiteralPath'] )        { $null = $PSBoundParameters.Remove('LiteralPath'); $PSBoundParameters['Path'] = $LiteralPath }
    if ( $PSBoundParameters['AllowPrerelease'] )    { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }

    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['InputObject'] )     { $null = $PSBoundParameters.Remove('InputObject') }
    if ( $PSBoundParameters['Proxy'] )           { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] ) { $null = $PSBoundParameters.Remove('ProxyCredential') }
    if ( $PSBoundParameters['Force'] )          { $null = $PSBoundParameters.Remove('Force') }
    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Save-PSResource', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Save-Script
.ForwardHelpCategory Function

#>

}

function Set-PSRepository {
[CmdletBinding(PositionalBinding=$false, HelpUri='https://go.microsoft.com/fwlink/?LinkID=517128')]
param(
    [Parameter(Mandatory=$true, Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string]
    ${Name},

    [Parameter(Position=1)]
    [ValidateNotNullOrEmpty()]
    [string]
    ${SourceLocation},

    [ValidateNotNullOrEmpty()]
    [uri]
    ${PublishLocation},

    [ValidateNotNullOrEmpty()]
    [uri]
    ${ScriptSourceLocation},

    [ValidateNotNullOrEmpty()]
    [uri]
    ${ScriptPublishLocation},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${Credential},

    [ValidateSet('Trusted','Untrusted')]
    [string]
    ${InstallationPolicy},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [uri]
    ${Proxy},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${ProxyCredential},

    [ValidateNotNullOrEmpty()]
    [string]
    ${PackageManagementProvider})

begin
{
    Write-Warning -Message "The cmdlet 'Set-PSRepository' is deprecated, please use 'Set-PSResourceRepository'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # add new specifier 
    # Parameter translations
    if ( $PSBoundParameters['InstallationPolicy'] ) {
        $null = $PSBoundParameters.Remove('InstallationPolicy')
        if  ( $InstallationPolicy -eq "Trusted" ) {
            $PSBoundParameters['Trusted'] = $true
        }
        else {
            $PSBoundParameters['Trusted'] = $false
        }
    }
    if ( $PSBoundParameters['SourceLocation'] )            { $null = $PSBoundParameters.Remove('SourceLocation'); $PSBoundParameters['Uri'] = $SourceLocation }
    if ( $PSBoundParameters['Default'] )                   { $null = $PSBoundParameters.Remove('Default'); $PSBoundParameters['PSGallery'] = $Default }
    
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['PublishLocation'] )           { $null = $PSBoundParameters.Remove('PublishLocation') }
    if ( $PSBoundParameters['ScriptSourceLocation'] )      { $null = $PSBoundParameters.Remove('ScriptSourceLocation') }
    if ( $PSBoundParameters['ScriptPublishLocation'] )     { $null = $PSBoundParameters.Remove('ScriptPublishLocation') }
    if ( $PSBoundParameters['Proxy'] )                     { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] )           { $null = $PSBoundParameters.Remove('ProxyCredential') }
    if ( $PSBoundParameters['PackageManagementProvider'] ) { $null = $PSBoundParameters.Remove('PackageManagementProvider') }

    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Set-PSResourceRepository', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Set-PSRepository
.ForwardHelpCategory Function

#>

}

function Uninstall-Module {
[CmdletBinding(DefaultParameterSetName='NameParameterSet', SupportsShouldProcess=$true, ConfirmImpact='Medium', HelpUri='https://go.microsoft.com/fwlink/?LinkId=526864')]
param(
    [Parameter(ParameterSetName='NameParameterSet', Mandatory=$true, Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Name},

    [Parameter(ParameterSetName='InputObject', Mandatory=$true, Position=0, ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [psobject[]]
    ${InputObject},

    [Parameter(ParameterSetName='NameParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MinimumVersion},

    [Parameter(ParameterSetName='NameParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${RequiredVersion},

    [Parameter(ParameterSetName='NameParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MaximumVersion},

    [Parameter(ParameterSetName='NameParameterSet')]
    [switch]
    ${AllVersions},

    [switch]
    ${Force},

    [Parameter(ParameterSetName='NameParameterSet')]
    [switch]
    ${AllowPrerelease})

begin
{
    Write-Warning -Message "The cmdlet 'Uninstall-Module' is deprecated, please use 'Uninstall-PSResource'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # add new specifier 
    # Parameter translations
    $verArgs = @{}
    if ( $PSBoundParameters['MinimumVersion'] )      { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MinimumVersion }
    if ( $PSBoundParameters['MaximumVersion'] )      { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )     { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    $ver = Convert-VersionParamaters @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }
    if ( $PSBoundParameters['AllVersions'] )         { $null = $PSBoundParameters.Remove('AllVersions'); $PSBoundParameters['Version'] = '*' }
    if ( $PSBoundParameters['Name'] )                { $null = $PSBoundParameters.Remove('Name'); $PSBoundParameters['Name'] = $Name }

    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['InputObject'] )         { $null = $PSBoundParameters.Remove('InputObject') }
    if ( $PSBoundParameters['AllowPrerelease'] )     { $null = $PSBoundParameters.Remove('AllowPrerelease') }
    if ( $PSBoundParameters['Force'] )               { $null = $PSBoundParameters.Remove('Force') }
    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Uninstall-PSResource', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Uninstall-Module
.ForwardHelpCategory Function

#>

}

function Uninstall-Script {
[CmdletBinding(DefaultParameterSetName='NameParameterSet', SupportsShouldProcess=$true, ConfirmImpact='Medium', HelpUri='https://go.microsoft.com/fwlink/?LinkId=619789')]
param(
    [Parameter(ParameterSetName='NameParameterSet', Mandatory=$true, Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Name},

    [Parameter(ParameterSetName='InputObject', Mandatory=$true, Position=0, ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [psobject[]]
    ${InputObject},

    [Parameter(ParameterSetName='NameParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MinimumVersion},

    [Parameter(ParameterSetName='NameParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${RequiredVersion},

    [Parameter(ParameterSetName='NameParameterSet', ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MaximumVersion},

    [switch]
    ${Force},

    [Parameter(ParameterSetName='NameParameterSet')]
    [switch]
    ${AllowPrerelease})

begin
{
    Write-Warning -Message "The cmdlet 'Uninstall-Script' is deprecated, please use 'Uninstall-PSResource'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # Parameter translations
    $verArgs = @{}
    if ( $PSBoundParameters['MinimumVersion'] )      { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MinimumVersion }
    if ( $PSBoundParameters['MaximumVersion'] )      { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )     { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    $ver = Convert-VersionParamaters @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }
    if ( $PSBoundParameters['AllVersions'] )         { $null = $PSBoundParameters.Remove('AllVersions'); $PSBoundParameters['Version'] = '*' }
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['InputObject'] )         { $null = $PSBoundParameters.Remove('InputObject') }
    if ( $PSBoundParameters['AllowPrerelease'] )     { $null = $PSBoundParameters.Remove('AllowPrerelease') }
    if ( $PSBoundParameters['Force'] )               { $null = $PSBoundParameters.Remove('Force') }

    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Uninstall-PSResource', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Uninstall-Script
.ForwardHelpCategory Function

#>

}

function Unregister-PSRepository {
[CmdletBinding(HelpUri='https://go.microsoft.com/fwlink/?LinkID=517130')]
param(
    [Parameter(Mandatory=$true, Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Name})

begin
{
    Write-Warning -Message "The cmdlet 'Unregister-PSRepository' is deprecated, please use 'Unregister-PSResourceRepository'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # No changes between Unregister-PSRepository and Unregister-PSResourceRepository
    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Unregister-PSResourceRepository', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Unregister-PSRepository
.ForwardHelpCategory Function

#>

}

function Update-Module {
[CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium', HelpUri='https://go.microsoft.com/fwlink/?LinkID=398576')]
param(
    [Parameter(Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Name},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${RequiredVersion},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MaximumVersion},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${Credential},

    [ValidateSet('CurrentUser','AllUsers')]
    [string]
    ${Scope},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [uri]
    ${Proxy},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${ProxyCredential},

    [switch]
    ${Force},

    [switch]
    ${AllowPrerelease},

    [switch]
    ${AcceptLicense},

    [switch]
    ${PassThru})

begin
{
    Write-Warning -Message "The cmdlet 'Update-Module' is deprecated, please use 'Update-PSResource'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # handle version changes
    $verArgs = @{}
    if ( $PSBoundParameters['MaximumVersion'] )     { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )    { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    $ver = Convert-VersionParamaters @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }

    # Parameter translations
    if ( $PSBoundParameters['AllowPrerelease'] )    { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }

    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Proxy'] )              { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] )    { $null = $PSBoundParameters.Remove('ProxyCredential') }
    if ( $PSBoundParameters['Force'] )              { $null = $PSBoundParameters.Remove('Force') }

    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Update-PSResource', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Update-Module
.ForwardHelpCategory Function

#>

}

function Update-Script {
[CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium', HelpUri='https://go.microsoft.com/fwlink/?LinkId=619787')]
param(
    [Parameter(Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Name},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${RequiredVersion},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNull()]
    [string]
    ${MaximumVersion},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [uri]
    ${Proxy},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${ProxyCredential},

    [Parameter(ValueFromPipelineByPropertyName=$true)]
    [pscredential]
    [System.Management.Automation.CredentialAttribute()]
    ${Credential},

    [switch]
    ${Force},

    [switch]
    ${AllowPrerelease},

    [switch]
    ${AcceptLicense},

    [switch]
    ${PassThru})

begin
{
    Write-Warning -Message "The cmdlet 'Update-Script' is deprecated, please use 'Update-PSResource'."
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    $PSBoundParameters['Type'] = 'script'
    # handle version changes
    $verArgs = @{}
    if ( $PSBoundParameters['MaximumVersion'] )     { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )    { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    $ver = Convert-VersionParamaters @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }

    # Parameter translations
    if ( $PSBoundParameters['AllowPrerelease'] )    { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Proxy'] )              { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] )    { $null = $PSBoundParameters.Remove('ProxyCredential') }
    if ( $PSBoundParameters['PassThru'] )           { $null = $PSBoundParameters.Remove('PassThru') }
    if ( $PSBoundParameters['Force'] )              { $null = $PSBoundParameters.Remove('Force') }

    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('Update-PSResource', [System.Management.Automation.CommandTypes]::Cmdlet)
        $scriptCmd = {& $wrappedCmd @PSBoundParameters }

        $steppablePipeline = $scriptCmd.GetSteppablePipeline()
        $steppablePipeline.Begin($PSCmdlet)
    } catch {
        throw
    }
}

process
{
    try {
        $steppablePipeline.Process($_)
    } catch {
        throw
    }
}

end
{
    try {
        $steppablePipeline.End()
    } catch {
        throw
    }
}
<#

.ForwardHelpTargetName Update-Script
.ForwardHelpCategory Function

#>

}

$functionsToExport = @(
    "Find-Command",
    "Find-DscResource",
    "Find-Module",
    "Find-RoleCapability",
    "Find-Script",
    "Get-InstalledModule",
    "Get-InstalledScript",
    "Get-PSRepository",
    "Install-Module",
    "Install-Script",
    "Publish-Module",
    "Publish-Script",
    "Register-PSRepository",
    "Save-Module",
    "Save-Script",
    "Set-PSRepository",
    "Uninstall-Module",
    "Uninstall-Script",
    "Unregister-PSRepository",
    "Update-Module",
    "Update-Script"
)

export-ModuleMember -Function $functionsToExport