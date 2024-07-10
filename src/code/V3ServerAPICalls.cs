// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections;
using System.Management.Automation;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    internal class V3ServerAPICalls : ServerApiCall
    {
        #region Members
        public override PSRepositoryInfo Repository { get; set; }
        private readonly PSCmdlet _cmdletPassedIn;
        private HttpClient _sessionClient { get; set; }
        private bool _isNuGetRepo { get; set; }
        private bool _isJFrogRepo { get; set; }
        private bool _isGHPkgsRepo { get; set; }
        private bool _isMyGetRepo { get; set; }
        public FindResponseType v3FindResponseType = FindResponseType.ResponseString;
        private static readonly Hashtable[] emptyHashResponses = new Hashtable[]{};
        private static readonly string nugetRepoUri = "https://api.nuget.org/v3/index.json";
        private static readonly string resourcesName = "resources";
        private static readonly string itemsName = "items";
        private static readonly string versionName = "version";
        private static readonly string dataName = "data";
        private static readonly string idName = "id";
        private static readonly string idLinkName = "@id";
        private static readonly string tagsName = "tags";
        private static readonly string catalogEntryProperty = "catalogEntry";
        private static readonly string packageContentProperty = "packageContent";
        // MyGet.org repository responses from SearchQueryService have a peculiarity where the totalHits property int returned is 10,000 + actual number of hits.
        // This is intentional on their end and "is to preserve the uninterupted pagination of NuGet within Visual Studio 2015".
        private readonly int myGetTotalHitsBuffer = 10000;

        #endregion

        #region Constructor

        public V3ServerAPICalls(PSRepositoryInfo repository, PSCmdlet cmdletPassedIn, NetworkCredential networkCredential, string userAgentString) : base(repository, networkCredential)
        {
            this.Repository = repository;
            _cmdletPassedIn = cmdletPassedIn;
            HttpClientHandler handler = new HttpClientHandler();
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            bool token = false;

            if(networkCredential != null) 
            {
                token = String.Equals("token", networkCredential.UserName) ? true : false;
            };

            if (token)
            {
                string credString = string.Format(":{0}", networkCredential.Password);
                byte[] byteArray = Encoding.ASCII.GetBytes(credString);

                _sessionClient = new HttpClient(handler);
                _sessionClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

            } else {

                handler.Credentials = networkCredential;
                
                _sessionClient = new HttpClient(handler);
            };

            _sessionClient.Timeout = TimeSpan.FromMinutes(10);
            _sessionClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgentString);

            _isNuGetRepo = String.Equals(Repository.Uri.AbsoluteUri, nugetRepoUri, StringComparison.InvariantCultureIgnoreCase);
            _isJFrogRepo = Repository.Uri.AbsoluteUri.ToLower().Contains("jfrog.io");
            _isGHPkgsRepo = Repository.Uri.AbsoluteUri.ToLower().Contains("pkg.github.com");
            _isMyGetRepo = Repository.Uri.AbsoluteUri.ToLower().Contains("myget.org");
        }

        #endregion

        #region Overriden Methods

        /// <summary>
        /// Find method which allows for searching for all packages from a repository and returns latest version for each.
        /// Not supported for V3 repository.
        /// </summary>
        public override FindResults FindAll(bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::FindAll()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find all is not supported for the V3 server protocol repository '{Repository.Name}'"),
                "FindAllFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag(s) from a repository and returns latest version for each.
        /// This is supported only for the NuGet repository special case, not other V3 repositories.
        /// </summary>
        public override FindResults FindTags(string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::FindTags()");
            if (_isNuGetRepo || _isJFrogRepo)
            {
                return FindTagsFromNuGetRepo(tags, includePrerelease, out errRecord);
            }
            else
            {
                errRecord = new ErrorRecord(
                    new InvalidOperationException($"Find by Tags is not supported for the V3 server protocol repository '{Repository.Name}'"),
                    "FindTagsFailure",
                    ErrorCategory.InvalidOperation,
                    this);

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }
        }

        /// <summary>
        /// Find method which allows for searching for packages with specified Command or DSCResource name.
        /// Not supported for V3 repository.
        /// </summary>
        public override FindResults FindCommandOrDscResource(string[] tags, bool includePrerelease, bool isSearchingForCommands, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::FindCommandOrDscResource()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find by CommandName or DSCResource is not supported for the V3 server protocol repository '{Repository.Name}'"),
                "FindCommandOrDscResourceFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "Newtonsoft.Json"
        /// We use the latest RegistrationBaseUrl version resource we can find and check if contains an entry with the package name.
        /// </summary>
        public override FindResults FindName(string packageName, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::FindName()");
            return FindNameHelper(packageName, tags: Utils.EmptyStrArray, includePrerelease, type, out errRecord);
        }

        /// <summary>
        /// Find method which allows for searching for single name and specified tag(s) and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "Newtonsoft.Json" -Tag "json"
        /// We use the latest RegistrationBaseUrl version resource we can find and check if contains an entry with the package name.
        /// </summary>
        public override FindResults FindNameWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::FindNameWithTag()");
            return FindNameHelper(packageName, tags, includePrerelease, type, out errRecord);
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and returns latest version.
        /// This is supported only for the NuGet repository special case, not other V3 repositories.
        /// </summary>
        public override FindResults FindNameGlobbing(string packageName, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::FindNameGlobbing()");
            if (_isNuGetRepo || _isJFrogRepo || _isGHPkgsRepo || _isMyGetRepo)
            {
                return FindNameGlobbingFromNuGetRepo(packageName, tags: Utils.EmptyStrArray, includePrerelease, out errRecord);
            }
            else
            {
                errRecord = new ErrorRecord(
                    new InvalidOperationException($"Find with Name containing wildcards is not supported for the V3 server protocol repository '{Repository.Name}'"),
                    "FindNameGlobbingFailure",
                    ErrorCategory.InvalidOperation,
                    this);

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and tag and returns latest version.
        /// This is supported only for the NuGet repository special case, not other V3 repositories.
        /// </summary>
        public override FindResults FindNameGlobbingWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::FindNameGlobbingWithTag()");
            if (_isNuGetRepo || _isJFrogRepo || _isGHPkgsRepo || _isMyGetRepo)
            {
                return FindNameGlobbingFromNuGetRepo(packageName, tags, includePrerelease, out errRecord);
            }
            else
            {
                errRecord = new ErrorRecord(
                    new InvalidOperationException($"Find with Name containing wildcards is not supported for the V3 server protocol repository '{Repository.Name}'"),
                    "FindNameGlobbingWithTagFailure",
                    ErrorCategory.InvalidOperation,
                    this);

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }
        }

        /// <summary>
        /// Find method which allows for searching for single name with version range.
        /// Name: no wildcard support
        /// Version: supports wildcards
        /// Examples: Search "NuGet.Server.Core" "[1.0.0.0, 5.0.0.0]"
        ///           Search "NuGet.Server.Core" "3.*"
        /// We use the latest RegistrationBaseUrl version resource we can find and check if contains an entry with the package name, then get all versions and match to satisfying versions.
        /// </summary>
        public override FindResults FindVersionGlobbing(string packageName, VersionRange versionRange, bool includePrerelease, ResourceType type, bool getOnlyLatest, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::FindVersionGlobbing()");
            string[] versionedResponses = GetVersionedPackageEntriesFromRegistrationsResource(packageName, catalogEntryProperty, isSearch: true, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            List<string> satisfyingVersions = new List<string>();
            foreach (string response in versionedResponses)
            {
                try
                {
                    using (JsonDocument pkgVersionEntry = JsonDocument.Parse(response))
                    {
                        JsonElement rootDom = pkgVersionEntry.RootElement;
                        if (!rootDom.TryGetProperty(versionName, out JsonElement pkgVersionElement))
                        {
                            errRecord = new ErrorRecord(
                                new InvalidOrEmptyResponse($"Response does not contain '{versionName}' element."),
                                "FindVersionGlobbingFailure",
                                ErrorCategory.InvalidData,
                                this);

                            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                        }

                        if (NuGetVersion.TryParse(pkgVersionElement.ToString(), out NuGetVersion pkgVersion) && versionRange.Satisfies(pkgVersion))
                        {
                            _cmdletPassedIn.WriteDebug($"Package version parsed as '{pkgVersion}' satisfies the version range");
                            if (!pkgVersion.IsPrerelease || includePrerelease)
                            {
                                satisfyingVersions.Add(response);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    errRecord = new ErrorRecord(
                        exception: e,
                        "FindVersionGlobbingFailure",
                        ErrorCategory.InvalidResult,
                        this);

                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                }
            }

            return new FindResults(stringResponse: satisfyingVersions.ToArray(), hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "NuGet.Server.Core" "3.0.0-beta"
        /// We use the latest RegistrationBaseUrl version resource we can find and check if contains an entry with the package name, then match to the specified version.
        /// </summary>
        public override FindResults FindVersion(string packageName, string version, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::FindVersion()");
            return FindVersionHelper(packageName, version, tags: Utils.EmptyStrArray, type, out errRecord);
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version and tag(s).
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "NuGet.Server.Core" "3.0.0-beta" -Tag "core"
        /// We use the latest RegistrationBaseUrl version resource we can find and check if contains an entry with the package name, then match to the specified version.
        /// </summary>
        public override FindResults FindVersionWithTag(string packageName, string version, string[] tags, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::FindVersionWithTag()");
            return FindVersionHelper(packageName, version, tags: tags, type, out errRecord);
        }

        /**  INSTALL APIS **/

        /// <summary>
        /// Installs a specific package.
        /// User may request to install package with or without providing version (as seen in examples below), but prior to calling this method the package is located and package version determined.
        /// Therefore, package version should not be null in this method.
        /// Name: no wildcard support.
        /// Examples: Install "PowerShellGet"
        ///           Install "PowerShellGet" -Version "3.0.0"
        /// </summary>
        public override Stream InstallPackage(string packageName, string packageVersion, bool includePrerelease, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::InstallPackage()");
            Stream results = new MemoryStream();
            if (string.IsNullOrEmpty(packageVersion))
            {
                errRecord = new ErrorRecord(
                    exception: new ArgumentNullException($"Package version could not be found for {packageName}"),
                    "PackageVersionNullOrEmptyError",
                    ErrorCategory.InvalidArgument,
                    _cmdletPassedIn);

                return results;
            }

            results = InstallVersion(packageName, packageVersion, out errRecord);
            return results;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Helper method called by FindNameGlobbing() and FindNameGlobbingWithTag() for special case where repository is NuGet.org repository.
        /// </summary>
        private FindResults FindNameGlobbingFromNuGetRepo(string packageName, string[] tags, bool includePrerelease, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::FindNameGlobbingFromNuGetRepo()");
            var names = packageName.Split(new char[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
            string querySearchTerm;

            if (names.Length == 0)
            {
                errRecord = new ErrorRecord(
                    new ArgumentException("-Name '*' for V3 server protocol repositories is not supported"),
                    "FindNameGlobbingFromNuGetRepoFailure",
                    ErrorCategory.InvalidArgument,
                    this);

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }
            if (names.Length == 1)
            {
                // packageName: *get*       -> q: get
                // packageName: PowerShell* -> q: PowerShell
                // packageName: *ShellGet   -> q: ShellGet
                querySearchTerm = names[0];
            }
            else
            {
                // *pow*get*
                // pow*get -> only support this (V2)
                // pow*get*
                // *pow*get
                errRecord = new ErrorRecord(
                    new ArgumentException("-Name with wildcards is only supported for scenarios similar to the following examples: PowerShell*, *ShellGet, *Shell*."),
                    "FindNameGlobbingFromNuGetRepoFailure",
                    ErrorCategory.InvalidArgument,
                    this);

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            var matchingPkgEntries = GetVersionedPackageEntriesFromSearchQueryResource(querySearchTerm, includePrerelease, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            List<string> matchingResponses = new List<string>();
            foreach (var pkgEntry in matchingPkgEntries)
            {
                string id = string.Empty;
                string latestVersion = string.Empty;
                string[] pkgTags = Utils.EmptyStrArray;

                try
                {
                    if (!pkgEntry.TryGetProperty(idName, out JsonElement idItem))
                    {
                        errRecord = new ErrorRecord(
                            new JsonParsingException("FindNameGlobbing(): Name element could not be found in response."),
                            "GetEntriesFromSearchQueryResourceFailure",
                            ErrorCategory.InvalidResult,
                            this);

                        return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                    }

                    if (!pkgEntry.TryGetProperty(tagsName, out JsonElement tagsItem))
                    {
                        errRecord = new ErrorRecord(
                            new JsonParsingException("FindNameGlobbing(): Tags element could not be found in response."),
                            "GetEntriesFromSearchQueryResourceFailure",
                            ErrorCategory.InvalidResult,
                            this);

                        return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                    }

                    id = idItem.ToString();
                    _cmdletPassedIn.WriteDebug($"Id for package that could be candidate for FindNameGlobbing found '{id}'");

                    // determine if id matches our wildcard criteria
                    if ((packageName.StartsWith("*") && packageName.EndsWith("*") && id.ToLower().Contains(querySearchTerm.ToLower())) ||
                        (packageName.EndsWith("*") && id.StartsWith(querySearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                        (packageName.StartsWith("*") && id.EndsWith(querySearchTerm, StringComparison.OrdinalIgnoreCase)))
                    {
                        _cmdletPassedIn.WriteDebug($"Id '{id}' matches wildcard search criteria");
                        bool isTagMatch = IsRequiredTagSatisfied(tagsItem, tags, out errRecord);
                        if (!isTagMatch)
                        {
                            continue;
                        }

                        matchingResponses.Add(pkgEntry.ToString());
                    }
                }

                catch (Exception e)
                {
                    errRecord = new ErrorRecord(
                        exception: e,
                        "GetEntriesFromSearchQueryResourceFailure",
                        ErrorCategory.InvalidResult,
                        this);

                    break;
                }
            }

            return new FindResults(stringResponse: matchingResponses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Helper method called by FindTags() for special case where repository is NuGet.org repository.
        /// </summary>
        private FindResults FindTagsFromNuGetRepo(string[] tags, bool includePrerelease, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::FindTagsFromNuGetRepo()");
            string tagsQueryTerm = $"tags:{String.Join(" ", tags)}";
            // Get responses for all packages that contain the required tags
            // example query:
            var tagPkgEntries = GetVersionedPackageEntriesFromSearchQueryResource(tagsQueryTerm, includePrerelease, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            if (tagPkgEntries.Count == 0)
            {
                errRecord = new ErrorRecord(
                    new ResourceNotFoundException($"Package with Tags '{String.Join(", ", tags)}' could not be found in repository '{Repository.Name}'."),
                    "PackageWithSpecifiedTagsNotFound",
                    ErrorCategory.ObjectNotFound,
                    this);

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            List<string> matchingPkgResponses = new List<string>();
            foreach (var pkgEntry in tagPkgEntries)
            {
                matchingPkgResponses.Add(pkgEntry.ToString());
            }

            return new FindResults(stringResponse: matchingPkgResponses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Helper method called by FindName() and FindNameWithTag()
        /// <summary>
        private FindResults FindNameHelper(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::FindNameHelper()");
            string[] versionedResponses = GetVersionedPackageEntriesFromRegistrationsResource(packageName, catalogEntryProperty, isSearch: true, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string latestVersionResponse = String.Empty;
            bool isTagMatch = true;
            foreach (string response in versionedResponses)
            {
                try
                {
                    using (JsonDocument pkgVersionEntry = JsonDocument.Parse(response))
                    {
                        JsonElement rootDom = pkgVersionEntry.RootElement;
                        if (!rootDom.TryGetProperty(versionName, out JsonElement pkgVersionElement))
                        {
                            errRecord = new ErrorRecord(
                                new InvalidOrEmptyResponse($"Response does not contain '{versionName}' element for search with Name '{packageName}' in '{Repository.Name}'."),
                                "FindNameFailure",
                                ErrorCategory.InvalidResult,
                                this);

                            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                        }
                        if (!rootDom.TryGetProperty(tagsName, out JsonElement tagsItem) && tags.Length != 0)
                        {
                            errRecord = new ErrorRecord(
                                new InvalidOrEmptyResponse($"Response does not contain '{tagsName}' element for search with Name '{packageName}' in '{Repository.Name}'."),
                                "FindNameFailure",
                                ErrorCategory.InvalidResult,
                                this);

                            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                        }

                        if (NuGetVersion.TryParse(pkgVersionElement.ToString(), out NuGetVersion pkgVersion))
                        {
                            _cmdletPassedIn.WriteDebug($"'{packageName}' version parsed as '{pkgVersion}'");
                            if (!pkgVersion.IsPrerelease || includePrerelease)
                            {
                                // Versions are always in descending order i.e 5.0.0, 3.0.0, 1.0.0 so grabbing the first match suffices
                                latestVersionResponse = response;
                                isTagMatch = IsRequiredTagSatisfied(tagsItem, tags, out errRecord);

                                break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    errRecord = new ErrorRecord(
                        exception: e,
                        "FindNameFailure",
                        ErrorCategory.InvalidResult,
                        this);

                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                }
            }

            if (String.IsNullOrEmpty(latestVersionResponse))
            {
                errRecord = new ErrorRecord(
                    new ResourceNotFoundException($"Package with name '{packageName}' could not be found in repository '{Repository.Name}'."),
                    "PackageNotFound",
                    ErrorCategory.ObjectNotFound,
                    this);

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            // Check and write error for tags matching requirement. If no tags were required the isTagMatch variable will be true.
            if (!isTagMatch)
            {
                if (errRecord == null)
                {
                    errRecord = new ErrorRecord(
                        new ResourceNotFoundException($"Package with name '{packageName}' and tags '{String.Join(", ", tags)}' could not be found in repository '{Repository.Name}'."),
                        "PackageNotFound",
                        ErrorCategory.ObjectNotFound,
                        this);
                }

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            return new FindResults(stringResponse: new string[] { latestVersionResponse }, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Helper method called by FindVersion() and FindVersionWithTag()
        /// </summary>
        private FindResults FindVersionHelper(string packageName, string version, string[] tags, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::FindVersionHelper()");
            if (!NuGetVersion.TryParse(version, out NuGetVersion requiredVersion))
            {
                errRecord = new ErrorRecord(
                    new ArgumentException($"Version {version} to be found is not a valid NuGet version."),
                    "FindNameFailure",
                    ErrorCategory.InvalidArgument,
                    this);

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }
            _cmdletPassedIn.WriteDebug($"'{packageName}' version parsed as '{requiredVersion}'");

            string[] versionedResponses = GetVersionedPackageEntriesFromRegistrationsResource(packageName, catalogEntryProperty, isSearch: true, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string latestVersionResponse = String.Empty;
            bool isTagMatch = true;
            foreach (string response in versionedResponses)
            {
                // Versions are always in descending order i.e 5.0.0, 3.0.0, 1.0.0
                try
                {
                    using (JsonDocument pkgVersionEntry = JsonDocument.Parse(response))
                    {
                        JsonElement rootDom = pkgVersionEntry.RootElement;
                        if (!rootDom.TryGetProperty(versionName, out JsonElement pkgVersionElement))
                        {
                            errRecord = new ErrorRecord(
                                new InvalidOrEmptyResponse($"Response does not contain '{versionName}' element for search with name '{packageName}' and version '{version}' in repository '{Repository.Name}'."),
                                "FindVersionFailure",
                                ErrorCategory.InvalidResult,
                                this);

                            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                        }
                        if (!rootDom.TryGetProperty(tagsName, out JsonElement tagsItem) && tags.Length != 0)
                        {
                            errRecord = new ErrorRecord(
                                new InvalidOrEmptyResponse($"Response does not contain '{tagsName}' element for search with name '{packageName}' and version '{version}' in repository '{Repository.Name}'."),
                                "FindVersionFailure",
                                ErrorCategory.InvalidResult,
                                this);

                            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                        }
                        if (NuGetVersion.TryParse(pkgVersionElement.ToString(), out NuGetVersion pkgVersion))
                        {
                            if (pkgVersion == requiredVersion)
                            {
                                latestVersionResponse = response;
                                isTagMatch = IsRequiredTagSatisfied(tagsItem, tags, out errRecord);

                                break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    errRecord = new ErrorRecord(
                        exception: e,
                        "FindVersionFailure",
                        ErrorCategory.InvalidResult,
                        this);

                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                }
            }

            if (String.IsNullOrEmpty(latestVersionResponse))
            {
                errRecord = new ErrorRecord(
                    new ResourceNotFoundException($"Package with name '{packageName}', version '{version}' could not be found in repository '{Repository.Name}'."),
                    "PackageNotFound",
                    ErrorCategory.ObjectNotFound,
                    this);

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            if (!isTagMatch)
            {
                if (errRecord == null)
                {
                    errRecord = new ErrorRecord(
                        new ResourceNotFoundException($"FindVersion(): Package with name '{packageName}', version '{version}' and tags '{String.Join(", ", tags)}' could not be found in repository '{Repository.Name}'."),
                        "PackageNotFound",
                        ErrorCategory.ObjectNotFound,
                        this);
                }

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            return new FindResults(stringResponse: new string[] { latestVersionResponse }, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Installs specific package.
        /// Name: no wildcard support.
        /// Examples: Install "Newtonsoft.json"
        /// </summary>
        private Stream InstallName(string packageName, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::InstallName()");
            return InstallHelper(packageName, version: null, out errRecord);
        }

        /// <summary>
        /// Installs package with specific name and version.
        /// Name: no wildcard support.
        /// Version: no wildcard support.
        /// Examples: Install "Newtonsoft.json" -Version "1.0.0.0"
        ///           Install "Newtonsoft.json" -Version "2.5.0-beta"
        /// </summary>
        private Stream InstallVersion(string packageName, string version, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::InstallVersion()");
            if (!NuGetVersion.TryParse(version, out NuGetVersion requiredVersion))
            {
                errRecord = new ErrorRecord(
                    new ArgumentException($"Version {version} to be installed is not a valid NuGet version."),
                    "InstallVersionFailure",
                    ErrorCategory.InvalidArgument,
                    this);

                return null;
            }

            return InstallHelper(packageName, requiredVersion, out errRecord);
        }

        /// <summary>
        /// Helper method that is called by InstallName() and InstallVersion()
        /// For InstallName() we want latest version installed (so version parameter passed in will be null), for InstallVersion() we want specified, non-null version installed.
        /// </summary>
        private Stream InstallHelper(string packageName, NuGetVersion version, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::InstallHelper()");
            Stream pkgStream = null;
            bool getLatestVersion = true;
            if (version != null)
            {
                getLatestVersion = false;
            }

            string[] versionedResponses = GetVersionedPackageEntriesFromRegistrationsResource(packageName, packageContentProperty, isSearch: false, out errRecord);
            if (errRecord != null)
            {
                return pkgStream;
            }

            if (versionedResponses.Length == 0)
            {
                errRecord = new ErrorRecord(
                    new Exception($"Package with name '{packageName}' and version '{version}' could not be found in repository '{Repository.Name}'"),
                    "InstallFailure",
                    ErrorCategory.InvalidResult,
                    this);

                return null;
            }

            string pkgContentUrl = String.Empty;
            if (getLatestVersion)
            {
                pkgContentUrl = versionedResponses[0];
            }
            else
            {
                // loop through responses to find one containing required version
                foreach (string response in versionedResponses)
                {
                    // Response will be "packageContent" element value that looks like: "{packageBaseAddress}/{packageName}/{normalizedVersion}/{packageName}.{normalizedVersion}.nupkg"
                    // Ex: https://api.nuget.org/v3-flatcontainer/test_module/1.0.0/test_module.1.0.0.nupkg
                    if (response.Contains(version.ToNormalizedString()))
                    {
                        pkgContentUrl = response;
                        break;
                    }
                }
            }

            if (String.IsNullOrEmpty(pkgContentUrl))
            {
                errRecord = new ErrorRecord(
                    new Exception($"Package with name '{packageName}' and version '{version}' could not be found in repository '{Repository.Name}'"),
                    "InstallFailure",
                    ErrorCategory.InvalidResult,
                    this);

                return null;
            }

            var content = HttpRequestCallForContent(pkgContentUrl, out errRecord);
            if (errRecord != null)
            {
                return null;
            }

            if (content is null)
            {
                errRecord = new ErrorRecord(
                    new Exception($"No content was returned by repository '{Repository.Name}'"),
                    "InstallFailureContentNullv3",
                    ErrorCategory.InvalidResult,
                    this);

                return null;
            }

            return content.ReadAsStreamAsync().Result;
        }

        /// <summary>
        /// Gets the versioned package entries from the RegistrationsBaseUrl resource
        /// i.e when the package Name being searched for does not contain wildcard
        /// This is called by FindNameHelper(), FindVersionHelper(), FindVersionGlobbing(), InstallHelper()
        /// </summary>
        private string[] GetVersionedPackageEntriesFromRegistrationsResource(string packageName, string propertyName, bool isSearch, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::GetVersionedPackageEntriesFromRegistrationsResource()");
            string[] responses = Utils.EmptyStrArray;
            Dictionary<string, string> resources = GetResourcesFromServiceIndex(out errRecord);
            if (errRecord != null)
            {
                return responses;
            }

            string registrationsBaseUrl = FindRegistrationsBaseUrl(resources, out errRecord);
            if (errRecord != null)
            {
                return responses;
            }

            responses = GetVersionedResponsesFromRegistrationsResource(registrationsBaseUrl, packageName, propertyName, isSearch, out errRecord);
            if (errRecord != null)
            {
                return Utils.EmptyStrArray;
            }

            return responses;
        }

        /// <summary>
        /// Gets the versioned package entries from SearchQueryService resource
        /// i.e when the package Name being searched for contains wildcards or a Tag query search is performed
        /// This is called by FindNameGlobbingFromNuGetRepo() and FindTagsFromNuGetRepo()
        /// </summary>
        private List<JsonElement> GetVersionedPackageEntriesFromSearchQueryResource(string queryTerm, bool includePrerelease, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::GetVersionedPackageEntriesFromSearchQueryResource()");
            List<JsonElement> pkgEntries = new();
            Dictionary<string, string> resources = GetResourcesFromServiceIndex(out errRecord);
            if (errRecord != null)
            {
                return pkgEntries;
            }

            string searchQueryServiceUrl = FindSearchQueryService(resources, out errRecord);
            if (errRecord != null)
            {
                return pkgEntries;
            }

            // Get initial response
            int skip = 0;
            string query = $"{searchQueryServiceUrl}?q={queryTerm}&prerelease={includePrerelease}&semVerLevel=2.0.0&skip={skip}&take=100";

            // Get responses for all packages that contain the required tags
            pkgEntries.AddRange(GetJsonElementArr(query, dataName, out int initialCount, out errRecord).ToList());

            // check count (ie "totalHits") 425 ==> count/100  ~~> 4 calls ~~> + 1 = 5 calls
            int count = initialCount / 100 + 1;
            // if more than 100 count, loop and add response to list
            while (count > 0)
            {
                skip += 100;
                query = $"{searchQueryServiceUrl}?q={queryTerm}&prerelease={includePrerelease}&semVerLevel=2.0.0&skip={skip}&take=100";
                pkgEntries.AddRange(GetJsonElementArr(query, dataName, out int unneededCount, out errRecord).ToList());
                count--;
            }

            return pkgEntries;
        }

        /// <summary>
        /// Finds all resources present in the repository's service index.
        /// For example: https://api.nuget.org/v3/index.json
        /// </summary>
        private Dictionary<string, string> GetResourcesFromServiceIndex(out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::GetResourcesFromServiceIndex()");
            Dictionary<string, string> resources = new Dictionary<string, string>();
            JsonElement[] resourcesArray = GetJsonElementArr($"{Repository.Uri}", resourcesName, out int totalHits, out errRecord);
            if (errRecord != null)
            {
                return resources;
            }

            foreach (JsonElement resource in resourcesArray)
            {
                try
                {
                    if (!resource.TryGetProperty("@type", out JsonElement typeElement))
                    {
                        errRecord = new ErrorRecord(
                            new JsonParsingException($"@type element not found for resource in service index for repository '{Repository.Name}'"), "GetResourcesFromServiceIndexFailure",
                            ErrorCategory.InvalidResult,
                            this);

                        return new Dictionary<string, string>();
                    }

                    if (!resource.TryGetProperty("@id", out JsonElement idElement))
                    {
                        errRecord = new ErrorRecord(
                            new JsonParsingException($"@id element not found for resource in service index for repository '{Repository.Name}'"),
                            "GetResourcesFromServiceIndexFailure",
                            ErrorCategory.InvalidResult,
                            this);

                        return new Dictionary<string, string>();
                    }

                    if (!resources.ContainsKey(typeElement.ToString()))
                    {
                        // Some resources have a primary and secondary entry. The @id value is the same, so we only choose the primary entry.
                        resources.Add(typeElement.ToString(), idElement.ToString());
                    }
                }
                catch (Exception e)
                {
                    errRecord = new ErrorRecord(
                        new Exception($"Exception parsing service index JSON for respository '{Repository.Name}' with error: {e.Message}"),
                        "GetResourcesFromServiceIndexFailure",
                        ErrorCategory.InvalidResult,
                        this);

                    return new Dictionary<string, string>();
                }
            }

            return resources;
        }

        /// <summary>
        /// Gets the resource of type "RegistrationBaseUrl" from the repository's resources.
        /// A repository can have multiple resources of type "RegistrationsBaseUrl" so it finds the best match according to the guideline comment in the method.
        /// </summary>
        private string FindRegistrationsBaseUrl(Dictionary<string, string> resources, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::FindRegistrationsBaseUrl()");
            errRecord = null;
            string registrationsBaseUrl = String.Empty;

            /**
            If RegistrationsBaseUrl/3.6.0 exists, use RegistrationsBaseUrl/3.6.0
            Otherwise, if RegistrationsBaseUrl/3.4.0 exists, use RegistrationsBaseUrl/3.4.0
            Otherwise, if RegistrationsBaseUrl/3.0.0-rc exists, use RegistrationsBaseUrl/3.0.0-rc
            Otherwise, if RegistrationsBaseUrl/3.0.0-beta exists, use RegistrationsBaseUrl/3.0.0-beta
            Otherwise, if RegistrationsBaseUrl exists, use RegistrationsBaseUrl
            Otherwise, report an error
            */

            if (resources.ContainsKey("RegistrationsBaseUrl/3.6.0"))
            {
                registrationsBaseUrl = resources["RegistrationsBaseUrl/3.6.0"];
            }
            else if (resources.ContainsKey("RegistrationsBaseUrl/3.4.0"))
            {
                registrationsBaseUrl = resources["RegistrationsBaseUrl/3.4.0"];
            }
            else if (resources.ContainsKey("RegistrationsBaseUrl/3.0.0-rc"))
            {
                registrationsBaseUrl = resources["RegistrationsBaseUrl/3.0.0-rc"];
            }
            else if (resources.ContainsKey("RegistrationsBaseUrl/3.0.0-beta"))
            {
                registrationsBaseUrl = resources["RegistrationsBaseUrl/3.0.0-beta"];
            }
            else if (resources.ContainsKey("RegistrationsBaseUrl"))
            {
                registrationsBaseUrl = resources["RegistrationsBaseUrl"];
            }
            else
            {
                errRecord = new ErrorRecord(
                    new ResourceNotFoundException($"RegistrationBaseUrl resource could not be found for repository '{Repository.Name}'"),
                    "FindRegistrationsBaseUrlFailure",
                    ErrorCategory.InvalidResult,
                    this);
            }

            return registrationsBaseUrl;
        }

        /// <summary>
        /// Gets the resource of type "SearchQueryService" from the repository's resources.
        /// A repository can have multiple resources of type "SearchQueryService" so it finds the best match according to the guideline comment in the method.
        /// </summary>
        private string FindSearchQueryService(Dictionary<string, string> resources, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::FindSearchQueryService()");
            errRecord = null;
            string searchQueryServiceUrl = String.Empty;

            if (resources.ContainsKey("SearchQueryService/3.5.0"))
            {
                searchQueryServiceUrl = resources["SearchQueryService/3.5.0"];
            }
            else if (resources.ContainsKey("SearchQueryService/3.0.0-rc"))
            {
                searchQueryServiceUrl = resources["SearchQueryService/3.0.0-rc"];
            }
            else if (resources.ContainsKey("SearchQueryService/3.0.0-beta"))
            {
                searchQueryServiceUrl = resources["SearchQueryService/3.0.0-beta"];
            }
            else if (resources.ContainsKey("SearchQueryService"))
            {
                searchQueryServiceUrl = resources["SearchQueryService"];
            }
            else
            {
                errRecord = new ErrorRecord(
                    new ResourceNotFoundException($"SearchQueryService resource could not be found for Repository '{Repository.Name}'"),
                    "FindSearchQueryServiceFailure",
                    ErrorCategory.InvalidResult,
                    this);
            }

            return searchQueryServiceUrl;
        }

        /// <summary>
        /// For some packages (that we know of: JFrog repo and some packages on NuGet.org), the metadata is located under outer "items" element > "@id" element > inner "items" element
        /// This requires a different search.
        /// </summary>
        private JsonElement[] GetMetadataElementFromIdLinkElement(JsonElement idLinkElement, string packageName, out string upperVersion, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::GetMetadataElementFromIdLinkElement()");
            upperVersion = String.Empty;
            JsonElement[] innerItems = new JsonElement[]{};
            List<JsonElement> innerItemsList = new List<JsonElement>();

            string metadataUri = idLinkElement.ToString();
            string response = HttpRequestCall(metadataUri, out errRecord);
            if (errRecord != null)
            {
                if (errRecord.Exception is ResourceNotFoundException) {
                    errRecord = new ErrorRecord(
                        new ResourceNotFoundException($"Package with name '{packageName}' could not be found in repository '{Repository.Name}'.", errRecord.Exception),
                        "PackageNotFound",
                        ErrorCategory.ObjectNotFound,
                        this);
                }

                return innerItems;
            }

            try
            {
                using (JsonDocument metadataEntries = JsonDocument.Parse(response))
                {
                    JsonElement rootDom = metadataEntries.RootElement;
                    if (!rootDom.TryGetProperty(itemsName, out JsonElement innerItemsElement))
                    {
                        errRecord = new ErrorRecord(
                            new ResourceNotFoundException($"'{itemsName}' element for package with name '{packageName}' could not be found in JFrog repository '{Repository.Name}'"),
                            "GetElementForJFrogRepoFailure",
                            ErrorCategory.InvalidResult,
                            this);

                        return innerItems;
                    }

                    if (rootDom.TryGetProperty("upper", out JsonElement upperVerElement))
                    {
                        upperVersion = upperVerElement.ToString();
                    }
                    else
                    {
                        _cmdletPassedIn.WriteDebug($"Package with name '{packageName}' did not have 'upper' property so package versions may not be in descending order.");
                    }

                    foreach(JsonElement entry in innerItemsElement.EnumerateArray())
                    {
                        // add clone, otherwise this JsonElement will be out of scope to the caller once JsonDocument is disposed
                        innerItemsList.Add(entry.Clone());
                    }
                }
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "MetadataElementForIdElementRetrievalFailure",
                    ErrorCategory.InvalidResult,
                    this);
            }

            return innerItemsList.ToArray();
        }

        /// <summary>
        ///  For most packages returned from V3 server protocol responses, the metadata is located under outer "items" element > inner "items" element.
        /// </summary>
        private JsonElement[] GetMetadataElementFromItemsElement(JsonElement itemsElement, string packageName, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::GetMetadataElementFromItemsElement()");
            errRecord = null;
            List<JsonElement> innerItemsList = new List<JsonElement>();

            try
            {
                foreach (JsonElement entry in itemsElement.EnumerateArray())
                {
                    // add clone, otherwise this JsonElement will be out of scope to the caller once JsonDocument is disposed
                    innerItemsList.Add(entry.Clone());
                }
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "MetadataElementForItemsElementRetrievalFailure",
                    ErrorCategory.InvalidResult,
                    this);
            }

            return innerItemsList.ToArray();
        }

        /// <summary>
        ///  Uses the response returned from the registrations based url query, and gets the package's entries.
        ///  For package metadata responses returned from the V3 server protocol, the metadata can either be located:
        ///  under outer "items" element > inner "items" element (for which we call helper method GetMetadataElementFromItemsElement()), OR
        ///  under outer "items" element > "@Id" element > inner "items" element (for which we call helper method GetMetadataElementFromIdLinkElement)
        /// </summary>
        private string[] GetMetadataElementsFromResponse(string response, string property, string packageName, out string upperVersion, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::GetMetadataElementsFromResponse()");
            errRecord = null;
            upperVersion = String.Empty;
            List<string> versionedPkgResponses = new List<string>();

            try
            {
                // parse out JSON response we get from RegistrationsUrl
                using (JsonDocument pkgVersionEntry = JsonDocument.Parse(response))
                {
                    // The response has a outer "items" array element, which can have multiple elements but should have at least 1.
                    JsonElement rootDom = pkgVersionEntry.RootElement;
                    if (!rootDom.TryGetProperty(itemsName, out JsonElement itemsElement) || itemsElement.GetArrayLength() == 0)
                    {
                        errRecord = new ErrorRecord(
                            new ArgumentException($"Response does not contain '{itemsName}' element for package with name '{packageName}' from repository '{Repository.Name}'."),
                            "GetResponsesFromRegistrationsResourceFailure",
                            ErrorCategory.InvalidResult,
                            this);

                        return Utils.EmptyStrArray;
                    }

                    int outerItemsArrayCount = itemsElement.GetArrayLength();
                    // this will be a list of the inner most items elements (i.e containing the package metadata metadata)
                    List<JsonElement> innerItemsElements = new List<JsonElement>();

                    // Loop through outer "items" array and get inner "items" entries based on how data is structured.
                    for (int i = 0; i < outerItemsArrayCount; i++)
                    {
                        JsonElement currentItem = itemsElement[i];
                        if (currentItem.TryGetProperty(itemsName, out JsonElement currentInnerItemsElement))
                        {
                            // Scenarios: NuGet.org majority responses
                            JsonElement[] innerItemsFromItemsElement = GetMetadataElementFromItemsElement(currentInnerItemsElement, packageName, out errRecord);
                            if (errRecord != null)
                            {
                                continue;
                            }

                            if (currentItem.TryGetProperty("upper", out JsonElement upperVersionElement))
                            {
                                upperVersion = upperVersionElement.ToString();
                            }
                            else
                            {
                                _cmdletPassedIn.WriteDebug($"Package with name '{packageName}' did not have 'upper' property so package versions may not be in descending order.");
                            }

                            innerItemsElements.AddRange(innerItemsFromItemsElement);
                        }
                        else if (currentItem.TryGetProperty(idLinkName, out JsonElement idLinkElement))
                        {
                            // Scenarios: JFrog responses, some NuGet.org responses
                            JsonElement[] innerItemsFromIdElement = GetMetadataElementFromIdLinkElement(idLinkElement, packageName, out upperVersion, out errRecord);
                            if (errRecord != null)
                            {
                                continue;
                            }

                            innerItemsElements.AddRange(innerItemsFromIdElement);
                        }
                        else
                        {
                            _cmdletPassedIn.WriteDebug($"Metadata for package with name '{packageName}' did not have inner 'items' or '@Id' properties.");
                        }
                    }

                    // Loop through inner "items" entries we collected, and get the specific entry for each package version
                    foreach (var item in innerItemsElements)
                    {
                        if (!item.TryGetProperty(property, out JsonElement metadataElement))
                        {
                            errRecord = new ErrorRecord(
                                new ArgumentException($"Response does not contain inner '{property}' element for package '{packageName}' from repository '{Repository.Name}'."),
                                "GetResponsesFromRegistrationsResourceFailure",
                                ErrorCategory.InvalidResult,
                                this);

                            continue;
                        }

                        if (metadataElement.ValueKind == JsonValueKind.String)
                        {
                            // This is when property is "packageContent"
                            versionedPkgResponses.Add(metadataElement.ToString());
                        }
                        else if(metadataElement.ValueKind == JsonValueKind.Object)
                        {
                            // This is when property is "catalogEntry"
                            // If metadata has a "listed" property, but it's set to false, skip this package version
                            if (metadataElement.TryGetProperty("listed", out JsonElement listedElement))
                            {
                                if (bool.TryParse(listedElement.ToString(), out bool listed) && !listed)
                                {
                                    continue;
                                }
                            }

                            versionedPkgResponses.Add(metadataElement.ToString());
                        }
                        else
                        {
                            _cmdletPassedIn.WriteDebug($"Metadata for package with name '{packageName}' was not of value kind type string or object.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "GetResponsesFromRegistrationsResourceFailure",
                    ErrorCategory.InvalidResult,
                    this);

                return Utils.EmptyStrArray;
            }

            return versionedPkgResponses.ToArray();
        }

        /// <summary>
        /// Helper method iterates through the entries in the registrationsUrl for a specific package and all its versions.
        /// This contains an inner items element (containing the package metadata) and the packageContent element (containing URI through which the .nupkg can be downloaded)
        /// <param name="property"> This can be the "catalogEntry" or "packageContent" property.
        ///     The "catalogEntry" property is used for search, and the value is package metadata.
        ///     The "packageContent" property is used for download, and the value is a URI for the .nupkg file.
        /// </param>
        /// <summary>
        private string[] GetVersionedResponsesFromRegistrationsResource(string registrationsBaseUrl, string packageName, string property, bool isSearch, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::GetVersionedResponsesFromRegistrationsResource()");
            List<string> versionedResponses = new List<string>();
            var requestPkgMapping = registrationsBaseUrl.EndsWith("/") ? $"{registrationsBaseUrl}{packageName.ToLower()}/index.json" : $"{registrationsBaseUrl}/{packageName.ToLower()}/index.json";

            string pkgMappingResponse = HttpRequestCall(requestPkgMapping, out errRecord);
            if (errRecord != null)
            {
                if (errRecord.Exception is ResourceNotFoundException)
                {
                    errRecord = new ErrorRecord(
                        new ResourceNotFoundException($"Package with name '{packageName}' could not be found in repository '{Repository.Name}'.", errRecord.Exception),
                        "PackageNotFound",
                        ErrorCategory.ObjectNotFound,
                        this);
                }

                return Utils.EmptyStrArray;
            }

            string upperVersion = String.Empty;
            string[] versionedResponseArr = GetMetadataElementsFromResponse(pkgMappingResponse, property, packageName, out upperVersion, out errRecord);
            if (errRecord != null)
            {
                return Utils.EmptyStrArray;
            }

            // Reverse array of versioned responses, if needed, so that version entries are in descending order.
            if (String.IsNullOrEmpty(upperVersion))
            {
                // add write Debug and use these results
                return versionedResponseArr;
            }

            if (isSearch)
            {
                if (!IsLatestVersionFirstForSearch(versionedResponseArr, out errRecord))
                {
                    Array.Reverse(versionedResponseArr);
                }
            }
            else
            {
                if (!IsLatestVersionFirstForInstall(versionedResponseArr, upperVersion, out errRecord))
                {
                    Array.Reverse(versionedResponseArr);
                }
            }

            return versionedResponseArr;
        }

        /// <summary>
        /// Returns true if the metadata entries are arranged in descending order with respect to the package's version.
        /// ADO feeds usually return version entries in descending order, but Nuget.org repository returns them in ascending order.
        /// Package versions will reflect prerelease preference, but upper version and lower version would not so we don't use them for comparision.
        /// </summary>
        private bool IsLatestVersionFirstForSearch(string[] versionedResponses, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::IsLatestVersionFirstForSearch()");
            errRecord = null;
            bool latestVersionFirst = true;
            int versionResponsesCount = versionedResponses.Length;
            // We don't need to perform this check if no responses, or single response
            if (versionResponsesCount < 2)
            {
                return latestVersionFirst;
            }

            string firstResponse = versionedResponses[0];
            string lastResponse = versionedResponses[versionResponsesCount - 1];
            NuGetVersion firstPkgVersion;
            NuGetVersion lastPkgVersion;

            try
            {
                using (JsonDocument firstResponseJson = JsonDocument.Parse(firstResponse))
                {
                    JsonElement firstResponseDom = firstResponseJson.RootElement;
                    if (!firstResponseDom.TryGetProperty(versionName, out JsonElement firstVersionElement))
                    {
                        errRecord = new ErrorRecord(
                            new JsonParsingException($"Response did not contain '{versionName}' element"),
                            "FirstVersionFirstSearchFailure",
                            ErrorCategory.InvalidResult,
                            this);

                        return latestVersionFirst;
                    }


                    string firstVersion = firstVersionElement.ToString();
                    if (!NuGetVersion.TryParse(firstVersion, out firstPkgVersion))
                    {
                        errRecord = new ErrorRecord(
                            new JsonParsingException($"First version '{firstVersion}' could not be parsed into NuGetVersion."),
                            "FirstVersionFirstSearchFailure",
                            ErrorCategory.InvalidResult,
                            this);

                        return latestVersionFirst;
                    }
                }

                using (JsonDocument lastResponseJson = JsonDocument.Parse(lastResponse))
                {
                    JsonElement lastResponseDom = lastResponseJson.RootElement;
                    if (!lastResponseDom.TryGetProperty(versionName, out JsonElement lastVersionElement))
                    {
                        errRecord = new ErrorRecord(
                            new JsonParsingException($"Response did not contain '{versionName}' element"),
                            "LatestVersionFirstSearchFailure",
                            ErrorCategory.InvalidResult,
                            this);

                        return latestVersionFirst;
                    }


                    string lastVersion = lastVersionElement.ToString();
                    if (!NuGetVersion.TryParse(lastVersion, out lastPkgVersion))
                    {
                        errRecord = new ErrorRecord(
                            new JsonParsingException($"Last version '{lastVersion}' could not be parsed into NuGetVersion."),
                            "LatestVersionFirstSearchFailure",
                            ErrorCategory.InvalidResult,
                            this);

                        return latestVersionFirst;
                    }
                }

                if (firstPkgVersion < lastPkgVersion)
                {
                    latestVersionFirst = false;
                }
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "LatestVersionFirstSearchFailure",
                    ErrorCategory.InvalidResult,
                    this);

                return true;
            }

            return latestVersionFirst;
        }

        /// <summary>
        /// Returns true if the nupkg URI entries for each package version are arranged in descending order with respect to the package's version.
        /// ADO feeds usually return version entries in descending order, but Nuget.org repository returns them in ascending order.
        /// Entries do not reflect prerelease preference so all versions (including prerelease) are being considered here, so upper version (including prerelease) can be used for comparision.
        /// </summary>
        private bool IsLatestVersionFirstForInstall(string[] versionedResponses, string upperVersion, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::IsLatestVersionFirstForInstall()");
            errRecord = null;
            bool latestVersionFirst = true;

            // We don't need to perform this check if no responses, or single response
            if (versionedResponses.Length < 2)
            {
                return latestVersionFirst;
            }

            string firstResponse = versionedResponses[0];
            // for Install, response will be a URI value for the package .nupkg, not JSON
            if (!firstResponse.Contains(upperVersion))
            {
                latestVersionFirst = false;
            }

            return latestVersionFirst;
        }

        /// <summary>
        /// Helper method that determines if specified tags are present in package's tags.
        /// </summary>
        private bool IsRequiredTagSatisfied(JsonElement tagsElement, string[] tags, out ErrorRecord errRecord)
        {
            errRecord = null;
            string[] pkgTags = Utils.EmptyStrArray;

            // Get the package's tags from the tags JsonElement
            try
            {
                if (tagsElement.ValueKind == JsonValueKind.Array)
                {
                    var arrayLength = tagsElement.GetArrayLength();
                    List<string> tagsFound = new List<string>(arrayLength);
                    foreach (JsonElement tagItem in tagsElement.EnumerateArray())
                    {
                        tagsFound.Add(tagItem.ToString());
                    }

                    pkgTags = tagsFound.ToArray();
                }
                else if (tagsElement.ValueKind == JsonValueKind.String)
                {
                    string tagStr = tagsElement.ToString();
                    pkgTags = tagStr.Split(Utils.WhitespaceSeparator, StringSplitOptions.RemoveEmptyEntries);
                }
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "GetResponsesFromRegistrationsResourceFailure",
                    ErrorCategory.InvalidResult,
                    this);

                return false;
            }

            // determine if all required tags are present within package's tags.
            bool isTagMatch = true;
            foreach (string requiredTag in tags)
            {
                if (!pkgTags.Contains(requiredTag, StringComparer.OrdinalIgnoreCase))
                {
                    isTagMatch = false;
                    break;
                }
            }

            return isTagMatch;
        }

        /// <summary>
        /// Helper method that parses response for given property and returns result for that property as a JsonElement array.
        /// </summary>
        private JsonElement[] GetJsonElementArr(string request, string propertyName, out int totalHits, out ErrorRecord errRecord)
        {
            List<JsonElement> responseEntries = new List<JsonElement>();
            JsonElement[] entries = new JsonElement[0];
            totalHits = 0;
            try
            {
                string response = HttpRequestCall(request, out errRecord);
                if (errRecord != null)
                {
                    return new JsonElement[]{};
                }

                using (JsonDocument pkgsDom = JsonDocument.Parse(response))
                {
                    pkgsDom.RootElement.TryGetProperty(propertyName, out JsonElement entryElement);
                    foreach (JsonElement entry in entryElement.EnumerateArray())
                    {
                        // return clone, otherwise this JsonElement will be out of scope to the caller once JsonDocument is disposed
                        responseEntries.Add(entry.Clone());
                    }

                    int reportedHits = 0;
                    if (pkgsDom.RootElement.TryGetProperty("totalHits", out JsonElement totalHitsElement))
                    {
                        int.TryParse(totalHitsElement.ToString(), out reportedHits);
                    }

                    // MyGet.org repository responses from SearchQueryService have a bug where the totalHits property int returned is 1000 + actual number of hits
                    // so reduce totalHits by 1000 iff MyGet repository
                    totalHits = _isMyGetRepo && reportedHits >= myGetTotalHitsBuffer ? reportedHits - myGetTotalHitsBuffer : reportedHits;
                    entries = responseEntries.ToArray();
                }
            }
            catch (JsonException e)
            {
                // scenario where the feed is not active anymore, i.e confirmed for JFrogArtifactory. The default error message is not intuitive.
                errRecord = new ErrorRecord(
                    exception: new Exception($"JSON response from repository {Repository.Name} could not be parsed, likely due to the feed being inactive or invalid, with inner exception: {e.Message}"),
                    "FindVersionGlobbingFailure",
                    ErrorCategory.InvalidResult,
                    this);
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "GetResponsesFromRegistrationsResourceFailure",
                    ErrorCategory.InvalidResult,
                    this);
            }

            return entries;
        }

        /// <summary>
        /// Helper method that makes the HTTP request for the V3 server protocol url passed in for find APIs.
        /// </summary>
        private string HttpRequestCall(string requestUrlV3, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::HttpRequestCall()");
            errRecord = null;
            string response = string.Empty;

            try
            {
                _cmdletPassedIn.WriteDebug($"Request url is '{requestUrlV3}'");
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV3);

                response = SendV3RequestAsync(request, _sessionClient).GetAwaiter().GetResult();
            }
            catch (ResourceNotFoundException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "ResourceNotFound",
                    ErrorCategory.InvalidResult,
                    this);
            }
            catch (UnauthorizedException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "UnauthorizedRequest",
                    ErrorCategory.InvalidResult,
                    this);
            }
            catch (HttpRequestException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "HttpRequestCallFailure",
                    ErrorCategory.InvalidResult,
                    this);
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "HttpRequestCallFailure",
                    ErrorCategory.InvalidResult,
                    this);
            }

            return response;
        }

        /// <summary>
        /// Helper method that makes the HTTP request for the V3 server protocol url passed in for install APIs.
        /// </summary>
        private HttpContent HttpRequestCallForContent(string requestUrlV3, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V3ServerAPICalls::HttpRequestCallForContent()");
            errRecord = null;
            HttpContent content = null;
            try
            {
                _cmdletPassedIn.WriteDebug($"Request url is '{requestUrlV3}'");
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV3);

                content = SendV3RequestForContentAsync(request, _sessionClient).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "HttpRequestCallForContentFailure",
                    ErrorCategory.InvalidResult,
                    this);
            }

            if (string.IsNullOrEmpty(content.ToString()))
            {
                _cmdletPassedIn.WriteDebug("Response is empty");
            }

            return content;
        }

        /// <summary>
        /// Helper method called by HttpRequestCall() that makes the HTTP request for string response.
        /// </summary>
        private static async Task<string> SendV3RequestAsync(HttpRequestMessage message, HttpClient s_client)
        {
            HttpStatusCode responseStatusCode = HttpStatusCode.OK;
            try
            {
                HttpResponseMessage response = await s_client.SendAsync(message);
                responseStatusCode = response.StatusCode;
                response.EnsureSuccessStatusCode();

                var responseStr = await response.Content.ReadAsStringAsync();

                return responseStr;
            }
            catch (HttpRequestException e)
            {
                if (responseStatusCode.Equals(HttpStatusCode.NotFound)) {
                    throw new ResourceNotFoundException(Utils.FormatRequestsExceptions(e, message));
                }
                // ADO feed will return a 401 if a package does not exist on the feed, with the following message:
                // 401 (Unauthorized - No local versions of package 'NonExistentModule'; please provide authentication to access
                // versions from upstream that have not yet been saved to your feed. (DevOps Activity ID: 5E5CF528-5B3D-481D-95B5-5DDB5476D7EF))
                if (responseStatusCode.Equals(HttpStatusCode.Unauthorized))
                {
                    if (e.Message.Contains("access versions from upstream that have not yet been saved to your feed"))
                    {
                        throw new ResourceNotFoundException(Utils.FormatRequestsExceptions(e, message));
                    }

                    throw new UnauthorizedException(Utils.FormatCredentialRequestExceptions(e));
                }

                throw new HttpRequestException(Utils.FormatRequestsExceptions(e, message));
            }
            catch (ArgumentNullException e)
            {
                throw new ArgumentNullException(Utils.FormatRequestsExceptions(e, message));
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException(Utils.FormatRequestsExceptions(e, message));
            }
        }

        /// <summary>
        /// Helper method called by HttpRequestCallForContent() that makes the HTTP request for string response.
        /// </summary>
        private static async Task<HttpContent> SendV3RequestForContentAsync(HttpRequestMessage message, HttpClient s_client)
        {
            HttpStatusCode responseStatusCode = HttpStatusCode.OK;
            try
            {
                HttpResponseMessage response = await s_client.SendAsync(message);
                responseStatusCode = response.StatusCode;
                response.EnsureSuccessStatusCode();

                return response.Content;
            }
            catch (HttpRequestException e)
            {
                if (responseStatusCode.Equals(HttpStatusCode.NotFound))
                {
                    throw new ResourceNotFoundException(Utils.FormatRequestsExceptions(e, message));
                }
                if (responseStatusCode.Equals(HttpStatusCode.Unauthorized))
                {
                    throw new UnauthorizedException(Utils.FormatCredentialRequestExceptions(e));
                }

                throw new HttpRequestException(Utils.FormatRequestsExceptions(e, message));
            }
            catch (ArgumentNullException e)
            {
                throw new ArgumentNullException(Utils.FormatRequestsExceptions(e, message));
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException(Utils.FormatRequestsExceptions(e, message));
            }
        }

        #endregion
    }
}
