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
        $this._inDesiredState = $true
    }

    [bool] IsInDesiredState([PSResource] $other) {
        $retValue = $true

        if ($this.name -ne $other.name) {
            Write-Trace -message "Name mismatch: $($this.name) vs $($other.name)" -level warn
            $retValue = $false
        }
        elseif ($null -ne $this.version -and $null -ne $other.version -and -not (SatisfiesVersion -version $this.version -versionRange $other.version)) {
            Write-Trace -message "Version mismatch: $($this.version) vs $($other.version)" -level warn
            $retValue = $false
        }
        elseif ($null -ne $this.scope -and $this.scope -ne $other.scope) {
            Write-Trace -message "Scope mismatch: $($this.scope) vs $($other.scope)" -level warn
            $retValue = $false
        }
        elseif ($null -ne $this.repositoryName -and $this.repositoryName -ne $other.repositoryName) {
            Write-Trace -message "Repository mismatch: $($this.repositoryName) vs $($other.repositoryName)" -level warn
            $retValue = $false
        }
        elseif ($null -ne $this.preRelease -and $this.preRelease -ne $other.preRelease) {
            Write-Trace -message "PreRelease mismatch: $($this.preRelease) vs $($other.preRelease)" -level warn
            $retValue = $false
        }
        elseif ($this._exist -ne $other._exist) {
            Write-Trace -message "_exist mismatch: $($this._exist) vs $($other._exist)" -level warn
            $retValue = $false
        }

        return $retValue
    }

    [string] ToJson() {
        return ($this | Select-Object -ExcludeProperty _inDesiredState | ConvertTo-Json -Compress)
    }

    [string] ToJsonForTest() {
        return ($this | Select-Object -ExcludeProperty version, preRelease, repositoryName, scope | ConvertTo-Json -Compress)
    }
}

class PSResourceList {
    [string]$repositoryName
    [PSResource[]]$resources
    [bool]$_exist
    [bool]$_inDesiredState

    PSResourceList([string]$repositoryName, [PSResource[]]$resources) {
        $this.repositoryName = $repositoryName
        $this.resources = $resources
    }

    [bool] IsInDesiredState([PSResourceList] $other) {
        if ($this.repositoryName -ne $other.repositoryName) {
            Write-Trace -message "RepositoryName mismatch: $($this.repositoryName) vs $($other.repositoryName)" -level warn
            return $false
        }

        if ($null -ne $this.resources -and $this.resources.Count -ne $other.resources.Count) {
            Write-Trace -message "Resources count mismatch: $($this.resources.Count) vs $($other.resources.Count)" -level warn
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

            if (-not $found) {
                Write-Trace -message "Resource mismatch for: $($otherResource.name)" -level warn
                return $false
            }
        }

        return $true
    }

    [string] ToJson() {
        $resourceJson = ($this.resources | ForEach-Object { $_.ToJson() }) -join ','
        $resourceJson = "[$resourceJson]"
        $jsonString = "{'repositoryName': '$($this.repositoryName)','resources': $resourceJson}"
        $jsonString = $jsonString -replace "'", '"'
        return $jsonString | ConvertFrom-Json | ConvertTo-Json -Compress
    }

    [string] ToJsonForTest() {
        return ($this | Select-Object -ExcludeProperty resources | ConvertTo-Json -Compress)
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
    }

