# PSGet → PSResourceGet Migration Tool

A PowerShell-based tool to automatically migrate scripts from PowerShellGet v2 (PSGet)
cmdlets to their [PSResourceGet](https://github.com/PowerShell/PSResourceGet) equivalents.

## Quick Start

```powershell
# Dry-run: scan a directory and show the migration report
.\tool\migration\ConvertTo-PSResourceGet.ps1 -Path .\scripts -Recurse

# Apply changes (creates .bak backups automatically)
.\tool\migration\ConvertTo-PSResourceGet.ps1 -Path .\scripts -Recurse -Apply

# Apply changes with a custom backup directory
.\tool\migration\ConvertTo-PSResourceGet.ps1 -Path .\scripts -Recurse -Apply -BackupPath .\backups

# Get structured output for pipeline processing
.\tool\migration\ConvertTo-PSResourceGet.ps1 -Path .\deploy.ps1 -PassThru
```

## What It Does

### Cmdlet Conversions

| PSGet v2 | PSResourceGet |
|---|---|
| `Find-Module` | `Find-PSResource` |
| `Find-Script` | `Find-PSResource` |
| `Find-DscResource` | `Find-PSResource -Type DscResource` |
| `Find-Command` | `Find-PSResource -Type Command` |
| `Install-Module` | `Install-PSResource` |
| `Install-Script` | `Install-PSResource` |
| `Update-Module` | `Update-PSResource` |
| `Update-Script` | `Update-PSResource` |
| `Uninstall-Module` | `Uninstall-PSResource` |
| `Uninstall-Script` | `Uninstall-PSResource` |
| `Save-Module` | `Save-PSResource` |
| `Save-Script` | `Save-PSResource` |
| `Publish-Module` | `Publish-PSResource` |
| `Publish-Script` | `Publish-PSResource` |
| `Get-InstalledModule` | `Get-InstalledPSResource` |
| `Get-InstalledScript` | `Get-InstalledPSResource` |
| `Register-PSRepository` | `Register-PSResourceRepository` |
| `Unregister-PSRepository` | `Unregister-PSResourceRepository` |
| `Set-PSRepository` | `Set-PSResourceRepository` |
| `Get-PSRepository` | `Get-PSResourceRepository` |
| `New-ScriptFileInfo` | `New-PSScriptFileInfo` |
| `Test-ScriptFileInfo` | `Test-PSScriptFileInfo` |
| `Update-ScriptFileInfo` | `Update-PSScriptFileInfo` |
| `Update-ModuleManifest` | `Update-PSModuleManifest` |

### Parameter Conversions

| PSGet v2 | PSResourceGet | Notes |
|---|---|---|
| `-RequiredVersion '1.0'` | `-Version '1.0'` | Exact version |
| `-MinimumVersion '1.0'` | `-Version '[1.0,)'` | NuGet version range |
| `-MaximumVersion '2.0'` | `-Version '(,2.0]'` | NuGet version range |
| `-MinimumVersion '1.0' -MaximumVersion '2.0'` | `-Version '[1.0,2.0]'` | Combined range |
| `-AllVersions` | `-Version '*'` | Wildcard |
| `-AllowPrerelease` | `-Prerelease` | Switch renamed |
| `-NuGetApiKey` | `-ApiKey` | Parameter renamed |
| `-Force` (with `Install-Module`) | `-Reinstall` | Semantic equivalent |

### Removed Parameters (with warnings)

- `-AllowClobber` — Not needed; PSResourceGet allows clobber by default.
- `-SkipPublisherCheck` — Not supported in PSResourceGet.
- `-Proxy` / `-ProxyCredential` — Configure at system level instead.
- `-Force` — Semantics differ; review migrated commands.

### Unsupported Cmdlets

- `Find-RoleCapability` — No PSResourceGet equivalent. Generates a warning.

## Using as a Script Library

```powershell
# Dot-source the script to load all functions
. .\tool\migration\ConvertTo-PSResourceGet.ps1

# Scan a single file
$results = ConvertTo-PSResourceGetScript -Path .\deploy.ps1

# Display the report
$results | Format-MigrationReport

# Apply changes
ConvertTo-PSResourceGetScript -Path .\deploy.ps1 -Apply

# Get the mapping tables
Get-PSGetCommandMapping
Get-PSGetParameterMapping
```

## How It Works

The tool uses PowerShell's **Abstract Syntax Tree (AST)** parser to accurately identify
PSGet v2 cmdlet invocations. This approach is more reliable than regex-based text
replacement because it:

- Correctly handles splatting, multiline commands, and nested expressions.
- Identifies commands by their AST node type, avoiding false positives in comments or strings.
- Preserves file structure outside of the converted commands.

## Output

The migration report shows each file with:
- **Line number** of each PSGet v2 command found
- **Original** command (in red)
- **Converted** command (in green)
- **Warnings** for removed parameters or unsupported cmdlets (in yellow)
- **Summary** counts of total, converted, and problematic commands
