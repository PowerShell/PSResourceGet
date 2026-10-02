## Copyright (c) Microsoft Corporation. All rights reserved.
## Licensed under the MIT License.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('repository', 'psresource', 'repositorylist', 'psresourcelist')]
    [string]$ResourceType,
    [Parameter(Mandatory = $true)]
    [ValidateSet('get', 'set', 'test', 'delete', 'export')]
    [string]$Operation,
    [Parameter(ValueFromPipeline)]
    $stdinput,
    [switch]$WhatIf
)

enum Scope {
    CurrentUser
    AllUsers
}

enum ExitCode {
    Success = 0
    Error = 1
    RepositoryNotFound = 2
    RepositoryNotTrusted = 3
    InstallationFailed = 4
    UnknownResourceType = 5
    ResourceNotImplemented = 6
    TestNotImplemented = 7
    ExportNotImplemented = 8
    GetNotImplemented = 9
    SetNotImplemented = 10
    DeleteNotImplemented = 11
    UnknownOperation = 12
}

class PSResource {
    [string]$name
    [string]$version
    [Scope]$scope
    [string]$repositoryName
    [bool]$preRelease
    [bool]$_exist
    [bool]$_inDesiredState
    [object]$_metadata

    PSResource([string]$name, [string]$version, [Scope]$scope, [string]$repositoryName, [bool]$preRelease) {
        $this.name = $name
        $this.version = $version
        $this.scope = $scope
        $this.repositoryName = $repositoryName
        $this.preRelease = $preRelease
        $this._exist = $true
    }

    PSResource([string]$name) {
        $this.name = $name
        $this._exist = $false
    }

    ## $this is the current state of the resource and $other is the desired state
    [bool] IsInDesiredState([PSResource] $other) {
        $retValue = $true

        if ($this.name -ne $other.name) {
            Write-Trace -message "Name mismatch: $($this.name) vs $($other.name)" -level debug
            $retValue = $false
        }
        ## Compare _exist first. When the resource should not exist, only its absence matters
        elseif ($this._exist -ne $other._exist) {
            Write-Trace -message "_exist mismatch: $($this._exist) vs $($other._exist)" -level debug
            $retValue = $false
        }
        elseif (-not $other._exist) {
            Write-Trace -message "Resource '$($this.name)' does not exist, as desired." -level debug
        }
        ## The string properties are empty instead of null when not specified, which means there is no constraint
        elseif (-not [string]::IsNullOrEmpty($other.version) -and -not (SatisfiesVersion -version $this.version -versionRange $other.version)) {
            Write-Trace -message "Version mismatch: $($this.version) vs $($other.version)" -level debug
            $retValue = $false
        }
        elseif ($this.scope -ne $other.scope) {
            Write-Trace -message "Scope mismatch: $($this.scope) vs $($other.scope)" -level debug
            $retValue = $false
        }
        elseif (-not [string]::IsNullOrEmpty($other.repositoryName) -and $this.repositoryName -ne $other.repositoryName) {
            Write-Trace -message "Repository mismatch: $($this.repositoryName) vs $($other.repositoryName)" -level debug
            $retValue = $false
        }

        return $retValue
    }

    [string] ToJson() {
        [string[]]$excludeProps = @('_inDesiredState')
        if ($null -eq $this._metadata) { $excludeProps += '_metadata' }
        $retVal = ($this | Select-Object -ExcludeProperty $excludeProps | ConvertTo-Json -Compress -EnumsAsStrings)
        Write-Trace -message "Serializing PSResource to JSON. Name: $($this.name), Version: $($this.version), Scope: $($this.scope), RepositoryName: $($this.repositoryName), PreRelease: $($this.preRelease), _exist: $($this._exist)" -level debug
        Write-Trace -message "Serialized JSON: $retVal" -level trace
        return $retVal
    }

    [string] ToJsonForTest() {
        [string[]]$excludeProps = @()
        if ($null -eq $this._metadata) { $excludeProps += '_metadata' }
        return ($this | Select-Object -ExcludeProperty $excludeProps | ConvertTo-Json -Compress -Depth 5 -EnumsAsStrings)
    }
}

class PSResourceList {
    [string]$repositoryName
    [PSResource[]]$resources
    [bool]$trustedRepository
    [bool]$_inDesiredState

