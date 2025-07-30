## Copyright (c) Microsoft Corporation. All rights reserved.
## Licensed under the MIT License.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('repository', 'psresource', 'repositories', 'psresources')]
    [string]$ResourceType,
    [Parameter(Mandatory = $true)]
    [ValidateSet('get', 'set', 'export', 'test')]
    [string]$Operation,
    [Parameter(ValueFromPipeline)]
    $stdinput
)

function Write-Trace {
    param(
        [string]$message,
        [string]$level = 'Error'
    )

    $trace = [pscustomobject]@{
        $level.ToLower() = $message
    } | ConvertTo-Json -Compress

    if ($level -eq 'Error') {
        $host.ui.WriteErrorLine($trace)
    }
    elseif ($level -eq 'Warning') {
        $host.ui.WriteWarningLine($trace)
    }
    elseif ($level -eq 'Verbose') {
        $host.ui.WriteVerboseLine($trace)
    }
    elseif ($level -eq 'Debug') {
        $host.ui.WriteDebugLine($trace)
    }
    else {
        $host.ui.WriteInformation($trace)
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
            $rep = Get-PSResourceRepository -Name $inputObj.Name -ErrorVariable err -ErrorAction SilentlyContinue

            if ($err.FullyQualifiedErrorId -eq 'ErrorGettingSpecifiedRepo,Microsoft.PowerShell.PSResourceGet.Cmdlets.GetPSResourceRepository') {
                return PopulateRepositoryObject -RepositoryInfo $null
            }

            $ret = PopulateRepositoryObject -RepositoryInfo $rep
            return $ret
        }

        'repositories' { throw [System.NotImplementedException]::new("Get operation is not implemented for Repositories resource.") }
        'psresource' { throw [System.NotImplementedException]::new("Get operation is not implemented for PSResource resource.") }
        'psresources' {
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
            $rep = Get-PSResourceRepository -ErrorAction Stop

            $rep | ForEach-Object {
                PopulateRepositoryObject -RepositoryInfo $_
            }
        }

        'repositories' { throw [System.NotImplementedException]::new("Get operation is not implemented for Repositories resource.") }
        'psresource' { throw [System.NotImplementedException]::new("Get operation is not implemented for PSResource resource.") }
        'psresources' {
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
        Write-Trace -message "Uninstalling resources: $($resourcesToUninstall | ForEach-Object { "$($_.Name) - $($_.Version)" })" -Level Verbose
        $resourcesToUninstall | ForEach-Object {
            Uninstall-PSResource -Name $_.Name -Scope $scope -ErrorAction Stop
        }
    }

    if ($resourcesToInstall.Count -gt 0) {
        Write-Trace -message "Installing resources: $($resourcesToInstall.Values | ForEach-Object { " $($_.Name) -- $($_.Version) " })" -Level Verbose
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

        'repositories' { throw [System.NotImplementedException]::new("Get operation is not implemented for Repositories resource.") }
        'psresource' { throw [System.NotImplementedException]::new("Get operation is not implemented for PSResource resource.") }
        'psresources' { return SetPSResources -inputObj $inputObj }
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

function PopulateRepositoryObject {
    param(
        $RepositoryInfo
    )

    $repository = if (-not $RepositoryInfo) {
        Write-Trace -message "RepositoryInfo is null or empty. Returning _exist = false" -Level Information

        $inputJson = $stdinput | ConvertFrom-Json -ErrorAction Stop

        [pscustomobject]@{
            name           = $inputJson.Name
            uri            = $inputJson.Uri
            trusted        = $inputJson.Trusted
            priority       = $inputJson.Priority
            repositoryType = $inputJson.repositoryType
            _exist        = $false
        }
    }
    else {
        Write-Trace -message "Populating repository object for: $($RepositoryInfo.Name)" -Level Verbose
        [pscustomobject]@{
            name           = $RepositoryInfo.Name
            uri            = $RepositoryInfo.Uri
            trusted        = $RepositoryInfo.Trusted
            priority       = $RepositoryInfo.Priority
            repositoryType = $RepositoryInfo.ApiVersion
            _exist        = $true
        }
    }

    return ($repository | ConvertTo-Json -Compress)
}

switch ($Operation.ToLower()) {
    'get' { return (GetOperation -ResourceType $ResourceType) }
    'set' { return (SetOperation -ResourceType $ResourceType) }
    'export' { return (ExportOperation -ResourceType $ResourceType) }
    default { throw "Unknown Operation: $Operation" }
}
