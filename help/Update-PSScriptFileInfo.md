---
external help file: PowerShellGet-help.xml
Module Name: PowerShellGet
online version: 
schema: 2.0.0
---

# Update-PSScriptFileInfo

## SYNOPSIS

Updates an existing .ps1 file with requested properties and ensures it's valid

## SYNTAX

### __AllParameterSets

```
Update-PSScriptFileInfo [-FilePath] <String> [-Author <String>] [-CompanyName <String>] [-Copyright <String>] [-Description <String>] [-ExternalModuleDependencies <String[]>] [-ExternalScriptDependencies <String[]>] [-Force] [-Guid <Guid>] [-IconUri <String>] [-LicenseUri <String>] [-PrivateData <String>] [-ProjectUri <String>] [-ReleaseNotes <String[]>] [-RemoveSignature] [-RequiredModules <Hashtable[]>] [-RequiredScripts <String[]>] [-Tags <String[]>] [-Version <String>] [<CommonParameters>]
```

## DESCRIPTION

The Update-PSScriptFileInfo cmdlet updates an existing .ps1 file with requested properties and ensures it's valid.

## EXAMPLES

### Example 1: Example 1

```
PS C:\> {{ Add example code here }}
```

{{ Add example description here }}

## PARAMETERS

### -Author

The author of the script.

```yaml
Type: String
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -CompanyName

The name of the company owning the script.

```yaml
Type: String
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -Copyright

The copyright information for the script.

```yaml
Type: String
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -Description

The description of the script.

```yaml
Type: String
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -ExternalModuleDependencies

The list of external module dependencies taken by this script.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -ExternalScriptDependencies

The list of external script dependencies taken by this script.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -FilePath

The path the .ps1 script info file will be created at.

```yaml
Type: String
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: True
Position: 0
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -Force

If used with FilePath parameter and .ps1 file specified at the path exists, it rewrites the file

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -Guid

The GUID for the script.

```yaml
Type: Guid
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -IconUri

The Uri for the icon associated with the script.

```yaml
Type: String
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -LicenseUri

The Uri for the license associated with the script.

```yaml
Type: String
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -PrivateData

The private data associated with the script.

```yaml
Type: String
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -ProjectUri

The Uri for the project associated with the script.

```yaml
Type: String
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -ReleaseNotes

The release notes for the script.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -RemoveSignature

Remove signature from signed .ps1 (if present) thereby allowing update of script to happen. User should re-sign the updated script afterwards.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -RequiredModules

The list of modules required by the script.

```yaml
Type: Hashtable[]
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -RequiredScripts

The list of scripts required by the script.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -Tags

The tags associated with the script.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```

### -Version

The version of the script.

```yaml
Type: String
Parameter Sets: (All)
Aliases: 
Accepted values: 

Required: False
Position: Named
Default value: 
Accept pipeline input: False
Accept wildcard characters: False
DontShow: False
```


### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None



## OUTPUTS

### None



## NOTES


## RELATED LINKS

Fill Related Links Here

