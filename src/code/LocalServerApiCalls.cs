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
                    var dir = Directory.CreateDirectory(tempDiscoveryPath);  // should check it gets created properly
                                                                        // To delete file attributes from the existing ones get the current file attributes first and use AND (&) operator
                                                                        // with a mask (bitwise complement of desired attributes combination).
                                                                        // TODO: check the attributes and if it's read only then set it
                                                                        // attribute may be inherited from the parent
                                                                        // TODO:  are there Linux accommodations we need to consider here?
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
        public override FindResults FindTag(string tag, bool includePrerelease, ResourceType _type, out ExceptionDispatchInfo edi)
        {
            edi = null;
            List<string> responses = new List<string>();

            // call into FindAll() which returns string responses for all 
            // look at tags field for each string response

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: new Hashtable[]{}, responseType: localServerFindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for all packages that have specified Command or DSCResource name.
        /// </summary>
        public override FindResults FindCommandOrDscResource(string tag, bool includePrerelease, bool isSearchingForCommands, out ExceptionDispatchInfo edi)
        {
            List<string> responses = new List<string>();
            edi = null;

            // call into FindAll() which returns string responses for all 
            // look at tags field for each string response

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: new Hashtable[]{}, responseType: localServerFindResponseType);
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
            WildcardPattern pkgNamePattern = new WildcardPattern($"{packageName}*", WildcardOptions.IgnoreCase);
            Hashtable pkgVersionsFound = new Hashtable(StringComparer.OrdinalIgnoreCase);

            foreach (string path in Directory.GetFiles(repository.Uri.AbsolutePath))
            {
                string packageFullName = Path.GetFileName(path);

                if (!String.IsNullOrEmpty(packageFullName) && pkgNamePattern.IsMatch(packageFullName))
                {
                    string[] packageWithoutName = packageFullName.Split(new string[]{ $"{packageName}." }, StringSplitOptions.RemoveEmptyEntries);
                    string packageVersionAndExtension = packageWithoutName[0];
                    int extensionDot = packageVersionAndExtension.LastIndexOf('.');
                    string version = packageVersionAndExtension.Substring(0, extensionDot);
                    NuGetVersion.TryParse(version, out NuGetVersion nugetVersion);

                    // version: 3.0.0-alpha, Prerelease: true
                    // version: 3.5.0        Prerelease: true
                    if (!nugetVersion.IsPrerelease || includePrerelease)
                    {
                        if (!pkgVersionsFound.ContainsKey(nugetVersion))
                        {
                            pkgVersionsFound.Add(nugetVersion, path);
                        }
                    }
                }
            }

            List<NuGetVersion> pkgVersionsList = pkgVersionsFound.Keys.Cast<NuGetVersion>().ToList();
            NuGetVersion latestVersion = pkgVersionsList.First(); // TODO: do similar to name globbing, can't assume it's first or last

            string packagePath = (string) pkgVersionsFound[latestVersion];

            if (!File.Exists(packagePath))
            {
                edi = ExceptionDispatchInfo.Capture(new LocalResourceNotFoundException($"Package with specified criteria: Name {packageName} and version {latestVersion.ToString()} does not exist in this repository"));
                return findResponse;
            }

            // create temp dir
            var tempDiscoveryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                var dir = Directory.CreateDirectory(tempDiscoveryPath);  // should check it gets created properly
                                                                        // To delete file attributes from the existing ones get the current file attributes first and use AND (&) operator
                                                                        // with a mask (bitwise complement of desired attributes combination).
                                                                        // TODO: check the attributes and if it's read only then set it
                                                                        // attribute may be inherited from the parent
                                                                        // TODO:  are there Linux accommodations we need to consider here?
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
            WildcardPattern pkgNamePattern = new WildcardPattern($"{packageName}*", WildcardOptions.IgnoreCase);
            Hashtable pkgVersionsFound = new Hashtable(StringComparer.OrdinalIgnoreCase);

            foreach (string path in Directory.GetFiles(repository.Uri.AbsolutePath))
            {
                string packageFullName = Path.GetFileName(path);

                if (!String.IsNullOrEmpty(packageFullName) && pkgNamePattern.IsMatch(packageFullName))
                {
                    string[] packageWithoutName = packageFullName.Split(new string[]{ $"{packageName}." }, StringSplitOptions.RemoveEmptyEntries);
                    string packageVersionAndExtension = packageWithoutName[0];
                    int extensionDot = packageVersionAndExtension.LastIndexOf('.');
                    string version = packageVersionAndExtension.Substring(0, extensionDot);
                    NuGetVersion.TryParse(version, out NuGetVersion nugetVersion);

                    // version: 3.0.0-alpha, Prerelease: true
                    // version: 3.5.0        Prerelease: true
                    if (!nugetVersion.IsPrerelease || includePrerelease)
                    {
                        if (!pkgVersionsFound.ContainsKey(nugetVersion))
                        {
                            pkgVersionsFound.Add(nugetVersion, path);
                        }
                    }
                }
            }

            List<NuGetVersion> pkgVersionsList = pkgVersionsFound.Keys.Cast<NuGetVersion>().ToList();
            NuGetVersion latestVersion = pkgVersionsList.First(); // TODO: do similar to name globbing, can't assume it's first or last

            string packagePath = (string) pkgVersionsFound[latestVersion];

            if (!File.Exists(packagePath))
            {
                edi = ExceptionDispatchInfo.Capture(new LocalResourceNotFoundException($"Package with specified criteria: Name {packageName} and version {latestVersion.ToString()} does not exist in this repository"));
                return findResponse;
            }

            // create temp dir
            var tempDiscoveryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                var dir = Directory.CreateDirectory(tempDiscoveryPath);  // should check it gets created properly
                                                                        // To delete file attributes from the existing ones get the current file attributes first and use AND (&) operator
                                                                        // with a mask (bitwise complement of desired attributes combination).
                                                                        // TODO: check the attributes and if it's read only then set it
                                                                        // attribute may be inherited from the parent
                                                                        // TODO:  are there Linux accommodations we need to consider here?
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

                    // parse out PSData > add directly as keys to the hashtable

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

                bool isTagMatch = DeterminePkgTagsSatisfyRequiredTags(pkgTags: pkgMetadata["Tags"] as string[], requiredTags: tags);
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
                    var dir = Directory.CreateDirectory(tempDiscoveryPath);  // should check it gets created properly
                                                                        // To delete file attributes from the existing ones get the current file attributes first and use AND (&) operator
                                                                        // with a mask (bitwise complement of desired attributes combination).
                                                                        // TODO: check the attributes and if it's read only then set it
                                                                        // attribute may be inherited from the parent
                                                                        // TODO:  are there Linux accommodations we need to consider here?
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
        /// Find method which allows for searching for single name with wildcards and tag and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "PowerShell*" -Tag "Provider"
        /// Implementation Note: filter additionally and verify ONLY package name was a match.
        /// </summary>
        public override FindResults FindNameGlobbingWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            List<string> responses = new List<string>();
            edi = null;

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: new Hashtable[]{}, responseType: localServerFindResponseType);
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

            WildcardPattern pkgNamePattern = new WildcardPattern($"{packageName}*", WildcardOptions.IgnoreCase);
            Hashtable pkgVersionsFound = new Hashtable(StringComparer.OrdinalIgnoreCase);

            foreach (string path in Directory.GetFiles(repository.Uri.AbsolutePath))
            {
                string packageFullName = Path.GetFileName(path);

                if (!String.IsNullOrEmpty(packageFullName) && pkgNamePattern.IsMatch(packageFullName))
                {
                    string[] packageWithoutName = packageFullName.Split(new string[]{ $"{packageName}." }, StringSplitOptions.RemoveEmptyEntries);
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
            List<Hashtable> foundPkgs = new List<Hashtable>();
            foreach(NuGetVersion satisfyingVersion in pkgVersionsList)
            {
                string packagePath = (string) pkgVersionsFound[satisfyingVersion];
                
                // create temp dir
                var tempDiscoveryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

                try
                {
                    var dir = Directory.CreateDirectory(tempDiscoveryPath);  // should check it gets created properly
                                                                            // To delete file attributes from the existing ones get the current file attributes first and use AND (&) operator
                                                                            // with a mask (bitwise complement of desired attributes combination).
                                                                            // TODO: check the attributes and if it's read only then set it
                                                                            // attribute may be inherited from the parent
                                                                            // TODO:  are there Linux accommodations we need to consider here?
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
                var dir = Directory.CreateDirectory(tempDiscoveryPath);  // should check it gets created properly
                                                                        // To delete file attributes from the existing ones get the current file attributes first and use AND (&) operator
                                                                        // with a mask (bitwise complement of desired attributes combination).
                                                                        // TODO: check the attributes and if it's read only then set it
                                                                        // attribute may be inherited from the parent
                                                                        // TODO:  are there Linux accommodations we need to consider here?
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
        /// Find method which allows for searching for single name with specific version and tag.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5" -Tag "Provider"
        /// </summary>
        public override FindResults FindVersionWithTag(string packageName, string version, string[] tags, ResourceType type, out ExceptionDispatchInfo edi)
        {
            edi = null;
            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: new Hashtable[]{}, responseType: localServerFindResponseType);
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
            edi = null;
            return null;
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

            // find packagename that matches our criteria -> gives us the nupkg
            // if we must return Stream:
            // 1) take nupkg -> zip file
            // read contents of zip into stream
            // return stream
            // in InstallHelper read out stream to get zip file
            // (redundant packing)

            // have separate interfaces for local versus remote repos

            // return path where zip file is present
            // for local this would be okay
            // for remote, we'd read stream contents into temp path, and need that passed in (seems messy)

            string packageFullName = $"{packageName.ToLower()}.{version}.nupkg"; // TODO: test with 3 and 4 digit versions
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
                if (!pkgTags.Contains(tag.ToLower()))
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

        #endregion

    }
}