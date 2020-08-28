﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using static System.Environment;
using MoreLinq;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// It retrieves a resource that was installEd with Install-PSResource
    /// Returns a single resource or multiple resource.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "PSResource", SupportsShouldProcess = true,
        HelpUri = "<add>", RemotingCapability = RemotingCapability.None)]
    public sealed
    class GetPSResource : PSCmdlet
    {
        /// <summary>
        /// Specifies the desired name for the resource to look for.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string[] Name
        {
            get
            { return _name; }

            set
            { _name = value; }
        }
        private string[] _name;


        /// <summary>
        /// Specifies the version of the resource to include to look for. 
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty()]
        public string Version
        {
            get
            { return _version; }

            set
            { _version = value; }
        }
        private string _version;

        /// <summary>
        /// Specifies the path to look in. 
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty()]
        public string Path
        {
            get
            { return _path; }

            set
            { _path = value; }
        }
        private string _path;
        

        /*
        /// <summary>
        /// Specifies to include prerelease versions
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        public SwitchParameter Prerelease
        {
            get
            { return _prerelease; }

            set
            { _prerelease = value; }
        }
        private SwitchParameter _prerelease;
        */

        public static readonly string OsPlatform = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
      
        private string programFilesPath;
        private string myDocumentsPath;


        protected override void ProcessRecord()
        {
            WriteDebug("Entering GetPSResource");
            var dirsToSearch = new List<string>();

            if (_path != null)
            {
                WriteDebug(string.Format("Provided path is: '{0}'", _path));
                dirsToSearch.AddRange(Directory.GetDirectories(_path).ToList());
            }
            else
            { 
                var isWindows = OsPlatform.ToLower().Contains("windows");

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
                WriteDebug(string.Format("Current user scope path: '{0}'", myDocumentsPath));
                WriteDebug(string.Format("All users scope path: '{0}'", programFilesPath));

                /*** Will search first in PSModulePath, then will search in default paths ***/
                try
                {
                    foreach (var path in modulePaths)
                    {
                        dirsToSearch.AddRange(Directory.GetDirectories(path).ToList());
                    }
                    WriteDebug(string.Format("PSModulePath directories: '{0}'", dirsToSearch.ToString()));
                }
                catch { }

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
                WriteDebug(string.Format("All directories to search: '{0}'", dir));
            }

            // Or a list of the passed in names
            if (_name != null && !_name[0].Equals("*"))
            {
                var nameLowerCased = new List<string>();
                var scriptXMLnames = new List<string>();
                Array.ForEach(_name, n => nameLowerCased.Add(n.ToLower()));
                Array.ForEach(_name, n => scriptXMLnames.Add((n + "_InstalledScriptInfo.xml").ToLower()));

                /* 
                foreach (var name in nameLowerCased)
                {
                    WriteDebug(string.Format("Name in nameLowerCased: '{0}'", name));
                }
                


                foreach (var dir in dirsToSearch)
                {
                    WriteDebug((System.IO.Path.GetFileNameWithoutExtension(dir)).ToLower());
                }
                */
                dirsToSearch = dirsToSearch.FindAll(p => (nameLowerCased.Contains(new DirectoryInfo(p).Name.ToLower())
                    || scriptXMLnames.Contains((System.IO.Path.GetFileName(p)).ToLower())));
                    
                WriteDebug(dirsToSearch.Any().ToString());
            }

            // try to parse into a specific NuGet version
            VersionRange versionRange = null;
            if (_version != null)
            {
                NuGetVersion specificVersion;
                NuGetVersion.TryParse(_version, out specificVersion);

                if (specificVersion != null)
                {
                    // exact version
                    versionRange = new VersionRange(specificVersion, true, specificVersion, true, null, null);
                    WriteDebug(string.Format("A specific version, '{0}', is specified", versionRange.ToString()));

                }
                else
                {
                    // check if version range
                    versionRange = VersionRange.Parse(_version);
                    WriteDebug(string.Format("A version range, '{0}', is specified", versionRange.ToString()));

                }
            }

            List<string> installedPkgsToReturn = new List<string>();

            IEnumerable<string> returnPkgs = null;
            var versionDirs = new List<string>();


            //2) use above list to check 
            // if the version specificed is a version range
            if (versionRange != null)
            {
                foreach (var pkgPath in dirsToSearch)
                {
                    WriteDebug(string.Format("Searching through package path: '{0}'", pkgPath));
                    var versionsDirs = Directory.GetDirectories(pkgPath);

                    foreach (var versionPath in versionsDirs)
                    {
                        WriteDebug(string.Format("Searching through package version path: '{0}'", versionPath));
                        NuGetVersion dirAsNugetVersion;
                        var dirInfo = new DirectoryInfo(versionPath);
                        NuGetVersion.TryParse(dirInfo.Name, out dirAsNugetVersion);
                        WriteDebug(string.Format("Directory parsed as NuGet version: '{0}'", dirAsNugetVersion));

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
                                WriteDebug(string.Format("Getting sub directories from : '{0}'", pkgPath));

                                // Check if the pkg path actually has version sub directories.
                                if (versionsDirs.Length != 0)
                                {
                                    Array.Sort(versionsDirs, StringComparer.OrdinalIgnoreCase);
                                    Array.Reverse(versionsDirs);

                                    var pkgXmlFilePath = System.IO.Path.Combine(versionsDirs.First(), "PSGetModuleInfo.xml");

                                    // TODO:  check if this xml file exists, if it doesn't check if it exists in a previous version
                                    WriteDebug(string.Format("Found module XML: '{0}'", pkgXmlFilePath));

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
                WriteDebug(string.Format("No version provided-- check each path for the requested package"));
                // if no version is specified, just get the latest version
                foreach (var pkgPath in dirsToSearch)
                {
                    WriteDebug(string.Format("Searching through package path: '{0}'", pkgPath));

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
                            WriteDebug(string.Format("Found package XML: '{0}'", pkgXmlFilePath));
                            installedPkgsToReturn.Add(pkgXmlFilePath);
                        }
                    }
                }
            }


            // Flatten returned pkgs before displaying output returnedPkgsFound.Flatten().ToList()[0]
            var flattenedPkgs = installedPkgsToReturn.Flatten();

            foreach (string xmlFilePath in flattenedPkgs)
            {
                WriteDebug(string.Format("Reading package metadata from: '{0}'", xmlFilePath));

                // Open xml and read metadata from it     
                if (File.Exists(xmlFilePath))
                {
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> nameInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> versionInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> additionalMetadataInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> psDataInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> repositoryInfo;
                    ReadOnlyPSMemberInfoCollection<PSPropertyInfo> descriptionversionInfo;

                    var isPrelease = false;
                    using (StreamReader sr = new StreamReader(xmlFilePath))
                    {

                        string text = sr.ReadToEnd();
                        var deserializedObj = (PSObject)PSSerializer.Deserialize(text);

                        nameInfo = deserializedObj.Properties.Match("Name");

                        /* // testing adding prerelease parameter
                        additionalMetadataInfo = deserializedObj.Properties.Match("AdditionalMetadata");
                        if (additionalMetadataInfo.Any())
                        {
                            isPrelease = additionalMetadataInfo.FirstOrDefault().Value.ToString().Contains("IsPrerelease=true");
                            if ((isPrelease == true) && _prerelease) // find a stable version of the pkg {}
                        }
                        */

                        versionInfo = deserializedObj.Properties.Match("Version");
                        repositoryInfo = deserializedObj.Properties.Match("Repository");
                        descriptionversionInfo = deserializedObj.Properties.Match("Description");

                    };

                    // if -Prerelease is not passed in as a parameter, don't allow prerelease pkgs to be returned,
                    // we still want all pkgs to be returned if -Prerelease is passed in as a param
                    //if ((_prerelease == false && isPrelease == false) || _prerelease == true)
                    //{
                    PSObject pkgAsPSObject = new PSObject();
                    try
                    {
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Name", nameInfo.FirstOrDefault().Value));   // need to fix output
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Version", versionInfo.FirstOrDefault().Value));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Repository", repositoryInfo.FirstOrDefault().Value));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Description", descriptionversionInfo.FirstOrDefault().Value));
                        WriteObject(pkgAsPSObject);
                    }
                    catch { }
                    //}
                }
            }
        }
    }
}
