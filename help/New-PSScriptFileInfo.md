---
external help file: PowerShellGet-help.xml
Module Name: PowerShellGet
online version: 
schema: 2.0.0
---

# New-PSScriptFileInfo

## SYNOPSIS

Creates a new .ps1 file containing metadata for the script, which is used when publishing a script package.

## SYNTAX

### __AllParameterSets

```
New-PSScriptFileInfo [-FilePath] <String> -Description <String> [-Author <String>] [-CompanyName <String>] [-Copyright <String>] [-ExternalModuleDependencies <String[]>] [-ExternalScriptDependencies <String[]>] [-Force] [-Guid <Guid>] [-IconUri <String>] [-LicenseUri <String>] [-PrivateData <String>] [-ProjectUri <String>] [-ReleaseNotes <String[]>] [-RequiredModules <Hashtable[]>] [-RequiredScripts <String[]>] [-Tags <String[]>] [-Version <String>] [<CommonParameters>]
```

## DESCRIPTION

The New-PSScriptFileInfo cmdlet creates a .ps1 file containing metadata for the script.

## EXAMPLES

### Example 1: Creating a script with minimum required parameters

```
PS C:\> New-PSScriptFileInfo -FilePath "C:\Users\johndoe\MyScripts\test_script.ps1" -Description "this is a test script"
PS C:\> cat "C:\Users\johndoe\MyScripts\test_script.ps1"
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
this is a test script


#>
        
```

This example runs the cmdlet with the only required parameters, the 'FilePath' parameter sets the path the script is to be created and the 'Description' parameter contains the description for the script. The script is successfully created and if the contents of the file are viewed we can see the Description set as well as Author, Guid, and Version (with default values).

### Example 2: Creating a script with RequiredModules, Author, Version and Description parameters

```
PS C:\> $requiredModules =  @(@{ModuleName = "PackageManagement"; ModuleVersion = "1.0.0.0" }, @{ModuleName = "PSReadLine"})
PS C:\> New-PSScriptFileInfo -FilePath "C:\Users\johndoe\MyScripts\test_script2.ps1" -Description "this is a test script" -Version "2.0.0.0" -Author "janedoe" -RequiredModules $requiredModules
PS C:\> cat "C:\Users\johndoe\MyScripts\test_script.ps1"
<#PSScriptInfo

.VERSION 2.0.0.0

.GUID 7ec4832e-a4e1-562b-8a8c-241e535ad7d7

.AUTHOR janedoe

.COMPANYNAME Jane Corporation

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
this is a test script


#>
        
```

This example runs the cmdlet with the required 'FilePath' and 'Description' parameters, as well as 'Author', 'Version', and 'RequiredModules' parameters. The 'RequiredModules' parameter describes modules required by the script. It is necessary to provide the ModuleName key in the hashtable and if one wishes to specify verion they must also specify ModuleVersion, RequiredVersion, MaximumVersion, or MinimumVersion keys. The script is successfully created and if the contents of the file are viewed we can see the following values are set in the script file: Description, Author, Guid, and Version and RequiredModules.



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

Required: True
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

If used and the .ps1 file specified at the path exists, it rewrites the file.

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

Required: True (None) False (All)
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

Required: True (None) False (All)
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

Required: True (None) False (All)
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

