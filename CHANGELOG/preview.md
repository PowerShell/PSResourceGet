## [1.1.0-RC1](https://github.com/PowerShell/PSResourceGet/compare/v1.1.0-preview2...v1.1.0-RC1) - 2024-09-13

### New Features

- Group Policy configurations for enabling or disabling PSResource repositories (#1730)

### Bug Fixes

- Fix packaging name matching when searching in local repositories (#1731)
- `Compress-PSResource` `-PassThru` now passes `FileInfo` instead of string (#1720)
- Fix for `Compress-PSResource` not properly compressing scripts  (#1719) 
- Add `AcceptLicense` to Save-PSResource (#1718 Thanks @o-l-a-v!)
- Better support for NuGet v2 feeds (#1713 Thanks @o-l-a-v!)
- Better handling of `-WhatIf` support in `Install-PSResource` (#1531 Thanks @o-l-a-v!)
- Fix for some nupkgs failing to extract due to empty directories (#1707 Thanks @o-l-a-v!)
- Fix for searching for `-Name *` in `Find-PSResource` (#1706 Thanks @o-l-a-v!)

## [1.1.0-preview2](https://github.com/PowerShell/PSResourceGet/compare/v1.1.0-preview1...v1.1.0-preview2) - 2024-09-13

### New Features

- New cmdlet `Compress-PSResource` which packs a package into a .nupkg and saves it to the file system (#1682, #1702)
- New `-Nupkg` parameter for `Publish-PSResource` which pushes pushes a .nupkg to a repository (#1682)
- New `-ModulePrefix` parameter for `Publish-PSResource` which adds a prefix to a module name for container registry repositories to add a module prefix.This is only used for publishing and is not part of metadata. MAR will drop the prefix when syndicating from ACR to MAR (#1694)

### Bug Fixes

- Add prerelease string when NormalizedVersion doesn't exist, but prelease string does (#1681 Thanks @sean-r-williams)
- Add retry logic when deleting files (#1667 Thanks @o-l-a-v!)
- Fix broken PAT token use (#1672)
- Updated error messaging for authenticode signature failures (#1701)

## [1.1.0-preview1](https://github.com/PowerShell/PSResourceGet/compare/v1.0.3...v1.1.0-preview1) - 2024-04-01

### New Features

- Support for Azure Container Registries (#1495, #1497-#1499, #1501, #1502, #1505, #1522, #1545, #1548, #1550, #1554, #1560, #1567, 
#1573, #1576, #1587, #1588, #1589, #1594, #1598, #1600, #1602, #1604, #1615)

### Bug Fixes

- Fix incorrect request URL when installing resources from ADO (#1597 Thanks @anytonyoni!)
- Fix for swallowed exceptions (#1569)
- Fix for PSResourceGet not working in Constrained Languange Mode (#1564)
