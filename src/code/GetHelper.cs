// Copyright (c) Microsoft Corporation. All rights reserved.
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
        private CancellationToken cancellationToken;
        private readonly PSCmdlet cmdletPassedIn;
        private readonly string cmdletName;

        public GetHelper(CancellationToken cancellationToken, PSCmdlet cmdletPassedIn, string cmdletName)
        {
            this.cancellationToken = cancellationToken;
            this.cmdletPassedIn = cmdletPassedIn;
            this.cmdletName = cmdletName;
        }

        public IEnumerable<PSResourceInfo> ProcessGetParams(string[] name, VersionRange versionRange, List<string>pathsToSearch)
        {
            List<string> filteredPathsToSearch = FilterPkgPathsByName(name, pathsToSearch);

            foreach (PSResourceInfo pkgObject in GetResourceMetadataFiles(versionRange, filteredPathsToSearch))
            {
                yield return pkgObject;
            }
        }

        // Filter packages by user provided name
        public List<string> FilterPkgPathsByName(string[] names, List<string> dirsToSearch)
        {
            List<string> wildCardDirsToSearch = new List<string>();

            if (names == null)
            {
                cmdletPassedIn.WriteVerbose("No names were provided.");
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
             cmdletPassedIn.WriteDebug(wildCardDirsToSearch.Any().ToString());

            return wildCardDirsToSearch;
        }

        // Filter by user provided version
        public IEnumerable<PSResourceInfo> GetResourceMetadataFiles(VersionRange versionRange, List<string> dirsToSearch)
        {
            // This will contain the metadata xmls
            List<string> installedPkgsToReturn = new List<string>();
            
            if (versionRange == null && cmdletName.Equals("Get-InstalledPSResource"))
            {
                System.Diagnostics.Debug.Assert(false, "FilterPkgPathsByName: version argument should not be null.");
                cmdletPassedIn.WriteVerbose("FilterPkgPathsByName: version argument should not be null.");

                yield break;
            }

            // if no version is specified, just get the latest version
            foreach (string pkgPath in dirsToSearch)
            {
                cmdletPassedIn.WriteDebug(string.Format("Searching through package path: '{0}'", pkgPath));

                // if this is a module directory
                if (Directory.Exists(pkgPath))
                {
                    // search modules paths
                    // ./Modules/Test-Module/1.0.0
                    // ./Modules/Test-Module/2.0.0
                    cmdletPassedIn.WriteDebug(string.Format("Searching through package path: '{0}'", pkgPath));

                    string[] versionsDirs = new string[]{};
                    try
                    {
                        versionsDirs = Directory.GetDirectories(pkgPath);
                    }
                    catch (Exception e){
                        cmdletPassedIn.WriteVerbose(string.Format("Error retreiving directories from path '{0}': '{1}'", pkgPath, e.Message));

                        // skip to next iteration of the loop
                        continue;
                    }

                    foreach (string versionPath in versionsDirs)
                    {
                        cmdletPassedIn.WriteDebug(string.Format("Searching through package version path: '{0}'", versionPath));
                        DirectoryInfo dirInfo = new DirectoryInfo(versionPath);

                        // if the version is not valid, we'll just skip it and output a debug message
                        if (!NuGetVersion.TryParse(dirInfo.Name, out NuGetVersion dirAsNugetVersion))
                        {
                            cmdletPassedIn.WriteVerbose(string.Format("Leaf directory in path '{0}' cannot be parsed into a version.", versionPath));

                            // skip to next iteration of the loop
                            continue;
                        }
                        cmdletPassedIn.WriteVerbose(string.Format("Directory parsed as NuGet version: '{0}'", dirAsNugetVersion));
                        
                        if (versionRange.Satisfies(dirAsNugetVersion))
                        {
                            // This will be one version or a version range.
                            string pkgXmlFilePath = System.IO.Path.Combine(versionPath, "PSGetModuleInfo.xml");

                            // yield results then continue with this iteration of the loop
                            yield return OutputPackageObject(dirInfo.Parent.ToString(), pkgXmlFilePath);
                        }
                    }
                }
                else if (File.Exists(pkgPath))
                {
                    // if it's a script
                    // find the xml path
                    // ./Scripts/Test-Script.ps1
                    // ./Scripts/InstalledScriptsInfos/Test-Script_InstalledScriptInfo.xml
                    var parentDir = new DirectoryInfo(pkgPath).Parent;
                    var scriptName = System.IO.Path.GetFileNameWithoutExtension(pkgPath);
                    string scriptXmlFilePath = System.IO.Path.Combine(parentDir.ToString(), "InstalledScriptInfos", scriptName + "_InstalledScriptInfo.xml");

                    cmdletPassedIn.WriteDebug(string.Format("Adding package XML: '{0}'", scriptXmlFilePath));

                    if (versionRange == VersionRange.All)
                    {
                        PSResourceInfo scriptInfoAllVersions = OutputPackageObject(scriptName, scriptXmlFilePath);
                        if (scriptInfoAllVersions != null)
                        {
                            // yield results then continue with this iteration of the loop
                            yield return scriptInfoAllVersions;
                        }

                        // We are now done with the current iteration of the for loop because
                        // only one script version can be installed in a particular script path at a time.
                        // if looking for all versions and one was found, then we have found all possible versions at that ./Scripts path
                        // and do not need to parse and check for the version number in the metadata file.
                    }
                    else
                    {
                        // check to make sure it's within the version range.
                        // script versions will be parsed from the script xml file
                        PSResourceInfo scriptInfo = OutputPackageObject(scriptName, scriptXmlFilePath);
                        if (scriptInfo == null)
                        {
                            // if script was not found skip to the next iteration of the loop
                            continue;
                        }

                        if (!NuGetVersion.TryParse(scriptInfo.Version.ToString(), out NuGetVersion scriptVersion))
                        {
                            cmdletPassedIn.WriteVerbose(string.Format("Version '{0}' could not be properly parsed from the script metadata file '{1}'", scriptInfo.Version.ToString(), scriptXmlFilePath));
                        }
                        else if (versionRange.Satisfies(scriptVersion))
                        {
                            yield return scriptInfo;
                        }
                    }
                }
            }
        }
        
        // Create package object for each found resource directory
        public PSResourceInfo OutputPackageObject(string pkgName, string xmlFilePath)
        {
            // Read metadata from XML and parse into PSResourceInfo object
            cmdletPassedIn.WriteVerbose(string.Format("Reading package metadata from: '{0}'", xmlFilePath));
            if (!TryRead(xmlFilePath, out PSResourceInfo psGetInfo, out string errorMsg))
            {
                cmdletPassedIn.WriteVerbose(errorMsg);
            }
            else
            {
                cmdletPassedIn.WriteDebug(string.Format("Found module XML: '{0}'", xmlFilePath));
                return psGetInfo;
            }

            return null;
        }
    }
}
