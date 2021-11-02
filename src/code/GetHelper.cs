// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using Dbg = System.Diagnostics.Debug;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using MoreLinq.Extensions;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using static Microsoft.PowerShell.PowerShellGet.UtilClasses.PSResourceInfo;
using NuGet.Versioning;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// Get helper class provides the core functionality for Get-InstalledPSResource.
    /// </summary>
    internal class GetHelper
    {
        #region Members

        private readonly PSCmdlet _cmdletPassedIn;
        private readonly Dictionary<string, PSResourceInfo> _scriptDictionary;

        #endregion

        #region Constructors

        public GetHelper(PSCmdlet cmdletPassedIn)
        {
            _cmdletPassedIn = cmdletPassedIn;
            _scriptDictionary = new Dictionary<string, PSResourceInfo>();
        }

        #endregion

        #region Public methods

        public IEnumerable<PSResourceInfo> FilterPkgPaths(
            string[] name,
            VersionRange versionRange,
            List<string> pathsToSearch)
        {
            List<string> pgkPathsByName = FilterPkgPathsByName(name, pathsToSearch);

            foreach (string pkgPath in FilterPkgPathsByVersion(versionRange, pgkPathsByName))
            {
                PSResourceInfo pkg = OutputPackageObject(pkgPath, _scriptDictionary);
                if (pkg != null)
                {
                    yield return pkg;
                }
            }
        }

        // Filter packages by user provided name
        public List<string> FilterPkgPathsByName(string[] names, List<string> dirsToSearch)
        {
            List<string> wildCardDirsToSearch = new List<string>();

            if (names == null)
            {
                _cmdletPassedIn.WriteVerbose("No names were provided.");
                return wildCardDirsToSearch;
            }

            foreach (string name in names)
            {
                WildcardPattern nameWildCardPattern = new WildcardPattern(name, WildcardOptions.IgnoreCase);

                wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(
                    path => nameWildCardPattern.IsMatch(
                        GetResourceNameFromPath(path))));
            }

            return wildCardDirsToSearch;
        }

        // Filter by user provided version
        public IEnumerable<String> FilterPkgPathsByVersion(VersionRange versionRange, List<string> dirsToSearch)
        {
            Dbg.Assert(versionRange != null, "Version Range cannot be null");
            
            // if no version is specified, just get the latest version
            foreach (string pkgPath in dirsToSearch)
            {
                _cmdletPassedIn.WriteVerbose(string.Format("Searching through package path: '{0}'", pkgPath));

                // if this is a module directory
                if (Directory.Exists(pkgPath))
                {
                    // search modules paths
                    // ./Modules/Test-Module/1.0.0
                    // ./Modules/Test-Module/2.0.0
                    _cmdletPassedIn.WriteVerbose(string.Format("Searching through package path: '{0}'", pkgPath));

                    string[] versionsDirs = Utils.GetSubDirectories(pkgPath);

                    if (versionsDirs.Length == 0)
                    {
                        _cmdletPassedIn.WriteVerbose(
                            $"No version subdirectories found for path: {pkgPath}");
                        continue;
                    }

                    // sort and reverse to get package versions in descending order to maintain consistency with V2
                    Array.Sort(versionsDirs);
                    Array.Reverse(versionsDirs);

                    foreach (string versionPath in versionsDirs)
                    {
                        _cmdletPassedIn.WriteVerbose(string.Format("Searching through package version path: '{0}'", versionPath));
                        if(!Utils.GetVersionFromPSGetModuleInfoFile(installedPkgPath: versionPath,
                            isModule: true,
                            cmdletPassedIn: _cmdletPassedIn,
                            out NuGetVersion dirAsNugetVersion))
                        {
                            // skip to next iteration of the loop
                            continue;
                        }

                        _cmdletPassedIn.WriteVerbose(string.Format("Directory parsed as NuGet version: '{0}'", dirAsNugetVersion));

                        if (versionRange.Satisfies(dirAsNugetVersion))
                        {
                            // This will be one version or a version range.
                            // yield results then continue with this iteration of the loop
                            yield return versionPath;
                        }
                    }
                }
                else if (File.Exists(pkgPath))
                {
                    // if it's a script
                    if (versionRange == null || versionRange == VersionRange.All)
                    {
                        // yield results then continue with this iteration of the loop
                        yield return pkgPath;

                        // We are now done with the current iteration of the for loop because
                        // only one script version can be installed in a particular script path at a time.
                        // if looking for all versions and one was found, then we have found all possible versions at that ./Scripts path
                        // and do not need to parse and check for the version number in the metadata file.
                    }
                    else
                    {
                        // check to make sure it's within the version range.
                        // script versions will be parsed from the script xml file
                        PSResourceInfo scriptInfo = OutputPackageObject(pkgPath, _scriptDictionary);
                        if(!Utils.GetVersionFromPSGetModuleInfoFile(installedPkgPath: pkgPath,
                            isModule: false,
                            cmdletPassedIn: _cmdletPassedIn,
                            out NuGetVersion dirAsNugetVersion))
                        {
                            // skip to next iteration of the loop
                            yield return pkgPath;
                        }

                        _cmdletPassedIn.WriteVerbose(string.Format("Directory parsed as NuGet version: '{0}'", dirAsNugetVersion));
                        if (versionRange.Satisfies(dirAsNugetVersion))
                        {
                            _scriptDictionary.Add(pkgPath, scriptInfo);
                            yield return pkgPath;
                        }
                    }
                }
            }
        }

        // Create package object for each found resource directory
        public PSResourceInfo OutputPackageObject(string pkgPath, Dictionary<string,PSResourceInfo> scriptDictionary)
        {
            // If the package path is in the deserialized script dictionary, just return that
            if (scriptDictionary.ContainsKey(pkgPath))
            {
                return scriptDictionary[pkgPath];
            }

            // If the pkgName from pkgpath is a script, find the xml file
            string pkgName = Utils.GetInstalledPackageName(pkgPath);
            string xmlFilePath;
            if (File.Exists(pkgPath))
            {
                // Package path is a script file
                xmlFilePath = System.IO.Path.Combine(
                    (new DirectoryInfo(pkgPath).Parent).FullName,
                    "InstalledScriptInfos",
                    $"{pkgName}_InstalledScriptInfo.xml");
            }
            else
            {
                // Otherwise assume it's a module, and look for the xml path that way
                xmlFilePath = System.IO.Path.Combine(pkgPath, "PSGetModuleInfo.xml");
            }

            // Read metadata from XML and parse into PSResourceInfo object
            _cmdletPassedIn.WriteVerbose(string.Format("Reading package metadata from: '{0}'", xmlFilePath));
            if (TryRead(xmlFilePath, out PSResourceInfo psGetInfo, out string errorMsg))
            {
                return psGetInfo;
            }

            _cmdletPassedIn.WriteVerbose(
                $"Reading metadata for package {pkgName} failed with error: {errorMsg}");
            return null;
        }

        #endregion

        #region Private methods

        private static string GetResourceNameFromPath(string path)
        {
            // Resource paths may end in a directory or script file name.
            // Directory name is the same as the resource name.
            // Script file name is the resource name without the file extension.
            // ./Modules/Microsoft.PowerShell.Test-Module     : Microsoft.PowerShell.Test-Module
            // ./Scripts/Microsoft.PowerShell.Test-Script.ps1 : Microsoft.PowerShell.Test-Script
            var resourceName = Path.GetFileName(path);
            return Path.GetExtension(resourceName).Equals(".ps1", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(resourceName) : resourceName;
        }

        #endregion
    }
}
