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

        public GetHelper(CancellationToken cancellationToken, PSCmdlet cmdletPassedIn)
        {
            this.cancellationToken = cancellationToken;
            this.cmdletPassedIn = cmdletPassedIn;
        }

        public IEnumerable<PSResourceInfo> ProcessGetParams(string[] name, VersionRange versionRange, List<string>pathsToSearch)
        {
            List<string> filteredPathsToSearch = FilterPkgsByName(name, pathsToSearch);
            filteredPathsToSearch = GetResourceMetadataFiles(versionRange, filteredPathsToSearch);

            foreach (PSResourceInfo pkgObject in OutputPackageObject(filteredPathsToSearch, versionRange))
            {
                yield return pkgObject;
            }
        }

        // Filter packages by user provided name
        public List<string> FilterPkgsByName(string[] names, List<string> dirsToSearch)
        {
            List<string> wildCardDirsToSearch = new List<string>();

            if (names == null)
            {
                names = new string[] { "*" };
            }

            foreach (string n in names)
            {
                WildcardPattern nameWildCardPattern = new WildcardPattern(n, WildcardOptions.IgnoreCase);
                
                // ./Modules/Test-Module
                // ./Scripts/Test-Script.ps1
                // Where vs FindAll
                wildCardDirsToSearch.AddRange(dirsToSearch.Where(
                    p => nameWildCardPattern.IsMatch(
                        System.IO.Path.GetFileNameWithoutExtension((new DirectoryInfo(p).Name)))));
            }
             cmdletPassedIn.WriteDebug(wildCardDirsToSearch.Any().ToString());

            return wildCardDirsToSearch;
        }

        // Filter by user provided version
        public List<string> GetResourceMetadataFiles(VersionRange versionRange, List<string> dirsToSearch)
        {
            List<string> installedPkgsToReturn = new List<string>();  // these are the xmls

            if (versionRange == null)
            {
                // if version is not specified, return all versions
                versionRange = VersionRange.All;
            }

            // if no version is specified, just get the latest version
            foreach (string pkgPath in dirsToSearch)
            {
                cmdletPassedIn.WriteDebug(string.Format("Searching through package path: '{0}'", pkgPath));

                // if this is a module directory
                if (Directory.Exists(pkgPath))
                {
                    // search modules paths
                    // ./Modules/Test-Module
                    // ./Modules/Test-Module/1.0.0
                    // ./Modules/Test-Module/2.0.0
                    cmdletPassedIn.WriteDebug(string.Format("Searching through package path: '{0}'", pkgPath));
                    string[] versionsDirs = Directory.GetDirectories(pkgPath);  // ERROR HANDLING

                    foreach (string versionPath in versionsDirs)
                    {
                        cmdletPassedIn.WriteDebug(string.Format("Searching through package version path: '{0}'", versionPath));
                        DirectoryInfo dirInfo = new DirectoryInfo(versionPath);
                        NuGetVersion.TryParse(dirInfo.Name, out NuGetVersion dirAsNugetVersion);
                        cmdletPassedIn.WriteDebug(string.Format("Directory parsed as NuGet version: '{0}'", dirAsNugetVersion));

                        if (dirAsNugetVersion != null && versionRange.Satisfies(dirAsNugetVersion))
                        {
                            // This will be one version or a version range.
                            string pkgXmlFilePath = System.IO.Path.Combine(versionPath, "PSGetModuleInfo.xml");
                            if (File.Exists(pkgXmlFilePath))
                            {
                                cmdletPassedIn.WriteDebug(string.Format("Found module XML: '{0}'", pkgXmlFilePath));
                                installedPkgsToReturn.Add(pkgXmlFilePath);
                            }
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
                        installedPkgsToReturn.Add(scriptXmlFilePath);
                    }
                    else
                    {
                        // check to make sure it's within the version range.
                        // script versions will be parsed from the script xml file
                        ReadOnlyPSMemberInfoCollection<PSPropertyInfo> versionInfo;
                        using (StreamReader sr = new StreamReader(scriptXmlFilePath))
                        {
                            string text = sr.ReadToEnd();
                            var deserializedObj = (PSObject)PSSerializer.Deserialize(text);

                            versionInfo = deserializedObj.Properties.Match("Version");
                        };

                        if (NuGetVersion.TryParse(versionInfo.ToString(), out NuGetVersion scriptVersion) &&
                            versionRange.Satisfies(scriptVersion))
                        {
                            // if version satisfies the condition, add it to the list
                            installedPkgsToReturn.Add(scriptXmlFilePath);
                        }
                    }
                }
            }

            return installedPkgsToReturn;
        }
        
        // Create package object for each found resource directory
        public IEnumerable<PSResourceInfo> OutputPackageObject(List<string> installedPkgsToReturn, VersionRange versionRange)
        {
            IEnumerable<object> flattenedPkgs = FlattenExtension.Flatten(installedPkgsToReturn);
            List<PSResourceInfo> foundInstalledPkgs = new List<PSResourceInfo>();

            // Read metadata from XML and parse into PSResourceInfo object
            foreach (string xmlFilePath in flattenedPkgs)
            {
                cmdletPassedIn.WriteDebug(string.Format("Reading package metadata from: '{0}'", xmlFilePath));
                
                if (File.Exists(xmlFilePath))
                {
                    if (!TryRead(xmlFilePath, out PSResourceInfo psGetInfo, out string errorMsg))
                    {
                        cmdletPassedIn.WriteVerbose(errorMsg);
                        yield break;
                    }
                    yield return psGetInfo;
                }
            }
        }
    }
}
