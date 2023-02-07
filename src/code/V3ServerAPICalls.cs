using System.Security.AccessControl;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Versioning;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Collections;
using System.Xml.Linq;
using System.Drawing;
using System.Management.Automation;
using NuGet.Protocol.Core.Types;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    internal class V3ServerAPICalls : IServerAPICalls
    {
        // Any interface method that is not implemented here should be processed in the parent method and then call one of the implemented 
        // methods below.
        #region Members

        private static readonly HttpFindPSResource _httpFindPSResource = new HttpFindPSResource();
		private static readonly HttpClientHandler handler = new HttpClientHandler()
		{
			AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
		};
		private static readonly HttpClient s_client = new HttpClient(handler);

		#endregion

		#region Constructor

		internal V3ServerAPICalls() {}

        #endregion
        
        #region Methods
        // High level design: Find-PSResource >>> IFindPSResource (loops, version checks, etc.) >>> IServerAPICalls (call to repository endpoint/url)    

        // TODO:  Find all -- Consider not implementing
        /// <summary>
        /// Find method which allows for searching for all packages from a repository and returns latest version for each.
        /// Examples: Search -Repository PSGallery
        /// API call: 
        /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion
        /// </summary>
        public string[] FindAll(PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out string errRecord) {
            errRecord = string.Empty;
            List<string> responses = new List<string>();

            
            /*
                int moduleSkip = 0;
                string initialModuleResponse = FindAllFromTypeEndPoint(repository, includePrerelease, isSearchingModule: true, moduleSkip, out errRecord);
                responses.Add(initialModuleResponse);
                int initalModuleCount = _httpFindPSResource.GetCountFromResponse(initialModuleResponse);
                int count = initalModuleCount / 6000;
                
                // if more than 100 count, loop and add response to list
                while (count > 0)
                {
                    moduleSkip += 6000;
                    var tmpResponse = FindAllFromTypeEndPoint(repository, includePrerelease, isSearchingModule: true, moduleSkip, out errRecord);
                    responses.Add(tmpResponse);
                    count--;
                }
            */

            return responses.ToArray();
        }

        // TODO:  Find tag -- consider not implementing
        /// <summary>
        /// Find method which allows for searching for packages with tag from a repository and returns latest version for each.
        /// Examples: Search -Tag "JSON" -Repository PSGallery
        /// API call: 
        /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&searchTerm=tag:JSON&includePrerelease=true
        /// </summary>
        public string[] FindTag(string tag, PSRepositoryInfo repository, bool includePrerelease, ResourceType _type, out string errRecord)
        {
            errRecord = string.Empty;
            List<string> responses = new List<string>();

            /*
                int moduleSkip = 0;
                string initialModuleResponse = FindTagFromEndpoint(tag, repository, includePrerelease, isSearchingModule: true, moduleSkip, out errRecord);
                responses.Add(initialModuleResponse);
                int initalModuleCount = _httpFindPSResource.GetCountFromResponse(initialModuleResponse);
                int count = initalModuleCount / 100;
                    // if more than 100 count, loop and add response to list
                while (count > 0)
                {
                    moduleSkip += 100;
                    var tmpResponse = FindTagFromEndpoint(tag, repository, includePrerelease, isSearchingModule: true, moduleSkip, out errRecord);
                    responses.Add(tmpResponse);
                    count--;
                }
            */
            return responses.ToArray();
        }

        // DONE FOR NOW
        public void FindCommandOrDscResource(string tag, PSRepositoryInfo repository, bool includePrerelease, bool isSearchingForCommands, out string errRecord)
        {
            // not applicable for v3 repositories
            errRecord = string.Empty;
        }

		// TODO:  Complete this now - URL complete
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
		public string FindName(string packageName, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out string errRecord)
        {
            // 1) Find the package base address ("PackageBaseAddress/3.0.0")
            string typeName = "PackageBaseAddress/3.0.0";
            // https://msazure.pkgs.visualstudio.com/b32aa71e-8ed2-41b2-9d77-5bc261222004/_packaging/0d5429e2-c871-4347-bdc9-d1cbbac5eb3b/nuget/v3/flat2/
            string packageBaseAddressUrl = FindResourceType(typeName, repository, out errRecord);
            string flattenedPkgVersionsUrl = $"{packageBaseAddressUrl}{packageName}/index.json";

            // TODO: note that .Contains() is case sensitive. This shouldn't matter since the url we recieve is a response and (we hope) is always lowercase. 
            bool isNuGetRepo = packageBaseAddressUrl.Contains("v3-flatcontainer");

            // This response will be an array called Versions that contains all the versions
            string allPkgVersionsResponse = HttpRequestCall(flattenedPkgVersionsUrl, out errRecord);

            JsonDocument pkgVersionsDom = JsonDocument.Parse(allPkgVersionsResponse);
            JsonElement rootPkgVersionsDom = pkgVersionsDom.RootElement;
            rootPkgVersionsDom.TryGetProperty("versions", out JsonElement pkgVersionsElement);

            string registrationsBaseUrlType = "RegistrationsBaseUrl/Versioned";
            string registrationsBaseUrl = FindResourceType(registrationsBaseUrlType, repository, out errRecord);

            // sort array
            JsonElement[] pkgVersionsArr = isNuGetRepo ? pkgVersionsElement.EnumerateArray().Reverse().ToArray() : pkgVersionsElement.EnumerateArray().ToArray();
            
            List<string> responses = new List<string>();
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
                        return FindVersionHelper(registrationsBaseUrl, packageName, version.ToString(), out errRecord);
                    }
                }
            }

            return null;
        }

        // TODO:  Complete this later - URL complete
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
        public string[] FindNameGlobbing(string packageName, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out string errRecord)
        {
            List<string> responses = new List<string>();
            errRecord = string.Empty;

            /*
            // 1) Find the package base address ("SearchQueryService/3.0.0-beta")
            string typeName = "SearchQueryService/3.0.0-beta";
            // https://msazure.pkgs.visualstudio.com/.../_packaging/.../nuget/v3/query2
            string searchQueryServiceUrl = FindResourceType(typeName, repository, out errRecord);
            string query = $""

            string flattenedPkgVersionsUrl = $"{searchQueryServiceUrl}{packageName}/index.json";
            */
            return responses.ToArray();
        }

        // Complete
        /// <summary>
        /// Find method which allows for searching for single name with version range.
        /// Name: no wildcard support
        /// Version: supports wildcards
        /// Examples: Search "NuGet.Server.Core" "[1.0.0.0, 5.0.0.0]"
        ///           Search "NuGet.Server.Core" "3.*"
        /// API Call: 
        ///           First, find "PackageBaseAddress/3.0.0" (see FindName for details)
        /// 
        ///     
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
        public string[] FindVersionGlobbing(string packageName, VersionRange versionRange, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, bool getOnlyLatest, out string errRecord)
        {
            // 1) Find the package base address ("PackageBaseAddress/3.0.0")
            string typeName = "PackageBaseAddress/3.0.0";  
            // https://msazure.pkgs.visualstudio.com/b32aa71e-8ed2-41b2-9d77-5bc261222004/_packaging/0d5429e2-c871-4347-bdc9-d1cbbac5eb3b/nuget/v3/flat2/
            string packageBaseAddressUrl = FindResourceType(typeName, repository, out errRecord);
            string flattenedPkgVersionsUrl = $"{packageBaseAddressUrl}{packageName}/index.json";

            // TODO: note that .Contains() is case sensitive. This shouldn't matter since the url we recieve is a response and (we hope) is always lowercase. 
            bool isNuGetRepo = packageBaseAddressUrl.Contains("v3-flatcontainer");

            // This response will be an array called Versions that contains all the versions
            string allPkgVersionsResponse = HttpRequestCall(flattenedPkgVersionsUrl, out errRecord);

            JsonDocument pkgVersionsDom = JsonDocument.Parse(allPkgVersionsResponse);
            JsonElement rootPkgVersionsDom = pkgVersionsDom.RootElement;
            rootPkgVersionsDom.TryGetProperty("versions", out JsonElement pkgVersionsElement);

            string registrationsBaseUrlType = "RegistrationsBaseUrl/Versioned";
            string registrationsBaseUrl = FindResourceType(registrationsBaseUrlType, repository, out errRecord);

            List<string> responses = new List<string>();
            foreach (var version in pkgVersionsElement.EnumerateArray()) {
                // parse as NuGetVersion
                if (NuGetVersion.TryParse(version.ToString(), out NuGetVersion nugetVersion) && versionRange.Satisfies(nugetVersion))
                {
                    /* 
                     * pkgVersion == !prerelease   &&   includePrerelease == true   -->   keep pkg   
                     * pkgVersion == !prerelease   &&   includePrerelease == false  -->   keep pkg 
                     * pkgVersion == prerelease    &&   includePrerelease == true   -->   keep pkg   
                     * pkgVersion == prerelease    &&   includePrerelease == false  -->   throw away pkg 
                     */
                    if (!nugetVersion.IsPrerelease || includePrerelease) {
                        responses.Add(FindVersionHelper(registrationsBaseUrl, packageName, version.ToString(), out errRecord));
                    }
                }
            }

            return responses.ToArray();
        }

		// Complete
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
		public string FindVersion(string packageName, string version, PSRepositoryInfo repository, ResourceType type, out string errRecord) {
            string typeName = "RegistrationsBaseUrl/Versioned";
            string registrationsBaseUrl = FindResourceType(typeName, repository, out errRecord);

            return FindVersionHelper(registrationsBaseUrl, packageName, version, out errRecord);
        }


        /**  INSTALL APIS **/

            // TODO:  Install name
            /// <summary>
            /// Installs specific package.
            /// Name: no wildcard support.
            /// Examples: Install "PowerShellGet"
            /// Implementation Note:   if not prerelease: https://www.powershellgallery.com/api/v2/package/powershellget (Returns latest stable)
            ///                        if prerelease, the calling method should first call IFindPSResource.FindName(), 
            ///                             then find the exact version to install, then call into install version
            /// </summary>
        public HttpContent InstallName(string packageName, PSRepositoryInfo repository, out string errRecord) {
            var requestUrlV2 = $"{repository.Uri.ToString()}/package/{packageName}";

            return HttpRequestCallForContent(requestUrlV2, out errRecord);  
        }

        // TODO:  Install version
        /// <summary>
        /// Installs package with specific name and version.
        /// Name: no wildcard support.
        /// Version: no wildcard support.
        /// Examples: Install "PowerShellGet" -Version "3.0.0.0"
        ///           Install "PowerShellGet" -Version "3.0.0-beta16"
        /// API Call: https://www.powershellgallery.com/api/v2/package/Id/version (version can be prerelease)
        /// </summary>    
        public HttpContent InstallVersion(string packageName, string version, PSRepositoryInfo repository, out string errRecord) {
            var requestUrlV2 = $"{repository.Uri.ToString()}/package/{packageName}/{version}";

            return HttpRequestCallForContent(requestUrlV2, out errRecord); 
        }


        // DONE FOR NOW
        private static String HttpRequestCall(string requestUrlV3, out string errRecord) {
            errRecord = string.Empty;
            //JsonDocument responseDom = null;
            string response = string.Empty;

            // request object will send requestUrl 
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV3);

				        // We can have this return a Task, or the response (json string)
				        response = Utils.SendV3RequestAsync(request, s_client).GetAwaiter().GetResult();

                // response will be json metadata object that will get returned
                //return response;
            }
            catch (HttpRequestException e)
            {
                errRecord = "Error occured while trying to retrieve response: " + e.Message;
                /*
                if (responseDom != null) {
                    responseDom.Dispose();
                }*/
            }

            // Todo: test that this works as expected if a responseDom was disposed before exiting this method
            return response;
        }

        // DONE FOR NOW
        private static HttpContent HttpRequestCallForContent(string requestUrlV3, out string errRecord) {
            errRecord = string.Empty;

            // request object will send requestUrl 
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV3);
                
                // We can have this return a Task, or the response (json string)
                var response = Utils.SendV3RequestForContentAsync(request, s_client).GetAwaiter().GetResult();

                // Do we want to check if response is 200?
                // response will be json metadata object that will get returned
                return response;
            }
            catch (HttpRequestException e)
            {
                errRecord = "Error occured while trying to retrieve response: " + e.Message;
                throw new HttpRequestException(errRecord);
            }
        }


		#endregion

		#region Private Methods

        private string FindResourceType(string resourceTypeName, PSRepositoryInfo repository, out string errMsg) {
            string resourcesName = "resources";
            string resourceUrl = string.Empty;

            // Quotations around package name and version do not matter, same metadata gets returned.
            //var requestUrlV2 = $"{repository.Uri.ToStrin g()}/Packages(Id='{packageName}', Version='{version}')?{select}";
            // string typeFilterPart = type == ResourceType.None ? String.Empty :  $" and substringof('PS{type.ToString()}', Tags) eq true";

            // 1) find the RegistrationBaseUrl
            // ie send a request out for the index resources (response is json)
            var requestV3index = $"{repository.Uri}";

            // JSON Document
            string indexResponse = HttpRequestCall(requestV3index, out errMsg);
            JsonDocument indexDom = JsonDocument.Parse(indexResponse);

            JsonElement rootIndexDom = indexDom.RootElement;
            rootIndexDom.TryGetProperty(resourcesName, out JsonElement resources);

            foreach (JsonElement resource in resources.EnumerateArray())
            {
                if (resource.TryGetProperty("@type", out JsonElement typeElement) && resourceTypeName.Equals(typeElement.ToString()))
                {
                    if (resource.TryGetProperty("@id", out JsonElement idElement))
                    {
                        resourceUrl = idElement.ToString();
                    }
                    else
                    {
                        // error out here
                        errMsg = $"@id element not found in service index '{repository.Uri}' for {resourceTypeName}.";
                    }
                }
            }

            // todo: this is probably not necessary
            if (String.IsNullOrEmpty(resourceUrl))
            {
                // throw error
            }

            return resourceUrl;
        }

        private string FindVersionHelper(string registrationsBaseUrl, string packageName, string version, out string errMsg) {
            // https://api.nuget.org/v3/registration5-gz-semver2/newtonsoft.json/13.0.2.json
            var requestPkgMapping = $"{registrationsBaseUrl}{packageName}/{version}.json";
            string pkgMappingResponse = HttpRequestCall(requestPkgMapping, out errMsg);

            JsonDocument pkgMappingDom = JsonDocument.Parse(pkgMappingResponse);
            JsonElement rootPkgMappingDom = pkgMappingDom.RootElement;
            rootPkgMappingDom.TryGetProperty("catalogEntry", out JsonElement catalogEntryUrlElement);
            string catalogEntryUrl = catalogEntryUrlElement.ToString();

            if (string.IsNullOrEmpty(catalogEntryUrl))
            {
                // throw error
                return null;
            }

            // string response
            string responsePkgCatalogEntry = HttpRequestCall(catalogEntryUrl, out errMsg);

            return responsePkgCatalogEntry;
        }

        #endregion
    }
}