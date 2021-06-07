# Uninstall-PSResource

The `Uninstall-PSResource` cmdlet combines the `Uninstall-Module, Uninstall-Script` cmdlets from V2.
It uninstalls a package found in a module or script installation path based on the `-Name` parameter argument.
It does not return an object.
Other parameters allow the returned results to be further filtered.

## Syntax

### NameParameterSet (Default)
``` PowerShell
[[-Name] <string[]>] [-Version <string>] [-PrereleaseOnly] [-Tags <string[]>]
[-WhatIf] [-Confirm] [<CommonParameters>]
```

## Parameters

### -Name

Name of a resource or resources that has been installed.
Accepts wild card characters.

```yml
Type: string[]
Parameter Sets: NameParameterSet
```

### -Version

Specifies the version of the resource to be uninstalled.

```yml
Type: string
Parameter Sets: NameParameterSet
```

### -PrereleaseOnly

When specified, allows ONLY prerelease versions to be uninstalled.

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet
```

### Outputs

No output.


## Notes

Should a -PassThru parameter be added?

## Tests

Most search tests can be performed on a local repository.

### -Name param

- Single name search
- Wildcard search
- Multiple name search
- Cancel search
- Errors: Not found (single name, wildcard, multiple name)
- Errors: Path errors (OneDrive access, etc)

### -Version param

- Validate correct resource version returned
- Validate wild card (if supported), correct version range returned
- Errors: Not found
- Errors: Invalid version string format

### -Prerelease param

- Validate prerelease version returned


## Work Items

### Create cmdlet and parameter sets

Create cmdlet class, parameters, and functional stubs  
1 day

### Implement uninstall helper

Create helper functions that support all search functions  
Use existing code as starting point  
2 days

### Create test support

Create any test repositories and mocks for tests  
4 days

### Write cmdlet tests

Create all functional tests to validate cmdlet  
5 days
