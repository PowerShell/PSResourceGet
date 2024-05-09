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
using System.Runtime.ExceptionServices;
using System.Management.Automation;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    internal class NuGetServerAPICalls : ServerApiCall
    {
        #region Members

        public override PSRepositoryInfo Repository { get; set; }
        private readonly PSCmdlet _cmdletPassedIn;
        private HttpClient _sessionClient { get; set; }
        private static readonly Hashtable[] emptyHashResponses = new Hashtable[]{};
        public FindResponseType FindResponseType = FindResponseType.ResponseString;

        #endregion

        #region Constructor

        public NuGetServerAPICalls (PSRepositoryInfo repository, PSCmdlet cmdletPassedIn, NetworkCredential networkCredential, string userAgentString) : base (repository, networkCredential)
        {
            this.Repository = repository;
            _cmdletPassedIn = cmdletPassedIn;
            HttpClientHandler handler = new HttpClientHandler()
            {
                Credentials = networkCredential
            };

            _sessionClient = new HttpClient(handler);
            _sessionClient.Timeout = TimeSpan.FromMinutes(10);
            _sessionClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgentString);
        }

        #endregion

        #region Overriden Methods

        /// <summary>
        /// Find method which allows for searching for all packages from a repository and returns latest version for each.
        /// Examples: Search -Repository MyNuGetServer
        /// API call:
        /// - No prerelease: {repoUri}/api/v2/Search()?$filter=IsLatestVersion
        /// </summary>
        public override FindResults FindAll(bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::FindAll()");
            errRecord = null;
            List<string> responses = new List<string>();

            int skip = 0;
            string initialResponse = FindAllFromEndPoint(includePrerelease, skip, out errRecord);
            if (errRecord != null)
            {
                _cmdletPassedIn.WriteDebug($"Error: {errRecord.Exception.Message}");
                return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
            }

            responses.Add(initialResponse);
            int initalCount = GetCountFromResponse(initialResponse, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
            }

            int count = initalCount / 6000;
            // if more than 100 count, loop and add response to list
            while (count > 0)
            {
                skip += 6000;
                var tmpResponse = FindAllFromEndPoint(includePrerelease, skip, out errRecord);
                if (errRecord != null)
                {
                    return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
                }

                responses.Add(tmpResponse);
                count--;
            }

            return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag from a repository and returns latest version for each.
        /// Examples: Search -Tag "JSON" -Repository PSGallery
        /// API call:
        /// - Include prerelease: {repoUri}/api/v2/Search()?$filter=IsAbsoluteLatestVersion&searchTerm=tag:JSON&includePrerelease=true
        /// </summary>
        public override FindResults FindTags(string[] tags, bool includePrerelease, ResourceType _type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::FindTags()");
            errRecord = null;
            List<string> responses = new List<string>();

            int skip = 0;
            string initialResponse = FindTagFromEndpoint(tags, includePrerelease, skip, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
            }

            responses.Add(initialResponse);
            int initalCount = GetCountFromResponse(initialResponse, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
            }

            int count = initalCount / 100;
            // if more than 100 count, loop and add response to list
            while (count > 0)
            {
                // skip 100
                skip += 100;
                var tmpResponse = FindTagFromEndpoint(tags, includePrerelease, skip, out errRecord);
                if (errRecord != null)
                {
                    return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
                }

                responses.Add(tmpResponse);
                count--;
            }

            return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for all packages that have specified Command or DSCResource name.
        /// </summary>
        public override FindResults FindCommandOrDscResource(string[] tags, bool includePrerelease, bool isSearchingForCommands, out ErrorRecord errRecord)
        {
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find by CommandName or DSCResource is not supported for the repository '{Repository.Name}'"),
                "FindCommandOrDscResourceFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "PowerShellGet"
        /// API call:
        /// - No prerelease: {repoUri}/api/v2/FindPackagesById()?id='PowerShellGet'
        /// - Include prerelease: {repoUri}/api/v2/FindPackagesById()?id='PowerShellGet'
        /// Implementation Note: Need to filter further for latest version (prerelease or non-prerelease dependening on user preference)
        /// </summary>
        public override FindResults FindName(string packageName, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::FindName()");
            // This should return the latest stable version or the latest prerelease version (respectively)
            // https://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'&$filter=IsLatestVersion and substringof('PSModule', Tags) eq true

            // Make sure to include quotations around the package name
            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string>{
                { "id", $"'{packageName}'" },
            });
            var filterBuilder = queryBuilder.FilterBuilder;

            filterBuilder.AddCriterion(includePrerelease ? "IsAbsoluteLatestVersion" : "IsLatestVersion");

            // We need to explicitly add 'Id eq <packageName>' whenever $filter is used, otherwise arbitrary results are returned.
            filterBuilder.AddCriterion($"Id eq '{packageName}'");
            var requestUrl = $"{Repository.Uri}/FindPackagesById()?{queryBuilder.BuildQueryString()}";
            string response = HttpRequestCall(requestUrl, out errRecord);

            return new FindResults(stringResponse: new string[]{ response }, hashtableResponse: emptyHashResponses, responseType: FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name and tag and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "PowerShellGet" -Tag "Provider"
        /// Implementation Note: Need to filter further for latest version (prerelease or non-prerelease dependening on user preference)
        /// </summary>
        public override FindResults FindNameWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::FindNameWithTag()");

            // This should return the latest stable version or the latest prerelease version (respectively)
            // https://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'&$filter=IsLatestVersion and substringof('PSModule', Tags) eq true
            
            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string>{
                { "id", $"'{packageName}'" },
            });
            var filterBuilder = queryBuilder.FilterBuilder;
            
            // We need to explicitly add 'Id eq <packageName>' whenever $filter is used, otherwise arbitrary results are returned.
            filterBuilder.AddCriterion($"Id eq '{packageName}'");

            filterBuilder.AddCriterion(includePrerelease ? "IsAbsoluteLatestVersion" : "IsLatestVersion");

            foreach (string tag in tags)
            {
                filterBuilder.AddCriterion($"substringof('{tag}', Tags) eq true");
            }

            var requestUrl = $"{Repository.Uri}/FindPackagesById()?{queryBuilder.BuildQueryString()}";
            string response = HttpRequestCall(requestUrl, out errRecord);

            return new FindResults(stringResponse: new string[] { response }, hashtableResponse: emptyHashResponses, responseType: FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "PowerShell*"
        /// API call:
        /// - No prerelease: {repoUri}/api/v2/Search()?$filter=IsLatestVersion&searchTerm='az*'
        /// Implementation Note: filter additionally and verify ONLY package name was a match.
        /// </summary>
        public override FindResults FindNameGlobbing(string packageName, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::FindNameGlobbing()");
            List<string> responses = new List<string>();
            int skip = 0;

            var initialResponse = FindNameGlobbing(packageName, includePrerelease, skip, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
            }

            responses.Add(initialResponse);

            // check count (regex)  425 ==> count/100  ~~>  4 calls
            int initalCount = GetCountFromResponse(initialResponse, out errRecord);  // count = 4
            if (errRecord != null)
            {
                return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
            }

            int count = initalCount / 100;
            // if more than 100 count, loop and add response to list
            while (count > 0)
            {
                // skip 100
                skip += 100;
                var tmpResponse = FindNameGlobbing(packageName, includePrerelease, skip, out errRecord);
                if (errRecord != null)
                {
                    return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
                }
                responses.Add(tmpResponse);
                count--;
            }

            return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and tag and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "PowerShell*" -Tag "Provider"
        /// Implementation Note: filter additionally and verify ONLY package name was a match.
        /// </summary>
        public override FindResults FindNameGlobbingWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::FindNameGlobbingWithTag()");
            List<string> responses = new List<string>();
            int skip = 0;

            var initialResponse = FindNameGlobbingWithTag(packageName, tags, includePrerelease, skip, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
            }

            responses.Add(initialResponse);

            // check count (regex)  425 ==> count/100  ~~>  4 calls
            int initalCount = GetCountFromResponse(initialResponse, out errRecord);  // count = 4
            if (errRecord != null)
            {
                return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
            }

            int count = initalCount / 100;
            // if more than 100 count, loop and add response to list
            while (count > 0)
            {
                // skip 100
                skip += 100;
                var tmpResponse = FindNameGlobbingWithTag(packageName, tags, includePrerelease, skip, out errRecord);
                if (errRecord != null)
                {
                    return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
                }
                responses.Add(tmpResponse);
                count--;
            }

            return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with version range.
        /// Name: no wildcard support
        /// Version: supports wildcards
        /// Examples: Search "PowerShellGet" "[3.0.0.0, 5.0.0.0]"
        ///           Search "PowerShellGet" "3.*"
        /// API Call: {repoUri}/api/v2/FindPackagesById()?id='PowerShellGet'
        /// Implementation note: Returns all versions, including prerelease ones. Later (in the API client side) we'll do filtering on the versions to satisfy what user provided.
        /// </summary>
        public override FindResults FindVersionGlobbing(string packageName, VersionRange versionRange, bool includePrerelease, ResourceType type, bool getOnlyLatest, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::FindVersionGlobbing()");
            List<string> responses = new List<string>();
            int skip = 0;

            var initialResponse = FindVersionGlobbing(packageName, versionRange, includePrerelease, skip, getOnlyLatest, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
            }

            responses.Add(initialResponse);

            if (!getOnlyLatest)
            {
                int initalCount = GetCountFromResponse(initialResponse, out errRecord);
                if (errRecord != null)
                {
                    return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
                }

                int count = initalCount / 100;

                while (count > 0)
                {
                    // skip 100
                    skip += 100;
                    var tmpResponse = FindVersionGlobbing(packageName, versionRange, includePrerelease, skip, getOnlyLatest, out errRecord);
                    if (errRecord != null)
                    {
                        return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
                    }

                    responses.Add(tmpResponse);
                    count--;
                }
            }

            return new FindResults(stringResponse: responses.ToArray(), hashtableResponse: emptyHashResponses, responseType: FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5"
        /// API call: {repoUri}/api/v2/Packages(Id='PowerShellGet', Version='2.2.5')
        /// </summary>
        public override FindResults FindVersion(string packageName, string version, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::FindVersion()");
            // https://www.powershellgallery.com/api/v2/FindPackagesById()?id='blah'&includePrerelease=false&$filter= NormalizedVersion eq '1.1.0' and substringof('PSModule', Tags) eq true
            // Quotations around package name and version do not matter, same metadata gets returned.

            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string>{
                { "id", $"'{packageName}'" },
            });
            var filterBuilder = queryBuilder.FilterBuilder;

            // We need to explicitly add 'Id eq <packageName>' whenever $filter is used, otherwise arbitrary results are returned.
            filterBuilder.AddCriterion($"Id eq '{packageName}'");
            filterBuilder.AddCriterion($"NormalizedVersion eq '{packageName}'");

            var requestUrl = $"{Repository.Uri}/FindPackagesById()?{queryBuilder.BuildQueryString()}";
            string response = HttpRequestCall(requestUrl, out errRecord);

            return new FindResults(stringResponse: new string[] { response }, hashtableResponse: emptyHashResponses, responseType: FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version and tag.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5" -Tag "Provider"
        /// </summary>
        public override FindResults FindVersionWithTag(string packageName, string version, string[] tags, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::FindVersionWithTag()");

            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string>{
                { "id", $"'{packageName}'" },
            });
            var filterBuilder = queryBuilder.FilterBuilder;

            // We need to explicitly add 'Id eq <packageName>' whenever $filter is used, otherwise arbitrary results are returned.
            filterBuilder.AddCriterion($"Id eq '{packageName}'");
            filterBuilder.AddCriterion($"NormalizedVersion eq '{packageName}'");

            foreach (string tag in tags)
            {
                filterBuilder.AddCriterion($"substringof('{tag}', Tags) eq true");
            }

            var requestUrl = $"{Repository.Uri}/FindPackagesById()?{queryBuilder.BuildQueryString()}";
            string response = HttpRequestCall(requestUrl, out errRecord);

            return new FindResults(stringResponse: new string[] { response }, hashtableResponse: emptyHashResponses, responseType: FindResponseType);
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
        /// Helper method that makes the HTTP request for the NuGet server protocol url passed in for find APIs.
        /// </summary>
        private string HttpRequestCall(string requestUrl, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::HttpRequestCall()");
            errRecord = null;
            string response = string.Empty;

            try
            {
                _cmdletPassedIn.WriteDebug($"Request url is: '{requestUrl}'");
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

                response = SendRequestAsync(request, _sessionClient).GetAwaiter().GetResult();
            }
            catch (HttpRequestException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "HttpRequestFallFailure",
                    ErrorCategory.ConnectionError,
                    this);
            }
            catch (ArgumentNullException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "HttpRequestFallFailure",
                    ErrorCategory.ConnectionError,
                    this);
            }
            catch (InvalidOperationException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "HttpRequestFallFailure",
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
        /// Helper method that makes the HTTP request for the NuGet server protocol url passed in for install APIs.
        /// </summary>
        private HttpContent HttpRequestCallForContent(string requestUrl, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::HttpRequestCallForContent()");
            errRecord = null;
            HttpContent content = null;

            try
            {
                _cmdletPassedIn.WriteDebug($"Request url is: '{requestUrl}'");
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

                content = SendRequestForContentAsync(request, _sessionClient).GetAwaiter().GetResult();
            }
            catch (HttpRequestException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "HttpRequestFailure",
                    ErrorCategory.ConnectionError ,
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

            if (string.IsNullOrEmpty(content.ToString()))
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
        private string FindAllFromEndPoint(bool includePrerelease, int skip, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::FindAllFromEndPoint()");

            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string> {
                { "$inlinecount", "allpages" },
                { "$skip", skip.ToString() },
                { "$top", "6000" },
                { "$orderBy", "Id desc" },
            });

            var filterBuilder = queryBuilder.FilterBuilder;

            if (includePrerelease) {
                queryBuilder.AdditionalParameters["includePrerelease"] = "true";
                filterBuilder.AddCriterion("IsAbsoluteLatestVersion");
            } else {
                filterBuilder.AddCriterion("IsLatestVersion");
            }
            
            var requestUrl = $"{Repository.Uri}/Search()?{queryBuilder.BuildQueryString()}";

            return HttpRequestCall(requestUrl, out errRecord);
        }

        /// <summary>
        /// Helper method for string[] FindTag(string, PSRepositoryInfo, bool, bool, ResourceType, out string)
        /// </summary>
        private string FindTagFromEndpoint(string[] tags, bool includePrerelease, int skip, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::FindTagFromEndpoint()");

            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string> {
                { "$inlinecount", "allpages" },
                { "$skip", skip.ToString() },
                { "$top", "6000" },
                { "$orderBy", "Id desc" },
            });

            var filterBuilder = queryBuilder.FilterBuilder;

            if (includePrerelease) {
                queryBuilder.AdditionalParameters["includePrerelease"] = "true";
                filterBuilder.AddCriterion("IsAbsoluteLatestVersion");
            } else {
                filterBuilder.AddCriterion("IsLatestVersion");
            }

            foreach (string tag in tags)
            {
                filterBuilder.AddCriterion($"substringof('{tag}', Tags) eq true");
            }

            var requestUrl = $"{Repository.Uri}/Search()?{queryBuilder.BuildQueryString()}";

            return HttpRequestCall(requestUrl, out errRecord);
        }

        /// <summary>
        /// Helper method for string[] FindNameGlobbing()
        /// </summary>
        private string FindNameGlobbing(string packageName, bool includePrerelease, int skip, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::FindNameGlobbing()");
            // https://www.powershellgallery.com/api/v2/Search()?$filter=endswith(Id, 'Get') and startswith(Id, 'PowerShell') and IsLatestVersion (stable)
            // https://www.powershellgallery.com/api/v2/Search()?$filter=endswith(Id, 'Get') and IsAbsoluteLatestVersion&includePrerelease=true

            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string> {
                { "$inlinecount", "allpages" },
                { "$skip", skip.ToString() },
                { "$top", "100" },
                { "$orderBy", "NormalizedVersion desc" },
            });

            var filterBuilder = queryBuilder.FilterBuilder;

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
                    new ArgumentException("-Name '*' for NuGet.Server hosted feed repository is not supported"),
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

            var requestUrl = $"{Repository.Uri}/Search()?{queryBuilder.BuildQueryString()}";

            return HttpRequestCall(requestUrl, out errRecord);
        }

        /// <summary>
        /// Helper method for string[] FindNameGlobbingWithTag()
        /// </summary>
        private string FindNameGlobbingWithTag(string packageName, string[] tags, bool includePrerelease, int skip, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::FindNameGlobbingWithTag()");
            // https://www.powershellgallery.com/api/v2/Search()?$filter=endswith(Id, 'Get') and startswith(Id, 'PowerShell') and IsLatestVersion (stable)
            // https://www.powershellgallery.com/api/v2/Search()?$filter=endswith(Id, 'Get') and IsAbsoluteLatestVersion&includePrerelease=true

            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string> {
                { "$inlinecount", "allpages" },
                { "$skip", skip.ToString() },
                { "$top", "100" },
                { "$orderBy", "Id desc" },
            });

            var filterBuilder = queryBuilder.FilterBuilder;

            if (includePrerelease) {
                filterBuilder.AddCriterion("IsAbsoluteLatestVersion");
                queryBuilder.AdditionalParameters["includePrerelease"] = "true";
            } else {
                filterBuilder.AddCriterion("IsLatestVersion");
            }

            var names = packageName.Split(new char[] {'*'}, StringSplitOptions.RemoveEmptyEntries);

            if (names.Length == 0)
            {
                errRecord = new ErrorRecord(
                    new ArgumentException("-Name '*' for NuGet.Server hosted feed repository  is not supported"),
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
                    "FindNameGlobbing",
                    ErrorCategory.InvalidArgument,
                    this);

                return string.Empty;
            }

            foreach (string tag in tags)
            {
                filterBuilder.AddCriterion($"substringof('{tag}', Tags) eq true");
            }

            var requestUrl = $"{Repository.Uri}/Search()?{queryBuilder.BuildQueryString()}";

            return HttpRequestCall(requestUrl, out errRecord);
        }

        /// <summary>
        /// Helper method for string[] FindVersionGlobbing()
        /// </summary>
        private string FindVersionGlobbing(string packageName, VersionRange versionRange, bool includePrerelease, int skip, bool getOnlyLatest, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::FindVersionGlobbing()");
            //https://www.powershellgallery.com/api/v2//FindPackagesById()?id='blah'&includePrerelease=false&$filter= NormalizedVersion gt '1.0.0' and NormalizedVersion lt '2.2.5' and substringof('PSModule', Tags) eq true
            //https://www.powershellgallery.com/api/v2//FindPackagesById()?id='PowerShellGet'&includePrerelease=false&$filter= NormalizedVersion gt '1.1.1' and NormalizedVersion lt '2.2.5'
            // NormalizedVersion doesn't include trailing zeroes
            // Notes: this could allow us to take a version range (i.e (2.0.0, 3.0.0.0]) and deconstruct it and add options to the Filter for Version to describe that range
            // will need to filter additionally, if IncludePrerelease=false, by default we get stable + prerelease both back
            // Current bug: Find PSGet -Version "2.0.*" -> https://www.powershellgallery.com/api/v2//FindPackagesById()?id='PowerShellGet'&includePrerelease=false&$filter= Version gt '2.0.*' and Version lt '2.1'
            // Make sure to include quotations around the package name

            var queryBuilder = new NuGetV2QueryBuilder(new Dictionary<string, string> {
                { "id", $"'{packageName}'" },
                { "$inlinecount", "allpages" },
                { "$skip", skip.ToString() },
                { "$top", getOnlyLatest ? "1" : "100" },
                { "$orderBy", "NormalizedVersion desc" },
            });

            var filterBuilder = queryBuilder.FilterBuilder;

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
            filterBuilder.AddCriterion($"Id eq '{packageName}'");

            var requestUrl = $"{Repository.Uri}/FindPackagesById()?{queryBuilder.BuildQueryString()}";

            return HttpRequestCall(requestUrl, out errRecord);
        }

        /// <summary>
        /// Installs specific package.
        /// Name: no wildcard support.
        /// Examples: Install "PowerShellGet"
        /// Implementation Note:   {repoUri}/Packages(Id='test_local_mod')/Download
        ///                        if prerelease, call into InstallVersion instead.
        /// </summary>
        private Stream InstallName(string packageName, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::InstallName()");
            var requestUrl = $"{Repository.Uri}/Packages/(Id='{packageName}')/Download";
            var response = HttpRequestCallForContent(requestUrl, out errRecord);

            if (response is null)
            {
                errRecord = new ErrorRecord(
                    new Exception($"No content was returned by repository '{Repository.Name}'"),
                    "InstallFailureContentNullNuGetServer",
                    ErrorCategory.InvalidResult,
                    this);

                return null;
            }

            return response.ReadAsStreamAsync().Result;
        }

        /// <summary>
        /// Installs package with specific name and version.
        /// Name: no wildcard support.
        /// Version: no wildcard support.
        /// Examples: Install "PowerShellGet" -Version "3.0.0.0"
        ///           Install "PowerShellGet" -Version "3.0.0-beta16"
        /// API Call: {repoUri}/Packages(Id='Castle.Core',Version='5.1.1')/Download
        /// </summary>
        private Stream InstallVersion(string packageName, string version, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In NuGetServerAPICalls::InstallVersion()");
            var requestUrl = $"{Repository.Uri}/Packages(Id='{packageName}',Version='{version}')/Download";
            var response = HttpRequestCallForContent(requestUrl, out errRecord);

            if (response is null)
            {
                errRecord = new ErrorRecord(
                    new Exception($"No content was returned by repository '{Repository.Name}'"),
                    "InstallFailureContentNullNuGetServer",
                    ErrorCategory.InvalidResult,
                    this);

                return null;
            }

            return response.ReadAsStreamAsync().Result;
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
            }
            catch (XmlException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "GetCountFromResponse",
                    ErrorCategory.InvalidData,
                    this);
            }
            if (errRecord != null)
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
        public static async Task<string> SendRequestAsync(HttpRequestMessage message, HttpClient s_client)
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
        public static async Task<HttpContent> SendRequestForContentAsync(HttpRequestMessage message, HttpClient s_client)
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
