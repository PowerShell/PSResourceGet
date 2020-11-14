#
# Copyright (c) Microsoft Corporation.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
# THE SOFTWARE.
#

<#
    Helper functions for PowerShellGet DSC Resources.
#>

# Import localization helper functions.
$helperName = 'PowerShellGet.LocalizationHelper'
$resourceModuleRoot = Split-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -Parent
$dscResourcesFolderFilePath = Join-Path -Path $resourceModuleRoot -ChildPath "Modules\$helperName\$helperName.psm1"
Import-Module -Name $dscResourcesFolderFilePath

# Import Localization Strings
$script:localizedData = Get-LocalizedData -ResourceName 'PowerShellGet.ResourceHelper' -ScriptRoot $PSScriptRoot

<#
    .SYNOPSIS
        This is a helper function that extract the parameters from a given table.

    .PARAMETER FunctionBoundParameters
        Specifies the hash table containing a set of parameters to be extracted.

    .PARAMETER ArgumentNames
        Specifies a list of arguments you want to extract.
#>
function New-SplatParameterHashTable {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [OutputType([System.Collections.Hashtable])]
    param
    (
        [Parameter(Mandatory = $true)]
        [System.Collections.Hashtable]
        $FunctionBoundParameters,

        [Parameter(Mandatory = $true)]
        [System.String[]]
        $ArgumentNames
    )

    Write-Verbose -Message ($script:localizedData.CallingFunction -f $($MyInvocation.MyCommand))

    $returnValue = @{}

    foreach ($arg in $ArgumentNames) {
        if ($FunctionBoundParameters.ContainsKey($arg)) {
            # Found an argument we are looking for, so we add it to return collection.
            $returnValue.Add($arg, $FunctionBoundParameters[$arg])
        }
    }

    return $returnValue
}

<#
    .SYNOPSIS
        This is a helper function that validate that a value is correct and used correctly.

    .PARAMETER Value
        Specifies the value to be validated.

    .PARAMETER Type
        Specifies the type of argument.

    .PARAMETER Type
        Specifies the name of the provider.

    .OUTPUTS
        None. Throws an error if the test fails.
#>
function Test-ParameterValue {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', '')]
    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [System.String]
        $Value,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [System.String]
        $Type,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [System.String]
        $ProviderName
    )

    Write-Verbose -Message ($script:localizedData.CallingFunction -f $($MyInvocation.MyCommand))

    switch ($Type) {
        'SourceUri' {
            # Checks whether given URI represents specific scheme
            # Most common schemes: file, http, https, ftp
            $scheme = @('http', 'https', 'file', 'ftp')

            $newUri = $Value -as [System.URI]
            $returnValue = ($newUri -and $newUri.AbsoluteURI -and ($scheme -icontains $newUri.Scheme))

            if ($returnValue -eq $false) {
                $errorMessage = $script:localizedData.InValidUri -f $Value
                New-InvalidArgumentException -ArgumentName $Type -Message $errorMessage
            }
        }

        'DestinationPath' {
            $returnValue = Test-Path -Path $Value

            if ($returnValue -eq $false) {
                $errorMessage = $script:localizedData.PathDoesNotExist -f $Value
                New-InvalidArgumentException -ArgumentName $Type -Message $errorMessage
            }
        }

        <#
        'PackageSource' {
            # Value can be either the package source Name or source Uri.
            # Check if the source is a Uri.
            $uri = $Value -as [System.URI]

            if ($uri -and $uri.AbsoluteURI) {
                # Check if it's a valid Uri.
                Test-ParameterValue -Value $Value -Type 'SourceUri' -ProviderName $ProviderName
            }
            else {
                # Check if it's a registered package source name.
               # $source = PackageManagement\Get-PackageSource -Name $Value -ProviderName $ProviderName -ErrorVariable ev

                if ((-not $source) -or $ev) {
                    # We do not need to throw error here as Get-PackageSource does already.
                    Write-Verbose -Message ($script:localizedData.SourceNotFound -f $source)
                }
            }
        } #>

        default {
            $errorMessage = $script:localizedData.UnexpectedArgument -f $Type
            New-InvalidArgumentException -ArgumentName $Type -Message $errorMessage
        }
    }
}

