---
external help file: PowerShellGet-help.xml
Module Name: PowerShellGet
online version: 
schema: 2.0.0
---

# Test-PSScriptFileInfo

## SYNOPSIS

Tests a .ps1 file at the specified path to ensure it is valid.

## SYNTAX

### __AllParameterSets

```
Test-PSScriptFileInfo [-FilePath] <String> [<CommonParameters>]
```

## DESCRIPTION

The Test-PSScriptFileInfo cmdlet tests a .ps1 file at the specified path to ensure it is valid.

## EXAMPLES

### Example 1: Test a valid script

```
PS C:\> New-PSScriptFileInfo -FilePath "C:\Users\johndoe\MyScripts\test_script.ps1" -Description "this is a test script"
PS C:\> Test-PSScriptFileInfo -FilePath "C:\Users\johndoe\MyScripts\test_script.ps1"
True
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

Assume that the script file specified was created by the New-PSScriptFileInfo cmdlet prior to this example and is valid. This example runs the Test-PSScriptFileInfo cmdlet against a script located at the path provided to the 'FilePath' parameter. Since the script is a valid script the cmdlet outputs "True". To see what this valid script looks like we can see the contents of the file.

### Example 2: Test an invalid script (missing Author)

```
PS C:\> Test-PSScriptFileInfo -FilePath "C:\Users\johndoe\MyScripts\invalid_test_script.ps1"
WARNING: The .ps1 script file passed in was not valid due to: PSScript file is missing the required Author property
False

PS C:\> cat "C:\Users\johndoe\MyScripts\test_script.ps1"
<#PSScriptInfo

.VERSION 1.0.0.0

.GUID 7ec4832e-a4e1-562b-8a8c-241e535ad7d7

.AUTHOR

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

This example runs the Test-PSScriptFileInfo cmdlet against a script located at the path provided to the 'FilePath' parameter. Since the script is not a valid script and is missing the required Author metadata property, the cmdlet writes an informative warning message and outputs "False". To see what this invalid script looks like we can see the contents of the file.


## PARAMETERS

### -FilePath

The path that the .ps1 script info file which is to be tested is located at.

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


### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None



## OUTPUTS

### bool



## NOTES


## RELATED LINKS

Fill Related Links Here

