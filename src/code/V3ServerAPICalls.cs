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
    internal class V3ServerAPICalls : ServerApiCall
    {
        #region Members
        public override PSRepositoryInfo repository { get; set; }
        public override HttpClient s_client { get; set; }

        private static readonly string resourcesName = "resources";
        private static readonly string packageBaseAddressName = "PackageBaseAddress/3.0.0";
        private static readonly string searchQueryServiceName = "SearchQueryService/3.0.0-beta";
        private static readonly string registrationsBaseUrlName = "RegistrationsBaseUrl/Versioned";
        private static readonly string dataName = "data";
        private static readonly string idName = "id";
        private static readonly string versionName = "version";
        private static readonly string tagsName = "tags";
        private static readonly string versionsName = "versions";

        #endregion

        #region Constructor

        public V3ServerAPICalls(PSRepositoryInfo repository, NetworkCredential networkCredential) : base(repository, networkCredential)
        {
            this.repository = repository;

            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                Credentials = networkCredential
            };

            s_client = new HttpClient(handler);
        }

        #endregion

        #region Overriden Methods

        /// <summary>
        /// Find method which allows for searching for all packages from a repository and returns latest version for each.
        /// Not supported
        /// </summary>
        public override string[] FindAll(bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            string errMsg = $"Find all is not supported for the repository {repository.Uri}";
            edi = ExceptionDispatchInfo.Capture(new InvalidOperationException(errMsg));

            return Utils.EmptyStrArray;
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag from a repository and returns latest version for each.
        /// Examples: Search -Tag "Redis" -Repository PSGallery
        /// API call: 
        /// https://azuresearch-ussc.nuget.org/query?q=tags:redis&prerelease=False&semVerLevel=2.0.0
        /// 
        /// Azure Artifacts does not support querying on tags, so if support this scenario we need to search on the term and then filter
        /// </summary>
        public override string[] FindTag(string tag, bool includePrerelease, ResourceType _type, out ExceptionDispatchInfo edi)
        {
            List<string> responses = new List<string>();

            Hashtable resourceUrls = FindResourceType(new string[] { searchQueryServiceName, registrationsBaseUrlName }, out edi);
            if (edi != null)
            {
                return responses.ToArray();
            }

            string searchQueryServiceUrl = resourceUrls[searchQueryServiceName] as string;
            string registrationsBaseUrl = resourceUrls[registrationsBaseUrlName] as string;

            bool isNuGetRepo = searchQueryServiceUrl.Contains("nuget.org");

            string query = isNuGetRepo ? $"{searchQueryServiceUrl}?q=tags:{tag.ToLower()}&prerelease={includePrerelease}&semVerLevel=2.0.0" :
                          $"{searchQueryServiceUrl}?q={tag.ToLower()}&prerelease={includePrerelease}&semVerLevel=2.0.0";

            // 2) call query with tags. (for Azure artifacts) get unique names, see which ones truly match
            JsonElement[] tagPkgs = GetJsonElementArr(query, dataName, out edi);
            if (edi != null)
            {
                return responses.ToArray();
            }

            List<string> matchingResponses = new List<string>();
            string id;
            string latestVersion;
            foreach (var pkgId in tagPkgs)
            { 
                try
                {
                    if (!pkgId.TryGetProperty(idName, out JsonElement idItem) || !pkgId.TryGetProperty(versionName, out JsonElement versionItem))
                    {
                        string errMsg = $"FindTag(): Id or Version element could not be found in response.";
                        edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                        return Utils.EmptyStrArray;
                    }

                    id = idItem.ToString();
                    latestVersion = versionItem.ToString();
                }
                catch (Exception e)
                {
                    string errMsg = $"FindTag(): Id or Version element could not be parsed from response due to exception {e.Message}.";
                    edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                    return Utils.EmptyStrArray;
                }

                // determine if id matches our wildcard criteria
                if (isNuGetRepo)
                {
                    string response = FindVersionHelper(registrationsBaseUrl, id, latestVersion, out edi);
                    if (edi != null)
                    {
                        return Utils.EmptyStrArray;
                    }

                    matchingResponses.Add(response);
                }
                else
                {
                    try {
                        if (!pkgId.TryGetProperty("tags", out JsonElement tagsItem))
                        {
                            string errMsg = $"FindTag(): Tag element could not be found in response.";
                            edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                            return Utils.EmptyStrArray;
                        }

                        foreach (var tagItem in tagsItem.EnumerateArray())
                        {
                            if (tag.Equals(tagItem.ToString(), StringComparison.InvariantCultureIgnoreCase))
                            {
                                string response = FindVersionHelper(registrationsBaseUrl, id, latestVersion, out edi);
                                if (edi != null)
                                {
                                    return Utils.EmptyStrArray;
                                }

                                matchingResponses.Add(response);
                                break;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        string errMsg = $"FindTag(): Tags element could not be parsed from response due to exception {e.Message}.";
                        edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                        return Utils.EmptyStrArray;
                    }
                }
            }

            return matchingResponses.ToArray();
        }

        /// <summary>
        /// This functionality is not supported for V3 protocol server.
        /// Find method which allows for searching for packages with specified Command or DSCResource name.
        /// </summary>
        public override string[] FindCommandOrDscResource(string tag, bool includePrerelease, bool isSearchingForCommands, out ExceptionDispatchInfo edi)
        {
            string errMsg = $"Find by CommandName or DSCResource is not supported for {repository.Name} as it uses the V3 server protocol";
            edi = ExceptionDispatchInfo.Capture(new InvalidOperationException(errMsg));

            return Utils.EmptyStrArray;
        }

        /// <summary>
        /// Find method which allows for searching for single name and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "Newtonsoft.Json"
        /// API call: 
        ///               https://api.nuget.org/v3/registration5-gz-semver2/nuget.server/index.json
        ///               https://msazure.pkgs.visualstudio.com/One/_packaging/testfeed/nuget/v3/registrations2-semver2/newtonsoft.json/index.json
        ///               https://msazure.pkgs.visualstudio.com/999aa88e-7ed7-41b2-9d77-5bc261222004/_packaging/0d5429e2-c871-4347-bdc9-d1cbbac5eb3b/nuget/v3/registrations2-semver2/newtonsoft.json/index.json
        /// The RegistrationBaseUrl that we're using is "RegistrationBaseUrl/Versioned"
        /// This type points to the url to use (ex above)
        /// Implementation Note: Need to filter further for latest version (prerelease or non-prerelease dependening on user preference)
        /// </summary>
        public override string FindName(string packageName, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            Hashtable resourceUrls = FindResourceType(new string[] { packageBaseAddressName, registrationsBaseUrlName }, out edi);
            if (edi != null)
            {
                return String.Empty;
            }

            string packageBaseAddressUrl = resourceUrls[packageBaseAddressName] as string;
            string registrationsBaseUrl = resourceUrls[registrationsBaseUrlName] as string;

            bool isNuGetRepo = packageBaseAddressUrl.Contains("v3-flatcontainer");
            JsonElement[] pkgVersionsArr = GetPackageVersions(packageBaseAddressUrl, packageName, isNuGetRepo, out edi);
            if (edi != null)
            {
                return String.Empty;
            }

            string response = string.Empty;
            foreach (JsonElement version in pkgVersionsArr)
            {
                // parse as NuGetVersion
                if (NuGetVersion.TryParse(version.ToString(), out NuGetVersion nugetVersion))
                {
                    /* 
                     * pkgVersion == !prerelease   &&   includePrerelease == true   -->   keep pkg   
                     * pkgVersion == !prerelease   &&   includePrerelease == false  -->   keep pkg 
                     * pkgVersion == prerelease    &&   includePrerelease == true   -->   keep pkg   
                     * pkgVersion == prerelease    &&   includePrerelease == false  -->   throw away pkg 
                     */
                    if (!nugetVersion.IsPrerelease || includePrerelease)
                    {
                        response = FindVersionHelper(registrationsBaseUrl, packageName, version.ToString(), out edi);
                        if (edi != null)
                        {
                            return String.Empty;
                        }

                        break;
                    }
                }
            }

            if (String.IsNullOrEmpty(response))
            {
                edi = ExceptionDispatchInfo.Capture(new InvalidOrEmptyResponse($"FindName() with {packageName} returned empty response."));
            }

            return response;
        }

        /// <summary>
        /// Find method which allows for searching for single name and tag and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "Newtonsoft.Json" - Tag "json"
        /// </summary>
        public override string FindNameWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            Hashtable resourceUrls = FindResourceType(new string[] { packageBaseAddressName, registrationsBaseUrlName }, out edi);
            if (edi != null)
            {
                return String.Empty;
            }

            string packageBaseAddressUrl = resourceUrls[packageBaseAddressName] as string;
            string registrationsBaseUrl = resourceUrls[registrationsBaseUrlName] as string;

            bool isNuGetRepo = packageBaseAddressUrl.Contains("v3-flatcontainer");
            JsonElement[] pkgVersionsArr = GetPackageVersions(packageBaseAddressUrl, packageName, isNuGetRepo, out edi);
            if (edi != null)
            {
                return String.Empty;
            }

            string response = string.Empty;
            foreach (JsonElement version in pkgVersionsArr)
            {
                // parse as NuGetVersion
                if (NuGetVersion.TryParse(version.ToString(), out NuGetVersion nugetVersion))
                {
                    /* 
                     * pkgVersion == !prerelease   &&   includePrerelease == true   -->   keep pkg   
                     * pkgVersion == !prerelease   &&   includePrerelease == false  -->   keep pkg 
                     * pkgVersion == prerelease    &&   includePrerelease == true   -->   keep pkg   
                     * pkgVersion == prerelease    &&   includePrerelease == false  -->   throw away pkg 
                     */
                    if (!nugetVersion.IsPrerelease || includePrerelease)
                    {
                        response = FindVersionHelper(registrationsBaseUrl, packageName, version.ToString(), out edi);
                        if (edi != null)
                        {
                            return String.Empty;
                        }

                        bool isTagMatch = DetermineTagsPresent(response: response, tags: tags, out edi);

                        if (!isTagMatch)
                        {
                            if (edi == null)
                            {
                                string errMsg = $"FindNameWithTag(): Tags required were not found in package {packageName} {version.ToString()}.";
                                edi = ExceptionDispatchInfo.Capture(new SpecifiedTagsNotFoundException(errMsg));
                            }

                            return String.Empty;
                        }

                        break;
                    }
                }
            }

            if (String.IsNullOrEmpty(response))
            {
                edi = ExceptionDispatchInfo.Capture(new InvalidOrEmptyResponse($"FindNameWithTag() with {packageName} and tags {String.Join(",", tags)} returned empty response."));
            }

            return response;
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "Nuget.Server*"
        /// API call: 
        /// - No prerelease: https://api-v2v3search-0.nuget.org/autocomplete?q=storage&prerelease=false
        /// - Prerelease:  https://api-v2v3search-0.nuget.org/autocomplete?q=storage&prerelease=true  
        /// 
        /// https://msazure.pkgs.visualstudio.com/b32aa71e-8ed2-41b2-9d77-5bc261222004/_packaging/0d5429e2-c871-4347-bdc9-d1cbbac5eb3b/nuget/v3/query2?q=Newtonsoft&prerelease=false&semVerLevel=2.0.0
        ///         
        ///        Note:  response only returns names
        ///        
        ///        Make another query to get the latest version of each package  (ie call "FindVersionGlobbing")
        /// </summary>
        public override string[] FindNameGlobbing(string packageName, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            var names = packageName.Split(new char[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
            string querySearchTerm;

            if (names.Length == 0)
            {
                edi = ExceptionDispatchInfo.Capture(new ArgumentException("-Name '*' for V3 server protocol repositories is not supported"));
                return Utils.EmptyStrArray;
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
                return Utils.EmptyStrArray;
            }

            // https://msazure.pkgs.visualstudio.com/.../_packaging/.../nuget/v3/query2 (no support for * in search term, but matches like NuGet)
            // https://azuresearch-usnc.nuget.org/query?q=Newtonsoft&prerelease=false&semVerLevel=1.0.0 (NuGet) (supports * at end of searchterm q but equivalent to q = text w/o *)
            Hashtable resourceUrls = FindResourceType(new string[] { searchQueryServiceName, registrationsBaseUrlName }, out edi);
            if (edi != null)
            {
                return Utils.EmptyStrArray;
            }

            string searchQueryServiceUrl = resourceUrls[searchQueryServiceName] as string;
            string registrationsBaseUrl = resourceUrls[registrationsBaseUrlName] as string;

            string query = $"{searchQueryServiceUrl}?q={querySearchTerm}&prerelease={includePrerelease}&semVerLevel=2.0.0";

            // 2) call query with search term, get unique names, see which ones truly match
            JsonElement[] matchingPkgIds = GetJsonElementArr(query, dataName, out edi);
            if (edi != null)
            {
                return Utils.EmptyStrArray;
            }

            List<string> matchingResponses = new List<string>();
            foreach (var pkgId in matchingPkgIds)
            {
                string id = string.Empty;
                string latestVersion = string.Empty;

                try
                {
                    if (!pkgId.TryGetProperty(idName, out JsonElement idItem) || ! pkgId.TryGetProperty(versionName, out JsonElement versionItem))
                    {
                        string errMsg = $"FindNameGlobbing(): Name or Version element could not be found in response.";
                        edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                        return Utils.EmptyStrArray; 
                    }

                    id = idItem.ToString();
                    latestVersion = versionItem.ToString();
                }
                catch (Exception e)
                {
                    string errMsg = $"FindNameGlobbing(): Name or Version element could not be parsed from response due to exception {e.Message}.";
                    edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                    break;
                }

                // determine if id matches our wildcard criteria
                if ((packageName.StartsWith("*") && packageName.EndsWith("*") && id.ToLower().Contains(querySearchTerm.ToLower())) ||
                    (packageName.EndsWith("*") && id.StartsWith(querySearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (packageName.StartsWith("*") && id.EndsWith(querySearchTerm, StringComparison.OrdinalIgnoreCase)))
                {
                    string response = FindVersionHelper(registrationsBaseUrl, id, latestVersion, out edi);

                    if (edi != null)
                    {
                        return Utils.EmptyStrArray;
                    }

                    matchingResponses.Add(response);
                }
            }

            return matchingResponses.ToArray();
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and tag and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "Nuget.Server*" -Tag "nuget"
        /// </summary>
        public override string[] FindNameGlobbingWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            var names = packageName.Split(new char[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
            string querySearchTerm;

            if (names.Length == 0)
            {
                edi = ExceptionDispatchInfo.Capture(new ArgumentException("-Name '*' for V3 server protocol repositories is not supported"));
                return Utils.EmptyStrArray;
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
                return Utils.EmptyStrArray;
            }

            // https://msazure.pkgs.visualstudio.com/.../_packaging/.../nuget/v3/query2 (no support for * in search term, but matches like NuGet)
            // https://azuresearch-usnc.nuget.org/query?q=Newtonsoft&prerelease=false&semVerLevel=1.0.0 (NuGet) (supports * at end of searchterm q but equivalent to q = text w/o *)
            Hashtable resourceUrls = FindResourceType(new string[] { searchQueryServiceName, registrationsBaseUrlName }, out edi);
            if (edi != null)
            {
                return Utils.EmptyStrArray;
            }

            string searchQueryServiceUrl = resourceUrls[searchQueryServiceName] as string;
            string registrationsBaseUrl = resourceUrls[registrationsBaseUrlName] as string;

            string query = $"{searchQueryServiceUrl}?q={querySearchTerm}&prerelease={includePrerelease}&semVerLevel=2.0.0";

            // 2) call query with search term, get unique names, see which ones truly match
            JsonElement[] matchingPkgIds = GetJsonElementArr(query, dataName, out edi);
            if (edi != null)
            {
                return Utils.EmptyStrArray;
            }

            List<string> matchingResponses = new List<string>();
            foreach (var pkgId in matchingPkgIds)
            {
                string id = string.Empty;
                string latestVersion = string.Empty;
                string[] pkgTags = Utils.EmptyStrArray;

                try
                {
                    if (!pkgId.TryGetProperty(idName, out JsonElement idItem) || !pkgId.TryGetProperty(versionName, out JsonElement versionItem))
                    {
                        string errMsg = $"FindNameGlobbing(): Name or Version element could not be found in response.";
                        edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                        return Utils.EmptyStrArray;
                    }

                    if (!pkgId.TryGetProperty(tagsName, out JsonElement tagsItem))
                    {
                        string errMsg = $"FindNameGlobbing(): Tags element could not be found in response.";
                        edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                        return Utils.EmptyStrArray;
                    }

                    id = idItem.ToString();
                    latestVersion = versionItem.ToString();

                    pkgTags = GetTagsFromJsonElement(tagsElement: tagsItem);
                }
                catch (Exception e)
                {
                    string errMsg = $"FindNameGlobbingWithTag(): Name or Version or Tags element could not be parsed from response due to exception {e.Message}.";
                    edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                    break;
                }

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

                    string response = FindVersionHelper(registrationsBaseUrl, id, latestVersion, out edi);

                    if (edi != null)
                    {
                        continue;
                    }

                    matchingResponses.Add(response);
                }
            }

            return matchingResponses.ToArray();
        }

        /// <summary>
        /// Find method which allows for searching for single name with version range.
        /// Name: no wildcard support
        /// Version: supports wildcards
        /// Examples: Search "NuGet.Server.Core" "[1.0.0.0, 5.0.0.0]"
        ///           Search "NuGet.Server.Core" "3.*"
        /// API Call: 
        ///           then, find all versions for a pkg
        ///           for nuget:
        ///               this contains all pkg version info: https://api.nuget.org/v3/registration5-gz-semver2/nuget.server/index.json
        ///               However, we will use the flattened version list: https://api.nuget.org/v3-flatcontainer/newtonsoft.json/index.json
        ///           for Azure Artifacts:
        ///               https://msazure.pkgs.visualstudio.com/b32aa71e-8ed2-41b2-9d77-5bc261222004/_packaging/0d5429e2-c871-4347-bdc9-d1cbbac5eb3b/nuget/v3/flat2/newtonsoft.json/index.json
        ///            (azure artifacts)
        ///            
        ///             Note:  very different responses for nuget vs azure artifacts
        ///            
        ///            After we figure out what version we want, call "FindVersion" (or some helper method)
        /// need to filter client side
        /// Implementation note: Returns all versions, including prerelease ones. Later (in the API client side) we'll do filtering on the versions to satisfy what user provided.
        /// </summary>
        public override string[] FindVersionGlobbing(string packageName, VersionRange versionRange, bool includePrerelease, ResourceType type, bool getOnlyLatest, out ExceptionDispatchInfo edi)
        {
            Hashtable resourceUrls = FindResourceType(new string[] { packageBaseAddressName, registrationsBaseUrlName }, out edi);
            if (edi != null)
            {
                return Utils.EmptyStrArray;
            }

            string packageBaseAddressUrl = resourceUrls[packageBaseAddressName] as string;
            string registrationsBaseUrl = resourceUrls[registrationsBaseUrlName] as string;

            bool isNuGetRepo = packageBaseAddressUrl.Contains("v3-flatcontainer");
            JsonElement[] pkgVersionsArr = GetPackageVersions(packageBaseAddressUrl, packageName, isNuGetRepo, out edi);
            if (edi != null)
            {
                return Utils.EmptyStrArray;
            }

            List<string> responses = new List<string>();
            foreach (var version in pkgVersionsArr) {
                if (NuGetVersion.TryParse(version.ToString(), out NuGetVersion nugetVersion) && versionRange.Satisfies(nugetVersion))
                {
                    /* 
                     * pkgVersion == !prerelease   &&   includePrerelease == true   -->   keep pkg   
                     * pkgVersion == !prerelease   &&   includePrerelease == false  -->   keep pkg 
                     * pkgVersion == prerelease    &&   includePrerelease == true   -->   keep pkg   
                     * pkgVersion == prerelease    &&   includePrerelease == false  -->   throw away pkg 
                     */
                    if (!nugetVersion.IsPrerelease || includePrerelease) {
                        string response = FindVersionHelper(registrationsBaseUrl, packageName, version.ToString(), out edi);
                        if (edi != null)
                        {
                            return Utils.EmptyStrArray;
                        }

                        responses.Add(response);
                    }
                }
            }

            return responses.ToArray();
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "NuGet.Server.Core" "3.0.0-beta"
        /// API call: 
        ///     first find the RegistrationBaseUrl
        ///     https://api.nuget.org/v3/registration5-gz-semver2/nuget.server/index.json
        ///     
        ///     https://msazure.pkgs.visualstudio.com/One/_packaging/testfeed/nuget/v3/registrations2-semver2/newtonsoft.json/index.json
        ///     https://msazure.pkgs.visualstudio.com/999aa88e-7ed7-41b2-9d77-5bc261222004/_packaging/0d5429e2-c871-4347-bdc9-d1cbbac5eb3b/nuget/v3/registrations2-semver2/newtonsoft.json/index.json
        ///         The RegistrationBaseUrl that we're using is "RegistrationBaseUrl/Versioned"
        ///         This type points to the url to use (ex above)
        ///         
        ///     then we can make a call for the specific version  
        ///     https://api.nuget.org/v3/registration5-gz-semver2/nuget.server.core/3.0.0-beta
        ///     (alternative url for nuget gallery):  https://api.nuget.org/v3/registration5-gz-semver2/nuget.server.core/index.json#page/3.0.0-beta/3.0.0-beta
        ///     https://msazure.pkgs.visualstudio.com/b32aa71e-8ed2-41b2-9d77-5bc261222004/_packaging/0d5429e2-c871-4347-bdc9-d1cbbac5eb3b/nuget/v3/registrations2/newtonsoft.json/13.0.2.json 
        ///     
        /// </summary>
        public override string FindVersion(string packageName, string version, ResourceType type, out ExceptionDispatchInfo edi)
        {
            Hashtable resourceUrls = FindResourceType(new string[] { registrationsBaseUrlName }, out edi);
            if (edi != null)
            {
                return String.Empty;
            }

            string registrationsBaseUrl = resourceUrls[registrationsBaseUrlName] as string;

            string response = FindVersionHelper(registrationsBaseUrl, packageName, version, out edi);
            if (edi != null)
            {
                return String.Empty;
            }

            if (String.IsNullOrEmpty(response))
            {
                edi = ExceptionDispatchInfo.Capture(new InvalidOrEmptyResponse($"FindVersion() with {packageName} and version {version} returned empty response."));
            }

            return response;
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version and tag.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "NuGet.Server.Core" -Version "3.0.0-beta" -Tag "nuget"
        /// API call: 
        ///     first find the RegistrationBaseUrl
        ///     https://api.nuget.org/v3/registration5-gz-semver2/nuget.server/index.json
        ///     
        ///     https://msazure.pkgs.visualstudio.com/One/_packaging/testfeed/nuget/v3/registrations2-semver2/newtonsoft.json/index.json
        ///     https://msazure.pkgs.visualstudio.com/999aa88e-7ed7-41b2-9d77-5bc261222004/_packaging/0d5429e2-c871-4347-bdc9-d1cbbac5eb3b/nuget/v3/registrations2-semver2/newtonsoft.json/index.json
        ///         The RegistrationBaseUrl that we're using is "RegistrationBaseUrl/Versioned"
        ///         This type points to the url to use (ex above)
        ///         
        ///     then we can make a call for the specific version  
        ///     https://api.nuget.org/v3/registration5-gz-semver2/nuget.server.core/3.0.0-beta
        ///     (alternative url for nuget gallery):  https://api.nuget.org/v3/registration5-gz-semver2/nuget.server.core/index.json#page/3.0.0-beta/3.0.0-beta
        ///     https://msazure.pkgs.visualstudio.com/b32aa71e-8ed2-41b2-9d77-5bc261222004/_packaging/0d5429e2-c871-4347-bdc9-d1cbbac5eb3b/nuget/v3/registrations2/newtonsoft.json/13.0.2.json 
        ///     
        /// </summary>        
        public override string FindVersionWithTag(string packageName, string version, string[] tags, ResourceType type, out ExceptionDispatchInfo edi)
        {
            Hashtable resourceUrls = FindResourceType(new string[] { registrationsBaseUrlName }, out edi);
            if (edi != null)
            {
                return String.Empty;
            }

            string registrationsBaseUrl = resourceUrls[registrationsBaseUrlName] as string;

            string response = FindVersionHelper(registrationsBaseUrl, packageName, version, out edi);
            if (edi != null)
            {
                return String.Empty;
            }

            bool isTagMatch = DetermineTagsPresent(response: response, tags: tags, out edi);

            if (!isTagMatch)
            {
                if (edi == null)
                {
                    string errMsg = $"FindVersionWithTag(): Tags required were not found in package {packageName} {version.ToString()}.";
                    edi = ExceptionDispatchInfo.Capture(new SpecifiedTagsNotFoundException(errMsg));
                }

                return String.Empty;
            }

            if (String.IsNullOrEmpty(response))
            {
                edi = ExceptionDispatchInfo.Capture(new InvalidOrEmptyResponse($"FindVersion() with {packageName}, tags {String.Join(", ", tags)} and version {version} returned empty response."));
            }

            return response;
        }

        /**  INSTALL APIS **/

        /// <summary>
        /// Installs specific package.
        /// Name: no wildcard support.
        /// Examples: Install "PowerShellGet"
        /// Implementation Note:   if not prerelease: https://www.powershellgallery.com/api/v2/package/powershellget (Returns latest stable)
        ///                        if prerelease, the calling method should first call IFindPSResource.FindName(), 
        ///                             then find the exact version to install, then call into install version
        /// </summary>
        public override Stream InstallName(string packageName, bool includePrerelease, out ExceptionDispatchInfo edi)
        {
            Hashtable resourceUrls = FindResourceType(new string[] { packageBaseAddressName }, out edi);
            if (edi != null)
            {
                return null;
            }

            string packageBaseAddressUrl = resourceUrls[packageBaseAddressName] as string;

            bool isNuGetRepo = packageBaseAddressUrl.Contains("v3-flatcontainer");

            JsonElement[] pkgVersionsArr = GetPackageVersions(packageBaseAddressUrl, packageName, isNuGetRepo, out edi);
            if (edi != null)
            {
                return null;
            }

            foreach (JsonElement version in pkgVersionsArr)
            {
                if (NuGetVersion.TryParse(version.ToString(), out NuGetVersion nugetVersion))
                {
                    /* 
                     * pkgVersion == !prerelease   &&   includePrerelease == true   -->   keep pkg   
                     * pkgVersion == !prerelease   &&   includePrerelease == false  -->   keep pkg 
                     * pkgVersion == prerelease    &&   includePrerelease == true   -->   keep pkg   
                     * pkgVersion == prerelease    &&   includePrerelease == false  -->   throw away pkg 
                     */
                    if (!nugetVersion.IsPrerelease || includePrerelease)
                    {
                        var responseStream = InstallVersion(packageName, version.ToString(), out edi);
                        if (edi != null)
                        {
                            return null;
                        }

                        return responseStream;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Installs package with specific name and version.
        /// Name: no wildcard support.
        /// Version: no wildcard support.
        /// Examples: Install "PowerShellGet" -Version "3.0.0.0"
        ///           Install "PowerShellGet" -Version "3.0.0-beta16"
        ///           
        ///  https://api.nuget.org/v3-flatcontainer/newtonsoft.json/9.0.1/newtonsoft.json.9.0.1.nupkg
        /// API Call: 
        /// </summary>    
        public override Stream InstallVersion(string packageName, string version, out ExceptionDispatchInfo edi)
        {
            Hashtable resourceUrls = FindResourceType(new string[] { packageBaseAddressName }, out edi);
            if (edi != null)
            {
                return null;
            }

            string packageBaseAddressUrl = resourceUrls[packageBaseAddressName] as string;

            string pkgName = packageName.ToLower();
            string installPkgUrl = $"{packageBaseAddressUrl}{pkgName}/{version}/{pkgName}.{version}.nupkg";

            var content = HttpRequestCallForContent(installPkgUrl, out edi);
            if (edi != null)
            {
                return null;
            }

            return content.ReadAsStreamAsync().Result;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Helper method that makes the HTTP request for the V3 server protocol url passed in for find APIs.
        /// </summary>
        private String HttpRequestCall(string requestUrlV3, out ExceptionDispatchInfo edi)
        {
            edi = null;
            string response = string.Empty;

            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV3);

                response = SendV3RequestAsync(request, s_client).GetAwaiter().GetResult();
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

                content = SendV3RequestForContentAsync(request, s_client).GetAwaiter().GetResult();
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

        /// <summary>
        /// Helper method that makes finds the specified V3 server protocol resources from the service index.
        /// </summary>
        private Hashtable FindResourceType(string[] resourceTypeName, out ExceptionDispatchInfo edi)
        {
            Hashtable resourceHash = new Hashtable();
            JsonElement[] resources = GetJsonElementArr($"{repository.Uri}", resourcesName, out edi);
            if (edi != null)
            {
                return resourceHash;
            }

            foreach (JsonElement resource in resources)
            {
                try
                {
                    if (resource.TryGetProperty("@type", out JsonElement typeElement) && resourceTypeName.Contains(typeElement.ToString()))
                    {
                        // check if key already present in hastable, as there can be resources with same type but primary/secondary instances
                        if (!resourceHash.ContainsKey(typeElement.ToString()))
                        {
                            if (resource.TryGetProperty("@id", out JsonElement idElement))
                            {
                                // add name of the resource and its url
                                resourceHash.Add(typeElement.ToString(), idElement.ToString());
                            }
                            else
                            {
                                string errMsg = $"@type element was found but @id element not found in service index '{repository.Uri}' for {resourceTypeName}.";
                                edi = ExceptionDispatchInfo.Capture(new V3ResourceNotFoundException(errMsg));
                                return resourceHash;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    string errMsg = $"Exception parsing JSON for respository {repository.Uri} with error: {e.Message}";
                    edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                    return resourceHash;
                }

                if (resourceHash.Count == resourceTypeName.Length)
                {
                    break;
                }
            }

            foreach (string resourceType in resourceTypeName)
            {
                if (!resourceHash.ContainsKey(resourceType))
                {
                    string errMsg = $"FindResourceType(): Could not find resource type {resourceType} from the service index.";
                    edi = ExceptionDispatchInfo.Capture(new V3ResourceNotFoundException(errMsg));
                    break;
                }
            }

            return resourceHash;
        }

        /// <summary>
        /// Helper method finds package with name and specified version
        /// <summary>
        private string FindVersionHelper(string registrationsBaseUrl, string packageName, string version, out ExceptionDispatchInfo edi)
        {
            // https://api.nuget.org/v3/registration5-gz-semver2/newtonsoft.json/13.0.2.json
            var requestPkgMapping = $"{registrationsBaseUrl}{packageName.ToLower()}/{version}.json";
            string pkgMappingResponse = HttpRequestCall(requestPkgMapping, out edi);
            if (edi != null)
            {
                return String.Empty;
            }

            string catalogEntryUrl = string.Empty;
            try
            {
                JsonDocument pkgMappingDom = JsonDocument.Parse(pkgMappingResponse);
                JsonElement rootPkgMappingDom = pkgMappingDom.RootElement;

                if (!rootPkgMappingDom.TryGetProperty("catalogEntry", out JsonElement catalogEntryUrlElement))
                {
                    string errMsg = $"FindVersionHelper(): CatalogEntry element could not be found in response or was empty.";
                    edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                    return String.Empty;
                }

                catalogEntryUrl = catalogEntryUrlElement.ToString();
            }
            catch (Exception e)
            {
                string errMsg = $"FindVersionHelper(): Exception parsing JSON for respository {repository.Uri} with error: {e.Message}";
                edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                return String.Empty;
            }

            string response = HttpRequestCall(catalogEntryUrl, out edi);
            if (edi != null)
            {
                return String.Empty;
            }

            return response;
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
                string errMsg = $"DetermineTagsPresent(): Exception parsing JSON for respository {repository.Uri} with error: {e.Message}";
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
                if (!pkgTags.Contains(tag.ToLower()))
                {
                    isTagMatch = false;
                    break;
                }
            }
            
            return isTagMatch;
        }

        /// <summary>
        /// Helper method that returns a flattened list of all versions present for a package.
        /// Implementation note: NuGet server and Azure Artifacts server return this flattened version list in opposite orders so we reverse array accordingly.
        /// </summary>
        private JsonElement[] GetPackageVersions(string packageBaseAddressUrl, string packageName, bool isNuGetRepo, out ExceptionDispatchInfo edi)
        {
            if (String.IsNullOrEmpty(packageBaseAddressUrl))
            {
                edi = ExceptionDispatchInfo.Capture(new ArgumentException($"GetPackageVersions(): Package Base URL cannot be null or empty"));
                return new JsonElement[]{};
            }

            JsonElement[] pkgVersionsElement = GetJsonElementArr($"{packageBaseAddressUrl}{packageName.ToLower()}/index.json", versionsName, out edi);
            if (edi != null)
            {
                return new JsonElement[]{};
            }

            return isNuGetRepo ? pkgVersionsElement.Reverse().ToArray() : pkgVersionsElement.ToArray();
        }

        /// <summary>
        /// Helper method that parses response for given property and returns result for that property as a JsonElement array.
        /// </summary>
        private JsonElement[] GetJsonElementArr(string request, string propertyName, out ExceptionDispatchInfo edi)
        {
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
                string errMsg = $"Exception parsing JSON for respository {repository.Uri} with error: {e.Message}";
                edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
            }

            return pkgsArr;
        }

        /// <summary>
        /// Helper method called by HttpRequestCall() that makes the HTTP request for string response.
        /// </summary>
        public static async Task<string> SendV3RequestAsync(HttpRequestMessage message, HttpClient s_client)
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
        public static async Task<HttpContent> SendV3RequestForContentAsync(HttpRequestMessage message, HttpClient s_client)
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
