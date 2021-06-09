﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
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
        private CancellationToken _cancellationToken;
        private readonly PSCmdlet _cmdletPassedIn;
        private Dictionary<string, PSResourceInfo> _scriptDictionary;

        public GetHelper(CancellationToken cancellationToken, PSCmdlet cmdletPassedIn)
        {
            _cancellationToken = cancellationToken;
            _cmdletPassedIn = cmdletPassedIn;
            _scriptDictionary = new Dictionary<string, PSResourceInfo>();
        }

        public IEnumerable<PSResourceInfo> ProcessGetParams(string[] name, VersionRange versionRange, List<string> pathsToSearch)
        {
            List<string> filteredPathsToSearch = FilterPkgPathsByName(name, pathsToSearch);

            foreach (string pkgPath in FilterPkgPathsByVersion(versionRange, filteredPathsToSearch))
            {
                yield return OutputPackageObject(pkgPath, _scriptDictionary);
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

                // ./Modules/Test-Module
                // ./Scripts/Test-Script.ps1
                wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(
                    p => nameWildCardPattern.IsMatch(
                        System.IO.Path.GetFileNameWithoutExtension((new DirectoryInfo(p).Name)))));
            }
            _cmdletPassedIn.WriteDebug(wildCardDirsToSearch.Any().ToString());

            return wildCardDirsToSearch;
        }

        // Filter by user provided version
        public IEnumerable<String> FilterPkgPathsByVersion(VersionRange versionRange, List<string> dirsToSearch)
        {
            // if no version is specified, just get the latest version
            foreach (string pkgPath in dirsToSearch)
            {
                _cmdletPassedIn.WriteDebug(string.Format("Searching through package path: '{0}'", pkgPath));

                // if this is a module directory
                if (Directory.Exists(pkgPath))
                {
                    // search modules paths
                    // ./Modules/Test-Module/1.0.0
                    // ./Modules/Test-Module/2.0.0
                    _cmdletPassedIn.WriteDebug(string.Format("Searching through package path: '{0}'", pkgPath));

                    string[] versionsDirs = new string[] { };
                    try
                    {
                        versionsDirs = Directory.GetDirectories(pkgPath);
                    }
                    catch (Exception e){
                        _cmdletPassedIn.WriteVerbose(string.Format("Error retreiving directories from path '{0}': '{1}'", pkgPath, e.Message));

                        // skip to next iteration of the loop
                        continue;
                    }

                    // versionRange should not be null if the cmdlet is Get-InstalledPSResource
                    if (versionRange == null)
                    {
                        // if no version is specified, just delete the latest version
                        Array.Sort(versionsDirs);

                        yield return (versionsDirs[versionsDirs.Length - 1]);
                        continue;
                    }

                    foreach (string versionPath in versionsDirs)
                    {
                        _cmdletPassedIn.WriteDebug(string.Format("Searching through package version path: '{0}'", versionPath));
                        DirectoryInfo dirInfo = new DirectoryInfo(versionPath);

                        // if the version is not valid, we'll just skip it and output a debug message
                        if (!NuGetVersion.TryParse(dirInfo.Name, out NuGetVersion dirAsNugetVersion))
                        {
                            _cmdletPassedIn.WriteVerbose(string.Format("Leaf directory in path '{0}' cannot be parsed into a version.", versionPath));

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
                    if (versionRange == VersionRange.All)
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
                        if (scriptInfo == null)
                        {
                            // if script was not found skip to the next iteration of the loop
                            continue;
                        }

                        if (!NuGetVersion.TryParse(scriptInfo.Version.ToString(), out NuGetVersion scriptVersion))
                        {
                            _cmdletPassedIn.WriteVerbose(string.Format("Version '{0}' could not be properly parsed from the script metadata file from the script installed at '{1}'", scriptInfo.Version.ToString(), scriptInfo.InstalledLocation));
                        }
                        else if (versionRange.Satisfies(scriptVersion))
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
            string xmlFilePath = string.Empty;
            var parentDir = new DirectoryInfo(pkgPath).Parent;

            // find package name
            string pkgName = Utils.GetInstalledPackageName(pkgPath);
            _cmdletPassedIn.WriteDebug(string.Format("OutputPackageObject:: package name is {0}.", pkgName));
            // Find xml file
            // if the package path is in the deserialized script dictionary, just return that 
            if (scriptDictionary.ContainsKey(pkgPath))
            {
                return scriptDictionary[pkgPath];
            }
            // else if the pkgName from pkgpath is a script, find the xml file
            else if (File.Exists(pkgPath))
            {
                xmlFilePath = System.IO.Path.Combine(parentDir.ToString(), "InstalledScriptInfos", pkgName + "_InstalledScriptInfo.xml");
            }
            // else we assume it's a module, and look for the xml path that way 
            else
            {
                xmlFilePath = System.IO.Path.Combine(pkgPath, "PSGetModuleInfo.xml");
            }
            
            // Read metadata from XML and parse into PSResourceInfo object
            _cmdletPassedIn.WriteVerbose(string.Format("Reading package metadata from: '{0}'", xmlFilePath));
            if (!TryRead(xmlFilePath, out PSResourceInfo psGetInfo, out string errorMsg))
            {
                _cmdletPassedIn.WriteVerbose(errorMsg);
            }
            else
            {
                _cmdletPassedIn.WriteDebug(string.Format("Found module XML: '{0}'", xmlFilePath));
                return psGetInfo;
            }

            return null;
        }
    }
}
