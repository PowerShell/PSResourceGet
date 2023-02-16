// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.Net.Http;
using NuGet.Versioning;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    internal class V2ServerAPICalls : ServerApiCall
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

        public readonly HttpClient s_client = new HttpClient();
        public readonly HttpFindPSResource _httpFindPSResource = new HttpFindPSResource();

        public readonly string select = "$select=Id,Version,NormalizedVersion,Authors,Copyright,Dependencies,Description,IconUrl,IsPrerelease,Published,ProjectUrl,ReleaseNotes,Tags,LicenseUrl,CompanyName";

        #endregion

        #region Constructor

        // internal V2ServerAPICalls() {}

        #endregion
        
        #region Methods
        // High level design: Find-PSResource >>> IFindPSResource (loops, version checks, etc.) >>> IServerAPICalls (call to repository endpoint/url)    

        /// <summary>
        /// Find method which allows for searching for all packages from a repository and returns latest version for each.
        /// Examples: Search -Repository PSGallery
        /// API call: 
        /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion
        /// </summary>
        public override string[] FindAll(PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out string errRecord) {
            errRecord = string.Empty;
            List<string> responses = new List<string>();

            if (type == ResourceType.Script || type == ResourceType.None)
            {
                int scriptSkip = 0;
                string initialScriptResponse = FindAllFromTypeEndPoint(repository, includePrerelease, isSearchingModule: false, scriptSkip, out errRecord);
                responses.Add(initialScriptResponse);
                int initalScriptCount = _httpFindPSResource.GetCountFromResponse(initialScriptResponse);
                int count = initalScriptCount / 6000;
                    // if more than 100 count, loop and add response to list
                while (count > 0)
                {
                    scriptSkip += 6000;
                    var tmpResponse = FindAllFromTypeEndPoint(repository, includePrerelease, isSearchingModule: false, scriptSkip, out errRecord);
                    responses.Add(tmpResponse);
                    count--;
                }
            }
            if (type != ResourceType.Script)
            {
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
            }

            return responses.ToArray();
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag from a repository and returns latest version for each.
        /// Examples: Search -Tag "JSON" -Repository PSGallery
        /// API call: 
        /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&searchTerm=tag:JSON&includePrerelease=true
        /// </summary>
        public override string[] FindTag(string tag, PSRepositoryInfo repository, bool includePrerelease, ResourceType _type, out string errRecord)
        {
            errRecord = string.Empty;
            List<string> responses = new List<string>();

            if (_type == ResourceType.Script || _type == ResourceType.None)
            {
                int scriptSkip = 0;
                string initialScriptResponse = FindTagFromEndpoint(tag, repository, includePrerelease, isSearchingModule: false, scriptSkip, out errRecord);
                responses.Add(initialScriptResponse);
                int initalScriptCount = _httpFindPSResource.GetCountFromResponse(initialScriptResponse);
                int count = initalScriptCount / 100;
                    // if more than 100 count, loop and add response to list
                while (count > 0)
                {
                    // skip 100
                    scriptSkip += 100;
                    var tmpResponse = FindTagFromEndpoint(tag, repository, includePrerelease, isSearchingModule: false,  scriptSkip, out errRecord);
                    responses.Add(tmpResponse);
                    count--;
                }
            }
            if (_type != ResourceType.Script)
            {
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
            }

            return responses.ToArray();
        }

        public override string[] FindCommandOrDscResource(string tag, PSRepositoryInfo repository, bool includePrerelease, bool isSearchingForCommands, out string errRecord)
        {
            List<string> responses = new List<string>();
            int skip = 0;

            string initialResponse = FindCommandOrDscResource(tag, repository, includePrerelease, isSearchingForCommands, skip, out errRecord);
            responses.Add(initialResponse);
            int initialCount = _httpFindPSResource.GetCountFromResponse(initialResponse);
            int count = initialCount / 100;

            while (count > 0)
            {
                skip += 100;
                var tmpResponse = FindCommandOrDscResource(tag, repository, includePrerelease, isSearchingForCommands, skip, out errRecord);
                responses.Add(tmpResponse);
                count--;
            }

            return responses.ToArray();
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
        public override string FindName(string packageName, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out string errRecord)
        {
            // Make sure to include quotations around the package name
            var prerelease = includePrerelease ? "IsAbsoluteLatestVersion" : "IsLatestVersion";

            // This should return the latest stable version or the latest prerelease version (respectively)
            //var requestUrlV2 = $"{repository.Uri.ToString()}/FindPackagesById()?id='{packageName}'?&$orderby=Version desc&$filter={prerelease}&{select}";



            // https://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'&$filter=IsLatestVersion and substringof('PSModule', Tags) eq true
            string typeFilterPart = type == ResourceType.None ? $" and Id eq '{packageName}'" :  $" and substringof('PS{type.ToString()}', Tags) eq true";
            var requestUrlV2 = $"{repository.Uri.ToString()}/FindPackagesById()?id='{packageName}'&$filter={prerelease}{typeFilterPart}&{select}";

            return HttpRequestCall(requestUrlV2, out errRecord);  
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "PowerShell*"
        /// API call: 
        /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='az*'
        /// Implementation Note: filter additionally and verify ONLY package name was a match.
        /// </summary>
        public override string[] FindNameGlobbing(string packageName, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out string errRecord)
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

        /// <summary>
        /// Find method which allows for searching for single name with version range.
        /// Name: no wildcard support
        /// Version: supports wildcards
        /// Examples: Search "PowerShellGet" "[3.0.0.0, 5.0.0.0]"
        ///           Search "PowerShellGet" "3.*"
        /// API Call: http://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'
        /// Implementation note: Returns all versions, including prerelease ones. Later (in the API client side) we'll do filtering on the versions to satisfy what user provided.
        /// </summary>
        public override string[] FindVersionGlobbing(string packageName, VersionRange versionRange, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, bool getOnlyLatest, out string errRecord)
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

        /// <summary>
        /// Find method which allows for searching for single name with specific version.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5"
        /// API call: http://www.powershellgallery.com/api/v2/Packages(Id='PowerShellGet', Version='2.2.5')
        /// </summary>
        public override string FindVersion(string packageName, string version, PSRepositoryInfo repository, ResourceType type, out string errRecord) {
            // https://www.powershellgallery.com/api/v2//FindPackagesById()?id='blah'&includePrerelease=false&$filter= NormalizedVersion eq '1.1.0' and substringof('PSModule', Tags) eq true 
            // Quotations around package name and version do not matter, same metadata gets returned.
            string typeFilterPart = type == ResourceType.None ? String.Empty :  $" and substringof('PS{type.ToString()}', Tags) eq true";
            var requestUrlV2 = $"{repository.Uri.ToString()}/FindPackagesById()?id='{packageName}'&$filter= NormalizedVersion eq '{version}'{typeFilterPart}&{select}";
            
            return HttpRequestCall(requestUrlV2, out errRecord);  
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
        public override HttpContent InstallName(string packageName, bool includePrerelease, PSRepositoryInfo repository, out string errRecord) {
            var requestUrlV2 = $"{repository.Uri.ToString()}/package/{packageName}";

            return HttpRequestCallForContent(requestUrlV2, out errRecord);  
        }


        /// <summary>
        /// Installs package with specific name and version.
        /// Name: no wildcard support.
        /// Version: no wildcard support.
        /// Examples: Install "PowerShellGet" -Version "3.0.0.0"
        ///           Install "PowerShellGet" -Version "3.0.0-beta16"
        /// API Call: https://www.powershellgallery.com/api/v2/package/Id/version (version can be prerelease)
        /// </summary>    
        public override HttpContent InstallVersion(string packageName, string version, PSRepositoryInfo repository, out string errRecord) {
            var requestUrlV2 = $"{repository.Uri.ToString()}/package/{packageName}/{version}";

            return HttpRequestCallForContent(requestUrlV2, out errRecord); 
        }


        private static string HttpRequestCall(string requestUrlV2, out string errRecord) {
            errRecord = string.Empty;

            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV2);
                
                return Utils.SendV2RequestAsync(request, s_client).GetAwaiter().GetResult();
            }
            catch (HttpRequestException e)
            {
                errRecord = "Error occured while trying to retrieve response: " + e.Message;
                throw new HttpRequestException(errRecord);
            }
        }

        private static HttpContent HttpRequestCallForContent(string requestUrlV2, out string errRecord) {
            errRecord = string.Empty;

            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV2);
                
                var response = Utils.SendV2RequestForContentAsync(request, s_client).GetAwaiter().GetResult();

                // Do we want to check if response is 200?
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

        /// <summary>
        /// Helper method for string[] FindAll(string, PSRepositoryInfo, bool, bool, ResourceType, out string)
        /// </summary>
        private string FindAllFromTypeEndPoint(PSRepositoryInfo repository, bool includePrerelease, bool isSearchingModule, int skip, out string errRecord) {
            string typeEndpoint = isSearchingModule ? String.Empty : "/items/psscript";
            string paginationParam = $"&$orderby=Id desc&$inlinecount=allpages&$skip={skip}&$top=6000";
            var prereleaseFilter = includePrerelease ? "IsAbsoluteLatestVersion&includePrerelease=true" : "IsLatestVersion";

            var requestUrlV2 = $"{repository.Uri}{typeEndpoint}/Search()?$filter={prereleaseFilter}{paginationParam}";

            return HttpRequestCall(requestUrlV2, out errRecord);
        }

        /// <summary>
        /// Helper method for string[] FindTag(string, PSRepositoryInfo, bool, bool, ResourceType, out string)
        /// </summary>
        private string FindTagFromEndpoint(string tag, PSRepositoryInfo repository, bool includePrerelease, bool isSearchingModule, int skip, out string errRecord)
        {
            // scenarios with type + tags:
            // type: None -> search both endpoints
            // type: M -> just search Module endpoint
            // type: S -> just search Scripts end point
            // type: DSCResource -> just search Modules
            // type: Command -> just search Modules
            errRecord = String.Empty;
            string typeEndpoint = isSearchingModule ? String.Empty : "/items/psscript";
            string paginationParam = $"&$orderby=Id desc&$inlinecount=allpages&$skip={skip}&$top=6000";
            var prereleaseFilter = includePrerelease ? "$filter=IsAbsoluteLatestVersion&includePrerelease=true" : "$filter=IsLatestVersion";
            
            var scriptsRequestUrlV2 = $"{repository.Uri}{typeEndpoint}/Search()?{prereleaseFilter}&searchTerm='tag:{tag}'{paginationParam}&{select}";
            // TODO: add error handling here

            return HttpRequestCall(requestUrlV2: scriptsRequestUrlV2, out string scriptErrorRecord);  
        }

        /// <summary>
        /// Helper method for string[] FindCommandOrDSCResource(string, PSRepositoryInfo, bool, bool, ResourceType, out string)
        /// </summary>
        private string FindCommandOrDscResource(string tag, PSRepositoryInfo repository, bool includePrerelease, bool isSearchingForCommands, int skip, out string errRecord)
        {
            // can only find from Modules endpoint
            string paginationParam = $"&$orderby=Id desc&$inlinecount=allpages&$skip={skip}&$top=6000";
            var prereleaseFilter = includePrerelease ? "$filter=IsAbsoluteLatestVersion&includePrerelease=true" : "$filter=IsLatestVersion";
            var tagFilter = isSearchingForCommands ? "PSCommand_" : "PSDscResource_";
            var requestUrlV2 = $"{repository.Uri}/Search()?{prereleaseFilter}&searchTerm='tag:{tagFilter}{tag}'{prereleaseFilter}{paginationParam}&{select}";

            return HttpRequestCall(requestUrlV2, out errRecord);
        }

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
            
            var requestUrlV2 = $"{repository.Uri.ToString()}/Search()?$filter={nameFilter} and {prerelease}&{select}{extraParam}";
            
            return HttpRequestCall(requestUrlV2, out errRecord);  
        }

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
            var requestUrlV2 = $"{repository.Uri.ToString()}/FindPackagesById()?id='{packageName}'&$orderby=NormalizedVersion desc&{paginationParam}&{select}{filterQuery}";

            return HttpRequestCall(requestUrlV2, out errRecord);  
        }

        #endregion
    }
}