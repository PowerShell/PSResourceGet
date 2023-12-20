// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Versioning;
using System.Text.RegularExpressions;
using System.Xml;
using System.Net;
using System.Management.Automation;
using System.Runtime.ExceptionServices;
using Microsoft.PowerShell.PSResourceGet.Cmdlets;

namespace Microsoft.PowerShell.PSResourceGet
{
    internal class LocalServerAPICalls : ServerApiCall
    {
        #region Members

        public override PSRepositoryInfo Repository { get; set; }
        private readonly PSCmdlet _cmdletPassedIn;
        private readonly FindResponseType _localServerFindResponseType = FindResponseType.ResponseHashtable;
        private readonly string _fileTypeKey = "filetype";

        #endregion

        #region Constructor

        public LocalServerAPICalls (PSRepositoryInfo repository, PSCmdlet cmdletPassedIn, NetworkCredential networkCredential) : base (repository, networkCredential)
        {
            this.Repository = repository;
            _cmdletPassedIn = cmdletPassedIn;
        }

        #endregion

        #region Overriden Methods

        /// <summary>
        /// Find method which allows for searching for all packages from a repository and returns latest version for each.
        /// Examples: Search -Repository PSGallery
        /// API call:
        /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion
        /// </summary>
        public override FindResults FindAll(bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::FindAll()");
            return FindTagsHelper(tags: Utils.EmptyStrArray, includePrerelease, out errRecord);
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag from a repository and returns latest version for each.
        /// Examples: Search -Tag "JSON" -Repository PSGallery
        /// API call:
        /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&searchTerm=tag:JSON&includePrerelease=true
        /// </summary>
        public override FindResults FindTags(string[] tags, bool includePrerelease, ResourceType _type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::FindTags()");
            FindResults tagFindResults = FindTagsHelper(tags, includePrerelease, out errRecord);
            if (tagFindResults.IsFindResultsEmpty())
            {
                errRecord = new ErrorRecord(
                    new ResourceNotFoundException($"Package(s) with Tags '{String.Join(", ", tags)}' could not be found in repository '{Repository.Name}'."), 
                    "FindTagsPackageNotFound", 
                    ErrorCategory.ObjectNotFound, 
                    this);
            }

            return tagFindResults;
        }

        /// <summary>
        /// Find method which allows for searching for all packages that have specified Command or DSCResource name.
        /// </summary>
        public override FindResults FindCommandOrDscResource(string[] tags, bool includePrerelease, bool isSearchingForCommands, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::FindCommandOrDscResource()");
            string[] cmdsOrDSCs = GetCmdsOrDSCTags(tags: tags, isSearchingForCommands: isSearchingForCommands);
            FindResults cmdOrDSCFindResults = FindTagsHelper(cmdsOrDSCs, includePrerelease, out errRecord);
            if (cmdOrDSCFindResults.IsFindResultsEmpty())
            {
                string paramName = isSearchingForCommands ? "Command Name(s)" : "DSCResource Name(s)";
                errRecord = new ErrorRecord(
                    new ResourceNotFoundException($"Package(s) with {paramName} '{String.Join(", ", tags)}' could not be found in repository '{Repository.Name}'."), 
                    "FindCmdOrDSCNamesPackageNotFound", 
                    ErrorCategory.ObjectNotFound, 
                    this);
            }

            return cmdOrDSCFindResults;
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
        public override FindResults FindName(string packageName, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::FindName()");
            return FindNameHelper(packageName, Utils.EmptyStrArray, includePrerelease, type, out errRecord);
        }

        /// <summary>
        /// Find method which allows for searching for single name and tag and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "PowerShellGet" -Tag "Provider"
        /// Implementation Note: Need to filter further for latest version (prerelease or non-prerelease dependening on user preference)
        /// </summary>
        public override FindResults FindNameWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::FindNameWithTag()");
            return FindNameHelper(packageName, tags, includePrerelease, type, out errRecord);
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "PowerShell*"
        /// API call:
        /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='az*'
        /// Implementation Note: filter additionally and verify ONLY package name was a match.
        /// </summary>
        public override FindResults FindNameGlobbing(string packageName, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::FindNameGlobbing()");
            return FindNameGlobbingHelper(packageName, Utils.EmptyStrArray, includePrerelease, type, out errRecord);
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and tag and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "PowerShell*" -Tag "Provider"
        /// Implementation Note: filter additionally and verify ONLY package name was a match.
        /// </summary>
        public override FindResults FindNameGlobbingWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::FindNameGlobbingWithTag()");
            return FindNameGlobbingHelper(packageName, tags, includePrerelease, type, out errRecord);
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
        public override FindResults FindVersionGlobbing(string packageName, VersionRange versionRange, bool includePrerelease, ResourceType type, bool getOnlyLatest, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::FindVersionGlobbing()");
            FindResults findResponse = new FindResults();
            errRecord = null;
            string actualPkgName = packageName;

            Hashtable pkgVersionsFound = GetMatchingFilesGivenSpecificName(packageName: packageName, includePrerelease: includePrerelease, versionRange: versionRange, actualName: out actualPkgName, errRecord: out errRecord);

            List<NuGetVersion> pkgVersionsList = pkgVersionsFound.Keys.Cast<NuGetVersion>().ToList();
            pkgVersionsList.Sort();
            List<Hashtable> foundPkgs = new List<Hashtable>();
            for (int i = pkgVersionsList.Count - 1; i >= 0; i--)
            {
                // Versions are present in pkgVersionsList in asc order, wherease we need it in desc so we traverse it in reverse.
                NuGetVersion satisfyingVersion = pkgVersionsList[i];

                string packagePath = (string) pkgVersionsFound[satisfyingVersion];

                Hashtable pkgMetadata = GetMetadataFromNupkg(packageName: actualPkgName, packagePath: packagePath, requiredTags: Utils.EmptyStrArray, errRecord: out errRecord);
                if (errRecord != null || pkgMetadata.Count == 0)
                {
                    continue;
                }

                foundPkgs.Add(pkgMetadata);
            }

            findResponse = new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: foundPkgs.ToArray(), responseType: _localServerFindResponseType);

            return findResponse;
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5"
        /// API call: http://www.powershellgallery.com/api/v2/Packages(Id='PowerShellGet', Version='2.2.5')
        /// </summary>
        public override FindResults FindVersion(string packageName, string version, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::FindVersion()");
            return FindVersionHelper(packageName, version, Utils.EmptyStrArray, type, out errRecord);
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version and tag.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5" -Tag "Provider"
        /// </summary>
        public override FindResults FindVersionWithTag(string packageName, string version, string[] tags, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::FindVersionWithTag()");
            return FindVersionHelper(packageName, version, tags, type, out errRecord);
        }

        /**  INSTALL APIS **/

        /// <summary>
        /// Installs a specific package.
        /// Name: no wildcard support.
        /// Examples: Install "PowerShellGet"
        ///           Install "PowerShellGet" -Version "3.0.0"
        /// </summary>
        public override Stream InstallPackage(string packageName, string packageVersion, bool includePrerelease, out ErrorRecord errRecord)
        {
            Stream results = new MemoryStream();
            if (string.IsNullOrEmpty(packageVersion))
            {
                results = InstallName(packageName, includePrerelease, out errRecord);
            }
            else {
                results = InstallVersion(packageName, packageVersion, out errRecord);
            }

            return results;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Helper method called by FindName() and FindNameWithTag()
        /// </summary>
        private FindResults FindNameHelper(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::FindNameHelper()");
            FindResults findResponse = new FindResults();
            errRecord = null;

            WildcardPattern pkgNamePattern = new WildcardPattern($"{packageName}.*", WildcardOptions.IgnoreCase);
            NuGetVersion latestVersion = new NuGetVersion("0.0.0.0");
            String latestVersionPath = String.Empty;
            string actualPkgName = packageName;

            foreach (string path in Directory.GetFiles(Repository.Uri.LocalPath))
            {
                string packageFullName = Path.GetFileName(path);

                if (!String.IsNullOrEmpty(packageFullName) && pkgNamePattern.IsMatch(packageFullName))
                {
                    NuGetVersion nugetVersion = GetInfoFromFileName(packageFullName: packageFullName, packageName: packageName, actualName: out actualPkgName, out errRecord);
                    _cmdletPassedIn.WriteDebug($"Version parsed as '{nugetVersion}'");

                    if (errRecord != null)
                    {
                        return findResponse;
                    }

                    if ((!nugetVersion.IsPrerelease || includePrerelease) && (nugetVersion > latestVersion))
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
                errRecord = new ErrorRecord(
                    new ResourceNotFoundException($"Package with name {packageName} could not be found in repository '{Repository.Name}'."), 
                    "PackageNotFound", 
                    ErrorCategory.ResourceUnavailable, 
                    this);

                return findResponse;
            }

            Hashtable pkgMetadata = GetMetadataFromNupkg(packageName: actualPkgName, packagePath: latestVersionPath, requiredTags: tags, errRecord: out errRecord);
            if (errRecord != null)
            {
                return findResponse;
            }

            // this condition will be true, for FindNameWithTags() when package satisfying that criteria is not met
            if (pkgMetadata.Count == 0)
            {
                errRecord = new ErrorRecord(
                    new ResourceNotFoundException($"Package with name '{packageName}' and tags '{String.Join(", ", tags)}' could not be found in repository '{Repository.Name}'."), 
                    "PackageNotFound", 
                    ErrorCategory.ResourceUnavailable, 
                    this);
            }

            findResponse = new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: new Hashtable[]{pkgMetadata}, responseType: _localServerFindResponseType);

            return findResponse;
        }

        /// <summary>
        /// Helper method called by FindNameGlobbing() and FindNameGlobbingWithTag()
        /// </summary>
        private FindResults FindNameGlobbingHelper(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::FindNameGlobbingHelper()");
            FindResults findResponse = new FindResults();
            List<Hashtable> pkgsFound = new List<Hashtable>();
            errRecord = null;

            Hashtable pkgVersionsFound = GetMatchingFilesGivenNamePattern(packageNameWithWildcard: packageName, includePrerelease: includePrerelease);

            List<string> pkgNamesList = pkgVersionsFound.Keys.Cast<string>().ToList();
            foreach(string pkgFound in pkgNamesList)
            {
                Hashtable pkgInfo = pkgVersionsFound[pkgFound] as Hashtable;
                string pkgPath = pkgInfo["path"] as string;
                _cmdletPassedIn.WriteDebug($"Package '{pkgFound}' found from path '{pkgPath}'");

                Hashtable pkgMetadata = GetMetadataFromNupkg(packageName: pkgFound, packagePath: pkgPath, requiredTags: tags, errRecord: out errRecord);
                if (errRecord != null || pkgMetadata.Count == 0)
                {
                    continue;
                }

                pkgsFound.Add(pkgMetadata);
            }

            findResponse = new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: pkgsFound.ToArray(), responseType: _localServerFindResponseType);

            return findResponse;
        }

        /// <summary>
        /// Helper method called by FindVersion() and FindVersionWithTag()
        /// </summary>
        private FindResults FindVersionHelper(string packageName, string version, string[] tags, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::FindVersionHelper()");
            FindResults findResponse = new FindResults();
            errRecord = null;

            if (!NuGetVersion.TryParse(version, out NuGetVersion requiredVersion))
            {
                errRecord = new ErrorRecord(
                    new InvalidOperationException($"Version {version} could not be parsed into a valid NuGetVersion"),
                    "FindVersionFailure", 
                    ErrorCategory.InvalidData, 
                    this);
                    
                return findResponse;
            }

            WildcardPattern pkgNamePattern = new WildcardPattern($"{packageName}.*", WildcardOptions.IgnoreCase);
            string pkgPath = String.Empty;
            string actualPkgName = String.Empty;
            foreach (string path in Directory.GetFiles(Repository.Uri.LocalPath))
            {
                string packageFullName = Path.GetFileName(path);
                if (!String.IsNullOrEmpty(packageFullName) && pkgNamePattern.IsMatch(packageFullName))
                {
                    NuGetVersion nugetVersion = GetInfoFromFileName(packageFullName: packageFullName, packageName: packageName, actualName: out actualPkgName, out errRecord);
                    _cmdletPassedIn.WriteDebug($"'{packageName}' version parsed as '{nugetVersion}'");

                    if (errRecord != null)
                    {
                        return findResponse;
                    }

                    if (nugetVersion == requiredVersion)
                    {
                        _cmdletPassedIn.WriteDebug("Found matching version");
                        string pkgFullName = $"{actualPkgName}.{nugetVersion.ToString()}.nupkg";
                        pkgPath = Path.Combine(Repository.Uri.LocalPath, pkgFullName);
                        break;
                    }
                }
            }

            if (String.IsNullOrEmpty(pkgPath))
            {
                // means no package was found with this name, version (and possibly tags).
                errRecord = new ErrorRecord(
                    new ResourceNotFoundException($"Package with name '{packageName}', version '{version}' and tags '{String.Join(", ", tags)}' could not be found in repository '{Repository.Name}'."), 
                    "PackageNotFound", 
                    ErrorCategory.ResourceUnavailable, 
                    this);

                return findResponse;
            }

            Hashtable pkgMetadata = GetMetadataFromNupkg(packageName: actualPkgName, packagePath: pkgPath, requiredTags: tags, errRecord: out errRecord);
            if (errRecord != null)
            {
                return findResponse;
            }

            // this condition will be true, for FindVersionWithTags() when package satisfying that criteria is not met
            if (pkgMetadata.Count == 0)
            {
                errRecord = new ErrorRecord(
                    new ResourceNotFoundException($"Package with name '{packageName}', and tags '{String.Join(", ", tags)}' could not be found in repository '{Repository.Name}'."), 
                    "PackageNotFound", 
                    ErrorCategory.InvalidResult, 
                    this);
            }

            findResponse = new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: new Hashtable[]{pkgMetadata}, responseType: _localServerFindResponseType);
            return findResponse;
        }

        /// <summary>
        /// Helper method called by FindTags(), FindAll(), and FindCommandOrDSCResource()
        /// </summary>
        private FindResults FindTagsHelper(string[] tags, bool includePrerelease, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::FindTagsHelper()");
            FindResults findResponse = new FindResults();
            List<Hashtable> pkgsFound = new List<Hashtable>();
            errRecord = null;

            Hashtable pkgVersionsFound = GetMatchingFilesGivenNamePattern(packageNameWithWildcard: String.Empty, includePrerelease: includePrerelease);

            List<string> pkgNamesList = pkgVersionsFound.Keys.Cast<string>().ToList();
            foreach(string pkgFound in pkgNamesList)
            {
                Hashtable pkgInfo = pkgVersionsFound[pkgFound] as Hashtable;
                NuGetVersion pkgVersion = pkgInfo["version"] as NuGetVersion;
                string pkgPath = pkgInfo["path"] as string;
                _cmdletPassedIn.WriteDebug($"Found package '{pkgFound}' from path '{pkgPath}'");

                Hashtable pkgMetadata = GetMetadataFromNupkg(packageName: pkgFound, packagePath: pkgPath, requiredTags: tags, errRecord: out errRecord);
                if (errRecord != null)
                {
                    return findResponse;
                }

                // This condition is hit if the package is not a match with respect to tags, in which case we should skip the package and not return from this method.
                if (pkgMetadata.Count == 0)
                {
                    continue;
                }

                pkgsFound.Add(pkgMetadata);
            }

            findResponse = new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: pkgsFound.ToArray(), responseType: _localServerFindResponseType);

            return findResponse;
        }

        /// <summary>
        /// Installs specific package.
        /// Name: no wildcard support.
        /// Examples: Install "PowerShellGet"
        /// Implementation Note:   if not prerelease: https://www.powershellgallery.com/api/v2/package/powershellget (Returns latest stable)
        ///                        if prerelease, call into InstallVersion instead.
        /// </summary>
        private Stream InstallName(string packageName, bool includePrerelease, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::InstallName()");
            FileStream fs = null;
            errRecord = null;
            WildcardPattern pkgNamePattern = new WildcardPattern($"{packageName}.*", WildcardOptions.IgnoreCase);
            NuGetVersion latestVersion = new NuGetVersion("0.0.0.0");
            String latestVersionPath = String.Empty;

            foreach (string path in Directory.GetFiles(Repository.Uri.LocalPath))
            {
                string packageFullName = Path.GetFileName(path);

                if (!String.IsNullOrEmpty(packageFullName) && pkgNamePattern.IsMatch(packageFullName))
                {
                    _cmdletPassedIn.WriteDebug($"'{packageName}' found in '{path}'");
                    string[] packageWithoutName = packageFullName.ToLower().Split(new string[] { $"{packageName.ToLower()}." }, StringSplitOptions.RemoveEmptyEntries);
                    string packageVersionAndExtension = packageWithoutName[0];
                    int extensionDot = packageVersionAndExtension.LastIndexOf('.');
                    string version = packageVersionAndExtension.Substring(0, extensionDot);
                    _cmdletPassedIn.WriteDebug($"Parsing version '{version}' of package '{packageName}'");
                    NuGetVersion.TryParse(version, out NuGetVersion nugetVersion);

                    if ((!nugetVersion.IsPrerelease || includePrerelease) && (nugetVersion > latestVersion))
                    {
                        latestVersion = nugetVersion;
                        latestVersionPath = path;
                    }
                }
            }

            if (String.IsNullOrEmpty(latestVersionPath))
            {
                errRecord = new ErrorRecord(
                    new LocalResourceEmpty($"'{packageName}' is not present in repository"),
                    "InstallNameFailure",
                    ErrorCategory.ResourceUnavailable,
                    this);

                return fs;
            }

            try
            {
                _cmdletPassedIn.WriteDebug($"Reading file '{latestVersionPath}'");
                fs = new FileStream(latestVersionPath, FileMode.Open, FileAccess.Read);
                if (fs == null)
                {
                    errRecord = new ErrorRecord(
                        new LocalResourceEmpty("The contents of the package file for specified resource was empty or invalid"),
                        "InstallNameFailure",
                        ErrorCategory.ResourceUnavailable,
                        this);
                }
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "InstallNameFailure",
                    ErrorCategory.ReadError,
                    this);
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
        private Stream InstallVersion(string packageName, string version, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::InstallVersion()");
            errRecord = null;
            FileStream fs = null;

            // if 4 digits and last is 0, create 3 digit equiv string
            // 4 digit version (where last is 0) is always passed in.
            NuGetVersion.TryParse(version, out NuGetVersion pkgVersion);
            _cmdletPassedIn.WriteDebug($"Version parsed as '{pkgVersion}'");

            if (pkgVersion.Revision == 0)
            {
                version = pkgVersion.ToNormalizedString();
            }

            WildcardPattern pkgNamePattern = new WildcardPattern($"{packageName}.{version}.nupkg*", WildcardOptions.IgnoreCase);
            String pkgVersionPath = String.Empty;

            foreach (string path in Directory.GetFiles(Repository.Uri.LocalPath))
            {
                string packageFullName = Path.GetFileName(path);

                if (!String.IsNullOrEmpty(packageFullName) && pkgNamePattern.IsMatch(packageFullName))
                {
                    _cmdletPassedIn.WriteDebug($"Found match with '{path}'");
                    pkgVersionPath = path;
                }
            }

            if (String.IsNullOrEmpty(pkgVersionPath))
            {
                errRecord = new ErrorRecord(
                    new LocalResourceEmpty($"'{packageName}' is not present in repository"),
                    "InstallNameFailure",
                    ErrorCategory.ResourceUnavailable,
                    this);

                return fs;
            }

            try
            {
                _cmdletPassedIn.WriteDebug($"Reading file '{pkgVersionPath}'");
                fs = new FileStream(pkgVersionPath, FileMode.Open, FileAccess.Read);

                if (fs == null)
                {
                    errRecord = new ErrorRecord(
                        new LocalResourceEmpty("The contents of the package file for specified resource was empty or invalid"),
                        "InstallNameFailure",
                        ErrorCategory.ResourceUnavailable,
                        this);
                }
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "InstallVersionFailure",
                    ErrorCategory.ReadError,
                    this);
            }

            return fs;
        }

        /// <summary>
        /// Extract metadata from .nupkg package file.
        /// This is called only for packages that are ascertained to be a match for our search criteria.
        /// </summary>
        private Hashtable GetMetadataFromNupkg(string packageName, string packagePath, string[] requiredTags, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::GetMetadataFromNupkg()");
            Hashtable pkgMetadata = new Hashtable(StringComparer.OrdinalIgnoreCase);
            errRecord = null;

            // create temp directory where we will copy .nupkg to, extract contents, etc.
            var tempDiscoveryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string packageFullName = Path.GetFileName(packagePath);

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
                _cmdletPassedIn.WriteDebug($"Extracting '{zipFilePath}' to '{tempDiscoveryPath}'");
                System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, tempDiscoveryPath);

                string psd1FilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.psd1");
                string ps1FilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.ps1");
                string nuspecFilePath = Path.Combine(tempDiscoveryPath, $"{packageName}.nuspec");

                List<string> pkgTags = new List<string>();

                if (File.Exists(psd1FilePath))
                {
                    _cmdletPassedIn.WriteDebug($"Attempting to read module manifest file '{psd1FilePath}'");
                    if (!Utils.TryReadManifestFile(psd1FilePath, out pkgMetadata, out Exception readManifestError))
                    {
                        errRecord = new ErrorRecord(
                            readManifestError, 
                            "GetMetadataFromNupkgFailure", 
                            ErrorCategory.ParserError, 
                            this);
                        
                        return pkgMetadata;
                    }

                    GetPrivateDataFromHashtable(pkgMetadata, out string prereleaseLabel, out Uri licenseUri, out Uri projectUri, out Uri iconUri, out string releaseNotes, out string[] pkgHashTags);
                    pkgMetadata.Add("Tags", pkgHashTags);
                    pkgMetadata.Add("Prerelease", prereleaseLabel);
                    pkgMetadata.Add("LicenseUri", licenseUri);
                    pkgMetadata.Add("ProjectUri", projectUri);
                    pkgMetadata.Add("IconUri", iconUri);
                    pkgMetadata.Add("ReleaseNotes", releaseNotes);
                    pkgMetadata.Add("Id", packageName);
                    pkgMetadata.Add(_fileTypeKey, Utils.MetadataFileType.ModuleManifest);

                    pkgTags.AddRange(pkgHashTags);
                }
                else if (File.Exists(ps1FilePath))
                {
                    _cmdletPassedIn.WriteDebug($"Attempting to read script file '{ps1FilePath}'");
                    if (!PSScriptFileInfo.TryTestPSScriptFileInfo(ps1FilePath, out PSScriptFileInfo parsedScript, out ErrorRecord[] errors, out string[] verboseMsgs))
                    {
                        errRecord = new ErrorRecord(
                            new InvalidDataException($"PSScriptFile could not be read properly"), 
                            "GetMetadataFromNupkgFailure", 
                            ErrorCategory.ParserError, 
                            this);

                        return pkgMetadata;
                    }

                    pkgMetadata = parsedScript.ToHashtable();
                    pkgMetadata.Add("Id", packageName);
                    pkgMetadata.Add(_fileTypeKey, Utils.MetadataFileType.ScriptFile);
                    pkgTags.AddRange(pkgMetadata["Tags"] as string[]);

                }
                else if (File.Exists(nuspecFilePath))
                {
                    _cmdletPassedIn.WriteDebug($"Attempting to read nuspec file '{nuspecFilePath}'");
                    pkgMetadata = GetHashtableForNuspec(nuspecFilePath, out errRecord);
                    if (errRecord != null)
                    {
                        return pkgMetadata;
                    }

                    pkgMetadata.Add(_fileTypeKey, Utils.MetadataFileType.Nuspec);
                    string nuspecTags = pkgMetadata["tags"] as string;
                    string[] nuspecTagsArray = nuspecTags.Split(new char[]{' '});
                    pkgTags.AddRange(nuspecTagsArray);
                }
                else
                {
                    errRecord = new ErrorRecord(
                        new InvalidDataException($".nupkg package must contain either .psd1, .ps1, or .nuspec file and none were found"),
                        "GetMetadataFromNupkgFailure", 
                        ErrorCategory.InvalidData, 
                        this);
                        
                    return pkgMetadata;
                }

                // if no RequiredTags are specified for the API, this will return true by default.
                bool isTagMatch = DeterminePkgTagsSatisfyRequiredTags(pkgTags: pkgTags.ToArray(), requiredTags: requiredTags);
                if (!isTagMatch)
                {
                    return new Hashtable();
                }
            }
            catch (Exception e)
            {
               errRecord = new ErrorRecord(
                   new InvalidOperationException($"Temporary folder for installation could not be created or set due to: {e.Message}"), 
                   "GetMetadataFromNupkgFailure", 
                   ErrorCategory.InvalidOperation, 
                   this);
            }
            finally
            {
                if (Directory.Exists(tempDiscoveryPath))
                {
                    Utils.DeleteDirectory(tempDiscoveryPath);
                }
            }

            return pkgMetadata;
        }

