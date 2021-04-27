# Save-PSResource

The `Save-PSResource` cmdlet combines the `Save-Module, Save-Script` cmdlets from V2.
It saves from a package found on a repository (local or remote) based on the `-Name` parameter argument.
It does not return an object.
Other parameters allow the returned results to be further filtered.

## Syntax

### NameParameterSet (Default)
``` PowerShell
[[-Name] <string[]>] [-Version <string>] [-Prerelease] [-Repository <string[]>] 
[-Path string] [-Credential <pscredential>] [-AsNupkg] [-IncludeXML] [-TrustRepository] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### InputObjectParameterSet
``` PowerShell
[[-InputObject] <object[]>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## Parameters

### -Name

Name of a resource or resources to save from a repository.
A comma-separated list of module names is accepted. The resource name must match the resource name in the repository.

```yml
Type: string[]
Parameter Sets: NameParameterSet
```

### -Version

Specifies the version or version range of the resource to be saved.

```yml
Type: string
Parameter Sets: NameParameterSet
```

### -Prerelease

When specified, allow saving prerelease versions.

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet
```

### -Repository

Specifies one or more repository names to search.
If not specified, search will only search the highest priority repository.

```yml
Type: string[]
Parameter Sets: NameParameterSet
```

### -Credential

Optional credentials to be used when accessing a private repository.

```yml
Type: PSCredential
Parameter Sets: NameParameterSet
```

### -AsNupkg

When specified, saves the resource as a .nupkg.

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet
```

### -IncludeXML

Saves the metadata XML file with the resource. 

```yml
Type: string[]
Parameter Sets: NameParameterSet
```

### -Path

Specifies the destination where the resource is to be saved.

```yml
Type: string
Parameter Sets: NameParameterSet
```

### -TrustRepository

When specified, suppresses being prompted for untrusted sources.

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet
```

### -InputObject

Used to pass in an object via pipeline to save.

```yml
Type: object[]
Parameter Sets: InputObjectParameterSet
```

### Outputs

No output.

## Notes

Should -TrustRepository parameter be removed?  Ie, should repositories be trusted by default?
Now that we have repository priorities, one could set an untrusted repository to a lower priority.

How often is a repository intentionally set as 'untrusted'?


## Tests

Most search tests can be performed on a local repository.

### -Name param

- Single name search
- Wildcard search
- Multiple name search
- Cancel search
- Errors: Not found (single name, wildcard, multiple name)
- Errors: Repository: Invalid name, connection error, etc

### -Version param

- Validate correct resource version returned
- Validate wild card (if supported), correct version range returned
- Errors: Not found
- Errors: Invalid version string format

### -Prerelease param

- Validate prerelease version returned

### -Repository param

- All repository search
- Single repository search
- Multiple repository search
- Errors: Repository not found

### -Credential param

- Validate credential search
- Errors: Credential: Invalid

### -AsNupkg param

- Validate that package gets saved as .nupkg
- Errors: package is unable to save

### -IncludeXML param

- Validate that package gets saved with xml
- Errors: unable to create XML, unable to save XML

### -Path param

- Validate that package saves in the correct path
- Errors: unable to access path (such as OneDrive or paths with spaces)

### -TrustRepository

- Validate that user is not prompted and has access to repository



## Work Items

### Create cmdlet and parameter sets

Create cmdlet class, parameters, and functional stubs  
1 day

### Implement uninstall helpers

Create helper functions that support all search functions  
Use existing code as starting point  
2 days

### Create test support

Create any test repositories and mocks for tests  
4 days

### Write cmdlet tests

Create all functional tests to validate cmdlet  
5 days
