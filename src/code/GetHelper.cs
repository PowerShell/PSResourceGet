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

            List<string> installedPkgsToReturn = FilterPkgsByVersion(version, dirsToSearch, out VersionRange versionRange);

            foreach (PSResourceInfo pkgObject in OutputPackageObject(installedPkgsToReturn, versionRange))
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
                string pfScriptsPath = System.IO.Path.Combine(programFilesPath, "Scripts", "InstalledScriptInfos");
                if (Directory.Exists(pfScriptsPath))
                {
                    dirsToSearch.AddRange(Directory.GetFiles(pfScriptsPath).ToList());
                }
                string mdScriptsPath = System.IO.Path.Combine(myDocumentsPath, "Scripts", "InstalledScriptInfos");
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

                List<string> scriptXMLnames = new List<string>();
                Array.ForEach(name, n => scriptXMLnames.Add((n + "_InstalledScriptInfo.xml").ToLower()));
                char[] fileNameDelimiter = new char[] { '_' };

                foreach (var n in name)
                {
                    if (n.Contains("*"))
                    {
                        char[] wildcardDelimiter = new char[] { '*' };
                        var tokenizedName = n.Split(wildcardDelimiter, StringSplitOptions.RemoveEmptyEntries);

                        // 1)  *owershellge*
                        if (n.StartsWith("*") && n.EndsWith("*"))
                        {
                            wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(p => (new DirectoryInfo(p).Name.ToLower().Contains(tokenizedName[0]))));

                            wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(p =>
                                (System.IO.Path.GetFileName(p).Split(fileNameDelimiter, StringSplitOptions.RemoveEmptyEntries))[0].ToLower().Contains(tokenizedName[0])));
                        }
                        // 2)  *erShellGet
                        else if (n.StartsWith("*"))
                        {
                            wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(p => (new DirectoryInfo(p).Name.ToLower().EndsWith(tokenizedName[0]))));

                            wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(p =>
                                (System.IO.Path.GetFileName(p).Split(fileNameDelimiter, StringSplitOptions.RemoveEmptyEntries))[0].ToLower().EndsWith(tokenizedName[0])));
                        }
                        // 3)  PowerShellG*
                        else if (n.EndsWith("*"))
                        {
                            wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(p => (new DirectoryInfo(p).Name.ToLower().StartsWith(tokenizedName[0]))));
                            wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(p =>
                                (System.IO.Path.GetFileName(p).Split(fileNameDelimiter, StringSplitOptions.RemoveEmptyEntries))[0].ToLower().StartsWith(tokenizedName[0])));
                        }
                        // 4)  Power*Get
                        else if (tokenizedName.Length == 2)
                        {
                            wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(p =>
                                (new DirectoryInfo(p).Name.ToLower().StartsWith(tokenizedName[0])
                                && new DirectoryInfo(p).Name.ToLower().EndsWith(tokenizedName[1]))));
                            wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(p =>
                                (System.IO.Path.GetFileName(p).Split(fileNameDelimiter, StringSplitOptions.RemoveEmptyEntries))[0].ToLower().StartsWith(tokenizedName[0])
                                && (System.IO.Path.GetFileName(p).Split(fileNameDelimiter, StringSplitOptions.RemoveEmptyEntries))[0].ToLower().EndsWith(tokenizedName[0])));
                        }
                    }
                    else
                    {
                        wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(p => (new DirectoryInfo(p).Name.ToLower().Equals(n))));
                        // script paths will look something like this:  InstalledScriptInfos \  <name of script>.xml

                        wildCardDirsToSearch.AddRange(dirsToSearch.FindAll(p => (scriptXMLnames.Contains(System.IO.Path.GetFileName(p).ToLower()))));
                    }
                }

                cmdletPassedIn.WriteDebug(wildCardDirsToSearch.Any().ToString());
            }

            return wildCardDirsToSearch;
        }

        // Filter by user provided version
        public List<string> FilterPkgsByVersion(string version, List<string> dirsToSearch, out VersionRange versionRange)
        {
            List<string> installedPkgsToReturn = new List<string>();  // these are the xmls

            // try to parse into a specific NuGet version
           versionRange = null;
            if (version != null)
            {
                NuGetVersion.TryParse(version, out NuGetVersion specificVersion);

                if (specificVersion != null)
                {
                    // check if exact version
                    versionRange = new VersionRange(specificVersion, true, specificVersion, true, null, null);
                    cmdletPassedIn.WriteDebug(string.Format("A specific version, '{0}', is specified", versionRange.ToString()));
                }
                else
                {
                    // check if version range
                    if (!VersionRange.TryParse(version, out versionRange))
                    {
                        cmdletPassedIn.WriteError(new ErrorRecord(
                            new ParseException(),
                            "ErrorParsingVersion",
                            ErrorCategory.ParserError,
                            this));
                    }
                    cmdletPassedIn.WriteDebug(string.Format("A version range, '{0}', is specified", versionRange.ToString()));
                }
            }

            
            // check if the version specified is within a version range
            if (versionRange != null)
            {
                foreach (string pkgPath in dirsToSearch)
                {
                    // this is going to happen only for modules, not for scripts 
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
            else
            {
                cmdletPassedIn.WriteDebug("No version provided-- check each path for the requested package");
                // if no version is specified, just get the latest version
                foreach (string pkgPath in dirsToSearch)
                {
                    cmdletPassedIn.WriteDebug(string.Format("Searching through package path: '{0}'", pkgPath));
                   
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
            return installedPkgsToReturn;
        }

        // Create package object for each found resource directory
        public IEnumerable<PSResourceInfo> OutputPackageObject(List<string> installedPkgsToReturn, VersionRange versionRange)
        {
            ///// Create package object for each found resource directory
            /////  Read metadata from XML and parse into PSResourceInfo object           
            IEnumerable<object> flattenedPkgs = FlattenExtension.Flatten(installedPkgsToReturn);
            List<PSResourceInfo> foundInstalledPkgs = new List<PSResourceInfo>();

            // Read metadata from XML and parse into PSResourceInfo object
            foreach (string xmlFilePath in flattenedPkgs)
            {
                cmdletPassedIn.WriteDebug(string.Format("Reading package metadata from: '{0}'", xmlFilePath));

                if (File.Exists(xmlFilePath))
                {
                    PSObject deserializedObj = null;
                    using (StreamReader sr = new StreamReader(xmlFilePath))
                    {
                        string text = sr.ReadToEnd();
                        deserializedObj = (PSObject)PSSerializer.Deserialize(text);
                    };

                    PSResourceInfo pkgAsPSObject = new PSResourceInfo();

                    if (deserializedObj != null)
                    {
                        try
                        {
                            LanguagePrimitives.TryConvertTo(
                                (deserializedObj.Properties["Name"] != null ? deserializedObj.Properties["Name"].Value?.ToString() : String.Empty), out string nameProp);
                            pkgAsPSObject.Name = nameProp;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("NAME error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingName = new ErrorRecord(ex, "ErrorParsingName", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingName);
                        }

                        // We need to check versions specifically for scripts...
                        try
                        {
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
                        }
                        catch (Exception e)
                        {
                            // make these debug statements 
                            string exMessage = String.Format("VERSION error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingVersion = new ErrorRecord(ex, "ErrorParsingVersion", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingVersion);
                        }

                        pkgAsPSObject.Type = string.Equals(deserializedObj.Properties["Type"].Value?.ToString(), "Module", StringComparison.InvariantCultureIgnoreCase) ? ResourceType.Module : ResourceType.Script;

                        try
                        {
                            LanguagePrimitives.TryConvertTo(
                                (deserializedObj.Properties["Description"] != null ? deserializedObj.Properties["Description"].Value?.ToString() : String.Empty), out string descriptionProp);
                            pkgAsPSObject.Description = descriptionProp;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("DESCRIPTION error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingDescription = new ErrorRecord(ex, "ErrorParsingDescription", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingDescription);
                        }

                        try
                        {
                            LanguagePrimitives.TryConvertTo(
                                (deserializedObj.Properties["Author"] != null ? deserializedObj.Properties["Author"].Value?.ToString() : String.Empty), out string authorProp);
                            pkgAsPSObject.Author = authorProp;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("AUTHOR error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingAuthor = new ErrorRecord(ex, "ErrorParsingAuthor", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingAuthor);
                        }

                        try
                        {
                            LanguagePrimitives.TryConvertTo(
                                    (deserializedObj.Properties["CompanyName"] != null ? deserializedObj.Properties["CompanyName"].Value?.ToString() : String.Empty), out string companyNameProp);
                            pkgAsPSObject.CompanyName = companyNameProp;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("COMPANY NAME error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingCompanyName = new ErrorRecord(ex, "ErrorParsingCompanyName", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingCompanyName);
                        }

                        try
                        {
                            LanguagePrimitives.TryConvertTo(
                                (deserializedObj.Properties["Copyright"] != null ? deserializedObj.Properties["Copyright"].Value?.ToString() : String.Empty), out string copyrightProp);
                            pkgAsPSObject.Copyright = copyrightProp;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("COPYRIGHT error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingCopyright = new ErrorRecord(ex, "ErrorParsingCopyright", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingCopyright);
                        }

                        try
                        {
                            LanguagePrimitives.TryConvertTo(
                                (deserializedObj.Properties["PublishedDate"] != null ? (DateTime?)(((deserializedObj.Properties["PublishedDate"]).Value) as PSObject).Properties["DateTime"].Value : null), out DateTime publishedDateProp);
                            pkgAsPSObject.PublishedDate = publishedDateProp;
                        }
                        catch (Exception e)
                        {
                            try
                            {
                                LanguagePrimitives.TryConvertTo(
                                    (deserializedObj.Properties["PublishedDate"] != null ? (DateTime?)deserializedObj.Properties["PublishedDate"].Value : null), out DateTime? publishedDateProp);
                                pkgAsPSObject.PublishedDate = publishedDateProp;
                            }
                            catch (Exception e2)
                            {
                                string exMessage = String.Format("PUBLISHED DATE error: " + e.Message);
                                ParseException ex = new ParseException(exMessage);
                                var ErrorParsingPublishedDate = new ErrorRecord(ex, "ErrorParsingPublishedDate", ErrorCategory.ParserError, null);
                                cmdletPassedIn.WriteError(ErrorParsingPublishedDate);
                            }
                        }

                        try
                        {
                            LanguagePrimitives.TryConvertTo(
                                (deserializedObj.Properties["InstalledDate"] != null ? (DateTime?)(deserializedObj.Properties["InstalledDate"].Value as PSObject).Properties["Date"].Value : null), out DateTime? installedDateProp);
                            pkgAsPSObject.InstalledDate = installedDateProp;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("INSTALLED DATE error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingInstalledDate = new ErrorRecord(ex, "ErrorParsingInstalledDate", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingInstalledDate);
                        }

                        try
                        {
                            LanguagePrimitives.TryConvertTo(
                                (deserializedObj.Properties["UpdatedDate"] != null ? (DateTime?)deserializedObj.Properties["UpdatedDate"].Value : null), out DateTime updatedDateProp);
                            pkgAsPSObject.UpdatedDate = updatedDateProp;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("UPDATED DATE error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingUpdatedDate = new ErrorRecord(ex, "ErrorParsingUpdatedDate", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingUpdatedDate);
                        }

                        try
                        {
                            LanguagePrimitives.TryConvertTo(
                                (deserializedObj.Properties["LicenseUri"] != null ? (Uri)deserializedObj.Properties["LicenseUri"].Value : null), out Uri licenseUriProp);
                            pkgAsPSObject.LicenseUri = licenseUriProp;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("LICENSE URI error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingLicenseUri = new ErrorRecord(ex, "ErrorParsingLicenseUri", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingLicenseUri);
                        }

                        try
                        {
                            LanguagePrimitives.TryConvertTo(
                                (deserializedObj.Properties["ProjectUri"] != null ? (Uri)deserializedObj.Properties["ProjectUri"].Value : null), out Uri projectUriProp);
                            pkgAsPSObject.ProjectUri = projectUriProp;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("PROJECT URI error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingProjectUri = new ErrorRecord(ex, "ErrorParsingProjectUri", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingProjectUri);
                        }

                        try
                        {
                            LanguagePrimitives.TryConvertTo(
                                 (deserializedObj.Properties["IconUri"] != null ? (Uri)deserializedObj.Properties["IconUri"].Value : null), out Uri iconUriProp);
                            pkgAsPSObject.IconUri = iconUriProp;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("ICON URI error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingIconUri = new ErrorRecord(ex, "ErrorParsingIconUri", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingIconUri);
                        }

                        try
                        {
                            System.Version.TryParse(
                                (deserializedObj.Properties["PowerShellGetFormatVersion"] != null ? deserializedObj.Properties["PowerShellGetFormatVersion"].Value?.ToString() : String.Empty),
                                out System.Version powerShellGetFormatVersionProp);
                            pkgAsPSObject.PowerShellGetFormatVersion = powerShellGetFormatVersionProp;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("POWERSHELLGET FORMAT VERSION error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingPowerShellGetFormatVersion = new ErrorRecord(ex, "ErrorParsingPowerShellGetFormatVersion", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingPowerShellGetFormatVersion);
                        }

                        try
                        {
                            LanguagePrimitives.TryConvertTo(
                                   (deserializedObj.Properties["ReleaseNotes"] != null ? deserializedObj.Properties["ReleaseNotes"].Value?.ToString() : null), out string releaseNotesProp);
                            pkgAsPSObject.ReleaseNotes = releaseNotesProp;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("RELEASE NOTES error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingReleaseNotes = new ErrorRecord(ex, "ErrorParsingReleaseNotes", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingReleaseNotes);
                        }

                        try
                        {
                            LanguagePrimitives.TryConvertTo(
                                (deserializedObj.Properties["Repository"] != null ? deserializedObj.Properties["Repository"].Value?.ToString() : null), out string repositoryProp);
                            pkgAsPSObject.Repository = repositoryProp;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("REPOSITORY error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingRepository = new ErrorRecord(ex, "ErrorParsingRepository", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingRepository);
                        }

                        try
                        {
                            bool.TryParse(deserializedObj.Properties["IsPrerelease"] != null ? deserializedObj.Properties["IsPrerelease"].Value?.ToString() : null, out bool isPrerelease);
                            pkgAsPSObject.IsPrerelease = isPrerelease;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("IS PRERELEASE error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingIsPrerelease = new ErrorRecord(ex, "ErrorParsingIsPrerelease", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingIsPrerelease);
                        }

                        try
                        {

                            string[] emptyArr = new string[] { };

                            LanguagePrimitives.TryConvertTo(
                                (deserializedObj.Properties["Tags"] != null ? (string[])(deserializedObj.Properties["Tags"].Value.ToString()).Split(' ') : emptyArr), out string[] tagsProp);
                            pkgAsPSObject.Tags = tagsProp;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("TAGS error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingTags = new ErrorRecord(ex, "ErrorParsingTags", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingTags);
                        }

                        try
                        {
                            ArrayList listOfDependencies = ((deserializedObj.Properties["Dependencies"].Value as PSObject).BaseObject as ArrayList);

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
                        catch (Exception e)
                        {
                            string exMessage = String.Format("DEPENDENCIES error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingDependencies = new ErrorRecord(ex, "ErrorParsingDependencies", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingDependencies);
                        }

                        try
                        {
                            Hashtable includesHash = ((deserializedObj.Properties["Includes"].Value as PSObject).BaseObject) as Hashtable;

                            pkgAsPSObject.Commands = (ArrayList)(includesHash["Command"] as PSObject).BaseObject;
                            pkgAsPSObject.DscResources = (ArrayList)(includesHash["DscResource"] as PSObject).BaseObject;
                            pkgAsPSObject.Functions = (ArrayList)(includesHash["Function"] as PSObject).BaseObject;
                            pkgAsPSObject.Cmdlets = (ArrayList)(includesHash["Cmdlet"] as PSObject).BaseObject;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("INCLUDES error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingIncludes = new ErrorRecord(ex, "ErrorParsingIncludes", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingIncludes);
                        }

                        try
                        {
                            LanguagePrimitives.TryConvertTo(
                                (deserializedObj.Properties["AdditionalMetadata"] != null ? (string)deserializedObj.Properties["AdditionalMetadata"].Value.ToString() : string.Empty), out string additionalMetadataProp);
                            pkgAsPSObject.AdditionalMetadata = additionalMetadataProp;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("ADDITIONAL METADATA error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingAdditionalMetadata = new ErrorRecord(ex, "ErrorParsingAdditionalMetadata", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingAdditionalMetadata);
                        }

                        try
                        {
                            LanguagePrimitives.TryConvertTo(
                                (deserializedObj.Properties["InstalledLocation"] != null ? (string)deserializedObj.Properties["InstalledLocation"].Value : string.Empty), out string installedLocationProp);
                            pkgAsPSObject.InstalledLocation = installedLocationProp;
                        }
                        catch (Exception e)
                        {
                            string exMessage = String.Format("ADDITIONAL METADATA error: " + e.Message);
                            ParseException ex = new ParseException(exMessage);
                            var ErrorParsingAdditionalMetadata = new ErrorRecord(ex, "ErrorParsingAdditionalMetadata", ErrorCategory.ParserError, null);
                            cmdletPassedIn.WriteError(ErrorParsingAdditionalMetadata);
                        }

                        yield return pkgAsPSObject;
                    }
                }
            }
        }
    }
}
