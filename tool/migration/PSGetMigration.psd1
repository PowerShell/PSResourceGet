# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

@{
    RootModule        = 'PSGetMigration.psm1'
    ModuleVersion     = '0.1.0'
    GUID              = 'a3f7b2c1-4d5e-6f78-9a0b-c1d2e3f4a5b6'
    Author            = 'Microsoft Corporation'
    CompanyName       = 'Microsoft Corporation'
    Copyright         = '(c) Microsoft Corporation. All rights reserved.'
    Description       = 'Migration tool to convert PowerShellGet v2 (PSGet) cmdlet usage to PSResourceGet equivalents.'
    PowerShellVersion = '5.1'
    FunctionsToExport = @(
        'Get-PSGetCommandMapping',
        'Get-PSGetParameterMapping',
        'Find-PSGetCommand',
        'Convert-PSGetCommand',
        'ConvertTo-PSResourceGetScript',
        'ConvertTo-PSResourceGetString',
        'Format-MigrationReport'
    )
    CmdletsToExport   = @()
    VariablesToExport  = @()
    AliasesToExport    = @()
    PrivateData = @{
        PSData = @{
            Tags       = @('Migration', 'PSResourceGet', 'PowerShellGet', 'PSGet')
            ProjectUri = 'https://github.com/PowerShell/PSResourceGet'
        }
    }
}
