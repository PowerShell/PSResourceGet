---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version:
schema: 2.0.0
---

# Update-ModuleManifest

## SYNOPSIS
Updates a module manifest file.

## SYNTAX

### NameParameterSet (Default)
```
Update-ModuleManifest [-Path] <String> [-NestedModules <object[]>] [-Guid <Guid>] [-Author <String>]
 [-CompanyName <String>] [-Copyright <String>] [-RootModule <String>] [-ModuleVersion <Version>] 
 [-Description <String>] [-ProcessorArchitecture <ProcessorArchitecture>] [-CompatiblePSEditions <String[]>] 
 [-PowerShellVersion <Version>] [-ClrVersion <Version>] [-DotNetFrameworkVersion <Version>] 
 [-PowerShellHostName <String>] [-PowerShellHostVersion <Version>] [-RequiredModules <Object[]>] 
 [-TypesToProcess <String[]>] [-FormatsToProcess <String[]>] [-ScriptsToProcess <String[]>] 
 [-RequiredAssemblies <String[]>] [-FileList <String[]>] [-ModuleList <Object[]>] [-FunctionsToExport <String[]>]
 [-AliasesToExport <String[]>] [-VariablesToExport <String[]>] [-CmdletsToExport <String[]>] 
 [-DscResourcesToExport <String[]>] [-PrivateData <Hashtable>] [-Tags <String[]>] [-ProjectUri <Uri>] 
 [-LicenseUri <Uri>] [-IconUri <Uri>] [-ReleaseNotes <String[]>] [-Prerelease <String>] [-HelpInfoUri <Uri>] 
 [-DefaultCommandPrefix <String>] [-ExternalModuleDependencies <String[]>] [-RequireLicenseAcceptance] 
 [<CommonParameters>]
```

## DESCRIPTION
The Update-ModuleManifest cmdlet replaces the Update-ModuleManifest cmdlet from V2.
It updates a module manifest based on the `-Path` parameter argument.
It does not return an object. Other parameters allow specific properties of the manifest to be updated.

## EXAMPLES

### Example 1
```powershell
PS C:\> Update-ModuleManifest -Path "C:\MyModules\TestModule" -Author "New Author"
```
In this example the author property in the module manifest will be updated to "New Author".

```powershell
PS C:\>  Update-ModuleManifest -Path "C:\MyModules\TestModule" -Prerelease "beta2"
```
In this example the prerelease property will be updated to "beta2"

```powershell
PS C:\> Update-ModuleManifest -Path "C:\MyModules\TestModule" -Tags "Windows", "Linux" -Description "A module for managing packages."
```
In this example the tags and description will be updated to the passed in values.

In this example, the user already has the TestModule package installed and they update the package. Update-PSResource will install the latest version of the package without deleting the older version installed.

## PARAMETERS

### -Path
Specifies script modules (.psm1) and binary modules (.dll) that are imported into the module's session state. The files in the NestedModules key run in the order in which they're listed in the value.

