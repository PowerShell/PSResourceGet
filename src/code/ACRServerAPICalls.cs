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
using System.Net;
using System.Management.Automation;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Net.Http.Headers;
using System.Linq;
using Microsoft.PowerShell.PSResourceGet.Cmdlets;
using System.Text;
using System.Security.Cryptography;

namespace Microsoft.PowerShell.PSResourceGet
{
    internal class ACRServerAPICalls : ServerApiCall
    {
        // Any interface method that is not implemented here should be processed in the parent method and then call one of the implemented
        // methods below.
        #region Members

        public override PSRepositoryInfo Repository { get; set; }
        private readonly PSCmdlet _cmdletPassedIn;
        private HttpClient _sessionClient { get; set; }
        private static readonly Hashtable[] emptyHashResponses = new Hashtable[] { };
        public FindResponseType v3FindResponseType = FindResponseType.ResponseString;

        const string acrRefreshTokenTemplate = "grant_type=access_token&service={0}&tenant={1}&access_token={2}"; // 0 - registry, 1 - tenant, 2 - access token
        const string acrAccessTokenTemplate = "grant_type=refresh_token&service={0}&scope=repository:*:*&refresh_token={1}"; // 0 - registry, 1 - refresh token
        const string acrOAuthExchangeUrlTemplate = "https://{0}/oauth2/exchange"; // 0 - registry
        const string acrOAuthTokenUrlTemplate = "https://{0}/oauth2/token"; // 0 - registry
        const string acrManifestUrlTemplate = "https://{0}/v2/{1}/manifests/{2}"; // 0 - registry, 1 - repo(modulename), 2 - tag(version)
        const string acrBlobDownloadUrlTemplate = "https://{0}/v2/{1}/blobs/{2}"; // 0 - registry, 1 - repo(modulename), 2 - layer digest
        const string acrFindImageVersionUrlTemplate = "https://{0}/acr/v1/{1}/_tags{2}"; // 0 - registry, 1 - repo(modulename), 2 - /tag(version)
        const string acrStartUploadTemplate = "https://{0}/v2/{1}/blobs/uploads/"; // 0 - registry, 1 - packagename
        const string acrEndUploadTemplate = "https://{0}{1}&digest=sha256:{2}"; // 0 - registry, 1 - location, 2 - digest

        private static readonly HttpClient s_client = new HttpClient();

        #endregion

        #region Constructor

