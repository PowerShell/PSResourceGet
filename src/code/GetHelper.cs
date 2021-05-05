// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections;
using System.Collections.Generic;
using static System.Environment;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Threading;
using MoreLinq;
using MoreLinq.Extensions;
using static Microsoft.PowerShell.PowerShellGet.UtilClasses.PSResourceInfo;
using NuGet.Versioning;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// Get helper class provides the core functionality for Get-InstalledPSResource.
    /// </summary>
    internal class GetHelper
    {
        private CancellationToken cancellationToken;
        private readonly PSCmdlet cmdletPassedIn;
        private string programFilesPath;
        private string myDocumentsPath;
        public static readonly string OsPlatform = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

        public GetHelper(CancellationToken cancellationToken, PSCmdlet cmdletPassedIn)
        {
            this.cancellationToken = cancellationToken;
            this.cmdletPassedIn = cmdletPassedIn;
        }

        public IEnumerable<PSResourceInfo> ProcessGetParams(string[] name, string version, string path)
        {
            List<string> dirsToSearch = GetPackageDirectories(path);

            dirsToSearch = FilterPkgsByName(name, dirsToSearch);
            dirsToSearch = GetResourceMetadataFiles(version, dirsToSearch, out VersionRange versionRange);

            foreach (PSResourceInfo pkgObject in OutputPackageObject(dirsToSearch, versionRange))
            {
                yield return pkgObject;
            }
        }
       
        // Gather resource directories to search through
        public List<string> GetPackageDirectories(string path)
        {
            List<string> dirsToSearch = new List<string>();

            if (path != null)
            {
                cmdletPassedIn.WriteDebug(string.Format("Provided path is: '{0}'", path));
                dirsToSearch.AddRange(Directory.GetDirectories(path).ToList());
            }
            else
            {
                string psModulePath = Environment.GetEnvironmentVariable("PSModulePath");
                string[] modulePaths = psModulePath.Split(';');

                var PSVersion6 = new Version(6, 0);
                var isCorePS = cmdletPassedIn.Host.Version >= PSVersion6;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // If PowerShell 6+
                    if (isCorePS)
                    {
                        myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.MyDocuments), "PowerShell");
                        programFilesPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.ProgramFiles), "PowerShell");
                    }
                    else
                    {
                        myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.MyDocuments), "WindowsPowerShell");
                        programFilesPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.ProgramFiles), "WindowsPowerShell");
                    }
                }
                else
                {
                    // Paths are the same for both Linux and MacOS
                    myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "Powershell");
                    programFilesPath = Path.Combine("usr", "local", "share", "Powershell");
                }

                cmdletPassedIn.WriteVerbose(string.Format("Current user scope path: '{0}'", myDocumentsPath));
                cmdletPassedIn.WriteVerbose(string.Format("All users scope path: '{0}'", programFilesPath));

                // will search first in PSModulePath, then will search in default paths
                foreach (string modulePath in modulePaths)
                {
                    cmdletPassedIn.WriteDebug(string.Format("Retrieving directories in the '{0}' module path", modulePath));
                    try
                    {
                        dirsToSearch.AddRange(Directory.GetDirectories(modulePath).ToList());
                    }
                    catch (Exception e)
                    {
                        cmdletPassedIn.WriteVerbose(string.Format("Error retrieving directories from '{0}': '{1)'", modulePath, e.Message));
                    }
                }

                string pfModulesPath = System.IO.Path.Combine(programFilesPath, "Modules");
                if (Directory.Exists(pfModulesPath))
                {
                    dirsToSearch.AddRange(Directory.GetDirectories(pfModulesPath).ToList());
                }
                string mdModulesPath = System.IO.Path.Combine(myDocumentsPath, "Modules");
                if (Directory.Exists(mdModulesPath))
                {
                    dirsToSearch.AddRange(Directory.GetDirectories(mdModulesPath).ToList());
                }

                string pfScriptsPath = System.IO.Path.Combine(programFilesPath, "Scripts");
                if (Directory.Exists(pfScriptsPath))
                {
                    dirsToSearch.AddRange(Directory.GetFiles(pfScriptsPath).ToList());
                }
                string mdScriptsPath = System.IO.Path.Combine(myDocumentsPath, "Scripts");
                if (Directory.Exists(mdScriptsPath))
                {
                    dirsToSearch.AddRange(Directory.GetFiles(mdScriptsPath).ToList());
                }

                string pfInstalledScriptsPath = System.IO.Path.Combine(programFilesPath, "Scripts", "InstalledScriptInfos");
                if (Directory.Exists(pfScriptsPath))
                {
                    dirsToSearch.AddRange(Directory.GetFiles(pfScriptsPath).ToList());
                }
                string mdInstalledScriptsPath = System.IO.Path.Combine(myDocumentsPath, "Scripts", "InstalledScriptInfos");
                if (Directory.Exists(mdScriptsPath))
                {
                    dirsToSearch.AddRange(Directory.GetFiles(mdScriptsPath).ToList());
                }

                dirsToSearch = dirsToSearch.Distinct().ToList();
            }

            dirsToSearch.ForEach(dir => cmdletPassedIn.WriteDebug(string.Format("All directories to search: '{0}'", dir)));

            return dirsToSearch;
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
        public List<string> GetResourceMetadataFiles(string version, List<string> dirsToSearch, out VersionRange versionRange)
        {
            List<string> installedPkgsToReturn = new List<string>();  // these are the xmls
            versionRange = null;

            if (version == null)
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
            else if (Utils.TryParseVersionOrVersionRange(version, out versionRange, out bool allVersions))
            {
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
                }
            }
            else {
                versionRange = null;
                // invalid version
                var exMessage = String.Format("Argument for -Version parameter is not in the proper format.");
                var ex = new ArgumentException(exMessage);
                var IncorrectVersionFormat = new ErrorRecord(ex, "IncorrectVersionFormat", ErrorCategory.InvalidArgument, null);
                cmdletPassedIn.ThrowTerminatingError(IncorrectVersionFormat);
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