    PSResourceList([string]$repositoryName, [PSResource[]]$resources, [bool]$trustedRepository) {
        $this.repositoryName = $repositoryName
        $this.resources = $resources
        $this.trustedRepository = $trustedRepository
    }

    [string] ToJson() {
        ## Assign the array directly so that an empty list serializes as [] rather than null
        [object[]]$resourceObjects = @()
        if ($this.resources) {
            $resourceObjects = @($this.resources | ForEach-Object {
                [string[]]$excludeProps = @('_inDesiredState')
                if ($null -eq $_._metadata) { $excludeProps += '_metadata' }
                $_ | Select-Object -ExcludeProperty $excludeProps
            })
        }
        $retVal = [ordered]@{
            repositoryName = $this.repositoryName
            resources      = $resourceObjects
        } | ConvertTo-Json -Compress -Depth 5 -EnumsAsStrings
        Write-Trace -message "Serializing PSResourceList to JSON. RepositoryName: $($this.repositoryName), TrustedRepository: $($this.trustedRepository), Resources count: $($this.resources.Count)" -level debug
        Write-Trace -message "Serialized JSON: $retVal" -level trace
        return $retVal
    }

    [string] ToJsonForTest() {
        Write-Trace -message "Serializing PSResourceList to JSON for test output. RepositoryName: $($this.repositoryName), TrustedRepository: $($this.trustedRepository), Resources count: $($this.resources.Count)" -level debug
        [object[]]$resourceObjects = @()
        if ($this.resources) {
            $resourceObjects = @($this.resources | ForEach-Object {
                [string[]]$excludeProps = @()
                if ($null -eq $_._metadata) { $excludeProps += '_metadata' }
                if ($excludeProps.Count -gt 0) { $_ | Select-Object -ExcludeProperty $excludeProps } else { $_ }
            })
        }
        $retVal = [ordered]@{
            repositoryName    = $this.repositoryName
            resources         = $resourceObjects
            trustedRepository = $this.trustedRepository
            _inDesiredState   = $this._inDesiredState
        } | ConvertTo-Json -Compress -Depth 5 -EnumsAsStrings
        Write-Trace -message "Serialized JSON: $retVal" -level trace
        return $retVal
    }
}

class Repository {
    [string]$name
    [string]$uri
    [bool]$trusted
    [int]$priority
    [string]$repositoryType
    [bool]$_exist

    Repository([string]$name) {
        $this.name = $name
        $this._exist = $false
        $this.repositoryType = 'Unknown'
    }

    Repository([string]$name, [string]$uri, [bool]$trusted, [int]$priority, [string]$repositoryType) {
        $this.name = $name
        $this.uri = $uri
        $this.trusted = $trusted
        $this.priority = $priority
        $this.repositoryType = $repositoryType
        $this._exist = $true
    }

    Repository([PSCustomObject]$repositoryInfo) {
        $this.name = $repositoryInfo.Name
        $this.uri = $repositoryInfo.Uri
        $this.trusted = $repositoryInfo.Trusted
        $this.priority = $repositoryInfo.Priority
        $this.repositoryType = $repositoryInfo.ApiVersion
        $this._exist = $true
    }

    Repository([string]$name, [bool]$exist) {
        $this.name = $name
        $this._exist = $exist
        $this.repositoryType = 'Unknown'
    }

    [string] ToJson() {
        return ($this | ConvertTo-Json -Compress -EnumsAsStrings)
    }
}

function Write-Trace {
    param(
        [string]$message,

        [ValidateSet('error', 'warn', 'info', 'debug', 'trace')]
        [string]$level = 'trace'
    )

    $trace = [pscustomobject]@{
        $level.ToLower() = $message
    } | ConvertTo-Json -Compress

    $host.ui.WriteErrorLine($trace)
}

function SatisfiesVersion {
    param(
        [string]$version,
        [string]$versionRange
    )

    $typeName = 'NuGet.Versioning.VersionRange'

    Write-Trace -message "Checking if version '$version' satisfies version range '$versionRange'." -level debug

    if ($typeName -as [type]) {
        Write-Trace -message "NuGet.Versioning assembly is already loaded. Using existing assembly." -level debug
    }
    else {
        Write-Trace -message "Loading NuGet.Versioning assembly from $PSScriptRoot/dependencies/NuGet.Versioning.dll" -level debug
        Add-Type -Path "$PSScriptRoot/dependencies/NuGet.Versioning.dll" -ErrorAction Stop | Out-Null
    }

    try {
        $versionRangeObj = [NuGet.Versioning.VersionRange]::Parse($versionRange)
        $resourceVersion = [NuGet.Versioning.NuGetVersion]::Parse($version)
        return $versionRangeObj.Satisfies($resourceVersion)
    }
    catch {
        Write-Trace -message "Error parsing version or version range: $($_.Exception.Message)" -level error
        return $false
    }
}

