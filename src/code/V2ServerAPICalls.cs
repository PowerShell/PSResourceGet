// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using NuGet.Versioning;
using System.Threading.Tasks;
using System.Xml;
using System.Net;
using System.Runtime.ExceptionServices;

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

        public override PSRepositoryInfo Repository { get; set; }
        private HttpClient _sessionClient { get; set; }
        private static readonly Hashtable[] emptyHashResponses = new Hashtable[]{};
        public FindResponseType v2FindResponseType = FindResponseType.ResponseString;

        #endregion

        #region Constructor

        public V2ServerAPICalls (PSRepositoryInfo repository, NetworkCredential networkCredential) : base (repository, networkCredential)
        {
            this.Repository = repository;
            HttpClientHandler handler = new HttpClientHandler()
            {
                Credentials = networkCredential
            };

            _sessionClient = new HttpClient(handler);
        }

        #endregion

        #region Overriden Methods 

        /// <summary>
        /// Find method which allows for searching for all packages from a repository and returns latest version for each.
        /// Examples: Search -Repository PSGallery
        /// API call: 
        /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion
        /// </summary>
        public override FindResults FindAll(bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi) {
            edi = null;
            List<string> responses = new List<string>();

            if (type == ResourceType.Script || type == ResourceType.None)
            {
                int scriptSkip = 0;
                string initialScriptResponse = FindAllFromTypeEndPoint(includePrerelease, isSearchingModule: false, scriptSkip, out edi);
                if (edi != null)
                {
                    return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }
                responses.Add(initialScriptResponse);
                int initalScriptCount = GetCountFromResponse(initialScriptResponse, out edi);
                if (edi != null)
                {
                    return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }
                int count = initalScriptCount / 6000;
                // if more than 100 count, loop and add response to list
                while (count > 0)
                {
                    scriptSkip += 6000;
                    var tmpResponse = FindAllFromTypeEndPoint(includePrerelease, isSearchingModule: false, scriptSkip, out edi);
                    if (edi != null)
                    {
                        return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                    }
                    responses.Add(tmpResponse);
                    count--;
                }
            }
            if (type != ResourceType.Script)
            {
                int moduleSkip = 0;
                string initialModuleResponse = FindAllFromTypeEndPoint(includePrerelease, isSearchingModule: true, moduleSkip, out edi);
                if (edi != null)
                {
                    return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }
                responses.Add(initialModuleResponse);
                int initalModuleCount = GetCountFromResponse(initialModuleResponse, out edi);
                if (edi != null)
                {
                    return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }
                int count = initalModuleCount / 6000;
                
                // if more than 100 count, loop and add response to list
                while (count > 0)
                {
                    moduleSkip += 6000;
                    var tmpResponse = FindAllFromTypeEndPoint(includePrerelease, isSearchingModule: true, moduleSkip, out edi);
                    if (edi != null)
                    {
                        return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                    }
                    responses.Add(tmpResponse);
                    count--;
                }
            }

            return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag from a repository and returns latest version for each.
        /// Examples: Search -Tag "JSON" -Repository PSGallery
        /// API call: 
        /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&searchTerm=tag:JSON&includePrerelease=true
        /// </summary>
        public override FindResults FindTags(string[] tags, bool includePrerelease, ResourceType _type, out ExceptionDispatchInfo edi)
        {
            edi = null;
            List<string> responses = new List<string>();

            if (_type == ResourceType.Script || _type == ResourceType.None)
            {
                int scriptSkip = 0;
                string initialScriptResponse = FindTagFromEndpoint(tags, includePrerelease, isSearchingModule: false, scriptSkip, out edi);
                if (edi != null)
                {
                    return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }
                responses.Add(initialScriptResponse);
                int initalScriptCount = GetCountFromResponse(initialScriptResponse, out edi);
                if (edi != null)
                {
                    return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }
                int count = initalScriptCount / 100;
                // if more than 100 count, loop and add response to list
                while (count > 0)
                {
                    // skip 100
                    scriptSkip += 100;
                    var tmpResponse = FindTagFromEndpoint(tags, includePrerelease, isSearchingModule: false,  scriptSkip, out edi);
                    if (edi != null)
                    {
                        return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                    }
                    responses.Add(tmpResponse);
                    count--;
                }
            }
            if (_type != ResourceType.Script)
            {
                int moduleSkip = 0;
                string initialModuleResponse = FindTagFromEndpoint(tags, includePrerelease, isSearchingModule: true, moduleSkip, out edi);
                if (edi != null)
                {
                    return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }
                responses.Add(initialModuleResponse);
                int initalModuleCount = GetCountFromResponse(initialModuleResponse, out edi);
                if (edi != null)
                {
                    return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }
                int count = initalModuleCount / 100;
                    // if more than 100 count, loop and add response to list
                while (count > 0)
                {
                    moduleSkip += 100;
                    var tmpResponse = FindTagFromEndpoint(tags, includePrerelease, isSearchingModule: true, moduleSkip, out edi);
                    if (edi != null)
                    {
                        return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                    }
                    responses.Add(tmpResponse);
                    count--;
                }
            }

            return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for all packages that have specified Command or DSCResource name.
        /// </summary>
        public override FindResults FindCommandOrDscResource(string[] tags, bool includePrerelease, bool isSearchingForCommands, out ExceptionDispatchInfo edi)
        {
            List<string> responses = new List<string>();
            int skip = 0;

            string initialResponse = FindCommandOrDscResource(tags, includePrerelease, isSearchingForCommands, skip, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }
            responses.Add(initialResponse);
            int initialCount = GetCountFromResponse(initialResponse, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }
            int count = initialCount / 100;

            while (count > 0)
            {
                skip += 100;
                var tmpResponse = FindCommandOrDscResource(tags, includePrerelease, isSearchingForCommands, skip, out edi);
                if (edi != null)
                {
                    return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }
                responses.Add(tmpResponse);
                count--;
            }

            return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
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
            // Make sure to include quotations around the package name
            var prerelease = includePrerelease ? "IsAbsoluteLatestVersion" : "IsLatestVersion";

            // This should return the latest stable version or the latest prerelease version (respectively)
            // https://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'&$filter=IsLatestVersion and substringof('PSModule', Tags) eq true
            // We need to explicitly add 'Id eq <packageName>' whenever $filter is used, otherwise arbitrary results are returned.
            string idFilterPart = $" and Id eq '{packageName}'";
            string typeFilterPart = GetTypeFilterForRequest(type);
            var requestUrlV2 = $"{Repository.Uri}/FindPackagesById()?id='{packageName}'&$filter={prerelease}{idFilterPart}{typeFilterPart}";

            string response = HttpRequestCall(requestUrlV2, out edi);  
            return new FindResults(stringResponse: new string[]{ response }, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name and tag and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "PowerShellGet" -Tag "Provider"
        /// Implementation Note: Need to filter further for latest version (prerelease or non-prerelease dependening on user preference)
        /// </summary>
        public override FindResults FindNameWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            // Make sure to include quotations around the package name
            var prerelease = includePrerelease ? "IsAbsoluteLatestVersion" : "IsLatestVersion";

            // This should return the latest stable version or the latest prerelease version (respectively)
            // https://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'&$filter=IsLatestVersion and substringof('PSModule', Tags) eq true
            // We need to explicitly add 'Id eq <packageName>' whenever $filter is used, otherwise arbitrary results are returned.
            string idFilterPart = $" and Id eq '{packageName}'";
            string typeFilterPart = GetTypeFilterForRequest(type);
            string tagFilterPart = String.Empty;
            foreach (string tag in tags)
            {
                tagFilterPart += $" and substringof('{tag}', Tags) eq true";
            }

            var requestUrlV2 = $"{Repository.Uri}/FindPackagesById()?id='{packageName}'&$filter={prerelease}{idFilterPart}{typeFilterPart}{tagFilterPart}";

            string response = HttpRequestCall(requestUrlV2, out edi);
            return new FindResults(stringResponse: new string[] { response }, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
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
            List<string> responses = new List<string>();
            int skip = 0;

            var initialResponse = FindNameGlobbing(packageName, type, includePrerelease, skip, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            responses.Add(initialResponse);

            // check count (regex)  425 ==> count/100  ~~>  4 calls 
            int initalCount = GetCountFromResponse(initialResponse, out edi);  // count = 4
            if (edi != null)
            {
                return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }
            int count = initalCount / 100;
            // if more than 100 count, loop and add response to list
            while (count > 0)
            {
                // skip 100
                skip += 100;
                var tmpResponse = FindNameGlobbing(packageName, type, includePrerelease, skip, out edi);
                if (edi != null)
                {
                    return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }
                responses.Add(tmpResponse);
                count--;
            }

            return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
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
            int skip = 0;

            var initialResponse = FindNameGlobbingWithTag(packageName, tags, type, includePrerelease, skip, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            responses.Add(initialResponse);

            // check count (regex)  425 ==> count/100  ~~>  4 calls 
            int initalCount = GetCountFromResponse(initialResponse, out edi);  // count = 4
            if (edi != null)
            {
                return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }
            int count = initalCount / 100;
            // if more than 100 count, loop and add response to list
            while (count > 0)
            {
                // skip 100
                skip += 100;
                var tmpResponse = FindNameGlobbingWithTag(packageName, tags, type, includePrerelease, skip, out edi);
                if (edi != null)
                {
                    return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }
                responses.Add(tmpResponse);
                count--;
            }

            return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
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
            List<string> responses = new List<string>();
            int skip = 0;

            var initialResponse = FindVersionGlobbing(packageName, versionRange, includePrerelease, type, skip, getOnlyLatest, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }
            responses.Add(initialResponse);

            if (!getOnlyLatest)
            {
                int initalCount = GetCountFromResponse(initialResponse, out edi);
                if (edi != null)
                {
                    return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }
                int count = initalCount / 100;

                while (count > 0)
                {
                    // skip 100
                    skip += 100;
                    var tmpResponse = FindVersionGlobbing(packageName, versionRange, includePrerelease, type, skip, getOnlyLatest, out edi);
                    if (edi != null)
                    {
                        return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                    }
                    responses.Add(tmpResponse);
                    count--;
                }
            }

            return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
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
            // https://www.powershellgallery.com/api/v2/FindPackagesById()?id='blah'&includePrerelease=false&$filter= NormalizedVersion eq '1.1.0' and substringof('PSModule', Tags) eq true 
            // Quotations around package name and version do not matter, same metadata gets returned.
            // We need to explicitly add 'Id eq <packageName>' whenever $filter is used, otherwise arbitrary results are returned.
            string idFilterPart = $" and Id eq '{packageName}'";
            string typeFilterPart = GetTypeFilterForRequest(type);
            var requestUrlV2 = $"{Repository.Uri}/FindPackagesById()?id='{packageName}'&$filter= NormalizedVersion eq '{version}'{idFilterPart}{typeFilterPart}";
            
            string response = HttpRequestCall(requestUrlV2, out edi);  
            return new FindResults(stringResponse: new string[] { response }, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version and tag.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5" -Tag "Provider"
        /// </summary>
        public override FindResults FindVersionWithTag(string packageName, string version, string[] tags, ResourceType type, out ExceptionDispatchInfo edi)
        {
            // We need to explicitly add 'Id eq <packageName>' whenever $filter is used, otherwise arbitrary results are returned.
            string idFilterPart = $" and Id eq '{packageName}'";
            string typeFilterPart = GetTypeFilterForRequest(type);
            string tagFilterPart = String.Empty;
            foreach (string tag in tags)
            {
                tagFilterPart += $" and substringof('{tag}', Tags) eq true";
            }

            var requestUrlV2 = $"{Repository.Uri}/FindPackagesById()?id='{packageName}'&$filter= NormalizedVersion eq '{version}'{idFilterPart}{typeFilterPart}{tagFilterPart}";
            
            string response = HttpRequestCall(requestUrlV2, out edi);
            return new FindResults(stringResponse: new string[] { response }, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
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
            var requestUrlV2 = $"{Repository.Uri}/package/{packageName}";

            var response = HttpRequestCallForContent(requestUrlV2, out edi);
            var responseStream = response.ReadAsStreamAsync().Result;
            return responseStream;
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
            var requestUrlV2 = $"{Repository.Uri}/package/{packageName}/{version}";

            var response = HttpRequestCallForContent(requestUrlV2, out edi);
            var responseStream = response.ReadAsStreamAsync().Result;
            return responseStream;
        }

        /// <summary>
        /// Helper method that makes the HTTP request for the V2 server protocol url passed in for find APIs.
        /// </summary>
        private string HttpRequestCall(string requestUrlV2, out ExceptionDispatchInfo edi)
        {
            edi = null;
            string response = string.Empty;

            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV2);
                
                response = SendV2RequestAsync(request, _sessionClient).GetAwaiter().GetResult();
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

        /// <summary>
        /// Helper method that makes the HTTP request for the V2 server protocol url passed in for install APIs.
        /// </summary>
        private HttpContent HttpRequestCallForContent(string requestUrlV2, out ExceptionDispatchInfo edi) 
        {
            edi = null;
            HttpContent content = null;

            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV2);
                
                content = SendV2RequestForContentAsync(request, _sessionClient).GetAwaiter().GetResult();
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

        #endregion

        #region Private Methods

        /// <summary>
        /// Helper method for string[] FindAll(string, PSRepositoryInfo, bool, bool, ResourceType, out string)
        /// </summary>
        private string FindAllFromTypeEndPoint(bool includePrerelease, bool isSearchingModule, int skip, out ExceptionDispatchInfo edi)
        {
            string typeEndpoint = isSearchingModule ? String.Empty : "/items/psscript";
            string paginationParam = $"&$orderby=Id desc&$inlinecount=allpages&$skip={skip}&$top=6000";
            var prereleaseFilter = includePrerelease ? "IsAbsoluteLatestVersion&includePrerelease=true" : "IsLatestVersion";

            var requestUrlV2 = $"{Repository.Uri}{typeEndpoint}/Search()?$filter={prereleaseFilter}{paginationParam}";

            return HttpRequestCall(requestUrlV2, out edi);
        }

        /// <summary>
        /// Helper method for string[] FindTag(string, PSRepositoryInfo, bool, bool, ResourceType, out string)
        /// </summary>
        private string FindTagFromEndpoint(string[] tags, bool includePrerelease, bool isSearchingModule, int skip, out ExceptionDispatchInfo edi)
        {
            // scenarios with type + tags:
            // type: None -> search both endpoints
            // type: M -> just search Module endpoint
            // type: S -> just search Scripts end point
            // type: DSCResource -> just search Modules
            // type: Command -> just search Modules
            string typeEndpoint = isSearchingModule ? String.Empty : "/items/psscript";
            string paginationParam = $"&$orderby=Id desc&$inlinecount=allpages&$skip={skip}&$top=6000";
            var prereleaseFilter = includePrerelease ? "$filter=IsAbsoluteLatestVersion&includePrerelease=true" : "$filter=IsLatestVersion";
            string typeFilterPart = isSearchingModule ?  $" and substringof('PSModule', Tags) eq true" : $" and substringof('PSScript', Tags) eq true";
            
            string tagFilterPart = String.Empty;
            foreach (string tag in tags)
            {
                tagFilterPart += $" and substringof('{tag}', Tags) eq true";
            }

            var requestUrlV2 = $"{Repository.Uri}{typeEndpoint}/Search()?{prereleaseFilter}{typeFilterPart}{tagFilterPart}{paginationParam}";

            return HttpRequestCall(requestUrlV2: requestUrlV2, out edi);  
        }

        /// <summary>
        /// Helper method for string[] FindCommandOrDSCResource(string, PSRepositoryInfo, bool, bool, ResourceType, out string)
        /// </summary>
        private string FindCommandOrDscResource(string[] tags, bool includePrerelease, bool isSearchingForCommands, int skip, out ExceptionDispatchInfo edi)
        {
            // can only find from Modules endpoint
            string paginationParam = $"&$orderby=Id desc&$inlinecount=allpages&$skip={skip}&$top=6000";
            var prereleaseFilter = includePrerelease ? "$filter=IsAbsoluteLatestVersion&includePrerelease=true" : "$filter=IsLatestVersion";

            var tagPrefix = isSearchingForCommands ? "PSCommand_" : "PSDscResource_";
            string tagSearchTermPart = String.Empty;
            foreach (string tag in tags)
            {
                if (!String.IsNullOrEmpty(tagSearchTermPart))
                {
                    tagSearchTermPart += " ";
                }

                tagSearchTermPart += $"tag:{tagPrefix}{tag}";
            }

            var requestUrlV2 = $"{Repository.Uri}/Search()?{prereleaseFilter}&searchTerm='{tagSearchTermPart}'{paginationParam}";
            return HttpRequestCall(requestUrlV2, out edi);
        }

        /// <summary>
        /// Helper method for string[] FindNameGlobbing()
        /// </summary>
        private string FindNameGlobbing(string packageName, ResourceType type, bool includePrerelease, int skip, out ExceptionDispatchInfo edi)
        {
            // https://www.powershellgallery.com/api/v2/Search()?$filter=endswith(Id, 'Get') and startswith(Id, 'PowerShell') and IsLatestVersion (stable)
            // https://www.powershellgallery.com/api/v2/Search()?$filter=endswith(Id, 'Get') and IsAbsoluteLatestVersion&includePrerelease=true
            
            string extraParam = $"&$orderby=Id desc&$inlinecount=allpages&$skip={skip}&$top=100";
            var prerelease = includePrerelease ? "IsAbsoluteLatestVersion&includePrerelease=true" : "IsLatestVersion";
            string nameFilter;

            var names = packageName.Split(new char[] {'*'}, StringSplitOptions.RemoveEmptyEntries);

            if (names.Length == 0)
            {
                edi = ExceptionDispatchInfo.Capture(new ArgumentException("-Name '*' for V2 server protocol repositories is not supported"));
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
                edi = ExceptionDispatchInfo.Capture(new ArgumentException("-Name with wildcards is only supported for scenarios similar to the following examples: PowerShell*, *ShellGet, *Shell*."));
                return string.Empty;
            }

            string typeFilterPart = GetTypeFilterForRequest(type);
            var requestUrlV2 = $"{Repository.Uri}/Search()?$filter={nameFilter}{typeFilterPart} and {prerelease}{extraParam}";
            
            return HttpRequestCall(requestUrlV2, out edi);  
        }

        /// <summary>
        /// Helper method for string[] FindNameGlobbingWithTag()
        /// </summary>
        private string FindNameGlobbingWithTag(string packageName, string[] tags, ResourceType type, bool includePrerelease, int skip, out ExceptionDispatchInfo edi)
        {
            // https://www.powershellgallery.com/api/v2/Search()?$filter=endswith(Id, 'Get') and startswith(Id, 'PowerShell') and IsLatestVersion (stable)
            // https://www.powershellgallery.com/api/v2/Search()?$filter=endswith(Id, 'Get') and IsAbsoluteLatestVersion&includePrerelease=true
            
            string extraParam = $"&$orderby=Id desc&$inlinecount=allpages&$skip={skip}&$top=100";
            var prerelease = includePrerelease ? "IsAbsoluteLatestVersion&includePrerelease=true" : "IsLatestVersion";
            string nameFilter;

            var names = packageName.Split(new char[] {'*'}, StringSplitOptions.RemoveEmptyEntries);

            if (names.Length == 0)
            {
                edi = ExceptionDispatchInfo.Capture(new ArgumentException("-Name '*' for V2 server protocol repositories is not supported"));
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
                edi = ExceptionDispatchInfo.Capture(new ArgumentException("-Name with wildcards is only supported for scenarios similar to the following examples: PowerShell*, *ShellGet, *Shell*."));
                return string.Empty;
            }

            string tagFilterPart = String.Empty;
            foreach (string tag in tags)
            {
                tagFilterPart += $" and substringof('{tag}', Tags) eq true";
            }
            
            string typeFilterPart = GetTypeFilterForRequest(type);
            var requestUrlV2 = $"{Repository.Uri}/Search()?$filter={nameFilter}{tagFilterPart}{typeFilterPart} and {prerelease}{extraParam}";
            
            return HttpRequestCall(requestUrlV2, out edi);  
        }

        /// <summary>
        /// Helper method for string[] FindVersionGlobbing()
        /// </summary>
        private string FindVersionGlobbing(string packageName, VersionRange versionRange, bool includePrerelease, ResourceType type, int skip, bool getOnlyLatest, out ExceptionDispatchInfo edi)
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
            
            string andOperator = " and ";
            string joiningOperator = filterQuery.EndsWith("=") ? String.Empty : andOperator;
            // We need to explicitly add 'Id eq <packageName>' whenever $filter is used, otherwise arbitrary results are returned.
            string idFilterPart = $"{joiningOperator}Id eq '{packageName}'";
            filterQuery += idFilterPart;
            filterQuery += type == ResourceType.Script ? $"{andOperator}substringof('PS{type.ToString()}', Tags) eq true" : String.Empty;

            if (!String.IsNullOrEmpty(versionFilterParts))
            {
                // Check if includePrerelease is true, if it is we want to add "$filter"
                // Single case where version is "*" (or "[,]") and includePrerelease is true, then we do not want to add "$filter" to the requestUrl.
        
                // Note: could be null/empty if Version was "*" -> [,]
                filterQuery +=  $"{andOperator}{versionFilterParts}";
            }

            string topParam = getOnlyLatest ? "$top=1" : "$top=100"; // only need 1 package if interested in latest
            string paginationParam = $"$inlinecount=allpages&$skip={skip}&{topParam}";

            filterQuery = filterQuery.EndsWith("=") ? string.Empty : filterQuery;
            var requestUrlV2 = $"{Repository.Uri}/FindPackagesById()?id='{packageName}'&$orderby=NormalizedVersion desc&{paginationParam}{filterQuery}";

            return HttpRequestCall(requestUrlV2, out edi);  
        }

        private string GetTypeFilterForRequest(ResourceType type) {
            string typeFilterPart = string.Empty;
            if (type == ResourceType.Script)
            {
                typeFilterPart += $" and substringof('PS{type.ToString()}', Tags) eq true ";
            }
            else if (type == ResourceType.Module)
            {
                typeFilterPart += $" and substringof('PS{ResourceType.Script.ToString()}', Tags) eq false ";
            }

            return typeFilterPart;
        }

        /// <summary>
        /// Helper method that makes gets 'count' property from http response string.
        /// The count property is used to determine the number of total results found (for pagination).
        /// </summary>
        public int GetCountFromResponse(string httpResponse, out ExceptionDispatchInfo edi)
        {
            edi = null;
            int count = 0;

            //Create the XmlDocument.
            XmlDocument doc = new XmlDocument();

            try
            {
                doc.LoadXml(httpResponse);
            }
            catch (XmlException e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
            }
            if (edi != null)
            {
                return count;
            }

            XmlNodeList elemList = doc.GetElementsByTagName("m:count");
            if (elemList.Count > 0)
            {
                XmlNode node = elemList[0];
                count = int.Parse(node.InnerText);
            }

            return count;
        }

        /// <summary>
        /// Helper method called by HttpRequestCall() that makes the HTTP request for string response.
        /// </summary>
        public static async Task<string> SendV2RequestAsync(HttpRequestMessage message, HttpClient s_client)
        {
            string errMsg = "Error occured while trying to retrieve response: ";
            try
            {
                HttpResponseMessage response = await s_client.SendAsync(message);
                response.EnsureSuccessStatusCode();
                return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
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
        /// Helper method called by HttpRequestCallForContent() that makes the HTTP request for HTTP Content response.
        /// </summary>
        public static async Task<HttpContent> SendV2RequestForContentAsync(HttpRequestMessage message, HttpClient s_client)
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
