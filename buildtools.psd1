# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

@{
    # Script module or binary module file associated with this manifest.
    RootModule = '.\buildtools.psm1'
    
    # Version number of this module.
    ModuleVersion = '1.0.0'
    
    # Supported PSEditions
    CompatiblePSEditions = @('Core')
    
    # ID used to uniquely identify this module
    GUID = 'fcdd259e-1163-4da2-8bfa-ce36a839f337'
    
    # Author of this module
    Author = 'Microsoft Corporation'
    
    # Company or vendor of this module
    CompanyName = 'Microsoft Corporation'
    
    # Copyright statement for this module
    Copyright = '(c) Microsoft Corporation. All rights reserved.'
    
    # Description of the functionality provided by this module
    Description = "Build utilties."

    # Modules that must be imported into the global environment prior to importing this module
    #RequiredModules = @(
    #    @{ ModuleName = 'platyPS'; ModuleVersion = '0.14.0' },
    #    @{ ModuleName = 'Pester'; ModuleVersion = '4.8.1' },
    #    @{ ModuleName = 'PSScriptAnalyzer'; ModuleVersion = '1.18.0' }
    #)
    
    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion = '5.1'
    
    # Cmdlets to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no cmdlets to export.
    CmdletsToExport = @()
    
    # Functions to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no functions to export.
    FunctionsToExport = @(
        'Get-BuildConfiguration', 'Invoke-ModuleBuild', 'Publish-ModulePackage', 'Install-ModulePackageForTest', 'Invoke-ModuleTests')
    
    # Variables to export from this module
    VariablesToExport = '*'
    
    # Aliases to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no aliases to export.
    AliasesToExport = @()
}