function ConvertInputToPSResource(
    [PSCustomObject]$inputObj,
    [string]$repositoryName = $null
) {
    $scope = if ($inputObj.Scope) { [Scope]$inputObj.Scope } else { [Scope]"CurrentUser" }

    $psResource = [PSResource]::new(
        $inputObj.Name,
        $inputObj.Version,
        $scope,
        $inputObj.repositoryName ? $inputObj.repositoryName : $repositoryName,
        $inputObj.PreRelease
    )

    if ($null -ne $inputObj._exist) {
        $psResource._exist = $inputObj._exist
    }

    return $psResource
}

# catch any un-caught exception and write it to the error stream
# trace it as an error, otherwise only the exit code description from the manifest is shown to the user
trap {
    Write-Trace -message "Exiting with error code 1 due to unhandled exception: $($_.Exception.Message)" -level error
    exit [ExitCode]::Error
}

## The current state of a PSResourceList is the current resource of every desired resource, in input order
function GetPSResourceList {
    param(
        [PSCustomObject]$inputObj
    )

    $repositoryState = Get-PSResourceRepository -Name $inputObj.repositoryName -ErrorAction SilentlyContinue
    $currentResources = @(GetPSResourceListActions -inputObj $inputObj | ForEach-Object { $_.current })

    return [PSResourceList]::new($inputObj.repositoryName, $currentResources, [bool]$repositoryState.Trusted)
}

function ConvertInputToPSResourceList {
    param(
        [PSCustomObject]$inputObj
    )


    if ($inputObj.resources) {
        $inputObj.resources | ForEach-Object {
            ConvertInputToPSResource -inputObj $_ -repositoryName $inputObj.repositoryName
        }
    }
}

## Gets the resources that are installed from the repository, in both scopes
function GetInstalledPSResources {
    param(
        [string]$repositoryName
    )

    if (-not $repositoryName) {
        return
    }

    foreach ($scope in @('CurrentUser', 'AllUsers')) {
        Get-PSResource -Scope $scope -ErrorAction SilentlyContinue | Where-Object { $_.Repository -eq $repositoryName } | ForEach-Object {
            [PSResource]::new(
                $_.Name,
                $_.Prerelease ? $_.Version.ToString() + "-" + $_.Prerelease : $_.Version.ToString(),
                [Scope]$scope,
                $_.Repository,
                $_.Prerelease ? $true : $false
            )
        }
    }
}

## Resolves the current state of one desired resource. The installed resources are matched by name, and a copy in the
## desired scope is preferred over a copy in the other scope, so that set converges when the resource is installed in both.
function ResolveCurrentPSResource {
    param(
        [PSResource]$desiredResource,
        [PSResource[]]$installedResources
    )

    $name = $desiredResource.name
    $matchingResources = @($installedResources | Where-Object { $_.name -eq $name })

    if ($matchingResources.Count -eq 0) {
        Write-Trace -message "Resource '$name' is not installed. Reporting _exist = false." -level debug
        return [PSResource]::new($name)
    }

    $candidates = @($matchingResources | Where-Object { $_.scope -eq $desiredResource.scope }) + @($matchingResources | Where-Object { $_.scope -ne $desiredResource.scope })

    if (-not $desiredResource.version) {
        # No version constraint: any installed version means the resource exists.
        Write-Trace -message "No version constraint for input: $name. Treating installed version $($candidates[0].version) in scope $($candidates[0].scope) as a match." -level debug
        return $candidates[0]
    }

    $preferred = $candidates | Where-Object {
        try { SatisfiesVersion -version $_.version -versionRange $desiredResource.version } catch { $false }
    } | Select-Object -First 1

    if ($preferred) {
        Write-Trace -message "Resource '$name' version '$($preferred.version)' in scope $($preferred.scope) satisfies requested range '$($desiredResource.version)'." -level debug
        return $preferred
    }

    # Installed but doesn't satisfy the version range - report actual installed version with _exist = false
    # Use a copy, the installed resource can also be the current resource of another desired resource with the same name
    $installed = $candidates[0]
    Write-Trace -message "Resource '$name' installed at '$($installed.version)' does not satisfy requested range '$($desiredResource.version)'. Reporting _exist = false." -level debug
    $fallback = [PSResource]::new($installed.name, $installed.version, $installed.scope, $installed.repositoryName, $installed.preRelease)
    $fallback._exist = $false
    return $fallback
}