        public ACRServerAPICalls(PSRepositoryInfo repository, PSCmdlet cmdletPassedIn, NetworkCredential networkCredential, string userAgentString) : base(repository, networkCredential)
        {
            Repository = repository;
            _cmdletPassedIn = cmdletPassedIn;
            HttpClientHandler handler = new HttpClientHandler()
            {
                Credentials = networkCredential
            };

            _sessionClient = new HttpClient(handler);
            _sessionClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgentString);
            var repoURL = repository.Uri.ToString().ToLower();
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
            _cmdletPassedIn.WriteDebug("In ACRServerAPICalls::FindAll()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find all is not supported for the ACR server protocol repository '{Repository.Name}'"),
                "FindAllFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag from a repository and returns latest version for each.
        /// Examples: Search -Tag "JSON" -Repository PSGallery
        /// API call:
        /// - Include prerelease: https://www.powershellgallery.com/api/v2/Search()?includePrerelease=true&$filter=IsAbsoluteLatestVersion and substringof('PSModule', Tags) eq true and substringof('CrescendoBuilt', Tags) eq true&$orderby=Id desc&$inlinecount=allpages&$skip=0&$top=6000
        /// </summary>
        public override FindResults FindTags(string[] tags, bool includePrerelease, ResourceType _type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ACRServerAPICalls::FindTags()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find tags is not supported for the ACR server protocol repository '{Repository.Name}'"),
                "FindTagsFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for all packages that have specified Command or DSCResource name.
        /// </summary>
        public override FindResults FindCommandOrDscResource(string[] tags, bool includePrerelease, bool isSearchingForCommands, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ACRServerAPICalls::FindCommandOrDscResource()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find Command or DSC Resource is not supported for the ACR server protocol repository '{Repository.Name}'"),
                "FindCommandOrDscResourceFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
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
            _cmdletPassedIn.WriteDebug("In ACRServerAPICalls::FindName()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find name is not supported for the ACR server protocol repository '{Repository.Name}'"),
                "FindNameFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name and tag and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "PowerShellGet" -Tag "Provider"
        /// Implementation Note: Need to filter further for latest version (prerelease or non-prerelease dependening on user preference)
        /// </summary>
        public override FindResults FindNameWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ACRServerAPICalls::FindNameWithTag()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find name with tag(s) is not supported for the ACR server protocol repository '{Repository.Name}'"),
                "FindNameWithTagFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);

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
            _cmdletPassedIn.WriteDebug("In ACRServerAPICalls::FindNameGlobbing()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"FindNameGlobbing all is not supported for the ACR server protocol repository '{Repository.Name}'"),
                "FindNameGlobbingFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and tag and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "PowerShell*" -Tag "Provider"
        /// Implementation Note: filter additionally and verify ONLY package name was a match.
        /// </summary>
        public override FindResults FindNameGlobbingWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ACRServerAPICalls::FindNameGlobbingWithTag()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find name globbing with tag(s) is not supported for the ACR server protocol repository '{Repository.Name}'"),
                "FindNameGlobbingWithTagFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
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
            _cmdletPassedIn.WriteDebug("In ACRServerAPICalls::FindVersionGlobbing()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find version globbing is not supported for the ACR server protocol repository '{Repository.Name}'"),
                "FindVersionGlobbingFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
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

            _cmdletPassedIn.WriteDebug("In ACRServerAPICalls::FindVersion()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find version is not supported for the ACR server protocol repository '{Repository.Name}'"),
                "FindVersionFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version and tag.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5" -Tag "Provider"
        /// </summary>
        public override FindResults FindVersionWithTag(string packageName, string version, string[] tags, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ACRServerAPICalls::FindVersionWithTag()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find version with tag(s) is not supported for the ACR server protocol repository '{Repository.Name}'"),
                "FindVersionWithTagFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /**  INSTALL APIS **/

        /// <summary>
        /// Installs a specific package.
        /// Name: no wildcard support.
        /// Examples: Install "PowerShellGet"
        ///           Install "PowerShellGet" -Version "3.0.0"
        /// </summary>
        public override Stream InstallPackage(string packageName, string packageVersion, bool includePrerelease, out ErrorRecord errRecord)
        {
            Stream results = new MemoryStream();
            errRecord = null;

            _cmdletPassedIn.WriteDebug("In ACRServerAPICalls::InstallPackage()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Install is not supported for the ACR server protocol repository '{Repository.Name}'"),
                "InstallFailure",
                ErrorCategory.InvalidOperation,
                this);

            return results;
        }

        /// <summary>
        /// Helper method that makes the HTTP request for the V2 server protocol url passed in for find APIs.
        /// </summary>
        private string HttpRequestCall(string requestUrlV2, out ErrorRecord errRecord)
        {
            string response = string.Empty;
            errRecord = null;

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

            return content;
        }


        internal static PSResourceInfo Install(
            PSRepositoryInfo repo,
            string moduleName,
            string moduleVersion,
            bool savePkg,
            bool asZip,
            List<string> installPath,
            PSCmdlet callingCmdlet)
        {
            string accessToken = string.Empty;
            string tenantID = string.Empty;
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            // Need to set up secret management vault before hand
            var repositoryCredentialInfo = repo.CredentialInfo;
            if (repositoryCredentialInfo != null)
            {
                accessToken = Utils.GetACRAccessTokenFromSecretManagement(
                    repo.Name,
                    repositoryCredentialInfo,
                    callingCmdlet);

                callingCmdlet.WriteVerbose("Access token retrieved.");

                tenantID = Utils.GetSecretInfoFromSecretManagement(
                    repo.Name,
                    repositoryCredentialInfo,
                    callingCmdlet);
            }

            // Call asynchronous network methods in a try/catch block to handle exceptions.
            string registry = repo.Uri.Host;

            callingCmdlet.WriteVerbose("Getting acr refresh token");
            var acrRefreshToken = GetAcrRefreshTokenAsync(registry, tenantID, accessToken).Result;
            callingCmdlet.WriteVerbose("Getting acr access token");
            var acrAccessToken = GetAcrAccessTokenAsync(registry, acrRefreshToken).Result;
            callingCmdlet.WriteVerbose($"Getting manifest for {moduleName} - {moduleVersion}");
            var manifest = GetAcrRepositoryManifestAsync(registry, moduleName, moduleVersion, acrAccessToken).Result;
            var digest = manifest["layers"].FirstOrDefault()["digest"].ToString();
            callingCmdlet.WriteVerbose($"Downloading blob for {moduleName} - {moduleVersion}");
            var responseContent = GetAcrBlobAsync(registry, moduleName, digest, acrAccessToken).Result;

            callingCmdlet.WriteVerbose($"Writing module zip to temp path: {tempPath}");

            // download the module
            var pathToFile = Path.Combine(tempPath, $"{moduleName}.{moduleVersion}.zip");
            using var content = responseContent.ReadAsStreamAsync().Result;
            using var fs = File.Create(pathToFile);
            content.Seek(0, SeekOrigin.Begin);
            content.CopyTo(fs);
            fs.Close();

            PSResourceInfo pkgInfo = null;
            /*
			var pkgInfo = new PSResourceInfo(
							additionalMetadata: new Hashtable { },
							author: string.Empty,
							companyName: string.Empty,
							copyright: string.Empty,
							dependencies: new Dependency[] { },
							description: string.Empty,
							iconUri: string.Empty,
							includes: new ResourceIncludes(),
							installedDate: null,
							installedLocation: null,
							isPrerelease: false,
							licenseUri: string.Empty,
							name: moduleName,
							powershellGetFormatVersion: null,
							prerelease: string.Empty,
							projectUri: string.Empty,
							publishedDate: null,
							releaseNotes: string.Empty,
							repository: string.Empty,
							repositorySourceLocation: repo.Name,
							tags: new string[] { },
							type: ResourceType.Module,
							updatedDate: null,
							version: moduleVersion);
			*/

            // If saving the package as a zip
            if (savePkg && asZip)
            {
                // Just move to the zip to the proper path
                Utils.MoveFiles(pathToFile, Path.Combine(installPath.FirstOrDefault(), $"{moduleName}.{moduleVersion}.zip"));

            }
            // If saving the package and unpacking OR installing the package
            else
            {
                string expandedPath = Path.Combine(tempPath, moduleName.ToLower(), moduleVersion);
                Directory.CreateDirectory(expandedPath);
                callingCmdlet.WriteVerbose($"Expanding module to temp path: {expandedPath}");
                // Expand the zip file
                System.IO.Compression.ZipFile.ExtractToDirectory(pathToFile, expandedPath);
                Utils.DeleteExtraneousFiles(callingCmdlet, moduleName, expandedPath);

                callingCmdlet.WriteVerbose("Expanding completed");
                File.Delete(pathToFile);

                Utils.MoveFilesIntoInstallPath(
                            pkgInfo,
                            isModule: true,
                            isLocalRepo: false,
                            savePkg,
                            moduleVersion,
                            tempPath,
                            installPath.FirstOrDefault(),
                            moduleVersion,
                            moduleVersion,
                            scriptPath: null,
                            callingCmdlet);

                if (Directory.Exists(tempPath))
                {
                    try
                    {
                        Utils.DeleteDirectory(tempPath);
                        callingCmdlet.WriteVerbose(string.Format("Successfully deleted '{0}'", tempPath));
                    }
                    catch (Exception e)
                    {
                        ErrorRecord TempDirCouldNotBeDeletedError = new ErrorRecord(e, "errorDeletingTempInstallPath", ErrorCategory.InvalidResult, null);
                        callingCmdlet.WriteError(TempDirCouldNotBeDeletedError);
                    }
                }
            }

            return pkgInfo;
        }


        #endregion

        internal static List<PSResourceInfo> Find(PSRepositoryInfo repo, string pkgName, string pkgVersion, PSCmdlet callingCmdlet)
        {
            List<PSResourceInfo> foundPkgs = new List<PSResourceInfo>();
            string accessToken = string.Empty;
            string tenantID = string.Empty;

            // Need to set up secret management vault before hand
            var repositoryCredentialInfo = repo.CredentialInfo;
            if (repositoryCredentialInfo != null)
            {
                accessToken = Utils.GetACRAccessTokenFromSecretManagement(
                    repo.Name,
                    repositoryCredentialInfo,
                    callingCmdlet);

                callingCmdlet.WriteVerbose("Access token retrieved.");

                tenantID = Utils.GetSecretInfoFromSecretManagement(
                    repo.Name,
                    repositoryCredentialInfo,
                    callingCmdlet);
            }

            // Call asynchronous network methods in a try/catch block to handle exceptions.
            string registry = repo.Uri.Host;

            callingCmdlet.WriteVerbose("Getting acr refresh token");
            var acrRefreshToken = GetAcrRefreshTokenAsync(registry, tenantID, accessToken).Result;
            callingCmdlet.WriteVerbose("Getting acr access token");
            var acrAccessToken = GetAcrAccessTokenAsync(registry, acrRefreshToken).Result;

            callingCmdlet.WriteVerbose("Getting tags");
            var foundTags = FindAcrImageTags(registry, pkgName, pkgVersion, acrAccessToken).Result;

            if (foundTags != null)
            {
                if (string.Equals(pkgVersion, "*", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var item in foundTags["tags"])
                    {
                        // digest: {item["digest"]";
                        string tagVersion = item["name"].ToString();

                        /*
						foundPkgs.Add(new PSResourceInfo(name: pkgName, version: tagVersion, repository: repo.Name));
						*/
                    }
                }
                else
                {
                    // pkgVersion was used in the API call (same as foundTags["name"])
                    // digest: foundTags["tag"]["digest"]";
                    /*
					foundPkgs.Add(new PSResourceInfo(name: pkgName, version: pkgVersion, repository: repo.Name));
					*/
                }
            }

            return foundPkgs;
        }

        #region Private Methods
        internal static async Task<string> GetAcrRefreshTokenAsync(string registry, string tenant, string accessToken)
        {
            string content = string.Format(acrRefreshTokenTemplate, registry, tenant, accessToken);
            var contentHeaders = new Collection<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Content-Type", "application/x-www-form-urlencoded") };
            string exchangeUrl = string.Format(acrOAuthExchangeUrlTemplate, registry);
            return (await GetHttpResponseJObject(exchangeUrl, HttpMethod.Post, content, contentHeaders))["refresh_token"].ToString();
        }

        internal static async Task<string> GetAcrAccessTokenAsync(string registry, string refreshToken)
        {
            string content = string.Format(acrAccessTokenTemplate, registry, refreshToken);
            var contentHeaders = new Collection<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Content-Type", "application/x-www-form-urlencoded") };
            string tokenUrl = string.Format(acrOAuthTokenUrlTemplate, registry);
            return (await GetHttpResponseJObject(tokenUrl, HttpMethod.Post, content, contentHeaders))["access_token"].ToString();
        }

        internal static async Task<JObject> GetAcrRepositoryManifestAsync(string registry, string repositoryName, string version, string acrAccessToken)
        {
            string manifestUrl = string.Format(acrManifestUrlTemplate, registry, repositoryName, version);
            var defaultHeaders = GetDefaultHeaders(acrAccessToken);
            return await GetHttpResponseJObject(manifestUrl, HttpMethod.Get, defaultHeaders);
        }

        internal static async Task<HttpContent> GetAcrBlobAsync(string registry, string repositoryName, string digest, string acrAccessToken)
        {
            string blobUrl = string.Format(acrBlobDownloadUrlTemplate, registry, repositoryName, digest);
            var defaultHeaders = GetDefaultHeaders(acrAccessToken);
            return await GetHttpContentResponseJObject(blobUrl, defaultHeaders);
        }

        internal static async Task<JObject> FindAcrImageTags(string registry, string repositoryName, string version, string acrAccessToken)
        {
            try
            {
                string resolvedVersion = string.Equals(version, "*", StringComparison.OrdinalIgnoreCase) ? null : $"/{version}";
                string findImageUrl = string.Format(acrFindImageVersionUrlTemplate, registry, repositoryName, resolvedVersion);
                var defaultHeaders = GetDefaultHeaders(acrAccessToken);
                return await GetHttpResponseJObject(findImageUrl, HttpMethod.Get, defaultHeaders);
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error finding ACR artifact: " + e.Message);
            }
        }

        internal static async Task<string> GetStartUploadBlobLocation(string registry, string pkgName, string acrAccessToken)
        {
            try
            {
                var defaultHeaders = GetDefaultHeaders(acrAccessToken);
                var startUploadUrl = string.Format(acrStartUploadTemplate, registry, pkgName);
                return (await GetHttpResponseHeader(startUploadUrl, HttpMethod.Post, defaultHeaders)).Location.ToString();
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error starting publishing to ACR: " + e.Message);
            }
        }

        internal static async Task<bool> EndUploadBlob(string registry, string location, string filePath, string digest, bool isManifest, string acrAccessToken)
        {
            try
            {
                var endUploadUrl = string.Format(acrEndUploadTemplate, registry, location, digest);
                var defaultHeaders = GetDefaultHeaders(acrAccessToken);
                return await PutRequestAsync(endUploadUrl, filePath, isManifest, defaultHeaders);
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to uploading module to ACR: " + e.Message);
            }
        }

        internal static async Task<bool> CreateManifest(string registry, string pkgName, string pkgVersion, string configPath, bool isManifest, string acrAccessToken)
        {
            try
            {
                var createManifestUrl = string.Format(acrManifestUrlTemplate, registry, pkgName, pkgVersion);
                var defaultHeaders = GetDefaultHeaders(acrAccessToken);
                return await PutRequestAsync(createManifestUrl, configPath, isManifest, defaultHeaders);
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to create manifest: " + e.Message);
            }
        }

        internal static async Task<HttpContent> GetHttpContentResponseJObject(string url, Collection<KeyValuePair<string, string>> defaultHeaders)
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                SetDefaultHeaders(defaultHeaders);
                return await SendContentRequestAsync(request);
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to retrieve response: " + e.Message);
            }
        }

        internal static async Task<JObject> GetHttpResponseJObject(string url, HttpMethod method, Collection<KeyValuePair<string, string>> defaultHeaders)
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(method, url);
                SetDefaultHeaders(defaultHeaders);
                return await SendRequestAsync(request);
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to retrieve response: " + e.Message);
            }
        }

