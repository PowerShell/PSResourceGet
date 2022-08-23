---
external help file: PowerShellGet-help.xml
Module Name: PowerShellGet
ms.date: 08/03/2022
online version:
schema: 2.0.0
---

# Update-PSScriptFileInfo

## SYNOPSIS
This cmdlet updates the comment-based metadata in an existing script `.ps1` file.

## SYNTAX

### __AllParameterSets

```
Update-PSScriptFileInfo [-Path] <string> [-Author <string>] [-CompanyName <string>]
 [-Copyright <string>] [-Description <string>] [-ExternalModuleDependencies <string[]>]
 [-ExternalScriptDependencies <string[]>] [-Guid <guid>] [-IconUri <string>]
 [-LicenseUri <string>] [-PrivateData <string>] [-ProjectUri <string>] [-ReleaseNotes <string>]
 [-RemoveSignature] [-RequiredModules <hashtable[]>] [-RequiredScripts <string[]>]
 [-Tags <string[]>] [-Version <string>] [<CommonParameters>]
```

## DESCRIPTION

This cmdlet updates the comment-based metadata in an existing script `.ps1` file. This is similar to
`Update-ModuleManifest`.

## EXAMPLES

### Example 1: Update the version of a script

In this example, a script is created with **Version** set to `1.0.0.0`. `Update-PSScriptFileInfo`
changes the **Version**' to `2.0.0.0`. The `Get-Content` cmdlet shows the updated contents of the
script.

```powershell
$parameters = @{
    FilePath = "C:\Users\johndoe\MyScripts\test_script.ps1"
    Version = "1.0.0.0"
    Description = "this is a test script"
}
New-PSScriptFileInfo @parameters
$parameters.Version = "2.0.0.0"
Update-PSScriptFileInfo @parameters
Get-Content $parameters.FilePath
```

```Output
<#PSScriptInfo

.VERSION 2.0.0.0

.GUID 6ec3934e-a2e0-495b-9a9c-480e555ad1d1

.AUTHOR johndoe

.COMPANYNAME

.COPYRIGHT

.TAGS

.LICENSEURI

.PROJECTURI

.ICONURI

.EXTERNALMODULEDEPENDENCIES

.REQUIREDSCRIPTS

.EXTERNALSCRIPTDEPENDENCIES

.RELEASENOTES


.PRIVATEDATA

#>

<#

.DESCRIPTION
this is a test script

#>

```

## PARAMETERS

### -Author

The name of the author of the script.

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

### -CompanyName

The name of the company owning the script.

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

### -Copyright

The copyright information for the script.

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

The description of the script.

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

The list of external module dependencies taken by this script.

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

### -ExternalScriptDependencies

The list of external script dependencies taken by this script.

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

### -Path

The filename and location of the script.

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

### -Guid

The unique identifier for the script in GUID format.

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

### -IconUri

A Uniform Resource Identifier (URI) pointing to the icon associated with the script.

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

### -LicenseUri

The URI pointing to the license agreement file associated with the script.

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

The private data associated with the script.

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

### -ProjectUri

The URI pointing to the project site associated with the script.

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

### -ReleaseNotes

The release notes for the script.

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

### -RemoveSignature

Removes the signature from a signed `.ps1` file, allowing you to update the script. You should
re-sign the after updating the file.

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

### -RequiredModules

The parameter takes an array of module specification hashtables. A module specification is a
hashtable that has the following keys.

- `ModuleName` - **Required** Specifies the module name.
- `GUID` - **Optional** Specifies the GUID of the module.
- It's also **Required** to specify at least one of the three below keys.
  - `ModuleVersion` - Specifies a minimum acceptable version of the module.
  - `MaximumVersion` - Specifies the maximum acceptable version of the module.
  - `RequiredVersion` - Specifies an exact, required version of the module. This can't be used with
    the other Version keys.

```yaml
Type: System.Collections.Hashtable[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequiredScripts

The list of scripts required by the script.

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

The tags associated with the script. Tag values are strings that should not contain spaces. For more
information, see [Tag details][1].

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

### -Version

The version of the script.

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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose,
-WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS

[PowerShellGallery Publishing Guidelines and Best Practices][2]

[Package manifest values that impact the PowerShell Gallery UI][3]

<!-- link references -->

[1]: /powershell/scripting/gallery/concepts/package-manifest-affecting-ui#tag-details
[2]: /powershell/scripting/gallery/concepts/publishing-guidelines
[3]: /powershell/scripting/gallery/concepts/package-manifest-affecting-ui