## Pairs every desired resource with its current resource and decides the action needed to reach the desired state.
## Every desired resource resolves its own current resource by name, so the order of the installed resources does not matter.
## The get, test, set and what-if operations all use this, so that they always agree on what has to happen.
function GetPSResourceListActions {
    param(
        [PSCustomObject]$inputObj
    )

    $desiredResources = @(ConvertInputToPSResourceList -inputObj $inputObj)

    if (Get-PSResourceRepository -Name $inputObj.repositoryName -ErrorAction SilentlyContinue) {
        $installedResources = @(GetInstalledPSResources -repositoryName $inputObj.repositoryName)
    }
    else {
        ## Nothing counts as installed from a repository that is not registered
        Write-Trace -message "Repository not found: $($inputObj.repositoryName)" -level info
        $installedResources = @()
    }

    foreach ($desired in $desiredResources) {
        $current = ResolveCurrentPSResource -desiredResource $desired -installedResources $installedResources

        $action = if ($current.IsInDesiredState($desired)) {
            'None'
        }
        elseif ($desired._exist) {
            'Install'
        }
        else {
            'Uninstall'
        }

        [pscustomobject]@{
            desired = $desired
            current = $current
            action  = $action
        }
    }
}

function GetOperation {
    param(
        [string]$ResourceType
    )

    if ([string]::IsNullOrEmpty($stdinput)) {
        Write-Trace -level error -message "Get operation requires --input with the resource properties. No input was provided."
        exit [ExitCode]::Error
    }

    $inputObj = $stdinput | ConvertFrom-Json -ErrorAction Stop

    Write-Trace -message "Starting Get operation for ResourceType: $ResourceType" -level trace

    switch ($ResourceType) {
        'repository' {
            $inputRepository = [Repository]::new($inputObj)
            $rep = Get-PSResourceRepository -Name $inputRepository.Name -ErrorVariable err -ErrorAction SilentlyContinue
            Write-Trace -message "Get-PSResourceRepository returned: $($rep | ConvertTo-Json -Compress)" -level trace

            $ret = if ($err.FullyQualifiedErrorId -eq 'ErrorGettingSpecifiedRepo,Microsoft.PowerShell.PSResourceGet.Cmdlets.GetPSResourceRepository') {
                Write-Trace -message "Repository not found: $($inputRepository.Name). Returning _exist = false" -level debug
                [Repository]::new(
                    $InputRepository.Name,
                    $false
                )
            }
            else {
                [Repository]::new(
                    $rep.Name,
                    $rep.Uri,
                    $rep.Trusted,
                    $rep.Priority,
                    $rep.ApiVersion
                )

                Write-Trace -message "Returning repository object for: $($ret.Name)" -level trace
            }

            Write-Trace -message "Serialized JSON output for Get operation: $($ret.ToJson())" -level trace

            return ( $ret.ToJson() )
        }

        'repositorylist' {
            Write-Trace -level error -message "Get operation is not implemented for RepositoryList resource."
            exit [ExitCode]::ResourceNotImplemented
        }
        'psresource' {
            Write-Trace -level error -message "Get operation is not implemented for PSResource resource."
            exit [ExitCode]::ResourceNotImplemented
        }
        'psresourcelist' {
            (GetPSResourceList -inputObj $inputObj).ToJson()
        }
        default {
            Write-Trace -level error -message "Unknown ResourceType: $ResourceType"
            exit [ExitCode]::ResourceNotImplemented
        }
    }
}

