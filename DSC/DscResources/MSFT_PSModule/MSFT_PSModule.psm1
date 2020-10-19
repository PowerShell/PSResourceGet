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

$resourceModuleRoot = Split-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -Parent

# Import localization helper functions.
$helperName = 'PowerShellGet.LocalizationHelper'
$dscResourcesFolderFilePath = Join-Path -Path $resourceModuleRoot -ChildPath "Modules\$helperName\$helperName.psm1"
Import-Module -Name $dscResourcesFolderFilePath

$script:localizedData = Get-LocalizedData -ResourceName 'MSFT_PSModule' -ScriptRoot $PSScriptRoot

# Import resource helper functions.
$helperName = 'PowerShellGet.ResourceHelper'
$dscResourcesFolderFilePath = Join-Path -Path $resourceModuleRoot -ChildPath "Modules\$helperName\$helperName.psm1"
Import-Module -Name $dscResourcesFolderFilePath -Force

<#
    .SYNOPSIS
        This DSC resource provides a mechanism to download PowerShell modules from the PowerShell
        Gallery and install it on your computer.

        Get-TargetResource returns the current state of the resource.

    .PARAMETER Name
        Specifies the name of the PowerShell module to be installed or uninstalled.

    .PARAMETER Repository
        Specifies the name of the module source repository where the module can be found.

    .PARAMETER Version
        Provides the version of the module you want to install or uninstall.

    .PARAMETER NoClobber
        Does not allow the installation of modules if other existing module on the computer have cmdlets
        of the same name.

    .PARAMETER SkipPublisherCheck
        Allows the installation of modules that have not been catalog signed.
