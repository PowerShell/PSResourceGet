---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
ms.date: 08/03/2022
online version:
schema: 2.0.0
---

# Update-ModuleManifest

## SYNOPSIS
Updates a module manifest file.

## SYNTAX

### __AllParameterSets

```
Update-ModuleManifest [-Path] <string> [-NestedModules <Object[]>] [-Guid <guid>]
 [-Author <string>] [-CompanyName <string>] [-Copyright <string>] [-RootModule <string>]
 [-ModuleVersion <version>] [-Description <string>] [-ProcessorArchitecture <ProcessorArchitecture>]
 [-CompatiblePSEditions <string[]>] [-PowerShellVersion <version>] [-ClrVersion <version>]
 [-DotNetFrameworkVersion <version>] [-PowerShellHostName <string>]
 [-PowerShellHostVersion <version>] [-RequiredModules <Object[]>] [-TypesToProcess <string[]>]
 [-FormatsToProcess <string[]>] [-ScriptsToProcess <string[]>] [-RequiredAssemblies <string[]>]
 [-FileList <string[]>] [-ModuleList <Object[]>] [-FunctionsToExport <string[]>]
 [-AliasesToExport <string[]>] [-VariablesToExport <string[]>] [-CmdletsToExport <string[]>]
 [-DscResourcesToExport <string[]>] [-Tags <string[]>] [-ProjectUri <uri>] [-LicenseUri <uri>]
 [-IconUri <uri>] [-ReleaseNotes <string>] [-Prerelease <string>] [-HelpInfoUri <uri>] [-PassThru]
 [-DefaultCommandPrefix <string>] [-ExternalModuleDependencies <string[]>]
 [-RequireLicenseAcceptance] [-PrivateData <hashtable>] [<CommonParameters>]
```

## DESCRIPTION

This cmdlet updates the data stored in a module manifest file. The parameters allow you to specify
which properties get updated. `Update-ModuleManifest` overwrites any existing values in the module
manifest.

The cmdlet doesn't return an object.

## EXAMPLES

### Example 1

This example changes the **Author** property in the module manifest to `New Author`.

```powershell
Update-ModuleManifest -Path "C:\MyModules\TestModule" -Author "New Author"
```

### Example 2

This example changes the **Prerelease** property to `beta2`.

```powershell
Update-ModuleManifest -Path "C:\MyModules\TestModule" -Prerelease "beta2"
```

### Example 3

This example updates multiple properties.

```powershell
Update-ModuleManifest -Path "C:\MyModules\TestModule" -Tags "Windows", "Linux" -Description "A module for managing packages."
```

## PARAMETERS

### -AliasesToExport

Specifies the aliases that the module exports. Wildcards are permitted.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Author

Specifies the module author.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClrVersion

Specifies the minimum version of the Common Language Runtime (CLR) of the Microsoft .NET Framework
required by the module.

```yaml
Type: System.Version
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CmdletsToExport

Specifies the cmdlets that the module exports. Wildcards are permitted.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -CompanyName

Specifies the company or vendor who created the module.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CompatiblePSEditions

Specifies the compatible **PSEditions** of the module. For information about **PSEdition**, see
[Modules with compatible PowerShell Editions](/powershell/scripting/gallery/concepts/module-psedition-support).

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:
Accepted values: Desktop, Core

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Copyright

Specifies a copyright statement for the module.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DefaultCommandPrefix

Specifies the default command prefix.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Description

Specifies a description of the module.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DotNetFrameworkVersion

Specifies the minimum version of the Microsoft .NET Framework required by the module.

```yaml
Type: System.Version
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DscResourcesToExport

Specifies the Desired State Configuration (DSC) resources that the module exports. Wildcards are
permitted.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ExternalModuleDependencies

Specifies an array of external module dependencies.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FileList

Specifies all items that are included in the module.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FormatsToProcess

Specifies the formatting files (`.ps1xml`) that are processed when the module is imported.

When you import a module, PowerShell runs the `Update-FormatData` cmdlet with the specified files.
Because formatting files aren't scoped, they affect all session states in the session.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FunctionsToExport

Specifies the functions that the module exports. Wildcards are permitted.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Guid

Specifies a unique identifier for the module. The **GUID** is used to distinguish between modules
with the same name.

```yaml
Type: System.Guid
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HelpInfoUri

Specifies the internet address of the module's HelpInfo XML file. Enter a Uniform Resource
Identifier (URI) that begins with `http:` or `https:`.

For more information, see
[Updatable Help](/powershell/module/microsoft.powershell.core/about/about_updatable_help).

```yaml
Type: System.Uri
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IconUri

Specifies the URI of an icon for the module. The specified icon is displayed on the gallery web page
for the module.

```yaml
Type: System.Uri
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LicenseUri

Specifies the URL of licensing terms for the module.

```yaml
Type: System.Uri
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ModuleList

Specifies an array of modules that are included in the module.

