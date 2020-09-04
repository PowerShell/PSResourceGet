// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using static System.Environment;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.PowerShell.PowerShellGet.RepositorySettings;
using MoreLinq.Extensions;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// Find helper class
    /// </summary>
    class GetHelper : PSCmdlet
    {
        private CancellationToken cancellationToken;
        private readonly bool update;
        private readonly PSCmdlet cmdletPassedIn;


        public static readonly string OsPlatform = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

        private string programFilesPath;
        private string myDocumentsPath;

        public GetHelper(CancellationToken cancellationToken, PSCmdlet cmdletPassedIn)
        {
            this.cancellationToken = cancellationToken;
            this.cmdletPassedIn = cmdletPassedIn;
        }

        public List<PSObject> ProcessGetParams(string[] name, string version, bool prerelease, string path)
        {
            var dirsToSearch = new List<string>();

            if (path != null)
            {
                cmdletPassedIn.WriteDebug(string.Format("Provided path is: '{0}'", path));
                dirsToSearch.AddRange(Directory.GetDirectories(path).ToList());
            }
            else
            {
                // PSModules path
                var psModulePath = Environment.GetEnvironmentVariable("PSModulePath");
                var modulePaths = psModulePath.Split(';');


#if NET472
                programFilesPath = System.IO.Path.Combine(Environment.GetFolderPath(SpecialFolder.ProgramFiles), "WindowsPowerShell");
                myDocumentsPath = System.IO.Path.Combine(Environment.GetFolderPath(SpecialFolder.MyDocuments), "WindowsPowerShell");

#else
                // If PS6+ on Windows
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    myDocumentsPath = System.IO.Path.Combine(Environment.GetFolderPath(SpecialFolder.MyDocuments), "PowerShell");
                    programFilesPath = System.IO.Path.Combine(Environment.GetFolderPath(SpecialFolder.ProgramFiles), "PowerShell");
                }
                else
                {
                    // Paths are the same for both Linux and MacOS
                    myDocumentsPath = System.IO.Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "powershell");
                    programFilesPath = System.IO.Path.Combine("/usr", "local", "share", "powershell");
                }
#endif
                cmdletPassedIn.WriteDebug(string.Format("Current user scope path: '{0}'", myDocumentsPath));
                cmdletPassedIn.WriteDebug(string.Format("All users scope path: '{0}'", programFilesPath));

                /*** Will search first in PSModulePath, then will search in default paths ***/

                foreach (var modulePath in modulePaths)
                {
                    dirsToSearch.AddRange(Directory.GetDirectories(modulePath).ToList());
                }


                var pfModulesPath = System.IO.Path.Combine(programFilesPath, "Modules");
                if (Directory.Exists(pfModulesPath))
                {
                    dirsToSearch.AddRange(Directory.GetDirectories(pfModulesPath).ToList());
                }

                var mdModulesPath = System.IO.Path.Combine(myDocumentsPath, "Modules");
                if (Directory.Exists(mdModulesPath))
                {
                    dirsToSearch.AddRange(Directory.GetDirectories(mdModulesPath).ToList());
                }


                var pfScriptsPath = System.IO.Path.Combine(programFilesPath, "Scripts", "InstalledScriptInfos");
                if (Directory.Exists(pfScriptsPath))
                {
                    dirsToSearch.AddRange(Directory.GetFiles(pfScriptsPath).ToList());
                }

                var mdScriptsPath = System.IO.Path.Combine(myDocumentsPath, "Scripts", "InstalledScriptInfos");
                if (Directory.Exists(mdScriptsPath))
                {
                    dirsToSearch.AddRange(Directory.GetFiles(mdScriptsPath).ToList());
                }


                // uniqueify 
                dirsToSearch = dirsToSearch.Distinct().ToList();
            }

            foreach (var dir in dirsToSearch)
            {
                cmdletPassedIn.WriteDebug(string.Format("All directories to search: '{0}'", dir));
            }

            // Or a list of the passed in names
            if (name != null && !name[0].Equals("*"))
            {
                var nameLowerCased = new List<string>();
                var scriptXMLnames = new List<string>();
                Array.ForEach(name, n => nameLowerCased.Add(n.ToLower()));
                Array.ForEach(name, n => scriptXMLnames.Add((n + "_InstalledScriptInfo.xml").ToLower()));

                dirsToSearch = dirsToSearch.FindAll(p => (nameLowerCased.Contains(new DirectoryInfo(p).Name.ToLower())
                    || scriptXMLnames.Contains((System.IO.Path.GetFileName(p)).ToLower())));

                cmdletPassedIn.WriteDebug(dirsToSearch.Any().ToString());
            }

            // try to parse into a specific NuGet version
            VersionRange versionRange = null;
            if (version != null)
            {
                NuGetVersion specificVersion;
                NuGetVersion.TryParse(version, out specificVersion);

                if (specificVersion != null)
                {
                    // exact version
                    versionRange = new VersionRange(specificVersion, true, specificVersion, true, null, null);
                    cmdletPassedIn.WriteDebug(string.Format("A specific version, '{0}', is specified", versionRange.ToString()));

                }
                else
                {
                    // check if version range
                    versionRange = VersionRange.Parse(version);
                    cmdletPassedIn.WriteDebug(string.Format("A version range, '{0}', is specified", versionRange.ToString()));

                }
            }

            List<string> installedPkgsToReturn = new List<string>();

            IEnumerable<string> returnPkgs = null;

            //2) use above list to check 
            // if the version specificed is a version range
            if (versionRange != null)
            {
                foreach (var pkgPath in dirsToSearch)
                {
                    cmdletPassedIn.WriteDebug(string.Format("Searching through package path: '{0}'", pkgPath));
                    var versionsDirs = Directory.GetDirectories(pkgPath);

                    foreach (var versionPath in versionsDirs)
                    {
                        cmdletPassedIn.WriteDebug(string.Format("Searching through package version path: '{0}'", versionPath));
                        NuGetVersion dirAsNugetVersion;
                        var dirInfo = new DirectoryInfo(versionPath);
                        NuGetVersion.TryParse(dirInfo.Name, out dirAsNugetVersion);
                        cmdletPassedIn.WriteDebug(string.Format("Directory parsed as NuGet version: '{0}'", dirAsNugetVersion));

                        if (versionRange.Satisfies(dirAsNugetVersion))
                        {
                            // just search scripts paths
                            if (pkgPath.ToLower().Contains("Scripts"))
                            {
                                if (File.Exists(pkgPath))
                                {
                                    installedPkgsToReturn.Add(pkgPath);
                                }
                            }
                            else
                            {
                                // modules paths
                                versionsDirs = Directory.GetDirectories(pkgPath);
                                cmdletPassedIn.WriteDebug(string.Format("Getting sub directories from : '{0}'", pkgPath));

                                // Check if the pkg path actually has version sub directories.
                                if (versionsDirs.Length != 0)
                                {
                                    Array.Sort(versionsDirs, StringComparer.OrdinalIgnoreCase);
                                    Array.Reverse(versionsDirs);

                                    var pkgXmlFilePath = System.IO.Path.Combine(versionsDirs.First(), "PSGetModuleInfo.xml");

                                    // TODO:  check if this xml file exists, if it doesn't check if it exists in a previous version
                                    cmdletPassedIn.WriteDebug(string.Format("Found module XML: '{0}'", pkgXmlFilePath));

                                    installedPkgsToReturn.Add(pkgXmlFilePath);
                                }
                            }

                            installedPkgsToReturn.Add(versionPath);
                        }
                    }
                }
            }
            else
            {
                cmdletPassedIn.WriteDebug("No version provided-- check each path for the requested package");
                // if no version is specified, just get the latest version
                foreach (var pkgPath in dirsToSearch)
                {
                    cmdletPassedIn.WriteDebug(string.Format("Searching through package path: '{0}'", pkgPath));

                    // just search scripts paths
                    if (pkgPath.ToLower().Contains("scripts"))
                    {
                        if (File.Exists(pkgPath))   //check to make sure properly formatted
                        {
                            installedPkgsToReturn.Add(pkgPath);
                        }
                    }
                    else
                    {
                        // modules paths
                        string[] versionsDirs = new string[0];

                        versionsDirs = Directory.GetDirectories(pkgPath);

                        // Check if the pkg path actually has version sub directories.
                        if (versionsDirs.Length != 0)
                        {
                            Array.Sort(versionsDirs, StringComparer.OrdinalIgnoreCase);
                            Array.Reverse(versionsDirs);

                            var pkgXmlFilePath = System.IO.Path.Combine(versionsDirs.First(), "PSGetModuleInfo.xml");

                            // TODO:  check if this xml file exists, if it doesn't check if it exists in a previous version
                            cmdletPassedIn.WriteDebug(string.Format("Found package XML: '{0}'", pkgXmlFilePath));
                            installedPkgsToReturn.Add(pkgXmlFilePath);
                        }
                    }
                }
            }


            // Flatten returned pkgs before displaying output returnedPkgsFound.Flatten().ToList()[0]
            var flattenedPkgs = installedPkgsToReturn.Flatten();



            List<PSObject> foundInstalledPkgs = new List<PSObject>();

            foreach (string xmlFilePath in flattenedPkgs)
            {
                cmdletPassedIn.WriteDebug(string.Format("Reading package metadata from: '{0}'", xmlFilePath));

                // Open xml and read metadata from it     
                if (File.Exists(xmlFilePath))
                {
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> nameInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> versionInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> typeInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> descriptionInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> authorInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> companyNameInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> copyrightInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> publishedDateInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> installedDateInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> updatedDateInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> licenseUriInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> projectUriInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> iconUriInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> tagsInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> includesInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> powerShellGetFormatVersionInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> releaseNotesInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> dependenciesInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> repositorySourceLocationInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> repositoryInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> additionalMetadataInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> installedLocationInfo;


                    //var isPrelease = false;
                    using (StreamReader sr = new StreamReader(xmlFilePath))
                    {

                        string text = sr.ReadToEnd();
                        var deserializedObj = (PSObject)PSSerializer.Deserialize(text);

                        nameInfo = deserializedObj.Properties.Match("Name");
                        versionInfo = deserializedObj.Properties.Match("Version");
                        typeInfo = deserializedObj.Properties.Match("Type");
                        descriptionInfo = deserializedObj.Properties.Match("Description");
                        authorInfo = deserializedObj.Properties.Match("Author");
                        companyNameInfo = deserializedObj.Properties.Match("CompanyName");
                        copyrightInfo = deserializedObj.Properties.Match("Copyright");
                        publishedDateInfo = deserializedObj.Properties.Match("PublishedDate");
                        installedDateInfo = deserializedObj.Properties.Match("InstalledDate");
                        updatedDateInfo = deserializedObj.Properties.Match("UpdatedDate");
                        licenseUriInfo = deserializedObj.Properties.Match("LicenseUri");
                        projectUriInfo = deserializedObj.Properties.Match("ProjectUri");
                        iconUriInfo = deserializedObj.Properties.Match("IconUri");
                        tagsInfo = deserializedObj.Properties.Match("Tags");
                        includesInfo = deserializedObj.Properties.Match("Includes");
                        powerShellGetFormatVersionInfo = deserializedObj.Properties.Match("PowerShellGetFormatVersion");
                        releaseNotesInfo = deserializedObj.Properties.Match("ReleaseNotes");
                        dependenciesInfo = deserializedObj.Properties.Match("Dependencies");
                        repositorySourceLocationInfo = deserializedObj.Properties.Match("RepositorySourceLocation");
                        repositoryInfo = deserializedObj.Properties.Match("Repository");
                        additionalMetadataInfo = deserializedObj.Properties.Match("AdditionalMetadata");
                        installedLocationInfo = deserializedObj.Properties.Match("InstalledLocation");


                        /* // testing adding prerelease parameter
                        additionalMetadataInfo = deserializedObj.Properties.Match("AdditionalMetadata");
                        if (additionalMetadataInfo.Any())
                        {
                            isPrelease = additionalMetadataInfo.FirstOrDefault().Value.ToString().Contains("IsPrerelease=true");
                            if ((isPrelease == true) && _prerelease) // find a stable version of the pkg {}
                        }
                        */


                    };

                    // if -Prerelease is not passed in as a parameter, don't allow prerelease pkgs to be returned,
                    // we still want all pkgs to be returned if -Prerelease is passed in as a param
                    //if ((_prerelease == false && isPrelease == false) || _prerelease == true)
                    //{

                    //var foundPkgs = List<PSObject>();
                    
                    PSObject pkgAsPSObject = new PSObject();
                    try
                    {
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Name", nameInfo.Any()? nameInfo.FirstOrDefault().Value : string.Empty)); 
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Version", versionInfo.Any()? versionInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Type", typeInfo.Any()? typeInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Description", descriptionInfo.Any()? descriptionInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Author", authorInfo.Any()? authorInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("CompanyName", companyNameInfo.Any()? companyNameInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Copyright", copyrightInfo.Any()? copyrightInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("PublishedDate", publishedDateInfo.Any()? publishedDateInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("InstalledDate", installedDateInfo.Any()? installedDateInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("UpdatedDate", updatedDateInfo.Any()? updatedDateInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("LicenseUri", licenseUriInfo.Any()? licenseUriInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("ProjectUri", projectUriInfo.Any()? projectUriInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("IconUri", iconUriInfo.Any()? iconUriInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Tags", tagsInfo.Any()? tagsInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Includes", includesInfo.Any()? includesInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("PowerShellGetFormatVersion", powerShellGetFormatVersionInfo.Any()? powerShellGetFormatVersionInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("ReleaseNotes", releaseNotesInfo.Any()? releaseNotesInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Dependencies", dependenciesInfo.Any()? dependenciesInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("RepositorySourceLocation", repositorySourceLocationInfo.Any()? repositorySourceLocationInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Repository", repositoryInfo.Any()? repositoryInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("AdditionalMetadata", additionalMetadataInfo.Any()? additionalMetadataInfo.FirstOrDefault().Value : string.Empty));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("InstalledLocation", installedLocationInfo.Any()? installedLocationInfo.FirstOrDefault().Value : string.Empty));


                        // verify that this works, then add the object to a list and return it
                        //WriteObject(pkgAsPSObject);
                        foundInstalledPkgs.Add(pkgAsPSObject);

                    }
                    catch { }
                    
                }
            }

            // return here 
            return foundInstalledPkgs;
        }
    }
}
