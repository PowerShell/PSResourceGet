# Platform-Aware Installation Flow

## Extraction Filtering Pipeline

```mermaid
flowchart TD
    A["Install-PSResource -Name MyModule<br/>[-SkipRuntimeFiltering] [-RuntimeIdentifier] [-TargetFramework]"] --> B["1. Download .nupkg from gallery<br/>(full package, all platforms)"]
    B --> C["2. Open as ZipArchive in TryExtractToDirectory()"]
    C --> D["Detect TFM"]
    C --> E["Detect RID"]

    D --> D1{"-TargetFramework<br/>specified?"}
    D1 -->|YES| D2["Parse user value<br/>NuGetFramework.ParseFolder()"]
    D1 -->|NO| D3["Auto-detect via<br/>GetCurrentFramework()"]
    D2 --> D4["GetBestLibFramework()<br/>FrameworkReducer.GetNearest()<br/>→ Best TFM e.g., net8.0"]
    D3 --> D4

    E --> E1{"-RuntimeIdentifier<br/>specified?"}
    E1 -->|YES| E2["Use user value<br/>e.g., linux-x64"]
    E1 -->|NO| E3["DetectRuntimeIdentifier()<br/>ProcessArchitecture + OSPlatform"]
    E2 --> E4["Target RID<br/>e.g., win-x64"]
    E3 --> E4

    D4 --> F["3. Filter each zip entry"]
    E4 --> F

    F --> G{"Entry type?"}

    G -->|"runtimes/**"| H{"-SkipRuntimeFiltering?"}
    H -->|YES| I["✅ INCLUDE all runtimes"]
    H -->|NO| J{"ShouldIncludeEntry()<br/>RID compatible?"}
    J -->|"runtimes/win-x64/ vs win-x64"| K["✅ INCLUDE"]
    J -->|"runtimes/linux-arm64/ vs win-x64"| L["❌ SKIP"]
    J -->|"runtimes/osx-arm64/ vs win-x64"| L

    G -->|"lib/**"| M{"Best TFM<br/>match found?"}
    M -->|NO| N["✅ INCLUDE all lib/ entries"]
    M -->|YES| O{"ShouldIncludeLibEntry()<br/>TFM matches best?"}
    O -->|"lib/net8.0/ vs best net8.0"| P["✅ INCLUDE"]
    O -->|"lib/net472/ vs best net8.0"| Q["❌ SKIP"]
    O -->|"lib/netstandard2.0/ vs best net8.0"| Q

    G -->|"*.psd1, *.psm1, etc."| R["✅ INCLUDE always"]

    K --> S["4. DeleteExtraneousFiles()<br/>Delete: Content_Types.xml, _rels/, package/, .nuspec"]
    I --> S
    N --> S
    P --> S
    R --> S

    S --> T["5. Installed Module<br/>MyModule/1.0.0/<br/>├── MyModule.psd1<br/>├── lib/net8.0/ ← only best TFM<br/>└── runtimes/win-x64/ ← only matching RID"]

    style K fill:#2d6a2d,color:#fff
    style I fill:#2d6a2d,color:#fff
    style N fill:#2d6a2d,color:#fff
    style P fill:#2d6a2d,color:#fff
    style R fill:#2d6a2d,color:#fff
    style L fill:#8b1a1a,color:#fff
    style Q fill:#8b1a1a,color:#fff
    style T fill:#1a3d5c,color:#fff
```

### Code References

