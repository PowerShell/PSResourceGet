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
    $stdinput
)

enum Scope {
    CurrentUser
    AllUsers
}

class PSResource {
    [string]$name
    [string]$version
    [Scope]$scope
    [string]$repositoryName
    [bool]$preRelease
    [bool]$_exist
    [bool]$_inDesiredState

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

    [bool] IsInDesiredState([PSResource] $other) {
        $retValue = $true

        $psResourceSplat = @{
            Name           = $this.name
            Version        = if ($this.version) { $this.version } else { '*' }
        }

        Get-PSResource @psResourceSplat | Where-Object {
            ($null -eq $this.scope -or $_.Scope -eq $this.scope) -and
            ($null -eq $this.repositoryName -or $_.Repository -eq $this.repositoryName)
        } | Select-Object -First 1 | ForEach-Object {
            Write-Trace -message "Matching resource found: Name=$($_.Name), Version=$($_.Version), Scope=$($_.Scope), Repository=$($_.Repository), PreRelease=$($_.PreRelease)" -level trace
            $this._exist = $true
        }

        if ($this.name -ne $other.name) {
            Write-Trace -message "Name mismatch: $($this.name) vs $($other.name)" -level trace
            $retValue = $false
        }
        elseif ($null -ne $this.version -and $null -ne $other.version -and -not (SatisfiesVersion -version $this.version -versionRange $other.version)) {
            Write-Trace -message "Version mismatch: $($this.version) vs $($other.version)" -level trace
            $retValue = $false
        }
        elseif ($null -ne $this.scope -and $this.scope -ne $other.scope) {
            Write-Trace -message "Scope mismatch: $($this.scope) vs $($other.scope)" -level trace
            $retValue = $false
        }
        elseif ($null -ne $this.repositoryName -and $this.repositoryName -ne $other.repositoryName) {
            Write-Trace -message "Repository mismatch: $($this.repositoryName) vs $($other.repositoryName)" -level trace
            $retValue = $false
        }
        elseif ($this._exist -ne $other._exist) {
            Write-Trace -message "_exist mismatch: $($this._exist) vs $($other._exist)" -level trace
            $retValue = $false
        }

        return $retValue
    }

    [string] ToJson() {
        $retVal = ($this | Select-Object -ExcludeProperty _inDesiredState | ConvertTo-Json -Compress -EnumsAsStrings)
        Write-Trace -message "Serializing PSResource to JSON. Name: $($this.name), Version: $($this.version), Scope: $($this.scope), RepositoryName: $($this.repositoryName), PreRelease: $($this.preRelease), _exist: $($this._exist)" -level trace
        Write-Trace -message "Serialized JSON: $retVal" -level trace
        return $retVal
    }