function TestPSResourceList {
    param(
        [PSCustomObject]$inputObj
    )

    $inputResources = @(ConvertInputToPSResourceList -inputObj $inputObj)

    $repositoryState = Get-PSResourceRepository -Name $inputObj.repositoryName -ErrorAction SilentlyContinue

    if (-not $repositoryState) {
        Write-Trace -message "Repository not found: $($inputObj.repositoryName). Returning PSResourceList with _inDesiredState = false." -level debug
        $retValue = [PSResourceList]::new($inputObj.repositoryName, $inputResources, $false)
        $retValue._inDesiredState = $false
        $retValue.ToJsonForTest()
        '["repositoryName", "resources"]'
        ## DSC expects exactly one state and one diff line
        return
    }

    $inputPSResourceList = [PSResourceList]::new($inputObj.repositoryName, $inputResources, $repositoryState.Trusted)

    $resourceActions = @(GetPSResourceListActions -inputObj $inputObj)
    $currentState = [PSResourceList]::new($inputObj.repositoryName, @($resourceActions | ForEach-Object { $_.current }), $repositoryState.Trusted)
    $pendingActions = @($resourceActions | Where-Object { $_.action -ne 'None' })
    foreach ($pendingAction in $pendingActions) {
        Write-Trace -message "Resource mismatch for: $($pendingAction.desired.name). Required action: $($pendingAction.action)" -level debug
    }

    $inDesiredState = $pendingActions.Count -eq 0
    $currentState._inDesiredState = $inDesiredState

    if ($inDesiredState) {
        Write-Trace -message "PSResourceList is in desired state." -level debug
        $currentState.ToJsonForTest()
        ## Return empty array as we are in desired state and there are no differing properties
        '[]'
    }
    else {
        Write-Trace -message "PSResourceList is NOT in desired state." -level debug
        $inputPSResourceList.ToJsonForTest()
        '["resources"]'
    }
}

function TestOperation {
    param(
        [string]$ResourceType
    )

    $inputObj = $stdinput | ConvertFrom-Json -ErrorAction Stop

    switch ($ResourceType) {
        'repository' {
            Write-Trace -level error -message "Test operation is not implemented for Repository resource."
            exit [ExitCode]::TestNotImplemented
        }
        'repositorylist' {
            Write-Trace -level error -message "Test operation is not implemented for RepositoryList resource."
            exit [ExitCode]::TestNotImplemented
        }
        'psresource' {
            Write-Trace -level error -message "Test operation is not implemented for PSResource resource."
            exit [ExitCode]::TestNotImplemented
        }
        'psresourcelist' {
            TestPSResourceList -inputObj $inputObj
        }

        default {
            Write-Trace -level error -message "Unknown ResourceType: $ResourceType"
            exit [ExitCode]::UnknownResourceType
        }
    }
}

function ExportOperation {
    switch ($ResourceType) {
        'repository' {
            $rep = Get-PSResourceRepository -ErrorAction SilentlyContinue

            if (-not $rep) {
                Write-Trace -message "No repositories found. Returning empty array." -level debug
                return @()
            }

            $rep | ForEach-Object {
                [Repository]::new(
                    $_.Name,
                    $_.Uri,
                    $_.Trusted,
                    $_.Priority,
                    $_.ApiVersion
                ).ToJson()
            }
        }

        'repositorylist' {
            Write-Trace -level error -message "Export operation is not implemented for RepositoryList resource."
            exit [ExitCode]::ExportNotImplemented
        }
        'psresource' {
            Write-Trace -level error -message "Export operation is not implemented for PSResource resource."
            exit [ExitCode]::ExportNotImplemented
        }
        'psresourcelist' {
            $currentUserPSResources = Get-PSResource
            $allUsersPSResources = Get-PSResource -Scope AllUsers
            PopulatePSResourceListObject -allUsersPSResources $allUsersPSResources -currentUserPSResources $currentUserPSResources
        }
        default {
            Write-Trace -level error -message "Unknown ResourceType: $ResourceType"
            exit [ExitCode]::UnknownResourceType
        }
    }
}

