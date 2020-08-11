---
external help file: PowerShellGet.dll-Help.xml
Module Name: PowerShellGet
online version: <add>
schema: 2.0.0
---

# Install-PSResource

## SYNOPSIS

Downloads one or more PowerShell resources from a repository, and installs them on the local computer.

## SYNTAX

### NameParameterSet (Default)
```
Install-PSResource [-Name] <String[]> [-Type <String[]>] [-Version <String>] [-Prerelease]
 [-Repository <String[]>] [-Credential <PSCredential>] [-Scope <String>] [-NoClobber] [-TrustRepository]
 [-Reinstall] [-Quiet] [-AcceptLicense] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### InputObjectSet
```
Install-PSResource [-InputObject] <Object[]> [-WhatIf] [-Confirm] [<CommonParameters>]
```

### RequiredResourceFileParameterSet
```
Install-PSResource [-Type <String[]>] [-Prerelease] [-Repository <String[]>] [-Credential <PSCredential>]
 [-Scope <String>] [-NoClobber] [-TrustRepository] [-Reinstall] [-Quiet] [-AcceptLicense]
 [-RequiredResourceFile <String>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### RequiredResourceParameterSet
```
Install-PSResource [-RequiredResource <Object>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION

The `Install-PSResource` cmdlet gets one or more PSResources that meet specified criteria from a PowerShell
repository. The cmdlet verifies that search results are valid resources and copies the resource folders
to the installation location. Installed resources are not automatically imported after installation.
You can filter which resource is installed based on the specifed version.

If the resource being installed has the same name or version, or contains commands in an existing
module, warning messages are displayed. 
Dependent upon your
repository settings, you might need to answer a prompt for the module installation to continue.

These examples use the [PowerShell Gallery](https://www.powershellgallery.com/) as the only
registered repository. `Get-PSResourceRepository` displays the registered repositories. If you have multiple
registered repositories, use the `-Repository` parameter to specify the repository's name.

## EXAMPLES

### Example 1: Find and install a module

This example finds a module in the repository and installs the module.

```powershell
Find-PSResource -Name PowerShellGet | Install-PSResource
```

The `Find-PSResource` uses the **Name** parameter to specify the **PowerShellGet** module. By default,
the newest version of the module is downloaded from the repository. The object is sent down the
pipeline to the `Install-PSResource` cmdlet. `Install-PSResource` installs the module for all users in
`$env:ProgramFiles\PowerShell\Modules`.

### Example 2: Install a PSResource by name

In this example, the newest version of the **PowerShellGet** module is installed.

```powershell
Install-PSResource -Name PowerShellGet
```

The `Install-Module` uses the **Name** parameter to specify the **PowerShellGet** module. By
default, the newest version of the module is downloaded from the repository and installed.

### Example 3: Install a specific version of a PSResource

In this example, a specific version of the **PowerShellGet** module is installed.

```powershell
Install-PSResource -Name PowerShellGet -Version "2.0.0"
```

The `Install-PSResource` uses the **Name** parameter to specify the **PowerShellGet** module. The
**Version** parameter specifies that version **2.0.0** is downloaded and installed for all
users.

### Example 5: Install a PSResource only for the current user

This example downloads and installs the newest version of a module, only for the current user.

```powershell
Install-PSResource -Name PowerShellGet -Scope CurrentUser
```

The `Install-PSResource` uses the **Name** parameter to specify the **PowerShellGet** module.
`Install-PSResource` downloads and installs the newest version of **PowerShellGet** into the current
user's directory, `$home\Documents\PowerShell\Modules`.

## PARAMETERS

### -AcceptLicense

For modules that require a license, **AcceptLicense** automatically accepts the license agreement
during installation. For more information, see [Modules Requiring License Acceptance](/powershell/scripting/gallery/concepts/module-license-acceptance).

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential

Specifies a user account that has rights to install a PSResource for a specified package provider or
source.

```yaml
Type: System.Management.Automation.PSCredential
Parameter Sets: NameParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

```yaml
Type: System.Management.Automation.PSCredential
Parameter Sets: RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -InputObject

Used for pipeline input.

```yaml
Type: System.Object[]
Parameter Sets: InputObjectSet
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -Name

Specifies the exact names of resources to install from the online gallery. A comma-separated list of
resource names is accepted. The resource name must match the resource name in the repository. Use
`Find-PSResource` to get a list of resource names.

```yaml
Type: System.String[]
Parameter Sets: NameParameterSet
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -NoClobber
Does not allow installation if existing commands that have the same name as commands being installed by a resource.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Prerelease

Allows you to install a module marked as a pre-release.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Quiet
{{ Fill Quiet Description }}

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Reinstall

Installs a resource even if that resource is already installed.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Repository

Use the **Repository** parameter to specify which repository is used to download and install a
resource.

```yaml
Type: System.String[]
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequiredResource
{{ Fill RequiredResource Description }}

```yaml
Type: System.Object
Parameter Sets: RequiredResourceParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequiredResourceFile
{{ Fill RequiredResourceFile Description }}

```yaml
Type: System.String
Parameter Sets: RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Scope

Specifies the installation scope of the module. The acceptable values for this parameter are
**AllUsers** and **CurrentUser**.

```yaml
Type: System.String
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
Aliases:
Accepted values: CurrentUser, AllUsers

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TrustRepository
{{ Fill TrustRepository Description }}

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Type
{{ Fill Type Description }}

```yaml
Type: System.String[]
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Version
{{ Fill Version Description }}

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

### System.Object[]

### System.Management.Automation.PSCredential

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS


[Find-PSResource](Find-PSResource.md)

[Get-PSResourceRepository](Get-PSResourceRepository.md)

[Import-Module](../Microsoft.PowerShell.Core/Import-Module.md)

[Publish-PSResource](Publish-PSResource.md)

[Register-PSResourceRepository](Register-PSResourceRepository.md)

[Uninstall-PSResource](Uninstall-PSResource.md)

[Update-PSResource](Update-PSResource.md)

[about_Module](../Microsoft.PowerShell.Core/About/about_Modules.md)

