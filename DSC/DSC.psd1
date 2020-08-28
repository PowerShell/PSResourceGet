<#
    This is a dummy PowerShell manifest file so that the DscResource.Tests
    test framework recognize the module folder as correct (expected) folder
    and file structure.
    THIS FILE IS NOT USE DURING DEPLOYMENT.
#>
@{
    # Version number of this module.
    moduleVersion      = '0.0.0.1'

    # ID used to uniquely identify this module
    GUID               = 'e102ebd2-bdc3-4d0f-bc93-4b8cc3eb7074'

    # Author of this module
    Author             = 'Microsoft Corporation'

    # Company or vendor of this module
    CompanyName        = 'Microsoft Corporation'

    # Copyright statement for this module
    Copyright          = '(c) 2019 Microsoft Corporation. All rights reserved.'

    # Description of the functionality provided by this module
    Description        = 'Module with DSC Resources for deployment of PowerShell modules.'

    # Minimum version of the Windows PowerShell engine required by this module
    PowerShellVersion  = '5.0'

    # Minimum version of the common language runtime (CLR) required by this module
    CLRVersion         = '4.0'

    # Functions to export from this module
    FunctionsToExport  = @()

    # Cmdlets to export from this module
    CmdletsToExport    = @()

    RequiredAssemblies = @()

    <#
        Private data to pass to the module specified in RootModule/ModuleToProcess.
        This may also contain a PSData hashtable with additional module metadata used by PowerShell.
    #>
    PrivateData        = @{

        PSData = @{

            # Tags applied to this module. These help with module discovery in online galleries.
            Tags         = @('DesiredStateConfiguration', 'DSC', 'DSCResource')

            # A URL to the license for this module.
            LicenseUri   = 'https://github.com/PowerShell/PowerShellGet/blob/master/LICENSE'

            # A URL to the main website for this project.
            ProjectUri   = 'https://github.com/PowerShell/PowerShellGet'

            # ReleaseNotes of this module
            ReleaseNotes = ''

        } # End of PSData hashtable

    } # End of PrivateData hashtable
}
