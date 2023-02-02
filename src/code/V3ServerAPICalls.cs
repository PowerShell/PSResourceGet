using System.Security.AccessControl;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Versioning;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Net;

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
            // Make sure to include quotations around the package name
            var prerelease = includePrerelease;

			// This should return all versions 
			//var requestUrlV2 = $"{repository.Uri.ToString()}/FindPackagesById()?id='{packageName}'&$filter={prerelease}{typeFilterPart}&{select}";
			// registered URL: https://api.nuget.org/v3/index.json
			// URL we need to create: "https://api.nuget.org/v3/registrations2/nuget.server.core/index.json";
			
			string repoUri = repository.Uri.ToString();
			int idx = repoUri.LastIndexOf('/'); 

			if (idx != -1)
			{
				// throw error
			}

			// First part of Uri, eg:  "https://api.nuget.org/v3/"
			string firstPartUri = repoUri.Substring(0, idx);
			// Last part of the Uri, eg: "index.json"
			string lastPartUri = repoUri.Substring(idx+1);

            var requestUrlV3 = $"{firstPartUri}/registrations2/{packageName}/{lastPartUri}";           

            return HttpRequestCall(requestUrlV3, out errRecord);  
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
        ///        Note:  response only returns names
        ///        
        ///        Make another query to get the latest version of each package  (ie call "FindVersionGlobbing")
		/// </summary>
		public string[] FindNameGlobbing(string packageName, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out string errRecord)
        {
            List<string> responses = new List<string>();
            int skip = 0;

            var initialResponse = FindNameGlobbing(packageName, repository, includePrerelease, skip, out errRecord);
            responses.Add(initialResponse);

            // check count (regex)  425 ==> count/100  ~~>  4 calls 
            int initalCount = _httpFindPSResource.GetCountFromResponse(initialResponse);  // count = 4
            int count = initalCount / 100;
            // if more than 100 count, loop and add response to list
            while (count > 0)
            {
                // skip 100
                skip += 100;
                var tmpResponse = FindNameGlobbing(packageName, repository, includePrerelease, skip, out errRecord);
                responses.Add(tmpResponse);
                count--;
            }

            return responses.ToArray();
        }

		// TODO:  Complete this later -  URL complete
		/// <summary>
		/// Find method which allows for searching for single name with version range.
		/// Name: no wildcard support
		/// Version: supports wildcards
		/// Examples: Search "NuGet.Server.Core" "[1.0.0.0, 5.0.0.0]"
		///           Search "NuGet.Server.Core" "3.*"
		/// API Call: 
		///           first, find RegistrationBaseUrl (see FindName for details)
        /// 
        ///           then, find all versions for a pkg
        ///           for nuget:
        ///               https://api.nuget.org/v3/registration5-gz-semver2/nuget.server/index.json
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
            List<string> responses = new List<string>();
            int skip = 0;

            var initialResponse = FindVersionGlobbing(packageName, versionRange, repository, includePrerelease, type, skip, getOnlyLatest, out errRecord);
            responses.Add(initialResponse);

            if (!getOnlyLatest)
            {
                int initalCount = _httpFindPSResource.GetCountFromResponse(initialResponse);
                int count = initalCount / 100;

                while (count > 0)
                {
                    // skip 100
                    skip += 100;
                    var tmpResponse = FindVersionGlobbing(packageName, versionRange, repository, includePrerelease, type, skip, getOnlyLatest, out errRecord);
                    responses.Add(tmpResponse);
                    count--;
                }
            }

            return responses.ToArray();
        }

		// TODO:  Complete this now  -  URL complete
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
            
            // Quotations around package name and version do not matter, same metadata gets returned.
            //var requestUrlV2 = $"{repository.Uri.ToStrin g()}/Packages(Id='{packageName}', Version='{version}')?{select}";
           // string typeFilterPart = type == ResourceType.None ? String.Empty :  $" and substringof('PS{type.ToString()}', Tags) eq true";
            
            // 1) find the RegistrationBaseUrl
            // ie send a request out for the index resources (response is json)
            var requestV3index = $"{repository.Uri}";
            var indexResponse = HttpRequestCall(requestV3index, out errRecord);

			JObject indexResources = JObject.Parse(indexResponse);

			var resources = indexResources.SelectToken("resources");

            string registrationBaseUrl = string.Empty;
			foreach (JObject item in resources) 
			{
				string resourceType = item.GetValue("@type").ToString();

                if (resourceType.Equals("RegistrationsBaseUrl/Versioned")) {
                    // use this resourceId
                    registrationBaseUrl = item.GetValue("@id").ToString();
                    break;
				}
			}

            if (String.IsNullOrEmpty(registrationBaseUrl)) { 
                // throw error
            }

			// https://api.nuget.org/v3/registration5-gz-semver2/newtonsoft.json/13.0.2.json
			var requestPkgMapping = $"{registrationBaseUrl}{packageName}/{version}.json";  
			var responsePkgMapping = HttpRequestCall(requestPkgMapping, out errRecord);

			JObject pkgMapping = JObject.Parse(responsePkgMapping);
			JToken catalogEntryKey = pkgMapping.SelectToken("catalogEntry");

            var catalogEntryValue = catalogEntryKey.Value<string>();

            if (string.IsNullOrEmpty(catalogEntryValue)) {
                // throw error
                return "bye";
            }

			var requestPkgCatalogEntry = $"{catalogEntryValue}";
			var responsePkgCatalogEntry = HttpRequestCall(requestPkgCatalogEntry, out errRecord);

            return responsePkgCatalogEntry;
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
        private static string HttpRequestCall(string requestUrlV3, out string errRecord) {
            errRecord = string.Empty;

            // request object will send requestUrl 
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV3);

				// We can have this return a Task, or the response (json string)
				var response = Utils.SendV3RequestAsync(request, s_client).GetAwaiter().GetResult();

                // Do we want to check if response is 200?
                // response will be json metadata object that will get returned
                return response.ToString();
            }
            catch (HttpRequestException e)
            {
                errRecord = "Error occured while trying to retrieve response: " + e.Message;
            }

            return string.Empty;
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

		// TODO:  complete this for v3 -- below is all v2
		/// <summary>
		/// Helper method for string[] FindNameGlobbing(string, PSRepositoryInfo, bool, ResourceType, out string)
		/// </summary>
		private string FindNameGlobbing(string packageName, PSRepositoryInfo repository, bool includePrerelease, int skip, out string errRecord)
        {
            // https://www.powershellgallery.com/api/v2/Search()?$filter=endswith(Id, 'Get') and startswith(Id, 'PowerShell') and IsLatestVersion (stable)
            // https://www.powershellgallery.com/api/v2/Search()?$filter=endswith(Id, 'Get') and IsAbsoluteLatestVersion&includePrerelease=true
            
            string extraParam = $"&$orderby=Id desc&$inlinecount=allpages&$skip={skip}&$top=100";
            var prerelease = includePrerelease ? "IsAbsoluteLatestVersion&includePrerelease=true" : "IsLatestVersion";
            var nameFilter = string.Empty;

            var names = packageName.Split(new char[] {'*'}, StringSplitOptions.RemoveEmptyEntries);

            if (names.Length == 0)
            {
                errRecord = "We don't support -Name *";
                return string.Empty;
            }
            if (names.Length == 1)
            {
                if (packageName.StartsWith("*") && packageName.EndsWith("*"))
                {
                    // *get*
                    nameFilter = $"substringof('{names[0]}', Id)";
                }
                else if (packageName.EndsWith("*"))
                {
                    // PowerShell*
                    nameFilter = $"startswith(Id, '{names[0]}')";
                }
                else
                {
                    // *ShellGet
                    nameFilter = $"endswith(Id, '{names[0]}')";
                }
            }
            else if (names.Length == 2 && !packageName.StartsWith("*") && !packageName.EndsWith("*"))
            {
                // *pow*get*
                // pow*get -> only support this
                // pow*get*
                // *pow*get
                nameFilter = $"startswith(Id, '{names[0]}') and endswith(Id, '{names[1]}')";
            }
            else 
            {
                errRecord = "We only support wildcards for scenarios similar to the following examples: PowerShell*, *ShellGet, Power*Get, *Shell*.";
                return string.Empty;
            }
            
            var requestUrlV2 = $"{repository.Uri.ToString()}";
            
            return HttpRequestCall(requestUrlV2, out errRecord);  
        }

        // TODO:  complete this for v3 -- below is all v2
        /// <summary>
        /// Helper method for string[] FindVersionGlobbing(string, VersionRange, PSRepositoryInfo, bool, ResourceType, out string)
        /// </summary>
        private string FindVersionGlobbing(string packageName, VersionRange versionRange, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, int skip, bool getOnlyLatest, out string errRecord)
        {
            //https://www.powershellgallery.com/api/v2//FindPackagesById()?id='blah'&includePrerelease=false&$filter= NormalizedVersion gt '1.0.0' and NormalizedVersion lt '2.2.5' and substringof('PSModule', Tags) eq true 
            //https://www.powershellgallery.com/api/v2//FindPackagesById()?id='PowerShellGet'&includePrerelease=false&$filter= NormalizedVersion gt '1.1.1' and NormalizedVersion lt '2.2.5'
            // NormalizedVersion doesn't include trailing zeroes
            // Notes: this could allow us to take a version range (i.e (2.0.0, 3.0.0.0]) and deconstruct it and add options to the Filter for Version to describe that range
            // will need to filter additionally, if IncludePrerelease=false, by default we get stable + prerelease both back
            // Current bug: Find PSGet -Version "2.0.*" -> https://www.powershellgallery.com/api/v2//FindPackagesById()?id='PowerShellGet'&includePrerelease=false&$filter= Version gt '2.0.*' and Version lt '2.1'
            // Make sure to include quotations around the package name
            
            //and IsPrerelease eq false
            // ex:
            // (2.0.0, 3.0.0)
            // $filter= NVersion gt '2.0.0' and NVersion lt '3.0.0'

            // [2.0.0, 3.0.0]
            // $filter= NVersion ge '2.0.0' and NVersion le '3.0.0'

            // [2.0.0, 3.0.0)
            // $filter= NVersion ge '2.0.0' and NVersion lt '3.0.0'

            // (2.0.0, 3.0.0]
            // $filter= NVersion gt '2.0.0' and NVersion le '3.0.0'

            // [, 2.0.0]
            // $filter= NVersion le '2.0.0'

            string format = "NormalizedVersion {0} {1}";
            string minPart = String.Empty;
            string maxPart = String.Empty;

            if (versionRange.MinVersion != null)
            {
                string operation = versionRange.IsMinInclusive ? "ge" : "gt";
                minPart = String.Format(format, operation, $"'{versionRange.MinVersion.ToNormalizedString()}'");
            }

            if (versionRange.MaxVersion != null)
            {
                string operation = versionRange.IsMaxInclusive ? "le" : "lt";
                maxPart = String.Format(format, operation, $"'{versionRange.MaxVersion.ToNormalizedString()}'");
            }

            string versionFilterParts = String.Empty;
            if (!String.IsNullOrEmpty(minPart) && !String.IsNullOrEmpty(maxPart))
            {
                versionFilterParts += minPart + " and " + maxPart;
            }
            else if (!String.IsNullOrEmpty(minPart))
            {
                versionFilterParts += minPart;
            }
            else if (!String.IsNullOrEmpty(maxPart))
            {
                versionFilterParts += maxPart;
            }

            string filterQuery = "&$filter=";
            filterQuery += includePrerelease ? string.Empty : "IsPrerelease eq false";
            //filterQuery +=  type == ResourceType.None ? String.Empty : $" and substringof('PS{type.ToString()}', Tags) eq true";

            string joiningOperator = filterQuery.EndsWith("=") ? String.Empty : " and " ;
            filterQuery += type == ResourceType.None ? String.Empty : $"{joiningOperator}substringof('PS{type.ToString()}', Tags) eq true";

            if (!String.IsNullOrEmpty(versionFilterParts))
            {
                // Check if includePrerelease is true, if it is we want to add "$filter"
                // Single case where version is "*" (or "[,]") and includePrerelease is true, then we do not want to add "$filter" to the requestUrl.
        
                // Note: could be null/empty if Version was "*" -> [,]
                joiningOperator = filterQuery.EndsWith("=") ? String.Empty : " and " ;
                filterQuery +=  $"{joiningOperator}{versionFilterParts}";
            }

            string topParam = getOnlyLatest ? "$top=1" : "$top=100"; // only need 1 package if interested in latest
            string paginationParam = $"$inlinecount=allpages&$skip={skip}&{topParam}";

            filterQuery = filterQuery.EndsWith("=") ? string.Empty : filterQuery;
            var requestUrlV2 = $"{repository.Uri.ToString()}";

            return HttpRequestCall(requestUrlV2, out errRecord);  
        }

        #endregion
    }
}