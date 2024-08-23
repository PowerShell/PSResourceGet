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
        private static FindResponseType containerRegistryFindResponseType = FindResponseType.ResponseString;
        private static readonly FindResults emptyResponseResults = new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: containerRegistryFindResponseType);

        const string containerRegistryRefreshTokenTemplate = "grant_type=access_token&service={0}&tenant={1}&access_token={2}"; // 0 - registry, 1 - tenant, 2 - access token
        const string containerRegistryAccessTokenTemplate = "grant_type=refresh_token&service={0}&scope=repository:*:*&refresh_token={1}"; // 0 - registry, 1 - refresh token
        const string containerRegistryOAuthExchangeUrlTemplate = "https://{0}/oauth2/exchange"; // 0 - registry
        const string containerRegistryOAuthTokenUrlTemplate = "https://{0}/oauth2/token"; // 0 - registry
        const string containerRegistryManifestUrlTemplate = "https://{0}/v2/{1}/manifests/{2}"; // 0 - registry, 1 - repo(modulename), 2 - tag(version)
        const string containerRegistryBlobDownloadUrlTemplate = "https://{0}/v2/{1}/blobs/{2}"; // 0 - registry, 1 - repo(modulename), 2 - layer digest
        const string containerRegistryFindImageVersionUrlTemplate = "https://{0}/acr/v1/{1}/_tags{2}"; // 0 - registry, 1 - repo(modulename), 2 - /tag(version)
        const string containerRegistryStartUploadTemplate = "https://{0}/v2/{1}/blobs/uploads/"; // 0 - registry, 1 - packagename
        const string containerRegistryEndUploadTemplate = "https://{0}{1}&digest=sha256:{2}"; // 0 - registry, 1 - location, 2 - digest

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
            _sessionClient.Timeout = TimeSpan.FromMinutes(10);
            _sessionClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgentString);
        }

        #endregion

        #region Overriden Methods

        /// <summary>
        /// Find method which allows for searching for all packages from a repository and returns latest version for each.
        /// </summary>
        public override FindResults FindAll(bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindAll()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find all is not supported for the ContainerRegistry server protocol repository '{Repository.Name}'"),
                "FindAllFailure",
                ErrorCategory.InvalidOperation,
                this);

            return emptyResponseResults;
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag from a repository and returns latest version for each.
        /// </summary>
        public override FindResults FindTags(string[] tags, bool includePrerelease, ResourceType _type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindTags()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find tags is not supported for the ContainerRegistry server protocol repository '{Repository.Name}'"),
                "FindTagsFailure",
                ErrorCategory.InvalidOperation,
                this);

            return emptyResponseResults;
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

            return emptyResponseResults;
        }

        /// <summary>
        /// Find method which allows for searching for single name and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "PowerShellGet"
        /// Implementation Note: Need to filter further for latest version (prerelease or non-prerelease dependening on user preference)
        /// </summary>
        public override FindResults FindName(string packageName, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindName()");

            // for FindName(), need to consider all versions (hence VersionType.VersionRange and VersionRange.All, and no requiredVersion) but only pick latest (hence getOnlyLatest: true)
            Hashtable[] pkgResult = FindPackagesWithVersionHelper(packageName, VersionType.VersionRange, versionRange: VersionRange.All, requiredVersion: null, includePrerelease, getOnlyLatest: true, out errRecord);
            if (errRecord != null)
            {
                return emptyResponseResults;
            }

            return new FindResults(stringResponse: new string[] { }, hashtableResponse: pkgResult.ToArray(), responseType: containerRegistryFindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name and tag and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "PowerShellGet" -Tag "Provider"
        /// </summary>
        public override FindResults FindNameWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindNameWithTag()");
            errRecord = new ErrorRecord(
                new InvalidOperationException($"Find name with tag(s) is not supported for the ContainerRegistry server protocol repository '{Repository.Name}'"),
                "FindNameWithTagFailure",
                ErrorCategory.InvalidOperation,
                this);

            return emptyResponseResults;
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "PowerShell*"
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

            return emptyResponseResults;
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

            return emptyResponseResults;
        }

        /// <summary>
        /// Find method which allows for searching for single name with version range.
        /// Name: no wildcard support
        /// Version: supports wildcards
        /// Examples: Search "PowerShellGet" "[3.0.0.0, 5.0.0.0]"
        ///           Search "PowerShellGet" "3.*"
        /// Implementation note: Returns all versions, including prerelease ones. Later (in the API client side) we'll do filtering on the versions to satisfy what user provided.
        /// </summary>
        public override FindResults FindVersionGlobbing(string packageName, VersionRange versionRange, bool includePrerelease, ResourceType type, bool getOnlyLatest, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindVersionGlobbing()");

            // for FindVersionGlobbing(), need to consider all versions that match version range criteria (hence VersionType.VersionRange and no requiredVersion)
            Hashtable[] pkgResults = FindPackagesWithVersionHelper(packageName, VersionType.VersionRange, versionRange: versionRange, requiredVersion: null, includePrerelease, getOnlyLatest: false, out errRecord);
            if (errRecord != null)
            {
                return emptyResponseResults;
            }

            return new FindResults(stringResponse: new string[] { }, hashtableResponse: pkgResults.ToArray(), responseType: containerRegistryFindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5"
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

                return emptyResponseResults;
            }

            _cmdletPassedIn.WriteDebug($"'{packageName}' version parsed as '{requiredVersion}'");
            bool includePrereleaseVersions = requiredVersion.IsPrerelease;

            // for FindVersion(), need to consider the specific required version (hence VersionType.SpecificVersion and no version range)
            Hashtable[] pkgResult = FindPackagesWithVersionHelper(packageName, VersionType.SpecificVersion, versionRange: VersionRange.None, requiredVersion: requiredVersion, includePrereleaseVersions, getOnlyLatest: false, out errRecord);
            if (errRecord != null)
            {
                return emptyResponseResults;
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

            return emptyResponseResults;
        }

        /**  INSTALL APIS **/

        /// <summary>
        /// Installs a specific package.
        /// User may request to install package with or without providing version (as seen in examples below), but prior to calling this method the package is located and package version determined.
        /// Therefore, package version should not be null in this method.
        /// Name: no wildcard support.
        /// Examples: Install "PowerShellGet" -Version "3.5.0-alpha"
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

        /// <summary>
        /// Installs a package with version specified.
        /// Version can be prerelease or stable.
        /// </summary>
        private Stream InstallVersion(
            string packageName,
            string packageVersion,
            out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::InstallVersion()");
            errRecord = null;
            string packageNameLowercase = packageName.ToLower();
            string accessToken = string.Empty;
            string tenantID = string.Empty;
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                Directory.CreateDirectory(tempPath);
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "InstallVersionTempDirCreationError",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);

                return null;
            }

            string containerRegistryAccessToken = GetContainerRegistryAccessToken(out errRecord);
            if (errRecord != null)
            {
                return null;
            }

            _cmdletPassedIn.WriteVerbose($"Getting manifest for {packageNameLowercase} - {packageVersion}");
            var manifest = GetContainerRegistryRepositoryManifest(packageNameLowercase, packageVersion, containerRegistryAccessToken, out errRecord);
            if (errRecord != null)
            {
                return null;
            }
            string digest = GetDigestFromManifest(manifest, out errRecord);
            if (errRecord != null)
            {
                return null;
            }

            _cmdletPassedIn.WriteVerbose($"Downloading blob for {packageNameLowercase} - {packageVersion}");
            HttpContent responseContent;
            try
            {
                responseContent = GetContainerRegistryBlobAsync(packageNameLowercase, digest, containerRegistryAccessToken).Result;
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "InstallVersionGetContainerRegistryBlobAsyncError",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);

                return null;
            }

            return responseContent.ReadAsStreamAsync().Result;
        }

        #endregion

        #region Authentication and Token Methods

        /// <summary>
        /// Gets the access token for the container registry by following the below logic:
        /// If a credential is provided when registering the repository, retrieve the token from SecretsManagement.
        /// If no credential provided at registration then, check if the ACR endpoint can be accessed without a token. If not, try using Azure.Identity to get the az access token, then ACR refresh token and then ACR access token.
        /// Note: Access token can be empty if the repository is unauthenticated
        /// </summary>
        internal string GetContainerRegistryAccessToken(out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::GetContainerRegistryAccessToken()");
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
            }
            else
            {
                bool isRepositoryUnauthenticated = IsContainerRegistryUnauthenticated(Repository.Uri.ToString(), out errRecord);
                if (errRecord != null)
                {
                    return null;
                }

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
                else
                {
                    _cmdletPassedIn.WriteVerbose("Repository is unauthenticated");
                }
            }

            var containerRegistryRefreshToken = GetContainerRegistryRefreshToken(tenantID, accessToken, out errRecord);
            if (errRecord != null)
            {
                return null;
            }

            containerRegistryAccessToken = GetContainerRegistryAccessTokenByRefreshToken(containerRegistryRefreshToken, out errRecord);
            if (errRecord != null)
            {
                return null;
            }

            return containerRegistryAccessToken;
        }

        /// <summary>
        /// Checks if container registry repository is unauthenticated.
        /// </summary>
        internal bool IsContainerRegistryUnauthenticated(string containerRegistyUrl, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::IsContainerRegistryUnauthenticated()");
            errRecord = null;
            string endpoint = $"{containerRegistyUrl}/v2/";
            HttpResponseMessage response;
            try
            {
                response = _sessionClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, endpoint)).Result;
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    e,
                    "RegistryUnauthenticationCheckError",
                    ErrorCategory.InvalidResult,
                    this);

                return false;
            }

            return (response.StatusCode == HttpStatusCode.OK);
        }

        /// <summary>
        /// Given the access token retrieved from credentials, gets the refresh token.
        /// </summary>
        internal string GetContainerRegistryRefreshToken(string tenant, string accessToken, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::GetContainerRegistryRefreshToken()");
            string content = string.Format(containerRegistryRefreshTokenTemplate, Registry, tenant, accessToken);
            var contentHeaders = new Collection<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Content-Type", "application/x-www-form-urlencoded") };
            string exchangeUrl = string.Format(containerRegistryOAuthExchangeUrlTemplate, Registry);
            var results = GetHttpResponseJObjectUsingContentHeaders(exchangeUrl, HttpMethod.Post, content, contentHeaders, out errRecord);
            if (errRecord != null || results == null || results["refresh_token"] == null)
            {
                return string.Empty;
            }

            return results["refresh_token"].ToString();
        }

        /// <summary>
        /// Given the refresh token, gets the new access token with appropriate scope access permissions.
        /// </summary>
        internal string GetContainerRegistryAccessTokenByRefreshToken(string refreshToken, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::GetContainerRegistryAccessTokenByRefreshToken()");
            string content = string.Format(containerRegistryAccessTokenTemplate, Registry, refreshToken);
            var contentHeaders = new Collection<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Content-Type", "application/x-www-form-urlencoded") };
            string tokenUrl = string.Format(containerRegistryOAuthTokenUrlTemplate, Registry);
            var results = GetHttpResponseJObjectUsingContentHeaders(tokenUrl, HttpMethod.Post, content, contentHeaders, out errRecord);
            if (errRecord != null || results == null || results["access_token"] == null)
            {
                return string.Empty;
            }

            return results["access_token"].ToString();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Parses package manifest JObject to find digest entry, which is the SHA needed to identify and get the package.
        /// </summary>
        private string GetDigestFromManifest(JObject manifest, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::GetDigestFromManifest()");
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

        /// <summary>
        /// Gets the manifest for a package (ie repository in container registry terms) from the repository (ie registry in container registry terms)
        /// </summary>
        internal JObject GetContainerRegistryRepositoryManifest(string packageName, string version, string containerRegistryAccessToken, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::GetContainerRegistryRepositoryManifest()");
            // example of manifestUrl: https://psgetregistry.azurecr.io/hello-world:3.0.0
            string manifestUrl = string.Format(containerRegistryManifestUrlTemplate, Registry, packageName, version);
            var defaultHeaders = GetDefaultHeaders(containerRegistryAccessToken);
            return GetHttpResponseJObjectUsingDefaultHeaders(manifestUrl, HttpMethod.Get, defaultHeaders, out errRecord);
        }

        /// <summary>
        /// Get the blob for the package (ie repository in container registry terms) from the repositroy (ie registry in container registry terms)
        /// Used when installing the package
        /// </summary>
        internal async Task<HttpContent> GetContainerRegistryBlobAsync(string packageName, string digest, string containerRegistryAccessToken)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::GetContainerRegistryBlobAsync()");
            string blobUrl = string.Format(containerRegistryBlobDownloadUrlTemplate, Registry, packageName, digest);
            var defaultHeaders = GetDefaultHeaders(containerRegistryAccessToken);
            return await GetHttpContentResponseJObject(blobUrl, defaultHeaders);
        }

        /// <summary>
        /// Gets the image tags associated with the package (i.e repository in container registry terms), where the tag corresponds to the package's versions.
        /// If the package version is specified search for that specific tag for the image, if the package version is "*" search for all tags for the image.
        /// </summary>
        internal JObject FindContainerRegistryImageTags(string packageName, string version, string containerRegistryAccessToken, out ErrorRecord errRecord)
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
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindContainerRegistryImageTags()");
            string resolvedVersion = string.Equals(version, "*", StringComparison.OrdinalIgnoreCase) ? null : $"/{version}";
            string findImageUrl = string.Format(containerRegistryFindImageVersionUrlTemplate, Registry, packageName, resolvedVersion);
            var defaultHeaders = GetDefaultHeaders(containerRegistryAccessToken);
            return GetHttpResponseJObjectUsingDefaultHeaders(findImageUrl, HttpMethod.Get, defaultHeaders, out errRecord);
        }

        /// <summary>
        /// Get metadata for a package version.
        /// </summary>
        internal Hashtable GetContainerRegistryMetadata(string packageName, string exactTagVersion, string containerRegistryAccessToken, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::GetContainerRegistryMetadata()");
            Hashtable requiredVersionResponse = new Hashtable();

            var foundTags = FindContainerRegistryManifest(packageName, exactTagVersion, containerRegistryAccessToken, out errRecord);
            if (errRecord != null)
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

            var serverPkgInfo = GetMetadataProperty(foundTags, packageName, out errRecord);
            if (errRecord != null)
            {
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

        /// <summary>
        /// Get the manifest associated with the package version.
        /// </summary>
        internal JObject FindContainerRegistryManifest(string packageName, string version, string containerRegistryAccessToken, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindContainerRegistryManifest()");
            var createManifestUrl = string.Format(containerRegistryManifestUrlTemplate, Registry, packageName, version);
            _cmdletPassedIn.WriteDebug($"GET manifest url:  {createManifestUrl}");

            var defaultHeaders = GetDefaultHeaders(containerRegistryAccessToken);
            return GetHttpResponseJObjectUsingDefaultHeaders(createManifestUrl, HttpMethod.Get, defaultHeaders, out errRecord);
        }

        /// <summary>
        /// Get metadata for the package by parsing its manifest.
        /// </summary>
        internal ContainerRegistryInfo GetMetadataProperty(JObject foundTags, string packageName, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::GetMetadataProperty()");
            errRecord = null;
            ContainerRegistryInfo serverPkgInfo = null;

            var layers = foundTags["layers"];
            if (layers == null || layers[0] == null)
            {
                errRecord = new ErrorRecord(
                    new InvalidOrEmptyResponse($"Response does not contain 'layers' element in manifest for package '{packageName}' in '{Repository.Name}'."),
                    "GetMetadataPropertyLayersError",
                    ErrorCategory.InvalidData,
                    this);

                return serverPkgInfo;
            }

            var annotations = layers[0]["annotations"];
            if (annotations == null)
            {
                errRecord = new ErrorRecord(
                    new InvalidOrEmptyResponse($"Response does not contain 'annotations' element in manifest for package '{packageName}' in '{Repository.Name}'."),
                    "GetMetadataPropertyAnnotationsError",
                    ErrorCategory.InvalidData,
                    this);

                return serverPkgInfo;
            }

            // Check for package name
            var pkgTitleJToken = annotations["org.opencontainers.image.title"];
            if (pkgTitleJToken == null)
            {
                errRecord = new ErrorRecord(
                    new InvalidOrEmptyResponse($"Response does not contain 'org.opencontainers.image.title' element for package '{packageName}' in '{Repository.Name}'."),
                    "GetMetadataPropertyOCITitleError",
                    ErrorCategory.InvalidData,
                    this);

                return serverPkgInfo;
            }

            string metadataPkgName = pkgTitleJToken.ToString();
            if (string.IsNullOrWhiteSpace(metadataPkgName))
            {
                errRecord = new ErrorRecord(
                    new InvalidOrEmptyResponse($"Response element 'org.opencontainers.image.title' is empty for package '{packageName}' in '{Repository.Name}'."),
                    "GetMetadataPropertyOCITitleEmptyError",
                    ErrorCategory.InvalidData,
                    this);

                return serverPkgInfo;
            }

            // Check for package metadata
            var pkgMetadataJToken = annotations["metadata"];
            if (pkgMetadataJToken == null)
            {
                errRecord = new ErrorRecord(
                    new InvalidOrEmptyResponse($"Response does not contain 'metadata' element in manifest for package '{packageName}' in '{Repository.Name}'."),
                    "GetMetadataPropertyMetadataError",
                    ErrorCategory.InvalidData,
                    this);

                return serverPkgInfo;
            }

            var metadata = pkgMetadataJToken.ToString();

            // Check for package artifact type
            var resourceTypeJToken = annotations["resourceType"];
            var resourceType = resourceTypeJToken != null ? resourceTypeJToken.ToString() : "None";

            return new ContainerRegistryInfo(metadataPkgName, metadata, resourceType);
        }

        /// <summary>
        /// Upload manifest for the package, used for publishing.
        /// </summary>
        internal async Task<HttpResponseMessage> UploadManifest(string packageName, string packageVersion, string configPath, bool isManifest, string containerRegistryAccessToken)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::UploadManifest()");
            try
            {
                var createManifestUrl = string.Format(containerRegistryManifestUrlTemplate, Registry, packageName, packageVersion);
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
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::GetHttpContentResponseJObject()");
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

        /// <summary>
        /// Get response object when using default headers in the request.
        /// </summary>
        internal JObject GetHttpResponseJObjectUsingDefaultHeaders(string url, HttpMethod method, Collection<KeyValuePair<string, string>> defaultHeaders, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::GetHttpResponseJObjectUsingDefaultHeaders()");
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

        /// <summary>
        /// Get response object when using content headers in the request.
        /// </summary>
        internal JObject GetHttpResponseJObjectUsingContentHeaders(string url, HttpMethod method, string content, Collection<KeyValuePair<string, string>> contentHeaders, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::GetHttpResponseJObjectUsingContentHeaders()");
            errRecord = null;
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(method, url);

                if (string.IsNullOrEmpty(content))
                {
                    errRecord = new ErrorRecord(
                    exception: new ArgumentNullException($"Content is null or empty and cannot be used to make a request as its content headers."),
                    "RequestContentHeadersNullOrEmpty",
                    ErrorCategory.InvalidData,
                    _cmdletPassedIn);

                    return null;
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

        /// <summary>
        /// Get response headers.
        /// </summary>
        internal async Task<HttpResponseHeaders> GetHttpResponseHeader(string url, HttpMethod method, Collection<KeyValuePair<string, string>> defaultHeaders)
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

        /// <summary>
        /// Set default headers for HttpClient.
        /// </summary>
        private void SetDefaultHeaders(Collection<KeyValuePair<string, string>> defaultHeaders)
        {
            _sessionClient.DefaultRequestHeaders.Clear();
            if (defaultHeaders != null)
            {
                foreach (var header in defaultHeaders)
                {
                    if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        _sessionClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", header.Value);
                    }
                    else if (string.Equals(header.Key, "Accept", StringComparison.OrdinalIgnoreCase))
                    {
                        _sessionClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(header.Value));
                    }
                    else
                    {
                        _sessionClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Sends request for content.
        /// </summary>
        private async Task<HttpContent> SendContentRequestAsync(HttpRequestMessage message)
        {
            try
            {
                HttpResponseMessage response = await _sessionClient.SendAsync(message);
                response.EnsureSuccessStatusCode();
                return response.Content;
            }
            catch (Exception e)
            {
                throw new SendRequestException($"Error occured while sending request to Container Registry server for content with: {e.GetType()} '{e.Message}'", e);
            }
        }

        /// <summary>
        /// Sends HTTP request.
        /// </summary>
        private async Task<JObject> SendRequestAsync(HttpRequestMessage message)
        {
            HttpResponseMessage response;
            try
            {
                response = await _sessionClient.SendAsync(message);
            }
            catch (Exception e)
            {
                throw new SendRequestException($"Error occured while sending request to Container Registry server with: {e.GetType()} '{e.Message}'", e);
            }

            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    break;

                case HttpStatusCode.Unauthorized:
                    throw new UnauthorizedException($"Response returned status code: {response.ReasonPhrase}.");

                case HttpStatusCode.NotFound:
                    throw new ResourceNotFoundException($"Response returned status code package: {response.ReasonPhrase}.");

                default:
                    throw new Exception($"Response returned error with status code {response.StatusCode}: {response.ReasonPhrase}.");
            }

            return JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync());
        }

        /// <summary>
        /// Send request to get response headers.
        /// </summary>
        private async Task<HttpResponseHeaders> SendRequestHeaderAsync(HttpRequestMessage message)
        {
            try
            {
                HttpResponseMessage response = await _sessionClient.SendAsync(message);
                response.EnsureSuccessStatusCode();
                return response.Headers;
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to retrieve response: " + e.Message);
            }
        }

        /// <summary>
        /// Sends a PUT request, used for publishing to container registry.
        /// </summary>
        private async Task<HttpResponseMessage> PutRequestAsync(string url, string filePath, bool isManifest, Collection<KeyValuePair<string, string>> contentHeaders)
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

                    return await _sessionClient.PutAsync(url, httpContent);
                }
            }
            catch (Exception e)
            {
                throw new SendRequestException($"Error occured while uploading module to ContainerRegistry: {e.GetType()} '{e.Message}'", e);
            }
        }

        /// <summary>
        /// Get the default headers associated with the access token.
        /// </summary>
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

        #endregion

        #region Publish Methods

        /// <summary>
        /// Helper method that publishes a package to the container registry.
        /// This gets called from Publish-PSResource.
        /// </summary>
        internal bool PushNupkgContainerRegistry(string psd1OrPs1File,
            string outputNupkgDir,
            string packageName,
            string modulePrefix,
            NuGetVersion packageVersion,
            ResourceType resourceType,
            Hashtable parsedMetadataHash,
            Hashtable dependencies,
            out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::PushNupkgContainerRegistry()");
            string fullNupkgFile = System.IO.Path.Combine(outputNupkgDir, packageName + "." + packageVersion.ToNormalizedString() + ".nupkg");

            string pkgNameForUpload = string.IsNullOrEmpty(modulePrefix) ? packageName : modulePrefix + "/" + packageName;
            string packageNameLowercase = pkgNameForUpload.ToLower();

            // Get access token (includes refresh tokens)
            _cmdletPassedIn.WriteVerbose($"Get access token for container registry server.");
            var containerRegistryAccessToken = GetContainerRegistryAccessToken(out errRecord);
            if (errRecord != null)
            {
                return false;
            }

            // Upload .nupkg
            _cmdletPassedIn.WriteVerbose($"Upload .nupkg file: {fullNupkgFile}");
            string nupkgDigest = UploadNupkgFile(packageNameLowercase, containerRegistryAccessToken, fullNupkgFile, out errRecord);
            if (errRecord != null)
            {
                return false;
            }

            // Create and upload an empty file-- needed by ContainerRegistry server
            CreateAndUploadEmptyFile(outputNupkgDir, packageNameLowercase, containerRegistryAccessToken, out errRecord);
            if (errRecord != null)
            {
                return false;
            }

            // Create config.json file
            var configFilePath = System.IO.Path.Combine(outputNupkgDir, "config.json");
            _cmdletPassedIn.WriteVerbose($"Create config.json file at path: {configFilePath}");
            string configDigest = CreateConfigFile(configFilePath, out errRecord);
            if (errRecord != null)
            {
                return false;
            }

            _cmdletPassedIn.WriteVerbose("Create package version metadata as JSON string");
            // Create module metadata string
            string metadataJson = CreateMetadataContent(resourceType, parsedMetadataHash, out errRecord);
            if (errRecord != null)
            {
                return false;
            }

            // Create and upload manifest
            TryCreateAndUploadManifest(fullNupkgFile, nupkgDigest, configDigest, packageName, modulePrefix, resourceType, metadataJson, configFilePath, packageVersion, containerRegistryAccessToken, out errRecord);
            if (errRecord != null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Upload the nupkg file, by creating a digest for it and uploading as blob.
        /// Note: ContainerRegistry registries will only accept a name that is all lowercase.
        /// </summary>
        private string UploadNupkgFile(string packageNameLowercase, string containerRegistryAccessToken, string fullNupkgFile, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::UploadNupkgFile()");
            _cmdletPassedIn.WriteVerbose("Start uploading blob");
            string nupkgDigest = string.Empty;
            errRecord = null;
            string moduleLocation;
            try
            {
                moduleLocation = GetStartUploadBlobLocation(packageNameLowercase, containerRegistryAccessToken).Result;
            }
            catch (Exception startUploadException)
            {
                errRecord = new ErrorRecord(
                        startUploadException,
                        "StartUploadBlobLocationError",
                        ErrorCategory.InvalidResult,
                        _cmdletPassedIn);

                return nupkgDigest;
            }

            _cmdletPassedIn.WriteVerbose("Computing digest for .nupkg file");
            nupkgDigest = CreateDigest(fullNupkgFile, out errRecord);
            if (errRecord != null)
            {
                return nupkgDigest;
            }

            _cmdletPassedIn.WriteVerbose("Finish uploading blob");
            try
            {
                var responseNupkg = EndUploadBlob(moduleLocation, fullNupkgFile, nupkgDigest, isManifest: false, containerRegistryAccessToken).Result;
                bool uploadSuccessful = responseNupkg.IsSuccessStatusCode;

                if (!uploadSuccessful)
                {
                    errRecord = new ErrorRecord(
                    new UploadBlobException("Uploading of blob for publish failed."),
                    "EndUploadBlobError",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);

                    return nupkgDigest;
                }
            }
            catch (Exception endUploadException)
            {
                errRecord = new ErrorRecord(
                    endUploadException,
                    "EndUploadBlobError",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);

                return nupkgDigest;
            }

            return nupkgDigest;
        }

        /// <summary>
        /// Uploads an empty file at the start of publish as is needed.
        /// </summary>
        private void CreateAndUploadEmptyFile(string outputNupkgDir, string pkgNameLower, string containerRegistryAccessToken, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::CreateAndUploadEmptyFile()");
            _cmdletPassedIn.WriteVerbose("Create an empty file");
            string emptyFileName = "empty" + Guid.NewGuid().ToString() + ".txt";
            var emptyFilePath = System.IO.Path.Combine(outputNupkgDir, emptyFileName);

            try
            {
                Utils.CreateFile(emptyFilePath);

                _cmdletPassedIn.WriteVerbose("Start uploading an empty file");
                string emptyLocation = GetStartUploadBlobLocation(pkgNameLower, containerRegistryAccessToken).Result;

                _cmdletPassedIn.WriteVerbose("Computing digest for empty file");
                string emptyFileDigest = CreateDigest(emptyFilePath, out errRecord);
                if (errRecord != null)
                {
                    return;
                }

                _cmdletPassedIn.WriteVerbose("Finish uploading empty file");
                var emptyResponse = EndUploadBlob(emptyLocation, emptyFilePath, emptyFileDigest, false, containerRegistryAccessToken).Result;
                bool uploadSuccessful = emptyResponse.IsSuccessStatusCode;

                if (!uploadSuccessful)
                {
                    errRecord = new ErrorRecord(
                        new UploadBlobException($"Error occurred while uploading blob, response code was: {emptyResponse.StatusCode} with reason {emptyResponse.ReasonPhrase}"),
                        "UploadEmptyFileError",
                        ErrorCategory.InvalidResult,
                        _cmdletPassedIn);

                    return;
                }
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    e,
                    "UploadEmptyFileError",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);

                return;
            }
        }

        /// <summary>
        /// Create config file associated with the package (i.e repository in container registry terms) as is needed for the package's manifest config layer
        /// </summary>
        private string CreateConfigFile(string configFilePath, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::CreateConfigFile()");
            string configFileDigest = string.Empty;
            _cmdletPassedIn.WriteVerbose("Create the config file");
            while (File.Exists(configFilePath))
            {
                configFilePath = Guid.NewGuid().ToString() + ".json";
            }

            try
            {
                Utils.CreateFile(configFilePath);

                _cmdletPassedIn.WriteVerbose("Computing digest for config");
                configFileDigest = CreateDigest(configFilePath, out errRecord);
                if (errRecord != null)
                {
                    return configFileDigest;
                }
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    e,
                    "CreateConfigFileError",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);

                return configFileDigest;
            }

            return configFileDigest;
        }

        /// <summary>
        /// Create the manifest for the package and upload it
        /// </summary>
        private bool TryCreateAndUploadManifest(string fullNupkgFile,
            string nupkgDigest,
            string configDigest,
            string packageName,
            string modulePrefix,
            ResourceType resourceType,
            string metadataJson,
            string configFilePath,
            NuGetVersion pkgVersion,
            string containerRegistryAccessToken,
            out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::TryCreateAndUploadManifest()");
            errRecord = null;

            string pkgNameForUpload = string.IsNullOrEmpty(modulePrefix) ? packageName : modulePrefix + "/" + packageName;
            string packageNameLowercase = pkgNameForUpload.ToLower();

            FileInfo nupkgFile = new FileInfo(fullNupkgFile);
            var fileSize = nupkgFile.Length;
            var fileName = System.IO.Path.GetFileName(fullNupkgFile);
            string fileContent = CreateManifestContent(nupkgDigest, configDigest, fileSize, fileName, packageName, resourceType, metadataJson);
            File.WriteAllText(configFilePath, fileContent);

            _cmdletPassedIn.WriteVerbose("Create the manifest layer");
            bool manifestCreated = false;
            try
            {
                HttpResponseMessage manifestResponse = UploadManifest(packageNameLowercase, pkgVersion.OriginalVersion, configFilePath, true, containerRegistryAccessToken).Result;
                manifestCreated = manifestResponse.IsSuccessStatusCode;
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    new UploadBlobException($"Error occured while uploading package manifest to ContainerRegistry: {e.GetType()} '{e.Message}'", e),
                    "PackageManifestUploadError",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);

                return manifestCreated;
            }

            return manifestCreated;
        }

        /// <summary>
        /// Create the content for the manifest for the packge.
        /// </summary>
        private string CreateManifestContent(
            string nupkgDigest,
            string configDigest,
            long nupkgFileSize,
            string fileName,
            string packageName,
            ResourceType resourceType,
            string metadata)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::CreateManifestContent()");
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

        /// <summary>
        /// Create SHA256 digest that will be associated with .nupkg, config file or empty file.
        /// </summary>
        private string CreateDigest(string fileName, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::CreateDigest()");
            errRecord = null;
            string digest = string.Empty;
            FileInfo fileInfo = new FileInfo(fileName);
            SHA256 mySHA256 = SHA256.Create();

            using (FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read))
            {
                try
                {
                    // Create a fileStream for the file.
                    // Be sure it's positioned to the beginning of the stream.
                    fileStream.Position = 0;
                    // Compute the hash of the fileStream.
                    byte[] hashValue = mySHA256.ComputeHash(fileStream);
                    StringBuilder stringBuilder = new StringBuilder();
                    foreach (byte b in hashValue)
                    {
                        stringBuilder.AppendFormat("{0:x2}", b);
                    }

                    digest = stringBuilder.ToString();
                }
                catch (IOException ex)
                {
                    errRecord = new ErrorRecord(ex, $"IOException for .nupkg file: {ex.Message}", ErrorCategory.InvalidOperation, null);
                    return digest;
                }
                catch (UnauthorizedAccessException ex)
                {
                    errRecord = new ErrorRecord(ex, $"UnauthorizedAccessException for .nupkg file: {ex.Message}", ErrorCategory.PermissionDenied, null);
                    return digest;
                }
                catch (Exception ex)
                {
                    errRecord = new ErrorRecord(ex, $"Exception when creating digest: {ex.Message}", ErrorCategory.PermissionDenied, null);
                    return digest;
                }
            }

            if (String.IsNullOrEmpty(digest))
            {
                errRecord = new ErrorRecord(new ArgumentNullException("Digest created was null or empty."), "DigestNullOrEmptyError.", ErrorCategory.InvalidResult, null);
            }

            return digest;
        }

        /// <summary>
        /// Create metadata for the package that will be populated in the manifest.
        /// </summary>
        private string CreateMetadataContent(ResourceType resourceType, Hashtable parsedMetadata, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::CreateMetadataContent()");
            errRecord = null;
            string jsonString = string.Empty;

            if (parsedMetadata == null || parsedMetadata.Count == 0)
            {
                errRecord = new ErrorRecord(
                    new ArgumentException("Hashtable created from .ps1 or .psd1 containing package metadata was null or empty"),
                    "MetadataHashtableEmptyError",
                    ErrorCategory.InvalidArgument,
                    _cmdletPassedIn);

                return jsonString;
            }

            _cmdletPassedIn.WriteVerbose("Serialize JSON into string.");

            if (parsedMetadata.ContainsKey("Version") && parsedMetadata["Version"] is NuGetVersion pkgNuGetVersion)
            {
                // For scripts, 'Version' entry will be present in hashtable and if it is of type NuGetVersion do not serialize NuGetVersion
                // as this will populate more metadata than is needed and makes it harder to deserialize later.
                // For modules, 'ModuleVersion' entry will already be present as type string which is correct.
                parsedMetadata.Remove("Version");
                parsedMetadata["Version"] = pkgNuGetVersion.ToString();
            }

            try
            {
                jsonString = System.Text.Json.JsonSerializer.Serialize(parsedMetadata);
            }
            catch (Exception ex)
            {
                errRecord = new ErrorRecord(ex, "JsonSerializationError", ErrorCategory.InvalidResult, _cmdletPassedIn);
                return jsonString;
            }

            return jsonString;
        }

        /// <summary>
        /// Get start location when uploading blob, used during publish.
        /// </summary>
        internal async Task<string> GetStartUploadBlobLocation(string packageName, string containerRegistryAccessToken)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::GetStartUploadBlobLocation()");
            try
            {
                var defaultHeaders = GetDefaultHeaders(containerRegistryAccessToken);
                var startUploadUrl = string.Format(containerRegistryStartUploadTemplate, Registry, packageName);
                return (await GetHttpResponseHeader(startUploadUrl, HttpMethod.Post, defaultHeaders)).Location.ToString();
            }
            catch (Exception e)
            {
                throw new UploadBlobException($"Error occured while starting to upload the blob location used for publishing to ContainerRegistry: {e.GetType()} '{e.Message}'", e);
            }
        }

        /// <summary>
        /// Upload blob, used for publishing
        /// </summary>
        internal async Task<HttpResponseMessage> EndUploadBlob(string location, string filePath, string digest, bool isManifest, string containerRegistryAccessToken)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::EndUploadBlob()");
            try
            {
                var endUploadUrl = string.Format(containerRegistryEndUploadTemplate, Registry, location, digest);
                var defaultHeaders = GetDefaultHeaders(containerRegistryAccessToken);
                return await PutRequestAsync(endUploadUrl, filePath, isManifest, defaultHeaders);
            }
            catch (Exception e)
            {
                throw new UploadBlobException($"Error occured while uploading module to ContainerRegistry: {e.GetType()} '{e.Message}'", e);
            }
        }

        #endregion

        #region Find Helper Methods

        /// <summary>
        /// Helper method for find scenarios.
        /// </summary>
        private Hashtable[] FindPackagesWithVersionHelper(string packageName, VersionType versionType, VersionRange versionRange, NuGetVersion requiredVersion, bool includePrerelease, bool getOnlyLatest, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindPackagesWithVersionHelper()");
            string accessToken = string.Empty;
            string tenantID = string.Empty;
            string registryUrl = Repository.Uri.ToString();
            string packageNameLowercase = packageName.ToLower();

            string containerRegistryAccessToken = GetContainerRegistryAccessToken(out errRecord);
            if (errRecord != null)
            {
                return emptyHashResponses;
            }

            var foundTags = FindContainerRegistryImageTags(packageNameLowercase, "*", containerRegistryAccessToken, out errRecord);
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
                Hashtable metadata = GetContainerRegistryMetadata(packageNameLowercase, exactTagVersion, containerRegistryAccessToken, out errRecord);
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

        /// <summary>
        /// Helper method used for find scenarios that resolves versions required from all versions found.
        /// </summary>
        private SortedDictionary<NuGet.Versioning.SemanticVersion, string> GetPackagesWithRequiredVersion(List<JToken> allPkgVersions, VersionType versionType, VersionRange versionRange, NuGetVersion specificVersion, string packageName, bool includePrerelease, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::GetPackagesWithRequiredVersion()");
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