<#
    .SYNOPSIS
        This is a helper function that does the version validation.

    .PARAMETER Version
        Provides the version.
#>
function Test-VersionParameter {
    [CmdletBinding()]
    param
    (
        [Parameter()]
        [System.String]
        $Version
    )

    Write-Verbose -Message ($localizedData.CallingFunction -f $($MyInvocation.MyCommand))

    $isValid = $false

    # Case 1: No further check required if a user provides either none or one of these: minimumVersion, maximumVersion, and requiredVersion.
    if ($PSBoundParameters.Count -le 1) {
        return $true
    }

        ########## COME BACK HERE
    # Case 2: #If no RequiredVersion is provided.
    #if (-not $PSBoundParameters.ContainsKey('Version')) {
        # If no RequiredVersion, both MinimumVersion and MaximumVersion are provided. Otherwise fall into the Case #1.
        #$isValid = $PSBoundParameters['MinimumVersion'] -le $PSBoundParameters['MaximumVersion']
    #}
    $isValid = $true
    # Case 3: RequiredVersion is provided.
    #        In this case  MinimumVersion and/or MaximumVersion also are provided. Otherwise fall in to Case #1.
    #        This is an invalid case. When RequiredVersion is provided, others are not allowed. so $isValid is false, which is already set in the init.

    if ($isValid -eq $false) {
        $errorMessage = $script:localizedData.VersionError
        New-InvalidArgumentException `
            -ArgumentName 'RequiredVersion, MinimumVersion or MaximumVersion' `
            -Message $errorMessage
    }
}

<#
    .SYNOPSIS
        This is a helper function that retrieves the InstallationPolicy from the given repository.

    .PARAMETER RepositoryName
        Provides the repository Name.
#>
function Get-InstallationPolicy {
    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [System.String] $RepositoryName
    )

    Write-Verbose -Message ($LocalizedData.CallingFunction -f $($MyInvocation.MyCommand))

    $repositoryObject = Get-PSResourceRepository -Name $RepositoryName -ErrorAction SilentlyContinue -WarningAction SilentlyContinue

    if ($repositoryObject) {
        return $repositoryObject.IsTrusted
    }
}

<#
    .SYNOPSIS
        This method is used to compare current and desired values for any DSC resource.

    .PARAMETER CurrentValues
        This is hash table of the current values that are applied to the resource.

    .PARAMETER DesiredValues
        This is a PSBoundParametersDictionary of the desired values for the resource.

    .PARAMETER ValuesToCheck
        This is a list of which properties in the desired values list should be checked.
        If this is empty then all values in DesiredValues are checked.