Enter each module name as a string or as a hashtable with **ModuleName** and **ModuleVersion** keys.
The hashtable can also have an optional **GUID** key. You can combine strings and hashtables in the
parameter value.

This key is designed to act as a module inventory.

```yaml
Type: System.Object[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ModuleVersion

Specifies the version of the module.

```yaml
Type: System.Version
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NestedModules

Specifies script modules (`.psm1`) and binary modules (`.dll`) that are imported into the module's
session state. The files in the **NestedModules** key run in the order in which they're listed.

Enter each module name as a string or as a hashtable with **ModuleName** and **ModuleVersion** keys.
The hashtable can also have an optional **GUID** key. You can combine strings and hashtables in the
parameter value.

```yaml
Type: System.Object[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path

Specifies the path and filename of the module manifest. Enter filename with a `.psd1` file
extension.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PowerShellHostName

Specifies the name of the PowerShell host program that the module requires. Enter the name of the
host program, such as PowerShell ISE Host or ConsoleHost. Wildcards aren't permitted.

The name of a host program is stored in `$Host.Name`.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PowerShellHostVersion

Specifies the minimum version of the PowerShell host program that works with the module. Enter a
version number, such as 1.1.

```yaml
Type: System.Version
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PowerShellVersion

Specifies the minimum version of PowerShell that works with this module. For example, you can
specify versions such as `5.1` or `7.2`.

```yaml
Type: System.Version
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Prerelease

Specifies the prerelease value that is appended to the module version. For example, if
**Prerelease** is `preview` and the **ModuleVersion** is `1.0.0`, the version of the module is
`1.0.0-preview`.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PrivateData

Specifies data that is passed to the module when it's imported. This can be any arbitrary values
stored in a hashtable.

```yaml
Type: System.Collections.Hashtable
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProcessorArchitecture

Specifies the processor architecture that the module requires.

The acceptable values for this parameter are:

- `Amd64`
- `Arm`
- `IA64`
- `MSIL`
- `None` (unknown or unspecified)
- `X86`

```yaml
Type: System.Reflection.ProcessorArchitecture
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProjectUri

Specifies the URI of a web page about this project.

```yaml
Type: System.Uri
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReleaseNotes

Specifies a string that contains release notes or comments for the module.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequiredAssemblies

Specifies the assembly (`.dll`) files required by the module. PowerShell loads the specified
assemblies before updating types or formats, importing nested modules, or importing the module file
specified in the **RootModule** key.

Use **RequiredAssemblies** for assemblies that must be loaded to update any formatting or type files
that are listed in the **FormatsToProcess** or **TypesToProcess** keys, even if those assemblies are
also listed in the **NestedModules** key.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequiredModules

Specifies modules that must be in the global session state. If the required modules aren't in the
global session state, PowerShell imports them. If the required modules aren't available, the
`Import-Module` command fails.

The value can be an array containing module names or module specifications. A module specification
is a hashtable that has the following keys.

- `ModuleName` - **Required** Specifies the module name.
- `GUID` - **Optional** Specifies the GUID of the module.
- It's also **Required** to specify at least one of the three below keys.
  - `ModuleVersion` - Specifies a minimum acceptable version of the module.
  - `MaximumVersion` - Specifies the maximum acceptable version of the module.
  - `RequiredVersion` - Specifies an exact, required version of the module. This can't be used with
    the other Version keys.

```yaml
Type: System.Object[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequireLicenseAcceptance

Specifies that a license acceptance is required for the module.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -RootModule

Specifies the primary or root file of the module. Enter the file name of a script (`.ps1`), a script
module (`.psm1`), a module manifest (`.psd1`), an assembly (`.dll`), or a cmdlet definition XML file
(`.cdxml`). When the module is imported, the members exported from the root module are imported into
the caller's session state.

If a module has a manifest file and no file is specified in the **RootModule** key, the manifest
becomes the primary file for the module. The module is known as a manifest module (**ModuleType** =
`Manifest`).

To export members from `.psm1` or `.dll` files, the names of those files must be specified in the
values of the **RootModule** or **NestedModules** keys in the manifest.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ScriptsToProcess

Specifies script (`.ps1`) files that run in the caller's session state when the module is imported.
You can use these scripts to prepare an environment, just as you might use a login script.

To specify scripts that run in the module's session state, use the **NestedModules** key.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Tags

Specifies an array of tags.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TypesToProcess

Specifies the type files (`.ps1xml`) that run when the module is imported.

When you import the module, PowerShell runs the `Update-TypeData` cmdlet with the specified files.
Because type files aren't scoped, they affect all session states in the session.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -VariablesToExport

Specifies the variables that the module exports. Wildcards are permitted.

Use this parameter to restrict which variables that are exported by the module.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose,
-WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String[]

## OUTPUTS

## NOTES

For a full description of the module manifest file, see
[about_Module_Manifests](/powershell/module/microsoft.powershell.core/about/about_module_manifests).

## RELATED LINKS

[New-ModuleManifest](/powershell/module/microsoft.powershell.core/new-modulemanifest)