        /// <summary>
        /// Looks through .nupkg files present in the repository and returns
        /// hashtable with those that matches the exact name and prerelease requirements provided.
        /// This helper method is called for FindVersionGlobbing()
        /// </summary>
        private Hashtable GetMatchingFilesGivenSpecificName(string packageName, bool includePrerelease, VersionRange versionRange, out string actualName, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::GetMatchingFilesGivenSpecificName()");
            actualName = packageName;
            // used for FindVersionGlobbing where we know exact non-wildcard name of the package
            WildcardPattern pkgNamePattern = new WildcardPattern($"{packageName}.*", WildcardOptions.IgnoreCase);
            Hashtable pkgVersionsFound = new Hashtable(StringComparer.OrdinalIgnoreCase);
            errRecord = null;

            foreach (string path in Directory.GetFiles(Repository.Uri.LocalPath))
            {
                string packageFullName = Path.GetFileName(path);

                if (!String.IsNullOrEmpty(packageFullName) && pkgNamePattern.IsMatch(packageFullName))
                {
                    NuGetVersion nugetVersion = GetInfoFromFileName(packageFullName: packageFullName, packageName: packageName, out actualName, errRecord: out errRecord);
                    _cmdletPassedIn.WriteDebug($"Found package '{packageName}' from path '{path}'");
                    if (errRecord != null)
                    {
                        continue;
                    }

                    if ((!nugetVersion.IsPrerelease || includePrerelease) && (versionRange.Satisfies(nugetVersion)))
                    {
                        if (!pkgVersionsFound.ContainsKey(nugetVersion))
                        {
                            pkgVersionsFound.Add(nugetVersion, path);
                        }
                    }
                }
            }

            return pkgVersionsFound;
        }