#>
function Test-DscParameterState {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory = $true)]
        [System.Collections.Hashtable]
        $CurrentValues,

        [Parameter(Mandatory = $true)]
        [System.Object]
        $DesiredValues,

        [Parameter()]
        [System.Array]
        $ValuesToCheck
    )

    $returnValue = $true

    if (($DesiredValues.GetType().Name -ne 'HashTable') `
            -and ($DesiredValues.GetType().Name -ne 'CimInstance') `
            -and ($DesiredValues.GetType().Name -ne 'PSBoundParametersDictionary')) {
        $errorMessage = $script:localizedData.PropertyTypeInvalidForDesiredValues -f $($DesiredValues.GetType().Name)
        New-InvalidArgumentException -ArgumentName 'DesiredValues' -Message $errorMessage
    }

    if (($DesiredValues.GetType().Name -eq 'CimInstance') -and ($null -eq $ValuesToCheck)) {
        $errorMessage = $script:localizedData.PropertyTypeInvalidForValuesToCheck
        New-InvalidArgumentException -ArgumentName 'ValuesToCheck' -Message $errorMessage
    }

    if (($null -eq $ValuesToCheck) -or ($ValuesToCheck.Count -lt 1)) {
        $keyList = $DesiredValues.Keys
    }
    else {
        $keyList = $ValuesToCheck
    }

    $keyList | ForEach-Object -Process {
        if (($_ -ne 'Verbose')) {
            if (($CurrentValues.ContainsKey($_) -eq $false) `
                    -or ($CurrentValues.$_ -ne $DesiredValues.$_) `
                    -or (($DesiredValues.GetType().Name -ne 'CimInstance' -and $DesiredValues.ContainsKey($_) -eq $true) -and ($null -ne $DesiredValues.$_ -and $DesiredValues.$_.GetType().IsArray))) {
                if ($DesiredValues.GetType().Name -eq 'HashTable' -or `
                        $DesiredValues.GetType().Name -eq 'PSBoundParametersDictionary') {
                    $checkDesiredValue = $DesiredValues.ContainsKey($_)
                }
                else {
                    # If DesiredValue is a CimInstance.
                    $checkDesiredValue = $false
                    if (([System.Boolean]($DesiredValues.PSObject.Properties.Name -contains $_)) -eq $true) {
                        if ($null -ne $DesiredValues.$_) {
                            $checkDesiredValue = $true
                        }
                    }
                }

                if ($checkDesiredValue) {
                    $desiredType = $DesiredValues.$_.GetType()
                    $fieldName = $_
                    if ($desiredType.IsArray -eq $true) {
                        if (($CurrentValues.ContainsKey($fieldName) -eq $false) `
                                -or ($null -eq $CurrentValues.$fieldName)) {
                            Write-Verbose -Message ($script:localizedData.PropertyValidationError -f $fieldName) -Verbose

                            $returnValue = $false
                        }
                        else {
                            $arrayCompare = Compare-Object -ReferenceObject $CurrentValues.$fieldName `
                                -DifferenceObject $DesiredValues.$fieldName
                            if ($null -ne $arrayCompare) {
                                Write-Verbose -Message ($script:localizedData.PropertiesDoesNotMatch -f $fieldName) -Verbose

                                $arrayCompare | ForEach-Object -Process {
                                    Write-Verbose -Message ($script:localizedData.PropertyThatDoesNotMatch -f $_.InputObject, $_.SideIndicator) -Verbose
                                }

                                $returnValue = $false
                            }
                        }
                    }
                    else {
                        switch ($desiredType.Name) {
                            'String' {
                                if (-not [System.String]::IsNullOrEmpty($CurrentValues.$fieldName) -or `
                                        -not [System.String]::IsNullOrEmpty($DesiredValues.$fieldName)) {
                                    Write-Verbose -Message ($script:localizedData.ValueOfTypeDoesNotMatch `
                                            -f $desiredType.Name, $fieldName, $($CurrentValues.$fieldName), $($DesiredValues.$fieldName)) -Verbose

                                    $returnValue = $false
                                }
                            }

                            'Int32' {
                                if (-not ($DesiredValues.$fieldName -eq 0) -or `
                                        -not ($null -eq $CurrentValues.$fieldName)) {
                                    Write-Verbose -Message ($script:localizedData.ValueOfTypeDoesNotMatch `
                                            -f $desiredType.Name, $fieldName, $($CurrentValues.$fieldName), $($DesiredValues.$fieldName)) -Verbose

                                    $returnValue = $false
                                }
                            }

                            { $_ -eq 'Int16' -or $_ -eq 'UInt16'} {
                                if (-not ($DesiredValues.$fieldName -eq 0) -or `
                                        -not ($null -eq $CurrentValues.$fieldName)) {
                                    Write-Verbose -Message ($script:localizedData.ValueOfTypeDoesNotMatch `
                                            -f $desiredType.Name, $fieldName, $($CurrentValues.$fieldName), $($DesiredValues.$fieldName)) -Verbose

                                    $returnValue = $false
                                }
                            }

                            default {
                                Write-Warning -Message ($script:localizedData.UnableToCompareProperty `
                                        -f $fieldName, $desiredType.Name)

                                $returnValue = $false
                            }
                        }
                    }
                }
            }
        }
    }

    return $returnValue
}