    [string] ToJsonForTest() {
        return ($this | ConvertTo-Json -Compress -Depth 5 -EnumsAsStrings)
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

    [bool] IsInDesiredState([PSResourceList] $other) {
        if ($this.repositoryName -ne $other.repositoryName) {
            Write-Trace -message "RepositoryName mismatch: $($this.repositoryName) vs $($other.repositoryName)" -level trace
            return $false
        }

        if ($null -ne $this.resources -and $this.resources.Count -ne $other.resources.Count) {
            Write-Trace -message "Resources count mismatch: $($this.resources.Count) vs $($other.resources.Count)" -level trace
            return $false
        }

        foreach ($otherResource in $other.resources) {
            $found = $false
            foreach ($resource in $this.resources) {
                if ($resource.IsInDesiredState($otherResource)) {
                    $found = $true
                    break
                }
            }

            if ($found) {
                Write-Trace -message "Resource match found for: $($otherResource.name)" -level trace
                break
            }
            else {
                Write-Trace -message "Resource mismatch for: $($otherResource.name)" -level trace
                return $false
            }
        }

        return $true
    }

    [string] ToJson() {
        $resourceJson = if ($this.resources) { ($this.resources | ForEach-Object { $_.ToJson() }) -join ',' } else { '' }
        $resourceJson = "[$resourceJson]"
        $jsonString = "{'repositoryName': '$($this.repositoryName)','resources': $resourceJson}"
        $jsonString = $jsonString -replace "'", '"'
        $retVal =  $jsonString | ConvertFrom-Json | ConvertTo-Json -Compress -EnumsAsStrings

        Write-Trace -message "Serializing PSResourceList to JSON. RepositoryName: $($this.repositoryName), TrustedRepository: $($this.trustedRepository), Resources count: $($this.resources.Count)" -level trace
        Write-Trace -message "Serialized JSON: $retVal" -level trace

        return $retVal
    }

    [string] ToJsonForTest() {
        Write-Trace -message "Serializing PSResourceList to JSON for test output. RepositoryName: $($this.repositoryName), TrustedRepository: $($this.trustedRepository), Resources count: $($this.resources.Count)" -level trace
        $jsonForTest = $this | ConvertTo-Json -Compress -Depth 5 -EnumsAsStrings
        Write-Trace -message "Serialized JSON: $jsonForTest" -level trace
        return $jsonForTest
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

    if ($env:SKIP_TRACE) {
        $host.ui.WriteVerboseLine($trace)
    }
    else {
        $host.ui.WriteErrorLine($trace)
    }
}

function SatisfiesVersion {
    param(
        [string]$version,
        [string]$versionRange
    )

    $typeName = 'NuGet.Versioning.VersionRange'

    Write-Trace -message "Checking if version '$version' satisfies version range '$versionRange'." -level trace

    if ($typeName -as [type]) {
        Write-Trace -message "NuGet.Versioning assembly is already loaded. Using existing assembly." -level trace
    }
    else {
        Write-Trace -message "Loading NuGet.Versioning assembly from $PSScriptRoot/dependencies/NuGet.Versioning.dll" -level trace
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
trap {
    Write-Trace -Level Error -message $_.Exception.Message
    Write-Trace -Level Error -message $_.Exception.FileName
    Write-Trace -Level Error -message $_.Exception.HResult

    # exit 1
    Get-LoadedAssembliesByALC |  Select-Object -Property ALCName, AssemblyName, Location | ForEach-Object {
        Write-Trace -message "ALC: $($_.ALCName) - Assembly: $($_.AssemblyName) - Location: $($_.Location)" -level trace
    }

    exit 1
}


function Get-LoadedAssembliesByALC {
    [CmdletBinding()]
    param(
        # Optional filter: only show assemblies whose simple name matches this regex
        [string] $NameMatch,

        # Optional: show only AssemblyLoadContexts whose name matches this regex
        [string] $ALCMatch
    )

    # AssemblyLoadContext is in System.Runtime.Loader
    $alcType = [System.Runtime.Loader.AssemblyLoadContext]

    # Enumerate all ALCs currently alive
    $alcs = $alcType::All

    $rows = foreach ($alc in $alcs) {

        if ($ALCMatch -and ($alc.Name -notmatch $ALCMatch)) { continue }

        foreach ($asm in $alc.Assemblies) {

            $an = $asm.GetName()

            if ($NameMatch -and ($an.Name -notmatch $NameMatch)) { continue }

            # Some assemblies are dynamic/in-memory and have no Location
            $location = $null
            $isDynamic = $false
            try {
                $isDynamic = $asm.IsDynamic
                if (-not $isDynamic) { $location = $asm.Location }
            } catch {
                # Some runtime assemblies can still throw on Location; ignore safely
                $location = $null
            }

            $pkt = $null
            try {
                $bytes = $an.GetPublicKeyToken()
                if ($bytes -and $bytes.Length -gt 0) {
                    $pkt = ($bytes | ForEach-Object { $_.ToString("x2") }) -join ""
                }
            } catch { $pkt = $null }

            [pscustomobject]@{
                ALCName        = if ($alc.Name) { $alc.Name } else { "<unnamed>" }
                IsDefaultALC   = ($alc -eq [System.Runtime.Loader.AssemblyLoadContext]::Default)
                IsCollectible  = $alc.IsCollectible
                AssemblyName   = $an.Name
                Version        = $an.Version.ToString()
                Culture        = if ($an.CultureInfo) { $an.CultureInfo.Name } else { "" }
                PublicKeyToken = $pkt
                IsDynamic      = $isDynamic
                Location       = if ($location) { $location } else { if ($isDynamic) { "<dynamic>" } else { "" } }
                FullName       = $asm.FullName
            }
        }
    }

    # Sort for readability: by ALC then assembly name
    $rows | Sort-Object ALCName, AssemblyName, Version
}

function GetPSResourceList {
    param(
        [PSCustomObject]$inputObj
    )

    $inputResources = @()
    $inputResources += if ($inputObj.resources) {
        $inputObj.resources | ForEach-Object {
            ConvertInputToPSResource -inputObj $_ -repositoryName $inputObj.repositoryName
        }
    }

    $repositoryState = Get-PSResourceRepository -Name $inputObj.repositoryName -ErrorAction SilentlyContinue

    if (-not $repositoryState) {
        Write-Trace -message "Repository not found: $($inputObj.repositoryName)" -level info
        $emptyResources = @()
        $emptyResources += $inputResources | ForEach-Object {
            [PSResource]::new($_.Name)
        }

        return [PSResourceList]::new($inputObj.repositoryName, $emptyResources, $false)
    }

    $inputPSResourceList = [PSResourceList]::new($inputObj.repositoryName, $inputResources, $repositoryState.Trusted)

    $allPSResources = @()

    if ($inputPSResourceList.repositoryName) {
        $currentUserPSResources = Get-PSResource -Scope CurrentUser -ErrorAction SilentlyContinue | Where-Object { $_.Repository -eq $inputPSResourceList.RepositoryName }
        $allUsersPSResources = Get-PSResource -Scope AllUsers -ErrorAction SilentlyContinue | Where-Object { $_.Repository -eq $inputPSResourceList.RepositoryName }
    }

    $allPSResources += $currentUserPSResources | ForEach-Object {
        [PSResource]::new(
            $_.Name,
            $_.Prerelease ? $_.Version.ToString() + "-" + $_.Prerelease : $_.Version.ToString(),
            [Scope]"CurrentUser",
            $_.Repository,
            $_.PreRelease
        )
    }

    $allPSResources += $allUsersPSResources | ForEach-Object {
        [PSResource]::new(
            $_.Name,
            $_.Prerelease ? $_.Version.ToString() + "-" + $_.Prerelease : $_.Version.ToString(),
            [Scope]"AllUsers",
            $_.Repository,
            $_.PreRelease ? $true : $false
        )
    }

    $resourcesExist = @()

    foreach ($resource in $allPSResources) {
        foreach ($inputResource in $inputResources) {
            if ($resource.Name -eq $inputResource.Name) {
                Write-Trace -message "Found matching resource for input: $($inputResource.Name). Checking version constraints. Input version: $($inputResource.Version), Resource version: $($resource.Version)" -level trace
                if ($inputResource.Version) {
                    # Use the NuGet.Versioning package if available, otherwise do a simple comparison
                    try {
                        if (SatisfiesVersion -version $resource.Version -versionRange $inputResource.Version) {
                            $resourcesExist += $resource
                        }
                    }
                    catch {
                        Write-Trace -message "Error checking version constraints for resource: $($inputResource.Name). Error details: $($_.Exception.Message)" -level error
                        # Fallback: simple string comparison (not full NuGet range support)
                        if ($resource.Version.ToString() -eq $inputResource.Version) {
                            $resourcesExist += $resource
                        }
                    }
                }
            }
        }
    }

    ## For get operation we only need the first resource that exists, which is always the latest for currentUser
    PopulatePSResourceListObjectByRepository -resourcesExist $resourcesExist -inputResources $inputResources -repositoryName $inputPSResourceList.RepositoryName -trustedRepository $inputPSResourceList.trustedRepository
}

function GetOperation {
    param(
        [string]$ResourceType
    )

    $inputObj = $stdinput | ConvertFrom-Json -ErrorAction Stop

    Write-Trace -message "Starting Get operation for ResourceType: $ResourceType" -level trace

    switch ($ResourceType) {
        'repository' {
            Write-Trace -message "Processing Get operation for Repository resource." -level trace

            $inputRepository = [Repository]::new($inputObj)

            Write-Trace -message "Looking up repository with name: $($inputRepository.Name)" -level trace

            $rep = Get-PSResourceRepository -Name $inputRepository.Name -ErrorVariable err -ErrorAction SilentlyContinue

            Write-Trace -message "Get-PSResourceRepository returned: $($rep | ConvertTo-Json -Compress)" -level trace

            $ret = if ($err.FullyQualifiedErrorId -eq 'ErrorGettingSpecifiedRepo,Microsoft.PowerShell.PSResourceGet.Cmdlets.GetPSResourceRepository') {
                Write-Trace -message "Repository not found: $($inputRepository.Name). Returning _exist = false" -level trace
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

            return ( $ret.ToJson() )
        }

        'repositorylist' {
            Write-Trace -level error -message "Get operation is not implemented for RepositoryList resource."
            exit 6
        }
        'psresource' {
            Write-Trace -level error -message "Get operation is not implemented for PSResource resource."
            exit 6
        }
        'psresourcelist' {
            (GetPSResourceList -inputObj $inputObj).ToJson()
        }
        default {
            Write-Trace -level error -message "Unknown ResourceType: $ResourceType"
            exit 6
        }
    }
}

function TestPSResourceList {
    param(
        [PSCustomObject]$inputObj
    )

    $inputResources = @()
    $inputResources += $inputObj.resources | ForEach-Object { ConvertInputToPSResource -inputObj $_ -repositoryName $inputObj.repositoryName }

    $repositoryState = Get-PSResourceRepository -Name $inputObj.repositoryName -ErrorAction SilentlyContinue

    if (-not $repositoryState) {
        Write-Trace -message "Repository not found: $($inputObj.repositoryName). Returning PSResourceList with _inDesiredState = false." -level info
        $retValue = [PSResourceList]::new($inputObj.repositoryName, $inputResources, $false)
        $retValue._inDesiredState = $false
        $retValue.ToJsonForTest()
        '["repositoryName", "resources"]'
    }

    $inputPSResourceList = [PSResourceList]::new($inputObj.repositoryName, $inputResources, $repositoryState.Trusted)

    $currentState = GetPSResourceList -inputObj $inputObj
    $inDesiredState = $currentState.IsInDesiredState($inputPSResourceList)

    $currentState._inDesiredState = $inDesiredState

    if ($inDesiredState) {
        Write-Trace -message "PSResourceList is in desired state." -level info
        $currentState.ToJsonForTest()
        ## Return empty array as we are in desired state and there are no differing properties
        '[]'
    }
    else {
        Write-Trace -message "PSResourceList is NOT in desired state." -level info
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
            exit 7
        }
        'repositorylist' {
            Write-Trace -level error -message "Test operation is not implemented for RepositoryList resource."
            exit 7
        }
        'psresource' {
            Write-Trace -level error -message "Test operation is not implemented for PSResource resource."
            exit 7
        }
        'psresourcelist' {
            TestPSResourceList -inputObj $inputObj
        }

        default {
            Write-Trace -level error -message "Unknown ResourceType: $ResourceType"
            exit 5
        }
    }
}

function ExportOperation {
    switch ($ResourceType) {
        'repository' {
            $rep = Get-PSResourceRepository -ErrorAction SilentlyContinue

            if (-not $rep) {
                Write-Trace -message "No repositories found. Returning empty array." -level trace
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
            exit 8
        }
        'psresource' {
            Write-Trace -level error -message "Export operation is not implemented for PSResource resource."
            exit 8
        }
        'psresourcelist' {
            $currentUserPSResources = Get-PSResource
            $allUsersPSResources = Get-PSResource -Scope AllUsers
            PopulatePSResourceListObject -allUsersPSResources $allUsersPSResources -currentUserPSResources $currentUserPSResources
        }
        default {
            Write-Trace -level error -message "Unknown ResourceType: $ResourceType"
            exit 5
        }
    }
}

function SetPSResourceList {
    param(
        $inputObj
    )

    $repositoryName = $inputObj.repositoryName
    $resourcesToUninstall = @()
    $resourcesToInstall = [System.Collections.Generic.Dictionary[string, psobject]]::new()

    $resourcesChanged = $false

    $currentState = GetPSResourceList -inputObj $inputObj

    $inputObj.resources | ForEach-Object {
        $resourceDesiredState = ConvertInputToPSResource -inputObj $_ -repositoryName $repositoryName
        $name = $resourceDesiredState.name
        $version = $resourceDesiredState.version
        $scope = if ($resourceDesiredState.scope) { $resourceDesiredState.scope } else { "CurrentUser" }

        # Resource should not exist - uninstall if it does
        $currentState.resources | ForEach-Object {

            $isInDesiredState = $_.IsInDesiredState($resourceDesiredState)

            # Uninstall if resource should not exist but does
            if (-not $resourceDesiredState._exist -and $_._exist) {
                Write-Trace -message "Resource $($resourceDesiredState.name) exists but _exist is false. Adding to uninstall list." -level info
                $resourcesToUninstall += $_
            }
            # Install if resource should exist but doesn't, or exists but not in desired state
            elseif ($resourceDesiredState._exist -and (-not $_._exist -or -not $isInDesiredState)) {
                Write-Trace -message "Resource $($resourceDesiredState.name) needs to be installed." -level info
                $versionStr = if ($version) { $resourceDesiredState.version } else { 'latest' }
                $key = $name.ToLowerInvariant() + '-' + $versionStr.ToLowerInvariant()
                if (-not $resourcesToInstall.ContainsKey($key)) {
                    $resourcesToInstall[$key] = $resourceDesiredState
                }
            }
            # Otherwise resource is in desired state, no action needed
            else {
                Write-Trace -message "Resource $($resourceDesiredState.name) is in desired state." -level info
            }
        }
    }

    if ($resourcesToUninstall.Count -gt 0) {
        Write-Trace -message "Uninstalling resources: $($resourcesToUninstall | ForEach-Object { "$($_.Name) - $($_.Version)" })"
        $resourcesToUninstall | ForEach-Object {
            Uninstall-PSResource -Name $_.Name -Scope $scope -ErrorAction Stop
        }
        $resourcesChanged = $true
    }

    if ($resourcesToInstall.Count -gt 0) {
        $psRepository = Get-PSResourceRepository -Name $repositoryName -ErrorAction SilentlyContinue

        if (-not $psRepository) {
            Write-Trace -level error -message "Repository '$repositoryName' not found. Cannot install resources."
            exit 2
        }

        if (-not $psRepository.Trusted -and -not $inputObj.trustedRepository) {
            Write-Trace -level error -message "Repository '$repositoryName' is not trusted. Cannot install resources."
            exit 3
        }

        Write-Trace -message "Installing resources: $($resourcesToInstall.Values | ForEach-Object { " $($_.Name) -- $($_.Version) " })"
        $resourcesToInstall.Values | ForEach-Object {
            $usePrerelease = if ($_.preRelease) { $true } else { $false }

            $installErrors = @()

            $name = $_.Name
            $version = $_.Version

            try {
                Install-PSResource -Name $_.Name -Version $_.Version -Scope $scope -Repository $repositoryName -ErrorAction Stop -TrustRepository:$inputObj.trustedRepository -Prerelease:$usePrerelease -Reinstall
            }
            catch {
                Write-Trace -level error -message "Failed to install resource '$name' with version '$version'. Error: $($_.Exception.Message)"
                $installErrors += $_.Exception.Message
            }

            if ($installErrors.Count -gt 0) {
                Write-Trace -level error -message "One or more errors occurred while installing resource '$name' with version '$version': $($installErrors -join '; ')"
                Write-Trace -level trace -message "Exiting with error code 4 due to installation failure."
                exit 4
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
                Register-PSResourceRepository @splatt
            }
            else {
                if ($inputObj._exist -eq $false) {
                    Write-Trace -message "Repository $($inputObj.Name) exists and _exist is false. Deleting it." -level info
                    Unregister-PSResourceRepository -Name $inputObj.Name
                }
                else {
                    Set-PSResourceRepository @splatt
                }
            }

            return GetOperation -ResourceType $ResourceType
        }

        'repositorylist' {
            Write-Trace -level error -message "Set operation is not implemented for RepositoryList resource."
            exit 10
        }
        'psresource' {
            Write-Trace -level error -message "Set operation is not implemented for PSResource resource."
            exit 11
        }
        'psresourcelist' { return SetPSResourceList -inputObj $inputObj }
        default {
            Write-Trace -level error -message "Unknown ResourceType: $ResourceType"
            exit 5
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
                Unregister-PSResourceRepository -Name $inputObj.Name
            }
            else {
                Write-Trace -message "Repository not found: $($inputObj.Name). Nothing to delete." -level info
            }

            return GetOperation -ResourceType $ResourceType
        }
        'repositorylist' {
            Write-Trace -level error -message "Delete operation is not implemented for RepositoryList resource."
            exit 11
        }
        'psresource' {
            Write-Trace -level error -message "Delete operation is not implemented for PSResource resource."
            exit 11
        }
        'psresourcelist' {
            Write-Trace -level error -message "Delete operation is not implemented for PSResourceList resource."
            exit 11
        }
        default {
            Write-Trace -level error -message "Unknown ResourceType: $ResourceType"
            exit 5
        }
    }
}

function PopulatePSResourceListObjectByRepository {
    param (
        $resourcesExist,
        $inputResources,
        $repositoryName,
        $trustedRepository
    )

    $resources = @()

    if (-not $resourcesExist) {
        $resources = $inputResources | ForEach-Object {
            [PSResource]::new(
                $_.Name
            )
        }
    }
    else {
        $resources += $resourcesExist | ForEach-Object {
            [PSResource]::new(
                $_.Name,
                $_.Version.PreRelease ? $_.Version.ToString() + "-" + $_.PreRelease : $_.Version.ToString(),
                $_.Scope,
                $_.RepositoryName,
                $_.PreRelease ? $true : $false
            )
        }
    }

    $psresourceListObj =
    [PSResourceList]::new(
        $repositoryName,
        $resources,
        $trustedRepository
    )

    return $psresourceListObj
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

if ($null -eq (Get-Module -Name Microsoft.PowerShell.PSResourceGet)) {
    Write-Trace -level trace -message "Microsoft.PowerShell.PSResourceGet module is not currently loaded. Getting loaded assemblies for diagnostics."
    Get-LoadedAssembliesByALC |  Select-Object -Property ALCName, AssemblyName, Location | ForEach-Object {
        Write-Trace -message "ALC: $($_.ALCName) - Assembly: $($_.AssemblyName) - Location: $($_.Location)" -level trace
    }

    Write-Trace -level trace -message "Microsoft.PowerShell.PSResourceGet module is not imported. Importing it."
    Import-Module -Name Microsoft.PowerShell.PSResourceGet -Force -ErrorAction Stop -Verbose
}

switch ($Operation.ToLower()) {
    'get' { return (GetOperation -ResourceType $ResourceType) }
    'set' { return (SetOperation -ResourceType $ResourceType) }
    'test' { return (TestOperation -ResourceType $ResourceType) }
    'export' { return (ExportOperation -ResourceType $ResourceType) }
    'delete' { return (DeleteOperation -ResourceType $ResourceType) }
    default {
        Write-Trace -level error -message "Unknown Operation: $Operation"
        exit 12
    }
}
