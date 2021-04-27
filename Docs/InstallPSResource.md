# Install-PSResource

The `Install-PSResource` cmdlet combines the `Install-Module, Install-Script` cmdlets from V2.
It performs an installation from a package found on a repository (local or remote) based on the `-Name` parameter argument.
It does not return an object.
Other parameters allow the returned results to be further filtered.  

## Syntax

### NameParameterSet (Default)
``` PowerShell
[[-Name] <string[]>] [-Version <string>] [-Prerelease] [-Repository <string[]>] 
[-Credential <pscredential>] [-Scope <string>] [-NoClobber] [-TrustRepository]
[-Reinstall] [-Quiet] [-AcceptLicense] [-WhatIf] [-Confirm] [<CommonParameters>]
```
### InputObjectParameterSet
``` PowerShell
[[-InputObject] <object[]>] 
```
### RequiredResourceParameterSet
``` PowerShell
[[-RequiredResource] <object>] [-Quiet] [-WhatIf] [-Confirm] [<CommonParameters>]
```
### RequiredResourceFileParameterSet
``` PowerShell
[[-RequiredResourceFile] <string>] [-Quiet] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## Parameters

### -Name

Name of a resource or resources to be installed.
Accepts wild card characters.

```yml
Type: string[]
Parameter Sets: NameParameterSet
```

### -Version

Specifies the version or version range of the resource to be installed.

```yml
Type: string
Parameter Sets: NameParameterSet
```

### -Prerelease

When specified, allows installation of prerelease versions.

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
```

### -Repository

Specifies one or more repository names to search.
If not specified, search will the highest priority repository.

```yml
Type: string[]
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
```

### -Credential

Optional credentials to be used when accessing a private repository.

```yml
Type: PSCredential
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
```

### -Scope

When specified, will install to either CurrentUser or AllUsers scope.

```yml
Type: String
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
AllowedValues: 'CurrentUser','AllUsers'
```

### -NoClobber

Prevents installing modules that have the same cmdlets as a differently named module already installed. 

```yml
Type: string[]
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
```

### -TrustRepository

If specified, suppresses prompt for untrusted repositories.

```yml
Type: SwitchParameter
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
```

### -Reinstall

If specified, overwrites a previously installed resource with the same name and version.

```yml
Type: string
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
```

### -Quiet

If specified, suppresses progress information.

```yml
Type: string
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
```

### -AcceptLicense

If specified, modules that require a license agreement will be automatically accepted during installation.

```yml
Type: string
Parameter Sets: NameParameterSet, RequiredResourceFileParameterSet
```

### -RequiredResourceFile

Specifies a file 

```yml
Type: string
Parameter Sets: RequiredResourceFileParameterSet
```

### -RequiredResourceJSON

Installs the resources specified in the json configuration.

```yml
Type: string or hashtable
Parameter Sets: RequiredResourceParameterSet
```

### -InputObject

Installs the resources passed in via pipeline.

```yml
Type: ?
Parameter Sets: InputObjectParameterSet
```

### Outputs

No output.

## Notes


### -Name param

- Single name search
- Wildcard search
- Multiple name search
- Cancel search
- Errors: Not found (single name, wildcard, multiple name)
- Errors: Repository: Invalid name, connection error, etc

### -Version param

- Validate correct resource version installed
- Validate wild card (if supported), correct version range installed
- Errors: Not found
- Errors: Invalid version string format

### -Prerelease param

- Validate prerelease version installed

### -Repository param

- All repository search
- Single repository search
- Multiple repository search
- Errors: Repository not found

### -Credential param

- Validate credential search
- Errors: Credential: Invalid

### -Scope param

- Validate installation to correct scope (both current and all users)

### -NoClobber param

- Validate proper warning message if clobbering will happen

### -TrustRepository param

- Validate that user is not prompted and has access to repository

### -Reinstall param

- Validate proper installation when the module version is already installed
- Validate proper installation if resource is not installed

### -Quiet param

- Validate that progress bar is supressed

### -AcceptLicense param

- Validate that there is no prompt for modules that require license agreement

### -RequiredResourceFile param

-

### -RequiredResourceJSON param

- Validate that installation works with hashtable input
- Validate that installation works with string input
- Validate that all parameters work in string or hashtable
- Error: incorrect formatting
- Error: incorrect object type

### -InputObject param

- Validate that object passed in is able to install


## Work Items

### Create cmdlet and parameter sets

Create cmdlet class, parameters, and functional stubs  
1 day

### Implement package install helpers

Create helper functions that support all search functions  
Use existing code as starting point  
4 days

### Create test support

Create any test repositories and mocks for tests  
4 days

### Write cmdlet tests

Create all functional tests to validate cmdlet  
5 days

