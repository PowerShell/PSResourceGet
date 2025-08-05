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
}

class PSResourceList {
    [string]$repositoryName
    [Scope]$scope
    [PSResource[]]$resources

    PSResourceList([string]$repositoryName, [Scope]$scope, [PSResource[]]$resources) {
        $this.repositoryName = $repositoryName
        $this.scope = $scope
        $this.resources = $resources
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

# catch any un-caught exception and write it to the error stream
trap {
    Write-Trace -Level Error -message $_.Exception.Message
    exit 1
}

function GetOperation {
    param(
        [string]$ResourceType
    )

    ## TODO : ensure that version returned includes pre-release versions

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

            return ( $ret | ConvertTo-Json -Compress )
        }

        'repositorylist' { throw [System.NotImplementedException]::new("Get operation is not implemented for RepositoryList resource.") }
        'psresource' { throw [System.NotImplementedException]::new("Get operation is not implemented for PSResource resource.") }
        'psresourcelist' {
            $allPSResources = if ($inputObj.scope) {
                Get-PSResource -Scope $inputObj.Scope
            }
            else {
                Get-PSResource
            }

            if ($inputObj.repositoryName) {
                $allPSResources = FilterPSResourcesByRepository -allPSResources $allPSResources -repositoryName $inputObj.repositoryName
            }

            $resourcesExist = @()

            Add-Type -AssemblyName "$PSScriptRoot/dependencies/NuGet.Versioning.dll"

            foreach ($resource in $allPSResources) {
                foreach ($inputResource in $inputObj.resources) {
                    if ($resource.Name -eq $inputResource.Name) {
                        if ($inputResource.Version) {
                            # Use the NuGet.Versioning package if available, otherwise do a simple comparison
                            try {
                                $versionRange = [NuGet.Versioning.VersionRange]::Parse($inputResource.Version)
                                $resourceVersion = [NuGet.Versioning.NuGetVersion]::Parse($resource.Version.ToString())
                                if ($versionRange.Satisfies($resourceVersion)) {
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

            PopulatePSResourcesObjectByRepository -resourcesExist $resourcesExist -inputResources $inputObj.resources -repositoryName $inputObj.repositoryName -scope $inputObj.Scope
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
                ) | ConvertTo-Json -Compress
            }

        }

        'repositorylist' { throw [System.NotImplementedException]::new("Get operation is not implemented for RepositoryList resource.") }
        'psresource' { throw [System.NotImplementedException]::new("Get operation is not implemented for PSResource resource.") }
        'psresourcelist' {
            $allPSResources = Get-PSResource
            PopulatePSResourcesObject -allPSResources $allPSResources
        }
        default { throw "Unknown ResourceType: $ResourceType" }
    }
}

function SetPSResources {
    param(
        $inputObj
    )

    $repositoryName = $inputObj.repositoryName
    $scope = $inputObj.scope

    if (-not $scope) {
        $scope = 'CurrentUser'
    }

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

            $splatt = @{}

            if ($inputObj.Name) {
                $splatt['Name'] = $inputObj.Name
            }

            if ($inputObj.Uri) {
                $splatt['Uri'] = $inputObj.Uri
            }

            if ($inputObj.Trusted) {
                $splatt['Trusted'] = $inputObj.Trusted
            }

            if ($null -ne $inputObj.Priority ) {
                $splatt['Priority'] = $inputObj.Priority
            }

            if ($inputObj.repositoryType) {
                $splatt['ApiVersion'] = $inputObj.repositoryType
            }

            if ($null -eq $rep) {
                Register-PSResourceRepository @splatt
            }
            else {
                Set-PSResourceRepository @splatt
            }

            return GetOperation -ResourceType $ResourceType
        }

        'repositorylist' { throw [System.NotImplementedException]::new("Get operation is not implemented for RepositoryList resource.") }
        'psresource' { throw [System.NotImplementedException]::new("Get operation is not implemented for PSResource resource.") }
        'psresourcelist' { return SetPSResources -inputObj $inputObj }
        default { throw "Unknown ResourceType: $ResourceType" }
    }
}

function DeleteOperation {
     param(
        [string]$ResourceType
    )

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


function FilterPSResourcesByRepository {
    param (
        $allPSResources,
        $repositoryName
    )

    if (-not $repositoryName) {
        return $allPSResources
    }

    $filteredResources = $allPSResources | Where-Object { $_.Repository -eq $repositoryName }

    return $filteredResources
}

function PopulatePSResourcesObjectByRepository {
    param (
        $resourcesExist,
        $inputResources,
        $repositoryName,
        $scope
    )

    $resources = @()
    $resourcesObj = @()

    if (-not $resourcesExist) {
        $resourcesObj = $inputResources | ForEach-Object {
            [pscustomobject]@{
                name    = $_.Name
                version = $_.Version.ToString()
                _exist = $false
            }
        }
    }
    else {
        $resources += $resourcesExist | ForEach-Object {
            [pscustomobject]@{
                name    = $_.Name
                version = $_.Version.ToString()
                _exist = $true
            }
        }

        $resourcesObj = if ($scope) {
            [pscustomobject]@{
                repositoryName = $repositoryName
                scope          = $scope
                resources      = $resources
            }
        }
        else {
            [pscustomobject]@{
                repositoryName = $repositoryName
                resources      = $resources
            }
        }
    }

    return ($resourcesObj | ConvertTo-Json -Compress)
}

function PopulatePSResourcesObject {
    param (
        $allPSResources
    )

    $repoGrps = $allPSResources | Group-Object -Property Repository

    $repoGrps | ForEach-Object {
        $repoName = $_.Name


        $resources = $_.Group | ForEach-Object {
            [pscustomobject]@{
                name    = $_.Name
                version = $_.Version.ToString()
                _exist = $true
            }
        }

        $resourcesObj = [pscustomobject]@{
            repositoryName = $repoName
            resources      = $resources
        }

        $resourcesObj | ConvertTo-Json -Compress
    }
}



switch ($Operation.ToLower()) {
    'get' { return (GetOperation -ResourceType $ResourceType) }
    'set' { return (SetOperation -ResourceType $ResourceType) }
    'export' { return (ExportOperation -ResourceType $ResourceType) }
    'delete' { return (DeleteOperation -ResourceType $ResourceType) }
    default { throw "Unknown Operation: $Operation" }
}