        /// <summary>
        /// Looks through .nupkg files present in the repository and returns
        /// hashtable with those that match the name wildcard pattern and prerelease requirements provided.
        /// This helper method is called for FindAll(), FindTags(), FindNameGlobbing() scenarios.
        /// </summary>
        private Hashtable GetMatchingFilesGivenNamePattern(string packageNameWithWildcard, bool includePrerelease)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::GetMatchingFilesGivenNamePattern()");
            bool isNameFilteringRequired = !String.IsNullOrEmpty(packageNameWithWildcard);

            // wildcard name possibilities: power*, *get, power*get
            WildcardPattern pkgNamePattern = new WildcardPattern($"{packageNameWithWildcard}", WildcardOptions.IgnoreCase);

            Regex rx = new Regex(@"\.\d+\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Hashtable pkgVersionsFound = new Hashtable(StringComparer.OrdinalIgnoreCase);

            foreach (string path in Directory.GetFiles(Repository.Uri.LocalPath))
            {
                string packageFullName = Path.GetFileName(path);
                MatchCollection matches = rx.Matches(packageFullName);
                if (matches.Count == 0)
                {
                    continue;
                }

                Match match = matches[0];

                GroupCollection groups = match.Groups;
                if (groups.Count == 0)
                {
                    continue;
                }

                Capture group = groups[0];

                string pkgFoundName = packageFullName.Substring(0, group.Index);

                if (isNameFilteringRequired)
                {
                    if (!pkgNamePattern.IsMatch(pkgFoundName))
                    {
                        continue;
                    }
                }

                string version = packageFullName.Substring(group.Index + 1, packageFullName.LastIndexOf('.') - group.Index - 1);
                _cmdletPassedIn.WriteDebug($"Found package '{pkgFoundName}', version '{version}', from path '{path}'");

                if (!NuGetVersion.TryParse(version, out NuGetVersion nugetVersion))
                {
                    continue;
                }

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

            return pkgVersionsFound;
        }

        /// <summary>
        /// Takes .nupkg package file name (i.e like mypackage.1.0.0.nupkg) and parses out version from it.
        /// </summary>
        private NuGetVersion GetInfoFromFileName(string packageFullName, string packageName, out string actualName, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In LocalServerApiCalls::GetInfoFromFileName()");
            // packageFullName will look like package.1.0.0.nupkg
            errRecord = null;

            string[] packageWithoutName = packageFullName.ToLower().Split(new string[]{ $"{packageName.ToLower()}." }, StringSplitOptions.RemoveEmptyEntries);
            string packageVersionAndExtension = packageWithoutName[0];
            string[] originalFileNameParts = packageFullName.Split(new string[]{ $".{packageVersionAndExtension}" }, StringSplitOptions.RemoveEmptyEntries);
            actualName = String.IsNullOrEmpty(originalFileNameParts[0]) ? packageName : originalFileNameParts[0];
            int extensionDot = packageVersionAndExtension.LastIndexOf('.');
            string version = packageVersionAndExtension.Substring(0, extensionDot);
            if (!NuGetVersion.TryParse(version, out NuGetVersion nugetVersion))
            {
                errRecord = new ErrorRecord(
                    new ArgumentException($"Could not parse version {version} from file {packageFullName}"), 
                    "GetInfoFromFileNameFilaure", 
                    ErrorCategory.ParserError, 
                    this);

                return null;
            }

            return nugetVersion;
        }

        /// <summary>
        /// Method that loads file content into XMLDocument. Used when reading .nuspec file.
        /// </summary>
        private XmlDocument LoadXmlDocument(string filePath, out ErrorRecord errRecord)
        {
            errRecord = null;
            XmlDocument doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            try { doc.Load(filePath); }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e, 
                    "LoadXmlDocumentFailure", 
                    ErrorCategory.ReadError, 
                    this);
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
        private Hashtable GetHashtableForNuspec(string filePath, out ErrorRecord errRecord)
        {
            Hashtable nuspecHashtable = new Hashtable(StringComparer.InvariantCultureIgnoreCase);

            XmlDocument nuspecXmlDocument = LoadXmlDocument(filePath, out errRecord);
            if (errRecord != null)
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
                errRecord = new ErrorRecord(
                    exception: e, 
                    "GetHashtableForNuspecFailure", 
                    ErrorCategory.ReadError, 
                    this);
            }

            return nuspecHashtable;
        }

        /// <summary>
        /// Helper method that takes hashtable containing metadata parsed from .psd1 file and removes
        /// metadata that is present in PSData key entry.
        /// </summary>
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
                                licenseUri = null;
                            }
                        }

                        if (psData.ContainsKey("ProjectUri") && psData["ProjectUri"] is string projectUriString)
                        {
                            if (!Uri.TryCreate(projectUriString, UriKind.Absolute, out projectUri))
                            {
                                projectUri = null;
                            }
                        }

                        if (psData.ContainsKey("IconUri") && psData["IconUri"] is string iconUriString)
                        {
                            if (!Uri.TryCreate(iconUriString, UriKind.Absolute, out iconUri))
                            {
                                iconUri = null;
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

        /// <summary>
        /// Prepends required tags with prefix for Command or DSCResource so they can later be compared against package's tags accurately.
        /// </summary>
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