function WhatIfPSResourceList {
    param(
        $inputObj
    )

    $repositoryName = $inputObj.repositoryName
    $psRepository = Get-PSResourceRepository -Name $repositoryName -ErrorAction SilentlyContinue
    $projectedResources = @()

    ## Use the same actions as the set operation, so that what-if reports what set is going to do
    foreach ($resourceAction in @(GetPSResourceListActions -inputObj $inputObj)) {
        $resourceDesiredState = $resourceAction.desired
        $currentResource = $resourceAction.current
        $name = $resourceDesiredState.name
        $version = $resourceDesiredState.version

        if ($resourceAction.action -eq 'Uninstall') {
            $msg = if ($version) { "Would uninstall resource '$name' version '$version'" } else { "Would uninstall resource '$name'" }
            Write-Trace -message "WhatIf: $msg." -level debug
            $resource = [PSResource]::new(
                $currentResource.name,
                $currentResource.version,
                $currentResource.scope,
                $currentResource.repositoryName,
                $currentResource.preRelease
            )
            $resource._exist = $false
            $resource._metadata = [pscustomobject]@{ whatIf = @($msg) }
            $projectedResources += $resource
        }
        elseif ($resourceAction.action -eq 'Install') {
            $versionStr = if ($version) { $version } else { 'latest' }
            $msg = "Would install resource '$name' version '$versionStr'"
            Write-Trace -message "WhatIf: $msg." -level debug
            $resource = [PSResource]::new($name, $versionStr, $resourceDesiredState.scope, $repositoryName, $resourceDesiredState.preRelease)
            $resource._metadata = [pscustomobject]@{ whatIf = @($msg) }
            $projectedResources += $resource
        }
        else {
            Write-Trace -message "WhatIf: Resource '$name' is already in desired state." -level debug
            $projectedResources += $currentResource
        }
    }

    ## Report the same failures a real set operation would hit before installing anything
    $installRequired = @($projectedResources | Where-Object { $_._exist -and $null -ne $_._metadata }).Count -gt 0
    if ($installRequired) {
        if (-not $psRepository) {
            Write-Trace -level error -message "Repository '$repositoryName' not found. Cannot install resources."
            exit [ExitCode]::RepositoryNotFound
        }

        if (-not $psRepository.Trusted -and -not $inputObj.trustedRepository) {
            Write-Trace -level error -message "Repository '$repositoryName' is not trusted. Cannot install resources."
            exit [ExitCode]::RepositoryNotTrusted
        }
    }

    $list = [PSResourceList]::new($repositoryName, $projectedResources, [bool]$psRepository.Trusted)
    $list.ToJson()
}

