# Publish-PSResource

The `Publish-PSResource` cmdlet combines the `Publish-Module` and `Publish-Script` cmdlets from V2.

It publishes a specified resource from the local computer to an online Nuget-based gallery by using an API key, stored as part of a user's profile in the gallery or to a local repository. You can specify the resource to publish either by the resource's name, or by the path to the folder containing the module or script resource.

## Syntax

### PathParameterSet
``` PowerShell
[[-APIKey] <string>] [-Repository <string>] [-DestinationPath <string>] [-Path] <string>] [-Credential <pscredential>] [-SkipDependenciesCheck] [-ReleaseNotes <string>] [-Tags <string[]>] [-LicenseUrl <string>] [-IconUrl <string>] [-ProjectUrl <string>] [-NuspecPath <string>] [-Proxy <Uri>][-ProxyCredential <pscredential>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### PathLiteralParameterSet
``` PowerShell
[[-APIKey] <string>] [-Repository <string>] [-DestinationPath <string>] [-LiteralPath] <string>] [-Credential <pscredential>] [-SkipDependenciesCheck] [-ReleaseNotes <string>] [-Tags <string[]>] [-LicenseUrl <string>] [-IconUrl <string>] [-ProjectUrl <string>] [-NuspecPath <string>] [-Proxy <Uri>][-ProxyCredential <pscredential>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## Parameters

### -APIKey

Specifies the API key that you want to use to publish a resource to the online gallery.
Not mandatory.

```yml
Type: string
Parameter Sets: PathParameterSet, PathLiteralSet
```

### -Repository

Specifies the repository to publish to.

```yml
Type: string
Parameter Sets: PathParameterSet, PathLiteralSet
```

### -DestinationPath

Specifies the location to be used to publish a nupkg locally.

```yml
Type: string
Parameter Sets: (All)
```

### -Path

When specified, includes prerelease versions in search.

```yml
Type: string
Parameter Sets: PathParameterSet
```

### -LiteralPath

Specifies a path to one or more locations. Unlike the Path parameter, the value of the LiteralPath parameter is used exactly as entered. No characters are interpreted as wildcards. If the path includes escape characters, enclose them in single quotation marks. Single quotation marks tell PowerShell not to interpret any characters as escape sequences.

```yml
Type: string
Parameter Sets: PathLiteralParameterSet
```

### -Credential

Specifies a user account that has rights to a specific repository (used for finding dependencies).

```yml
Type: PSCredential
Parameter Sets: PathParameterSet, PathLiteralSet
```

### -SkipDependenciesCheck

Bypasses the default check that all dependencies are present.

```yml
Type: SwitchParameter
Parameter Sets: PathParameterSet, PathLiteralSet
```

### -ReleaseNotes

Updates nuspec: specifies a string containing release notes or comments that you want to be available to users of this version of the resource.

```yml
Type: string
Parameter Sets: PathParameterSet, PathLiteralSet
```

### -Tags

Updates nuspec: adds one or more tags to the resource that you are publishing. This applies only to the nuspec.

```yml
Type: string[]
Parameter Sets: PathParameterSet, PathLiteralSet
```

### -LicenseUrl

Updates nuspec: specifies the URL of licensing terms for the resource you want to publish.

```yml
Type: string
Parameter Sets: PathParameterSet, PathLiteralSet
```

### -IconUrl

Updates nuspec: specifies the URL of an icon for the resource.

```yml
Type: string
Parameter Sets: PathParameterSet, PathLiteralSet
```

### -ProjectUrl

Updates nuspec: specifies the URL of a webpage about this project.

```yml
Type: string
Parameter Sets: PathParameterSet, PathLiteralSet
```

### -Exclude

Excludes files from a nuspec

```yml
Type: string[]
Parameter Sets: ModuleNameParameterSet
```

### -NuspecPath

Specifies a nuspec file by path rather than relying on this module to produce one.

```yml
Type: string
Parameter Sets: PathParameterSet, PathLiteralSet
```

### -Proxy

Specifies a proxy server for the request, rather than a direct connection to the internet resource.

```yml
Type: Uri
Parameter Sets: PathParameterSet, PathLiteralSet
```

### -ProxyCredential

Specifies a user account that has permission to use the proxy server that is specified by the Proxy parameter.

```yml
Type: PSCredential
Parameter Sets: PathParameterSet, PathLiteralSet
```

### -PassThru

When specified, displays the succcessfully published resource and its information.

```yml
Type: PSCredential
Parameter Sets: PathParameterSet, PathLiteralSet
```

### Outputs

if `-PassThru` is not specified output is none

if `-PassThru` is specified output is:

```json
"PSRepositoryItemInfo" : {
    "Name",
    "Version",
    "Type",
    "Description",
    "Author",
    "CompanyName",
    "Copyright",
    "PublishedDate",
    "InstalledDate",
    "UpdatedDate",
    "LicenseUri",
    "ProjectUri",
    "IconUri",
    "Tags",
    "Includes",
    "PowerShellGetFormatVersion",
    "ReleaseNotes",
    "Dependencies",
    "RepositorySourceLocation",
    "Repository",
    "PackageManagementProvider",
    "AdditionalMetadata"
}
```

## Notes

## Tests

Some tests should be performed to publish a resource of type Module, and othersfor resource of type Script.

Tests should have varying levels of required and optional nuspec data to test parsing helper methods.

### -APIKey param

- Validate not null or empty if paramater arguemnt provided
- Errors: APIKey not valid or incorrect format

### -Repository param

-Validate not null or empty if paramater arguemnt provided
-Errors: Repository referenced is not registered or found.

### -DestinationPath param

-Validate not null or empty if paramater arguemnt provided
-Errors: DestinationPath does not exist.

### -Path param

-Validate not null or empty if paramater arguemnt provided
-Errors: validate not null or empty if provided, path does not exist

### -LiteralPath param

-Validate not null or empty if paramater arguemnt provided
-Errors: Literal path does not exist.

### -Credential param

-Validate not null or empty if paramater arguemnt provided

### -ReleaseNotes param

-Validate not null or empty if paramater arguemnt provided

### -Tags param

-Validate not null or empty if paramater arguemnt provided

### -LicenseUrl param

-Validate not null or empty if paramater arguemnt provided

### -IconUrl param

-Validate not null or empty if paramater arguemnt provided

### -ProjectUrl param

-Validate not null or empty if paramater arguemnt provided

### -Exclude param

-Validate not null or empty if paramater arguemnt provided
-Errors: files specified do not exist

### -NuspecPath param

-Validate not null or empty if paramater arguemnt provided
-Errors: file does not exist, not of valid format or extension.

### -Proxy param

-Validate not null or empty if paramater arguemnt provided

## Work Items

### Create cmdlet and parameter sets

Create cmdlet class, parameters, and functional stubs
1 day

### Implement publish function helper methods

Create helper functions that support all search functions
Use existing code as starting point
1-2 days

### Implement publish function method

Create main publish function method, use existing code as starting point
1 day

### Create test support

Create any test repositories and mocks for tests
1 day

### Write cmdlet tests

Create all functional tests to validate cmdlet
1-2 days

### Write support function tests

Create all needed tests to validate publish helpers
1-2 days
