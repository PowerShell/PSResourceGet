
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
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

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    internal class ADOServerApiCalls : ServerApiCall
    {
        #region Members
        public override PSRepositoryInfo Repository { get; set; }
        private HttpClient _sessionClient { get; set; }
        private bool _isNuGetRepo { get; set; }
        public FindResponseType v3FindResponseType = FindResponseType.ResponseString;
        private static readonly Hashtable[] emptyHashResponses = new Hashtable[]{};
        private static readonly string resourcesName = "resources";
        private static readonly string itemsName = "items";
        private static readonly string countName = "count";
        private static readonly string versionName = "version";
        private static readonly string dataName = "data";
        private static readonly string idName = "id";
        private static readonly string tagsName = "tags";
        private static readonly string catalogEntryProperty = "catalogEntry";
        private static readonly string packageContentProperty = "packageContent";

        #endregion

        #region Constructor

        public ADOServerApiCalls(PSRepositoryInfo repository, NetworkCredential networkCredential) : base(repository, networkCredential)
        {
            this.Repository = repository;

            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                Credentials = networkCredential
            };

            _sessionClient = new HttpClient(handler);

            _isNuGetRepo = String.Equals(Repository.Uri.AbsoluteUri, "https://api.nuget.org/v3/index.json", StringComparison.InvariantCultureIgnoreCase);
        }

        #endregion

        #region Overriden Methods

        /// <summary>
        /// Find method which allows for searching for all packages from a repository and returns latest version for each.
        /// Not supported for ADO repository.
        /// </summary>
        public override FindResults FindAll(bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            string errMsg = $"Find all is not supported for the repository {Repository.Uri}";
            edi = ExceptionDispatchInfo.Capture(new InvalidOperationException(errMsg));

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag(s) from a repository and returns latest version for each.
        /// Not supported for ADO repository.
        /// </summary>
        public override FindResults FindTags(string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            if (_isNuGetRepo)
            {
                return FindTagsFromNuGetRepo(tags, includePrerelease, out edi);
            }
            else
            {
                string errMsg = $"Find by Tags is not supported for the repository {Repository.Uri}";
                edi = ExceptionDispatchInfo.Capture(new InvalidOperationException(errMsg));
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }
        }

        /// <summary>
        /// Find method which allows for searching for packages with specified Command or DSCResource name.
        /// Not supported for ADO repository.
        /// </summary>
        public override FindResults FindCommandOrDscResource(string[] tags, bool includePrerelease, bool isSearchingForCommands, out ExceptionDispatchInfo edi)
        {
            string errMsg = $"Find by CommandName or DSCResource is not supported for {Repository.Name} as it uses the V3 server protocol";
            edi = ExceptionDispatchInfo.Capture(new InvalidOperationException(errMsg));

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "Newtonsoft.Json"
        /// We use the latest RegistrationBaseUrl version resource we can find and check if contains an entry with the package name.
        /// </summary>
        public override FindResults FindName(string packageName, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            return FindNameHelper(packageName, tags: Utils.EmptyStrArray, includePrerelease, type, out edi);
        }

        /// <summary>
        /// Find method which allows for searching for single name and specified tag(s) and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "Newtonsoft.Json" -Tag "json"
        /// We use the latest RegistrationBaseUrl version resource we can find and check if contains an entry with the package name.
        /// </summary>
        public override FindResults FindNameWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            return FindNameHelper(packageName, tags, includePrerelease, type, out edi);
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and returns latest version.
        /// This is not supported for ADO repository.
        /// </summary>
        public override FindResults FindNameGlobbing(string packageName, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            if (_isNuGetRepo)
            {
                return FindNameGlobbingFromNuGetRepo(packageName, tags: Utils.EmptyStrArray, includePrerelease, out edi);
            }
            else
            {
                string errMsg = $"Find with Name containing wildcards is not supported for the repository {Repository.Name}";
                edi = ExceptionDispatchInfo.Capture(new InvalidOperationException(errMsg));

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and tag and returns latest version.
        /// This is not supported for ADO repository.
        /// </summary>
        public override FindResults FindNameGlobbingWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            if (_isNuGetRepo)
            {
                return FindNameGlobbingFromNuGetRepo(packageName, tags, includePrerelease, out edi);
            }
            else
            {
                string errMsg = $"Find with Name containing wildcards is not supported for the repository {Repository.Name}";
                edi = ExceptionDispatchInfo.Capture(new InvalidOperationException(errMsg));

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
        public override FindResults FindVersionGlobbing(string packageName, VersionRange versionRange, bool includePrerelease, ResourceType type, bool getOnlyLatest, out ExceptionDispatchInfo edi)
        {
            // Get all the resources for the service index
            Dictionary<string, string> resources = GetResourcesFromServiceIndex(out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string registrationsBaseUrl = FindRegistrationsBaseUrlForAllV3(resources, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string[] versionedResponses = GetVersionedResponses(registrationsBaseUrl, packageName, catalogEntryProperty, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            List<string> satisfyingVersions = new List<string>();
            foreach (string response in versionedResponses)
            {
                JsonElement pkgVersionElement;
                try
                {
                    JsonDocument pkgVersionEntry = JsonDocument.Parse(response);
                    JsonElement rootDom = pkgVersionEntry.RootElement;
                    if (!rootDom.TryGetProperty(versionName, out pkgVersionElement))
                    {
                        edi = ExceptionDispatchInfo.Capture(new InvalidOrEmptyResponse($"Response does not contain 'version' element."));
                        return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                    }
                }
                catch (Exception e)
                {
                    edi = ExceptionDispatchInfo.Capture(e);
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

            return new FindResults(stringResponse: satisfyingVersions.ToArray(), hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "NuGet.Server.Core" "3.0.0-beta"
        /// We use the latest RegistrationBaseUrl version resource we can find and check if contains an entry with the package name, then match to the specified version.
        /// </summary>
        public override FindResults FindVersion(string packageName, string version, ResourceType type, out ExceptionDispatchInfo edi)
        {
            return FindVersionHelper(packageName, version, tags: Utils.EmptyStrArray, type, out edi);
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version and tag(s).
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "NuGet.Server.Core" "3.0.0-beta" -Tag "core"
        /// We use the latest RegistrationBaseUrl version resource we can find and check if contains an entry with the package name, then match to the specified version.
        /// </summary>     
        public override FindResults FindVersionWithTag(string packageName, string version, string[] tags, ResourceType type, out ExceptionDispatchInfo edi)
        {
            return FindVersionHelper(packageName, version, tags: tags, type, out edi);
        }

        /**  INSTALL APIS **/

        /// <summary>
        /// Installs specific package.
        /// Name: no wildcard support.
        /// Examples: Install "Newtonsoft.json"
        /// </summary>
        public override Stream InstallName(string packageName, bool includePrerelease, out ExceptionDispatchInfo edi)
        {
            Stream pkgStream = null;
            // Get all the resources for the service index
            Dictionary<string, string> resources = GetResourcesFromServiceIndex(out edi);
            if (edi != null)
            {
                return pkgStream;
            }

            string registrationsBaseUrl = FindRegistrationsBaseUrlForAllV3(resources, out edi);
            if (edi != null)
            {
                return pkgStream;
            }

            string[] versionedResponses = GetVersionedResponses(registrationsBaseUrl, packageName, packageContentProperty, out edi);
            if (edi != null)
            {
                return pkgStream;
            }

            if (versionedResponses.Length == 0)
            {
                string errorMsg = $"Package with Name {packageName} could not be found in repository {Repository.Name}";
                edi = ExceptionDispatchInfo.Capture(new Exception(errorMsg));
                return pkgStream;
            }

            // get the Url for the latest version only
            string pkgContentUrl = versionedResponses[0];
            var content = HttpRequestCallForContent(pkgContentUrl, out edi);
            if (edi != null)
            {
                string errorMsg = $"Package with Name {packageName} could not be found in repository {Repository.Name}";
                edi = ExceptionDispatchInfo.Capture(new Exception(errorMsg));
                return null;
            }

            pkgStream = content.ReadAsStreamAsync().Result;
            return pkgStream;
        }

        /// <summary>
        /// Installs package with specific name and version.
        /// Name: no wildcard support.
        /// Version: no wildcard support.
        /// Examples: Install "Newtonsoft.json" -Version "1.0.0.0"
        ///           Install "Newtonsoft.json" -Version "2.5.0-beta"
        /// </summary>    
        public override Stream InstallVersion(string packageName, string version, out ExceptionDispatchInfo edi)
        {
            Stream pkgStream = null;
            if (!NuGetVersion.TryParse(version, out NuGetVersion requiredVersion))
            {
                edi = ExceptionDispatchInfo.Capture(new ArgumentException($"Version {version} to be installed is not a valid NuGet version."));
                return pkgStream;
            }

            // Get all the resources for the service index
            Dictionary<string, string> resources = GetResourcesFromServiceIndex(out edi);
            if (edi != null)
            {
                return pkgStream;
            }

            string registrationsBaseUrl = FindRegistrationsBaseUrlForAllV3(resources, out edi);
            if (edi != null)
            {
                return pkgStream;
            }

            string[] versionedResponses = GetVersionedResponses(registrationsBaseUrl, packageName, packageContentProperty, out edi);
            if (edi != null)
            {
                return pkgStream;
            }

            if (versionedResponses.Length == 0)
            {
                string errorMsg = $"Package with Name {packageName} and Version {version} could not be found in repository {Repository.Name}";
                edi = ExceptionDispatchInfo.Capture(new Exception(errorMsg));
                return null;
            }

            string pkgContentUrl = String.Empty;
            foreach (string response in versionedResponses)
            {
                // Response will be "packageContent" element value that looks like: "{packageBaseAddress}/{packageName}/{normalizedVersion}/{packageName}.{normalizedVersion}.nupkg"
                if (response.Contains(requiredVersion.ToNormalizedString()))
                {
                    pkgContentUrl = response;
                    break;
                }
            }

            if (String.IsNullOrEmpty(pkgContentUrl))
            {
                string errorMsg = $"Package with Name {packageName} and Version {version} could not be found in repository {Repository.Name}";
                edi = ExceptionDispatchInfo.Capture(new Exception(errorMsg));
                return null;
            }

            var content = HttpRequestCallForContent(pkgContentUrl, out edi);
            if (edi != null)
            {
                return null;
            }

            pkgStream = content.ReadAsStreamAsync().Result;
            return pkgStream;
        }

        #endregion

        #region Private Methods

        private FindResults FindNameGlobbingFromNuGetRepo(string packageName, string[] tags, bool includePrerelease, out ExceptionDispatchInfo edi)
        {
            var names = packageName.Split(new char[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
            string querySearchTerm;

            if (names.Length == 0)
            {
                edi = ExceptionDispatchInfo.Capture(new ArgumentException("-Name '*' for V3 server protocol repositories is not supported"));
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

                edi = ExceptionDispatchInfo.Capture(new ArgumentException("-Name with wildcards is only supported for scenarios similar to the following examples: PowerShell*, *ShellGet, *Shell*."));
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            Dictionary<string, string> resources = GetResourcesFromServiceIndex(out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string searchQueryServiceUrl = FindSearchQueryService(resources, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string query = $"{searchQueryServiceUrl}?q={querySearchTerm}&prerelease={includePrerelease}&semVerLevel=2.0.0";
            Console.WriteLine($"NameGlobbing query {query}");

            JsonElement[] matchingPkgIds = GetJsonElementArr(query, dataName, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            List<string> matchingResponses = new List<string>();
            foreach (var pkgEntry in matchingPkgIds)
            {
                string id = string.Empty;
                string latestVersion = string.Empty;
                string[] pkgTags = Utils.EmptyStrArray;

                try
                {
                    if (!pkgEntry.TryGetProperty(idName, out JsonElement idItem))
                    {
                        string errMsg = $"FindNameGlobbing(): Name element could not be found in response.";
                        edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                        return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                    }

                    if (!pkgEntry.TryGetProperty(tagsName, out JsonElement tagsItem))
                    {
                        string errMsg = $"FindNameGlobbing(): Tags element could not be found in response.";
                        edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                        return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                    }

                    id = idItem.ToString();
                    pkgTags = GetTagsFromJsonElement(tagsElement: tagsItem);

                    // determine if id matches our wildcard criteria
                    if ((packageName.StartsWith("*") && packageName.EndsWith("*") && id.ToLower().Contains(querySearchTerm.ToLower())) ||
                        (packageName.EndsWith("*") && id.StartsWith(querySearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                        (packageName.StartsWith("*") && id.EndsWith(querySearchTerm, StringComparison.OrdinalIgnoreCase)))
                    {
                        bool isTagMatch = DeterminePkgTagsSatisfyRequiredTags(pkgTags: pkgTags, requiredTags: tags);
                        if (!isTagMatch)
                        {
                            continue;
                        }

                        matchingResponses.Add(pkgEntry.ToString());
                    }
                }
                    
                catch (Exception e)
                {
                    string errMsg = $"FindNameGlobbing(): Name or Version element could not be parsed from response due to exception {e.Message}.";
                    edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                    break;
                }
                
            }

            return new FindResults(stringResponse: matchingResponses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }
        
        private FindResults FindTagsFromNuGetRepo(string[] tags, bool includePrerelease, out ExceptionDispatchInfo edi)
        {
            Dictionary<string, string> resources = GetResourcesFromServiceIndex(out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string searchQueryServiceUrl = FindSearchQueryService(resources, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string tagsQueryTerm = String.Join(" ", tags);

            string query = $"{searchQueryServiceUrl}?q=tags:{tagsQueryTerm}&prerelease={includePrerelease}&semVerLevel=2.0.0";
            Console.WriteLine($"tag query: {query}");

            // Get responses for all packages (only their latest, prerelease satisfying version) that includes the tag (somewhere in the metadata)
            JsonElement[] tagPkgs = GetJsonElementArr(query, dataName, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            // loop through responses returned
            // get tags really, and if it satisfies it all, add to list and return. ResponseUtil should handle rest

            List<string> matchingResponses = new List<string>();
            foreach (var pkgEntry in tagPkgs)
            {
                try
                {
                    // Since the query only matched the tag value to be present in the metadata, not the actual "tag" property
                    // if (!pkgId.TryGetProperty(idName, out JsonElement idItem) || !pkgId.TryGetProperty(versionName, out JsonElement versionItem))
                    // {
                    //     string errMsg = $"FindTag(): Id or Version element could not be found in response.";
                    //     edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                    //     return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                    // }

                    // RECENT
                    // if (!pkgEntry.TryGetProperty("tags", out JsonElement tagsItem))
                    // {
                    //     string errMsg = $"FindTag(): Tag element could not be found in response.";
                    //     edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                    //     return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                    // }

                    // string[] pkgTags = GetTagsFromJsonElement(tagsItem);
                    // bool isTagMatch = true;
                    // foreach (string rqTag in tags)
                    // {
                    //     if (!pkgTags.Contains(rqTag, StringComparer.OrdinalIgnoreCase))
                    //     {
                    //         isTagMatch = false;
                    //         break;
                    //     }
                    // }

                    // if (!isTagMatch)
                    // {
                    //     continue;
                    // }

                    matchingResponses.Add(pkgEntry.ToString());
                }
                catch (Exception e)
                {
                    string errMsg = $"FindTag(): Id or Version element could not be parsed from response due to exception {e.Message}.";
                    edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                }
            }

            return new FindResults(stringResponse: matchingResponses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Helper method called by FindName() and FindNameWithTag()
        /// <summary>
        private FindResults FindNameHelper(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            Dictionary<string, string> resources = GetResourcesFromServiceIndex(out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string registrationsBaseUrl = FindRegistrationsBaseUrlForAllV3(resources, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string[] versionedResponses = GetVersionedResponses(registrationsBaseUrl, packageName, catalogEntryProperty, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string latestVersionResponse = String.Empty;
            foreach (string response in versionedResponses)
            {
                JsonElement pkgVersionElement;
                try
                {
                    JsonDocument pkgVersionEntry = JsonDocument.Parse(response);
                    JsonElement rootDom = pkgVersionEntry.RootElement;
                    if (!rootDom.TryGetProperty(versionName, out pkgVersionElement))
                    {
                        edi = ExceptionDispatchInfo.Capture(new InvalidOrEmptyResponse($"Response does not contain '{versionName}' element."));
                        return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                    }
                }
                catch (Exception e)
                {
                    edi = ExceptionDispatchInfo.Capture(e);
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                }

                if (NuGetVersion.TryParse(pkgVersionElement.ToString(), out NuGetVersion pkgVersion))
                {
                    if (!pkgVersion.IsPrerelease || includePrerelease)
                    {
                        // versions are reported in descending order i.e 5.0.0, 3.0.0, 1.0.0 so grabbing the first match suffices
                        latestVersionResponse = response;
                        break;
                    }
                } 
            }

            if (String.IsNullOrEmpty(latestVersionResponse))
            {
                string errMsg = $"FindName(): Package with Name {packageName} was not found in repository {Repository.Name}.";
                edi = ExceptionDispatchInfo.Capture(new SpecifiedTagsNotFoundException(errMsg));
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            bool isTagMatch = DetermineTagsPresent(response: latestVersionResponse, tags: tags, out edi);
            if (!isTagMatch)
            {
                if (edi == null)
                {
                    string errMsg = $"FindName(): Package with Name {packageName} and Tags {String.Join(", ", tags)} was not found in repository {Repository.Name}.";
                    edi = ExceptionDispatchInfo.Capture(new SpecifiedTagsNotFoundException(errMsg));
                }

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            return new FindResults(stringResponse: new string[] { latestVersionResponse }, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }
        
        /// <summary>
        /// Helper method called by FindVersion() and FindVersionWithTag()
        /// </summary>
        private FindResults FindVersionHelper(string packageName, string version, string[] tags, ResourceType type, out ExceptionDispatchInfo edi)
        {
            if (!NuGetVersion.TryParse(version, out NuGetVersion requiredVersion))
            {
                edi = ExceptionDispatchInfo.Capture(new ArgumentException($"Version {version} to be found is not a valid NuGet version."));
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            // Get all the resources for the service index
            Dictionary<string, string> resources = GetResourcesFromServiceIndex(out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string registrationsBaseUrl = FindRegistrationsBaseUrlForAllV3(resources, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string[] versionedResponses = GetVersionedResponses(registrationsBaseUrl, packageName, catalogEntryProperty, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string latestVersionResponse = String.Empty;
            foreach (string response in versionedResponses)
            {
                // this assumes latest versions are reported first i.e 5.0.0, 3.0.0, 1.0.0
                JsonElement pkgVersionElement;
                try
                {
                    JsonDocument pkgVersionEntry = JsonDocument.Parse(response);
                    JsonElement rootDom = pkgVersionEntry.RootElement;
                    if (!rootDom.TryGetProperty(versionName, out pkgVersionElement))
                    {
                        edi = ExceptionDispatchInfo.Capture(new InvalidOrEmptyResponse($"Response does not contain '{versionName}' element for search with Name {packageName} and Version {version}."));
                        return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                    }
                }
                catch (Exception e)
                {
                    edi = ExceptionDispatchInfo.Capture(e);
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                }

                if (NuGetVersion.TryParse(pkgVersionElement.ToString(), out NuGetVersion pkgVersion))
                {
                    if (pkgVersion == requiredVersion)
                    {
                        latestVersionResponse = response;
                        break;
                    }
                }
            }

            if (String.IsNullOrEmpty(latestVersionResponse))
            {
                edi = ExceptionDispatchInfo.Capture(new InvalidOrEmptyResponse($"FindVersion(): Package with Name {packageName}, Version {version} was not found in repository {Repository.Name}"));
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            bool isTagMatch = DetermineTagsPresent(response: latestVersionResponse, tags: tags, out edi);
            if (!isTagMatch)
            {
                if (edi == null)
                {
                    string errMsg = $"FindVersion(): Package with Name {packageName}, Version {version} and Tags {String.Join(", ", tags)} was not found in repository {Repository.Name}.";
                    edi = ExceptionDispatchInfo.Capture(new SpecifiedTagsNotFoundException(errMsg));
                }

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            return new FindResults(stringResponse: new string[] { latestVersionResponse }, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Helper method that makes the HTTP request for the V3 server protocol url passed in for find APIs.
        /// </summary>
        private string HttpRequestCall(string requestUrlV3, out ExceptionDispatchInfo edi)
        {
            edi = null;
            string response = string.Empty;

            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV3);

                response = SendV3RequestAsync(request, _sessionClient).GetAwaiter().GetResult();
            }
            catch (HttpRequestException e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
            }
            catch (ArgumentNullException e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
            }
            catch (InvalidOperationException e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
            }
            catch (Exception e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
            }

            return response;
        }

        /// <summary>
        /// Helper method that makes the HTTP request for the V3 server protocol url passed in for install APIs.
        /// </summary>
        private HttpContent HttpRequestCallForContent(string requestUrlV3, out ExceptionDispatchInfo edi)
        {
            edi = null;
            HttpContent content = null;

            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV3);

                content = SendV3RequestForContentAsync(request, _sessionClient).GetAwaiter().GetResult();
            }
            catch (HttpRequestException e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
            }
            catch (ArgumentNullException e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
            }
            catch (InvalidOperationException e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
            }

            return content;
        }

        private Dictionary<string, string> GetResourcesFromServiceIndex(out ExceptionDispatchInfo edi)
        {
            Dictionary<string, string> resources = new Dictionary<string, string>();
            JsonElement[] resourcesArray = GetJsonElementArr($"{Repository.Uri}", resourcesName, out edi);
            if (edi != null)
            {
                return resources;
            }

            foreach (JsonElement resource in resourcesArray)
            {
                try
                {
                    if (!resource.TryGetProperty("@type", out JsonElement typeElement))
                    {
                        // TODO: some error
                    }

                    if (!resource.TryGetProperty("@id", out JsonElement idElement))
                    {
                        // TODO: some error
                    }

                    if (!resources.ContainsKey(typeElement.ToString()))
                    {
                        // Some resources have a primary and secondary entry. The @id value is the same, so we only choose the primary entry.
                        resources.Add(typeElement.ToString(), idElement.ToString());
                    }

                }
                catch (Exception e)
                {
                    string errMsg = $"Exception parsing service index JSON for respository {Repository.Name} with error: {e.Message}";
                    edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                    return new Dictionary<string, string>();
                }
            }

            return resources;
        }
        
        private string FindRegistrationsBaseUrlForAllV3(Dictionary<string, string> resources, out ExceptionDispatchInfo edi)
        {
            edi = null;
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
                edi = ExceptionDispatchInfo.Capture(new V3ResourceNotFoundException($"RegistrationBaseUrl resource could not be found for Repository '{Repository.Name}'"));
            }

            return registrationsBaseUrl;
        }

        private string FindSearchQueryService(Dictionary<string, string> resources, out ExceptionDispatchInfo edi)
        {
            edi = null;
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
                edi = ExceptionDispatchInfo.Capture(new V3ResourceNotFoundException($"SearchQueryService resource could not be found for Repository '{Repository.Name}'"));
            }

            return searchQueryServiceUrl;
        }

        private bool IsLatestVersionFirst(string[] versionedResponses, string upperVersion, out ExceptionDispatchInfo edi)
        {
            edi = null;
            bool latestVersionFirst = true;
            // We don't need to perform this check if no responses, or single response
            if (versionedResponses.Length < 2)
            {
                return latestVersionFirst;
            }

            string firstResponse = versionedResponses[0];
            JsonDocument firstResponseJson = JsonDocument.Parse(firstResponse);
            JsonElement firstResponseDom = firstResponseJson.RootElement;
            if (!firstResponseDom.TryGetProperty(versionName, out JsonElement firstVersionElement))
            {
                edi = ExceptionDispatchInfo.Capture(new JsonParsingException("Response did not contain 'version' element"));
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

            return latestVersionFirst;
        }

        /// <summary>
        /// Helper method iterates through the entries in the registrationsUrl for a specific package and all its versions.
        /// This contains an inner items element (containing the package metadata) and the packageContent element (containing URI through which the .nupkg can be downloaded)
        /// <param name="property"> This can be the "catalogEntry" or "packageContent" property.
        ///     The "catalogEntry" property is used for search, and the value is package metadata.
        ///     The "packageContent" property is used for download, and the value is a URI for the .nupkg file.
        /// </param>
        /// <summary>
        private string[] GetVersionedResponses(string registrationsBaseUrl, string packageName, string property, out ExceptionDispatchInfo edi)
        {
            List<string> versionedResponses = new List<string>();
            string[] versionedResponseArr;
            var requestPkgMapping = $"{registrationsBaseUrl}{packageName.ToLower()}/index.json";
            string pkgMappingResponse = HttpRequestCall(requestPkgMapping, out edi);
            if (edi != null)
            {
                return Utils.EmptyStrArray;
            }

            try
            {
                // parse out JSON response we get from RegistrationsUrl
                JsonDocument pkgVersionEntry = JsonDocument.Parse(pkgMappingResponse);

                // The response has a "items" array element, which only has useful 1st element
                JsonElement rootDom = pkgVersionEntry.RootElement;
                rootDom.TryGetProperty(itemsName, out JsonElement itemsElement);
                if (itemsElement.GetArrayLength() == 0)
                {
                    edi = ExceptionDispatchInfo.Capture(new ArgumentException($"Response does not contain '{itemsName}' element, for package with Name {packageName}."));
                    return Utils.EmptyStrArray;
                }

                JsonElement firstItem = itemsElement[0];

                // The "items" property contains an inner "items" element and a "count" element
                // The inner "items" property is the metadata array for each version of the package.
                // The "count" property represents how many versions are present for that package, (i.e how many elements are in the inner "items" array)
                // JsonElement innerItemsElements = firstItem.GetProperty(itemsName);
                if (!firstItem.TryGetProperty(itemsName, out JsonElement innerItemsElements))
                {
                    edi = ExceptionDispatchInfo.Capture(new ArgumentException($"Response does not contain inner '{itemsName}' element, for package with Name {packageName}."));
                    return Utils.EmptyStrArray;
                }
                
                if (!firstItem.TryGetProperty(countName, out JsonElement countElement) || !countElement.TryGetInt32(out int count))
                {
                    edi = ExceptionDispatchInfo.Capture(new ArgumentException($"Response does not contain inner '{countName}' element or it is not a valid integer, for package with Name {packageName}."));
                    return Utils.EmptyStrArray;
                }

                if (!firstItem.TryGetProperty("upper", out JsonElement upperVersionElement))
                {
                    edi = ExceptionDispatchInfo.Capture(new ArgumentException($"Response does not contain inner 'upper' element, for package with Name {packageName}."));
                    return Utils.EmptyStrArray;
                }

                for (int i = 0; i < count; i++)
                {
                    // Get the specific entry for each package version
                    JsonElement versionedItem = innerItemsElements[i];
                    if (!versionedItem.TryGetProperty(property, out JsonElement metadataElement))
                    {
                        edi = ExceptionDispatchInfo.Capture(new ArgumentException($"Response does not contain inner '{property}' element, for package with Name {packageName}."));
                        return Utils.EmptyStrArray;
                    }
                    
                    versionedResponses.Add(metadataElement.ToString());
                }

                // Reverse array of versioned responses, if needed, so that version entries are in descending order.
                string upperVersion = upperVersionElement.ToString();
                versionedResponseArr = versionedResponses.ToArray();
                if (!IsLatestVersionFirst(versionedResponseArr, upperVersion, out edi))
                {
                    Array.Reverse(versionedResponseArr);
                }

            }
            catch (Exception e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
                return Utils.EmptyStrArray;
            }

            return versionedResponseArr;
        }

        /// <summary>
        /// Helper method that determines if specified tags are present in response representing package(s).
        /// </summary>
        private bool DetermineTagsPresent(string response, string[] tags, out ExceptionDispatchInfo edi)
        {
            edi = null;
            string[] pkgTags = Utils.EmptyStrArray;

            try
            {
                JsonDocument pkgMappingDom = JsonDocument.Parse(response);
                JsonElement rootPkgMappingDom = pkgMappingDom.RootElement;

                if (!rootPkgMappingDom.TryGetProperty(tagsName, out JsonElement tagsElement))
                {
                    string errMsg = $"FindNameWithTag(): Tags element could not be found in response or was empty.";
                    edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                    return false;
                }

                pkgTags = GetTagsFromJsonElement(tagsElement: tagsElement);
            }
            catch (Exception e)
            {
                string errMsg = $"DetermineTagsPresent(): Exception parsing JSON for respository {Repository.Uri} with error: {e.Message}";
                edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                return false;
            }

            bool isTagMatch = DeterminePkgTagsSatisfyRequiredTags(pkgTags: pkgTags, requiredTags: tags);

            return isTagMatch;
        }

        /// <summary>
        /// Helper method that finds all tags for package with the given tags JSonElement.
        /// </summary>
        private string[] GetTagsFromJsonElement(JsonElement tagsElement)
        {
            List<string> tagsFound = new List<string>();
            JsonElement[] pkgTagElements = tagsElement.EnumerateArray().ToArray();
            foreach (JsonElement tagItem in pkgTagElements)
            {
                tagsFound.Add(tagItem.ToString().ToLower());
            }

            return tagsFound.ToArray();
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
        /// Helper method that parses response for given property and returns result for that property as a JsonElement array.
        /// </summary>
        private JsonElement[] GetJsonElementArr(string request, string propertyName, out ExceptionDispatchInfo edi)
        {
            // TODO: for "data" propertyName, basically SearchQueryService, if "totalHits" == 0 no matches were found, so populate edi and caller and early out.
            JsonElement[] pkgsArr = new JsonElement[0];
            try
            { 
                string response = HttpRequestCall(request, out edi);
                if (edi != null)
                {
                    return new JsonElement[]{};
                }

                JsonDocument pkgsDom = JsonDocument.Parse(response);

                pkgsDom.RootElement.TryGetProperty(propertyName, out JsonElement pkgs);

                pkgsArr = pkgs.EnumerateArray().ToArray();
            }
            catch (Exception e)
            {
                string errMsg = $"Exception parsing JSON for respository {Repository.Uri} with error: {e.Message}";
                edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
            }

            return pkgsArr;
        }

        /// <summary>
        /// Helper method called by HttpRequestCall() that makes the HTTP request for string response.
        /// </summary>
        private static async Task<string> SendV3RequestAsync(HttpRequestMessage message, HttpClient s_client)
        {
            string errMsg = "SendV3RequestAsync(): Error occured while trying to retrieve response: ";

            try
            {
                HttpResponseMessage response = await s_client.SendAsync(message);
                response.EnsureSuccessStatusCode();

                var responseStr = await response.Content.ReadAsStringAsync();

                return responseStr;
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException(errMsg + e.Message);
            }
            catch (ArgumentNullException e)
            {
                throw new ArgumentNullException(errMsg + e.Message);
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException(errMsg + e.Message);
            }
        }

        /// <summary>
        /// Helper method called by HttpRequestCallForContent() that makes the HTTP request for string response.
        /// </summary>
        private static async Task<HttpContent> SendV3RequestForContentAsync(HttpRequestMessage message, HttpClient s_client)
        {
            string errMsg = "SendV3RequestForContentAsync(): Error occured while trying to retrieve response for content: ";

            try
            {
                HttpResponseMessage response = await s_client.SendAsync(message);
                response.EnsureSuccessStatusCode();
                return response.Content;
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException(errMsg + e.Message);
            }
            catch (ArgumentNullException e)
            {
                throw new ArgumentNullException(errMsg + e.Message);
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException(errMsg + e.Message);
            }
        }

        #endregion
    }
}
