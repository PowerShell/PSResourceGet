---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version: <add>
schema: 2.0.0
---

# Get-PSResourceRepository

## SYNOPSIS
Gets PowerShell repositories.

## SYNTAX

```
Get-PSResourceRepository [[-Name] <String[]>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
The **Get-PSResourceRepository** cmdlet gets PowerShell module repositories that are registered for the current user.

## EXAMPLES

### Example 1: Get all module repositories

```
PS C:\> Get-PSResourceRepository
Name               Url                                     InstallationPolicy                           Priority
----               ---                                     ------------------                           --------
PSGallery          http://go.micro...                      Untrusted                                    50
myNuGetSource      https://myget.c...                      Trusted                                      49
```

This command gets all module repositories registered for the current user.

### Example 2: Get module repositories by name

```
PS C:\> Get-PSResourceRepository -Name "*NuGet*"
```

This command gets all module repositories that include NuGet in their names.

### Example 3: Get a module repository and format the output

```
PS C:\> Get-PSResourceRepository -Name "PSGallery" | Format-List * -Force

Name     : PSGallery
Url      : https://www.powershellgallery.com/api/v2
Trusted  : false
Priority : 49
```

This command gets the repository named PSGallery and uses the pipeline operator to pass that object to the Format-List cmdlet.

## PARAMETERS

### -Name

Specifies the names of the repositories to get.

```yaml
Type: System.String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
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

## INPUTS

### System.String[]

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS

[Register-PSResourceRepository](Register-PSResourceRepository.md)

[Set-PSResourceRepository](Set-PSResourceRepository.md)

[Unregister-PSResourceRepository](Unregister-PSResourceRepository.md)

