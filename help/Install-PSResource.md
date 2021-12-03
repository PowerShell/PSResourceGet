---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version: <add>
schema: 2.0.0
---

# Install-PSResource

## SYNOPSIS
Installs resources (modules and scripts) from a registered repository onto the machine.

## SYNTAX

### NameParameterSet
```
Install-PSResource [-Name] <String[]> [-Version <String>] [-Prerelease]
 [-Repository <String[]>] [-Credential <PSCredential>] [-Scope <ScopeType>] [-TrustRepository]
 [-Reinstall] [-Quiet] [-AcceptLicense] [-NoClobber] [-SkipDependencyCheck] [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### InputObjectParameterSet
```
Install-PSResource [-InputObject <PSResourceInfo>] [-Credential <PSCredential>] [-Scope <ScopeType>] [-TrustRepository]
 [-Reinstall] [-Quiet] [-AcceptLicense] [-NoClobber] [-SkipDependencyCheck] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
The Install-PSResource cmdlet combines the Install-Module and Install-Script cmdlets from V2. 
It installs a resource from a registered repository to an installation path on a machine based on the -Name parameter argument. It does not return any object. Other parameters allow the resource to be specified by repository and version, and allow the user to suppress prompts or specify the scope of installation.

## EXAMPLES

### Example 1
```powershell
PS C:\> Install-PSResource Az
```

Installs the Az module.

### Example 2
```powershell
PS C:\> Install-PSResource Az -Version "[2.0.0, 3.0.0]"
```

Installs the latest stable Az module that is within the range 2.0.0 and 3.0.0.

### Example 3
```powershell
PS C:\> Install-PSResource Az -Repository PSGallery
```

Installs the latest stable Az module from the PowerShellGallery.


### Example 3
```powershell
PS C:\> Install-PSResource Az -Reinstall
```

Installs the Az module and will write over any previously installed version if it is already installed.

## PARAMETERS

### -Name
Name of a resource or resources to install. Does not accept wildcard characters or a null value.

```yaml
Type: System.String[]
Parameter Sets: NameParameterSet
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Version
Specifies the version of the resource to be installed. The value can be an exact version or a version
range using the NuGet versioning syntax.

For more information about NuGet version ranges, see [Package versioning](/nuget/concepts/package-versioning#version-ranges)

PowerShellGet supports all but the _minimum inclusive version_ listed in the NuGet version range
documentation. So inputting "1.0.0.0" as the version doesn't yield versions 1.0.0.0 and higher
(minimum inclusive range). Instead, the values is considered as the required version and yields
version 1.0.0.0 only (required version). To use the minimum inclusive range, provide `[1.0.0.0, ]` as
the version range.

```yaml
Type: System.String
Parameter Sets: NameParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Prerelease
When specified, includes prerelease versions in search results returned.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Repository
Specifies one or more repository names to search.
If not specified, search will include all currently registered repositories, in order of highest priority, until first repository package is found in.

```yaml
Type: System.String[]
Parameter Sets: NameParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential
Optional credentials to be used when accessing a repository.

```yaml
Type: System.Management.Automation.PSCredential
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Scope
Specifies the scope under which a user has access.

```yaml
Type: Microsoft.PowerShell.PowerShellGet.UtilClasses.ScopeType
Parameter Sets: (All)
Aliases:
Accepted values: CurrentUser, AllUsers

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TrustRepository
Suppress prompts to trust repository. The prompt to trust repository only occurs if the repository is not already set to a trusted level.

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

### -Reinstall
Writes over any previously installed resource version that already exists on the machine.

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

### -Quiet
Supresses installation progress bar.

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

### -AcceptLicense
Specifies that the resource should accept any request to accept license. This will suppress prompting if the module mandates that a user accept their license.

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

### -NoClobber
Prevents installing a package that contains cmdlets that already exist on the machine.

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

### -SkipDependencyCheck
Skips the check for resource dependencies, so that only found resources are installed, and not any resources the found resource depends on.

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

### -PassThru
Passes the resource installed to the console.

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

### -InputObject
Used for pipeline input.

```yaml
Type: Microsoft.PowerShell.PowerShellGet.UtilClasses.PSResourceInfo
Parameter Sets: (All)
Aliases: wi

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Confirm
Prompts you for confirmation before running the cmdlet.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: (All)
Aliases: cf

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -WhatIf
Shows what would happen if the cmdlet runs.
The cmdlet is not run.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: (All)
Aliases: wi

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## OUTPUTS
None

## NOTES

## RELATED LINKS