Enter each module name as a string or as a hash table with ModuleName and ModuleVersion keys. The hash table can also have an optional GUID key. You can combine strings and hash tables in the parameter value.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: True
Accept wildcard characters: False
```

### -NestedModules 
Specifies script modules (.psm1) and binary modules (.dll) that are imported into the module's session state. The files in the NestedModules key run in the order in which they're listed in the value.

Enter each module name as a string or as a hash table with ModuleName and ModuleVersion keys. The hash table can also have an optional GUID key. You can combine strings and hash tables in the parameter value.

```yaml
Type: Object[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Guid
Specifies a unique identifier for the module. The GUID can be used to distinguish among modules with the same name.

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

### -Author 
Specifies the module author.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CompanyName
Specifies the company or vendor who created the module. 

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Copyright
Specifies a copyright statement for the module.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RootModule
Specifies the primary or root file of the module. Enter the file name of a script (.ps1), a script module (.psm1), a module manifest (.psd1), an assembly (.dll), a cmdlet definition XML file (.cdxml), or a workflow (.xaml). When the module is imported, the members that are exported from the root module file are imported into the caller's session state.

If a module has a manifest file and no root file has been specified in the RootModule key, the manifest becomes the primary file for the module. And, the module becomes a manifest module (ModuleType = Manifest).

To export members from .psm1 or .dll files in a module that has a manifest, the names of those files must be specified in the values of the RootModule or NestedModules keys in the manifest. Otherwise, their members aren't exported.

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

### -ProcessorArchitecture
Specifies the processor architecture that the module requires.

The acceptable values for this parameter are:

* Amd64
* Arm
* IA64
* MSIL
* None (unknown or unspecified)
* X86

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

### -CompatiblePSEditions
Specifies the compatible PSEditions of the module. For information about PSEdition, see: https://docs.microsoft.com/en-us/powershell/scripting/gallery/concepts/module-psedition-support

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:
Accepted Values: Desktop, Core

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PowerShellVersion 
Specifies the minimum version of PowerShell that will work with this module. For example, you can specify 3.0, 4.0, or 5.0 as the value of this parameter.

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

### -ClrVersion
Specifies the minimum version of the Common Language Runtime (CLR) of the Microsoft .NET Framework that the module requires.

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

### -DotNetFrameworkVersion
Specifies the minimum version of the Microsoft .NET Framework that the module requires. 

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

### -PowerShellHostName
Specifies the name of the PowerShell host program that the module requires. Enter the name of the host program, such as PowerShell ISE Host or ConsoleHost. Wildcards aren't permitted.

To find the name of a host program, in the program, type $Host.Name.

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
Specifies the minimum version of the PowerShell host program that works with the module. Enter a version number, such as 1.1.

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

### -RequiredModules
Specifies modules that must be in the global session state. If the required modules aren't in the global session state, PowerShell imports them. If the required modules aren't available, the Import-Module command fails.

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

### -TypesToProcess
Specifies the type files (.ps1xml) that run when the module is imported.

When you import the module, PowerShell runs the Update-TypeData cmdlet with the specified files. Because type files aren't scoped, they affect all session states in the session.

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
Specifies the formatting files (.ps1xml) that run when the module is imported.

When you import a module, PowerShell runs the Update-FormatData cmdlet with the specified files. Because formatting files aren't scoped, they affect all session states in the session.


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

### -ScriptsToProcess
Specifies script (.ps1) files that run in the caller's session state when the module is imported. You can use these scripts to prepare an environment, just as you might use a login script.

To specify scripts that run in the module's session state, use the NestedModules key.

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

### -RequiredAssemblies
Specifies the assembly (.dll) files that the module requires. Enter the assembly file names. PowerShell loads the specified assemblies before updating types or formats, importing nested modules, or importing the module file that is specified in the value of the RootModule key.

Use this parameter to specify all the assemblies that the module requires, including assemblies that must be loaded to update any formatting or type files that are listed in the FormatsToProcess or TypesToProcess keys, even if those assemblies are also listed as binary modules in the NestedModules key.

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

### -ModuleList
Specifies an array of modules that are included in the module.

Enter each module name as a string or as a hash table with ModuleName and ModuleVersion keys. The hash table can also have an optional GUID key. You can combine strings and hash tables in the parameter value.

This key is designed to act as a module inventory. The modules that are listed in the value of this key aren't automatically processed.
 
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

Use this parameter to restrict the functions that are exported by the module. FunctionsToExport can remove functions from the list of exported aliases, but it can't add functions to the list.
 
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

### -AliasesToExport
Specifies the aliases that the module exports. Wildcards are permitted.

Use this parameter to restrict the aliases that are exported by the module. AliasesToExport can remove aliases from the list of exported aliases, but it can't add aliases to the list.
 
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

Use this parameter to restrict the variables that are exported by the module. VariablesToExport can remove variables from the list of exported variables, but it can't add variables to the list.
 
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

### -CmdletsToExport
Specifies the cmdlets that the module exports. Wildcards are permitted.

Use this parameter to restrict the cmdlets that are exported by the module. CmdletsToExport can remove cmdlets from the list of exported cmdlets, but it can't add cmdlets to the list.
 
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

### -DscResourcesToExport
Specifies the Desired State Configuration (DSC) resources that the module exports. Wildcards are permitted. 
 
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

### -PrivateData
Specifies data that is passed to the module when it's imported.
 
```yaml
Type: Hashtable
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

### -ProjectUri
Specifies the URL of a web page about this project.
 
```yaml
Type: Uri
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
Type: Uri
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IconUri
Specifies the URL of an icon for the module. The specified icon is displayed on the gallery web page for the module.
 
```yaml
Type: Uri
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReleaseNotes
Specifies a string array that contains release notes or comments that you want available for this version of the script.
 
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

### -Prerelease
Specifies the prerelease tag that is appended to the module version.  For example, if prerelease is "preview" and the module version is "1.0.0" the version of the module would be "1.0.0-preview".

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

### -HelpInfoUri
Specifies the internet address of the module's HelpInfo XML file. Enter a Uniform Resource Identifier (URI) that begins with http or https.

The HelpInfo XML file supports the Updatable Help feature that was introduced in PowerShell version 3.0. It contains information about the location of the module's downloadable help files and the version numbers of the newest help files for each supported locale.

For information about Updatable Help, see: https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_updatable_help?view=powershell-7.2. For information about the HelpInfo XML file, see: https://docs.microsoft.com/en-us/powershell/scripting/developer/module/supporting-updatable-help.

```yaml
Type: Uri
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

### -RequireLicenseAcceptance
Specifies that a license acceptance is required for the module.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String[]

## OUTPUTS
None

## NOTES

## RELATED LINKS

[<add>](<add>)