function SetPSResourceList {
    param(
        $inputObj,
        [switch]$WhatIf
    )

    if ($WhatIf) {
        return WhatIfPSResourceList -inputObj $inputObj
    }

    $repositoryName = $inputObj.repositoryName
    $resourcesToUninstall = [System.Collections.Generic.Dictionary[string, psobject]]::new()
    $resourcesToInstall = [System.Collections.Generic.Dictionary[string, psobject]]::new()

    $resourcesChanged = $false

    foreach ($resourceAction in @(GetPSResourceListActions -inputObj $inputObj)) {
        $resourceDesiredState = $resourceAction.desired
        $name = $resourceDesiredState.name
        $versionStr = if ($resourceDesiredState.version) { $resourceDesiredState.version } else { 'latest' }

        # Uninstall if resource should not exist but does
        if ($resourceAction.action -eq 'Uninstall') {
            Write-Trace -message "Resource $name exists but _exist is false. Adding to uninstall list." -level debug
            # The resource has to be removed from the scope it is currently installed in
            $key = $name.ToLowerInvariant() + '-' + $versionStr.ToLowerInvariant() + '-' + $resourceAction.current.scope
            if (-not $resourcesToUninstall.ContainsKey($key)) {
                $resourcesToUninstall[$key] = $resourceAction
            }
        }
        # Install if resource should exist but doesn't, or exists but not in desired state
        elseif ($resourceAction.action -eq 'Install') {
            Write-Trace -message "Resource $name needs to be installed." -level debug
            # The same name and version can be requested in both scopes, so the scope is part of the key
            $key = $name.ToLowerInvariant() + '-' + $versionStr.ToLowerInvariant() + '-' + $resourceDesiredState.scope
            if (-not $resourcesToInstall.ContainsKey($key)) {
                $resourcesToInstall[$key] = $resourceDesiredState
            }
        }
        # Otherwise resource is in desired state, no action needed
        else {
            Write-Trace -message "Resource $name is in desired state." -level debug
        }
    }

    if ($resourcesToUninstall.Count -gt 0) {
        Write-Trace -message "Uninstalling resources: $($resourcesToUninstall.Values | ForEach-Object { "$($_.current.name) - $($_.current.version)" })" -level debug
        $resourcesToUninstall.Values | ForEach-Object {
            # Only remove the requested version (range) when there is one, otherwise all versions are removed
            $versionParam = @{}
            if ($_.desired.version) {
                $versionParam['Version'] = $_.desired.version
            }

            $cmdWarnings = $null
            Uninstall-PSResource -Name $_.current.name @versionParam -Scope $_.current.scope -ErrorAction Stop -WarningVariable cmdWarnings
            foreach ($w in $cmdWarnings) {
                Write-Trace -message ([string]$w) -level warn
            }
        }
        $resourcesChanged = $true
    }

    if ($resourcesToInstall.Count -gt 0) {
        $psRepository = Get-PSResourceRepository -Name $repositoryName -ErrorAction SilentlyContinue

        if (-not $psRepository) {
            Write-Trace -level error -message "Repository '$repositoryName' not found. Cannot install resources."
            exit [ExitCode]::RepositoryNotFound
        }

        if (-not $psRepository.Trusted -and -not $inputObj.trustedRepository) {
            Write-Trace -level error -message "Repository '$repositoryName' is not trusted. Cannot install resources."
            exit [ExitCode]::RepositoryNotTrusted
        }

        Write-Trace -message "Installing resources: $($resourcesToInstall.Values | ForEach-Object { " $($_.Name) -- $($_.Version) " })" -level debug
        $resourcesToInstall.Values | ForEach-Object {
            $usePrerelease = if ($_.preRelease) { $true } else { $false }

            $installErrors = @()

            $name = $_.Name
            $version = $_.Version

            # Install-PSResource does not accept an empty version, leave it out to install the latest version
            $versionParam = @{}
            if ($version) {
                $versionParam['Version'] = $version
            }

            try {
                $cmdWarnings = $null
                Install-PSResource -Name $_.Name @versionParam -Scope $_.Scope -Repository $repositoryName -ErrorAction Stop -TrustRepository:$inputObj.trustedRepository -Prerelease:$usePrerelease -Reinstall -WarningVariable cmdWarnings
                foreach ($w in $cmdWarnings) {
                    Write-Trace -message ([string]$w) -level warn
                }
            } catch {
                Write-Trace -level error -message "Failed to install resource '$name' with version '$version'. Error: $($_.Exception.Message)"
                $installErrors += $_.Exception.Message
            }

            if ($installErrors.Count -gt 0) {
                Write-Trace -level error -message "One or more errors occurred while installing resource '$name' with version '$version': $($installErrors -join '; ')"
                Write-Trace -level trace -message "Exiting with error code 4 due to installation failure."
                exit [ExitCode]::InstallationFailed
            }
        }

        $resourcesChanged = $true
    }

    (GetPSResourceList -inputObj $inputObj).ToJson()
    if ($resourcesChanged) {
        '["resources"]'
    }
    else {
        '[]'
    }
}

function SetOperation {
    param(
        [string]$ResourceType
    )

    $inputObj = $stdinput | ConvertFrom-Json -ErrorAction Stop

    switch ($ResourceType) {
        'repository' {
            $rep = Get-PSResourceRepository -Name $inputObj.Name -ErrorAction SilentlyContinue

            $properties = @('name', 'uri', 'trusted', 'priority', 'repositoryType')

            $splatt = @{}

            foreach ($property in $properties) {
                if ($null -ne $inputObj.PSObject.Properties[$property]) {
                    if ($property -eq 'repositoryType') {
                        $splatt['ApiVersion'] = $inputObj.$property
                    }
                    else {
                        $splatt[$property] = $inputObj.$property
                    }
                }
            }

            if ($null -eq $rep -and $inputObj._exist -ne $false) {
                Register-PSResourceRepository @splatt -ErrorAction Stop
            }
            else {
                if ($inputObj._exist -eq $false) {
                    Write-Trace -message "Repository $($inputObj.Name) exists and _exist is false. Deleting it." -level debug
                    Unregister-PSResourceRepository -Name $inputObj.Name -ErrorAction Stop
                }
                else {
                    Set-PSResourceRepository @splatt -ErrorAction Stop
                }
            }

            return GetOperation -ResourceType $ResourceType
        }

        'repositorylist' {
            Write-Trace -level error -message "Set operation is not implemented for RepositoryList resource."
            exit [ExitCode]::SetNotImplemented
        }
        'psresource' {
            Write-Trace -level error -message "Set operation is not implemented for PSResource resource."
            exit [ExitCode]::SetNotImplemented
        }
        'psresourcelist' { return SetPSResourceList -inputObj $inputObj -WhatIf:$WhatIf }
        default {
            Write-Trace -level error -message "Unknown ResourceType: $ResourceType"
            exit [ExitCode]::UnknownResourceType
        }
    }
}

