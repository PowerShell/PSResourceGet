// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using MoreLinq;
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

        public IEnumerable<PSResourceInfo> ProcessGetParams(string[] name, VersionRange versionRange, bool returnAllVersions, List<string>pathsToSearch)
        {
            List<string> filteredPathsToSearch = FilterPkgsByName(name, pathsToSearch);
            filteredPathsToSearch = GetResourceMetadataFiles(versionRange, returnAllVersions, filteredPathsToSearch);

            foreach (PSResourceInfo pkgObject in OutputPackageObject(filteredPathsToSearch, versionRange))
            {
                yield return pkgObject;
            }
        }
       
       
        
        // Filter packages by user provided name
        public List<string> FilterPkgsByName(string[] name, List<string> dirsToSearch)
        {
            List<string> wildCardDirsToSearch = new List<string>();

            if (name != null && !name[0].Equals("*"))
            {
                foreach (string n in name)
                {
                    if (n.Contains("*"))
                    {
                        WildcardPattern nameWildCardPattern = new WildcardPattern(n, WildcardOptions.IgnoreCase);

                        // modules
                        wildCardDirsToSearch.AddRange(dirsToSearch.Where(p => nameWildCardPattern.IsMatch((new DirectoryInfo(p).Name))));

                        // scripts
                        wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(p => nameWildCardPattern.IsMatch(System.IO.Path.GetFileNameWithoutExtension(p))));
                    }
                    else
                    {
                        // modules
                        wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(p => new DirectoryInfo(p).Name.Equals(n, StringComparison.OrdinalIgnoreCase)));
                        
                        // script paths will look something like this:  InstalledScriptInfos/<name of script>.xml
                        wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(p => System.IO.Path.GetFileNameWithoutExtension(p).Equals(n, StringComparison.OrdinalIgnoreCase)));
                    }
                }
            }
            else {
                wildCardDirsToSearch = dirsToSearch;
            }

            cmdletPassedIn.WriteDebug(wildCardDirsToSearch.Any().ToString());

            return wildCardDirsToSearch;
        }

        // Filter by user provided version
        public List<string> GetResourceMetadataFiles(VersionRange versionRange, bool returnAllVersions, List<string> dirsToSearch)
        {
            List<string> installedPkgsToReturn = new List<string>();  // these are the xmls

            if (versionRange == null)
            {
                cmdletPassedIn.WriteDebug("No version provided-- check each path for the requested package");
                // if no version is specified, just get the latest version
                foreach (string pkgPath in dirsToSearch)
                {
                    cmdletPassedIn.WriteDebug(string.Format("Searching through package path: '{0}'", pkgPath));

                    // if this is a module directory
                    if (Directory.Exists(pkgPath))
                    {
                        // search modules paths
                        string[] versionsDirs = new string[0];
                        versionsDirs = Directory.GetDirectories(pkgPath);

                        // Check if the pkg path actually has version sub directories.
                        if (versionsDirs.Length != 0)
                        {
                            Array.Sort(versionsDirs, StringComparer.OrdinalIgnoreCase);
                            Array.Reverse(versionsDirs);

                            string pkgXmlFilePath = System.IO.Path.Combine(versionsDirs.First(), "PSGetModuleInfo.xml");

                            cmdletPassedIn.WriteDebug(string.Format("Found package XML: '{0}'", pkgXmlFilePath));
                            installedPkgsToReturn.Add(pkgXmlFilePath);
                        }
                    }
                    else if (File.Exists(pkgPath))
                    {
                        // if it's a script
                        // find the xml path
                        var parentDir = new DirectoryInfo(pkgPath).Parent;
                        var scriptName = System.IO.Path.GetFileNameWithoutExtension(pkgPath);
                        string scriptXmlFilePath = System.IO.Path.Combine(parentDir.ToString(), "InstalledScriptInfos", scriptName + "_InstalledScriptInfo.xml");

                        installedPkgsToReturn.Add(scriptXmlFilePath);
                    }
                }
            }
            else {
                // check if the version specified is within a version range
                foreach (string pkgPath in dirsToSearch)
                {
                    // check if this path is a script or a module
                    if (Directory.Exists(pkgPath))
                    {
                        // this is only handles modules, not for scripts 
                        // we can pull the module version from the module path, but script versions are found within the xml
                        // therefore scripts will be handled later on when we parse the XML
                        cmdletPassedIn.WriteDebug(string.Format("Searching through package path: '{0}'", pkgPath));
                        string[] versionsDirs = Directory.GetDirectories(pkgPath);

                        foreach (string versionPath in versionsDirs)
                        {
                            cmdletPassedIn.WriteDebug(string.Format("Searching through package version path: '{0}'", versionPath));
                            DirectoryInfo dirInfo = new DirectoryInfo(versionPath);
                            NuGetVersion.TryParse(dirInfo.Name, out NuGetVersion dirAsNugetVersion);
                            cmdletPassedIn.WriteDebug(string.Format("Directory parsed as NuGet version: '{0}'", dirAsNugetVersion));

                            if (dirAsNugetVersion != null && (returnAllVersions || versionRange.Satisfies(dirAsNugetVersion)))
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
                    TryRead(xmlFilePath, out PSResourceInfo psGetInfo, out string errorMsg);

                    yield return psGetInfo;
                }
            }
        }
    }
}
