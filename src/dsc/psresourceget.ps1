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

    $host.ui.WriteErrorLine($trace)
}

# catch any un-caught exception and write it to the error stream
trap {
    Write-Trace -Level Error -message $_.Exception.Message
    exit 1
}

function GetAllPSResources {
    $resources = Get-PSResource
}

function GetOperation {
    param(
        [string]$ResourceType
    )

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

        'repositories' { return 'Get-PSRepository' }
        'psresource' { return 'Get-PSResource' }
        'psresources' {

            $allPSResources =  if ($inputObj.scope) {
                Get-PSResource -Scope $inputObj.Scope
            } else {
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

        'repositories' { return 'Get-PSRepository' }
        'psresource' { return 'Get-PSResource' }
        'psresources' {
            $allPSResources = Get-PSResource
            PopulatePSResourcesObject -allPSResources $allPSResources
         }
        default { throw "Unknown ResourceType: $ResourceType" }
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

        'repositories' { return 'Set-PSRepository' }
        'psresource' { return 'Set-PSResource' }
        'psresources' { return 'Set-PSResource' }
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
                _exists = $false
            }
        }
    }
    else {
        $resources += $resourcesExist | ForEach-Object {
            [pscustomobject]@{
                name    = $_.Name
                version = $_.Version.ToString()
                _exists = $true
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
                name        = $_.Name
                version     = $_.Version.ToString()
                _exists     = $true
            }
        }

        $resourcesObj = [pscustomobject]@{
            repositoryName = $repoName
            resources  = $resources
        }

        $resourcesObj | ConvertTo-Json -Compress
    }
}

function PopulateRepositoryObject {
    param(
        $RepositoryInfo
    )

    $repository = if (-not $RepositoryInfo) {
        Write-Trace -message "RepositoryInfo is null or empty. Returning _exists = false" -Level Information

        $inputJson = $stdinput | ConvertFrom-Json -ErrorAction Stop

        [pscustomobject]@{
            name           = $inputJson.Name
            uri            = $inputJson.Uri
            trusted        = $inputJson.Trusted
            priority       = $inputJson.Priority
            repositoryType = $inputJson.repositoryType
            _exists        = $false
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
            _exists        = $true
        }
    }

    return ($repository | ConvertTo-Json -Compress)
}

switch ($Operation.ToLower()) {
    'get'  { return (GetOperation -ResourceType $ResourceType) }
    'set'  { return (SetOperation -ResourceType $ResourceType) }
    'export' { return (ExportOperation -ResourceType $ResourceType) }
    default { throw "Unknown Operation: $Operation" }
}
