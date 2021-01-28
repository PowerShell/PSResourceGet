# Unregister-PSResourceRepository

The `Unregister-PSResourceRepository` cmdlet replaces the `Unregister-PSRepository` cmdlet from V2.

It unregisters a repository for the current user.

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

### -PassThru

When specified, displays the succcessfully set repository and its information

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet
```

### Outputs

if `-PassThru` not specified output is none

if `-PassThru` is specified output is:

```json
"PSRepositoryInfo" : {
    "Name",
    "Url",
    "Trusted",
    "Priority"
}
```

## Notes

`-Name` parameter doesn't support wildcard characters.

## Tests

Tests should be performed on repositories registered with each type of URL (HTTPS, HTTP, FTP (remote) and filebase (local))

Some tests should be performed where `-Name` contains a wildcard character and appropriate error message should be displayed.

### -Name param

- Single name search
- Multiple name search
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
