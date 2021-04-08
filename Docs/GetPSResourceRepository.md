# Get-PSResourceRepository

The `Get-PSResourceRepository` cmdlet replaces the `Get-PSRepository` cmdlet from V2.

It searches for the PowerShell resource repositories that are registered on the machine.
By default it will return all registered repositories, or if the `-Name` parameter argument is specified
then it wil return the repository whose name matches the specified value.

It returns `PSRepositoryInfo` objects which describe each resource item found.

## Syntax

### NameParameterSet (Default)
``` PowerShell
[[-Name] <string[]>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## Parameters

### -Name

Name of a registered repository to find.
Supports wild card characters.

```yml
Type: string[]
Parameter Sets: NameParameterSet
```

### Outputs

```json
"PSRepositoryInfo" : {
    "Name",
    "Url",
    "Trusted",
    "Priority"
}
```

## Notes

Running the `Get-PSResourceRepository` cmdlet without a Name parameter specified will return all registered repositories.

## Tests

Tests added for terminating and non-terminating error handling.

Tests able to get repositories with different allowed URL URI schemes.

### -Name param

- Single name search
- wilcard search and empty value (returns all) compatibility
- Multiple name search
- Errors: Repository not found (single name, multiple name)

## Work Items

### Create cmdlet and parameter sets

Create cmdlet class, parameters, and functional stubs
1 day

### Switch output to PSRepositoryInfo type

Switch from returning Object object to PSRepositoryInfo object, and create that class
1-2 days

### Create test support

Create any test repositories and mocks for tests
1 day

### Write cmdlet tests

Create all functional tests to validate cmdlet
1 day
