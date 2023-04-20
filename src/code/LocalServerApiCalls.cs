using System.Runtime.CompilerServices;
using System.Linq;
using System.Xml.Linq;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using NuGet.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Net;
using System.Management.Automation;
using System.Runtime.ExceptionServices;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    internal class LocalServerAPICalls : ServerApiCall
    {
        /*  ******NOTE*******:
        /*  Quotations in the urls can change the response.
        /*  for example:   http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='az* tag:PSScript'&includePrerelease=true
        /*  will return something different than 
        /*  http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm=az* tag:PSScript&includePrerelease=true
        /*  We believe the first example returns an "and" of the search term and the tag and the second returns "or",
        /*  this needs more investigation.
        /*  Some of the urls below may need to be modified.
        */

        // Any interface method that is not implemented here should be processed in the parent method and then call one of the implemented 
        // methods below.
        #region Members

        public override PSRepositoryInfo repository { get; set; }
        public override HttpClient s_client { get; set; }
        public FindResponseType localServerFindResponseType = FindResponseType.responseHashtable;
        public readonly string fileTypeKey = "filetype";

        #endregion

        #region Constructor

        public LocalServerAPICalls (PSRepositoryInfo repository, NetworkCredential networkCredential) : base (repository, networkCredential)
        {
            this.repository = repository;
            HttpClientHandler handler = new HttpClientHandler()
            {
                Credentials = networkCredential
            };

            s_client = new HttpClient(handler);
        }

        #endregion

        #region Overriden Methods 

        /// <summary>
        /// Find method which allows for searching for all packages from a repository and returns latest version for each.
        /// Examples: Search -Repository PSGallery
        /// API call: 
        /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion
        /// </summary>
        public override FindResults FindAll(bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            FindResults findResponse = new FindResults();
            List<Hashtable> pkgsFound = new List<Hashtable>();
            edi = null;

            Regex rx = new Regex(@"\.\d+\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Hashtable pkgVersionsFound = new Hashtable(StringComparer.OrdinalIgnoreCase);

            foreach (string path in Directory.GetFiles(repository.Uri.AbsolutePath))
            {
                string packageFullName = Path.GetFileName(path);
                MatchCollection matches = rx.Matches(packageFullName);
                Match match = matches[0];

                GroupCollection groups = match.Groups;
                Capture group = groups[0];

                string pkgName = packageFullName.Substring(0, group.Index);
                string version = packageFullName.Substring(group.Index + 1, packageFullName.LastIndexOf('.') - group.Index - 1);

                NuGetVersion.TryParse(version, out NuGetVersion nugetVersion);

                if (!nugetVersion.IsPrerelease || includePrerelease)
                {
                    if (!pkgVersionsFound.ContainsKey(pkgName))
                    {
                        Hashtable pkgInfo = new Hashtable(StringComparer.OrdinalIgnoreCase);
                        pkgInfo.Add("version", nugetVersion);
                        pkgInfo.Add("path", path);
                        pkgVersionsFound.Add(pkgName, pkgInfo);
                    }
                    else
                    {
                        Hashtable pkgInfo = pkgVersionsFound[pkgName] as Hashtable;
                        NuGetVersion existingVersion = pkgInfo["version"] as NuGetVersion;
                        if (nugetVersion > existingVersion)
                        {
                            pkgInfo["version"] = nugetVersion;
                            pkgInfo["path"] = path;
                            pkgVersionsFound[pkgName] = pkgInfo;
                        }
                    }
                
                }
            }

            List<string> pkgNamesList = pkgVersionsFound.Keys.Cast<string>().ToList();
            foreach(string pkgFound in pkgNamesList)
            {
                Hashtable pkgInfo = pkgVersionsFound[pkgFound] as Hashtable;
                NuGetVersion pkgVersion = pkgInfo["version"] as NuGetVersion;
                string pkgPath = pkgInfo["path"] as string;


                // create temp dir- unique for reach pkg
                var tempDiscoveryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                try
                {
                    var dir = Directory.CreateDirectory(tempDiscoveryPath);
                    dir.Attributes &= ~FileAttributes.ReadOnly;

                    // copy .nupkg
                    string destNupkgPath = Path.Combine(tempDiscoveryPath, Path.GetFileName(pkgPath));
                    File.Copy(pkgPath, destNupkgPath);

                    // change extension to .zip
                    string zipFilePath = Path.ChangeExtension(destNupkgPath, ".zip");
                    File.Move(destNupkgPath, zipFilePath);

                    // extract from .zip
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, tempDiscoveryPath);

                    string psd1FilePath = Path.Combine(tempDiscoveryPath, $"{pkgFound}.psd1");
                    string ps1FilePath = Path.Combine(tempDiscoveryPath, $"{pkgFound}.ps1");
                    string nuspecFilePath = Path.Combine(tempDiscoveryPath, $"{pkgFound}.nuspec");

                    Hashtable pkgMetadata = new Hashtable(StringComparer.InvariantCultureIgnoreCase);

                    if (File.Exists(psd1FilePath))
                    {
                        if (!Utils.TryReadManifestFile(psd1FilePath, out pkgMetadata, out Exception readManifestError))
                        {
                            edi = ExceptionDispatchInfo.Capture(readManifestError);
                            return findResponse;
                        }

                        GetPrivateDataFromHashtable(pkgMetadata, out string prereleaseLabel, out Uri licenseUri, out Uri projectUri, out Uri iconUri, out string releaseNotes, out string[] pkgHashTags);
                        pkgMetadata.Add("Tags", pkgHashTags);
                        pkgMetadata.Add("Prerelease", prereleaseLabel);
                        pkgMetadata.Add("LicenseUri", licenseUri);
                        pkgMetadata.Add("ProjectUri", projectUri);
                        pkgMetadata.Add("IconUri", iconUri);
                        pkgMetadata.Add("ReleaseNotes", releaseNotes);
                        pkgMetadata.Add("Id", pkgFound);
                        pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ModuleManifest);
                    }
                    else if (File.Exists(ps1FilePath))
                    {
                        if (!PSScriptFileInfo.TryTestPSScriptFile(ps1FilePath, out PSScriptFileInfo parsedScript, out ErrorRecord[] errors, out string[] verboseMsgs))
                        {
                            edi = ExceptionDispatchInfo.Capture(new InvalidDataException($"PSScriptFile could not be read properly")); // TODO: how to handle multiple? maybe just write a error of our own
                            return findResponse;
                        }

                        pkgMetadata = parsedScript.ToHashtable();
                        pkgMetadata.Add("Id", pkgFound);
                        pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ScriptFile);

                    }
                    else if (File.Exists(nuspecFilePath))
                    {
                        pkgMetadata = GetHashtableForNuspec(nuspecFilePath, out edi);
                        if (edi != null)
                        {
                            return findResponse;
                        }

                        pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.Nuspec);
                    }
                    else
                    {
                        edi = ExceptionDispatchInfo.Capture(new InvalidDataException($".nupkg package must contain either .psd1, .ps1, or .nuspec file and none were found")); // TODO: how to handle multiple? maybe just write a error of our own
                        return findResponse;
                    }

                    pkgsFound.Add(pkgMetadata);
                }
                catch (Exception e)
                {
                    edi = ExceptionDispatchInfo.Capture(new InvalidOperationException($"Temporary folder for installation could not be created or set due to: {e.Message}"));
                }
                finally
                {
                    if (Directory.Exists(tempDiscoveryPath))
                    {
                        Utils.DeleteDirectory(tempDiscoveryPath);
                    }
                }
            }

            findResponse = new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: pkgsFound.ToArray(), responseType: localServerFindResponseType);

            return findResponse;
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag from a repository and returns latest version for each.
        /// Examples: Search -Tag "JSON" -Repository PSGallery
        /// API call: 
        /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&searchTerm=tag:JSON&includePrerelease=true
        /// </summary>
        public override FindResults FindTags(string[] tags, bool includePrerelease, ResourceType _type, out ExceptionDispatchInfo edi)
        {
            FindResults findResponse = new FindResults();
            List<Hashtable> pkgsFound = new List<Hashtable>();
            edi = null;

            Regex rx = new Regex(@"\.\d+\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Hashtable pkgVersionsFound = new Hashtable(StringComparer.OrdinalIgnoreCase);

            foreach (string path in Directory.GetFiles(repository.Uri.AbsolutePath))
            {
                string packageFullName = Path.GetFileName(path);
                MatchCollection matches = rx.Matches(packageFullName);
                Match match = matches[0];

                GroupCollection groups = match.Groups;
                Capture group = groups[0];

                string pkgName = packageFullName.Substring(0, group.Index);
                string version = packageFullName.Substring(group.Index + 1, packageFullName.LastIndexOf('.') - group.Index - 1);

                NuGetVersion.TryParse(version, out NuGetVersion nugetVersion);

                if (!nugetVersion.IsPrerelease || includePrerelease)
                {
                    if (!pkgVersionsFound.ContainsKey(pkgName))
                    {
                        Hashtable pkgInfo = new Hashtable(StringComparer.OrdinalIgnoreCase);
                        pkgInfo.Add("version", nugetVersion);
                        pkgInfo.Add("path", path);
                        pkgVersionsFound.Add(pkgName, pkgInfo);
                    }
                    else
                    {
                        Hashtable pkgInfo = pkgVersionsFound[pkgName] as Hashtable;
                        NuGetVersion existingVersion = pkgInfo["version"] as NuGetVersion;
                        if (nugetVersion > existingVersion)
                        {
                            pkgInfo["version"] = nugetVersion;
                            pkgInfo["path"] = path;
                            pkgVersionsFound[pkgName] = pkgInfo;
                        }
                    }
                
                }
            }

            List<string> pkgNamesList = pkgVersionsFound.Keys.Cast<string>().ToList();
            foreach(string pkgFound in pkgNamesList)
            {
                Hashtable pkgInfo = pkgVersionsFound[pkgFound] as Hashtable;
                NuGetVersion pkgVersion = pkgInfo["version"] as NuGetVersion;
                string pkgPath = pkgInfo["path"] as string;


                // create temp dir- unique for reach pkg
                var tempDiscoveryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                try
                {
                    var dir = Directory.CreateDirectory(tempDiscoveryPath);
                    dir.Attributes &= ~FileAttributes.ReadOnly;

                    // copy .nupkg
                    string destNupkgPath = Path.Combine(tempDiscoveryPath, Path.GetFileName(pkgPath));
                    File.Copy(pkgPath, destNupkgPath);

                    // change extension to .zip
                    string zipFilePath = Path.ChangeExtension(destNupkgPath, ".zip");
                    File.Move(destNupkgPath, zipFilePath);

                    // extract from .zip
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, tempDiscoveryPath);

                    string psd1FilePath = Path.Combine(tempDiscoveryPath, $"{pkgFound}.psd1");
                    string ps1FilePath = Path.Combine(tempDiscoveryPath, $"{pkgFound}.ps1");
                    string nuspecFilePath = Path.Combine(tempDiscoveryPath, $"{pkgFound}.nuspec");

                    Hashtable pkgMetadata = new Hashtable(StringComparer.InvariantCultureIgnoreCase);
                    List<string> pkgTags = new List<string>();
                    if (File.Exists(psd1FilePath))
                    {
                        if (!Utils.TryReadManifestFile(psd1FilePath, out pkgMetadata, out Exception readManifestError))
                        {
                            edi = ExceptionDispatchInfo.Capture(readManifestError);
                            return findResponse;
                        }

                        GetPrivateDataFromHashtable(pkgMetadata, out string prereleaseLabel, out Uri licenseUri, out Uri projectUri, out Uri iconUri, out string releaseNotes, out string[] pkgHashTags);
                        pkgMetadata.Add("Tags", pkgHashTags);
                        pkgMetadata.Add("Prerelease", prereleaseLabel);
                        pkgMetadata.Add("LicenseUri", licenseUri);
                        pkgMetadata.Add("ProjectUri", projectUri);
                        pkgMetadata.Add("IconUri", iconUri);
                        pkgMetadata.Add("ReleaseNotes", releaseNotes);
                        pkgMetadata.Add("Id", pkgFound);
                        pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ModuleManifest);
                        pkgTags.AddRange(pkgHashTags);
                    }
                    else if (File.Exists(ps1FilePath))
                    {
                        if (!PSScriptFileInfo.TryTestPSScriptFile(ps1FilePath, out PSScriptFileInfo parsedScript, out ErrorRecord[] errors, out string[] verboseMsgs))
                        {
                            edi = ExceptionDispatchInfo.Capture(new InvalidDataException($"PSScriptFile could not be read properly")); // TODO: how to handle multiple? maybe just write a error of our own
                            return findResponse;
                        }

                        pkgMetadata = parsedScript.ToHashtable();
                        pkgMetadata.Add("Id", pkgFound);
                        pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ScriptFile);
                        pkgTags.AddRange(pkgMetadata["Tags"] as string[]);

                    }
                    else if (File.Exists(nuspecFilePath))
                    {
                        pkgMetadata = GetHashtableForNuspec(nuspecFilePath, out edi);
                        if (edi != null)
                        {
                            return findResponse;
                        }

                        pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.Nuspec);
                        string tagsEntry = pkgMetadata["tags"] as string;
                        pkgTags.AddRange(tagsEntry.Split(new char[] { ' ' }));
                    }
                    else
                    {
                        edi = ExceptionDispatchInfo.Capture(new InvalidDataException($".nupkg package must contain either .psd1, .ps1, or .nuspec file and none were found"));
                        return findResponse;
                    }

                    bool isTagMatch = DeterminePkgTagsSatisfyRequiredTags(pkgTags: pkgTags.ToArray(), requiredTags: tags);
                    if (isTagMatch)
                    {
                        pkgsFound.Add(pkgMetadata);
                    }
                }
                catch (Exception e)
                {
                    edi = ExceptionDispatchInfo.Capture(new InvalidOperationException($"Temporary folder for installation could not be created or set due to: {e.Message}"));
                }
                finally
                {
                    if (Directory.Exists(tempDiscoveryPath))
                    {
                        Utils.DeleteDirectory(tempDiscoveryPath);
                    }
                }
            }

            findResponse = new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: pkgsFound.ToArray(), responseType: localServerFindResponseType);

            return findResponse;
        }

        /// <summary>
        /// Find method which allows for searching for all packages that have specified Command or DSCResource name.
        /// </summary>
        public override FindResults FindCommandOrDscResource(string[] tags, bool includePrerelease, bool isSearchingForCommands, out ExceptionDispatchInfo edi)
        {
            FindResults findResponse = new FindResults();
            List<Hashtable> pkgsFound = new List<Hashtable>();
            edi = null;

            Regex rx = new Regex(@"\.\d+\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Hashtable pkgVersionsFound = new Hashtable(StringComparer.OrdinalIgnoreCase);

            foreach (string path in Directory.GetFiles(repository.Uri.AbsolutePath))
            {
                string packageFullName = Path.GetFileName(path);
                MatchCollection matches = rx.Matches(packageFullName);
                Match match = matches[0];

                GroupCollection groups = match.Groups;
                Capture group = groups[0];

                string pkgName = packageFullName.Substring(0, group.Index);
                string version = packageFullName.Substring(group.Index + 1, packageFullName.LastIndexOf('.') - group.Index - 1);

                NuGetVersion.TryParse(version, out NuGetVersion nugetVersion);

                if (!nugetVersion.IsPrerelease || includePrerelease)
                {
                    if (!pkgVersionsFound.ContainsKey(pkgName))
                    {
                        Hashtable pkgInfo = new Hashtable(StringComparer.OrdinalIgnoreCase);
                        pkgInfo.Add("version", nugetVersion);
                        pkgInfo.Add("path", path);
                        pkgVersionsFound.Add(pkgName, pkgInfo);
                    }
                    else
                    {
                        Hashtable pkgInfo = pkgVersionsFound[pkgName] as Hashtable;
                        NuGetVersion existingVersion = pkgInfo["version"] as NuGetVersion;
                        if (nugetVersion > existingVersion)
                        {
                            pkgInfo["version"] = nugetVersion;
                            pkgInfo["path"] = path;
                            pkgVersionsFound[pkgName] = pkgInfo;
                        }
                    }
                
                }
            }

            List<string> pkgNamesList = pkgVersionsFound.Keys.Cast<string>().ToList();
            foreach(string pkgFound in pkgNamesList)
            {
                Hashtable pkgInfo = pkgVersionsFound[pkgFound] as Hashtable;
                NuGetVersion pkgVersion = pkgInfo["version"] as NuGetVersion;
                string pkgPath = pkgInfo["path"] as string;


                // create temp dir- unique for reach pkg
                var tempDiscoveryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                try
                {
                    var dir = Directory.CreateDirectory(tempDiscoveryPath);
                    dir.Attributes &= ~FileAttributes.ReadOnly;

                    // copy .nupkg
                    string destNupkgPath = Path.Combine(tempDiscoveryPath, Path.GetFileName(pkgPath));
                    File.Copy(pkgPath, destNupkgPath);

                    // change extension to .zip
                    string zipFilePath = Path.ChangeExtension(destNupkgPath, ".zip");
                    File.Move(destNupkgPath, zipFilePath);

                    // extract from .zip
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, tempDiscoveryPath);

                    string psd1FilePath = Path.Combine(tempDiscoveryPath, $"{pkgFound}.psd1");
                    string ps1FilePath = Path.Combine(tempDiscoveryPath, $"{pkgFound}.ps1");
                    string nuspecFilePath = Path.Combine(tempDiscoveryPath, $"{pkgFound}.nuspec");

                    Hashtable pkgMetadata = new Hashtable(StringComparer.InvariantCultureIgnoreCase);
                    List<string> pkgTags = new List<string>();
                    if (File.Exists(psd1FilePath))
                    {
                        if (!Utils.TryReadManifestFile(psd1FilePath, out pkgMetadata, out Exception readManifestError))
                        {
                            edi = ExceptionDispatchInfo.Capture(readManifestError);
                            return findResponse;
                        }

                        GetPrivateDataFromHashtable(pkgMetadata, out string prereleaseLabel, out Uri licenseUri, out Uri projectUri, out Uri iconUri, out string releaseNotes, out string[] pkgHashTags);
                        pkgMetadata.Add("Tags", pkgHashTags);
                        pkgMetadata.Add("Prerelease", prereleaseLabel);
                        pkgMetadata.Add("LicenseUri", licenseUri);
                        pkgMetadata.Add("ProjectUri", projectUri);
                        pkgMetadata.Add("IconUri", iconUri);
                        pkgMetadata.Add("ReleaseNotes", releaseNotes);
                        pkgMetadata.Add("Id", pkgFound);
                        pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ModuleManifest);
                        pkgTags.AddRange(pkgHashTags);
                    }
                    else if (File.Exists(ps1FilePath))
                    {
                        if (!PSScriptFileInfo.TryTestPSScriptFile(ps1FilePath, out PSScriptFileInfo parsedScript, out ErrorRecord[] errors, out string[] verboseMsgs))
                        {
                            edi = ExceptionDispatchInfo.Capture(new InvalidDataException($"PSScriptFile could not be read properly")); // TODO: how to handle multiple? maybe just write a error of our own
                            return findResponse;
                        }

                        pkgMetadata = parsedScript.ToHashtable();
                        pkgMetadata.Add("Id", pkgFound);
                        pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ScriptFile);
                        pkgTags.AddRange(pkgMetadata["Tags"] as string[]);

                    }
                    else
                    {
                        continue;
                    }

                    string[] cmdsOrDSCs = GetCmdsOrDSCTags(tags: tags, isSearchingForCommands: isSearchingForCommands);
                    bool isTagMatch = DeterminePkgTagsSatisfyRequiredTags(pkgTags: pkgTags.ToArray(), requiredTags: cmdsOrDSCs);
                    if (isTagMatch)
                    {
                        pkgsFound.Add(pkgMetadata);
                    }
                }
                catch (Exception e)
                {
                    edi = ExceptionDispatchInfo.Capture(new InvalidOperationException($"Temporary folder for installation could not be created or set due to: {e.Message}"));
                }
                finally
                {
                    if (Directory.Exists(tempDiscoveryPath))
                    {
                        Utils.DeleteDirectory(tempDiscoveryPath);
                    }
                }
            }

            findResponse = new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: pkgsFound.ToArray(), responseType: localServerFindResponseType);

            return findResponse;

            // call into FindAll() which returns string responses for all 
            // look at tags field for each string response
            // just look at psd1 or ps1, not nuspec (not supported error)
            // DSCResourcesToExport, CommandsToExport
        }

        /// <summary>
        /// Find method which allows for searching for single name and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "PowerShellGet"
        /// API call: 
        /// - No prerelease: http://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'
        /// - Include prerelease: http://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'
        /// Implementation Note: Need to filter further for latest version (prerelease or non-prerelease dependening on user preference)
        /// </summary>
        public override FindResults FindName(string packageName, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            FindResults findResponse = new FindResults();
            edi = null;
            WildcardPattern pkgNamePattern = new WildcardPattern($"{packageName}.*", WildcardOptions.IgnoreCase);
            //Hashtable pkgVersionsFound = new Hashtable(StringComparer.OrdinalIgnoreCase);
            NuGetVersion latestVersion = new NuGetVersion("0.0.0.0");
            String latestVersionPath = String.Empty;

            foreach (string path in Directory.GetFiles(repository.Uri.AbsolutePath))
            {
                string packageFullName = Path.GetFileName(path);

                if (!String.IsNullOrEmpty(packageFullName) && pkgNamePattern.IsMatch(packageFullName))
                {
                    string[] packageWithoutName = packageFullName.ToLower().Split(new string[]{ $"{packageName.ToLower()}." }, StringSplitOptions.RemoveEmptyEntries);
                    string packageVersionAndExtension = packageWithoutName[0];
                    int extensionDot = packageVersionAndExtension.LastIndexOf('.');
                    string version = packageVersionAndExtension.Substring(0, extensionDot);
                    NuGetVersion.TryParse(version, out NuGetVersion nugetVersion);

                    if (!nugetVersion.IsPrerelease || includePrerelease)
                    {
                        if (nugetVersion > latestVersion)
                        {
                            latestVersion = nugetVersion;
                            latestVersionPath = path;
                        }
                    }
                }
            }

            if (String.IsNullOrEmpty(latestVersionPath))
            {
                // means no package was found with this name
                edi = ExceptionDispatchInfo.Capture(new LocalResourceNotFoundException($"Package with name {packageName} could not be found in this repository."));
                return findResponse;
            }

            // create temp dir
            var tempDiscoveryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                var dir = Directory.CreateDirectory(tempDiscoveryPath);
                dir.Attributes &= ~FileAttributes.ReadOnly;

                // copy .nupkg
                string destNupkgPath = Path.Combine(tempDiscoveryPath, Path.GetFileName(latestVersionPath));
                File.Copy(latestVersionPath, destNupkgPath);

                // change extension to .zip
                string zipFilePath = Path.ChangeExtension(destNupkgPath, ".zip");
                File.Move(destNupkgPath, zipFilePath);

                // extract from .zip
                System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, tempDiscoveryPath);

                string psd1FilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.psd1");
                string ps1FilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.ps1");
                string nuspecFilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.nuspec");

                Hashtable pkgMetadata = new Hashtable(StringComparer.InvariantCultureIgnoreCase);

                if (File.Exists(psd1FilePath))
                {
                    if (!Utils.TryReadManifestFile(psd1FilePath, out pkgMetadata, out Exception readManifestError))
                    {
                        edi = ExceptionDispatchInfo.Capture(readManifestError);
                        return findResponse;
                    }

                    GetPrivateDataFromHashtable(pkgMetadata, out string prereleaseLabel, out Uri licenseUri, out Uri projectUri, out Uri iconUri, out string releaseNotes, out string[] pkgHashTags);
                    pkgMetadata.Add("Tags", pkgHashTags);
                    pkgMetadata.Add("Prerelease", prereleaseLabel);
                    pkgMetadata.Add("LicenseUri", licenseUri);
                    pkgMetadata.Add("ProjectUri", projectUri);
                    pkgMetadata.Add("IconUri", iconUri);
                    pkgMetadata.Add("ReleaseNotes", releaseNotes);
                    pkgMetadata.Add("Id", packageName);
                    pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ModuleManifest);
                }
                else if (File.Exists(ps1FilePath))
                {
                    if (!PSScriptFileInfo.TryTestPSScriptFile(ps1FilePath, out PSScriptFileInfo parsedScript, out ErrorRecord[] errors, out string[] verboseMsgs))
                    {
                        edi = ExceptionDispatchInfo.Capture(new InvalidDataException($"PSScriptFile could not be read properly")); // TODO: how to handle multiple? maybe just write a error of our own
                        return findResponse;
                    }

                    pkgMetadata = parsedScript.ToHashtable();
                    pkgMetadata.Add("Id", packageName);
                    pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ScriptFile);

                }
                else if (File.Exists(nuspecFilePath))
                {
                    pkgMetadata = GetHashtableForNuspec(nuspecFilePath, out edi);
                    if (edi != null)
                    {
                        return findResponse;
                    }

                    pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.Nuspec);
                }
                else
                {
                    edi = ExceptionDispatchInfo.Capture(new InvalidDataException($".nupkg package must contain either .psd1, .ps1, or .nuspec file and none were found")); // TODO: how to handle multiple? maybe just write a error of our own
                    return findResponse;
                }

                findResponse = new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: new Hashtable[]{pkgMetadata}, responseType: localServerFindResponseType);
            }
            catch (Exception e)
            {
               edi = ExceptionDispatchInfo.Capture(new InvalidOperationException($"Temporary folder for installation could not be created or set due to: {e.Message}"));
            }
            finally
            {
                if (Directory.Exists(tempDiscoveryPath))
                {
                    Utils.DeleteDirectory(tempDiscoveryPath);
                }
            }

            return findResponse;
        }

        /// <summary>
        /// Find method which allows for searching for single name and tag and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "PowerShellGet" -Tag "Provider"
        /// Implementation Note: Need to filter further for latest version (prerelease or non-prerelease dependening on user preference)
        /// </summary>
        public override FindResults FindNameWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            FindResults findResponse = new FindResults();
            edi = null;
            WildcardPattern pkgNamePattern = new WildcardPattern($"{packageName}.*", WildcardOptions.IgnoreCase);
            NuGetVersion latestVersion = new NuGetVersion("0.0.0.0");
            String latestVersionPath = String.Empty;

            foreach (string path in Directory.GetFiles(repository.Uri.AbsolutePath))
            {
                string packageFullName = Path.GetFileName(path);

                if (!String.IsNullOrEmpty(packageFullName) && pkgNamePattern.IsMatch(packageFullName))
                {
                    string[] packageWithoutName = packageFullName.ToLower().Split(new string[]{ $"{packageName.ToLower()}." }, StringSplitOptions.RemoveEmptyEntries);
                    string packageVersionAndExtension = packageWithoutName[0];
                    int extensionDot = packageVersionAndExtension.LastIndexOf('.');
                    string version = packageVersionAndExtension.Substring(0, extensionDot);
                    NuGetVersion.TryParse(version, out NuGetVersion nugetVersion);

                    if (!nugetVersion.IsPrerelease || includePrerelease)
                    {
                        if (nugetVersion > latestVersion)
                        {
                            latestVersion = nugetVersion;
                            latestVersionPath = path;
                        }
                    }
                }
            }

            if (String.IsNullOrEmpty(latestVersionPath))
            {
                // means no package was found with this name
                edi = ExceptionDispatchInfo.Capture(new LocalResourceNotFoundException($"Package with name {packageName} could not be found in this repository."));
                return findResponse;
            }

            // create temp dir
            var tempDiscoveryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                var dir = Directory.CreateDirectory(tempDiscoveryPath);
                dir.Attributes &= ~FileAttributes.ReadOnly;

                // copy .nupkg
                string destNupkgPath = Path.Combine(tempDiscoveryPath, Path.GetFileName(latestVersionPath));
                File.Copy(latestVersionPath, destNupkgPath);

                // change extension to .zip
                string zipFilePath = Path.ChangeExtension(destNupkgPath, ".zip");
                File.Move(destNupkgPath, zipFilePath);

                // extract from .zip
                System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, tempDiscoveryPath);

                string psd1FilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.psd1");
                string ps1FilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.ps1");
                string nuspecFilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.nuspec");

                Hashtable pkgMetadata = new Hashtable(StringComparer.InvariantCultureIgnoreCase);
                List<string> pkgTags = new List<string>();

                if (File.Exists(psd1FilePath))
                {
                    if (!Utils.TryReadManifestFile(psd1FilePath, out pkgMetadata, out Exception readManifestError))
                    {
                        edi = ExceptionDispatchInfo.Capture(readManifestError);
                        return findResponse;
                    }

                    // parse out PSData > add directly as keys to the hashtable
                    GetPrivateDataFromHashtable(pkgMetadata, out string prereleaseLabel, out Uri licenseUri, out Uri projectUri, out Uri iconUri, out string releaseNotes, out string[] pkgHashTags);
                    pkgMetadata.Add("Tags", pkgHashTags);
                    pkgMetadata.Add("Prerelease", prereleaseLabel);
                    pkgMetadata.Add("LicenseUri", licenseUri);
                    pkgMetadata.Add("ProjectUri", projectUri);
                    pkgMetadata.Add("IconUri", iconUri);
                    pkgMetadata.Add("ReleaseNotes", releaseNotes);
                    pkgMetadata.Add("Id", packageName);
                    pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ModuleManifest);

                    pkgTags.AddRange(pkgHashTags);
                }
                else if (File.Exists(ps1FilePath))
                {
                    if (!PSScriptFileInfo.TryTestPSScriptFile(ps1FilePath, out PSScriptFileInfo parsedScript, out ErrorRecord[] errors, out string[] verboseMsgs))
                    {
                        edi = ExceptionDispatchInfo.Capture(new InvalidDataException($"PSScriptFile could not be read properly")); // TODO: how to handle multiple? maybe just write a error of our own
                        return findResponse;
                    }

                    pkgMetadata = parsedScript.ToHashtable();
                    pkgMetadata.Add("Id", packageName);
                    pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ScriptFile);
                    pkgTags.AddRange(pkgMetadata["Tags"] as string[]);

                }
                else if (File.Exists(nuspecFilePath))
                {
                    pkgMetadata = GetHashtableForNuspec(nuspecFilePath, out edi);
                    if (edi != null)
                    {
                        return findResponse;
                    }

                    pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.Nuspec);
                    pkgTags.AddRange(pkgMetadata["tags"] as string[]);
                }
                else
                {
                    edi = ExceptionDispatchInfo.Capture(new InvalidDataException($".nupkg package must contain either .psd1, .ps1, or .nuspec file and none were found"));
                    return findResponse;
                }

                bool isTagMatch = DeterminePkgTagsSatisfyRequiredTags(pkgTags: pkgTags.ToArray(), requiredTags: tags);
                if (isTagMatch)
                {
                    findResponse = new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: new Hashtable[]{pkgMetadata}, responseType: localServerFindResponseType);
                }
                else
                {
                    edi = ExceptionDispatchInfo.Capture(new SpecifiedTagsNotFoundException($"Package with name {packageName} and tags {String.Join(", ", tags)} could not be found."));
                }
            }
            catch (Exception e)
            {
               edi = ExceptionDispatchInfo.Capture(new InvalidOperationException($"Temporary folder for installation could not be created or set due to: {e.Message}"));
            }
            finally
            {
                if (Directory.Exists(tempDiscoveryPath))
                {
                    Utils.DeleteDirectory(tempDiscoveryPath);
                }
            }

            return findResponse;
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "PowerShell*"
        /// API call: 
        /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='az*'
        /// Implementation Note: filter additionally and verify ONLY package name was a match.
        /// </summary>
        public override FindResults FindNameGlobbing(string packageName, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            FindResults findResponse = new FindResults();
            List<Hashtable> pkgsFound = new List<Hashtable>();
            edi = null;

            // wildcard name possibilities: power*, *get, power*get
            WildcardPattern pkgNamePattern = new WildcardPattern($"{packageName}", WildcardOptions.IgnoreCase);

            Regex rx = new Regex(@"\.\d+\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Hashtable pkgVersionsFound = new Hashtable(StringComparer.OrdinalIgnoreCase);

            foreach (string path in Directory.GetFiles(repository.Uri.AbsolutePath))
            {
                string packageFullName = Path.GetFileName(path);
                MatchCollection matches = rx.Matches(packageFullName);
                Match match = matches[0];

                GroupCollection groups = match.Groups;
                Capture group = groups[0];

                string pkgFoundName = packageFullName.Substring(0, group.Index);

                if (pkgNamePattern.IsMatch(pkgFoundName))
                {
                    string version = packageFullName.Substring(group.Index + 1, packageFullName.LastIndexOf('.') - group.Index - 1);

                    NuGetVersion.TryParse(version, out NuGetVersion nugetVersion);

                    if (!nugetVersion.IsPrerelease || includePrerelease)
                    {
                        if (!pkgVersionsFound.ContainsKey(pkgFoundName))
                        {
                            Hashtable pkgInfo = new Hashtable(StringComparer.OrdinalIgnoreCase);
                            pkgInfo.Add("version", nugetVersion);
                            pkgInfo.Add("path", path);
                            pkgVersionsFound.Add(pkgFoundName, pkgInfo);
                        }
                        else
                        {
                            Hashtable pkgInfo = pkgVersionsFound[pkgFoundName] as Hashtable;
                            NuGetVersion existingVersion = pkgInfo["version"] as NuGetVersion;
                            if (nugetVersion > existingVersion)
                            {
                                pkgInfo["version"] = nugetVersion;
                                pkgInfo["path"] = path;
                                pkgVersionsFound[pkgFoundName] = pkgInfo;
                            }
                        }
                    }
                }
            }

            List<string> pkgNamesList = pkgVersionsFound.Keys.Cast<string>().ToList();
            foreach(string pkgFound in pkgNamesList)
            {
                Hashtable pkgInfo = pkgVersionsFound[pkgFound] as Hashtable;
                NuGetVersion pkgVersion = pkgInfo["version"] as NuGetVersion;
                string pkgPath = pkgInfo["path"] as string;


                // create temp dir- unique for reach pkg
                var tempDiscoveryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                try
                {
                    var dir = Directory.CreateDirectory(tempDiscoveryPath);
                    dir.Attributes &= ~FileAttributes.ReadOnly;

                    // copy .nupkg
                    string destNupkgPath = Path.Combine(tempDiscoveryPath, Path.GetFileName(pkgPath));
                    File.Copy(pkgPath, destNupkgPath);

                    // change extension to .zip
                    string zipFilePath = Path.ChangeExtension(destNupkgPath, ".zip");
                    File.Move(destNupkgPath, zipFilePath);

                    // extract from .zip
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, tempDiscoveryPath);

                    string psd1FilePath = Path.Combine(tempDiscoveryPath, $"{pkgFound}.psd1");
                    string ps1FilePath = Path.Combine(tempDiscoveryPath, $"{pkgFound}.ps1");
                    string nuspecFilePath = Path.Combine(tempDiscoveryPath, $"{pkgFound}.nuspec");

                    Hashtable pkgMetadata = new Hashtable(StringComparer.InvariantCultureIgnoreCase);

                    if (File.Exists(psd1FilePath))
                    {
                        if (!Utils.TryReadManifestFile(psd1FilePath, out pkgMetadata, out Exception readManifestError))
                        {
                            edi = ExceptionDispatchInfo.Capture(readManifestError);
                            return findResponse;
                        }

                        GetPrivateDataFromHashtable(pkgMetadata, out string prereleaseLabel, out Uri licenseUri, out Uri projectUri, out Uri iconUri, out string releaseNotes, out string[] pkgHashTags);
                        pkgMetadata.Add("Tags", pkgHashTags);
                        pkgMetadata.Add("Prerelease", prereleaseLabel);
                        pkgMetadata.Add("LicenseUri", licenseUri);
                        pkgMetadata.Add("ProjectUri", projectUri);
                        pkgMetadata.Add("IconUri", iconUri);
                        pkgMetadata.Add("ReleaseNotes", releaseNotes);
                        pkgMetadata.Add("Id", pkgFound);
                        pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ModuleManifest);
                    }
                    else if (File.Exists(ps1FilePath))
                    {
                        if (!PSScriptFileInfo.TryTestPSScriptFile(ps1FilePath, out PSScriptFileInfo parsedScript, out ErrorRecord[] errors, out string[] verboseMsgs))
                        {
                            edi = ExceptionDispatchInfo.Capture(new InvalidDataException($"PSScriptFile could not be read properly")); // TODO: how to handle multiple? maybe just write a error of our own
                            return findResponse;
                        }

                        pkgMetadata = parsedScript.ToHashtable();
                        pkgMetadata.Add("Id", pkgFound);
                        pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ScriptFile);

                    }
                    else if (File.Exists(nuspecFilePath))
                    {
                        pkgMetadata = GetHashtableForNuspec(nuspecFilePath, out edi);
                        if (edi != null)
                        {
                            return findResponse;
                        }

                        pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.Nuspec);
                    }
                    else
                    {
                        edi = ExceptionDispatchInfo.Capture(new InvalidDataException($".nupkg package must contain either .psd1, .ps1, or .nuspec file and none were found"));
                        return findResponse;
                    }

                    pkgsFound.Add(pkgMetadata);
                }
                catch (Exception e)
                {
                    edi = ExceptionDispatchInfo.Capture(new InvalidOperationException($"Temporary folder for installation could not be created or set due to: {e.Message}"));
                }
                finally
                {
                    if (Directory.Exists(tempDiscoveryPath))
                    {
                        Utils.DeleteDirectory(tempDiscoveryPath);
                    }
                }
            }

            findResponse = new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: pkgsFound.ToArray(), responseType: localServerFindResponseType);

            return findResponse;
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and tag and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "PowerShell*" -Tag "Provider"
        /// Implementation Note: filter additionally and verify ONLY package name was a match.
        /// </summary>
        public override FindResults FindNameGlobbingWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            FindResults findResponse = new FindResults();
            List<Hashtable> pkgsFound = new List<Hashtable>();
            edi = null;

            // wildcard name possibilities: power*, *get, power*get
            WildcardPattern pkgNamePattern = new WildcardPattern($"{packageName}", WildcardOptions.IgnoreCase);

            Regex rx = new Regex(@"\.\d+\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Hashtable pkgVersionsFound = new Hashtable(StringComparer.OrdinalIgnoreCase);

            foreach (string path in Directory.GetFiles(repository.Uri.AbsolutePath))
            {
                string packageFullName = Path.GetFileName(path);
                MatchCollection matches = rx.Matches(packageFullName);
                Match match = matches[0];

                GroupCollection groups = match.Groups;
                Capture group = groups[0];

                string pkgFoundName = packageFullName.Substring(0, group.Index);

                if (pkgNamePattern.IsMatch(pkgFoundName))
                {
                    string version = packageFullName.Substring(group.Index + 1, packageFullName.LastIndexOf('.') - group.Index - 1);

                    NuGetVersion.TryParse(version, out NuGetVersion nugetVersion);

                    if (!nugetVersion.IsPrerelease || includePrerelease)
                    {
                        if (!pkgVersionsFound.ContainsKey(pkgFoundName))
                        {
                            Hashtable pkgInfo = new Hashtable(StringComparer.OrdinalIgnoreCase);
                            pkgInfo.Add("version", nugetVersion);
                            pkgInfo.Add("path", path);
                            pkgVersionsFound.Add(pkgFoundName, pkgInfo);
                        }
                        else
                        {
                            Hashtable pkgInfo = pkgVersionsFound[pkgFoundName] as Hashtable;
                            NuGetVersion existingVersion = pkgInfo["version"] as NuGetVersion;
                            if (nugetVersion > existingVersion)
                            {
                                pkgInfo["version"] = nugetVersion;
                                pkgInfo["path"] = path;
                                pkgVersionsFound[pkgFoundName] = pkgInfo;
                            }
                        }
                    }
                }
            }

            List<string> pkgNamesList = pkgVersionsFound.Keys.Cast<string>().ToList();
            foreach(string pkgFound in pkgNamesList)
            {
                Hashtable pkgInfo = pkgVersionsFound[pkgFound] as Hashtable;
                NuGetVersion pkgVersion = pkgInfo["version"] as NuGetVersion;
                string pkgPath = pkgInfo["path"] as string;


                // create temp dir- unique for reach pkg
                var tempDiscoveryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                try
                {
                    var dir = Directory.CreateDirectory(tempDiscoveryPath);
                    dir.Attributes &= ~FileAttributes.ReadOnly;

                    // copy .nupkg
                    string destNupkgPath = Path.Combine(tempDiscoveryPath, Path.GetFileName(pkgPath));
                    File.Copy(pkgPath, destNupkgPath);

                    // change extension to .zip
                    string zipFilePath = Path.ChangeExtension(destNupkgPath, ".zip");
                    File.Move(destNupkgPath, zipFilePath);

                    // extract from .zip
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, tempDiscoveryPath);

                    string psd1FilePath = Path.Combine(tempDiscoveryPath, $"{pkgFound}.psd1");
                    string ps1FilePath = Path.Combine(tempDiscoveryPath, $"{pkgFound}.ps1");
                    string nuspecFilePath = Path.Combine(tempDiscoveryPath, $"{pkgFound}.nuspec");

                    Hashtable pkgMetadata = new Hashtable(StringComparer.InvariantCultureIgnoreCase);
                    List<string> pkgTags = new List<string>();

                    if (File.Exists(psd1FilePath))
                    {
                        if (!Utils.TryReadManifestFile(psd1FilePath, out pkgMetadata, out Exception readManifestError))
                        {
                            edi = ExceptionDispatchInfo.Capture(readManifestError);
                            return findResponse;
                        }

                        GetPrivateDataFromHashtable(pkgMetadata, out string prereleaseLabel, out Uri licenseUri, out Uri projectUri, out Uri iconUri, out string releaseNotes, out string[] pkgHashTags);
                        pkgMetadata.Add("Tags", pkgHashTags);
                        pkgMetadata.Add("Prerelease", prereleaseLabel);
                        pkgMetadata.Add("LicenseUri", licenseUri);
                        pkgMetadata.Add("ProjectUri", projectUri);
                        pkgMetadata.Add("IconUri", iconUri);
                        pkgMetadata.Add("ReleaseNotes", releaseNotes);
                        pkgMetadata.Add("Id", pkgFound);
                        pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ModuleManifest);
                        pkgTags.AddRange(pkgHashTags);
                    }
                    else if (File.Exists(ps1FilePath))
                    {
                        if (!PSScriptFileInfo.TryTestPSScriptFile(ps1FilePath, out PSScriptFileInfo parsedScript, out ErrorRecord[] errors, out string[] verboseMsgs))
                        {
                            edi = ExceptionDispatchInfo.Capture(new InvalidDataException($"PSScriptFile could not be read properly")); // TODO: how to handle multiple? maybe just write a error of our own
                            return findResponse;
                        }

                        pkgMetadata = parsedScript.ToHashtable();
                        pkgMetadata.Add("Id", pkgFound);
                        pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ScriptFile);
                        pkgTags.AddRange(pkgMetadata["Tags"] as string[]);

                    }
                    else if (File.Exists(nuspecFilePath))
                    {
                        pkgMetadata = GetHashtableForNuspec(nuspecFilePath, out edi);
                        if (edi != null)
                        {
                            return findResponse;
                        }

                        pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.Nuspec);
                        pkgTags.AddRange(pkgMetadata["tags"] as string[]);
                    }
                    else
                    {
                        edi = ExceptionDispatchInfo.Capture(new InvalidDataException($".nupkg package must contain either .psd1, .ps1, or .nuspec file and none were found"));
                        return findResponse;
                    }

                    bool isTagMatch = DeterminePkgTagsSatisfyRequiredTags(pkgTags: pkgTags.ToArray(), requiredTags: tags);
                    if (isTagMatch)
                    {
                        pkgsFound.Add(pkgMetadata);
                    }
                }
                catch (Exception e)
                {
                    edi = ExceptionDispatchInfo.Capture(new InvalidOperationException($"Temporary folder for installation could not be created or set due to: {e.Message}"));
                }
                finally
                {
                    if (Directory.Exists(tempDiscoveryPath))
                    {
                        Utils.DeleteDirectory(tempDiscoveryPath);
                    }
                }
            }

            findResponse = new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: pkgsFound.ToArray(), responseType: localServerFindResponseType);

            return findResponse;
        }

        /// <summary>
        /// Find method which allows for searching for single name with version range.
        /// Name: no wildcard support
        /// Version: supports wildcards
        /// Examples: Search "PowerShellGet" "[3.0.0.0, 5.0.0.0]"
        ///           Search "PowerShellGet" "3.*"
        /// API Call: http://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'
        /// Implementation note: Returns all versions, including prerelease ones. Later (in the API client side) we'll do filtering on the versions to satisfy what user provided.
        /// </summary>
        public override FindResults FindVersionGlobbing(string packageName, VersionRange versionRange, bool includePrerelease, ResourceType type, bool getOnlyLatest, out ExceptionDispatchInfo edi)
        {
            FindResults findResponse = new FindResults();
            edi = null;

            WildcardPattern pkgNamePattern = new WildcardPattern($"{packageName}.*", WildcardOptions.IgnoreCase);
            Hashtable pkgVersionsFound = new Hashtable(StringComparer.OrdinalIgnoreCase);

            foreach (string path in Directory.GetFiles(repository.Uri.AbsolutePath))
            {
                string packageFullName = Path.GetFileName(path);

                if (!String.IsNullOrEmpty(packageFullName) && pkgNamePattern.IsMatch(packageFullName))
                {
                    string[] packageWithoutName = packageFullName.ToLower().Split(new string[]{ $"{packageName.ToLower()}." }, StringSplitOptions.RemoveEmptyEntries);
                    string packageVersionAndExtension = packageWithoutName[0];
                    int extensionDot = packageVersionAndExtension.LastIndexOf('.');
                    string version = packageVersionAndExtension.Substring(0, extensionDot);
                    NuGetVersion.TryParse(version, out NuGetVersion nugetVersion);

                    if ((!nugetVersion.IsPrerelease || includePrerelease) && (versionRange.Satisfies(nugetVersion)))
                    {
                        if (!pkgVersionsFound.ContainsKey(nugetVersion))
                        {
                            pkgVersionsFound.Add(nugetVersion, path);
                        }
                    }
                }
            }

            List<NuGetVersion> pkgVersionsList = pkgVersionsFound.Keys.Cast<NuGetVersion>().ToList();
            pkgVersionsList.Sort();
            List<Hashtable> foundPkgs = new List<Hashtable>();
            for (int i = pkgVersionsList.Count - 1; i >=0; i--)
            {
                // Versions are present in pkgVersionsList in asc order, wherease we need it in desc so we traverse it in reverse.
                NuGetVersion satisfyingVersion = pkgVersionsList[i];

                string packagePath = (string) pkgVersionsFound[satisfyingVersion];
                
                // create temp dir
                var tempDiscoveryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

                try
                {
                    var dir = Directory.CreateDirectory(tempDiscoveryPath);
                    dir.Attributes &= ~FileAttributes.ReadOnly;

                    // copy .nupkg
                    string destNupkgPath = Path.Combine(tempDiscoveryPath, Path.GetFileName(packagePath));
                    File.Copy(packagePath, destNupkgPath);

                    // change extension to .zip
                    string zipFilePath = Path.ChangeExtension(destNupkgPath, ".zip");
                    File.Move(destNupkgPath, zipFilePath);

                    // extract from .zip
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, tempDiscoveryPath);

                    string psd1FilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.psd1");
                    string ps1FilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.ps1");
                    string nuspecFilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.nuspec");

                    Hashtable pkgMetadata = new Hashtable(StringComparer.InvariantCultureIgnoreCase);

                    if (File.Exists(psd1FilePath))
                    {
                        if (!Utils.TryReadManifestFile(psd1FilePath, out pkgMetadata, out Exception readManifestError))
                        {
                            edi = ExceptionDispatchInfo.Capture(readManifestError);
                            return findResponse;
                        }

                        GetPrivateDataFromHashtable(pkgMetadata, out string prereleaseLabel, out Uri licenseUri, out Uri projectUri, out Uri iconUri, out string releaseNotes, out string[] pkgHashTags);
                        pkgMetadata.Add("Tags", pkgHashTags);
                        pkgMetadata.Add("Prerelease", prereleaseLabel);
                        pkgMetadata.Add("LicenseUri", licenseUri);
                        pkgMetadata.Add("ProjectUri", projectUri);
                        pkgMetadata.Add("IconUri", iconUri);
                        pkgMetadata.Add("ReleaseNotes", releaseNotes);
                        pkgMetadata.Add("Id", packageName);
                        pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ModuleManifest);
                    }
                    else if (File.Exists(ps1FilePath))
                    {
                        if (!PSScriptFileInfo.TryTestPSScriptFile(ps1FilePath, out PSScriptFileInfo parsedScript, out ErrorRecord[] errors, out string[] verboseMsgs))
                        {
                            edi = ExceptionDispatchInfo.Capture(new InvalidDataException($"PSScriptFile could not be read properly")); // TODO: how to handle multiple? maybe just write a error of our own
                            return findResponse;
                        }

                        pkgMetadata = parsedScript.ToHashtable();
                        pkgMetadata.Add("Id", packageName);
                        pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ScriptFile);

                    }
                    else if (File.Exists(nuspecFilePath))
                    {
                        pkgMetadata = GetHashtableForNuspec(nuspecFilePath, out edi);
                        if (edi != null)
                        {
                            return findResponse;
                        }

                        pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.Nuspec);
                    }
                    else
                    {
                        edi = ExceptionDispatchInfo.Capture(new InvalidDataException($".nupkg package must contain either .psd1, .ps1, or .nuspec file and none were found"));
                        return findResponse;
                    }

                    foundPkgs.Add(pkgMetadata);
                }
                catch (Exception e)
                {
                    edi = ExceptionDispatchInfo.Capture(new InvalidOperationException($"Temporary folder for installation could not be created or set due to: {e.Message}"));
                }
                finally
                {
                    if (Directory.Exists(tempDiscoveryPath))
                    {
                        Utils.DeleteDirectory(tempDiscoveryPath);
                    }
                }
            }

            findResponse = new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: foundPkgs.ToArray(), responseType: localServerFindResponseType);

            return findResponse;
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5"
        /// API call: http://www.powershellgallery.com/api/v2/Packages(Id='PowerShellGet', Version='2.2.5')
        /// </summary>
        public override FindResults FindVersion(string packageName, string version, ResourceType type, out ExceptionDispatchInfo edi) 
        {
            FindResults findResponse = new FindResults();
            edi = null;

            string packageFullName = $"{packageName}.{version}.nupkg";
            string packagePath = Path.Combine(repository.Uri.AbsolutePath, packageFullName);

            if (!File.Exists(packagePath))
            {
                edi = ExceptionDispatchInfo.Capture(new LocalResourceNotFoundException($"Package with specified criteria: Name {packageName} and version {version} does not exist in this repository"));
                return findResponse;
            }

            // create temp dir
            var tempDiscoveryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                var dir = Directory.CreateDirectory(tempDiscoveryPath);
                dir.Attributes &= ~FileAttributes.ReadOnly;

                // copy .nupkg
                string destNupkgPath = Path.Combine(tempDiscoveryPath, packageFullName);
                File.Copy(packagePath, destNupkgPath);

                // change extension to .zip
                string zipFilePath = Path.ChangeExtension(destNupkgPath, ".zip");
                File.Move(destNupkgPath, zipFilePath);

                // extract from .zip
                System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, tempDiscoveryPath);

                string psd1FilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.psd1");
                string ps1FilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.ps1");
                string nuspecFilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.nuspec");

                Hashtable pkgMetadata = new Hashtable(StringComparer.InvariantCultureIgnoreCase);

                if (File.Exists(psd1FilePath))
                {
                    if (!Utils.TryReadManifestFile(psd1FilePath, out pkgMetadata, out Exception readManifestError))
                    {
                        edi = ExceptionDispatchInfo.Capture(readManifestError);
                        return findResponse;
                    }

                    GetPrivateDataFromHashtable(pkgMetadata, out string prereleaseLabel, out Uri licenseUri, out Uri projectUri, out Uri iconUri, out string releaseNotes, out string[] pkgHashTags);
                    pkgMetadata.Add("Tags", pkgHashTags);
                    pkgMetadata.Add("Prerelease", prereleaseLabel);
                    pkgMetadata.Add("LicenseUri", licenseUri);
                    pkgMetadata.Add("ProjectUri", projectUri);
                    pkgMetadata.Add("IconUri", iconUri);
                    pkgMetadata.Add("ReleaseNotes", releaseNotes);
                    pkgMetadata.Add("Id", packageName);
                    pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ModuleManifest);
                }
                else if (File.Exists(ps1FilePath))
                {
                    if (!PSScriptFileInfo.TryTestPSScriptFile(ps1FilePath, out PSScriptFileInfo parsedScript, out ErrorRecord[] errors, out string[] verboseMsgs))
                    {
                        edi = ExceptionDispatchInfo.Capture(new InvalidDataException($"PSScriptFile could not be read properly")); // TODO: how to handle multiple? maybe just write a error of our own
                        return findResponse;
                    }

                    pkgMetadata = parsedScript.ToHashtable();
                    pkgMetadata.Add("Id", packageName);
                    pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ScriptFile);

                }
                else if (File.Exists(nuspecFilePath))
                {
                    pkgMetadata = GetHashtableForNuspec(nuspecFilePath, out edi);
                    if (edi != null)
                    {
                        return findResponse;
                    }

                    pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.Nuspec);
                }
                else
                {
                    edi = ExceptionDispatchInfo.Capture(new InvalidDataException($".nupkg package must contain either .psd1, .ps1, or .nuspec file and none were found"));
                    return findResponse;
                }

                findResponse = new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: new Hashtable[]{pkgMetadata}, responseType: localServerFindResponseType);
            }
            catch (Exception e)
            {
               edi = ExceptionDispatchInfo.Capture(new InvalidOperationException($"Temporary folder for installation could not be created or set due to: {e.Message}"));
            }
            finally
            {
                if (Directory.Exists(tempDiscoveryPath))
                {
                    Utils.DeleteDirectory(tempDiscoveryPath);
                }
            }

            return findResponse;
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version and tag.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5" -Tag "Provider"
        /// </summary>
        public override FindResults FindVersionWithTag(string packageName, string version, string[] tags, ResourceType type, out ExceptionDispatchInfo edi)
        {
            FindResults findResponse = new FindResults();
            edi = null;

            string packageFullName = $"{packageName}.{version}.nupkg";
            string packagePath = Path.Combine(repository.Uri.AbsolutePath, packageFullName);

            if (!File.Exists(packagePath))
            {
                edi = ExceptionDispatchInfo.Capture(new LocalResourceNotFoundException($"Package with specified criteria: Name {packageName} and version {version} does not exist in this repository"));
                return findResponse;
            }

            // create temp dir
            var tempDiscoveryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                var dir = Directory.CreateDirectory(tempDiscoveryPath);
                dir.Attributes &= ~FileAttributes.ReadOnly;

                // copy .nupkg
                string destNupkgPath = Path.Combine(tempDiscoveryPath, packageFullName);
                File.Copy(packagePath, destNupkgPath);

                // change extension to .zip
                string zipFilePath = Path.ChangeExtension(destNupkgPath, ".zip");
                File.Move(destNupkgPath, zipFilePath);

                // extract from .zip
                System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, tempDiscoveryPath);

                string psd1FilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.psd1");
                string ps1FilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.ps1");
                string nuspecFilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.nuspec");

                Hashtable pkgMetadata = new Hashtable(StringComparer.InvariantCultureIgnoreCase);
                List<string> pkgTags = new List<string>();

                if (File.Exists(psd1FilePath))
                {
                    if (!Utils.TryReadManifestFile(psd1FilePath, out pkgMetadata, out Exception readManifestError))
                    {
                        edi = ExceptionDispatchInfo.Capture(readManifestError);
                        return findResponse;
                    }

                    GetPrivateDataFromHashtable(pkgMetadata, out string prereleaseLabel, out Uri licenseUri, out Uri projectUri, out Uri iconUri, out string releaseNotes, out string[] pkgHashTags);
                    pkgMetadata.Add("Tags", pkgHashTags);
                    pkgMetadata.Add("Prerelease", prereleaseLabel);
                    pkgMetadata.Add("LicenseUri", licenseUri);
                    pkgMetadata.Add("ProjectUri", projectUri);
                    pkgMetadata.Add("IconUri", iconUri);
                    pkgMetadata.Add("ReleaseNotes", releaseNotes);
                    pkgMetadata.Add("Id", packageName);
                    pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ModuleManifest);

                    pkgTags.AddRange(pkgHashTags);
                }
                else if (File.Exists(ps1FilePath))
                {
                    if (!PSScriptFileInfo.TryTestPSScriptFile(ps1FilePath, out PSScriptFileInfo parsedScript, out ErrorRecord[] errors, out string[] verboseMsgs))
                    {
                        edi = ExceptionDispatchInfo.Capture(new InvalidDataException($"PSScriptFile could not be read properly")); // TODO: how to handle multiple? maybe just write a error of our own
                        return findResponse;
                    }

                    pkgMetadata = parsedScript.ToHashtable();
                    pkgMetadata.Add("Id", packageName);
                    pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.ScriptFile);
                    pkgTags.AddRange(pkgMetadata["Tags"] as string[]);

                }
                else if (File.Exists(nuspecFilePath))
                {
                    pkgMetadata = GetHashtableForNuspec(nuspecFilePath, out edi);
                    if (edi != null)
                    {
                        return findResponse;
                    }

                    pkgMetadata.Add(fileTypeKey, Utils.MetadataFileType.Nuspec);
                    pkgTags.AddRange(pkgMetadata["tags"] as string[]);
                }
                else
                {
                    edi = ExceptionDispatchInfo.Capture(new InvalidDataException($".nupkg package must contain either .psd1, .ps1, or .nuspec file and none were found"));
                    return findResponse;
                }

                bool isTagMatch = DeterminePkgTagsSatisfyRequiredTags(pkgTags: pkgTags.ToArray(), requiredTags: tags);
                if (isTagMatch)
                {
                    findResponse = new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: new Hashtable[]{pkgMetadata}, responseType: localServerFindResponseType);
                }
                else
                {
                    edi = ExceptionDispatchInfo.Capture(new SpecifiedTagsNotFoundException($"Package with name {packageName}, version {version} and tags {String.Join(", ", tags)} could not be found."));
                }
            }
            catch (Exception e)
            {
               edi = ExceptionDispatchInfo.Capture(new InvalidOperationException($"Temporary folder for installation could not be created or set due to: {e.Message}"));
            }
            finally
            {
                if (Directory.Exists(tempDiscoveryPath))
                {
                    Utils.DeleteDirectory(tempDiscoveryPath);
                }
            }

            return findResponse;
        }


        /**  INSTALL APIS **/

        /// <summary>
        /// Installs specific package.
        /// Name: no wildcard support.
        /// Examples: Install "PowerShellGet"
        /// Implementation Note:   if not prerelease: https://www.powershellgallery.com/api/v2/package/powershellget (Returns latest stable)
        ///                        if prerelease, call into InstallVersion instead. 
        /// </summary>
        public override Stream InstallName(string packageName, bool includePrerelease, out ExceptionDispatchInfo edi)
        {
            FileStream fs = null;
            edi = null;
            WildcardPattern pkgNamePattern = new WildcardPattern($"{packageName}.*", WildcardOptions.IgnoreCase);
            NuGetVersion latestVersion = new NuGetVersion("0.0.0.0");
            String latestVersionPath = String.Empty;

            foreach (string path in Directory.GetFiles(repository.Uri.AbsolutePath))
            {
                string packageFullName = Path.GetFileName(path);

                if (!String.IsNullOrEmpty(packageFullName) && pkgNamePattern.IsMatch(packageFullName))
                {
                    string[] packageWithoutName = packageFullName.ToLower().Split(new string[]{ $"{packageName.ToLower()}." }, StringSplitOptions.RemoveEmptyEntries);
                    string packageVersionAndExtension = packageWithoutName[0];
                    int extensionDot = packageVersionAndExtension.LastIndexOf('.');
                    string version = packageVersionAndExtension.Substring(0, extensionDot);
                    NuGetVersion.TryParse(version, out NuGetVersion nugetVersion);

                    if (!nugetVersion.IsPrerelease || includePrerelease)
                    {
                        if (nugetVersion > latestVersion)
                        {
                            latestVersion = nugetVersion;
                            latestVersionPath = path;
                        }
                    }
                }
            }

            if (String.IsNullOrEmpty(latestVersionPath))
            {
                edi = ExceptionDispatchInfo.Capture(new LocalResourceEmpty($"Package for name {packageName} was not present in repository"));
            }
            else
            {
                fs = new FileStream(latestVersionPath, FileMode.Open, FileAccess.Read);

                if (fs == null)
                {
                    edi = ExceptionDispatchInfo.Capture(new LocalResourceEmpty("The contents of the package file for specified resource was empty or invalid"));
                }
            }

            return fs;
        }

        /// <summary>
        /// Installs package with specific name and version.
        /// Name: no wildcard support.
        /// Version: no wildcard support.
        /// Examples: Install "PowerShellGet" -Version "3.0.0.0"
        ///           Install "PowerShellGet" -Version "3.0.0-beta16"
        /// API Call: https://www.powershellgallery.com/api/v2/package/Id/version (version can be prerelease)
        /// </summary>    
        public override Stream InstallVersion(string packageName, string version, out ExceptionDispatchInfo edi)
        {
            edi = null;
            FileStream fs = null;

            // if 4 digits and last is 0, create 3 digit equiv string
            // 4 digit version (where last is 0) is always passed in.
            NuGetVersion.TryParse(version, out NuGetVersion pkgVersion);
            if (pkgVersion.Revision == 0)
            {
                version = pkgVersion.ToNormalizedString();
            }
            
            string packageFullName = $"{packageName.ToLower()}.{version}.nupkg";
            string packagePath = Path.Combine(repository.Uri.AbsolutePath, packageFullName);
            if (!File.Exists(packagePath))
            {
                edi = ExceptionDispatchInfo.Capture(new LocalResourceNotFoundException($"Package with specified criteria: Name {packageName} and version {version} does not exist in this repository"));
                return fs;
            }

            fs = new FileStream(packagePath, FileMode.Open, FileAccess.Read);

            if (fs == null)
            {
                edi = ExceptionDispatchInfo.Capture(new LocalResourceEmpty("The contents of the package file for specified resource was empty or invalid"));
            }

            return fs;
        }

        #endregion

        #region LocalRepo Specific Methods

        /// <summary>
        /// Method that loads file content into XMLDocument. Used when reading .nuspec file.
        /// </summary>
        private XmlDocument LoadXmlDocument(string filePath, out ExceptionDispatchInfo edi)
        {
            edi = null;
            XmlDocument doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            try { doc.Load(filePath); }
            catch (Exception e)
            {
                // TODO: catch more specific ones
                edi = ExceptionDispatchInfo.Capture(new InvalidOperationException(e.Message));
            }

            return doc;
        }

        /// <summary>
        /// Helper method that compares the tags requests to be present to the tags present in the package.
        /// </summary>
        private bool DeterminePkgTagsSatisfyRequiredTags(string[] pkgTags, string[] requiredTags)
        {
            bool isTagMatch = true;

            foreach (string tag in requiredTags)
            {
                if (!pkgTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    isTagMatch = false;
                    break;
                }
            }
            
            return isTagMatch;
        }

        /// <summary>
        /// Method that reads .nuspec file and parses out metadata information into Hashtable.
        /// </summary>
        private Hashtable GetHashtableForNuspec(string filePath, out ExceptionDispatchInfo edi)
        {
            Hashtable nuspecHashtable = new Hashtable(StringComparer.InvariantCultureIgnoreCase);

            XmlDocument nuspecXmlDocument = LoadXmlDocument(filePath, out edi);
            if (edi != null)
            {
                return nuspecHashtable;
            }

            try
            {
                XmlNodeList elemList = nuspecXmlDocument.GetElementsByTagName("metadata");
                for(int i = 0; i < elemList.Count; i++)
                {
                    XmlNode metadataInnerXml = elemList[i];

                    for(int j= 0; j<metadataInnerXml.ChildNodes.Count; j++)
                    {
                        string key = metadataInnerXml.ChildNodes[j].LocalName;
                        string value = metadataInnerXml.ChildNodes[j].InnerText;

                        if (!nuspecHashtable.ContainsKey(key))
                        {
                            nuspecHashtable.Add(key, value);
                        }
                    }  

                }
            }
            catch (Exception e)
            {
                edi = ExceptionDispatchInfo.Capture(new InvalidOperationException(e.Message));
            }

            return nuspecHashtable;        
        }

        private void GetPrivateDataFromHashtable(Hashtable pkgMetadata,
            out string prereleaseLabel,
            out Uri licenseUri,
            out Uri projectUri,
            out Uri iconUri,
            out string releaseNotes,
            out string[] tags)
        {
            prereleaseLabel = String.Empty;
            licenseUri = null;
            projectUri = null;
            iconUri = null;
            releaseNotes = String.Empty;
            tags = Utils.EmptyStrArray;

            // Look for Prerelease tag and then process any Tags in PrivateData > PSData
            if (pkgMetadata.ContainsKey("PrivateData"))
            {
                if (pkgMetadata["PrivateData"] is Hashtable privateData &&
                    privateData.ContainsKey("PSData"))
                {
                    if (privateData["PSData"] is Hashtable psData)
                    {
                        if (psData.ContainsKey("prerelease"))
                        {
                            prereleaseLabel = psData["prerelease"] as string;
                        }

                        if (psData.ContainsKey("LicenseUri") && psData["LicenseUri"] is string licenseUriString)
                        {
                            if (!Uri.TryCreate(licenseUriString, UriKind.Absolute, out licenseUri))
                            {
                                // todo error handle?
                            }
                        }

                        if (psData.ContainsKey("ProjectUri") && psData["ProjectUri"] is string projectUriString)
                        {
                            if (!Uri.TryCreate(projectUriString, UriKind.Absolute, out projectUri))
                            {
                                // TODO error handle?
                            }
                        }

                        if (psData.ContainsKey("IconUri") && psData["IconUri"] is string iconUriString)
                        {
                            if (!Uri.TryCreate(iconUriString, UriKind.Absolute, out iconUri))
                            {
                                // TODO error handle?
                            }
                        }

                        if (psData.ContainsKey("releasenotes"))
                        {
                            if (psData["ReleaseNotes"] is string releaseNotesStr)
                            {
                                releaseNotes = releaseNotesStr;
                            }
                            else if (psData["releasenotes"] is string[] releaseNotesArr)
                            {
                                releaseNotes = string.Join("\n", releaseNotesArr);
                            }
                        }

                        if (psData.ContainsKey("Tags") && psData["Tags"] is Array manifestTags)
                        {
                            var tagArr = new List<string>();
                            foreach (string tag in manifestTags)
                            {
                                tagArr.Add(tag);
                            }

                            tags = tagArr.ToArray();
                        }
                    }
                }
            }
        }

        private string[] GetCmdsOrDSCTags(string[] tags, bool isSearchingForCommands)
        {
            string tagPrefix = isSearchingForCommands ? "PSCommand_" : "PSDscResource_";
            List<string> cmdDSCTags = new List<string>();
            for (int i=0; i<tags.Length;i++)
            {
                cmdDSCTags.Add($"{tagPrefix}{tags[i]}");
            }

            return cmdDSCTags.ToArray();
        }

        #endregion

    }
}