        internal static async Task<JObject> GetHttpResponseJObject(string url, HttpMethod method, string content, Collection<KeyValuePair<string, string>> contentHeaders)
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(method, url);

                if (string.IsNullOrEmpty(content))
                {
                    throw new ArgumentNullException("content");
                }

                request.Content = new StringContent(content);
                request.Content.Headers.Clear();
                if (contentHeaders != null)
                {
                    foreach (var header in contentHeaders)
                    {
                        request.Content.Headers.Add(header.Key, header.Value);
                    }
                }

                return await SendRequestAsync(request);
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to retrieve response: " + e.Message);
            }
        }

        internal static async Task<HttpResponseHeaders> GetHttpResponseHeader(string url, HttpMethod method, Collection<KeyValuePair<string, string>> defaultHeaders)
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(method, url);
                SetDefaultHeaders(defaultHeaders);
                return await SendRequestHeaderAsync(request);
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to retrieve response header: " + e.Message);
            }
        }

        private static void SetDefaultHeaders(Collection<KeyValuePair<string, string>> defaultHeaders)
        {
            s_client.DefaultRequestHeaders.Clear();
            if (defaultHeaders != null)
            {
                foreach (var header in defaultHeaders)
                {
                    if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        s_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", header.Value);
                    }
                    else if (string.Equals(header.Key, "Accept", StringComparison.OrdinalIgnoreCase))
                    {
                        s_client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(header.Value));
                    }
                    else
                    {
                        s_client.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }
            }
        }

        private static async Task<HttpContent> SendContentRequestAsync(HttpRequestMessage message)
        {
            try
            {
                HttpResponseMessage response = await s_client.SendAsync(message);
                response.EnsureSuccessStatusCode();
                return response.Content;
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to retrieve response: " + e.Message);
            }
        }

        private static async Task<JObject> SendRequestAsync(HttpRequestMessage message)
        {
            try
            {
                HttpResponseMessage response = await s_client.SendAsync(message);
                response.EnsureSuccessStatusCode();
                return JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync());
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to retrieve response: " + e.Message);
            }
        }

        private static async Task<HttpResponseHeaders> SendRequestHeaderAsync(HttpRequestMessage message)
        {
            try
            {
                HttpResponseMessage response = await s_client.SendAsync(message);
                response.EnsureSuccessStatusCode();
                return response.Headers;
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to retrieve response: " + e.Message);
            }
        }

        private static async Task<bool> PutRequestAsync(string url, string filePath, bool isManifest, Collection<KeyValuePair<string, string>> contentHeaders)
        {
            try
            {
                SetDefaultHeaders(contentHeaders);

                FileInfo fileInfo = new FileInfo(filePath);
                FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read);
                HttpContent httpContent = new StreamContent(fileStream);
                if (isManifest)
                {
                    httpContent.Headers.Add("Content-Type", "application/vnd.oci.image.manifest.v1+json");
                }
                else
                {
                    httpContent.Headers.Add("Content-Type", "application/octet-stream");
                }

                HttpResponseMessage response = await s_client.PutAsync(url, httpContent);
                response.EnsureSuccessStatusCode();
                fileStream.Close();
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to uploading module to ACR: " + e.Message);
            }

        }

        private static Collection<KeyValuePair<string, string>> GetDefaultHeaders(string acrAccessToken)
        {
            return new Collection<KeyValuePair<string, string>> {
                    new KeyValuePair<string, string>("Authorization", acrAccessToken),
                    new KeyValuePair<string, string>("Accept", "application/vnd.oci.image.manifest.v1+json")
                };
        }

        private bool PushNupkgACR(string outputNupkgDir, string pkgName, NuGetVersion pkgVersion, PSRepositoryInfo repository, out ErrorRecord error)
        {
            error = null;
            // Push the nupkg to the appropriate repository
            var fullNupkgFile = System.IO.Path.Combine(outputNupkgDir, pkgName + "." + pkgVersion.ToNormalizedString() + ".nupkg");

            string accessToken = string.Empty;
            string tenantID = string.Empty;

            // Need to set up secret management vault before hand
            var repositoryCredentialInfo = repository.CredentialInfo;
            if (repositoryCredentialInfo != null)
            {
                accessToken = Utils.GetACRAccessTokenFromSecretManagement(
                    repository.Name,
                    repositoryCredentialInfo,
                    _cmdletPassedIn);

                _cmdletPassedIn.WriteVerbose("Access token retrieved.");

                tenantID = Utils.GetSecretInfoFromSecretManagement(
                    repository.Name,
                    repositoryCredentialInfo,
                    _cmdletPassedIn);
            }

            // Call asynchronous network methods in a try/catch block to handle exceptions.
            string registry = repository.Uri.Host;

            _cmdletPassedIn.WriteVerbose("Getting acr refresh token");
            var acrRefreshToken = GetAcrRefreshTokenAsync(registry, tenantID, accessToken).Result;
            _cmdletPassedIn.WriteVerbose("Getting acr access token");
            var acrAccessToken = GetAcrAccessTokenAsync(registry, acrRefreshToken).Result;

            _cmdletPassedIn.WriteVerbose("Start uploading blob");
            var moduleLocation = GetStartUploadBlobLocation(registry, pkgName, acrAccessToken).Result;

            _cmdletPassedIn.WriteVerbose("Computing digest for .nupkg file");
            bool digestCreated = CreateDigest(fullNupkgFile, out string digest, out ErrorRecord digestError);
            if (!digestCreated)
            {
                _cmdletPassedIn.ThrowTerminatingError(digestError);
            }

            _cmdletPassedIn.WriteVerbose("Finish uploading blob");
            bool moduleUploadSuccess = EndUploadBlob(registry, moduleLocation, fullNupkgFile, digest, false, acrAccessToken).Result;

            _cmdletPassedIn.WriteVerbose("Create an empty file");
            string emptyFileName = "empty.txt";
            var emptyFilePath = System.IO.Path.Combine(outputNupkgDir, emptyFileName);
            // Rename the empty file in case such a file already exists in the temp folder (although highly unlikely)
            while (File.Exists(emptyFilePath))
            {
                emptyFilePath = Guid.NewGuid().ToString() + ".txt";
            }
            FileStream emptyStream = File.Create(emptyFilePath);
            emptyStream.Close();

            _cmdletPassedIn.WriteVerbose("Start uploading an empty file");
            var emptyLocation = GetStartUploadBlobLocation(registry, pkgName, acrAccessToken).Result;

            _cmdletPassedIn.WriteVerbose("Computing digest for empty file");
            bool emptyDigestCreated = CreateDigest(emptyFilePath, out string emptyDigest, out ErrorRecord emptyDigestError);
            if (!emptyDigestCreated)
            {
                _cmdletPassedIn.ThrowTerminatingError(emptyDigestError);
            }

            _cmdletPassedIn.WriteVerbose("Finish uploading empty file");
            bool emptyFileUploadSuccess = EndUploadBlob(registry, emptyLocation, emptyFilePath, emptyDigest, false, acrAccessToken).Result;

            _cmdletPassedIn.WriteVerbose("Create the config file");
            string configFileName = "config.json";
            var configFilePath = System.IO.Path.Combine(outputNupkgDir, configFileName);
            while (File.Exists(configFilePath))
            {
                configFilePath = Guid.NewGuid().ToString() + ".json";
            }
            FileStream configStream = File.Create(configFilePath);
            configStream.Close();

            FileInfo nupkgFile = new FileInfo(fullNupkgFile);
            var fileSize = nupkgFile.Length;
            var fileName = System.IO.Path.GetFileName(fullNupkgFile);
            string fileContent = CreateJsonContent(digest, emptyDigest, fileSize, fileName);
            File.WriteAllText(configFilePath, fileContent);

            _cmdletPassedIn.WriteVerbose("Create the manifest layer");
            bool manifestCreated = CreateManifest(registry, pkgName, pkgVersion.OriginalVersion, configFilePath, true, acrAccessToken).Result;

            if (manifestCreated)
            {
                return true;
            }
            return false;
        }

        private string CreateJsonContent(string digest, string emptyDigest, long fileSize, string fileName)
        {
            StringBuilder stringBuilder = new StringBuilder();
            StringWriter stringWriter = new StringWriter(stringBuilder);
            JsonTextWriter jsonWriter = new JsonTextWriter(stringWriter);

            jsonWriter.Formatting = Newtonsoft.Json.Formatting.Indented;

            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("schemaVersion");
            jsonWriter.WriteValue(2);

            jsonWriter.WritePropertyName("config");
            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("mediaType");
            jsonWriter.WriteValue("application/vnd.unknown.config.v1+json");
            jsonWriter.WritePropertyName("digest");
            jsonWriter.WriteValue($"sha256:{emptyDigest}");
            jsonWriter.WritePropertyName("size");
            jsonWriter.WriteValue(0);
            jsonWriter.WriteEndObject();

            jsonWriter.WritePropertyName("layers");
            jsonWriter.WriteStartArray();
            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("mediaType");
            jsonWriter.WriteValue("application/vnd.oci.image.layer.nondistributable.v1.tar+gzip'");
            jsonWriter.WritePropertyName("digest");
            jsonWriter.WriteValue($"sha256:{digest}");
            jsonWriter.WritePropertyName("size");
            jsonWriter.WriteValue(fileSize);
            jsonWriter.WritePropertyName("annotations");

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("org.opencontainers.image.title");
            jsonWriter.WriteValue(fileName);
            jsonWriter.WriteEndObject();

            jsonWriter.WriteEndObject();
            jsonWriter.WriteEndArray();

            jsonWriter.WriteEndObject();

            return stringWriter.ToString();
        }



        // ACR method
        private bool CreateDigest(string fileName, out string digest, out ErrorRecord error)
        {
            FileInfo fileInfo = new FileInfo(fileName);
            SHA256 mySHA256 = SHA256.Create();
            FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read);
            digest = string.Empty;

            try
            {
                // Create a fileStream for the file.
                // Be sure it's positioned to the beginning of the stream.
                fileStream.Position = 0;
                // Compute the hash of the fileStream.
                byte[] hashValue = mySHA256.ComputeHash(fileStream);
                StringBuilder stringBuilder = new StringBuilder();
                foreach (byte b in hashValue)
                    stringBuilder.AppendFormat("{0:x2}", b);
                digest = stringBuilder.ToString();
                // Write the name and hash value of the file to the console.
                _cmdletPassedIn.WriteVerbose($"{fileInfo.Name}: {hashValue}");
                error = null;
            }
            catch (IOException ex)
            {
                var IOError = new ErrorRecord(ex, $"IOException for .nupkg file: {ex.Message}", ErrorCategory.InvalidOperation, null);
                error = IOError;
            }
            catch (UnauthorizedAccessException ex)
            {
                var AuthorizationError = new ErrorRecord(ex, $"UnauthorizedAccessException for .nupkg file: {ex.Message}", ErrorCategory.PermissionDenied, null);
                error = AuthorizationError;
            }

            fileStream.Close();
            if (error != null)
            {
                return false;
            }
            return true;
        }

        #endregion
    }
}
