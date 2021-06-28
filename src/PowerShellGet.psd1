@{
    RootModule        = './netstandard2.0/PowerShellGet.dll'
    ModuleVersion     = '3.0.0'
    GUID              = '1d73a601-4a6c-43c5-ba3f-619b18bbb404'
    Author            = 'Microsoft Corporation'
    CompanyName       = 'Microsoft Corporation'
    Copyright         = '(c) Microsoft Corporation. All rights reserved.'
    Description       = 'PowerShell module with commands for discovering, installing, updating and publishing the PowerShell artifacts like Modules, Scripts, and DSC Resources.'
    PowerShellVersion = '5.1'
    DotNetFrameworkVersion = '2.0'
    CLRVersion = '4.0.0'
    FormatsToProcess  = 'PSGet.Format.ps1xml'
    CmdletsToExport = @(
        'Find-PSResource',
        'Get-InstalledPSResource',
        'Get-PSResourceRepository',
        # 'Install-PSResource',
        'Register-PSResourceRepository',
        # 'Save-PSResource',
        'Set-PSResourceRepository',
        'Publish-PSResource',
        'Uninstall-PSResource',
        'Unregister-PSResourceRepository',
        'Update-PSResource')

    VariablesToExport = 'PSGetPath'
    AliasesToExport = @('inmo', 'fimo', 'upmo', 'pumo')
    PrivateData = @{
        PSData = @{
            Prerelease = 'beta10'
            Tags = @('PackageManagement',
                'PSEdition_Desktop',
                'PSEdition_Core',
                'Linux',
                'Mac',
                'Windows')
            ProjectUri = 'https://go.microsoft.com/fwlink/?LinkId=828955'
            LicenseUri = 'https://go.microsoft.com/fwlink/?LinkId=829061'
            ReleaseNotes = @'
### 3.0.0-beta11
'@
        }
    }

    HelpInfoURI       = 'http://go.microsoft.com/fwlink/?linkid=855963'
}