    [string] ToJson() {
        return ($this | ConvertTo-Json -Compress)
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

    if($env:SKIP_TRACE) {
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

    Add-Type -AssemblyName "$PSScriptRoot/dependencies/NuGet.Versioning.dll"

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
    $scope = if ($inputObj.Scope) { [Scope]$inputObj.Scope } else { [Scope]::CurrentUser }

    return [PSResource]::new(
        $inputObj.Name,
        $inputObj.Version,
        $scope,
        $inputObj.repositoryName ? $inputObj.repositoryName : $repositoryName,
        $inputObj.PreRelease
    )
}

# catch any un-caught exception and write it to the error stream
trap {
    Write-Trace -Level Error -message $_.Exception.Message
    exit 1
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

    $inputPSResourceList = [PSResourceList]::new($inputObj.repositoryName, $inputResources)

    $allPSResources = @()

    if ($inputPSResourceList.repositoryName) {
        $currentUserPSResources = Get-PSResource -Scope CurrentUser -ErrorAction SilentlyContinue | Where-Object { $_.Repository -eq $inputPSResourceList.RepositoryName }
        $allUsersPSResources = Get-PSResource -Scope AllUsers -ErrorAction SilentlyContinue | Where-Object { $_.Repository -eq $inputPSResourceList.RepositoryName }
    }

    $allPSResources += $currentUserPSResources | ForEach-Object {
        [PSResource]::new(
            $_.Name,
            $_.Prerelease ? $_.Version.ToString() + "-" + $_.Prerelease : $_.Version.ToString(),
            [Scope]::CurrentUser,
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

    ##Add-Type -AssemblyName "$PSScriptRoot/dependencies/NuGet.Versioning.dll"

    foreach ($resource in $allPSResources) {
        foreach ($inputResource in $inputResources) {
            if ($resource.Name -eq $inputResource.Name) {
                if ($inputResource.Version) {
                    # Use the NuGet.Versioning package if available, otherwise do a simple comparison
                    try {
                        # $versionRange = [NuGet.Versioning.VersionRange]::Parse($inputResource.Version)

                        # $resourceVersion = if ($resource.PreRelease) {
                        #     [NuGet.Versioning.NuGetVersion]::Parse($resource.Version.ToString() + "-" + $resource.PreRelease)
                        # }
                        # else {
                        #     [NuGet.Versioning.NuGetVersion]::Parse($resource.Version.ToString())
                        # }

                        # if ($versionRange.Satisfies($resourceVersion)) {
                        #     $resourcesExist += $resource
                        # }

                        if (SatisfiesVersion -version $resource.Version -versionRange $inputResource.Version) {
                            $resourcesExist += $resource
                        }
                    }
                    catch {
                        # Fallback: simple string comparison (not full NuGet range support)
                        if ($resource.Version.ToString() -eq $inputResource.Version) {
                            $resourcesExist += $resource
                        }
                    }
                }
            }
        }
    }

    PopulatePSResourceListObjectByRepository -resourcesExist $resourcesExist -inputResources $inputResources -repositoryName $inputPSResourceList.RepositoryName
}

function GetOperation {
    param(
        [string]$ResourceType
    )

    $inputObj = $stdinput | ConvertFrom-Json -ErrorAction Stop

    switch ($ResourceType) {
        'repository' {
            $inputRepository = [Repository]::new($inputObj)

            $rep = Get-PSResourceRepository -Name $inputRepository.Name -ErrorVariable err -ErrorAction SilentlyContinue

            $ret = if ($err.FullyQualifiedErrorId -eq 'ErrorGettingSpecifiedRepo,Microsoft.PowerShell.PSResourceGet.Cmdlets.GetPSResourceRepository') {
                Write-Trace -message "Repository not found: $($inputRepository.Name). Returning _exist = false"
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

                Write-Trace -message "Returning repository object for: $($ret.Name)"
            }

            return ( $ret.ToJson() )
        }

        'repositorylist' { throw [System.NotImplementedException]::new("Get operation is not implemented for RepositoryList resource.") }
        'psresource' { throw [System.NotImplementedException]::new("Get operation is not implemented for PSResource resource.") }
        'psresourcelist' {
            (GetPSResourceList -inputObj $inputObj).ToJson()
        }

        default { throw "Unknown ResourceType: $ResourceType" }
    }
}

function TestPSResourceList {
    param(
        [PSCustomObject]$inputObj
    )

    $inputResources = ConvertInputToPSResource -inputObj $inputObj.resources -repositoryName $inputObj.repositoryName
    $inputPSResourceList = [PSResourceList]::new($inputObj.repositoryName, $inputResources)

    $currentState = GetPSResourceList -inputObj $inputObj
    $inDesiredState = $currentState.IsInDesiredState($inputPSResourceList)

    if ($inDesiredState) {
        Write-Trace -message "PSResourceList is in desired state." -level info
    }
    else {
        Write-Trace -message "PSResourceList is NOT in desired state." -level warn
    }

    $currentState._inDesiredState = $inDesiredState

    ## TODO confirm the output format
    if ($inDesiredState)
    {
        $outputJson = [PSCustomObject]@{
            desiredState = '$desiredstate$'
            actualState = '$currentState$'
            inDesiredState = $inDesiredState
            differingProperties = @()
        } | ConvertTo-Json -Compress

        $outputJson = $outputJson -replace '\$desiredstate\$', $inputPSResourceList.ToJsonForTest()
        $outputJson = $outputJson -replace '\$currentState\$', $currentState.ToJsonForTest()
    }
}

function TestOperation {
    param(
        [string]$ResourceType
    )

    $inputObj = $stdinput | ConvertFrom-Json -ErrorAction Stop

    switch ($ResourceType) {
        'repository' { throw [System.NotImplementedException]::new("Test operation is not implemented for RepositoryList resource.") }
        'repositorylist' { throw [System.NotImplementedException]::new("Test operation is not implemented for RepositoryList resource.") }
        'psresource' { throw [System.NotImplementedException]::new("Test operation is not implemented for PSResource resource.") }
        'psresourcelist' {
            TestPSResourceList -inputObj $inputObj
        }

        default { throw "Unknown ResourceType: $ResourceType" }
    }
}

function ExportOperation {
    switch ($ResourceType) {
        'repository' {
            $rep = Get-PSResourceRepository -ErrorAction SilentlyContinue

            if (-not $rep) {
                Write-Trace -message "No repositories found. Returning empty array." -level info
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

        'repositorylist' { throw [System.NotImplementedException]::new("Get operation is not implemented for RepositoryList resource.") }
        'psresource' { throw [System.NotImplementedException]::new("Get operation is not implemented for PSResource resource.") }
        'psresourcelist' {
            $currentUserPSResources = Get-PSResource
            $allUsersPSResources = Get-PSResource -Scope AllUsers
            PopulatePSResourceListObject -allUsersPSResources $allUsersPSResources -currentUserPSResources $currentUserPSResources
        }
        default { throw "Unknown ResourceType: $ResourceType" }
    }
}

function SetPSResourceList {
    param(
        $inputObj
    )

    $inputResources = ConvertInputToPSResource -inputObj $inputObj
    $inputPSResourceList = [PSResourceList]::new($inputObj.repositoryName, $inputResources)

    $repositoryName = $inputObj.repositoryName
    $resourcesToUninstall = @()
    $resourcesToInstall = [System.Collections.Generic.Dictionary[string, psobject]]::new()

    Add-Type -AssemblyName "$PSScriptRoot/dependencies/NuGet.Versioning.dll"

    $inputObj.resources | ForEach-Object {
        $resource = $_
        $name = $resource.name
        $version = $resource.version

        $getSplat = @{
            Name   = $name
            Scope  = $scope
            ErrorAction = 'SilentlyContinue'
        }

        $existingResources = if ($repositoryName) {
            Get-PSResource @getSplat | Where-Object { $_.Repository -eq $repositoryName }
        }
        else {
            Get-PSResource @getSplat
        }

        # uninstall all resources that do not satisfy the version range and install the ones that do
        $existingResources | ForEach-Object {
            $versionRange = [NuGet.Versioning.VersionRange]::Parse($version)
            $resourceVersion = [NuGet.Versioning.NuGetVersion]::Parse($_.Version.ToString())
            if (-not $versionRange.Satisfies($resourceVersion)) {
                if ($resource._exist) {
                    #$resourcesToInstall += $resource
                    $key = $resource.Name.ToLowerInvariant() + '-' + $resource.Version.ToLowerInvariant()
                    if (-not $resourcesToInstall.ContainsKey($key)) {
                        $resourcesToInstall[$key] = $resource
                    }
                }

                $resourcesToUninstall += $_
            }
            else {
                if (-not $resource._exist) {
                    $resourcesToUninstall += $_
                }
            }
        }
    }

    if ($resourcesToUninstall.Count -gt 0) {
        Write-Trace -message "Uninstalling resources: $($resourcesToUninstall | ForEach-Object { "$($_.Name) - $($_.Version)" })"
        $resourcesToUninstall | ForEach-Object {
            Uninstall-PSResource -Name $_.Name -Scope $scope -ErrorAction Stop
        }
    }

    if ($resourcesToInstall.Count -gt 0) {
        Write-Trace -message "Installing resources: $($resourcesToInstall.Values | ForEach-Object { " $($_.Name) -- $($_.Version) " })"
        $resourcesToInstall.Values | ForEach-Object {
            Install-PSResource -Name $_.Name -Version $_.Version -Scope $scope -Repository $repositoryName -ErrorAction Stop
        }
    }
}

function SetOperation {
    param(
        [string]$ResourceType
    )

    <# TODO
    // for test and set everything
              // 2 json lines
                // state == current state of object
                // diff == array of properties that are different

            // for other operations, DONOT return _inDesiredState
    #>

    $inputObj = $stdinput | ConvertFrom-Json -ErrorAction Stop

    switch ($ResourceType) {
        'repository' {
            $rep = Get-PSResourceRepository -Name $inputObj.Name -ErrorAction SilentlyContinue

            $properties = @('Name', 'Uri', 'Trusted', 'Priority', 'RepositoryType')

            $splatt = @{}

            foreach($property in $properties) {
                if ($null -ne $inputObj.PSObject.Properties[$property]) {
                    $splatt[$property] = $inputObj.$property
                }
            }

            if ($null -eq $rep) {
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

        'repositorylist' { throw [System.NotImplementedException]::new("Get operation is not implemented for RepositoryList resource.") }
        'psresource' { throw [System.NotImplementedException]::new("Get operation is not implemented for PSResource resource.") }
        'psresourcelist' { return SetPSResourceList -inputObj $inputObj }
        default { throw "Unknown ResourceType: $ResourceType" }
    }
}

function DeleteOperation {
     param(
        [string]$ResourceType
    )

    ## todo: delete for PSresourceList

    $inputObj = $stdinput | ConvertFrom-Json -ErrorAction Stop
    switch ($ResourceType) {
        'repository' {
            if (-not $inputObj._exist -ne $false) {
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

        'repositorylist' { throw [System.NotImplementedException]::new("Delete operation is not implemented for RepositoryList resource.") }
        'psresource' { throw [System.NotImplementedException]::new("Delete operation is not implemented for PSResource resource.") }
        'psresourcelist' { throw [System.NotImplementedException]::new("Delete operation is not implemented for PSResourceList resource.") }
        default { throw "Unknown ResourceType: $ResourceType" }
    }
}

function PopulatePSResourceListObjectByRepository {
    param (
        $resourcesExist,
        $inputResources,
        $repositoryName
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
                $resources
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
            [Scope]::AllUsers,
            $_.Repository,
            $_.PreRelease ? $true : $false
        )
    }

    $allPSResources += $currentUserPSResources | ForEach-Object {
        return [PSResource]::new(
            $_.Name,
            $_.Version,
            [Scope]::CurrentUser,
            $_.Repository,
            $_.PreRelease ? $true : $false
        )
    }

    $repoGrps = $allPSResources | Group-Object -Property repositoryName

    $repoGrps | ForEach-Object {
        $repoName = $_.Name
        $resources = $_.Group
        [PSResourceList]::new($repoName, $resources).ToJson()
    }
}

switch ($Operation.ToLower()) {
    'get' { return (GetOperation -ResourceType $ResourceType) }
    'set' { return (SetOperation -ResourceType $ResourceType) }
    'test' { return (TestOperation -ResourceType $ResourceType) }
    'export' { return (ExportOperation -ResourceType $ResourceType) }
    'delete' { return (DeleteOperation -ResourceType $ResourceType) }
    default { throw "Unknown Operation: $Operation" }
}
