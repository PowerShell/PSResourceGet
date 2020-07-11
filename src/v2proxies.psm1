# PROXY HELPER
$semVerRegex = '^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$'

function Get-VersionType
{
    param ( $versionString )

    $version = $versionString -as [version]
    if ( $version ) {
        return $version
    }

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
    # return null?
    # throw "Cannot convert '$versionString' to version or semantic version"
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


# Convert-VersionsToNugetVersion -RequiredVersion $RequiredVersion  -MinimumVersion $MinimumVersion -MaximumVersion $MaximumVersion
function Convert-VersionsToNugetVersion
{
    param ( $RequiredVersion, $MinimumVersion, $MaximumVersion )
    # validate that required is not used with minimum or maximum version
    if ( $RequiredVersion -and ($MinimumVersion -or $MaximumVersion) ) {
        throw "RequiredVersion may not be used with MinimumVersion or MaximumVersion"
    }
    elseif ( ! $RequiredVersion -and ! $MinimuVersion -and ! $MaximumVersion ) {
        return $null
    }
    if ( $RequiredVersion -eq '*' ) { return $RequiredVersion }

    # validate that we can actually convert the received version to an allowed either a system.version or semanticversion
    foreach ( $version in "RequiredVersion","MinimumVersion", "MaximumVersion" ) {
        if ( $PSBoundParameters[$version] ) {
            $v = $PSBoundParameters[$version] -as [System.Version]
            $sv = $PSBoundParameters[$version] -match $semVerRegex
            if ( ! ($v -or $sv) ) {
                $val = $PSBoundParameters[$version]
                throw "'$version' ($val) cannot be converted to System.Version or System.Management.Automation.SemanticVersion"
            }
        }
    }

    # we've made sure that we've validated the string we got is correct, so just pass it back
    # we've also made sure that we didn't mix min/max with required
    if ( $RequiredVersion ) {
        return "$RequiredVersion"
    }

    # now return the appropriate string
    if ( $MinimumVersion -and ! $MaximumVersion ) {
        if ( Get-VersionType $MinimumVersion ) {
            return "$MinimumVersion"
        }
    }
    elseif ( ! $MinimumVersion -and $MaximumVersion ) {
        # no minimum version
        if ( Get-VersionType $MaximumVersion ) {
            return "(,${MaximumVersion}]"
        }
    }
    else {
        $result = Compare-Version $MinimumVersion $MaximumVersion
        if ( $result -ge 0 ) {
            throw "'$MaximumVersion' must be greater than '$MinimumVersion'"
        }
        return "[${MinimumVersion},${MaximumVersion}]"

    }

}


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
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # add new specifier 
    $PSBoundParameters['Type'] = 'command'
    # Parameter translations
    $verArgs = @{}
    if ( $PSBoundParameters['MinimumVersion'] )      { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MinumumVersion }
    if ( $PSBoundParameters['MaximumVersion'] )      { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )     { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    if ( $PSBoundParameters['AllVersions'] )         { $null = $PSBoundParameters.Remove('AllVersions'); $verArgs['RequiredVersion'] = '*' }
    $ver = Convert-VersionsToNugetVersion @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }
    if ( $PSBoundParameters['AllowPrerelease'] )     { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }
    if ( $PSBoundParameters['Tag'] )                 { $null = $PSBoundParameters.Remove('Tag'); $PSBoundParameters['Tags'] = $Tag }
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Includes'] )            { $null = $PSBoundParameters.Remove('Includes') }
    if ( $PSBoundParameters['DscResource'] )         { $null = $PSBoundParameters.Remove('DscResource'); $PSBoundParameters['Type'] = "DscResource" }
    if ( $PSBoundParameters['RoleCapability'] )      { $null = $PSBoundParameters.Remove('RoleCapability') ; $PSBoundParameters['Type'] = "RoleCapability"}
    if ( $PSBoundParameters['Command'] )             { $null = $PSBoundParameters.Remove('Command') ; $PSBoundParameters['Type'] = "command" }
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
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

        # PARAMETER MAP
        # add new specifier 
        $PSBoundParameters['Type'] = 'DscResource'
        # Parameter translations
        $verArgs = @{}
        if ( $PSBoundParameters['MinimumVersion'] )      { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MinumumVersion }
        if ( $PSBoundParameters['MaximumVersion'] )      { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
        if ( $PSBoundParameters['RequiredVersion'] )     { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
        if ( $PSBoundParameters['AllVersions'] )         { $null = $PSBoundParameters.Remove('AllVersions'); $verArgs['RequiredVersion'] = '*' }
        $ver = Convert-VersionsToNugetVersion @verArgs
        if ( $ver ) {
            $PSBoundParameters['Version'] = $ver
        }

        # Parameter Deletions (unsupported in v3)
        if ( $PSBoundParameters['AllowPrerelease'] )     { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }
        if ( $PSBoundParameters['Tag'] )                 { $null = $PSBoundParameters.Remove('Tag'); $PSBoundParameters['Tags'] = $Tag }
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
    if ( $PSBoundParameters['MinimumVersion'] )      { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MinumumVersion }
    if ( $PSBoundParameters['MaximumVersion'] )      { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )     { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    if ( $PSBoundParameters['AllVersions'] )         { $null = $PSBoundParameters.Remove('AllVersions'); $verArgs['RequiredVersion'] = '*' }
    $ver = Convert-VersionsToNugetVersion @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Tag'] )                 { $null = $PSBoundParameters.Remove('Tag'); $PSBoundParameters['Tags'] = $Tag }
    if ( $PSBoundParameters['Includes'] )            { $null = $PSBoundParameters.Remove('Includes') }
    if ( $PSBoundParameters['DscResource'] )         { $null = $PSBoundParameters.Remove('DscResource') }
    if ( $PSBoundParameters['RoleCapability'] )      { $null = $PSBoundParameters.Remove('RoleCapability') }
    if ( $PSBoundParameters['Command'] )             { $null = $PSBoundParameters.Remove('Command') }
    if ( $PSBoundParameters['Proxy'] )               { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] )     { $null = $PSBoundParameters.Remove('ProxyCredential') }
    if ( $PSBoundParameters['AllowPrerelease'] )     { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }
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
    # Parameter translations
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Name'] )     { $null = $PSBoundParameters.Remove('Name') }
    if ( $PSBoundParameters['ModuleName'] )     { $null = $PSBoundParameters.Remove('ModuleName') }
    if ( $PSBoundParameters['MinimumVersion'] )     { $null = $PSBoundParameters.Remove('MinimumVersion') }
    if ( $PSBoundParameters['MaximumVersion'] )     { $null = $PSBoundParameters.Remove('MaximumVersion') }
    if ( $PSBoundParameters['RequiredVersion'] )     { $null = $PSBoundParameters.Remove('RequiredVersion') }
    if ( $PSBoundParameters['AllVersions'] )     { $null = $PSBoundParameters.Remove('AllVersions') }
    if ( $PSBoundParameters['AllowPrerelease'] )     { $null = $PSBoundParameters.Remove('AllowPrerelease') }
    if ( $PSBoundParameters['Tag'] )     { $null = $PSBoundParameters.Remove('Tag') }
    if ( $PSBoundParameters['Filter'] )     { $null = $PSBoundParameters.Remove('Filter') }
    if ( $PSBoundParameters['Proxy'] )     { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] )     { $null = $PSBoundParameters.Remove('ProxyCredential') }
    if ( $PSBoundParameters['Repository'] )     { $null = $PSBoundParameters.Remove('Repository') }
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
    if ( $PSBoundParameters['MinimumVersion'] )      { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MinumumVersion }
    if ( $PSBoundParameters['MaximumVersion'] )      { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )     { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    if ( $PSBoundParameters['AllVersions'] )         { $null = $PSBoundParameters.Remove('AllVersions'); $verArgs['RequiredVersion'] = '*' }
    $ver = Convert-VersionsToNugetVersion @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }
    if ( $PSBoundParameters['Tag'] )                 { $null = $PSBoundParameters.Remove('Tag'); $PSBoundParameters['Tags'] = $Tag }
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

function Get-CredsFromCredentialProvider {
[CmdletBinding()]
param(
    [Parameter(Position=0)]
    [ValidateNotNullOrEmpty()]
    [uri]
    ${SourceLocation},

    [Parameter(Position=1)]
    [bool]
    ${isRetry})

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
    # Parameter translations
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['SourceLocation'] )     { $null = $PSBoundParameters.Remove('SourceLocation') }
    if ( $PSBoundParameters['isRetry'] )     { $null = $PSBoundParameters.Remove('isRetry') }
    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('unknown', [System.Management.Automation.CommandTypes]::Cmdlet)
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

.ForwardHelpTargetName Get-CredsFromCredentialProvider
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
    if ( $PSBoundParameters['Name'] )     { $null = $PSBoundParameters.Remove('Name') }
    if ( $PSBoundParameters['MinimumVersion'] )     { $null = $PSBoundParameters.Remove('MinimumVersion') }
    if ( $PSBoundParameters['RequiredVersion'] )     { $null = $PSBoundParameters.Remove('RequiredVersion') }
    if ( $PSBoundParameters['MaximumVersion'] )     { $null = $PSBoundParameters.Remove('MaximumVersion') }
    if ( $PSBoundParameters['AllVersions'] )     { $null = $PSBoundParameters.Remove('AllVersions') }
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
    if ( $PSBoundParameters['Name'] )     { $null = $PSBoundParameters.Remove('Name') }
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
    if ( $PSBoundParameters['Name'] )     { $null = $PSBoundParameters.Remove('Name') }
    # END PARAMETER MAP

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
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # add new specifier 


    $PSBoundParameters['Type'] = 'module'
    # handle version changes
    $verArgs = @{}
    if ( $PSBoundParameters['MinimumVersion'] )     { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MinumumVersion }
    if ( $PSBoundParameters['MaximumVersion'] )     { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )    { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    $ver = Convert-VersionsToNugetVersion @verArgs
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
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # add new specifier 

    $PSBoundParameters['Type'] = 'script'
    # handle version changes
    $verArgs = @{}
    if ( $PSBoundParameters['MinimumVersion'] )     { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MinumumVersion }
    if ( $PSBoundParameters['MaximumVersion'] )     { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )    { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    $ver = Convert-VersionsToNugetVersion @verArgs
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

function New-ScriptFileInfo {
[CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium', PositionalBinding=$false, HelpUri='https://go.microsoft.com/fwlink/?LinkId=619792')]
param(
    [Parameter(Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string]
    ${Path},

    [ValidateNotNullOrEmpty()]
    [string]
    ${Version},

    [ValidateNotNullOrEmpty()]
    [string]
    ${Author},

    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]
    ${Description},

    [ValidateNotNullOrEmpty()]
    [guid]
    ${Guid},

    [ValidateNotNullOrEmpty()]
    [string]
    ${CompanyName},

    [ValidateNotNullOrEmpty()]
    [string]
    ${Copyright},

    [ValidateNotNullOrEmpty()]
    [System.Object[]]
    ${RequiredModules},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${ExternalModuleDependencies},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${RequiredScripts},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${ExternalScriptDependencies},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Tags},

    [ValidateNotNullOrEmpty()]
    [uri]
    ${ProjectUri},

    [ValidateNotNullOrEmpty()]
    [uri]
    ${LicenseUri},

    [ValidateNotNullOrEmpty()]
    [uri]
    ${IconUri},

    [string[]]
    ${ReleaseNotes},

    [ValidateNotNullOrEmpty()]
    [string]
    ${PrivateData},

    [switch]
    ${PassThru},

    [switch]
    ${Force})

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
    # Parameter translations
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Path'] )     { $null = $PSBoundParameters.Remove('Path') }
    if ( $PSBoundParameters['Version'] )     { $null = $PSBoundParameters.Remove('Version') }
    if ( $PSBoundParameters['Author'] )     { $null = $PSBoundParameters.Remove('Author') }
    if ( $PSBoundParameters['Description'] )     { $null = $PSBoundParameters.Remove('Description') }
    if ( $PSBoundParameters['Guid'] )     { $null = $PSBoundParameters.Remove('Guid') }
    if ( $PSBoundParameters['CompanyName'] )     { $null = $PSBoundParameters.Remove('CompanyName') }
    if ( $PSBoundParameters['Copyright'] )     { $null = $PSBoundParameters.Remove('Copyright') }
    if ( $PSBoundParameters['RequiredModules'] )     { $null = $PSBoundParameters.Remove('RequiredModules') }
    if ( $PSBoundParameters['ExternalModuleDependencies'] )     { $null = $PSBoundParameters.Remove('ExternalModuleDependencies') }
    if ( $PSBoundParameters['RequiredScripts'] )     { $null = $PSBoundParameters.Remove('RequiredScripts') }
    if ( $PSBoundParameters['ExternalScriptDependencies'] )     { $null = $PSBoundParameters.Remove('ExternalScriptDependencies') }
    if ( $PSBoundParameters['Tags'] )     { $null = $PSBoundParameters.Remove('Tags') }
    if ( $PSBoundParameters['ProjectUri'] )     { $null = $PSBoundParameters.Remove('ProjectUri') }
    if ( $PSBoundParameters['LicenseUri'] )     { $null = $PSBoundParameters.Remove('LicenseUri') }
    if ( $PSBoundParameters['IconUri'] )     { $null = $PSBoundParameters.Remove('IconUri') }
    if ( $PSBoundParameters['ReleaseNotes'] )     { $null = $PSBoundParameters.Remove('ReleaseNotes') }
    if ( $PSBoundParameters['PrivateData'] )     { $null = $PSBoundParameters.Remove('PrivateData') }
    if ( $PSBoundParameters['PassThru'] )     { $null = $PSBoundParameters.Remove('PassThru') }
    if ( $PSBoundParameters['Force'] )     { $null = $PSBoundParameters.Remove('Force') }
    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('unknown', [System.Management.Automation.CommandTypes]::Cmdlet)
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

.ForwardHelpTargetName New-ScriptFileInfo
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
    if ( $PSBoundParameters['Name'] )              { $null = $PSBoundParameters.Remove('Name') }
    #if ( $PSBoundParameters['Path'] )              { $null = $PSBoundParameters.Remove('Path') }
    if ( $PSBoundParameters['RequiredVersion'] )   { $null = $PSBoundParameters.Remove('RequiredVersion') }
    if ( $PSBoundParameters['NuGetApiKey'] )       { $null = $PSBoundParameters.Remove('NuGetApiKey'); $PSBoundParameters['APIKey'] = $NuGetApiKey }
    if ( $PSBoundParameters['Repository'] )        { $null = $PSBoundParameters.Remove('Repository') }
    #if ( $PSBoundParameters['Credential'] )        { $null = $PSBoundParameters.Remove('Credential') }
    if ( $PSBoundParameters['FormatVersion'] )     { $null = $PSBoundParameters.Remove('FormatVersion') }
    #if ( $PSBoundParameters['ReleaseNotes'] )      { $null = $PSBoundParameters.Remove('ReleaseNotes') }
    #if ( $PSBoundParameters['Tags'] )              { $null = $PSBoundParameters.Remove('Tags') }
    #if ( $PSBoundParameters['LicenseUri'] )        { $null = $PSBoundParameters.Remove('LicenseUri') }
    #if ( $PSBoundParameters['IconUri'] )           { $null = $PSBoundParameters.Remove('IconUri') }
    #if ( $PSBoundParameters['ProjectUri'] )        { $null = $PSBoundParameters.Remove('ProjectUri') }
    #if ( $PSBoundParameters['Exclude'] )           { $null = $PSBoundParameters.Remove('Exclude') }
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
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # add new specifier 
    #if ( $PSBoundParameters['Path'] )     { $null = $PSBoundParameters.Remove('Path') }
    #if ( $PSBoundParameters['LiteralPath'] )     { $null = $PSBoundParameters.Remove('LiteralPath') }
    #if ( $PSBoundParameters['Repository'] )     { $null = $PSBoundParameters.Remove('Repository') }
    #if ( $PSBoundParameters['Credential'] )     { $null = $PSBoundParameters.Remove('Credential') }
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
    [uri]
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
    if ( $PSBoundParameters['Name'] )     { $null = $PSBoundParameters.Remove('Name') }
    if ( $PSBoundParameters['SourceLocation'] )     { $null = $PSBoundParameters.Remove('SourceLocation') }
    if ( $PSBoundParameters['PublishLocation'] )     { $null = $PSBoundParameters.Remove('PublishLocation') }
    if ( $PSBoundParameters['ScriptSourceLocation'] )     { $null = $PSBoundParameters.Remove('ScriptSourceLocation') }
    if ( $PSBoundParameters['ScriptPublishLocation'] )     { $null = $PSBoundParameters.Remove('ScriptPublishLocation') }
    if ( $PSBoundParameters['Credential'] )     { $null = $PSBoundParameters.Remove('Credential') }
    if ( $PSBoundParameters['Default'] )     { $null = $PSBoundParameters.Remove('Default') }
    if ( $PSBoundParameters['InstallationPolicy'] )     { $null = $PSBoundParameters.Remove('InstallationPolicy') }
    if ( $PSBoundParameters['Proxy'] )     { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] )     { $null = $PSBoundParameters.Remove('ProxyCredential') }
    if ( $PSBoundParameters['PackageManagementProvider'] )     { $null = $PSBoundParameters.Remove('PackageManagementProvider') }
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
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    # add new specifier 
    # if ( $PSBoundParameters['Name'] )     { $null = $PSBoundParameters.Remove('Name') }
    if ( $PSBoundParameters['InputObject'] )     { $null = $PSBoundParameters.Remove('InputObject') }

    # handle version changes
    $verArgs = @{}
    if ( $PSBoundParameters['MinimumVersion'] )     { $null = $PSBoundParameters.Remove('MinimumVersion'); $verArgs['MinimumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['MaximumVersion'] )     { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )    { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    $ver = Convert-VersionsToNugetVersion @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }

    # Parameter translations
    if ( $PSBoundParameters['AllowPrerelease'] )    { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }
    if ( $PSBoundParameters['LiteralPath'] )        { $null = $PSBoundParameters.Remove('LiteralPath'); $PSBoundParameters['Path'] = $LiteralPath }

    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Proxy'] )           { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] ) { $null = $PSBoundParameters.Remove('ProxyCredential') }
    if ( $PSBoundParameters['Force'] )          { $null = $PSBoundParameters.Remove('Force') }
    if ( $PSBoundParameters['AcceptLicense'] )   { $null = $PSBoundParameters.Remove('AcceptLicense') }
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
    $ver = Convert-VersionsToNugetVersion @verArgs
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
    [uri]
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
    if ( $PSBoundParameters['Name'] )     { $null = $PSBoundParameters.Remove('Name') }
    if ( $PSBoundParameters['SourceLocation'] )     { $null = $PSBoundParameters.Remove('SourceLocation') }
    if ( $PSBoundParameters['PublishLocation'] )     { $null = $PSBoundParameters.Remove('PublishLocation') }
    if ( $PSBoundParameters['ScriptSourceLocation'] )     { $null = $PSBoundParameters.Remove('ScriptSourceLocation') }
    if ( $PSBoundParameters['ScriptPublishLocation'] )     { $null = $PSBoundParameters.Remove('ScriptPublishLocation') }
    if ( $PSBoundParameters['Credential'] )     { $null = $PSBoundParameters.Remove('Credential') }
    if ( $PSBoundParameters['InstallationPolicy'] )     { $null = $PSBoundParameters.Remove('InstallationPolicy') }
    if ( $PSBoundParameters['Proxy'] )     { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] )     { $null = $PSBoundParameters.Remove('ProxyCredential') }
    if ( $PSBoundParameters['PackageManagementProvider'] )     { $null = $PSBoundParameters.Remove('PackageManagementProvider') }
    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('SetPSResourceRepository', [System.Management.Automation.CommandTypes]::Cmdlet)
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

function Test-ScriptFileInfo {
[CmdletBinding(DefaultParameterSetName='PathParameterSet', PositionalBinding=$false, HelpUri='https://go.microsoft.com/fwlink/?LinkId=619791')]
param(
    [Parameter(ParameterSetName='PathParameterSet', Mandatory=$true, Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string]
    ${Path},

    [Parameter(ParameterSetName='LiteralPathParameterSet', Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
    [Alias('PSPath')]
    [ValidateNotNullOrEmpty()]
    [string]
    ${LiteralPath})

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
    # Parameter translations
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Path'] )     { $null = $PSBoundParameters.Remove('Path') }
    if ( $PSBoundParameters['LiteralPath'] )     { $null = $PSBoundParameters.Remove('LiteralPath') }
    # END PARAMETER MAP

        $wrappedCmd = $ExecutionContext.InvokeCommand.GetCommand('unknown', [System.Management.Automation.CommandTypes]::Cmdlet)
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

.ForwardHelpTargetName Test-ScriptFileInfo
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
    if ( $PSBoundParameters['Name'] )     { $null = $PSBoundParameters.Remove('Name') }
    if ( $PSBoundParameters['InputObject'] )     { $null = $PSBoundParameters.Remove('InputObject') }
    if ( $PSBoundParameters['MinimumVersion'] )     { $null = $PSBoundParameters.Remove('MinimumVersion') }
    if ( $PSBoundParameters['RequiredVersion'] )     { $null = $PSBoundParameters.Remove('RequiredVersion') }
    if ( $PSBoundParameters['MaximumVersion'] )     { $null = $PSBoundParameters.Remove('MaximumVersion') }
    if ( $PSBoundParameters['AllVersions'] )     { $null = $PSBoundParameters.Remove('AllVersions') }
    if ( $PSBoundParameters['Force'] )     { $null = $PSBoundParameters.Remove('Force') }
    if ( $PSBoundParameters['AllowPrerelease'] )     { $null = $PSBoundParameters.Remove('AllowPrerelease') }
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
    if ( $PSBoundParameters['Name'] )     { $null = $PSBoundParameters.Remove('Name') }
    if ( $PSBoundParameters['InputObject'] )     { $null = $PSBoundParameters.Remove('InputObject') }
    if ( $PSBoundParameters['MinimumVersion'] )     { $null = $PSBoundParameters.Remove('MinimumVersion') }
    if ( $PSBoundParameters['RequiredVersion'] )     { $null = $PSBoundParameters.Remove('RequiredVersion') }
    if ( $PSBoundParameters['MaximumVersion'] )     { $null = $PSBoundParameters.Remove('MaximumVersion') }
    if ( $PSBoundParameters['Force'] )     { $null = $PSBoundParameters.Remove('Force') }
    if ( $PSBoundParameters['AllowPrerelease'] )     { $null = $PSBoundParameters.Remove('AllowPrerelease') }
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
    if ( $PSBoundParameters['Name'] )     { $null = $PSBoundParameters.Remove('Name') }
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
    try {
        $outBuffer = $null
        if ($PSBoundParameters.TryGetValue('OutBuffer', [ref]$outBuffer))
        {
            $PSBoundParameters['OutBuffer'] = 1
        }

    # PARAMETER MAP
    $PSBoundParameters['Type'] = 'module'
    # handle version changes
    $verArgs = @{}
    if ( $PSBoundParameters['MaximumVersion'] )     { $null = $PSBoundParameters.Remove('MaximumVersion'); $verArgs['MaximumVersion'] = $MaximumVersion }
    if ( $PSBoundParameters['RequiredVersion'] )    { $null = $PSBoundParameters.Remove('RequiredVersion'); $verArgs['RequiredVersion'] = $RequiredVersion }
    $ver = Convert-VersionsToNugetVersion @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }

    # Parameter translations
    if ( $PSBoundParameters['AllowPrerelease'] )    { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }

    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Proxy'] )              { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] )    { $null = $PSBoundParameters.Remove('ProxyCredential') }
    if ( $PSBoundParameters['SkipPublisherCheck'] ) { $null = $PSBoundParameters.Remove('SkipPublisherCheck') }
    if ( $PSBoundParameters['InputObject'] )        { $null = $PSBoundParameters.Remove('InputObject') }
    if ( $PSBoundParameters['PassThru'] )           { $null = $PSBoundParameters.Remove('PassThru') }
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

function Update-ModuleManifest {
[CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium', PositionalBinding=$false, HelpUri='https://go.microsoft.com/fwlink/?LinkId=619311')]
param(
    [Parameter(Mandatory=$true, Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string]
    ${Path},

    [ValidateNotNullOrEmpty()]
    [System.Object[]]
    ${NestedModules},

    [ValidateNotNullOrEmpty()]
    [guid]
    ${Guid},

    [ValidateNotNullOrEmpty()]
    [string]
    ${Author},

    [ValidateNotNullOrEmpty()]
    [string]
    ${CompanyName},

    [ValidateNotNullOrEmpty()]
    [string]
    ${Copyright},

    [ValidateNotNullOrEmpty()]
    [string]
    ${RootModule},

    [ValidateNotNullOrEmpty()]
    [version]
    ${ModuleVersion},

    [ValidateNotNullOrEmpty()]
    [string]
    ${Description},

    [ValidateNotNullOrEmpty()]
    [System.Reflection.ProcessorArchitecture]
    ${ProcessorArchitecture},

    [ValidateSet('Desktop','Core')]
    [string[]]
    ${CompatiblePSEditions},

    [ValidateNotNullOrEmpty()]
    [version]
    ${PowerShellVersion},

    [ValidateNotNullOrEmpty()]
    [version]
    ${ClrVersion},

    [ValidateNotNullOrEmpty()]
    [version]
    ${DotNetFrameworkVersion},

    [ValidateNotNullOrEmpty()]
    [string]
    ${PowerShellHostName},

    [ValidateNotNullOrEmpty()]
    [version]
    ${PowerShellHostVersion},

    [ValidateNotNullOrEmpty()]
    [System.Object[]]
    ${RequiredModules},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${TypesToProcess},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${FormatsToProcess},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${ScriptsToProcess},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${RequiredAssemblies},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${FileList},

    [ValidateNotNullOrEmpty()]
    [System.Object[]]
    ${ModuleList},

    [string[]]
    ${FunctionsToExport},

    [string[]]
    ${AliasesToExport},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${VariablesToExport},

    [string[]]
    ${CmdletsToExport},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${DscResourcesToExport},

    [ValidateNotNullOrEmpty()]
    [hashtable]
    ${PrivateData},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Tags},

    [ValidateNotNullOrEmpty()]
    [uri]
    ${ProjectUri},

    [ValidateNotNullOrEmpty()]
    [uri]
    ${LicenseUri},

    [ValidateNotNullOrEmpty()]
    [uri]
    ${IconUri},

    [string[]]
    ${ReleaseNotes},

    [string]
    ${Prerelease},

    [ValidateNotNullOrEmpty()]
    [uri]
    ${HelpInfoUri},

    [switch]
    ${PassThru},

    [ValidateNotNullOrEmpty()]
    [string]
    ${DefaultCommandPrefix},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${ExternalModuleDependencies},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${PackageManagementProviders},

    [switch]
    ${RequireLicenseAcceptance})

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
    # Parameter translations
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Path'] )     { $null = $PSBoundParameters.Remove('Path') }
    if ( $PSBoundParameters['NestedModules'] )     { $null = $PSBoundParameters.Remove('NestedModules') }
    if ( $PSBoundParameters['Guid'] )     { $null = $PSBoundParameters.Remove('Guid') }
    if ( $PSBoundParameters['Author'] )     { $null = $PSBoundParameters.Remove('Author') }
    if ( $PSBoundParameters['CompanyName'] )     { $null = $PSBoundParameters.Remove('CompanyName') }
    if ( $PSBoundParameters['Copyright'] )     { $null = $PSBoundParameters.Remove('Copyright') }
    if ( $PSBoundParameters['RootModule'] )     { $null = $PSBoundParameters.Remove('RootModule') }
    if ( $PSBoundParameters['ModuleVersion'] )     { $null = $PSBoundParameters.Remove('ModuleVersion') }
    if ( $PSBoundParameters['Description'] )     { $null = $PSBoundParameters.Remove('Description') }
    if ( $PSBoundParameters['ProcessorArchitecture'] )     { $null = $PSBoundParameters.Remove('ProcessorArchitecture') }
    if ( $PSBoundParameters['CompatiblePSEditions'] )     { $null = $PSBoundParameters.Remove('CompatiblePSEditions') }
    if ( $PSBoundParameters['PowerShellVersion'] )     { $null = $PSBoundParameters.Remove('PowerShellVersion') }
    if ( $PSBoundParameters['ClrVersion'] )     { $null = $PSBoundParameters.Remove('ClrVersion') }
    if ( $PSBoundParameters['DotNetFrameworkVersion'] )     { $null = $PSBoundParameters.Remove('DotNetFrameworkVersion') }
    if ( $PSBoundParameters['PowerShellHostName'] )     { $null = $PSBoundParameters.Remove('PowerShellHostName') }
    if ( $PSBoundParameters['PowerShellHostVersion'] )     { $null = $PSBoundParameters.Remove('PowerShellHostVersion') }
    if ( $PSBoundParameters['RequiredModules'] )     { $null = $PSBoundParameters.Remove('RequiredModules') }
    if ( $PSBoundParameters['TypesToProcess'] )     { $null = $PSBoundParameters.Remove('TypesToProcess') }
    if ( $PSBoundParameters['FormatsToProcess'] )     { $null = $PSBoundParameters.Remove('FormatsToProcess') }
    if ( $PSBoundParameters['ScriptsToProcess'] )     { $null = $PSBoundParameters.Remove('ScriptsToProcess') }
    if ( $PSBoundParameters['RequiredAssemblies'] )     { $null = $PSBoundParameters.Remove('RequiredAssemblies') }
    if ( $PSBoundParameters['FileList'] )     { $null = $PSBoundParameters.Remove('FileList') }
    if ( $PSBoundParameters['ModuleList'] )     { $null = $PSBoundParameters.Remove('ModuleList') }
    if ( $PSBoundParameters['FunctionsToExport'] )     { $null = $PSBoundParameters.Remove('FunctionsToExport') }
    if ( $PSBoundParameters['AliasesToExport'] )     { $null = $PSBoundParameters.Remove('AliasesToExport') }
    if ( $PSBoundParameters['VariablesToExport'] )     { $null = $PSBoundParameters.Remove('VariablesToExport') }
    if ( $PSBoundParameters['CmdletsToExport'] )     { $null = $PSBoundParameters.Remove('CmdletsToExport') }
    if ( $PSBoundParameters['DscResourcesToExport'] )     { $null = $PSBoundParameters.Remove('DscResourcesToExport') }
    if ( $PSBoundParameters['PrivateData'] )     { $null = $PSBoundParameters.Remove('PrivateData') }
    if ( $PSBoundParameters['Tags'] )     { $null = $PSBoundParameters.Remove('Tags') }
    if ( $PSBoundParameters['ProjectUri'] )     { $null = $PSBoundParameters.Remove('ProjectUri') }
    if ( $PSBoundParameters['LicenseUri'] )     { $null = $PSBoundParameters.Remove('LicenseUri') }
    if ( $PSBoundParameters['IconUri'] )     { $null = $PSBoundParameters.Remove('IconUri') }
    if ( $PSBoundParameters['ReleaseNotes'] )     { $null = $PSBoundParameters.Remove('ReleaseNotes') }
    if ( $PSBoundParameters['Prerelease'] )     { $null = $PSBoundParameters.Remove('Prerelease') }
    if ( $PSBoundParameters['HelpInfoUri'] )     { $null = $PSBoundParameters.Remove('HelpInfoUri') }
    if ( $PSBoundParameters['PassThru'] )     { $null = $PSBoundParameters.Remove('PassThru') }
    if ( $PSBoundParameters['DefaultCommandPrefix'] )     { $null = $PSBoundParameters.Remove('DefaultCommandPrefix') }
    if ( $PSBoundParameters['ExternalModuleDependencies'] )     { $null = $PSBoundParameters.Remove('ExternalModuleDependencies') }
    if ( $PSBoundParameters['PackageManagementProviders'] )     { $null = $PSBoundParameters.Remove('PackageManagementProviders') }
    if ( $PSBoundParameters['RequireLicenseAcceptance'] )     { $null = $PSBoundParameters.Remove('RequireLicenseAcceptance') }
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

.ForwardHelpTargetName Update-ModuleManifest
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
    $ver = Convert-VersionsToNugetVersion @verArgs
    if ( $ver ) {
        $PSBoundParameters['Version'] = $ver
    }

    # Parameter translations
    if ( $PSBoundParameters['AllowPrerelease'] )    { $null = $PSBoundParameters.Remove('AllowPrerelease'); $PSBoundParameters['Prerelease'] = $AllowPrerelease }
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Proxy'] )             { $null = $PSBoundParameters.Remove('Proxy') }
    if ( $PSBoundParameters['ProxyCredential'] )   { $null = $PSBoundParameters.Remove('ProxyCredential') }
    if ( $PSBoundParameters['PassThru'] )          { $null = $PSBoundParameters.Remove('PassThru') }
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

function Update-ScriptFileInfo {
[CmdletBinding(DefaultParameterSetName='PathParameterSet', SupportsShouldProcess=$true, ConfirmImpact='Medium', PositionalBinding=$false, HelpUri='https://go.microsoft.com/fwlink/?LinkId=619793')]
param(
    [Parameter(ParameterSetName='PathParameterSet', Mandatory=$true, Position=0, ValueFromPipelineByPropertyName=$true)]
    [ValidateNotNullOrEmpty()]
    [string]
    ${Path},

    [Parameter(ParameterSetName='LiteralPathParameterSet', Mandatory=$true, Position=0, ValueFromPipelineByPropertyName=$true)]
    [Alias('PSPath')]
    [ValidateNotNullOrEmpty()]
    [string]
    ${LiteralPath},

    [ValidateNotNullOrEmpty()]
    [string]
    ${Version},

    [ValidateNotNullOrEmpty()]
    [string]
    ${Author},

    [ValidateNotNullOrEmpty()]
    [guid]
    ${Guid},

    [ValidateNotNullOrEmpty()]
    [string]
    ${Description},

    [ValidateNotNullOrEmpty()]
    [string]
    ${CompanyName},

    [ValidateNotNullOrEmpty()]
    [string]
    ${Copyright},

    [ValidateNotNullOrEmpty()]
    [System.Object[]]
    ${RequiredModules},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${ExternalModuleDependencies},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${RequiredScripts},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${ExternalScriptDependencies},

    [ValidateNotNullOrEmpty()]
    [string[]]
    ${Tags},

    [ValidateNotNullOrEmpty()]
    [uri]
    ${ProjectUri},

    [ValidateNotNullOrEmpty()]
    [uri]
    ${LicenseUri},

    [ValidateNotNullOrEmpty()]
    [uri]
    ${IconUri},

    [string[]]
    ${ReleaseNotes},

    [ValidateNotNullOrEmpty()]
    [string]
    ${PrivateData},

    [switch]
    ${PassThru},

    [switch]
    ${Force})

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
    # Parameter translations
    # Parameter Deletions (unsupported in v3)
    if ( $PSBoundParameters['Path'] )     { $null = $PSBoundParameters.Remove('Path') }
    if ( $PSBoundParameters['LiteralPath'] )     { $null = $PSBoundParameters.Remove('LiteralPath') }
    if ( $PSBoundParameters['Version'] )     { $null = $PSBoundParameters.Remove('Version') }
    if ( $PSBoundParameters['Author'] )     { $null = $PSBoundParameters.Remove('Author') }
    if ( $PSBoundParameters['Guid'] )     { $null = $PSBoundParameters.Remove('Guid') }
    if ( $PSBoundParameters['Description'] )     { $null = $PSBoundParameters.Remove('Description') }
    if ( $PSBoundParameters['CompanyName'] )     { $null = $PSBoundParameters.Remove('CompanyName') }
    if ( $PSBoundParameters['Copyright'] )     { $null = $PSBoundParameters.Remove('Copyright') }
    if ( $PSBoundParameters['RequiredModules'] )     { $null = $PSBoundParameters.Remove('RequiredModules') }
    if ( $PSBoundParameters['ExternalModuleDependencies'] )     { $null = $PSBoundParameters.Remove('ExternalModuleDependencies') }
    if ( $PSBoundParameters['RequiredScripts'] )     { $null = $PSBoundParameters.Remove('RequiredScripts') }
    if ( $PSBoundParameters['ExternalScriptDependencies'] )     { $null = $PSBoundParameters.Remove('ExternalScriptDependencies') }
    if ( $PSBoundParameters['Tags'] )     { $null = $PSBoundParameters.Remove('Tags') }
    if ( $PSBoundParameters['ProjectUri'] )     { $null = $PSBoundParameters.Remove('ProjectUri') }
    if ( $PSBoundParameters['LicenseUri'] )     { $null = $PSBoundParameters.Remove('LicenseUri') }
    if ( $PSBoundParameters['IconUri'] )     { $null = $PSBoundParameters.Remove('IconUri') }
    if ( $PSBoundParameters['ReleaseNotes'] )     { $null = $PSBoundParameters.Remove('ReleaseNotes') }
    if ( $PSBoundParameters['PrivateData'] )     { $null = $PSBoundParameters.Remove('PrivateData') }
    if ( $PSBoundParameters['PassThru'] )     { $null = $PSBoundParameters.Remove('PassThru') }
    if ( $PSBoundParameters['Force'] )     { $null = $PSBoundParameters.Remove('Force') }
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

.ForwardHelpTargetName Update-ScriptFileInfo
.ForwardHelpCategory Function

#>

}

$functionsToExport = @(
"Find-Command",
"Find-Module",
"Find-DscResource",
"Find-RoleCapability",
"Find-Script",
"Install-Module",
"Install-Script",
"Update-Module",
"Update-Script",
"Publish-Module",
"Publish-Script"
)

export-ModuleMember -Function $functionsToExport

# High Priority
#  Install-Module -> Install-PSResource
#  Install-Script -> Install-PSResource
#  Find-Module -> Find-PSResource
#  Find-Script -> Find-PSResource
#  Update-Module -> Update-PSResource
#  Update-Script -> Update-PSResource
#  Publish-Module -> Publish-PSResource
#  Publish-Script -> Publish-PSResource
#  Save-Module -> Save-PSResource
#  Save-Script -> Save-PSResource
#  Register-PSRepository -> Register-PSResourceRepository
#
# lower priority
#  Get-InstalledModule -> Get-PSResource
#  Get-InstalledScript -> Get-PSResource
#  Get-PSRepository - Get-PSResourceRepository
#  Uninstall-Module -> Uninstall-PSResource
#  Uninstall-Script -> Uninstall-PSResource
#  Unregister-PSRepository -> Unregister->PSResourceRepository
#  Set-PSRepository -> Set-PSResourceRepository
#  Find-Module -> Find-PSResource
#  Find-Command -> Find-PSResource
#  Find-DSCResource -> Find-PSResource
#  Find-RoleCapability -> Find-PSResource