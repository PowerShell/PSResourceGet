# Update-PSResource

The `Update-PSResource` cmdlet combines the `Update-Module, Update-Script` cmdlets from V2.
It performs an upgraded installation of a package that is already installed based on the `-Name` parameter argument.
It does not return an object.
Other parameters allow the returned results to be further filtered.

## Syntax

### NameParameterSet (Default)
``` PowerShell
[[-Name] <string[]>] [-Version <string>] [-Prerelease] [-Scope <string>]
[-Repository <string[]>] [-TrustRepository] [-Credential <pscredential>] [-Quiet] 
[-AcceptLicense] [-NoClobber] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### InputObjectParameterSet
``` PowerShell
[[-InputObject] <object[]> [-WhatIf] [-Confirm] [<CommonParameters>]
```

## Parameters

### -Name

Name of a resource or resources to find.
Accepts wild card characters.

```yml
Type: string[]
Parameter Sets: NameParameterSet
```

### -InputObject

Specifies an object that is passed in via pipeline.
The object should be of type PSCustomObject.

```yml
Type: PSCustomObject[]
Parameter Sets: InputObjectParameterSet
```

### -Version

Specifies the version the resource is to be updated to.

```yml
Type: string
Parameter Sets: NameParameterSet
```

### -Prerelease

When specified, allows updating to a prerelease version.

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet
```

### -Repository

Specifies one or more repository names to update from.
If not specified, will search in the highest priority repository.

```yml
Type: string[]
Parameter Sets: NameParameterSet
```

### -Scope

Specifies the scope of the resource to update.

```yml
Type: string
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet 
AllowedValues: 'CurrentUser','AllUsers'
```

### -TrustRepository

Suppresses being prompted for untrusted sources.

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet  
```

### -Credential

Optional credentials to be used when accessing a repository.

```yml
Type: PSCredential
Parameter Sets: NameParameterSet
```

### -Quiet

Suppresses progress information.

```yml
Type: SwitchParameter
Parameter Sets: (All)
```

### -AcceptLicense

For modules that require a license, AcceptLicense automatically accepts the license agreement during update.

```yml
Type: SwitchParameter
Parameter Sets: (All)
```

### -NoClobber

Prevents updating modules that have the same cmdlets as a differently named module already

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet
```


### Outputs

No output.

## Notes
Input object still needs to be implemented.

Should a -PassThru parameter be added?

## Tests

Most update tests can be performed on a local repository.  

Some tests should be performed on remote repository (PSGallery) to verify remote operation, but can be limited.  

### -Name param

- Single name search
- Wildcard search
- Multiple name search
- Cancel search
- Errors: Not found (single name, wildcard, multiple name)
- Errors: Repository: Invalid name, connection error, etc

### -Type InputObject

- Validate pipeline input
- Errors: The object passed in is not the correct type

### -Version param

- Validate the resource is updated to the correct version 
- Validate wild card (if supported), that the resource is updated to the correct version range
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

### -Scope param

- Validate only the resource from the proper scope gets updated

### -TrustRepository param

- Validate that prompt is suppresed

### -Quiet param

- Validate that progress information is suppressed

### -AcceptLicense

- Validate that modules which require license agreements are approved without a prompt

### -NoClobber

- Validate that resources are not overwritten when flag is passed


## Work Items

### Create cmdlet and parameter sets

Create cmdlet class, parameters, and functional stubs  
1 day

### Implement package search helpers

Create helper functions that support all search functions  
Use existing code as starting point  
4 days

### Investigate V3 API wildcard support

Look into how V3 APIs can be used to reduce what is returned from the server  
7 days

### Implement package filtering functions

Create helper functions to provide filtering of search results  
3 days

### Investigate and implement local cache

Write mini-design document on local caching strategy  
Implement local cache  
10 days

### Create test support

Create any test repositories and mocks for tests  
4 days

### Write cmdlet tests

Create all functional tests to validate cmdlet  
5 days

### Write support function tests

Create all needed tests to validate caching and search helpers  
5 days
