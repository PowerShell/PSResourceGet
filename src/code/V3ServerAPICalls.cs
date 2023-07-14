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
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections;
using System.Runtime.ExceptionServices;
using System.Management.Automation;
using System.Numerics;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    internal class V3ServerAPICalls : ServerApiCall
    {
        #region Members
        public override PSRepositoryInfo Repository { get; set; }
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
        private static readonly string countName = "count";
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

        public V3ServerAPICalls(PSRepositoryInfo repository, NetworkCredential networkCredential, string userAgentString) : base(repository, networkCredential)
        {
            this.Repository = repository;

            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                Credentials = networkCredential
            };

            _sessionClient = new HttpClient(handler);
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
            string errMsg = $"Find all is not supported for the V3 repository {Repository.Name}";
            errRecord = new ErrorRecord(new InvalidOperationException(errMsg), "FindAllFailure", ErrorCategory.InvalidOperation, this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag(s) from a repository and returns latest version for each.
        /// This is supported only for the NuGet repository special case, not other V3 repositories.
        /// </summary>
        public override FindResults FindTags(string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            if (_isNuGetRepo || _isJFrogRepo)
            {
                return FindTagsFromNuGetRepo(tags, includePrerelease, out errRecord);
            }
            else
            {
                string errMsg = $"Find by Tags is not supported for the V3 repository {Repository.Name}";
                errRecord = new ErrorRecord(new InvalidOperationException(errMsg), "FindTagsFailure", ErrorCategory.InvalidOperation, this);
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }
        }

        /// <summary>
        /// Find method which allows for searching for packages with specified Command or DSCResource name.
        /// Not supported for V3 repository.
        /// </summary>
        public override FindResults FindCommandOrDscResource(string[] tags, bool includePrerelease, bool isSearchingForCommands, out ErrorRecord errRecord)
        {
            string errMsg = $"Find by CommandName or DSCResource is not supported for the V3 server repository {Repository.Name}";
            errRecord = new ErrorRecord(new InvalidOperationException(errMsg), "FindCommandOrDscResourceFailure", ErrorCategory.InvalidOperation, this);

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
            return FindNameHelper(packageName, tags, includePrerelease, type, out errRecord);
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and returns latest version.
        /// This is supported only for the NuGet repository special case, not other V3 repositories.
        /// </summary>
        public override FindResults FindNameGlobbing(string packageName, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            if (_isNuGetRepo || _isJFrogRepo || _isGHPkgsRepo || _isMyGetRepo)
            {
                return FindNameGlobbingFromNuGetRepo(packageName, tags: Utils.EmptyStrArray, includePrerelease, out errRecord);
            }
            else
            {
                string errMsg = $"Find with Name containing wildcards is not supported for the V3 server repository {Repository.Name}";
                errRecord = new ErrorRecord(new InvalidOperationException(errMsg), "FindNameGlobbingFailure", ErrorCategory.InvalidOperation, this);

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and tag and returns latest version.
        /// This is supported only for the NuGet repository special case, not other V3 repositories.
        /// </summary>
        public override FindResults FindNameGlobbingWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            if (_isNuGetRepo || _isJFrogRepo || _isGHPkgsRepo || _isMyGetRepo)
            {
                return FindNameGlobbingFromNuGetRepo(packageName, tags, includePrerelease, out errRecord);
            }
            else
            {
                string errMsg = $"Find with Name containing wildcards is not supported for the V3 server repository {Repository.Name}";
                errRecord = new ErrorRecord(new InvalidOperationException(errMsg), "FindNameGlobbingWithTagFailure", ErrorCategory.InvalidOperation, this);

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
                            errRecord = new ErrorRecord(new InvalidOrEmptyResponse($"Response does not contain '{versionName}' element."), "FindVersionGlobbingFailure", ErrorCategory.InvalidData, this);
                            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                        }

                        if (NuGetVersion.TryParse(pkgVersionElement.ToString(), out NuGetVersion pkgVersion) && versionRange.Satisfies(pkgVersion))
                        {
                            if (!pkgVersion.IsPrerelease || includePrerelease)
                            {
                                satisfyingVersions.Add(response);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    errRecord = new ErrorRecord(e, "FindVersionGlobbingFailure", ErrorCategory.InvalidResult, this);
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
            return FindVersionHelper(packageName, version, tags: tags, type, out errRecord);
        }

        /**  INSTALL APIS **/

        /// <summary>
        /// Installs specific package.
        /// Name: no wildcard support.
        /// Examples: Install "Newtonsoft.json"
        /// </summary>
        public override Stream InstallName(string packageName, bool includePrerelease, out ErrorRecord errRecord)
        {
            return InstallHelper(packageName, version: null, out errRecord);
        }

        /// <summary>
        /// Installs package with specific name and version.
        /// Name: no wildcard support.
        /// Version: no wildcard support.
        /// Examples: Install "Newtonsoft.json" -Version "1.0.0.0"
        ///           Install "Newtonsoft.json" -Version "2.5.0-beta"
        /// </summary>
        public override Stream InstallVersion(string packageName, string version, out ErrorRecord errRecord)
        {
            if (!NuGetVersion.TryParse(version, out NuGetVersion requiredVersion))
            {
                errRecord = new ErrorRecord(new ArgumentException($"Version {version} to be installed is not a valid NuGet version."), "InstallVersionFailure", ErrorCategory.InvalidArgument, this);
                return null;
            }

            return InstallHelper(packageName, requiredVersion, out errRecord);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Helper method called by FindNameGlobbing() and FindNameGlobbingWithTag() for special case where repository is NuGet.org repository.
        /// </summary>
        private FindResults FindNameGlobbingFromNuGetRepo(string packageName, string[] tags, bool includePrerelease, out ErrorRecord errRecord)
        {
            var names = packageName.Split(new char[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
            string querySearchTerm;

            if (names.Length == 0)
            {
                errRecord = new ErrorRecord(new ArgumentException("-Name '*' for V3 server protocol repositories is not supported"), "FindNameGlobbingFromNuGetRepoFailure", ErrorCategory.InvalidArgument, this);
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

                errRecord = new ErrorRecord(new ArgumentException("-Name with wildcards is only supported for scenarios similar to the following examples: PowerShell*, *ShellGet, *Shell*."), "FindNameGlobbingFromNuGetRepoFailure", ErrorCategory.InvalidArgument, this);
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
                        string errMsg = $"FindNameGlobbing(): Name element could not be found in response.";
                        errRecord = new ErrorRecord(new JsonParsingException(errMsg), "GetEntriesFromSearchQueryResourceFailure", ErrorCategory.InvalidResult, this);
                        return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                    }

                    if (!pkgEntry.TryGetProperty(tagsName, out JsonElement tagsItem))
                    {
                        string errMsg = $"FindNameGlobbing(): Tags element could not be found in response.";
                        errRecord = new ErrorRecord(new JsonParsingException(errMsg), "GetEntriesFromSearchQueryResourceFailure", ErrorCategory.InvalidResult, this);
                        return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                    }

                    id = idItem.ToString();

                    // determine if id matches our wildcard criteria
                    if ((packageName.StartsWith("*") && packageName.EndsWith("*") && id.ToLower().Contains(querySearchTerm.ToLower())) ||
                        (packageName.EndsWith("*") && id.StartsWith(querySearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                        (packageName.StartsWith("*") && id.EndsWith(querySearchTerm, StringComparison.OrdinalIgnoreCase)))
                    {
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
                    errRecord = new ErrorRecord(e, "GetEntriesFromSearchQueryResourceFailure", ErrorCategory.InvalidResult, this);
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
            string tagsQueryTerm = $"tags:{String.Join(" ", tags)}";
            // Get responses for all packages that contain the required tags
            // example query:
            var tagPkgEntries = GetVersionedPackageEntriesFromSearchQueryResource(tagsQueryTerm, includePrerelease, out errRecord);
            if (errRecord != null)
            {
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
                            errRecord = new ErrorRecord(new InvalidOrEmptyResponse($"Response does not contain '{versionName}' element for search with Name {packageName} in '{Repository.Name}'."), "FindNameFailure", ErrorCategory.InvalidResult, this);
                            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                        }
                        if (!rootDom.TryGetProperty(tagsName, out JsonElement tagsItem))
                        {
                            errRecord = new ErrorRecord(new InvalidOrEmptyResponse($"Response does not contain '{tagsName}' element for search with Name {packageName} in '{Repository.Name}'."), "FindNameFailure", ErrorCategory.InvalidResult, this);
                            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                        }

                        if (NuGetVersion.TryParse(pkgVersionElement.ToString(), out NuGetVersion pkgVersion))
                        {
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
                    errRecord = new ErrorRecord(e, "FindNameFailure", ErrorCategory.InvalidResult, this);
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                }
            }

            if (String.IsNullOrEmpty(latestVersionResponse))
            {
                string errMsg = $"Package with Name {packageName} was not found in repository {Repository.Name}.";
                errRecord = new ErrorRecord(new SpecifiedTagsNotFoundException(errMsg), "FindNameFailure", ErrorCategory.InvalidResult, this);
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            // Check and write error for tags matching requirement. If no tags were required the isTagMatch variable will be true.
            if (!isTagMatch)
            {
                if (errRecord == null)
                {
                    string errMsg = $"Package with Name {packageName} and Tags {String.Join(", ", tags)} was not found in repository {Repository.Name}.";
                    errRecord = new ErrorRecord(new SpecifiedTagsNotFoundException(errMsg), "FindNameFailure", ErrorCategory.InvalidResult, this);
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
            if (!NuGetVersion.TryParse(version, out NuGetVersion requiredVersion))
            {
                errRecord = new ErrorRecord(new ArgumentException($"Version {version} to be found is not a valid NuGet version."), "FindNameFailure", ErrorCategory.InvalidArgument, this);
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

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
                            errRecord = new ErrorRecord(new InvalidOrEmptyResponse($"Response does not contain '{versionName}' element for search with Name {packageName} and Version {version} in '{Repository.Name}'."), "FindVersionFailure", ErrorCategory.InvalidResult, this);
                            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                        }
                        if (!rootDom.TryGetProperty(tagsName, out JsonElement tagsItem))
                        {
                            errRecord = new ErrorRecord(new InvalidOrEmptyResponse($"Response does not contain '{tagsName}' element for search with Name {packageName} and Version {version} in '{Repository.Name}'."), "FindVersionFailure", ErrorCategory.InvalidResult, this);
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
                    errRecord = new ErrorRecord(e, "FindVersionFailure", ErrorCategory.InvalidResult, this);
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                }
            }

            if (String.IsNullOrEmpty(latestVersionResponse))
            {
                errRecord = new ErrorRecord(new InvalidOrEmptyResponse($"Package with Name {packageName}, Version {version} was not found in repository {Repository.Name}"), "FindVersionFailure", ErrorCategory.InvalidResult, this);
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            if (!isTagMatch)
            {
                if (errRecord == null)
                {
                    string errMsg = $"FindVersion(): Package with Name {packageName}, Version {version} and Tags {String.Join(", ", tags)} was not found in repository {Repository.Name}.";
                    errRecord = new ErrorRecord(new SpecifiedTagsNotFoundException(errMsg), "FindVersionFailure", ErrorCategory.InvalidResult, this);
                }

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            return new FindResults(stringResponse: new string[] { latestVersionResponse }, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Helper method that is called by InstallName() and InstallVersion()
        /// For InstallName() we want latest version installed (so version parameter passed in will be null), for InstallVersion() we want specified, non-null version installed.
        /// </summary>
        private Stream InstallHelper(string packageName, NuGetVersion version, out ErrorRecord errRecord)
        {
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
                string errorMsg = $"Package with Name {packageName} and Version {version} could not be found in repository {Repository.Name}";
                errRecord = new ErrorRecord(new Exception(errorMsg), "InstallFailure", ErrorCategory.InvalidResult, this);
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
                string errorMsg = $"Package with Name {packageName} and Version {version} could not be found in repository {Repository.Name}";
                errRecord = new ErrorRecord(new Exception(errorMsg), "InstallFailure", ErrorCategory.InvalidResult, this);
                return null;
            }

            var content = HttpRequestCallForContent(pkgContentUrl, out errRecord);
            if (errRecord != null)
            {
                return null;
            }

            pkgStream = content.ReadAsStreamAsync().Result;
            return pkgStream;
        }

        /// <summary>
        /// Gets the versioned package entries from the RegistrationsBaseUrl resource
        /// i.e when the package Name being searched for does not contain wildcard
        /// This is called by FindNameHelper(), FindVersionHelper(), FindVersionGlobbing(), InstallHelper()
        /// </summary>
        private string[] GetVersionedPackageEntriesFromRegistrationsResource(string packageName, string propertyName, bool isSearch, out ErrorRecord errRecord)
        {
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

            // check count (ie "totalHits") 425 ==> count/100  ~~> 5 calls
            int count = initialCount / 100;
            // if more than 100 count, loop and add response to list
            while (count > 0)
            {
                skip += 100;
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
                        errRecord = new ErrorRecord(new JsonParsingException($"@type element not found for resource in service index for repository {Repository.Name}"), "GetResourcesFromServiceIndexFailure", ErrorCategory.InvalidResult, this);
                        return new Dictionary<string, string>();
                    }

                    if (!resource.TryGetProperty("@id", out JsonElement idElement))
                    {
                        errRecord = new ErrorRecord(new JsonParsingException($"@id element not found for resource in service index for repository {Repository.Name}"), "GetResourcesFromServiceIndexFailure", ErrorCategory.InvalidResult, this);
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
                    // TODO:  how to keep repo name in error message?
                    string errMsg = $"Exception parsing service index JSON for respository {Repository.Name} with error: {e.Message}";
                    errRecord = new ErrorRecord(e, "GetResourcesFromServiceIndexFailure", ErrorCategory.InvalidResult, this);
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
                errRecord = new ErrorRecord(new V3ResourceNotFoundException($"RegistrationBaseUrl resource could not be found for Repository '{Repository.Name}'"), "FindRegistrationsBaseUrlFailure", ErrorCategory.InvalidResult, this);
            }

            return registrationsBaseUrl;
        }

        /// <summary>
        /// Gets the resource of type "SearchQueryService" from the repository's resources.
        /// A repository can have multiple resources of type "SearchQueryService" so it finds the best match according to the guideline comment in the method.
        /// </summary>
        private string FindSearchQueryService(Dictionary<string, string> resources, out ErrorRecord errRecord)
        {
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
                errRecord = new ErrorRecord(new V3ResourceNotFoundException($"SearchQueryService resource could not be found for Repository '{Repository.Name}'"), "FindSearchQueryServiceFailure", ErrorCategory.InvalidResult, this);
            }

            return searchQueryServiceUrl;
        }

        /// <summary>
        /// For JFrog repository, the metadata is located under "@id" > inner "items" element
        /// This is different than other V3 repositories response's metadata location.
        /// </summary>
        private JsonElement GetMetadataElementForJFrogRepo(JsonElement itemsElement, string packageName, out ErrorRecord errRecord)
        {
            JsonElement metadataElement;
            if (!itemsElement.TryGetProperty(idLinkName, out metadataElement))
            {
                errRecord = new ErrorRecord(new ArgumentException($"'{idLinkName}' element from package '{packageName}' could not be found in JFrog repository '{Repository.Name}'"), "GetElementForJFrogRepoFailure", ErrorCategory.InvalidResult, this);
                return metadataElement;
            }

            string metadataUri = metadataElement.ToString();
            string response = HttpRequestCall(metadataUri, out errRecord);
            if (errRecord != null)
            {
                if (errRecord.Exception is V3ResourceNotFoundException) {
                    errRecord = new ErrorRecord(new V3ResourceNotFoundException($"Package '{packageName}' was not found in repository '{Repository.Name}'", errRecord.Exception), "PackageNotFound", ErrorCategory.ObjectNotFound, this);
                }

                return metadataElement;
            }

            try
            {
                using (JsonDocument metadataEntries = JsonDocument.Parse(response))
                {
                    JsonElement rootDom = metadataEntries.RootElement;
                    if (!rootDom.TryGetProperty(itemsName, out JsonElement innerItemsElement))
                    {
                        errRecord = new ErrorRecord(new ArgumentException($"'{itemsName}' element from package '{packageName}' could not be found in JFrog repository '{Repository.Name}'"), "GetElementForJFrogRepoFailure", ErrorCategory.InvalidResult, this);
                        return metadataElement;
                    }

                    // return clone, otherwise this JsonElement will be out of scope to the caller once JsonDocument is disposed
                    metadataElement = innerItemsElement.Clone();
                }
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(e, "FindSearchQueryServiceFailure", ErrorCategory.InvalidResult, this);
            }

            return metadataElement;
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
            List<string> versionedResponses = new List<string>();
            string[] versionedResponseArr;
            var requestPkgMapping = registrationsBaseUrl.EndsWith("/") ? $"{registrationsBaseUrl}{packageName.ToLower()}/index.json" : $"{registrationsBaseUrl}/{packageName.ToLower()}/index.json";

            string pkgMappingResponse = HttpRequestCall(requestPkgMapping, out errRecord);
            if (errRecord != null)
            {
                if (errRecord.Exception is V3ResourceNotFoundException)
                {
                    errRecord = new ErrorRecord(new V3ResourceNotFoundException($"Package '{packageName}' was not found in repository '{Repository.Name}'", errRecord.Exception), "PackageNotFound", ErrorCategory.ObjectNotFound, this);
                }

                return Utils.EmptyStrArray;
            }

            try
            {
                // parse out JSON response we get from RegistrationsUrl
                using (JsonDocument pkgVersionEntry = JsonDocument.Parse(pkgMappingResponse))
                {
                    // The response has a "items" array element, which only has useful 1st element
                    JsonElement rootDom = pkgVersionEntry.RootElement;
                    if (!rootDom.TryGetProperty(itemsName, out JsonElement itemsElement) || itemsElement.GetArrayLength() == 0)
                    {
                        errRecord = new ErrorRecord(new ArgumentException($"Response does not contain '{itemsName}' element for package '{packageName}' from Repository '{Repository.Name}'."), " ", ErrorCategory.InvalidResult, this);
                        return Utils.EmptyStrArray;
                    }

                    JsonElement firstItem = itemsElement[0];

                    // this is the "items" element directly containing package version entries we hope to enumerate
                    if (!firstItem.TryGetProperty(itemsName, out JsonElement innerItemsElement))
                    {
                        if (_isJFrogRepo)
                        {
                            innerItemsElement = GetMetadataElementForJFrogRepo(firstItem, packageName, out errRecord);
                        }
                        else
                        {
                            errRecord = new ErrorRecord(new ArgumentException($"Response does not contain '{itemsName}' element for package '{packageName}' from Repository '{Repository.Name}'."), "GetResponsesFromRegistrationsResourceFailure", ErrorCategory.InvalidResult, this);
                            return Utils.EmptyStrArray;
                        }
                    }

                    // https://api.nuget.org/v3/registration5-gz-semver2/test_module/index.json
                    // The "items" property contains an inner "items" element and a "count" element
                    // The inner "items" property is the metadata array for each version of the package.
                    // The "count" property represents how many versions are present for that package, (i.e how many elements are in the inner "items" array)
                    if (!firstItem.TryGetProperty(countName, out JsonElement countElement) || !countElement.TryGetInt32(out int count))
                    {
                        errRecord = new ErrorRecord(new ArgumentException($"Response does not contain inner '{countName}' element for package '{packageName}' from repository '{Repository.Name}'."), "GetResponsesFromRegistrationsResourceFailure", ErrorCategory.InvalidResult, this);
                        return Utils.EmptyStrArray;
                    }

                    if (count == 0)
                    {
                        return Utils.EmptyStrArray;
                    }

                    if (!firstItem.TryGetProperty("upper", out JsonElement upperVersionElement))
                    {
                        errRecord = new ErrorRecord(new ArgumentException($"Response does not contain inner 'upper' element, for package with name {packageName} from repository '{Repository.Name}'."), "GetResponsesFromRegistrationsResourceFailure", ErrorCategory.InvalidResult, this);
                        return Utils.EmptyStrArray;
                    }

                    // Get the specific entry for each package version
                    foreach (JsonElement versionerrRecordtem in innerItemsElement.EnumerateArray())
                    {
                        // For search:
                        // The "catalogEntry" property in the specific package version entry contains package metadata
                        // For download:
                        // The "packageContent" property in the specific package version entry has the .nupkg URI for each version of the package.
                        if (!versionerrRecordtem.TryGetProperty(property, out JsonElement metadataElement))
                        {
                            errRecord = new ErrorRecord(new ArgumentException($"Response does not contain inner '{property}' element for package '{packageName}' from repository '{Repository.Name}'."), "GetResponsesFromRegistrationsResourceFailure", ErrorCategory.InvalidResult, this);
                            continue;
                        }

                        // If metadata has a "listed" property, but it's set to false, skip this package version
                        if (property.Equals("catalogEntry") && metadataElement.TryGetProperty("listed", out JsonElement listedElement))
                        {
                            if (bool.TryParse(listedElement.ToString(), out bool listed) && !listed)
                            {
                                continue;
                            }
                        }

                        versionedResponses.Add(metadataElement.ToString());
                    }

                    // Reverse array of versioned responses, if needed, so that version entries are in descending order.
                    string upperVersion = upperVersionElement.ToString();
                    versionedResponseArr = versionedResponses.ToArray();
                    if (isSearch)
                    {
                        if (!IsLatestVersionFirstForSearch(versionedResponseArr, upperVersion, out errRecord))
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
                }
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(e, "GetResponsesFromRegistrationsResourceFailure", ErrorCategory.InvalidResult, this);
                return Utils.EmptyStrArray;
            }

            return versionedResponseArr;
        }

        /// <summary>
        /// Returns true if the metadata entries are arranged in descending order with respect to the package's version.
        /// ADO feeds usually return version entries in descending order, but Nuget.org repository returns them in ascending order.
        /// </summary>
        private bool IsLatestVersionFirstForSearch(string[] versionedResponses, string upperVersion, out ErrorRecord errRecord)
        {
            errRecord = null;
            bool latestVersionFirst = true;

            // We don't need to perform this check if no responses, or single response
            if (versionedResponses.Length < 2)
            {
                return latestVersionFirst;
            }

            string firstResponse = versionedResponses[0];
            try
            {
                using (JsonDocument firstResponseJson = JsonDocument.Parse(firstResponse))
                {
                    JsonElement firstResponseDom = firstResponseJson.RootElement;
                    if (!firstResponseDom.TryGetProperty(versionName, out JsonElement firstVersionElement))
                    {
                        errRecord = new ErrorRecord(new JsonParsingException($"Response did not contain '{versionName}' element"), "LatestVersionFirstSearchFailure", ErrorCategory.InvalidResult, this);
                        return latestVersionFirst;
                    }

                    string firstVersion = firstVersionElement.ToString();
                    if (NuGetVersion.TryParse(upperVersion, out NuGetVersion upperPkgVersion) && NuGetVersion.TryParse(firstVersion, out NuGetVersion firstPkgVersion))
                    {
                        if (firstPkgVersion != upperPkgVersion)
                        {
                            latestVersionFirst = false;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(e, "LatestVersionFirstSearchFailure", ErrorCategory.InvalidResult, this);
                return true;
            }

            return latestVersionFirst;
        }

        /// <summary>
        /// Returns true if the nupkg URI entries for each package version are arranged in descending order with respect to the package's version.
        /// ADO feeds usually return version entries in descending order, but Nuget.org repository returns them in ascending order.
        /// </summary>
        private bool IsLatestVersionFirstForInstall(string[] versionedResponses, string upperVersion, out ErrorRecord errRecord)
        {
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
                errRecord = new ErrorRecord(e, "GetResponsesFromRegistrationsResourceFailure", ErrorCategory.InvalidResult, this);
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
            catch (Exception e)
            {
                errRecord = new ErrorRecord(e, "GetResponsesFromRegistrationsResourceFailure", ErrorCategory.InvalidResult, this);
            }

            return entries;
        }

        /// <summary>
        /// Helper method that makes the HTTP request for the V3 server protocol url passed in for find APIs.
        /// </summary>
        private string HttpRequestCall(string requestUrlV3, out ErrorRecord errRecord)
        {
            errRecord = null;
            string response = string.Empty;

            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV3);

                response = SendV3RequestAsync(request, _sessionClient).GetAwaiter().GetResult();
            }
            catch (V3ResourceNotFoundException e)
            {
                errRecord = new ErrorRecord(e, "ResourceNotFound", ErrorCategory.InvalidResult, this);
            }
            catch (UnauthorizedException e)
            {
                errRecord = new ErrorRecord(e, "UnauthorizedRequest", ErrorCategory.InvalidResult, this);
            }
            catch (HttpRequestException e)
            {
                errRecord = new ErrorRecord(e, "HttpRequestCallFailure", ErrorCategory.InvalidResult, this);
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(e, "HttpRequestCallFailure", ErrorCategory.InvalidResult, this);
            }

            return response;
        }

        /// <summary>
        /// Helper method that makes the HTTP request for the V3 server protocol url passed in for install APIs.
        /// </summary>
        private HttpContent HttpRequestCallForContent(string requestUrlV3, out ErrorRecord errRecord)
        {
            errRecord = null;
            HttpContent content = null;
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV3);

                content = SendV3RequestForContentAsync(request, _sessionClient).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(e, "HttpRequestCallForContentFailure", ErrorCategory.InvalidResult, this);
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
                    throw new V3ResourceNotFoundException(Utils.FormatRequestsExceptions(e, message));
                }
                // ADO feed will return a 401 if a package does not exist on the feed, with the following message:
                // 401 (Unauthorized - No local versions of package 'NonExistentModule'; please provide authentication to access
                // versions from upstream that have not yet been saved to your feed. (DevOps Activity ID: 5E5CF528-5B3D-481D-95B5-5DDB5476D7EF))
                if (responseStatusCode.Equals(HttpStatusCode.Unauthorized) && !e.Message.Contains("access versions from upstream that have not yet been saved to your feed"))
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
                    throw new V3ResourceNotFoundException(Utils.FormatRequestsExceptions(e, message));
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
