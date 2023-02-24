// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections;
using System.Runtime.ExceptionServices;
using System.ComponentModel;
using ExceptionHandlingDemo;

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
        // High level design: Find-PSResource >>> IFindPSResource (loops, version checks, etc.) >>> IServerAPICalls (call to repository endpoint/url)    

        /// <summary>
        /// Find method which allows for searching for all packages from a repository and returns latest version for each.
        /// Not supported
        /// </summary>
        public override string[] FindAll(bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            string errMsg = $"Find all is not supported for the repository {repository.Uri.ToString()}";
            edi = new OperationNotSupportedException(errMsg)
            
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
            string searchQueryServiceUrl = resourceUrls[searchQueryServiceName] as string;
            string registrationsBaseUrl = resourceUrls[registrationsBaseUrlName] as string;

            bool isNuGetRepo = searchQueryServiceUrl.Contains("nuget.org");

            string query = isNuGetRepo ? $"{searchQueryServiceUrl}?q=tags:{tag.ToLower()}&prerelease={includePrerelease}&semVerLevel=2.0.0" :
                          $"{searchQueryServiceUrl}?q={tag.ToLower()}&prerelease={includePrerelease}&semVerLevel=2.0.0";

            // 2) call query with tags. (for Azure artifacts) get unique names, see which ones truly match
            JsonElement[] tagPkgs = GetJsonElementArr(query, dataName, out edi);

            List<string> matchingResponses = new List<string>();
            foreach (var pkgId in tagPkgs)

                try
                {
                    pkgId.TryGetProperty(idName, out JsonElement idItem);
                    pkgId.TryGetProperty(versionName, out JsonElement versionItem);
                }
                catch (InvalidOperationException e)
                {
                    edi = ExceptionDispatchInfo.Capture(e);
                }
                catch (ArgumentNullException e)
                {
                    edi = ExceptionDispatchInfo.Capture(e);
                }
                catch (ObjectDisposedException e)
                {
                    edi = ExceptionDispatchInfo.Capture(e);
                }

                string id = idItem.ToString();
                string latestVersion = versionItem.ToString();

                // determine if id matches our wildcard criteria
                if (isNuGetRepo)
                {
                    matchingResponses.Add(FindVersionHelper(registrationsBaseUrl, id, latestVersion, out edi));
                }
                else
                {
                    try {
                        pkgId.TryGetProperty("tags", out JsonElement tagsItem);
                    }
                    catch (InvalidOperationException e)
                    {
                        edi = ExceptionDispatchInfo.Capture(e);
                    }
                    catch (ArgumentNullException e)
                    {
                        edi = ExceptionDispatchInfo.Capture(e);
                    }
                    catch (ObjectDisposedException e)
                    {
                        edi = ExceptionDispatchInfo.Capture(e);
                    }
                
                    foreach (var tagItem in tagsItem.EnumerateArray())
                    {
                        if (tag.Equals(tagItem.ToString(), StringComparison.InvariantCultureIgnoreCase))
                        {
                            matchingResponses.Add(FindVersionHelper(registrationsBaseUrl, id, latestVersion, out edi));
                            break;
                        }
                    }
                }
            }

            return matchingResponses.ToArray();
        }

        public override string[] FindCommandOrDscResource(string tag, bool includePrerelease, bool isSearchingForCommands, out ExceptionDispatchInfo edi)
        {
            string errMsg = $"Find by CommandName or DSCResource is not supported for {repository.Name} as it uses the V3 server protocol";
            edi = new OperationNotSupportedException(errMsg);

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
            string packageBaseAddressUrl = resourceUrls[packageBaseAddressName] as string;
            string registrationsBaseUrl = resourceUrls[registrationsBaseUrlName] as string;

            bool isNuGetRepo = packageBaseAddressUrl.Contains("v3-flatcontainer");   // TODO:  check that you can cast
            JsonElement[] pkgVersionsArr = GetPackageVersions(packageBaseAddressUrl, packageName, isNuGetRepo, out edi);

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

                        break;
                    }
                }
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
            edi = null;
            List<string> responses = new List<string>();
            string errRecord = string.Empty;

            var names = packageName.Split(new char[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
            string querySearchTerm = String.Empty;

            if (names.Length == 0)
            {
                errRecord = "We don't support -Name *";
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

                errRecord = "We only support wildcards for scenarios similar to the following examples: PowerShell*, *ShellGet, *Shell*.";
                return Utils.EmptyStrArray;
            }

            // https://msazure.pkgs.visualstudio.com/.../_packaging/.../nuget/v3/query2 (no support for * in search term, but matches like NuGet)
            // https://azuresearch-usnc.nuget.org/query?q=Newtonsoft&prerelease=false&semVerLevel=1.0.0 (NuGet) (supports * at end of searchterm q but equivalent to q = text w/o *)
            Hashtable resourceUrls = FindResourceType(new string[] { searchQueryServiceName, registrationsBaseUrlName }, out edi);
            string searchQueryServiceUrl = resourceUrls[searchQueryServiceName] as string;
            string registrationsBaseUrl = resourceUrls[registrationsBaseUrlName] as string;

            string query = $"{searchQueryServiceUrl}?q={querySearchTerm}&prerelease={includePrerelease}&semVerLevel=2.0.0";

            // 2) call query with search term, get unique names, see which ones truly match
            JsonElement[] matchingPkgIds = GetJsonElementArr(query, dataName, out edi);

            List<string> matchingResponses = new List<string>();
            foreach (var pkgId in matchingPkgIds) {

                try
                {
                    pkgId.TryGetProperty(idName, out JsonElement idItem);
                    pkgId.TryGetProperty(versionName, out JsonElement versionItem);
                }
                catch (InvalidOperationException e)
                {
                    edi = ExceptionDispatchInfo.Capture(e);
                    break;
                }
                catch (ArgumentNullException e)
                {
                    edi = ExceptionDispatchInfo.Capture(e);
                    break;
                }
                catch (ObjectDisposedException e)
                {
                    edi = ExceptionDispatchInfo.Capture(e);
                    break;
                }

                string id = idItem.ToString();
                string latestVersion = versionItem.ToString();

                // determine if id matches our wildcard criteria
                if ((packageName.StartsWith("*") && packageName.EndsWith("*") && id.Contains(querySearchTerm)) ||
                    (packageName.EndsWith("*") && id.StartsWith(querySearchTerm)) ||
                    (packageName.StartsWith("*") && id.EndsWith(querySearchTerm)))
                {
                    matchingResponses.Add(FindVersionHelper(registrationsBaseUrl, id, latestVersion, out edi));
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
            string packageBaseAddressUrl = resourceUrls[packageBaseAddressName] as string;
            string registrationsBaseUrl = resourceUrls[registrationsBaseUrlName] as string;

            bool isNuGetRepo = packageBaseAddressUrl.Contains("v3-flatcontainer");
            JsonElement[] pkgVersionsArr = GetPackageVersions(packageBaseAddressUrl, packageName, isNuGetRepo, out edi);

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
                        responses.Add(FindVersionHelper(registrationsBaseUrl, packageName, version.ToString(), out edi));
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
            string registrationsBaseUrl = resourceUrls[registrationsBaseUrlName] as string;

            return FindVersionHelper(registrationsBaseUrl, packageName, version, out edi);
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
        public override HttpContent InstallName(string packageName, bool includePrerelease, out ExceptionDispatchInfo edi)
        {
            Hashtable resourceUrls = FindResourceType(new string[] { packageBaseAddressName }, out edi);
            string packageBaseAddressUrl = resourceUrls[packageBaseAddressName] as string;

            bool isNuGetRepo = packageBaseAddressUrl.Contains("v3-flatcontainer");

            JsonElement[] pkgVersionsArr = GetPackageVersions(packageBaseAddressUrl, packageName, isNuGetRepo, out edi);

            List<string> responses = new List<string>();
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
                        return InstallVersion(packageName, version.ToString(), out edi);
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
        public override HttpContent InstallVersion(string packageName, string version, out ExceptionDispatchInfo edi)
        {
            Hashtable resourceUrls = FindResourceType(new string[] { packageBaseAddressName }, out edi);
            string packageBaseAddressUrl = resourceUrls[packageBaseAddressName] as string;

            string pkgName = packageName.ToLower();
            string installPkgUrl = $"{packageBaseAddressUrl}{pkgName}/{version}/{pkgName}.{version}.nupkg";

            return HttpRequestCallForContent(installPkgUrl, out edi);
        }

        #endregion

        #region Private Methods

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

            return response;
        }

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

        private Hashtable FindResourceType(string[] resourceTypeName, out ExceptionDispatchInfo edi)
        {
            Hashtable resourceHash = new Hashtable();
            JsonElement[] resources = GetJsonElementArr($"{repository.Uri}", resourcesName, out edi);

            foreach (JsonElement resource in resources)
            {
                try
                {
                    if (resource.TryGetProperty("@type", out JsonElement typeElement) && resourceTypeName.Contains(typeElement.ToString()))
                    {
                        if (resource.TryGetProperty("@id", out JsonElement idElement))
                        {
                            // add name of the resource and its url
                            resourceHash.Add(typeElement.ToString(), idElement.ToString());
                        }
                        else
                        {
                            // error out here
                            string errMsg = $"@id element not found in service index '{repository.Uri}' for {resourceTypeName}.";
                        }
                    }
                }
                catch (InvalidOperationException e)
                {
                    edi = ExceptionDispatchInfo.Capture(e);
                    break;
                }
                catch (ArgumentNullException e)
                {
                    edi = ExceptionDispatchInfo.Capture(e);
                    break;
                }
                catch (ObjectDisposedException e)
                {
                    edi = ExceptionDispatchInfo.Capture(e);
                    break;
                }

                if (resourceHash.Count == resourceTypeName.Length)
                {
                    break;
                }
            }

            return resourceHash;
        }

        private string FindVersionHelper(string registrationsBaseUrl, string packageName, string version, out ExceptionDispatchInfo edi)
        {
            // TODO: Check if registrationsBaseUrl is null or empty  
            // TODO: should check if url is empty before creating query 

            // https://api.nuget.org/v3/registration5-gz-semver2/newtonsoft.json/13.0.2.json
            var requestPkgMapping = $"{registrationsBaseUrl}{packageName.ToLower()}/{version}.json";
            string pkgMappingResponse = HttpRequestCall(requestPkgMapping, out edi);

            try
            {
                JsonDocument pkgMappingDom = JsonDocument.Parse(pkgMappingResponse);
                JsonElement rootPkgMappingDom = pkgMappingDom.RootElement;
            }
            catch (JsonException e)
            { 
                edi = ExceptionDispatchInfo.Capture(e);
            }
            catch (ArgumentException e)
            { 
                edi = ExceptionDispatchInfo.Capture(e);
            }

            try
            {
                rootPkgMappingDom.TryGetProperty("catalogEntry", out JsonElement catalogEntryUrlElement);
            }
            catch (InvalidOperationException e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
            }
            catch (ArgumentNullException e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
            }
            catch (ObjectDisposedException e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
            }

            string catalogEntryUrl = catalogEntryUrlElement.ToString();

            if (string.IsNullOrEmpty(catalogEntryUrl))
            {
                string errMsg = $"{packageName} catalog entry could not be found";
                return null;
            }

            return HttpRequestCall(catalogEntryUrl, out edi);
        }

        private JsonElement[] GetPackageVersions(string packageBaseAddressUrl, string packageName, bool isNuGetRepo, out ExceptionDispatchInfo edi)
        {
            // TODO: should check if url is empty before creating query 
            edi = null;
            JsonElement[] pkgVersionsElement = GetJsonElementArr($"{packageBaseAddressUrl}{packageName}/index.json", versionsName, out edi);

            return isNuGetRepo ? pkgVersionsElement.Reverse().ToArray() : pkgVersionsElement.ToArray();
        }

        private JsonElement[] GetJsonElementArr(string request, string propertyName, out ExceptionDispatchInfo edi)
        {
            try { 
                JsonDocument pkgsDom = JsonDocument.Parse(HttpRequestCall(request, out edi));
            }
            catch (JsonException e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
            }
            catch (ArgumentException e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
            }
            
            JsonElement[] pkgsArr = new JsonElement[0]; 

            try
            {
                pkgsDom.RootElement.TryGetProperty(propertyName, out JsonElement pkgs);

                pkgsArr = pkgs.EnumerateArray().ToArray();
            }
            catch (ObjectDisposedException e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
            }
            catch (InvalidOperationException e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
            }
            catch (ArgumentNullException e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
            }

            return pkgsArr;
        }

        public static async Task<string> SendV3RequestAsync(HttpRequestMessage message, HttpClient s_client)
        {
            string errMsg = "Error occured while trying to retrieve response: ";

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


        public static async Task<HttpContent> SendV3RequestForContentAsync(HttpRequestMessage message, HttpClient s_client)
        {
            string errMsg = "Error occured while trying to retrieve response for content: ";

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