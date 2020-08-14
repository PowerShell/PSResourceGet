---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version: <add>
schema: 2.0.0
---

# Publish-PSResource

## SYNOPSIS
Publishes a specified PowerShell resource from the local computer to an online gallery.

## SYNTAX

### PathParameterSet
```
Publish-PSResource [-APIKey <String>] [-Repository <String>] [-DestinationPath <String>] [-Path] <String>
 [-Credential <PSCredential>] [-SkipDependenciesCheck] [-ReleaseNotes <String>] [-Tags <String[]>]
 [-LicenseUrl <String>] [-IconUrl <String>] [-ProjectUrl <String>] [-Exclude <String[]>] [-Nuspec <String>]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

### PathLiteralParameterSet
```
Publish-PSResource [-APIKey <String>] [-Repository <String>] [-DestinationPath <String>] -LiteralPath <String>
 [-Credential <PSCredential>] [-SkipDependenciesCheck] [-ReleaseNotes <String>] [-Tags <String[]>]
 [-LicenseUrl <String>] [-IconUrl <String>] [-ProjectUrl <String>] [-Exclude <String[]>] [-Nuspec <String>]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

### CreateNuspecParameterSet
```
Publish-PSResource [-APIKey <String>] [-Repository <String>] [-DestinationPath <String>]
 [-Credential <PSCredential>] [-SkipDependenciesCheck] [-ReleaseNotes <String>] [-Tags <String[]>]
 [-LicenseUrl <String>] [-IconUrl <String>] [-ProjectUrl <String>] [-Exclude <String[]>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### ModuleNameParameterSet
```
Publish-PSResource [-APIKey <String>] [-Repository <String>] [-DestinationPath <String>]
 [-Credential <PSCredential>] [-SkipDependenciesCheck] [-Exclude <String[]>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### NuspecParameterSet
```
Publish-PSResource [-APIKey <String>] [-Repository <String>] [-DestinationPath <String>]
 [-Credential <PSCredential>] [-SkipDependenciesCheck] [-Exclude <String[]>] [-Nuspec <String>] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

## DESCRIPTION

The `Publish-PSResource` cmdlet publishes a PowerShell resource to an online NuGet-based gallery by using an API key,
stored as part of a user's profile in the gallery. You can specify the resource to publish either by
the resource's name, or by the path to the folder containing the resource.

When you specify a resource by name, `Publish-PSResource` publishes the first resource that would be found
by running `Get-Module -ListAvailable <Name>`. If you specify a minimum version of a module to
publish, `Publish-Module` publishes the first module with a version that is greater than or equal to
the minimum version that you have specified.

Publishing a module requires metadata that is displayed on the gallery page for the module. Required
metadata includes the module name, version, description, and author. Although most metadata is taken
from the module manifest, some metadata must be specified in `Publish-Module` parameters, such as
**Tag**, **ReleaseNote**, **IconUri**, **ProjectUri**, and **LicenseUri**, because these parameters
match fields in a NuGet-based gallery.

## EXAMPLES

### Example 1: Publish a module

In this example, MyDscModule is published to the online gallery by using the API key to indicate the
module owner's online gallery account. If MyDscModule is not a valid manifest module that specifies
a name, version, description, and author, an error occurs.

```powershell
Publish-PSResource -Name "MyDscModule" -NuGetApiKey "11e4b435-6cb4-4bf7-8611-5162ed75eb73"
```

### Example 2: Publish a module with gallery metadata

In this example, MyDscModule is published to the online gallery by using the API key to indicate the
module owner's gallery account. The additional metadata provided is displayed on the webpage for the
module in the gallery. The owner adds two search tags for the module, relating it to Active
Directory; a brief release note is added. If MyDscModule is not a valid manifest module that
specifies a name, version, description, and author, an error occurs.

```powershell
Publish-PSResource -Name "MyDscModule" -NuGetApiKey "11e4b435-6cb4-4bf7-8611-5162ed75eb73" -LicenseUri "http://contoso.com/license" -Tag "Active Directory","DSC" -ReleaseNote "Updated the ActiveDirectory DSC Resources to support adding users."
```

### Example 3: Publish a script

This publishes a script called `Demo-Script.ps1` to a local repository. 

```powershell
Publish-Script -Path D:\ScriptSharingDemo\Demo-Script.ps1 -Repository LocalRepo1
```

## PARAMETERS

### -APIKey

Specifies the API key that you want to use to publish a module to the online gallery. The API key is
part of your profile in the online gallery, and can be found on your user account page in the
gallery.

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

### -Credential

Specifies a user account that has rights to publish a module for a specified package provider or
source.

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

### -DestinationPath

Specifies the path to the module that you want to publish. This parameter accepts the path to the
folder that contains the module.

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

### -Exclude

Defines files to exclude from the published module.

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

### -IconUrl

Specifies the URL of an icon for the module. The specified icon is displayed on the gallery webpage
for the module.

```yaml
Type: System.String
Parameter Sets: PathParameterSet, PathLiteralParameterSet, CreateNuspecParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LicenseUrl

Specifies the URL of licensing terms for the module you want to publish.

```yaml
Type: System.String
Parameter Sets: PathParameterSet, PathLiteralParameterSet, CreateNuspecParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LiteralPath

Specifies the path to the module that you want to publish. This parameter accepts the path to the
folder that contains the module.

```yaml
Type: System.String
Parameter Sets: PathLiteralParameterSet
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -Nuspec
{{ Fill Nuspec Description }}

```yaml
Type: System.String
Parameter Sets: PathParameterSet, PathLiteralParameterSet, NuspecParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path

Specifies the path to the module that you want to publish. This parameter accepts the path to the
folder that contains the module.

```yaml
Type: System.String
Parameter Sets: PathParameterSet
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -ProjectUrl

Specifies the URL of a webpage about this project.

```yaml
Type: System.String
Parameter Sets: PathParameterSet, PathLiteralParameterSet, CreateNuspecParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReleaseNotes

Specifies a string containing release notes or comments that you want to be available to users of
this version of the module.


```yaml
Type: System.String
Parameter Sets: PathParameterSet, PathLiteralParameterSet, CreateNuspecParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Repository

Specifies the friendly name of a repository that has been registered by running
`Register-PSResourceRepository`. The repository must have a **PublishLocation**, which is a valid NuGet URI.
The **PublishLocation** can be set by running `Set-PSRepository`.

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

### -SkipDependenciesCheck
{{ Fill SkipDependenciesCheck Description }}

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

### -Tags

Adds one or more tags to the module that you are publishing. Example tags include
DesiredStateConfiguration, DSC, DSCResourceKit, or PSModule. Separate multiple tags with commas.

```yaml
Type: System.String[]
Parameter Sets: PathParameterSet, PathLiteralParameterSet, CreateNuspecParameterSet
Aliases:

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

## INPUTS

### System.String

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS

[Find-PSResource](Find-PSResource.md)

[Install-PSResource](Install-PSResource.md)

[Register-PSResourceRepository](Register-PSResourceRepository.md)

[Set-PSResourceRepository](Set-PSResourceRepository.md)

[Uninstall-PSResource](Uninstall-PSResource.md)

[Update-PSResource](Update-PSResource.md)

