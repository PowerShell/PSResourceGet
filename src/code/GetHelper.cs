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
using static Microsoft.PowerShell.PowerShellGet.UtilClasses.PSResourceInfo.VersionInfo;
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
                        wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(p => nameWildCardPattern.IsMatch(System.IO.Path.GetFileName(p))));
                    }
                    else
                    {
                        // modules
                        wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(p => new DirectoryInfo(p).Name.Equals(n, StringComparison.OrdinalIgnoreCase)));
                        
                        // script paths will look something like this:  InstalledScriptInfos/<name of script>.xml
                        wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(p => System.IO.Path.GetFileName(p).Equals(n, StringComparison.OrdinalIgnoreCase)));
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

            // check if the version specified is within a version range
            if (Utils.TryParseToNuGetVersionRange(version, cmdletPassedIn, out versionRange))
            {
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
        
                            if (versionRange.Satisfies(dirAsNugetVersion))
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
            else
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
                    using (StreamReader sr = new StreamReader(xmlFilePath))
                    {
                        string text = sr.ReadToEnd();
                        PSResourceInfo deserializedObj = (PSResourceInfo) PSSerializer.Deserialize(text);
                    
                        PSResourceInfo pkgAsPSObject = new PSResourceInfo();

                        if (deserializedObj != null)
                        {
                            // parsing version property
                            // We need to check versions specifically for scripts...
                            // Since we can only determine a script version from the metadata file or the script itself,
                            // we now need to validate that if a version parameter was passed into the cmdlet, the version of this script falls within that version range
                            // If version range is not null and the xmlFilePath is for a script
                            if (versionRange != null && xmlFilePath.EndsWith("_InstalledScriptInfo.xml", StringComparison.OrdinalIgnoreCase))
                            {
                                cmdletPassedIn.WriteDebug(string.Format("Searching through package path: '{0}'", xmlFilePath));

                                // try to parse the version in the xml
                                System.Version.TryParse(deserializedObj.Properties["Version"].Value?.ToString(), out System.Version versionProp);
                                NuGetVersion.TryParse(deserializedObj.Properties["Version"].Value?.ToString(), out NuGetVersion nugetVersion);

                                if (versionRange.Satisfies(nugetVersion))
                                {
                                    // Continue parsing the rest of the object
                                    pkgAsPSObject.Version = versionProp;
                                }
                                else
                                {
                                    // Otherwise skip parsing the rest of this object
                                    break;
                                }
                            }
                            else
                            {
                                System.Version.TryParse(deserializedObj.Properties["Version"].Value?.ToString(), out System.Version versionProp);
                                pkgAsPSObject.Version = versionProp;
                            }

                            Hashtable stringPropertiesHash = new Hashtable();
                            string[] hashKeys = { "Name", "Description", "Author", "CompanyName", "Copyright", "LicenseUri", "ProjectUri",
                            "IconUri", "PowerShellGetFormatVersion", "ReleaseNotes", "Repository", "IsPrerelease", "AdditionalMetadata", "InstalledLocation" };
                            // parsing string properties
                            foreach (string property in hashKeys)
                            {
                                if (LanguagePrimitives.TryConvertTo(
                                    (deserializedObj.Properties[property] != null ? deserializedObj.Properties[property].Value?.ToString() : String.Empty), out string propertyValue))
                                {
                                    // add parsed value to hashtable
                                    stringPropertiesHash.Add(property, propertyValue);
                                }
                                else
                                {
                                    // non-terminating error
                                    string exMessage = String.Format(string.Format("Error parsing '{0}' property from resource metadata file", property));
                                    ParseException ex = new ParseException(exMessage);
                                    var ErrorParsingStrProperty = new ErrorRecord(ex, "ErrorParsingStrProperty", ErrorCategory.ParserError, null);
                                    cmdletPassedIn.WriteError(ErrorParsingStrProperty);
                                }
                            }
                            pkgAsPSObject.Name = (string)stringPropertiesHash["Name"];
                            pkgAsPSObject.Description = (string)stringPropertiesHash["Description"];
                            pkgAsPSObject.Author = (string)stringPropertiesHash["Author"];
                            pkgAsPSObject.CompanyName = (string)stringPropertiesHash["CompanyName"];
                            pkgAsPSObject.Copyright = (string)stringPropertiesHash["Copyright"];
                            pkgAsPSObject.ReleaseNotes = (string)stringPropertiesHash["ReleaseNotes"];
                            pkgAsPSObject.Repository = (string)stringPropertiesHash["Repository"];
                            pkgAsPSObject.AdditionalMetadata = (string)stringPropertiesHash["AdditionalMetadata"];
                            pkgAsPSObject.InstalledLocation = (string)stringPropertiesHash["InstalledLocation"];
                            pkgAsPSObject.LicenseUri = (string)stringPropertiesHash["LicenseUri"];
                            pkgAsPSObject.ProjectUri = (string)stringPropertiesHash["ProjectUri"];
                            pkgAsPSObject.IconUri = (string)stringPropertiesHash["IconUri"];
                            pkgAsPSObject.PowerShellGetFormatVersion = (string)stringPropertiesHash["PowerShellGetFormatVersion"];
                            pkgAsPSObject.IsPrerelease = (string)stringPropertiesHash["IsPrerelease"];

                            // parsing type property
                            pkgAsPSObject.Type = string.Equals(deserializedObj.Properties["Type"].Value?.ToString(), "Module", StringComparison.InvariantCultureIgnoreCase) ? ResourceType.Module : ResourceType.Script;

                            if (deserializedObj.Properties["PublishedDate"] != null)
                            {
                                try
                                {
                                    if (LanguagePrimitives.TryConvertTo(deserializedObj.Properties["PublishedDate"].Value, out DateTime? publishedDateProp))
                                    {
                                        pkgAsPSObject.PublishedDate = publishedDateProp;

                                    }
                                    else if (LanguagePrimitives.TryConvertTo((deserializedObj.Properties["PublishedDate"].Value as PSObject).Properties["DateTime"].Value, out DateTime? publishedDatePropPSObj))
                                    {
                                        pkgAsPSObject.PublishedDate = publishedDatePropPSObj;
                                    }
                                    else
                                    {
                                        // non-terminating error
                                        string exMessage = String.Format("Error parsing 'PublishedDate' property from resource metadata file");
                                        ParseException ex = new ParseException(exMessage);
                                        var ErrorParsingPublishedDate = new ErrorRecord(ex, "ErrorParsingPublishedDate", ErrorCategory.ParserError, null);
                                        cmdletPassedIn.WriteError(ErrorParsingPublishedDate);
                                    }
                                }
                                catch
                                {
                                    throw new Exception("PublishedDate ERROR");
                                }
                            }

                            if (deserializedObj.Properties["InstalledDate"] != null)
                            {
                                try
                                {
                                    if (LanguagePrimitives.TryConvertTo(deserializedObj.Properties["InstalledDate"].Value, out DateTime? installedDateProp))
                                    {
                                        pkgAsPSObject.InstalledDate = installedDateProp;
                                    }
                                    else if (LanguagePrimitives.TryConvertTo((deserializedObj.Properties["InstalledDate"].Value as PSObject).Properties["Date"].Value, out installedDateProp))
                                    {
                                        pkgAsPSObject.InstalledDate = installedDateProp;
                                    }
                                    else
                                    {
                                        // non-terminating error
                                        string exMessage = String.Format("Error parsing 'InstalledDate' property from resource metadata file");
                                        ParseException ex = new ParseException(exMessage);
                                        var ErrorParsingInstalledDate = new ErrorRecord(ex, "ErrorParsingInstalledDate", ErrorCategory.ParserError, null);
                                        cmdletPassedIn.WriteError(ErrorParsingInstalledDate);
                                    }
                                }
                                catch
                                {
                                    throw new Exception("InstalledDate ERROR");
                                }
                            }

                            if (deserializedObj.Properties["UpdatedDate"] != null)
                            {
                                try
                                {

                                    if (LanguagePrimitives.TryConvertTo(deserializedObj.Properties["UpdatedDate"].Value, out DateTime? updatedDateProp))
                                    {
                                        pkgAsPSObject.UpdatedDate = updatedDateProp;
                                    }
                                    else
                                    {
                                        // non-terminating error
                                        string exMessage = String.Format("Error parsing 'UpdatedDate' property from resource metadata file");
                                        ParseException ex = new ParseException(exMessage);
                                        var ErrorParsingUpdatedDate = new ErrorRecord(ex, "ErrorParsingUpdatedDate", ErrorCategory.ParserError, null);
                                        cmdletPassedIn.WriteError(ErrorParsingUpdatedDate);
                                    }
                                }
                                catch
                                {
                                    throw new Exception("UpdatedDate ERROR");
                                }
                            }

                            if (deserializedObj.Properties["Tags"] != null)
                            {
                                try
                                {
                                    if (LanguagePrimitives.TryConvertTo(deserializedObj.Properties["Tags"].Value.ToString().Split(' '), out string[] tagsProp))
                                    {
                                        pkgAsPSObject.Tags = tagsProp;
                                    }
                                    else
                                    {
                                        // non-terminating error
                                        string exMessage = String.Format("Error parsing 'Tags' property from resource metadata file");
                                        ParseException ex = new ParseException(exMessage);
                                        var ErrorParsingTags = new ErrorRecord(ex, "ErrorParsingTags", ErrorCategory.ParserError, null);
                                        cmdletPassedIn.WriteError(ErrorParsingTags);
                                    }
                                }
                                catch
                                {
                                    throw new Exception("TAGS ERROR");
                                }
                            }

                            if (deserializedObj.Properties["Dependencies"] != null)
                            {
                                try
                                {
                                    ArrayList listOfDependencies = (deserializedObj.Properties["Dependencies"].Value as PSObject).BaseObject as ArrayList;

                                    Dictionary<string, VersionInfo> depDictionary = new Dictionary<string, VersionInfo>();


                                    foreach (PSObject dependency in listOfDependencies)
                                    {
                                        Hashtable depHash = dependency.BaseObject as Hashtable;
                                        VersionType versionType = VersionType.Unknown;
                                        System.Version versionNum = null;

                                        if (depHash["MinimumVersion"] != null)
                                        {
                                            versionType = VersionType.MinimumVersion;
                                            System.Version.TryParse(depHash["MinimumVersion"].ToString(), out versionNum);
                                        }
                                        if (depHash["RequiredVersion"] != null)
                                        {
                                            versionType = VersionType.RequiredVersion;
                                            System.Version.TryParse(depHash["RequiredVersion"].ToString(), out versionNum);
                                        }
                                        if (depHash["MaximumVersion"] != null)
                                        {
                                            versionType = VersionType.MaximumVersion;
                                            System.Version.TryParse(depHash["MaximumVersion"].ToString(), out versionNum);
                                        }
                                        VersionInfo versionInfo = new VersionInfo(versionType, versionNum);

                                        depDictionary.Add(depHash["Name"].ToString(), versionInfo);
                                    }

                                    pkgAsPSObject.Dependencies = depDictionary;
                                }
                                catch
                                {
                                    throw new Exception("DEPENDENCIES ERROR");
                                }
                            }

                            if (deserializedObj.Properties["Includes"] != null)
                            {
                                try
                                {
                                    if (LanguagePrimitives.TryConvertTo((deserializedObj.Properties["Includes"].Value as PSObject).BaseObject, out Hashtable includesHash))
                                    {
                                        if (includesHash.ContainsKey("Command") && includesHash["Command"] != null)
                                        {
                                            pkgAsPSObject.Commands = (ArrayList)(includesHash["Command"] as PSObject).BaseObject;
                                        }
                                        if (includesHash.ContainsKey("DscResource") && includesHash["DscResource"] != null)
                                        {
                                            pkgAsPSObject.DscResources = (ArrayList)(includesHash["DscResource"] as PSObject).BaseObject;
                                        }
                                        if (includesHash.ContainsKey("Function") && includesHash["Function"] != null)
                                        {
                                            pkgAsPSObject.Functions = (ArrayList)(includesHash["Function"] as PSObject).BaseObject;
                                        }
                                        if (includesHash.ContainsKey("Cmdlet") && includesHash["Cmdlet"] != null)
                                        {
                                            pkgAsPSObject.Cmdlets = (ArrayList)(includesHash["Cmdlet"] as PSObject).BaseObject;
                                        }
                                    }
                                    else if (LanguagePrimitives.TryConvertTo((deserializedObj.Properties["Includes"].Value as PSObject).BaseObject, out ArrayList includesArrayList))
                                    {
                                        var test = includesArrayList[0];
                                        var test2 = test as PSObject;
                                        var r1 = test2.Properties;
                                        var r2 = test2.Members;

                                        //Array test0 = (Array)includesArrayList[0];
                                        //ArrayList test = (ArrayList) includesArrayList[0];
                                        LanguagePrimitives.TryConvertTo(includesArrayList[0], out Hashtable testtt);
                                        //Dictionary<string, ArrayList> bleh = (Dictionary<string, ArrayList>) includesArrayList[0];


                                        if (includesHash.ContainsKey("Command") && includesHash["Command"] != null)
                                        {
                                            pkgAsPSObject.Commands = (ArrayList)(includesHash["Command"] as PSObject).BaseObject;
                                        }
                                        if (includesHash.ContainsKey("DscResource") && includesHash["DscResource"] != null)
                                        {
                                            pkgAsPSObject.DscResources = (ArrayList)(includesHash["DscResource"] as PSObject).BaseObject;
                                        }
                                        if (includesHash.ContainsKey("Function") && includesHash["Function"] != null)
                                        {
                                            pkgAsPSObject.Functions = (ArrayList)(includesHash["Function"] as PSObject).BaseObject;
                                        }
                                        if (includesHash.ContainsKey("Cmdlet") && includesHash["Cmdlet"] != null)
                                        {
                                            pkgAsPSObject.Cmdlets = (ArrayList)(includesHash["Cmdlet"] as PSObject).BaseObject;
                                        }
                                    }
                                    else
                                    {
                                        // non-terminating error
                                        string exMessage = String.Format("Error parsing 'Includes' property from resource metadata file");
                                        ParseException ex = new ParseException(exMessage);
                                        var ErrorParsingIncludes = new ErrorRecord(ex, "ErrorParsingIncludes", ErrorCategory.ParserError, null);
                                        cmdletPassedIn.WriteError(ErrorParsingIncludes);
                                    }
                                }
                                catch
                                {
                                    throw new Exception("INCLUDES ERROR");
                                }
                            }
                            

                            yield return pkgAsPSObject;
                        }
                    }
                }
            }
        }
    }
}