function DeleteOperation {
    param(
        [string]$ResourceType
    )

    $inputObj = $stdinput | ConvertFrom-Json -ErrorAction Stop
    switch ($ResourceType) {
        'repository' {
            if ($inputObj._exist -ne $false) {
                throw "_exist property is not set to false for the repository. Cannot delete."
            }

            $rep = Get-PSResourceRepository -Name $inputObj.Name -ErrorAction SilentlyContinue

            if ($null -ne $rep) {
                Unregister-PSResourceRepository -Name $inputObj.Name -ErrorAction Stop
            }
            else {
                Write-Trace -message "Repository not found: $($inputObj.Name). Nothing to delete." -level debug
            }

            return GetOperation -ResourceType $ResourceType
        }
        'repositorylist' {
            Write-Trace -level error -message "Delete operation is not implemented for RepositoryList resource."
            exit [ExitCode]::DeleteNotImplemented
        }
        'psresource' {
            Write-Trace -level error -message "Delete operation is not implemented for PSResource resource."
            exit [ExitCode]::DeleteNotImplemented
        }
        'psresourcelist' {
            Write-Trace -level error -message "Delete operation is not implemented for PSResourceList resource."
            exit [ExitCode]::DeleteNotImplemented
        }
        default {
            Write-Trace -level error -message "Unknown ResourceType: $ResourceType"
            exit [ExitCode]::UnknownResourceType
        }
    }
}

function PopulatePSResourceListObject {
    param (
        $allUsersPSResources,
        $currentUserPSResources
    )

    $allPSResources = @()

    $allPSResources += $allUsersPSResources | ForEach-Object {
        return [PSResource]::new(
            $_.Name,
            $_.Version,
            [Scope]"AllUsers",
            $_.Repository,
            $_.PreRelease ? $true : $false
        )
    }

    $allPSResources += $currentUserPSResources | ForEach-Object {
        return [PSResource]::new(
            $_.Name,
            $_.Version,
            [Scope]"CurrentUser",
            $_.Repository,
            $_.PreRelease ? $true : $false
        )
    }

    $repoGrps = $allPSResources | Group-Object -Property repositoryName

    $repoGrps | ForEach-Object {
        $repositoryTrust = if ($_.Name) { (Get-PSResourceRepository -Name $_.Name -ErrorAction SilentlyContinue).Trusted } else { $false }
        $repoName = $_.Name
        $resources = $_.Group
        [PSResourceList]::new($repoName, $resources, $repositoryTrust).ToJson()
    }
}

## This is mostly needed for CI tests as the PSModulePath has a different version PSResourceGet
## If the module is loaded from a different path, then we get an error "Assembly with same name is already loaded"
if ($null -eq (Get-Module -Name Microsoft.PowerShell.PSResourceGet)) {
    $path = Join-Path -Path $PSScriptRoot -ChildPath "Microsoft.PowerShell.PSResourceGet.psd1"
    Write-Trace -level trace -message "Importing Microsoft.PowerShell.PSResourceGet module from path: $path"
    Import-Module -Name $path -Force -ErrorAction Stop
}

# Suppress warnings from PSResourceGet cmdlets to prevent them from reaching stdout and
# breaking DSC's JSON parsing. Warnings should be captured on individual cmdlets
$WarningPreference = 'SilentlyContinue'

switch ($Operation.ToLower()) {
    'get' { return (GetOperation -ResourceType $ResourceType) }
    'set' { return (SetOperation -ResourceType $ResourceType) }
    'test' { return (TestOperation -ResourceType $ResourceType) }
    'export' { return (ExportOperation -ResourceType $ResourceType) }
    'delete' { return (DeleteOperation -ResourceType $ResourceType) }
    default {
        Write-Trace -level error -message "Unknown Operation: $Operation"
        exit [ExitCode]::UnknownOperation
    }
}
