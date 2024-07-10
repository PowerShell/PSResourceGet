// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using NuGet.Versioning;
using System.Threading.Tasks;
using System.Xml;
using System.Net;
using System.Text;
using System.Runtime.ExceptionServices;
using System.Management.Automation;
using System.Reflection;
using System.Data.Common;
using System.Linq;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
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
        private readonly PSCmdlet _cmdletPassedIn;
        private HttpClient _sessionClient { get; set; }
        private static readonly Hashtable[] emptyHashResponses = new Hashtable[]{};
        public FindResponseType v2FindResponseType = FindResponseType.ResponseString;
        private bool _isADORepo;
        private bool _isJFrogRepo;
        private bool _isPSGalleryRepo;

        #endregion

        #region Constructor

        public V2ServerAPICalls (PSRepositoryInfo repository, PSCmdlet cmdletPassedIn, NetworkCredential networkCredential, string userAgentString) : base (repository, networkCredential)
        {
            this.Repository = repository;
            _cmdletPassedIn = cmdletPassedIn;
            HttpClientHandler handler = new HttpClientHandler();
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
            var repoURL = repository.Uri.ToString().ToLower();
            _isADORepo = repoURL.Contains("pkgs.dev.azure.com") || repoURL.Contains("pkgs.visualstudio.com");
            _isJFrogRepo = repoURL.Contains("jfrog") || repoURL.Contains("artifactory");
            _isPSGalleryRepo = repoURL.Contains("powershellgallery.com/api/v2");
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
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::FindAll()");
            errRecord = null;
            List<string> responses = new List<string>();

            if (type == ResourceType.Script || type == ResourceType.None)
            {
                int scriptSkip = 0;
                string initialScriptResponse = FindAllFromTypeEndPoint(includePrerelease, isSearchingModule: false, scriptSkip, out errRecord);
                if (errRecord != null)
                {
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }

                int initialScriptCount = GetCountFromResponse(initialScriptResponse, out errRecord);
                if (errRecord != null)
                {
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }

                if (initialScriptCount != 0)
                {
                    responses.Add(initialScriptResponse);
                    int count = initialScriptCount / 6000;
                    // if more than 100 count, loop and add response to list
                    while (count > 0)
                    {
                        _cmdletPassedIn.WriteDebug($"Count is '{count}'");
                        scriptSkip += 6000;
                        var tmpResponse = FindAllFromTypeEndPoint(includePrerelease, isSearchingModule: false, scriptSkip, out errRecord);
                        if (errRecord != null)
                        {
                            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                        }

                        responses.Add(tmpResponse);
                        count--;
                    }
                }
            }
            if (type != ResourceType.Script)
            {
                int moduleSkip = 0;
                string initialModuleResponse = FindAllFromTypeEndPoint(includePrerelease, isSearchingModule: true, moduleSkip, out errRecord);
                if (errRecord != null)
                {
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }

                int initialModuleCount = GetCountFromResponse(initialModuleResponse, out errRecord);
                if (errRecord != null)
                {
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }

                if (initialModuleCount != 0)
                {
                    responses.Add(initialModuleResponse);
                    int count = initialModuleCount / 6000;

                    // if more than 100 count, loop and add response to list
                    while (count > 0)
                    {
                        _cmdletPassedIn.WriteDebug($"Count is '{count}'");
                        moduleSkip += 6000;
                        var tmpResponse = FindAllFromTypeEndPoint(includePrerelease, isSearchingModule: true, moduleSkip, out errRecord);
                        if (errRecord != null)
                        {
                            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                        }

                        responses.Add(tmpResponse);
                        count--;
                    }
                }
            }

            return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag from a repository and returns latest version for each.
        /// Examples: Search -Tag "JSON" -Repository PSGallery
        /// API call:
        /// - Include prerelease: https://www.powershellgallery.com/api/v2/Search()?includePrerelease=true&$filter=IsAbsoluteLatestVersion and substringof('PSModule', Tags) eq true and substringof('CrescendoBuilt', Tags) eq true&$orderby=Id desc&$inlinecount=allpages&$skip=0&$top=6000
        /// </summary>
        public override FindResults FindTags(string[] tags, bool includePrerelease, ResourceType _type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::FindTags()");
            errRecord = null;
            List<string> responses = new List<string>();

            if (_type == ResourceType.Script || _type == ResourceType.None)
            {
                int scriptSkip = 0;
                string initialScriptResponse = FindTagFromEndpoint(tags, includePrerelease, isSearchingModule: false, scriptSkip, out errRecord);
                if (errRecord != null)
                {
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }

                int initialScriptCount = GetCountFromResponse(initialScriptResponse, out errRecord);
                if (errRecord != null)
                {
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }

                if (initialScriptCount != 0)
                {
                    responses.Add(initialScriptResponse);
                    int count = initialScriptCount / 100;
                    // if more than 100 count, loop and add response to list
                    while (count > 0)
                    {
                        _cmdletPassedIn.WriteDebug($"Count is '{count}'");
                        // skip 100
                        scriptSkip += 100;
                        var tmpResponse = FindTagFromEndpoint(tags, includePrerelease, isSearchingModule: false,  scriptSkip, out errRecord);
                        if (errRecord != null)
                        {
                            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                        }

                        responses.Add(tmpResponse);
                        count--;
                    }
                }
            }
            if (_type != ResourceType.Script)
            {
                int moduleSkip = 0;
                string initialModuleResponse = FindTagFromEndpoint(tags, includePrerelease, isSearchingModule: true, moduleSkip, out errRecord);
                if (errRecord != null)
                {
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }

                int initialModuleCount = GetCountFromResponse(initialModuleResponse, out errRecord);
                if (errRecord != null)
                {
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                }

                if (initialModuleCount != 0)
                {
                    responses.Add(initialModuleResponse);
                    int count = initialModuleCount / 100;
                    // if more than 100 count, loop and add response to list
                    while (count > 0)
                    {
                        _cmdletPassedIn.WriteDebug($"Count is '{count}'");
                        moduleSkip += 100;
                        var tmpResponse = FindTagFromEndpoint(tags, includePrerelease, isSearchingModule: true, moduleSkip, out errRecord);
                        if (errRecord != null)
                        {
                            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                        }

                        responses.Add(tmpResponse);
                        count--;
                    }
                }
            }

            if (responses.Count == 0)
            {
                errRecord = new ErrorRecord(
                    new ResourceNotFoundException($"Package with Tags '{String.Join(", ", tags)}' could not be found in repository '{Repository.Name}'."),
                    "PackageWithSpecifiedTagsNotFound",
                    ErrorCategory.ObjectNotFound,
                    this);
            }

            return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for all packages that have specified Command or DSCResource name.
        /// </summary>
        public override FindResults FindCommandOrDscResource(string[] tags, bool includePrerelease, bool isSearchingForCommands, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::FindCommandOrDscResource()");
            List<string> responses = new List<string>();
            int skip = 0;

            string initialResponse = FindCommandOrDscResource(tags, includePrerelease, isSearchingForCommands, skip, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            int initialCount = GetCountFromResponse(initialResponse, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            if (initialCount != 0)
            {
                responses.Add(initialResponse);
                int count = (int)Math.Ceiling((double)(initialCount / 100));

                while (count > 0)
                {
                    _cmdletPassedIn.WriteDebug($"Count is '{count}'");
                    skip += 100;
                    var tmpResponse = FindCommandOrDscResource(tags, includePrerelease, isSearchingForCommands, skip, out errRecord);
                    if (errRecord != null)
                    {
                        return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
                    }

                    responses.Add(tmpResponse);
                    count--;
                }
            }

            if (responses.Count == 0)
            {
                string parameterForErrorMsg = isSearchingForCommands ? "Command" : "DSC Resource";
                errRecord = new ErrorRecord(
                    new ResourceNotFoundException($"Package with {parameterForErrorMsg} '{String.Join(", ", tags)}' could not be found in repository '{Repository.Name}'."),
                    "PackageWithSpecifiedCmdOrDSCNotFound",
                    ErrorCategory.InvalidResult,
                    this);
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
        public override FindResults FindName(string packageName, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::FindName()");
            // Make sure to include quotations around the package name

            // This should return the latest stable version or the latest prerelease version (respectively)
            // https://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'&$filter=IsLatestVersion and substringof('PSModule', Tags) eq true
            // We need to explicitly add 'Id eq <packageName>' whenever $filter is used, otherwise arbitrary results are returned.

            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string>{
                { "$inlinecount", "allpages" },
                { "id", $"'{packageName}'" },
            });
            var filterBuilder = queryBuilder.FilterBuilder;

            // If it's a JFrog repository do not include the Id filter portion since JFrog uses 'Title' instead of 'Id',
            // however filtering on 'and Title eq '<packageName>' returns "Response status code does not indicate success: 500".
            if (!_isJFrogRepo) {
                filterBuilder.AddCriterion($"Id eq '{packageName}'");
            }

            filterBuilder.AddCriterion(includePrerelease ? "IsAbsoluteLatestVersion" : "IsLatestVersion");
            if (type != ResourceType.None) {
                filterBuilder.AddCriterion(GetTypeFilterForRequest(type));
            }
            
            var requestUrlV2 = $"{Repository.Uri}/FindPackagesById()?{queryBuilder.BuildQueryString()}";
            string response = HttpRequestCall(requestUrlV2, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            int count = GetCountFromResponse(response, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            if (count == 0)
            {
                errRecord = new ErrorRecord(
                    new ResourceNotFoundException($"Package with name '{packageName}' could not be found in repository '{Repository.Name}'."),
                    "PackageNotFound",
                    ErrorCategory.ObjectNotFound,
                    this);
                response = string.Empty;
            }

            return new FindResults(stringResponse: new string[]{ response }, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name and tag and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "PowerShellGet" -Tag "Provider"
        /// Implementation Note: Need to filter further for latest version (prerelease or non-prerelease dependening on user preference)
        /// </summary>
        public override FindResults FindNameWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::FindNameWithTag()");
            // Make sure to include quotations around the package name

            // This should return the latest stable version or the latest prerelease version (respectively)
            // https://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'&$filter=IsLatestVersion and substringof('PSModule', Tags) eq true
            // We need to explicitly add 'Id eq <packageName>' whenever $filter is used, otherwise arbitrary results are returned.

            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string>{
                { "$inlinecount", "allpages" },
                { "id", $"'{packageName}'" },
            });
            var filterBuilder = queryBuilder.FilterBuilder;

            // If it's a JFrog repository do not include the Id filter portion since JFrog uses 'Title' instead of 'Id',
            // however filtering on 'and Title eq '<packageName>' returns "Response status code does not indicate success: 500".
            if (!_isJFrogRepo) {
                filterBuilder.AddCriterion($"Id eq '{packageName}'");
            }

            filterBuilder.AddCriterion(includePrerelease ? "IsAbsoluteLatestVersion" : "IsLatestVersion");
            if (type != ResourceType.None) {
                filterBuilder.AddCriterion(GetTypeFilterForRequest(type));
            }

            foreach (string tag in tags)
            {
                filterBuilder.AddCriterion($"substringof('{tag}', Tags) eq true");
            }

            var requestUrlV2 = $"{Repository.Uri}/FindPackagesById()?{queryBuilder.BuildQueryString()}";
            
            string response = HttpRequestCall(requestUrlV2, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            int count = GetCountFromResponse(response, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            if (count == 0)
            {
                errRecord = new ErrorRecord(
                    new ResourceNotFoundException($"Package with name '{packageName}' and tags '{String.Join(", ", tags)}' could not be found in repository '{Repository.Name}'."),
                    "PackageNotFound",
                    ErrorCategory.ObjectNotFound,
                    this);
                response = string.Empty;
            }

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
        public override FindResults FindNameGlobbing(string packageName, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::FindNameGlobbing()");
            List<string> responses = new List<string>();
            int skip = 0;

            var initialResponse = FindNameGlobbing(packageName, type, includePrerelease, skip, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            responses.Add(initialResponse);

            // check count (regex)  425 ==> count/100  ~~>  4 calls
            int initialCount = GetCountFromResponse(initialResponse, out errRecord);  // count = 4
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            // If count is 0, early out as this means no packages matching search criteria were found. We want to set the responses array to empty and not set ErrorRecord (as is a globbing scenario).
            if (initialCount == 0)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            int count = (int)Math.Ceiling((double)(initialCount / 100));
            // if more than 100 count, loop and add response to list
            while (count > 0)
            {
                _cmdletPassedIn.WriteDebug($"Count is '{count}'");
                // skip 100
                skip += 100;
                var tmpResponse = FindNameGlobbing(packageName, type, includePrerelease, skip, out errRecord);
                if (errRecord != null)
                {
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
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
        public override FindResults FindNameGlobbingWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::FindNameGlobbingWithTag()");
            List<string> responses = new List<string>();
            int skip = 0;

            var initialResponse = FindNameGlobbingWithTag(packageName, tags, type, includePrerelease, skip, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            responses.Add(initialResponse);

            // check count (regex)  425 ==> count/100  ~~>  4 calls
            int initialCount = GetCountFromResponse(initialResponse, out errRecord);  // count = 4
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            if (initialCount == 0)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            int count = (int)Math.Ceiling((double)(initialCount / 100));
            // if more than 100 count, loop and add response to list
            while (count > 0)
            {
                _cmdletPassedIn.WriteDebug($"Count is '{count}'");
                // skip 100
                skip += 100;
                var tmpResponse = FindNameGlobbingWithTag(packageName, tags, type, includePrerelease, skip, out errRecord);
                if (errRecord != null)
                {
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
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
        public override FindResults FindVersionGlobbing(string packageName, VersionRange versionRange, bool includePrerelease, ResourceType type, bool getOnlyLatest, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::FindVersionGlobbing()");
            List<string> responses = new List<string>();
            int skip = 0;

            var initialResponse = FindVersionGlobbing(packageName, versionRange, includePrerelease, type, skip, getOnlyLatest, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            int initialCount = GetCountFromResponse(initialResponse, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            if (initialCount == 0)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            responses.Add(initialResponse);

            if (!getOnlyLatest)
            {
                int count = (int)Math.Ceiling((double)(initialCount / 100));

                while (count > 0)
                {
                    _cmdletPassedIn.WriteDebug($"Count is '{count}'");
                    // skip 100
                    skip += 100;
                    var tmpResponse = FindVersionGlobbing(packageName, versionRange, includePrerelease, type, skip, getOnlyLatest, out errRecord);
                    if (errRecord != null)
                    {
                        return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
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
        public override FindResults FindVersion(string packageName, string version, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::FindVersion()");
            // https://www.powershellgallery.com/api/v2/FindPackagesById()?id='blah'&includePrerelease=false&$filter= NormalizedVersion eq '1.1.0' and substringof('PSModule', Tags) eq true
            // Quotations around package name and version do not matter, same metadata gets returned.
            // We need to explicitly add 'Id eq <packageName>' whenever $filter is used, otherwise arbitrary results are returned.

            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string>{
                { "$inlinecount", "allpages" },
                { "id", $"'{packageName}'" },
            });
            var filterBuilder = queryBuilder.FilterBuilder;

            // If it's a JFrog repository do not include the Id filter portion since JFrog uses 'Title' instead of 'Id',
            // however filtering on 'and Title eq '<packageName>' returns "Response status code does not indicate success: 500".
            if (!_isJFrogRepo) {
                filterBuilder.AddCriterion($"Id eq '{packageName}'");
            }
            
            filterBuilder.AddCriterion($"NormalizedVersion eq '{version}'");
            if (type != ResourceType.None) {
                filterBuilder.AddCriterion(GetTypeFilterForRequest(type));
            }

            var requestUrlV2 = $"{Repository.Uri}/FindPackagesById()?{queryBuilder.BuildQueryString()}";
            string response = HttpRequestCall(requestUrlV2, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            int count = GetCountFromResponse(response, out errRecord);
            _cmdletPassedIn.WriteDebug($"Count from response is '{count}'");

            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            if (count == 0)
            {
                errRecord = new ErrorRecord(
                    new ResourceNotFoundException($"Package with name '{packageName}', version '{version}' could not be found in repository '{Repository.Name}'."),
                    "PackageNotFound",
                    ErrorCategory.ObjectNotFound,
                    this);
                response = string.Empty;
            }

            return new FindResults(stringResponse: new string[] { response }, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version and tag.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5" -Tag "Provider"
        /// </summary>
        public override FindResults FindVersionWithTag(string packageName, string version, string[] tags, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::FindVersionWithTag()");

            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string>{
                { "$inlinecount", "allpages" },
                { "id", $"'{packageName}'" },
            });
            var filterBuilder = queryBuilder.FilterBuilder;
            // We need to explicitly add 'Id eq <packageName>' whenever $filter is used, otherwise arbitrary results are returned.

            // If it's a JFrog repository do not include the Id filter portion since JFrog uses 'Title' instead of 'Id',
            // however filtering on 'and Title eq '<packageName>' returns "Response status code does not indicate success: 500".
            if (!_isJFrogRepo) {
                filterBuilder.AddCriterion($"Id eq '{packageName}'");
            }
            
            filterBuilder.AddCriterion($"NormalizedVersion eq '{version}'");
            if (type != ResourceType.None) {
                filterBuilder.AddCriterion(GetTypeFilterForRequest(type));
            }

            foreach (string tag in tags)
            {
                filterBuilder.AddCriterion($"substringof('{tag}', Tags) eq true");
            }

            var requestUrlV2 = $"{Repository.Uri}/FindPackagesById()?{queryBuilder.BuildQueryString()}";
            string response = HttpRequestCall(requestUrlV2, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            int count = GetCountFromResponse(response, out errRecord);
            _cmdletPassedIn.WriteDebug($"Count from response is '{count}'");

            if (errRecord != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
            }

            if (count == 0)
            {
                errRecord = new ErrorRecord(
                    new ResourceNotFoundException($"Package with name '{packageName}', version '{version}' and tags '{String.Join(", ", tags)}' could not be found in repository '{Repository.Name}'."),
                    "PackageNotFound",
                    ErrorCategory.ObjectNotFound,
                    this);
                response = string.Empty;
            }

            return new FindResults(stringResponse: new string[] { response }, hashtableResponse: emptyHashResponses, responseType: v2FindResponseType);
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

        /// <summary>
        /// Helper method that makes the HTTP request for the V2 server protocol url passed in for find APIs.
        /// </summary>
        private string HttpRequestCall(string requestUrlV2, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::HttpRequestCall()");
            errRecord = null;
            string response = string.Empty;

            try
            {
                _cmdletPassedIn.WriteDebug($"Request url is '{requestUrlV2}'");
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV2);

                response = SendV2RequestAsync(request, _sessionClient).GetAwaiter().GetResult();
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
                    ErrorCategory.ConnectionError,
                    this);
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "HttpRequestCallFailure",
                    ErrorCategory.ConnectionError,
                    this);
            }

            if (string.IsNullOrEmpty(response))
            {
                _cmdletPassedIn.WriteDebug("Response is empty");
            }

            return response;
        }

        /// <summary>
        /// Helper method that makes the HTTP request for the V2 server protocol url passed in for install APIs.
        /// </summary>
        private HttpContent HttpRequestCallForContent(string requestUrlV2, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::HttpRequestCallForContent()");
            errRecord = null;
            HttpContent content = null;

            try
            {
                _cmdletPassedIn.WriteDebug($"Request url is '{requestUrlV2}'");
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV2);

                content = SendV2RequestForContentAsync(request, _sessionClient).GetAwaiter().GetResult();
            }
            catch (HttpRequestException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "HttpRequestFailure",
                    ErrorCategory.ConnectionError,
                    this);
            }
            catch (ArgumentNullException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "HttpRequestFailure",
                    ErrorCategory.InvalidData,
                    this);
            }
            catch (InvalidOperationException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "HttpRequestFailure",
                    ErrorCategory.InvalidOperation,
                    this);
            }

            if (content == null || string.IsNullOrEmpty(content.ToString()))
            {
                _cmdletPassedIn.WriteDebug("Response is empty");
            }

            return content;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Helper method for string[] FindAll(string, PSRepositoryInfo, bool, bool, ResourceType, out string)
        /// </summary>
        private string FindAllFromTypeEndPoint(bool includePrerelease, bool isSearchingModule, int skip, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::FindAllFromTypeEndPoint()");
            string typeEndpoint = _isPSGalleryRepo && !isSearchingModule ? "/items/psscript" : String.Empty;

            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string>{
                { "$inlinecount", "allpages" },
                { "$skip", skip.ToString()},
                { "$top", "6000"}
            });
            var filterBuilder = queryBuilder.FilterBuilder;

            if (_isPSGalleryRepo) {
                queryBuilder.AdditionalParameters["$orderby"] = "Id desc";
            }

            // JFrog/Artifactory requires an empty search term to enumerate all packages in the feed
            if (_isJFrogRepo) {
                queryBuilder.SearchTerm = "''";
            }

            if (includePrerelease) {
                queryBuilder.AdditionalParameters["includePrerelease"] = "true";
                filterBuilder.AddCriterion("IsAbsoluteLatestVersion");
            } else {
                filterBuilder.AddCriterion("IsLatestVersion");
            }
            var requestUrlV2 = $"{Repository.Uri}{typeEndpoint}/Search()?$filter={queryBuilder.BuildQueryString()}";
            return HttpRequestCall(requestUrlV2, out errRecord);
        }

        /// <summary>
        /// Helper method for string[] FindTag(string, PSRepositoryInfo, bool, bool, ResourceType, out string)
        /// </summary>
        private string FindTagFromEndpoint(string[] tags, bool includePrerelease, bool isSearchingModule, int skip, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::FindTagFromEndpoint()");
            // scenarios with type + tags:
            // type: None -> search both endpoints
            // type: M -> just search Module endpoint
            // type: S -> just search Scripts end point
            // type: DSCResource -> just search Modules
            // type: Command -> just search Modules
            string typeEndpoint = _isPSGalleryRepo && !isSearchingModule ? "/items/psscript" : String.Empty;

            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string>{
                { "$inlinecount", "allpages" },
                { "$skip", skip.ToString()},
                { "$top", "6000"}
            });
            var filterBuilder = queryBuilder.FilterBuilder;

            if (_isPSGalleryRepo) {
                queryBuilder.AdditionalParameters["$orderby"] = "Id desc";
            }

            // JFrog/Artifactory requires an empty search term to enumerate all packages in the feed
            if (_isJFrogRepo) {
                queryBuilder.SearchTerm = "''";
            }

            if (includePrerelease) {
                queryBuilder.AdditionalParameters["includePrerelease"] = "true";
                filterBuilder.AddCriterion("IsAbsoluteLatestVersion");
            } else {
                filterBuilder.AddCriterion("IsLatestVersion");
            }

            filterBuilder.AddCriterion($"substringof('PS{(isSearchingModule ? "Module" : "Script")}', Tags) eq true");
            
            foreach (string tag in tags)
            {
                filterBuilder.AddCriterion($"substringof('{tag}', Tags) eq true");
            }

            var requestUrlV2 = $"{Repository.Uri}{typeEndpoint}/Search()?{queryBuilder.BuildQueryString()}";

            return HttpRequestCall(requestUrlV2: requestUrlV2, out errRecord);
        }

        /// <summary>
        /// Helper method for string[] FindCommandOrDSCResource(string, PSRepositoryInfo, bool, bool, ResourceType, out string)
        /// </summary>
        private string FindCommandOrDscResource(string[] tags, bool includePrerelease, bool isSearchingForCommands, int skip, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::FindCommandOrDscResource()");

            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string>{
                { "$inlinecount", "allpages" },
                { "$skip", skip.ToString()},
                { "$top", "6000"}
            });
            var filterBuilder = queryBuilder.FilterBuilder;

            if (_isPSGalleryRepo) {
                queryBuilder.AdditionalParameters["$orderby"] = "Id desc";
            }

            if (includePrerelease) {
                queryBuilder.AdditionalParameters["includePrerelease"] = "true";
                filterBuilder.AddCriterion("IsAbsoluteLatestVersion");
            } else {
                filterBuilder.AddCriterion("IsLatestVersion");
            }


            // can only find from Modules endpoint
            var tagPrefix = isSearchingForCommands ? "PSCommand_" : "PSDscResource_";

            queryBuilder.SearchTerm = "'" + string.Join(
                " ",
                tags.Select(tag => $"tag:{tagPrefix}{tag}")
            ) + "'";
                

            var requestUrlV2 = $"{Repository.Uri}/Search()?{queryBuilder.BuildQueryString()}";

            return HttpRequestCall(requestUrlV2, out errRecord);
        }

        /// <summary>
        /// Helper method for string[] FindNameGlobbing()
        /// </summary>
        private string FindNameGlobbing(string packageName, ResourceType type, bool includePrerelease, int skip, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::FindNameGlobbing()");
            // https://www.powershellgallery.com/api/v2/Search()?$filter=endswith(Id, 'Get') and startswith(Id, 'PowerShell') and IsLatestVersion (stable)
            // https://www.powershellgallery.com/api/v2/Search()?$filter=endswith(Id, 'Get') and IsAbsoluteLatestVersion&includePrerelease=true

            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string>{
                { "$inlinecount", "allpages" },
                { "$skip", skip.ToString()},
                { "$top", "100"}
            });
            var filterBuilder = queryBuilder.FilterBuilder;

            if (_isPSGalleryRepo) {
                queryBuilder.AdditionalParameters["$orderby"] = "Id desc";
            }

            if (includePrerelease) {
                queryBuilder.AdditionalParameters["includePrerelease"] = "true";
                filterBuilder.AddCriterion("IsAbsoluteLatestVersion");
            } else {
                filterBuilder.AddCriterion("IsLatestVersion");
            }


            var names = packageName.Split(new char[] {'*'}, StringSplitOptions.RemoveEmptyEntries);

            if (names.Length == 0)
            {
                errRecord = new ErrorRecord(
                    new ArgumentException("-Name '*' for V2 server protocol repositories is not supported"),
                    "FindNameGlobbingFailure",
                    ErrorCategory.InvalidArgument,
                    this);

                return string.Empty;
            }
            if (names.Length == 1)
            {
                if (packageName.StartsWith("*") && packageName.EndsWith("*"))
                {
                    // *get*
                    filterBuilder.AddCriterion($"substringof('{names[0]}', Id)");
                }
                else if (packageName.EndsWith("*"))
                {
                    // PowerShell*
                    filterBuilder.AddCriterion($"startswith(Id, '{names[0]}')");
                }
                else
                {
                    // *ShellGet
                    filterBuilder.AddCriterion($"endswith(Id, '{names[0]}')");
                }
            }
            else if (names.Length == 2 && !packageName.StartsWith("*") && !packageName.EndsWith("*"))
            {
                // *pow*get*
                // pow*get -> only support this
                // pow*get*
                // *pow*get
                filterBuilder.AddCriterion($"startswith(Id, '{names[0]}') and endswith(Id, '{names[1]}')");
            }
            else
            {
                errRecord = new ErrorRecord(
                    new ArgumentException("-Name with wildcards is only supported for scenarios similar to the following examples: PowerShell*, *ShellGet, *Shell*."),
                    "FindNameGlobbingFailure",
                    ErrorCategory.InvalidArgument,
                    this);

                return string.Empty;
            }

            if (!_isPSGalleryRepo && type != ResourceType.None)
            {
                errRecord = new ErrorRecord(
                    new ArgumentException("-Name with wildcards with -Type is not supported for this repository."),
                        "FindNameGlobbingNotSupportedForRepo",
                        ErrorCategory.InvalidArgument,
                        this);

                return string.Empty;
            }
            if (type != ResourceType.None) {
                filterBuilder.AddCriterion(GetTypeFilterForRequest(type));
            }
            var requestUrlV2 = $"{Repository.Uri}/Search()?{queryBuilder.BuildQueryString()}";

            return HttpRequestCall(requestUrlV2, out errRecord);
        }

        /// <summary>
        /// Helper method for string[] FindNameGlobbingWithTag()
        /// </summary>
        private string FindNameGlobbingWithTag(string packageName, string[] tags, ResourceType type, bool includePrerelease, int skip, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::FindNameGlobbingWithTag()");
            // https://www.powershellgallery.com/api/v2/Search()?$filter=endswith(Id, 'Get') and startswith(Id, 'PowerShell') and IsLatestVersion (stable)
            // https://www.powershellgallery.com/api/v2/Search()?$filter=endswith(Id, 'Get') and IsAbsoluteLatestVersion&includePrerelease=true

            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string>{
                { "$inlinecount", "allpages" },
                { "$skip", skip.ToString()},
                { "$top", "100"}
            });
            var filterBuilder = queryBuilder.FilterBuilder;

            if (_isPSGalleryRepo) {
                queryBuilder.AdditionalParameters["$orderby"] = "Id desc";
            }

            if (includePrerelease) {
                queryBuilder.AdditionalParameters["includePrerelease"] = "true";
                filterBuilder.AddCriterion("IsAbsoluteLatestVersion");
            } else {
                filterBuilder.AddCriterion("IsLatestVersion");
            }


            var names = packageName.Split(new char[] {'*'}, StringSplitOptions.RemoveEmptyEntries);

            if (!_isPSGalleryRepo)
            {
                errRecord = new ErrorRecord(
                    new ArgumentException("Name globbing with tags is not supported for V2 server protocol repositories."),
                    "FindNameGlobbingAndTagFailure",
                    ErrorCategory.InvalidArgument,
                    this);

                return string.Empty;
            }
            if (names.Length == 0)
            {
                errRecord = new ErrorRecord(
                    new ArgumentException("-Name '*' for V2 server protocol repositories is not supported"),
                    "FindNameGlobbingFailure",
                    ErrorCategory.InvalidArgument,
                    this);

                return string.Empty;
            }
            if (names.Length == 1)
            {
                if (packageName.StartsWith("*") && packageName.EndsWith("*"))
                {
                    filterBuilder.AddCriterion($"substringof('{names[0]}', Id)");
                }
                else if (packageName.EndsWith("*"))
                {
                    // PowerShell*
                    filterBuilder.AddCriterion($"startswith(Id, '{names[0]}')");
                }
                else
                {
                    // *ShellGet
                    filterBuilder.AddCriterion($"endswith(Id, '{names[0]}')");
                }
            }
            else if (names.Length == 2 && !packageName.StartsWith("*") && !packageName.EndsWith("*"))
            {
                // *pow*get*
                // pow*get -> only support this
                // pow*get*
                // *pow*get
                filterBuilder.AddCriterion($"startswith(Id, '{names[0]}') and endswith(Id, '{names[1]}')");
            }
            else
            {
                errRecord = new ErrorRecord(
                    new ArgumentException("-Name with wildcards is only supported for scenarios similar to the following examples: PowerShell*, *ShellGet, *Shell*."),
                    "FindNameGlobbing",
                    ErrorCategory.InvalidArgument,
                    this);

                return string.Empty;
            }

            string tagFilterPart = String.Empty;
            foreach (string tag in tags)
            {
                filterBuilder.AddCriterion($"substringof('{tag}', Tags) eq true");
            }

            if (type != ResourceType.None) {
                filterBuilder.AddCriterion(GetTypeFilterForRequest(type));
            }
            var requestUrlV2 = $"{Repository.Uri}/Search()?{queryBuilder.BuildQueryString()}";

            return HttpRequestCall(requestUrlV2, out errRecord);
        }

        /// <summary>
        /// Helper method for string[] FindVersionGlobbing()
        /// </summary>
        private string FindVersionGlobbing(string packageName, VersionRange versionRange, bool includePrerelease, ResourceType type, int skip, bool getOnlyLatest, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::FindVersionGlobbing()");
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

            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string> {
                {"$inlinecount", "allpages"},
                {"$skip", skip.ToString()},
                {"$orderby", "NormalizedVersion desc"},
                {"id", $"'{packageName}'"}
            });

            var filterBuilder = queryBuilder.FilterBuilder;

            if (versionRange.MinVersion != null)
            {
                string operation = versionRange.IsMinInclusive ? "ge" : "gt";
                minPart = String.Format(format, operation, $"'{versionRange.MinVersion.ToNormalizedString()}'");
            }

            if (versionRange.MaxVersion != null)
            {
                string operation = versionRange.IsMaxInclusive ? "le" : "lt";
                // Adding '9' as a digit to the end of the patch portion of the version
                // because we want to retrieve all the prerelease versions for the upper end of the range
                // and PSGallery views prerelease as higher than its stable.
                // eg 3.0.0-prerelease > 3.0.0
                // If looking for versions within '[1.9.9,1.9.9]' including prerelease values, this will change it to search for '[1.9.9,1.9.99]'
                // and find any pkg versions that are 1.9.9-prerelease.
                string maxString = includePrerelease ? $"{versionRange.MaxVersion.Major}.{versionRange.MaxVersion.Minor}.{versionRange.MaxVersion.Patch.ToString() + "9"}" :
                                 $"{versionRange.MaxVersion.ToNormalizedString()}";
                if (NuGetVersion.TryParse(maxString, out NuGetVersion maxVersion))
                {
                    maxPart = String.Format(format, operation, $"'{maxVersion.ToNormalizedString()}'");
                }
                else {
                    maxPart = String.Format(format, operation, $"'{versionRange.MaxVersion.ToNormalizedString()}'");
                }
            }

            string versionFilterParts = String.Empty;
            if (!String.IsNullOrEmpty(minPart))
            {
                filterBuilder.AddCriterion(minPart);
            }
            if (!String.IsNullOrEmpty(maxPart))
            {
                filterBuilder.AddCriterion(maxPart);
            }
            if (!includePrerelease) {
                filterBuilder.AddCriterion("IsPrerelease eq false");
            }
            
            // We need to explicitly add 'Id eq <packageName>' whenever $filter is used, otherwise arbitrary results are returned.

            // If it's a JFrog repository do not include the Id filter portion since JFrog uses 'Title' instead of 'Id',
            // however filtering on 'and Title eq '<packageName>' returns "Response status code does not indicate success: 500".
            if (!_isJFrogRepo) {
                filterBuilder.AddCriterion($"Id eq '{packageName}'");
            }

            if (type == ResourceType.Script) {
                filterBuilder.AddCriterion($"substringof('PS{type.ToString()}', Tags) eq true");
            }
            

            var requestUrlV2 = $"{Repository.Uri}/FindPackagesById()?{queryBuilder.BuildQueryString()}";

            return HttpRequestCall(requestUrlV2, out errRecord);
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
            _cmdletPassedIn.WriteDebug("In V2ServerAPICalls::InstallVersion()");
            string requestUrlV2;

            if (_isADORepo)
            {
                // eg: https://pkgs.dev.azure.com/<org>/<project>/_packaging/<feed>/nuget/v2?id=test_module&version=5.0.0
                requestUrlV2 = $"{Repository.Uri}?id={packageName}&version={version}";
            }
            else if (_isJFrogRepo)
            {
                // eg: https://<project>.jfrog.io/artifactory/api/nuget/<feed>/Download/test_module/5.0.0
                requestUrlV2 = $"{Repository.Uri}/Download/{packageName}/{version}";
            }
            else
            {
                requestUrlV2 = $"{Repository.Uri}/package/{packageName}/{version}";
            }

            var response = HttpRequestCallForContent(requestUrlV2, out errRecord);

            if (errRecord != null)
            {
                return new MemoryStream();
            }

            if (response is null)
            {
                errRecord = new ErrorRecord(
                    new Exception($"No content was returned by repository '{Repository.Name}'"),
                    "InstallFailureContentNullv2",
                    ErrorCategory.InvalidResult,
                    this);

                return null;
            }

            return response.ReadAsStreamAsync().Result;
        }

        private string GetTypeFilterForRequest(ResourceType type) {
            string typeFilterPart = string.Empty;
            if (type == ResourceType.Script)
            {
                typeFilterPart += $"substringof('PS{type.ToString()}', Tags) eq true";
            }
            else if (type == ResourceType.Module)
            {
                typeFilterPart += $"substringof('PS{ResourceType.Script.ToString()}', Tags) eq false";
            }

            return typeFilterPart;
        }

        /// <summary>
        /// Helper method that makes gets 'count' property from http response string.
        /// The count property is used to determine the number of total results found (for pagination).
        /// </summary>
        public int GetCountFromResponse(string httpResponse, out ErrorRecord errRecord)
        {
            errRecord = null;
            int count = 0;

            //Create the XmlDocument.
            XmlDocument doc = new XmlDocument();

            try
            {
                doc.LoadXml(httpResponse);

                bool countSearchSucceeded = false;
                XmlNodeList elemList = doc.GetElementsByTagName("m:count");
                if (elemList.Count > 0)
                {
                    countSearchSucceeded = true;
                    XmlNode node = elemList[0];
                    if (node == null || String.IsNullOrWhiteSpace(node.InnerText))
                    {
                        countSearchSucceeded = false;
                        errRecord = new ErrorRecord(
                            new PSArgumentException("Count property from server response was empty, invalid or not present."),
                            "GetCountFromResponseFailure",
                            ErrorCategory.InvalidData,
                            this);
                    }
                    else
                    {
                        countSearchSucceeded = int.TryParse(node.InnerText, out count);
                    }
                }

                if (!countSearchSucceeded)
                {
                             // Note: not all V2 servers may have the 'count' property implemented or valid (i.e CloudSmith server), in this case try to get 'd:Id' property.
                    elemList = doc.GetElementsByTagName("d:Id");
                    if (elemList.Count > 0)
                    {
                        count = elemList.Count;
                        errRecord = null;
                    }
                    else
                    {
                        _cmdletPassedIn.WriteDebug($"Property 'count' and 'd:Id' could not be found in response. This may indicate that the package could not be found");
                    }
                }
            }
            catch (XmlException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "GetCountFromResponse",
                    ErrorCategory.InvalidData,
                    this);
            }

            return count;
        }

        /// <summary>
        /// Helper method called by HttpRequestCall() that makes the HTTP request for string response.
        /// </summary>
        public static async Task<string> SendV2RequestAsync(HttpRequestMessage message, HttpClient s_client)
        {
            HttpStatusCode responseStatusCode = HttpStatusCode.OK;
            try
            {
                HttpResponseMessage response = await s_client.SendAsync(message);
                responseStatusCode = response.StatusCode;
                response.EnsureSuccessStatusCode();

                return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            catch (HttpRequestException e)
            {
                if (responseStatusCode.Equals(HttpStatusCode.NotFound))
                {
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
        /// Helper method called by HttpRequestCallForContent() that makes the HTTP request for HTTP Content response.
        /// </summary>
        public static async Task<HttpContent> SendV2RequestForContentAsync(HttpRequestMessage message, HttpClient s_client)
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
