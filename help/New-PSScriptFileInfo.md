---
external help file: PowerShellGet-help.xml
Module Name: PowerShellGet
ms.date: 08/03/2022
online version:
schema: 2.0.0
---

# New-PSScriptFileInfo

## SYNOPSIS
The cmdlet creates a new script file, including metadata about the script.

## SYNTAX

### __AllParameterSets

```
New-PSScriptFileInfo [-Path] <string> -Description <string> [-Version <string>]
 [-Author <string>] [-Guid <guid>] [-CompanyName <string>] [-Copyright <string>]
 [-RequiredModules <hashtable[]>] [-ExternalModuleDependencies <string[]>]
 [-RequiredScripts <string[]>] [-ExternalScriptDependencies <string[]>] [-Tags <string[]>]
 [-ProjectUri <string>] [-LicenseUri <string>] [-IconUri <string>] [-ReleaseNotes <string>]
 [-PrivateData <string>] [-Force] [<CommonParameters>]
```

## DESCRIPTION

The cmdlet creates a new script file containing the required metadata needed to publish a script
package.

## EXAMPLES

### Example 1: Creating an empty script with minimal information

This example runs the cmdlet using only required parameters. The **Path** parameter specifies
the nane and location of the script. The **Description** parameter provide the description used in
the comment-based help for the script.

```powershell
New-PSScriptFileInfo -Path ./test_script.ps1 -Description "This is a test script."
Get-Content ./test_script.ps1
```

```Output
<#PSScriptInfo

.VERSION 1.0.0.0

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
This is a test script.


#>

```

### Example 2: Creating a script with required modules

This example runs the cmdlet with additional parameters, including **RequiredModules**.
**RequiredModules** is an array of module specifications.

```powershell
$parameters = @{
    Path = './test_script2.ps1'
    Description = 'This is a test script.'
    Version = '2.0.0.0'
    Author = 'janedoe'
    RequiredModules =  @(
        @{ModuleName = "PackageManagement"; ModuleVersion = "1.0.0.0" },
        @{ModuleName = "PSReadLine"}
    )
}
New-PSScriptFileInfo @parameters
Get-Content ./test_script2.ps1
```

```Output
<#PSScriptInfo

.VERSION 2.0.0.0

.GUID 7ec4832e-a4e1-562b-8a8c-241e535ad7d7

.AUTHOR janedoe

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

#Requires -Module PSReadLine
#Requires -Module @{ ModuleName = 'PackageManagement'; ModuleVersion = '1.0.0.0' }

<#

.DESCRIPTION
This is a test script.


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

Required: True
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

The filename and location where the script is created.

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

### -Force

Forces the cmdlet to overwrite any existing file.

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

### -Guid

The unique identifier for the script in GUID format. If you don't provide a GUID, the cmdlet creates
a new one automatically.

```yaml
Type: System.Guid
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: Randomly generated
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

The version of the script. If no value is provided **Version** defaults to `1.0.0.0`.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: 1.0.0.0
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
