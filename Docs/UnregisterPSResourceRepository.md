# Unregister-PSResourceRepository

The `Unregister-PSResourceRepository` cmdlet replaces the `Unregister-PSRepository` cmdlet from V2.

It unregisters a repository that's found to be registered on the machine.

## Syntax

### NameParameterSet (Default)
``` PowerShell
[[-Name] <string[]>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## Parameters

### -Name

Specifies name(s) of the repositories to unregister.
Does not accept wild card characters.

```yml
Type: string[]
Parameter Sets: NameParameterSet
```

### Outputs

none

## Notes

`-Name` parameter doesn't support wildcard characters.

## Tests

Tests able to unregister repositories with different allowed URL URI schemas.

Tests added for terminating and non-terminating error handling.

Tests to unregister multiple repositories provided to `-Name`.

### -Name param

- Single name unregister
- Multiple name unregister
- Errors: Not found (single name,multiple name
- Errors: Name contains wildcard which isn't supported

## Work Items

### Create cmdlet and parameter sets

Create cmdlet class, parameters, and functional stubs
1 day

### Implement unregister function

Create unregister function
1 day

### Create test support

Create any test repositories and mocks for tests
1 day

### Write cmdlet tests

Create all functional tests to validate cmdlet
1 day
