// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;
using System.IO;
using System.Runtime.ExceptionServices;

public interface IServerAPICalls
{
    #region Methods
    /// <summary>
    /// Find method which allows for searching for all packages from a repository and returns latest version for each.
    /// Examples: Search -Repository PSGallery
    /// </summary>
    string[] FindAll(bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi);

    /// <summary>
    /// Find method which allows for searching for packages with tag from a repository and returns latest version for each.
    /// Examples: Search -Tag "JSON" -Repository PSGallery
    /// </summary>
    string[] FindTag(string tag, bool includePrerelease, ResourceType _type, out ExceptionDispatchInfo edi);
  
    /// <summary>
    /// Find method which allows for searching for single name and returns latest version.
    /// Name: no wildcard support
    /// Examples: Search "PowerShellGet"
    /// </summary>
    string FindName(string packageName, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi);

    /// <summary>
    /// Find method which allows for searching for single name and returns latest version.
    /// Name: no wildcard support
    /// Examples: Search "PowerShellGet" -Tag "Provider"
    /// </summary>
    string FindNameWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi);

    /// <summary>
    /// Find method which allows for searching for single name with version range.
    /// Name: no wildcard support
    /// Version: supports wildcards
    /// Examples: Search "PowerShellGet" "[3.0.0.0, 5.0.0.0]"
    ///           Search "PowerShellGet" "3.*"
    /// </summary>
    string[] FindNameGlobbing(string packageName, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi);

    /// <summary>
    /// Find method which allows for searching for single name and tag with version range.
    /// Name: no wildcard support
    /// Version: supports wildcards
    /// Examples: Search "PowerShellGet" "[3.0.0.0, 5.0.0.0]"
    ///           Search "PowerShellGet" "3.*"
    /// </summary>
    string[] FindNameGlobbingWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi);

    /// <summary>
    /// Find method which allows for searching for single name with specific version.
    /// Name: no wildcard support
    /// Version: no wildcard support
    /// Examples: Search "PowerShellGet" "2.2.5"
    /// </summary>
    string[] FindVersionGlobbing(string packageName, VersionRange versionRange, bool includePrerelease, ResourceType type, bool getOnlyLatest, out ExceptionDispatchInfo edi);

    // <summary>
    /// Find method which allows for searching for single name with specific version.
    /// Name: no wildcard support
    /// Version: no wildcard support
    /// Examples: Search "PowerShellGet" "2.2.5"
    /// </summary>
    string FindVersion(string packageName, string version, ResourceType type, out ExceptionDispatchInfo edi);

    // <summary>
    /// Find method which allows for searching for single name and tag with specific version.
    /// Name: no wildcard support
    /// Version: no wildcard support
    /// Examples: Search "PowerShellGet" "2.2.5" -Tag "Provider"
    /// </summary>
    string FindVersionWithTag(string packageName, string version, string[] tags, ResourceType type, out ExceptionDispatchInfo edi);

    /// <summary>
    /// Installs specific package.
    /// Name: no wildcard support.
    /// Examples: Install "PowerShellGet"
    /// </summary>
    Stream InstallName(string packageName, bool includePrerelease, out ExceptionDispatchInfo edi);

    /// <summary>
    /// Installs package with specific name and version.
    /// Name: no wildcard support.
    /// Version: no wildcard support.
    /// Examples: Install "PowerShellGet" -Version "3.0.0.0"
    ///           Install "PowerShellGet" -Version "3.0.0-beta16"
    /// </summary>    
    Stream InstallVersion(string packageName, string version, out ExceptionDispatchInfo edi);

    #endregion
}