#>
function Get-TargetResource {
    <#
        These suppressions are added because this repository have other Visual Studio Code workspace
        settings than those in DscResource.Tests DSC test framework.
        Only those suppression that contradict this repository guideline is added here.
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('DscResource.AnalyzerRules\Measure-ForEachStatement', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('DscResource.AnalyzerRules\Measure-FunctionBlockBraces', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('DscResource.AnalyzerRules\Measure-IfStatement', '')]
    [CmdletBinding()]
    [OutputType([System.Collections.Hashtable])]
    param
    (
        [Parameter(Mandatory = $true)]
        [System.String]
        $Name,

        [Parameter()]
        [System.String]
        $Repository = 'PSGallery',

        [Parameter()]
        [System.String]
        $Version,

        [Parameter()]
        [System.Boolean]
        $NoClobber,

        [Parameter()]
        [System.Boolean]
        $SkipPublisherCheck
    )

    $returnValue = @{
        Ensure             = 'Absent'
        Name               = $Name
        Repository         = $Repository
        Priority           = $null
        Description        = $null
        Guid               = $null
        ModuleBase         = $null
        ModuleType         = $null
        Author             = $null
        InstalledVersion   = $null
        Version            = $Version
        NoClobber          = $NoClobber
        SkipPublisherCheck = $SkipPublisherCheck
        InstallationPolicy = $null
        Trusted            = $false
    }

    Write-Verbose -Message ($localizedData.GetTargetResourceMessage -f $Name)
    Write-Verbose("Name:")

    Write-Verbose("Name: $Name")
    Write-Verbose("Repository: $Repository")
    Write-Verbose("Version: $Version")

    $extractedArguments = New-SplatParameterHashTable -FunctionBoundParameters $PSBoundParameters `
    -ArgumentNames ('Name', 'Repository', 'Version')

    # Get the module with the right version and repository properties.
    $modules = Get-RightModule @extractedArguments -ErrorAction SilentlyContinue -WarningAction SilentlyContinue

    # If the module is found, the count > 0
    if ($modules.Count -gt 0) {
        Write-Verbose -Message ($localizedData.ModuleFound -f $Name)

        # Find a module with the latest version and return its properties.
        $latestModule = $modules[0]

        foreach ($module in $modules) {
            if ($module.Version -gt $latestModule.Version) {
                $latestModule = $module
            }
        }

        # Check if the repository matches.
        $repositoryName = Get-ModuleRepositoryName -Module $latestModule -ErrorAction SilentlyContinue -WarningAction SilentlyContinue

        ##if ($repositoryName) {
          ## $installationPolicy = Get-InstallationPolicy -RepositoryName $repositoryName -ErrorAction SilentlyContinue -WarningAction SilentlyContinue
        ##}

        ##if ($installationPolicy) {
        ##    $installationPolicyReturnValue = 'Trusted'
        ##    $Trusted = $true
        ##}
        ##else {
            ##$installationPolicyReturnValue = 'Untrusted'
            ##$Trusted = $false
        ##}

        Write-Verbose("returning value")
        $returnValue.Ensure = 'Present'
        $returnValue.Repository = $repositoryName
        $returnValue.Description = $latestModule.Description
        $returnValue.Guid = $latestModule.Guid
        $returnValue.ModuleBase = $latestModule.ModuleBase
        $returnValue.ModuleType = $latestModule.ModuleType
        $returnValue.Author = $latestModule.Author
        $returnValue.InstalledVersion = $latestModule.Version
        $returnValue.InstallationPolicy = $installationPolicyReturnValue
        $returnValue.Trusted = $trusted
    }
    else {
        Write-Verbose -Message ($localizedData.ModuleNotFound -f $Name)
    }

    return $returnValue
}

<#
    .SYNOPSIS
        This DSC resource provides a mechanism to download PowerShell modules from the PowerShell
        Gallery and install it on your computer.

        Test-TargetResource validates whether the resource is currently in the desired state.

    .PARAMETER Ensure
        Determines whether the module to be installed or uninstalled.

    .PARAMETER Name
        Specifies the name of the PowerShell module to be installed or uninstalled.

    .PARAMETER Repository
        Specifies the name of the module source repository where the module can be found.

    .PARAMETER InstallationPolicy
        Determines whether you trust the source repository where the module resides.

    .PARAMETER Trusted
        Determines whether you trust the source repository where the module resides.

    .PARAMETER Version
        Provides the version of the module you want to install or uninstall.

    .PARAMETER NoClobber
        Does not allow the installation of modules if other existing module on the computer have cmdlets
        of the same name.

    .PARAMETER SkipPublisherCheck
        Allows the installation of modules that have not been catalog signed.
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
        $Repository = 'PSGallery',

        [Parameter()]
        [ValidateSet('Trusted', 'Untrusted')]
        [System.String]
        $InstallationPolicy = 'Untrusted',

        [Parameter()]
        [System.Boolean]
        $Trusted = $false,

        [Parameter()]
        [System.String]
        $Version,

        [Parameter()]
        [System.Boolean]
        $NoClobber,

        [Parameter()]
        [System.Boolean]
        $SkipPublisherCheck
    )

    Write-Verbose -Message ($localizedData.TestTargetResourceMessage -f $Name)

    $extractedArguments = New-SplatParameterHashTable -FunctionBoundParameters $PSBoundParameters `
    -ArgumentNames ('Name', 'Repository', 'Version')

    $status = Get-TargetResource @extractedArguments

    # The ensure returned from Get-TargetResource is not equal to the desired $Ensure.
    if ($status.Ensure -ieq $Ensure) {
        Write-Verbose -Message ($localizedData.InDesiredState -f $Name)
        return $true
    }
    else {
        Write-Verbose -Message ($localizedData.NotInDesiredState -f $Name)
        return $false
    }
}

<#
    .SYNOPSIS
        This DSC resource provides a mechanism to download PowerShell modules from the PowerShell
        Gallery and install it on your computer.

        Set-TargetResource sets the resource to the desired state. "Make it so".

    .PARAMETER Ensure
        Determines whether the module to be installed or uninstalled.

    .PARAMETER Name
        Specifies the name of the PowerShell module to be installed or uninstalled.

    .PARAMETER Repository
        Specifies the name of the module source repository where the module can be found.

    .PARAMETER InstallationPolicy
        Determines whether you trust the source repository where the module resides.

    .PARAMETER Trusted
        Determines whether you trust the source repository where the module resides.

    .PARAMETER Version
        Provides the version of the module you want to install or uninstall.

    .PARAMETER NoClobber
        Does not allow the installation of modules if other existing module on the computer have cmdlets
        of the same name.

    .PARAMETER SkipPublisherCheck
        Allows the installation of modules that have not been catalog signed.
#>
function Set-TargetResource {
    <#
        These suppressions are added because this repository have other Visual Studio Code workspace
        settings than those in DscResource.Tests DSC test framework.
        Only those suppression that contradict this repository guideline is added here.
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('DscResource.AnalyzerRules\Measure-ForEachStatement', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('DscResource.AnalyzerRules\Measure-FunctionBlockBraces', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('DscResource.AnalyzerRules\Measure-IfStatement', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('DscResource.AnalyzerRules\Measure-TryStatement', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('DscResource.AnalyzerRules\Measure-CatchClause', '')]
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
        $Repository = 'PSGallery',

        [Parameter()]
        [ValidateSet('Trusted', 'Untrusted')]
        [System.String]
        $InstallationPolicy = 'Untrusted',

        [Parameter()]
        [System.Boolean]
        $Trusted = $false,

        [Parameter()]
        [System.String]
        $Version,

        [Parameter()]
        [System.Boolean]
        $NoClobber,

        [Parameter()]
        [System.Boolean]
        $SkipPublisherCheck
    )

    # Validate the repository argument
    if ($PSBoundParameters.ContainsKey('Repository')) {
        #Test-ParameterValue -Value $Repository -Type 'PackageSource' -Verbose
    }

    if ($Ensure -ieq 'Present') {
        # Version check
        $extractedArguments = New-SplatParameterHashTable -FunctionBoundParameters $PSBoundParameters `
        -ArgumentNames ('Version')

       # $null = Test-VersionParameter @extractedArguments

       $trusted = $null
       $moduleFound = $null

        try {
            $extractedArguments = New-SplatParameterHashTable -FunctionBoundParameters $PSBoundParameters `
            -ArgumentNames ('Name', 'Repository', 'Version')

            Write-Verbose -Message ($localizedData.StartFindModule -f $Name)
            Write-verbose ("Name is: $name")
            Write-verbose ("Repository is: $repository")
            Write-verbose ("Version is: $Version")
            
            $modules = Find-PSResource @extractedArguments -ErrorVariable ev -ErrorAction SilentlyContinue

            Write-verbose ("modules is: $modules")
            $moduleFound = $modules[0]
        }
        catch {
            $errorMessage = $script:localizedData.ModuleNotFoundInRepository -f $Name
            New-InvalidOperationException -Message $errorMessage -ErrorRecord $_
        }


        foreach ($m in $modules) {
            # Check for the installation policy.
            #$trusted = Get-InstallationPolicy -RepositoryName $m.Repository -ErrorAction SilentlyContinue -WarningAction SilentlyContinue

            # Stop the loop if found a trusted repository.
            #if ($trusted) {
               # $moduleFound = $m
               # break;
            #}
        }

        try {
            # The repository is trusted, so we install it.
            #if ($trusted) {
            #    Write-Verbose -Message ($localizedData.StartInstallModule -f $Name, $moduleFound.Version.toString(), $moduleFound.Repository)

                # Extract the installation options.
                ## $extractedSwitches = New-SplatParameterHashTable -FunctionBoundParameters $PSBoundParameters -ArgumentNames ('Force', 'AllowClobber', 'SkipPublisherCheck')

                ## $moduleFound | Install-Module @extractedSwitches 2>&1 | out-string | Write-Verbose
            Install-PSResource -name $moduleFound.Name -Repository $moduleFound.Repository -version $moduleFound.Version -TrustRepository:$true -NoClobber:$NoClobber -Verbose #$SkipPublisherCheck,

            ##}
            # The repository is untrusted but user's installation policy is trusted, so we install it with a warning.
            ##elseif ($InstallationPolicy -ieq $true) {
            ##    Write-Warning -Message ($localizedData.InstallationPolicyWarning -f $Name, $modules[0].Repository, $InstallationPolicy)

                # Extract installation options (Force implied by InstallationPolicy).
                ## $extractedSwitches = New-SplatParameterHashTable -FunctionBoundParameters $PSBoundParameters -ArgumentNames ('AllowClobber', 'SkipPublisherCheck')

                # If all the repositories are untrusted, we choose the first one.
                ## $modules[0] | Install-Module @extractedSwitches -Force 2>&1 | out-string | Write-Verbose
           ## }
            # Both user and repository is untrusted
            ##else {
            ##    $errorMessage = $script:localizedData.InstallationPolicyFailed -f $InstallationPolicy, 'Untrusted'
            ##    New-InvalidOperationException -Message $errorMessage
            ##}

            Write-Verbose -Message ($localizedData.InstalledSuccess -f $Name)
        }
        catch {
            $errorMessage = $script:localizedData.FailToInstall -f $Name
            New-InvalidOperationException -Message $errorMessage -ErrorRecord $_
        }
    }
    # Ensure=Absent
    else {

        $extractedArguments = New-SplatParameterHashTable -FunctionBoundParameters $PSBoundParameters `
        -ArgumentNames ('Name', 'Repository', 'Version')

        # Get the module with the right version and repository properties.
        $modules = Get-RightModule @extractedArguments

        if (-not $modules) {
            $errorMessage = $script:localizedData.ModuleWithRightPropertyNotFound -f $Name
            New-InvalidOperationException -Message $errorMessage
        }

        foreach ($module in $modules) {
            # Get the path where the module is installed.
            $path = $module.ModuleBase

            Write-Verbose -Message ($localizedData.StartUnInstallModule -f $Name)

            try {
                <#
                    There is no Uninstall-Module cmdlet for Windows PowerShell 4.0,
                    so we will remove the ModuleBase folder as an uninstall operation.
                #>
                Microsoft.PowerShell.Management\Remove-Item -Path $path -Force -Recurse

                Write-Verbose -Message ($localizedData.UnInstalledSuccess -f $module.Name)
            }
            catch {
                $errorMessage = $script:localizedData.FailToUninstall -f $module.Name
                New-InvalidOperationException -Message $errorMessage -ErrorRecord $_
            }
        } # foreach
    } # Ensure=Absent
}

<#
    .SYNOPSIS
        This is a helper function. It returns the modules that meet the specified versions and the repository requirements.

    .PARAMETER Name
        Specifies the name of the PowerShell module.

    .PARAMETER Version
        Provides the version of the module you want to install or uninstall.

    .PARAMETER Repository
        Specifies the name of the module source repository where the module can be found.
#>
function Get-RightModule {
    <#
        These suppressions are added because this repository have other Visual Studio Code workspace
        settings than those in DscResource.Tests DSC test framework.
        Only those suppression that contradict this repository guideline is added here.
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('DscResource.AnalyzerRules\Measure-ForEachStatement', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('DscResource.AnalyzerRules\Measure-FunctionBlockBraces', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('DscResource.AnalyzerRules\Measure-IfStatement', '')]
    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [System.String]
        $Name,

        [Parameter()]
        [System.String]
        $Version,

        [Parameter()]
        [System.String]
        $Repository
    )

    Write-Verbose -Message ($localizedData.StartGetModule -f $($Name))

    $modules = Microsoft.PowerShell.Core\Get-Module -Name $Name -ListAvailable -ErrorAction SilentlyContinue -WarningAction SilentlyContinue

    if (-not $modules) {
        return $null
    }

    <#
        As Get-Module does not take RequiredVersion, MinimumVersion, MaximumVersion, or Repository,
        below we need to check whether the modules are containing the right version and repository
        location.
    #>

    $extractedArguments = New-SplatParameterHashTable -FunctionBoundParameters $PSBoundParameters `
    -ArgumentNames ('Version')
    $returnVal = @()

    foreach ($m in $modules) {
        $versionMatch = $false
        $installedVersion = $m.Version

        # Case 1 - a user provides none of RequiredVersion, MinimumVersion, MaximumVersion
        if ($extractedArguments.Count -eq 0) {
            $versionMatch = $true
        }

        ########### COME BACK HERE
        # Case 2 - a user provides RequiredVersion
        elseif ($extractedArguments.ContainsKey('Version')) {
            # Check if it matches with the installed version
            $versionMatch = ($installedVersion -eq [System.Version] $RequiredVersion)
        }
        <#
        else {

            # Case 3 - a user provides MinimumVersion
            if ($extractedArguments.ContainsKey('MinimumVersion')) {
                $versionMatch = ($installedVersion -ge [System.Version] $extractedArguments['MinimumVersion'])
            }

            # Case 4 - a user provides MaximumVersion
            if ($extractedArguments.ContainsKey('MaximumVersion')) {
                $isLessThanMax = ($installedVersion -le [System.Version] $extractedArguments['MaximumVersion'])

                if ($extractedArguments.ContainsKey('MinimumVersion')) {
                    $versionMatch = $versionMatch -and $isLessThanMax
                }
                else {
                    $versionMatch = $isLessThanMax
                }
            }

            # Case 5 - Both MinimumVersion and MaximumVersion are provided. It's covered by the above.
            # Do not return $false yet to allow the foreach to continue
            if (-not $versionMatch) {
                Write-Verbose -Message ($localizedData.VersionMismatch -f $Name, $installedVersion)
                $versionMatch = $false
            }
        }
        #>

        # Case 6 - Version matches but need to check if the module is from the right repository.
        if ($versionMatch) {
            # A user does not provide Repository, we are good
            if (-not $PSBoundParameters.ContainsKey('Repository')) {
                Write-Verbose -Message ($localizedData.ModuleFound -f "$Name $installedVersion")
                $returnVal += $m
            }
            else {
                # Check if the Repository matches
                $sourceName = Get-ModuleRepositoryName -Module $m

                if ($Repository -ieq $sourceName) {
                    Write-Verbose -Message ($localizedData.ModuleFound -f "$Name $installedVersion")
                    $returnVal += $m
                }
                else {
                    Write-Verbose -Message ($localizedData.RepositoryMismatch -f $($Name), $($sourceName))
                }
            }
        }
    } # foreach

    return $returnVal
}

<#
    .SYNOPSIS
        This is a helper function that returns the module's repository name.

    .PARAMETER Module
        Specifies the name of the PowerShell module.
#>
function Get-ModuleRepositoryName {
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
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [System.Object]
        $Module
    )

    <#
        RepositorySourceLocation property is supported in PS V5 only. To work with the earlier
        PowerShell version, we need to do a different way. PSGetModuleInfo.xml exists for any
        PowerShell modules downloaded through PSModule provider.
    #>
    $psGetModuleInfoFileName = 'PSGetModuleInfo.xml'
    $psGetModuleInfoPath = Microsoft.PowerShell.Management\Join-Path -Path $Module.ModuleBase -ChildPath $psGetModuleInfoFileName

    Write-Verbose -Message ($localizedData.FoundModulePath -f $psGetModuleInfoPath)

    if (Microsoft.PowerShell.Management\Test-path -Path $psGetModuleInfoPath) {
        $psGetModuleInfo = Microsoft.PowerShell.Utility\Import-Clixml -Path $psGetModuleInfoPath

        return $psGetModuleInfo.Repository
    }
}