| Diagram Step | Method | File |
|---|---|---|
| Cmdlet parameters | `SkipRuntimeFiltering`, `RuntimeIdentifier`, `TargetFramework` | [InstallPSResource.cs#L147](src/code/InstallPSResource.cs#L147) |
| TryExtractToDirectory | Entry point for filtered extraction | [InstallHelper.cs#L1181](src/code/InstallHelper.cs#L1181) |
| GetCurrentFramework | Auto-detect TFM from `RuntimeInformation.FrameworkDescription` | [InstallHelper.cs#L1394](src/code/InstallHelper.cs#L1394) |
| GetBestLibFramework | `FrameworkReducer.GetNearest()` for lib/ selection | [InstallHelper.cs#L1289](src/code/InstallHelper.cs#L1289) |
| ShouldIncludeLibEntry | Filter lib/ entries against best TFM | [InstallHelper.cs#L1358](src/code/InstallHelper.cs#L1358) |
| DetectRuntimeIdentifier | Auto-detect RID from OS + architecture | [RuntimeIdentifierHelper.cs#L204](src/code/RuntimeIdentifierHelper.cs#L204) |
| ShouldIncludeEntry | Filter runtimes/ entries against target RID | [RuntimePackageHelper.cs#L83](src/code/RuntimePackageHelper.cs#L83) |
| DeleteExtraneousFiles | Cleanup: remove NuGet packaging artifacts | [InstallHelper.cs#L1709](src/code/InstallHelper.cs#L1709) |

## Dependency Parsing (TFM-Aware)

```mermaid
flowchart TD
    A[".nuspec dependencies"] --> B{"Source type?"}
    
    B -->|"Local repo (.nuspec file)"| C["GetHashtableForNuspec()<br/>NuspecReader.GetDependencyGroups()<br/>→ ParseNuspecDependencyGroups()"]
    B -->|"Remote V3 (JSON)"| D["Parse dependencyGroups JSON<br/>→ TryConvertFromJson()"]
    
    C --> E["FrameworkReducer.GetNearest()<br/>picks best TFM group"]
    D --> E
    
    E --> F{"Current runtime?"}
    
    F -->|".NET 8 (PS 7.4)"| G["Select net8.0 group<br/>→ 0 dependencies<br/>(APIs are inbox)"]
    F -->|".NET Framework 4.7.2 (PS 5.1)"| H["Select net472 group<br/>→ 2 dependencies<br/>(System.Memory, System.Buffers)"]

    style G fill:#2d6a2d,color:#fff
    style H fill:#c47a20,color:#fff
```

### Code References

| Diagram Step | Method | File |
|---|---|---|
| GetHashtableForNuspec | `NuspecReader` parses .nuspec for local repos | [LocalServerApiCalls.cs#L958](src/code/LocalServerApiCalls.cs#L958) |
| TryConvertFromJson | Parses V3 JSON dependency groups for remote repos | [PSResourceInfo.cs#L618](src/code/PSResourceInfo.cs#L618) |
| ParseNuspecDependencyGroups | TFM-aware group selection via `FrameworkReducer` | [PSResourceInfo.cs#L1704](src/code/PSResourceInfo.cs#L1704) |

## Before vs After

```mermaid
graph LR
    subgraph BEFORE["Before (no filtering) — ~56 MB"]
        B1["lib/net472/"]
        B2["lib/netstandard2.0/"]
        B3["lib/net6.0/"]
        B4["lib/net8.0/ ✓"]
        B5["runtimes/win-x64/ ✓"]
        B6["runtimes/win-x86/"]
        B7["runtimes/linux-x64/"]
        B8["runtimes/linux-arm64/"]
        B9["runtimes/osx-x64/"]
        B10["runtimes/osx-arm64/"]
    end

    subgraph AFTER["After (with filtering) — ~4 MB"]
        A1["lib/net8.0/ ✓"]
        A2["runtimes/win-x64/ ✓"]
    end

    style B1 fill:#8b1a1a,color:#fff
    style B2 fill:#8b1a1a,color:#fff
    style B3 fill:#8b1a1a,color:#fff
    style B4 fill:#2d6a2d,color:#fff
    style B5 fill:#2d6a2d,color:#fff
    style B6 fill:#8b1a1a,color:#fff
    style B7 fill:#8b1a1a,color:#fff
    style B8 fill:#8b1a1a,color:#fff
    style B9 fill:#8b1a1a,color:#fff
    style B10 fill:#8b1a1a,color:#fff
    style A1 fill:#2d6a2d,color:#fff
    style A2 fill:#2d6a2d,color:#fff
```

## RID Compatibility Chain

```mermaid
flowchart LR
    W1["win10-x64"] --> W2["win-x64"] --> W3["win"] --> ANY["any"]
    L1["linux-musl-x64"] --> L2["linux-x64"] --> L3["linux"] --> U["unix"] --> ANY
    O1["osx.12-arm64"] --> O2["osx-arm64"] --> O3["osx"] --> U


```
