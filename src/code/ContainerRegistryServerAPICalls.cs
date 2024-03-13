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
using System.Text.Json;

namespace Microsoft.PowerShell.PSResourceGet
{
    internal class ContainerRegistryServerAPICalls : ServerApiCall
    {
        // Any interface method that is not implemented here should be processed in the parent method and then call one of the implemented
        // methods below.
        #region Members

        public override PSRepositoryInfo Repository { get; set; }
        public String Registry { get; set; }
        private readonly PSCmdlet _cmdletPassedIn;
        private HttpClient _sessionClient { get; set; }
        private static readonly Hashtable[] emptyHashResponses = new Hashtable[] { };
        public FindResponseType containerRegistryFindResponseType = FindResponseType.ResponseString;

        const string containerRegistryRefreshTokenTemplate = "grant_type=access_token&service={0}&tenant={1}&access_token={2}"; // 0 - registry, 1 - tenant, 2 - access token
        const string containerRegistryAccessTokenTemplate = "grant_type=refresh_token&service={0}&scope=repository:*:*&refresh_token={1}"; // 0 - registry, 1 - refresh token
        const string containerRegistryOAuthExchangeUrlTemplate = "https://{0}/oauth2/exchange"; // 0 - registry
        const string containerRegistryOAuthTokenUrlTemplate = "https://{0}/oauth2/token"; // 0 - registry
        const string containerRegistryManifestUrlTemplate = "https://{0}/v2/{1}/manifests/{2}"; // 0 - registry, 1 - repo(modulename), 2 - tag(version)
        const string containerRegistryBlobDownloadUrlTemplate = "https://{0}/v2/{1}/blobs/{2}"; // 0 - registry, 1 - repo(modulename), 2 - layer digest
        const string containerRegistryFindImageVersionUrlTemplate = "https://{0}/acr/v1/{1}/_tags{2}"; // 0 - registry, 1 - repo(modulename), 2 - /tag(version)
        const string containerRegistryStartUploadTemplate = "https://{0}/v2/{1}/blobs/uploads/"; // 0 - registry, 1 - packagename
        const string containerRegistryEndUploadTemplate = "https://{0}{1}&digest=sha256:{2}"; // 0 - registry, 1 - location, 2 - digest

        private static readonly HttpClient s_client = new HttpClient();

        #endregion

        #region Constructor

        public ContainerRegistryServerAPICalls(PSRepositoryInfo repository, PSCmdlet cmdletPassedIn, NetworkCredential networkCredential, string userAgentString) : base(repository, networkCredential)
        {
            Repository = repository;
            Registry = Repository.Uri.Host;
            _cmdletPassedIn = cmdletPassedIn;
            HttpClientHandler handler = new HttpClientHandler()
            {
                Credentials = networkCredential
            };

            _sessionClient = new HttpClient(handler);
            _sessionClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgentString);
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
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindAll()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find all is not supported for the ContainerRegistry server protocol repository '{Repository.Name}'"),
                "FindAllFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: containerRegistryFindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag from a repository and returns latest version for each.
        /// Examples: Search -Tag "JSON" -Repository PSGallery
        /// API call:
        /// - Include prerelease: https://www.powershellgallery.com/api/v2/Search()?includePrerelease=true&$filter=IsAbsoluteLatestVersion and substringof('PSModule', Tags) eq true and substringof('CrescendoBuilt', Tags) eq true&$orderby=Id desc&$inlinecount=allpages&$skip=0&$top=6000
        /// </summary>
        public override FindResults FindTags(string[] tags, bool includePrerelease, ResourceType _type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindTags()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find tags is not supported for the ContainerRegistry server protocol repository '{Repository.Name}'"),
                "FindTagsFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: containerRegistryFindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for all packages that have specified Command or DSCResource name.
        /// </summary>
        public override FindResults FindCommandOrDscResource(string[] tags, bool includePrerelease, bool isSearchingForCommands, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindCommandOrDscResource()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find Command or DSC Resource is not supported for the ContainerRegistry server protocol repository '{Repository.Name}'"),
                "FindCommandOrDscResourceFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: containerRegistryFindResponseType);
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
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindName()");

            // for FindName(), need to consider all versions (hence VersionType.VersionRange and VersionRange.All, and no requiredVersion) but only pick latest (hence getOnlyLatest: true)
            Hashtable[] pkgResult = FindPackagesWithVersionHelper(packageName, VersionType.VersionRange, versionRange: VersionRange.All, requiredVersion: null, includePrerelease, getOnlyLatest: true, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: new string[] { }, hashtableResponse: emptyHashResponses, responseType: containerRegistryFindResponseType);
            }

            return new FindResults(stringResponse: new string[] { }, hashtableResponse: pkgResult.ToArray(), responseType: containerRegistryFindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name and tag and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "PowerShellGet" -Tag "Provider"
        /// Implementation Note: Need to filter further for latest version (prerelease or non-prerelease dependening on user preference)
        /// </summary>
        public override FindResults FindNameWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindNameWithTag()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find name with tag(s) is not supported for the ContainerRegistry server protocol repository '{Repository.Name}'"),
                "FindNameWithTagFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: containerRegistryFindResponseType);
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
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindNameGlobbing()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"FindNameGlobbing all is not supported for the ContainerRegistry server protocol repository '{Repository.Name}'"),
                "FindNameGlobbingFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: containerRegistryFindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and tag and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "PowerShell*" -Tag "Provider"
        /// Implementation Note: filter additionally and verify ONLY package name was a match.
        /// </summary>
        public override FindResults FindNameGlobbingWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindNameGlobbingWithTag()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find name globbing with tag(s) is not supported for the ContainerRegistry server protocol repository '{Repository.Name}'"),
                "FindNameGlobbingWithTagFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: containerRegistryFindResponseType);
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
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindVersionGlobbing()");

            // for FindVersionGlobbing(), need to consider all versions that match version range criteria (hence VersionType.VersionRange and no requiredVersion)
            Hashtable[] pkgResults = FindPackagesWithVersionHelper(packageName, VersionType.VersionRange, versionRange: versionRange, requiredVersion: null, includePrerelease, getOnlyLatest: false, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: new string[] { }, hashtableResponse: emptyHashResponses, responseType: containerRegistryFindResponseType);
            }

            return new FindResults(stringResponse: new string[] { }, hashtableResponse: pkgResults.ToArray(), responseType: containerRegistryFindResponseType);
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
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindVersion()");
            if (!NuGetVersion.TryParse(version, out NuGetVersion requiredVersion))
            {
                errRecord = new ErrorRecord(
                    new ArgumentException($"Version {version} to be found is not a valid NuGet version."),
                    "FindNameFailure",
                    ErrorCategory.InvalidArgument,
                    this);

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: containerRegistryFindResponseType);
            }

            _cmdletPassedIn.WriteDebug($"'{packageName}' version parsed as '{requiredVersion}'");
            bool includePrereleaseVersions = requiredVersion.IsPrerelease;

            // for FindVersion(), need to consider the specific required version (hence VersionType.SpecificVersion and no version range)
            Hashtable[] pkgResult = FindPackagesWithVersionHelper(packageName, VersionType.SpecificVersion, versionRange: VersionRange.None, requiredVersion: requiredVersion, includePrereleaseVersions, getOnlyLatest: false, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: new string[] { }, hashtableResponse: emptyHashResponses, responseType: containerRegistryFindResponseType);
            }

            return new FindResults(stringResponse: new string[] { }, hashtableResponse: pkgResult.ToArray(), responseType: containerRegistryFindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version and tag.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5" -Tag "Provider"
        /// </summary>
        public override FindResults FindVersionWithTag(string packageName, string version, string[] tags, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindVersionWithTag()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find version with tag(s) is not supported for the ContainerRegistry server protocol repository '{Repository.Name}'"),
                "FindVersionWithTagFailure",
                ErrorCategory.InvalidOperation,
                this);

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: containerRegistryFindResponseType);
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
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::InstallPackage()");
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

        private Stream InstallVersion(
            string packageName,
            string moduleVersion,
            out ErrorRecord errRecord)
        {
            errRecord = null;
            string packageNameLowercase = packageName.ToLower();
            string accessToken = string.Empty;
            string tenantID = string.Empty;
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            string registryUrl = Repository.Uri.ToString();
            string containerRegistryAccessToken = GetContainerRegistryAccessToken(Repository, out errRecord);
            if (errRecord != null)
            {
                return null;
            }

            string registry = Repository.Uri.Host;
            _cmdletPassedIn.WriteVerbose($"Getting manifest for {packageNameLowercase} - {moduleVersion}");
            var manifest = GetContainerRegistryRepositoryManifestAsync(registry, packageNameLowercase, moduleVersion, containerRegistryAccessToken, out errRecord);
            if (errRecord != null)
            {
                return null;
            }
            string digest = GetDigestFromManifest(manifest, out errRecord);
            if (errRecord != null)
            {
                return null;
            }

            _cmdletPassedIn.WriteVerbose($"Downloading blob for {packageNameLowercase} - {moduleVersion}");
            // TODO: error handling here?
            var responseContent = GetContainerRegistryBlobAsync(registry, packageNameLowercase, digest, containerRegistryAccessToken).Result;

            return responseContent.ReadAsStreamAsync().Result;
        }

        #endregion

        #region Private Methods

        private string GetDigestFromManifest(JObject manifest, out ErrorRecord errRecord)
        {
            errRecord = null;
            string digest = String.Empty;

            if (manifest == null)
            {
                errRecord = new ErrorRecord(
                    exception: new ArgumentNullException("Manifest (passed in to determine digest) is null."),
                    "ManifestNullError",
                    ErrorCategory.InvalidArgument,
                    _cmdletPassedIn);

                return digest;
            }

            JToken layers = manifest["layers"];
            if (layers == null || !layers.HasValues)
            {
                errRecord = new ErrorRecord(
                    exception: new ArgumentNullException("Manifest 'layers' property (passed in to determine digest) is null or does not have values."),
                    "ManifestLayersNullOrEmptyError",
                    ErrorCategory.InvalidArgument,
                    _cmdletPassedIn);

                return digest;
            }

            foreach (JObject item in layers)
            {
                if (item.ContainsKey("digest"))
                {
                    digest = item.GetValue("digest").ToString();
                    break;
                }
            }

            return digest;
        }

        // access token can be empty if the repository is unauthenticated
        internal string GetContainerRegistryAccessToken(PSRepositoryInfo repositoryInfo, out ErrorRecord errRecord)
        {
            string accessToken = string.Empty;
            string containerRegistryAccessToken = string.Empty;
            string tenantID = string.Empty;
            errRecord = null;

            var repositoryCredentialInfo = Repository.CredentialInfo;
            if (repositoryCredentialInfo != null)
            {
                accessToken = Utils.GetContainerRegistryAccessTokenFromSecretManagement(
                    Repository.Name,
                    repositoryCredentialInfo,
                    _cmdletPassedIn);

                _cmdletPassedIn.WriteVerbose("Access token retrieved.");

                tenantID = repositoryCredentialInfo.SecretName;
                _cmdletPassedIn.WriteVerbose($"Tenant ID: {tenantID}");
            }
            else
            {
                bool isRepositoryUnauthenticated = IsContainerRegistryUnauthenticated(repositoryInfo.Uri.ToString());

                if (!isRepositoryUnauthenticated)
                {
                    accessToken = Utils.GetAzAccessToken();
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        errRecord = new ErrorRecord(
                            new InvalidOperationException("Failed to get access token from Azure."),
                            "AzAccessTokenFailure",
                            ErrorCategory.AuthenticationError,
                            this);

                        return null;
                    }
                }
            }

            string registry = repositoryInfo.Uri.Host;

            var containerRegistryRefreshToken = GetContainerRegistryRefreshToken(registry, tenantID, accessToken, out errRecord);
            if (errRecord != null)
            {
                return null;
            }

            containerRegistryAccessToken = GetContainerRegistryAccessTokenByRefreshToken(registry, containerRegistryRefreshToken, out errRecord);
            if (errRecord != null)
            {
                return null;
            }

            return containerRegistryAccessToken;
        }

        internal bool IsContainerRegistryUnauthenticated(string registryUrl)
        {
            string endpoint = $"{registryUrl}/v2/";
            var response = s_client.SendAsync(new HttpRequestMessage(HttpMethod.Head, endpoint)).Result;
            return (response.StatusCode == HttpStatusCode.OK);
        }

        internal string GetContainerRegistryRefreshToken(string registry, string tenant, string accessToken, out ErrorRecord errRecord)
        {
            string content = string.Format(containerRegistryRefreshTokenTemplate, registry, tenant, accessToken);
            var contentHeaders = new Collection<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Content-Type", "application/x-www-form-urlencoded") };
            string exchangeUrl = string.Format(containerRegistryOAuthExchangeUrlTemplate, registry);
            var results = GetHttpResponseJObjectUsingContentHeaders(exchangeUrl, HttpMethod.Post, content, contentHeaders, out errRecord);

            if (results != null && results["refresh_token"] != null)
            {
                return results["refresh_token"].ToString();
            }

            return string.Empty;
        }

        internal string GetContainerRegistryAccessTokenByRefreshToken(string registry, string refreshToken, out ErrorRecord errRecord)
        {
            string content = string.Format(containerRegistryAccessTokenTemplate, registry, refreshToken);
            var contentHeaders = new Collection<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Content-Type", "application/x-www-form-urlencoded") };
            string tokenUrl = string.Format(containerRegistryOAuthTokenUrlTemplate, registry);
            var results = GetHttpResponseJObjectUsingContentHeaders(tokenUrl, HttpMethod.Post, content, contentHeaders, out errRecord);

            if (results != null && results["access_token"] != null)
            {
                return results["access_token"].ToString();
            }

            return string.Empty;
        }

        internal JObject GetContainerRegistryRepositoryManifestAsync(string registry, string packageName, string version, string containerRegistryAccessToken, out ErrorRecord errRecord)
        {
            // the packageName parameter here maps to repositoryName in ContainerRegistry, but to not conflict with PSGet definition of repository we will call it packageName
            // example of manifestUrl: https://psgetregistry.azurecr.io/hello-world:3.0.0
            string manifestUrl = string.Format(containerRegistryManifestUrlTemplate, registry, packageName, version);

            var defaultHeaders = GetDefaultHeaders(containerRegistryAccessToken);
            return GetHttpResponseJObjectUsingDefaultHeaders(manifestUrl, HttpMethod.Get, defaultHeaders, out errRecord);
        }

        internal async Task<HttpContent> GetContainerRegistryBlobAsync(string registry, string repositoryName, string digest, string containerRegistryAccessToken)
        {
            string blobUrl = string.Format(containerRegistryBlobDownloadUrlTemplate, registry, repositoryName, digest);
            var defaultHeaders = GetDefaultHeaders(containerRegistryAccessToken);
            return await GetHttpContentResponseJObject(blobUrl, defaultHeaders);
        }

        internal JObject FindContainerRegistryImageTags(string registry, string repositoryName, string version, string containerRegistryAccessToken, out ErrorRecord errRecord)
        {
            /* response returned looks something like:
             *   "registry": "myregistry.azurecr.io"
             *   "imageName": "hello-world"
             *   "tags": [
             *     {
             *       ""name"": ""1.0.0"",
             *       ""digest"": ""sha256:92c7f9c92844bbbb5d0a101b22f7c2a7949e40f8ea90c8b3bc396879d95e899a"",
             *       ""createdTime"": ""2023-12-23T18:06:48.9975733Z"",
             *       ""lastUpdateTime"": ""2023-12-23T18:06:48.9975733Z"",
             *       ""signed"": false,
             *       ""changeableAttributes"": {
             *         ""deleteEnabled"": true,
             *         ""writeEnabled"": true,
             *         ""readEnabled"": true,
             *         ""listEnabled"": true
             *       }
             *     }]
             */
            try
            {
                string resolvedVersion = string.Equals(version, "*", StringComparison.OrdinalIgnoreCase) ? null : $"/{version}";
                string findImageUrl = string.Format(containerRegistryFindImageVersionUrlTemplate, registry, repositoryName, resolvedVersion);
                var defaultHeaders = GetDefaultHeaders(containerRegistryAccessToken);
                return GetHttpResponseJObjectUsingDefaultHeaders(findImageUrl, HttpMethod.Get, defaultHeaders, out errRecord);
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error finding ContainerRegistry artifact: " + e.Message);
            }
        }

        internal Hashtable GetContainerRegistryMetadata(string registry, string packageName, string exactTagVersion, string containerRegistryAccessToken, out ErrorRecord errRecord)
        {
            Hashtable requiredVersionResponse = new Hashtable();

            var foundTags = FindContainerRegistryManifest(registry, packageName, exactTagVersion, containerRegistryAccessToken, out errRecord);
            if (errRecord != null || foundTags == null)
            {
                return requiredVersionResponse;
            }

            /*   Response returned looks something like:
             *    {
             *     "schemaVersion": 2,
             *     "config": {
             *       "mediaType": "application/vnd.unknown.config.v1+json",
             *       "digest": "sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
             *       "size": 0
             *     },
             *     "layers": [
             *       {
             *         "mediaType": "application/vnd.oci.image.layer.nondistributable.v1.tar+gzip'",
             *         "digest": "sha256:7c55c7b66cb075628660d8249cc4866f16e34741c246a42ed97fb23ccd4ea956",
             *         "size": 3533,
             *         "annotations": {
             *           "org.opencontainers.image.title": "test_module.1.0.0.nupkg",
             *           "metadata": "{\"GUID\":\"45219bf4-10a4-4242-92d6-9bfcf79878fd\",\"FunctionsToExport\":[],\"CompanyName\":\"Anam\",\"CmdletsToExport\":[],\"VariablesToExport\":\"*\",\"Author\":\"Anam Navied\",\"ModuleVersion\":\"1.0.0\",\"Copyright\":\"(c) Anam Navied. All rights reserved.\",\"PrivateData\":{\"PSData\":{\"Tags\":[\"Test\",\"CommandsAndResource\",\"Tag2\"]}},\"RequiredModules\":[],\"Description\":\"This is a test module, for PSGallery team internal testing. Do not take a dependency on this package. This version contains tags for the package.\",\"AliasesToExport\":[]}"
             *         }
             *       }
             *     ]
             *   }
             */

            var serverPkgInfo = GetMetadataProperty(foundTags, packageName, out Exception exception);
            if (exception != null)
            {
                errRecord = new ErrorRecord(exception, "ParseMetadataFailure", ErrorCategory.InvalidResult, this);

                return requiredVersionResponse;
            }

            try
            {
                using (JsonDocument metadataJSONDoc = JsonDocument.Parse(serverPkgInfo.Metadata))
                {
                    string pkgVersionString = String.Empty;
                    JsonElement rootDom = metadataJSONDoc.RootElement;
                    if (rootDom.TryGetProperty("ModuleVersion", out JsonElement pkgVersionElement))
                    {
                        // module metadata will have "ModuleVersion" property
                        pkgVersionString = pkgVersionElement.ToString();
                        if (rootDom.TryGetProperty("PrivateData", out JsonElement pkgPrivateDataElement) && pkgPrivateDataElement.TryGetProperty("PSData", out JsonElement pkgPSDataElement)
                            && pkgPSDataElement.TryGetProperty("Prerelease", out JsonElement pkgPrereleaseLabelElement) && !String.IsNullOrEmpty(pkgPrereleaseLabelElement.ToString().Trim()))
                        {
                            pkgVersionString += $"-{pkgPrereleaseLabelElement.ToString()}";
                        }
                    }
                    else if (rootDom.TryGetProperty("Version", out pkgVersionElement))
                    {
                        // script metadata will have "Version" property
                        pkgVersionString = pkgVersionElement.ToString();
                    }
                    else
                    {
                        errRecord = new ErrorRecord(
                            new InvalidOrEmptyResponse($"Response does not contain 'ModuleVersion' or 'Version' property in metadata for package '{packageName}' in '{Repository.Name}'."),
                            "ParseMetadataFailure",
                            ErrorCategory.InvalidResult,
                            this);

                        return requiredVersionResponse;
                    }

                    if (!NuGetVersion.TryParse(pkgVersionString, out NuGetVersion pkgVersion))
                    {
                        errRecord = new ErrorRecord(
                            new ArgumentException($"Version {pkgVersionString} to be parsed from metadata is not a valid NuGet version."),
                            "ParseMetadataFailure",
                            ErrorCategory.InvalidArgument,
                            this);

                        return requiredVersionResponse;
                    }

                    if (!NuGetVersion.TryParse(exactTagVersion, out NuGetVersion requiredVersion))
                    {
                        errRecord = new ErrorRecord(
                            new ArgumentException($"Version {exactTagVersion} to be parsed from method input is not a valid NuGet version."),
                            "ParseMetadataFailure",
                            ErrorCategory.InvalidArgument,
                            this);

                        return requiredVersionResponse;
                    }

                    _cmdletPassedIn.WriteDebug($"'{packageName}' version parsed as '{pkgVersion}'");
                    if (pkgVersion.ToNormalizedString() == requiredVersion.ToNormalizedString())
                    {
                        requiredVersionResponse = serverPkgInfo.ToHashtable();
                    }
                }
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                            new ArgumentException($"Error parsing server metadata: {e.Message}"),
                            "ParseMetadataFailure",
                            ErrorCategory.InvalidData,
                            this);

                return requiredVersionResponse;
            }

            return requiredVersionResponse;
        }

        internal ContainerRegistryInfo GetMetadataProperty(JObject foundTags, string packageName, out Exception exception)
        {
            exception = null;
            ContainerRegistryInfo serverPkgInfo = null;
            var layers = foundTags["layers"];
            if (layers == null || layers[0] == null)
            {
                exception = new InvalidOrEmptyResponse($"Response does not contain 'layers' element in manifest for package '{packageName}' in '{Repository.Name}'.");

                return serverPkgInfo;
            }

            var annotations = layers[0]["annotations"];
            if (annotations == null)
            {
                exception = new InvalidOrEmptyResponse($"Response does not contain 'annotations' element in manifest for package '{packageName}' in '{Repository.Name}'.");

                return serverPkgInfo;
            }

            // Check for package name
            var pkgTitleJToken = annotations["org.opencontainers.image.title"];
            if (pkgTitleJToken == null)
            {
                exception = new InvalidOrEmptyResponse($"Response does not contain 'org.opencontainers.image.title' element for package '{packageName}' in '{Repository.Name}'.");

                return serverPkgInfo;
            }
            string metadataPkgName = pkgTitleJToken.ToString();
            if (string.IsNullOrWhiteSpace(metadataPkgName))
            {
                exception = new InvalidOrEmptyResponse($"Response element 'org.opencontainers.image.title' is empty for package '{packageName}' in '{Repository.Name}'.");

                return serverPkgInfo;
            }

            // Check for package metadata
            var pkgMetadataJToken = annotations["metadata"];
            if (pkgMetadataJToken == null)
            {
                exception = new InvalidOrEmptyResponse($"Response does not contain 'metadata' element in manifest for package '{packageName}' in '{Repository.Name}'.");

                return serverPkgInfo;
            }
            var metadata = pkgMetadataJToken.ToString();

            // Check for package artifact type
            var resourceTypeJToken = annotations["resourceType"];
            var resourceType = resourceTypeJToken != null ? resourceTypeJToken.ToString() : string.Empty;

            return new ContainerRegistryInfo(metadataPkgName, metadata, resourceType);
        }

        internal JObject FindContainerRegistryManifest(string registry, string packageName, string version, string containerRegistryAccessToken, out ErrorRecord errRecord)
        {
            try
            {
                var createManifestUrl = string.Format(containerRegistryManifestUrlTemplate, registry, packageName, version);
                _cmdletPassedIn.WriteDebug($"GET manifest url:  {createManifestUrl}");

                var defaultHeaders = GetDefaultHeaders(containerRegistryAccessToken);
                return GetHttpResponseJObjectUsingDefaultHeaders(createManifestUrl, HttpMethod.Get, defaultHeaders, out errRecord);
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error finding ContainerRegistry manifest: " + e.Message);
            }
        }

        internal async Task<string> GetStartUploadBlobLocation(string pkgName, string containerRegistryAccessToken)
        {
            try
            {
                var defaultHeaders = GetDefaultHeaders(containerRegistryAccessToken);
                var startUploadUrl = string.Format(containerRegistryStartUploadTemplate, Registry, pkgName);
                return (await GetHttpResponseHeader(startUploadUrl, HttpMethod.Post, defaultHeaders)).Location.ToString();
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error starting publishing to ContainerRegistry: " + e.Message);
            }
        }

        internal async Task<HttpResponseMessage> EndUploadBlob(string location, string filePath, string digest, bool isManifest, string containerRegistryAccessToken)
        {
            try
            {
                var endUploadUrl = string.Format(containerRegistryEndUploadTemplate, Registry, location, digest);
                var defaultHeaders = GetDefaultHeaders(containerRegistryAccessToken);
                return await PutRequestAsync(endUploadUrl, filePath, isManifest, defaultHeaders);
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to uploading module to ContainerRegistry: " + e.Message);
            }
        }

        internal async Task<HttpResponseMessage> UploadManifest(string pkgName, string pkgVersion, string configPath, bool isManifest, string containerRegistryAccessToken)
        {
            try
            {
                var createManifestUrl = string.Format(containerRegistryManifestUrlTemplate, Registry, pkgName, pkgVersion);
                var defaultHeaders = GetDefaultHeaders(containerRegistryAccessToken);
                return await PutRequestAsync(createManifestUrl, configPath, isManifest, defaultHeaders);
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to create manifest: " + e.Message);
            }
        }

        internal async Task<HttpContent> GetHttpContentResponseJObject(string url, Collection<KeyValuePair<string, string>> defaultHeaders)
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

        internal JObject GetHttpResponseJObjectUsingDefaultHeaders(string url, HttpMethod method, Collection<KeyValuePair<string, string>> defaultHeaders, out ErrorRecord errRecord)
        {
            try
            {
                errRecord = null;
                HttpRequestMessage request = new HttpRequestMessage(method, url);
                SetDefaultHeaders(defaultHeaders);

                return SendRequestAsync(request).GetAwaiter().GetResult();
            }
            catch (ResourceNotFoundException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "ResourceNotFound",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);
            }
            catch (UnauthorizedException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "UnauthorizedRequest",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);
            }
            catch (HttpRequestException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "HttpRequestCallFailure",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "HttpRequestCallFailure",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);
            }

            return null;
        }

        internal JObject GetHttpResponseJObjectUsingContentHeaders(string url, HttpMethod method, string content, Collection<KeyValuePair<string, string>> contentHeaders, out ErrorRecord errRecord)
        {
            try
            {
                errRecord = null;
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

                return SendRequestAsync(request).GetAwaiter().GetResult();
            }
            catch (ResourceNotFoundException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "ResourceNotFound",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);
            }
            catch (UnauthorizedException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "UnauthorizedRequest",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);
            }
            catch (HttpRequestException e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "HttpRequestCallFailure",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "HttpRequestCallFailure",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);
            }

            return null;
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

                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        break;

                    case HttpStatusCode.Unauthorized:
                        throw new UnauthorizedException($"Response unauthorized: {response.ReasonPhrase}.");

                    case HttpStatusCode.NotFound:
                        throw new ResourceNotFoundException($"Package not found: {response.ReasonPhrase}.");

                    // all other errors
                    default:
                        throw new HttpRequestException($"Response returned error with status code {response.StatusCode}: {response.ReasonPhrase}.");
                }

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

        private static async Task<HttpResponseMessage> PutRequestAsync(string url, string filePath, bool isManifest, Collection<KeyValuePair<string, string>> contentHeaders)
        {
            try
            {
                SetDefaultHeaders(contentHeaders);

                FileInfo fileInfo = new FileInfo(filePath);
                using (FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read))
                {
                    HttpContent httpContent = new StreamContent(fileStream);
                    if (isManifest)
                    {
                        httpContent.Headers.Add("Content-Type", "application/vnd.oci.image.manifest.v1+json");
                    }
                    else
                    {
                        httpContent.Headers.Add("Content-Type", "application/octet-stream");
                    }

                    return await s_client.PutAsync(url, httpContent); ;
                }
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to uploading module to ContainerRegistry: " + e.Message);
            }

        }

        private static Collection<KeyValuePair<string, string>> GetDefaultHeaders(string containerRegistryAccessToken)
        {
            var defaultHeaders = new Collection<KeyValuePair<string, string>>();

            if (!string.IsNullOrEmpty(containerRegistryAccessToken))
            {
                defaultHeaders.Add(new KeyValuePair<string, string>("Authorization", containerRegistryAccessToken));
            }

            defaultHeaders.Add(new KeyValuePair<string, string>("Accept", "application/vnd.oci.image.manifest.v1+json"));

            return defaultHeaders;
        }

        internal bool PushNupkgContainerRegistry(string psd1OrPs1File, string outputNupkgDir, string pkgName, NuGetVersion pkgVersion, PSRepositoryInfo repository, ResourceType resourceType, Hashtable parsedMetadataHash, Hashtable dependencies, out ErrorRecord errRecord)
        {
            string fullNupkgFile = System.IO.Path.Combine(outputNupkgDir, pkgName + "." + pkgVersion.ToNormalizedString() + ".nupkg");
            string pkgNameLower = pkgName.ToLower();

            // Get access token (includes refresh tokens)
            var containerRegistryAccessToken = GetContainerRegistryAccessToken(Repository, out errRecord);

            // Upload .nupkg
            TryUploadNupkg(pkgNameLower, containerRegistryAccessToken, fullNupkgFile, out string nupkgDigest);

            // Create and upload an empty file-- needed by ContainerRegistry server
            TryCreateAndUploadEmptyFile(outputNupkgDir, pkgNameLower, containerRegistryAccessToken);

            // Create config.json file
            var configFilePath = System.IO.Path.Combine(outputNupkgDir, "config.json");
            TryCreateConfig(configFilePath, out string configDigest);

            _cmdletPassedIn.WriteVerbose("Create package version metadata as JSON string");
            // Create module metadata string
            string metadataJson = CreateMetadataContent(psd1OrPs1File, resourceType, parsedMetadataHash, out ErrorRecord metadataCreationError);
            if (metadataCreationError != null)
            {
                _cmdletPassedIn.ThrowTerminatingError(metadataCreationError);
            }

            // Create and upload manifest 
            TryCreateAndUploadManifest(fullNupkgFile, nupkgDigest, configDigest, pkgName, resourceType, metadataJson, configFilePath,
                pkgNameLower, pkgVersion, containerRegistryAccessToken);

            return true;
        }

        private bool TryUploadNupkg(string pkgNameLower, string containerRegistryAccessToken, string fullNupkgFile, out string nupkgDigest)
        {
            _cmdletPassedIn.WriteVerbose("Start uploading blob");
            // Note:  ContainerRegistry registries will only accept a name that is all lowercase.
            var moduleLocation = GetStartUploadBlobLocation(pkgNameLower, containerRegistryAccessToken).Result;

            _cmdletPassedIn.WriteVerbose("Computing digest for .nupkg file");
            bool nupkgDigestCreated = CreateDigest(fullNupkgFile, out nupkgDigest, out ErrorRecord nupkgDigestError);
            if (!nupkgDigestCreated)
            {
                _cmdletPassedIn.ThrowTerminatingError(nupkgDigestError);
            }

            _cmdletPassedIn.WriteVerbose("Finish uploading blob");
            var responseNupkg = EndUploadBlob(moduleLocation, fullNupkgFile, nupkgDigest, false, containerRegistryAccessToken).Result;

            return responseNupkg.IsSuccessStatusCode;
        }

        private bool TryCreateAndUploadEmptyFile(string outputNupkgDir, string pkgNameLower, string containerRegistryAccessToken)
        {
            _cmdletPassedIn.WriteVerbose("Create an empty file");
            string emptyFileName = "empty.txt";
            var emptyFilePath = System.IO.Path.Combine(outputNupkgDir, emptyFileName);
            // Rename the empty file in case such a file already exists in the temp folder (although highly unlikely)
            while (File.Exists(emptyFilePath))
            {
                emptyFilePath = Guid.NewGuid().ToString() + ".txt";
            }
            Utils.CreateFile(emptyFilePath);

            _cmdletPassedIn.WriteVerbose("Start uploading an empty file");
            var emptyLocation = GetStartUploadBlobLocation(pkgNameLower, containerRegistryAccessToken).Result;
            _cmdletPassedIn.WriteVerbose("Computing digest for empty file");
            bool emptyDigestCreated = CreateDigest(emptyFilePath, out string emptyDigest, out ErrorRecord emptyDigestError);
            if (!emptyDigestCreated)
            {
                _cmdletPassedIn.ThrowTerminatingError(emptyDigestError);
            }
            _cmdletPassedIn.WriteVerbose("Finish uploading empty file");
            var emptyResponse = EndUploadBlob(emptyLocation, emptyFilePath, emptyDigest, false, containerRegistryAccessToken).Result;

            return emptyResponse.IsSuccessStatusCode;
        }

        private bool TryCreateConfig(string configFilePath, out string configDigest)
        {
            _cmdletPassedIn.WriteVerbose("Create the config file");
            while (File.Exists(configFilePath))
            {
                configFilePath = Guid.NewGuid().ToString() + ".json";
            }
            Utils.CreateFile(configFilePath);

            _cmdletPassedIn.WriteVerbose("Computing digest for config");
            bool configDigestCreated = CreateDigest(configFilePath, out configDigest, out ErrorRecord configDigestError);
            if (!configDigestCreated)
            {
                _cmdletPassedIn.ThrowTerminatingError(configDigestError);
            }

            return configDigestCreated;
        }

        private bool TryCreateAndUploadManifest(string fullNupkgFile, string nupkgDigest, string configDigest, string pkgName, ResourceType resourceType, string metadataJson, string configFilePath,
            string pkgNameLower, NuGetVersion pkgVersion, string containerRegistryAccessToken)
        {
            FileInfo nupkgFile = new FileInfo(fullNupkgFile);
            var fileSize = nupkgFile.Length;
            var fileName = System.IO.Path.GetFileName(fullNupkgFile);
            string fileContent = CreateManifestContent(nupkgDigest, configDigest, fileSize, fileName, pkgName, resourceType, metadataJson);
            File.WriteAllText(configFilePath, fileContent);

            _cmdletPassedIn.WriteVerbose("Create the manifest layer");
            HttpResponseMessage manifestResponse = UploadManifest(pkgNameLower, pkgVersion.OriginalVersion, configFilePath, true, containerRegistryAccessToken).Result;
            bool manifestCreated = manifestResponse.IsSuccessStatusCode;
            if (!manifestCreated)
            {
                _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                        new ArgumentException("Error uploading package manifest"),
                        "PackageManifestUploadError",
                        ErrorCategory.InvalidResult,
                        _cmdletPassedIn));
                return false;
            }

            return manifestCreated;
        }

        private string CreateManifestContent(
            string nupkgDigest,
            string configDigest,
            long nupkgFileSize,
            string fileName,
            string packageName,
            ResourceType resourceType,
            string metadata)
        {
            StringBuilder stringBuilder = new StringBuilder();
            StringWriter stringWriter = new StringWriter(stringBuilder);
            JsonTextWriter jsonWriter = new JsonTextWriter(stringWriter);

            jsonWriter.Formatting = Newtonsoft.Json.Formatting.Indented;

            // start of manifest JSON object
            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("schemaVersion");
            jsonWriter.WriteValue(2);
            jsonWriter.WritePropertyName("mediaType");
            jsonWriter.WriteValue("application/vnd.oci.image.manifest.v1+json");

            jsonWriter.WritePropertyName("config");
            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("mediaType");
            jsonWriter.WriteValue("application/vnd.oci.image.config.v1+json");
            jsonWriter.WritePropertyName("digest");
            jsonWriter.WriteValue($"sha256:{configDigest}");
            jsonWriter.WritePropertyName("size");
            jsonWriter.WriteValue(0);
            jsonWriter.WriteEndObject();

            jsonWriter.WritePropertyName("layers");
            jsonWriter.WriteStartArray();

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("mediaType");
            jsonWriter.WriteValue("application/vnd.oci.image.layer.v1.tar+gzip");
            jsonWriter.WritePropertyName("digest");
            jsonWriter.WriteValue($"sha256:{nupkgDigest}");
            jsonWriter.WritePropertyName("size");
            jsonWriter.WriteValue(nupkgFileSize);
            jsonWriter.WritePropertyName("annotations");
            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("org.opencontainers.image.title");
            jsonWriter.WriteValue(packageName);
            jsonWriter.WritePropertyName("org.opencontainers.image.description");
            jsonWriter.WriteValue(fileName);
            jsonWriter.WritePropertyName("metadata");
            jsonWriter.WriteValue(metadata);
            jsonWriter.WritePropertyName("resourceType");
            jsonWriter.WriteValue(resourceType.ToString());
            jsonWriter.WriteEndObject(); // end of annotations object

            jsonWriter.WriteEndObject(); // end of 'layers' entry object

            jsonWriter.WriteEndArray(); // end of 'layers' array
            jsonWriter.WriteEndObject(); // end of manifest JSON object

            return stringWriter.ToString();
        }

        private bool CreateDigest(string fileName, out string digest, out ErrorRecord error)
        {
            FileInfo fileInfo = new FileInfo(fileName);
            SHA256 mySHA256 = SHA256.Create();

            using (FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read))
            {
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
                    _cmdletPassedIn.WriteVerbose($"{fileInfo.Name}: {digest}");
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
            }
            if (error != null)
            {
                return false;
            }

            return true;
        }

        private string CreateMetadataContent(string manifestFilePath, ResourceType resourceType, Hashtable parsedMetadata, out ErrorRecord metadataCreationError)
        {
            metadataCreationError = null;
            string jsonString = string.Empty;

            if (parsedMetadata == null || parsedMetadata.Count == 0)
            {
                metadataCreationError = new ErrorRecord(
                    new ArgumentException("Hashtable created from .ps1 or .psd1 containing package metadata was null or empty"),
                    "MetadataHashtableEmptyError",
                    ErrorCategory.InvalidArgument,
                    _cmdletPassedIn);

                return jsonString;
            }

            _cmdletPassedIn.WriteVerbose("Serialize JSON into string.");

            if (parsedMetadata.ContainsKey("Version") && parsedMetadata["Version"] is NuGetVersion pkgNuGetVersion)
            {
                // do not serialize NuGetVersion, this will populate more metadata than is needed and makes it harder to deserialize later
                parsedMetadata.Remove("Version");
                parsedMetadata["Version"] = pkgNuGetVersion.ToString();
            }

            try
            {
                jsonString = System.Text.Json.JsonSerializer.Serialize(parsedMetadata);
            }
            catch (Exception ex)
            {
                metadataCreationError = new ErrorRecord(ex, "JsonSerializationError", ErrorCategory.InvalidResult, _cmdletPassedIn);
                return jsonString;
            }

            return jsonString;
        }

        private Hashtable[] FindPackagesWithVersionHelper(string packageName, VersionType versionType, VersionRange versionRange, NuGetVersion requiredVersion, bool includePrerelease, bool getOnlyLatest, out ErrorRecord errRecord)
        {
            string accessToken = string.Empty;
            string tenantID = string.Empty;
            string registryUrl = Repository.Uri.ToString();
            string packageNameLowercase = packageName.ToLower();

            string containerRegistryAccessToken = GetContainerRegistryAccessToken(Repository, out errRecord);
            if (errRecord != null)
            {
                return emptyHashResponses;
            }

            var foundTags = FindContainerRegistryImageTags(Registry, packageNameLowercase, "*", containerRegistryAccessToken, out errRecord);
            if (errRecord != null || foundTags == null)
            {
                return emptyHashResponses;
            }

            List<Hashtable> latestVersionResponse = new List<Hashtable>();
            List<JToken> allVersionsList = foundTags["tags"].ToList();

            SortedDictionary<NuGet.Versioning.SemanticVersion, string> sortedQualifyingPkgs = GetPackagesWithRequiredVersion(allVersionsList, versionType, versionRange, requiredVersion, packageNameLowercase, includePrerelease, out errRecord);
            if (errRecord != null)
            {
                return emptyHashResponses;
            }

            var pkgsInDescendingOrder = sortedQualifyingPkgs.Reverse();

            foreach (var pkgVersionTag in pkgsInDescendingOrder)
            {
                string exactTagVersion = pkgVersionTag.Value.ToString();
                Hashtable metadata = GetContainerRegistryMetadata(Registry, packageNameLowercase, exactTagVersion, containerRegistryAccessToken, out errRecord);
                if (errRecord != null || metadata.Count == 0)
                {
                    return emptyHashResponses;
                }

                latestVersionResponse.Add(metadata);
                if (getOnlyLatest)
                {
                    // getOnlyLatest will be true for FindName(), as only the latest criteria satisfying version should be returned
                    break;
                }
            }

            return latestVersionResponse.ToArray();
        }

        private SortedDictionary<NuGet.Versioning.SemanticVersion, string> GetPackagesWithRequiredVersion(List<JToken> allPkgVersions, VersionType versionType, VersionRange versionRange, NuGetVersion specificVersion, string packageName, bool includePrerelease, out ErrorRecord errRecord)
        {
            errRecord = null;
            // we need NuGetVersion to sort versions by order, and string pkgVersionString (which is the exact tag from the server) to call GetContainerRegistryMetadata() later with exact version tag.
            SortedDictionary<NuGet.Versioning.SemanticVersion, string> sortedPkgs = new SortedDictionary<SemanticVersion, string>(VersionComparer.Default);
            bool isSpecificVersionSearch = versionType == VersionType.SpecificVersion;

            foreach (var pkgVersionTagInfo in allPkgVersions)
            {
                using (JsonDocument pkgVersionEntry = JsonDocument.Parse(pkgVersionTagInfo.ToString()))
                {
                    JsonElement rootDom = pkgVersionEntry.RootElement;
                    if (!rootDom.TryGetProperty("name", out JsonElement pkgVersionElement))
                    {
                        errRecord = new ErrorRecord(
                            new InvalidOrEmptyResponse($"Response does not contain version element ('name') for package '{packageName}' in '{Repository.Name}'."),
                            "FindNameFailure",
                            ErrorCategory.InvalidResult,
                            this);

                        return null;
                    }

                    string pkgVersionString = pkgVersionElement.ToString();
                    // determine if the package version that is a repository tag is a valid NuGetVersion
                    if (!NuGetVersion.TryParse(pkgVersionString, out NuGetVersion pkgVersion))
                    {
                        errRecord = new ErrorRecord(
                            new ArgumentException($"Version {pkgVersionString} to be parsed from metadata is not a valid NuGet version."),
                            "FindNameFailure",
                            ErrorCategory.InvalidArgument,
                            this);

                        return null;
                    }

                    _cmdletPassedIn.WriteDebug($"'{packageName}' version parsed as '{pkgVersion}'");

                    if (isSpecificVersionSearch)
                    {
                        if (pkgVersion.ToNormalizedString() == specificVersion.ToNormalizedString())
                        {
                            // accounts for FindVersion() scenario
                            sortedPkgs.Add(pkgVersion, pkgVersionString);
                            break;
                        }
                    }
                    else
                    {
                        if (versionRange.Satisfies(pkgVersion) && (!pkgVersion.IsPrerelease || includePrerelease))
                        {
                            // accounts for FindVersionGlobbing() and FindName() scenario
                            sortedPkgs.Add(pkgVersion, pkgVersionString);
                        }
                    }
                }
            }

            return sortedPkgs;
        }

        #endregion
    }
}
