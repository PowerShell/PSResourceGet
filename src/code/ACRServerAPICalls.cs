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
using NuGet.Packaging;

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
        public FindResponseType acrFindResponseType = FindResponseType.ResponseString;

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

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
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

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
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

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
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
            string accessToken = string.Empty;
            string tenantID = string.Empty;

            // Need to set up secret management vault before hand
            var repositoryCredentialInfo = Repository.CredentialInfo;
            if (repositoryCredentialInfo != null)
            {
                accessToken = Utils.GetACRAccessTokenFromSecretManagement(
                    Repository.Name,
                    repositoryCredentialInfo,
                    _cmdletPassedIn);

                _cmdletPassedIn.WriteVerbose("Access token retrieved.");

                tenantID = repositoryCredentialInfo.SecretName;
                _cmdletPassedIn.WriteVerbose($"Tenant ID: {tenantID}");
            }

            // Call asynchronous network methods in a try/catch block to handle exceptions.
            string registry = Repository.Uri.Host;

            _cmdletPassedIn.WriteVerbose("Getting acr refresh token");
            var acrRefreshToken = GetAcrRefreshToken(registry, tenantID, accessToken, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: new string[] { }, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
            }

            _cmdletPassedIn.WriteVerbose("Getting acr access token");
            var acrAccessToken = GetAcrAccessToken(registry, acrRefreshToken, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: new string[] { }, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
            }

            _cmdletPassedIn.WriteVerbose("Getting tags");
            var foundTags = FindAcrImageTags(registry, packageName, "*", acrAccessToken, out errRecord);
            if (errRecord != null || foundTags == null)
            {
                return new FindResults(stringResponse: new string[] { }, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
            }

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
            List<Hashtable> latestVersionResponse = new List<Hashtable>();
            List<JToken> allVersionsList = foundTags["tags"].ToList();
            allVersionsList.Reverse();

            foreach (var packageVersion in allVersionsList)
            {
                var packageVersionStr = packageVersion.ToString();
                using (JsonDocument pkgVersionEntry = JsonDocument.Parse(packageVersionStr))
                {
                    JsonElement rootDom = pkgVersionEntry.RootElement;
                    if (!rootDom.TryGetProperty("name", out JsonElement pkgVersionElement))
                    {
                        errRecord = new ErrorRecord(
                            new InvalidOrEmptyResponse($"Response does not contain version element ('name') for package '{packageName}' in '{Repository.Name}'."),
                            "FindNameFailure",
                            ErrorCategory.InvalidResult,
                            this);

                        return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
                    }

                    if (NuGetVersion.TryParse(pkgVersionElement.ToString(), out NuGetVersion pkgVersion))
                    {
                        _cmdletPassedIn.WriteDebug($"'{packageName}' version parsed as '{pkgVersion}'");
                        if (!pkgVersion.IsPrerelease || includePrerelease)
                        {
                            // Versions are always in descending order i.e 5.0.0, 3.0.0, 1.0.0 so grabbing the first match suffices
                            latestVersionResponse.Add(new Hashtable() { { packageName, packageVersionStr } });

                            break;
                        }
                    }
                }
            }

            return new FindResults(stringResponse: new string[] {}, hashtableResponse: latestVersionResponse.ToArray(), responseType: acrFindResponseType);
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

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);

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

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
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

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
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
            string accessToken = string.Empty;
            string tenantID = string.Empty;

            // Need to set up secret management vault beforehand
            var repositoryCredentialInfo = Repository.CredentialInfo;
            if (repositoryCredentialInfo != null)
            {
                accessToken = Utils.GetACRAccessTokenFromSecretManagement(
                    Repository.Name,
                    repositoryCredentialInfo,
                    _cmdletPassedIn);

                _cmdletPassedIn.WriteVerbose("Access token retrieved.");

                tenantID = repositoryCredentialInfo.SecretName;
                _cmdletPassedIn.WriteVerbose($"Tenant ID: {tenantID}");
            }

            string registry = Repository.Uri.Host;

            _cmdletPassedIn.WriteVerbose("Getting acr refresh token");
            var acrRefreshToken = GetAcrRefreshToken(registry, tenantID, accessToken, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: new string[] { }, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
            }

            _cmdletPassedIn.WriteVerbose("Getting acr access token");
            var acrAccessToken = GetAcrAccessToken(registry, acrRefreshToken, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: new string[] { }, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
            }

            _cmdletPassedIn.WriteVerbose("Getting tags");
            var foundTags = FindAcrImageTags(registry, packageName, "*", acrAccessToken, out errRecord);
            if (errRecord != null || foundTags == null)
            {
                return new FindResults(stringResponse: new string[] { }, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
            }

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
            List<Hashtable> latestVersionResponse = new List<Hashtable>();
            List<JToken> allVersionsList = foundTags["tags"].ToList();
            foreach (var packageVersion in allVersionsList)
            {
                var packageVersionStr = packageVersion.ToString();
                using (JsonDocument pkgVersionEntry = JsonDocument.Parse(packageVersionStr))
                {
                    JsonElement rootDom = pkgVersionEntry.RootElement;
                    if (!rootDom.TryGetProperty("name", out JsonElement pkgVersionElement))
                    {
                        errRecord = new ErrorRecord(
                            new InvalidOrEmptyResponse($"Response does not contain version element ('name') for package '{packageName}' in '{Repository.Name}'."),
                            "FindNameFailure",
                            ErrorCategory.InvalidResult,
                            this);

                        return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
                    }

                    if (NuGetVersion.TryParse(pkgVersionElement.ToString(), out NuGetVersion pkgVersion))
                    {
                        _cmdletPassedIn.WriteDebug($"'{packageName}' version parsed as '{pkgVersion}'");
                        if (versionRange.Satisfies(pkgVersion))
                        {
                            if (!includePrerelease && pkgVersion.IsPrerelease == true)
                            {
                                _cmdletPassedIn.WriteDebug($"Prerelease version '{pkgVersion}' found, but not included.");
                                continue;
                            }

                            latestVersionResponse.Add(new Hashtable() { { packageName, packageVersionStr } });
                        }
                    }
                }
            }

            return new FindResults(stringResponse: new string[] { }, hashtableResponse: latestVersionResponse.ToArray(), responseType: acrFindResponseType);
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
            if (!NuGetVersion.TryParse(version, out NuGetVersion requiredVersion))
            {
                errRecord = new ErrorRecord(
                    new ArgumentException($"Version {version} to be found is not a valid NuGet version."),
                    "FindNameFailure",
                    ErrorCategory.InvalidArgument,
                    this);

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
            }
            _cmdletPassedIn.WriteDebug($"'{packageName}' version parsed as '{requiredVersion}'");

            string accessToken = string.Empty;
            string tenantID = string.Empty;

            // Need to set up secret management vault beforehand
            var repositoryCredentialInfo = Repository.CredentialInfo;
            if (repositoryCredentialInfo != null)
            {
                accessToken = Utils.GetACRAccessTokenFromSecretManagement(
                    Repository.Name,
                    repositoryCredentialInfo,
                    _cmdletPassedIn);

                _cmdletPassedIn.WriteVerbose("Access token retrieved.");

                tenantID = repositoryCredentialInfo.SecretName;
                _cmdletPassedIn.WriteVerbose($"Tenant ID: {tenantID}");
            }

            string registry = Repository.Uri.Host;

            _cmdletPassedIn.WriteVerbose("Getting acr refresh token");
            var acrRefreshToken = GetAcrRefreshToken(registry, tenantID, accessToken, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: new string[] { }, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
            }

            _cmdletPassedIn.WriteVerbose("Getting acr access token");
            var acrAccessToken = GetAcrAccessToken(registry, acrRefreshToken, out errRecord);
            if (errRecord != null)
            {
                return new FindResults(stringResponse: new string[] { }, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
            }

            _cmdletPassedIn.WriteVerbose("Getting tags");
            var foundTags = FindAcrImageTags(registry, packageName, requiredVersion.ToString(), acrAccessToken, out errRecord);
            if (errRecord != null || foundTags == null)
            {
                return new FindResults(stringResponse: new string[] { }, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
            }

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
            List<Hashtable> requiredVersionResponse = new List<Hashtable>();

            var packageVersionStr = foundTags["tag"].ToString();
            using (JsonDocument pkgVersionEntry = JsonDocument.Parse(packageVersionStr))
            {
                JsonElement rootDom = pkgVersionEntry.RootElement;
                if (!rootDom.TryGetProperty("name", out JsonElement pkgVersionElement))
                {
                    errRecord = new ErrorRecord(
                        new InvalidOrEmptyResponse($"Response does not contain version element ('name') for package '{packageName}' in '{Repository.Name}'."),
                        "FindNameFailure",
                        ErrorCategory.InvalidResult,
                        this);

                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
                }

                if (NuGetVersion.TryParse(pkgVersionElement.ToString(), out NuGetVersion pkgVersion))
                {
                    _cmdletPassedIn.WriteDebug($"'{packageName}' version parsed as '{pkgVersion}'");

                    if (pkgVersion == requiredVersion)
                    {
                        requiredVersionResponse.Add(new Hashtable() { { packageName, packageVersionStr } });
                    }
                }
            }

            return new FindResults(stringResponse: new string[] { }, hashtableResponse: requiredVersionResponse.ToArray(), responseType: acrFindResponseType);
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

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: acrFindResponseType);
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
            _cmdletPassedIn.WriteDebug("In ACRServerAPICalls::InstallPackage()");
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
            string moduleName,
            out ErrorRecord errRecord)
        {
            errRecord = null;
            string accessToken = string.Empty;
            string tenantID = string.Empty;
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            string moduleVersion = String.Empty;

            var repositoryCredentialInfo = Repository.CredentialInfo;
            if (repositoryCredentialInfo != null)
            {
                accessToken = Utils.GetACRAccessTokenFromSecretManagement(
                    Repository.Name,
                    repositoryCredentialInfo,
                    _cmdletPassedIn);

                _cmdletPassedIn.WriteVerbose("Access token retrieved.");

                tenantID = repositoryCredentialInfo.SecretName;
                _cmdletPassedIn.WriteVerbose($"Tenant ID: {tenantID}");
            }

            // Call asynchronous network methods in a try/catch block to handle exceptions.
            string registry = Repository.Uri.Host;

            _cmdletPassedIn.WriteVerbose("Getting acr refresh token");
            var acrRefreshToken = GetAcrRefreshToken(registry, tenantID, accessToken, out errRecord);
            if (errRecord != null)
            {
                return null;
            }

            _cmdletPassedIn.WriteVerbose("Getting acr access token");
            var acrAccessToken = GetAcrAccessToken(registry, acrRefreshToken, out errRecord);
            if (errRecord != null)
            {
                return null;
            }

            _cmdletPassedIn.WriteVerbose($"Getting manifest for {moduleName} - {moduleVersion}");
            var manifest = GetAcrRepositoryManifestAsync(registry, moduleName, moduleVersion, acrAccessToken, out errRecord);
            if (errRecord != null)
            {
                return null;
            }
            string digest = GetDigestFromManifest(manifest, out errRecord);
            if (errRecord != null)
            {
                return null;
            }

            _cmdletPassedIn.WriteVerbose($"Downloading blob for {moduleName} - {moduleVersion}");
            // TODO: error handling here?
            var responseContent = GetAcrBlobAsync(registry, moduleName, digest, acrAccessToken).Result;

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

        internal string GetAcrRefreshToken(string registry, string tenant, string accessToken, out ErrorRecord errRecord)
        {
            string content = string.Format(acrRefreshTokenTemplate, registry, tenant, accessToken);
            var contentHeaders = new Collection<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Content-Type", "application/x-www-form-urlencoded") };
            string exchangeUrl = string.Format(acrOAuthExchangeUrlTemplate, registry);
            var results = GetHttpResponseJObjectUsingContentHeaders(exchangeUrl, HttpMethod.Post, content, contentHeaders, out errRecord);
            
            if (results != null && results["refresh_token"] != null)
            {
                return results["refresh_token"].ToString();
            }

            return string.Empty;
        }

        internal string GetAcrAccessToken(string registry, string refreshToken, out ErrorRecord errRecord)
        {
            string content = string.Format(acrAccessTokenTemplate, registry, refreshToken);
            var contentHeaders = new Collection<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Content-Type", "application/x-www-form-urlencoded") };
            string tokenUrl = string.Format(acrOAuthTokenUrlTemplate, registry);
            var results = GetHttpResponseJObjectUsingContentHeaders(tokenUrl, HttpMethod.Post, content, contentHeaders, out errRecord);

            if (results != null && results["access_token"] != null)
            { 
                return results["access_token"].ToString();
            }

            return string.Empty;
        }

        internal JObject GetAcrRepositoryManifestAsync(string registry, string packageName, string version, string acrAccessToken, out ErrorRecord errRecord)
        {
            // the packageName parameter here maps to repositoryName in ACR, but to not conflict with PSGet definition of repository we will call it packageName
            // example of manifestUrl: https://psgetregistry.azurecr.io/hello-world:3.0.0
            string manifestUrl = string.Format(acrManifestUrlTemplate, registry, packageName, version);

            var defaultHeaders = GetDefaultHeaders(acrAccessToken);
            return GetHttpResponseJObjectUsingDefaultHeaders(manifestUrl, HttpMethod.Get, defaultHeaders, out errRecord);
        }

        internal async Task<HttpContent> GetAcrBlobAsync(string registry, string repositoryName, string digest, string acrAccessToken)
        {
            string blobUrl = string.Format(acrBlobDownloadUrlTemplate, registry, repositoryName, digest);
            var defaultHeaders = GetDefaultHeaders(acrAccessToken);
            return await GetHttpContentResponseJObject(blobUrl, defaultHeaders);
        }

        internal JObject FindAcrImageTags(string registry, string repositoryName, string version, string acrAccessToken, out ErrorRecord errRecord)
        {
            try
            {
                string resolvedVersion = string.Equals(version, "*", StringComparison.OrdinalIgnoreCase) ? null : $"/{version}";
                string findImageUrl = string.Format(acrFindImageVersionUrlTemplate, registry, repositoryName, resolvedVersion);
                var defaultHeaders = GetDefaultHeaders(acrAccessToken);
                return GetHttpResponseJObjectUsingDefaultHeaders(findImageUrl, HttpMethod.Get, defaultHeaders, out errRecord);
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error finding ACR artifact: " + e.Message);
            }
        }

        internal async Task<string> GetStartUploadBlobLocation(string registry, string pkgName, string acrAccessToken)
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

        internal async Task<bool> EndUploadBlob(string registry, string location, string filePath, string digest, bool isManifest, string acrAccessToken)
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

        internal async Task<bool> CreateManifest(string registry, string pkgName, string pkgVersion, string configPath, bool isManifest, string acrAccessToken)
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

        internal bool PushNupkgACR(string psd1OrPs1File, string outputNupkgDir, string pkgName, NuGetVersion pkgVersion, PSRepositoryInfo repository, Hashtable parsedMetadataHash, out ErrorRecord errRecord)
        {
            errRecord = null;
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

                tenantID = repositoryCredentialInfo.SecretName;
                _cmdletPassedIn.WriteVerbose($"Tenant ID: {tenantID}");
            }

            // Call asynchronous network methods in a try/catch block to handle exceptions.
            string registry = repository.Uri.Host;

            _cmdletPassedIn.WriteVerbose("Getting acr refresh token");
            var acrRefreshToken = GetAcrRefreshToken(registry, tenantID, accessToken, out errRecord);
            _cmdletPassedIn.WriteVerbose("Getting acr access token");
            var acrAccessToken = GetAcrAccessToken(registry, acrRefreshToken, out errRecord);

            /* Uploading .nupkg */
            _cmdletPassedIn.WriteVerbose("Start uploading blob");
            var moduleLocation = GetStartUploadBlobLocation(registry, pkgName, acrAccessToken).Result;

            _cmdletPassedIn.WriteVerbose("Computing digest for .nupkg file");
            bool nupkgDigestCreated = CreateDigest(fullNupkgFile, out string nupkgDigest, out ErrorRecord nupkgDigestError);
            if (!nupkgDigestCreated)
            {
                _cmdletPassedIn.ThrowTerminatingError(nupkgDigestError);
            }

            _cmdletPassedIn.WriteVerbose("Finish uploading blob");
            bool moduleUploadSuccess = EndUploadBlob(registry, moduleLocation, fullNupkgFile, nupkgDigest, false, acrAccessToken).Result;

            _cmdletPassedIn.WriteVerbose("Create the config file");
            string configFileName = "config.json";
            var configFilePath = System.IO.Path.Combine(outputNupkgDir, configFileName);
            while (File.Exists(configFilePath))
            {
                configFilePath = Guid.NewGuid().ToString() + ".json";
            }
            FileStream configStream = File.Create(configFilePath);
            configStream.Close();
            _cmdletPassedIn.WriteVerbose("Computing digest for config");
            bool configDigestCreated = CreateDigest(configFilePath, out string configDigest, out ErrorRecord configDigestError);
            if (!configDigestCreated)
            {
                _cmdletPassedIn.ThrowTerminatingError(configDigestError);
            }

            /* Create manifest layer */
            _cmdletPassedIn.WriteVerbose("Create package version metadata as JSON string");
            string jsonString = CreateMetadataContent(psd1OrPs1File, parsedMetadataHash, out ErrorRecord metadataCreationError);
            if (metadataCreationError != null)
            {
                _cmdletPassedIn.ThrowTerminatingError(metadataCreationError);
            }

            FileInfo nupkgFile = new FileInfo(fullNupkgFile);
            var fileSize = nupkgFile.Length;
            var fileName = System.IO.Path.GetFileName(fullNupkgFile);
            string fileContent = CreateJsonContent(nupkgDigest, configDigest, configDigest, fileSize, fileName, jsonString);
            File.WriteAllText(configFilePath, fileContent);

            _cmdletPassedIn.WriteVerbose("Create the manifest layer");
            bool manifestCreated = CreateManifest(registry, pkgName, pkgVersion.OriginalVersion, configFilePath, true, acrAccessToken).Result;

            if (manifestCreated)
            {
                return true;
            }
            return false;
        }

        private string CreateJsonContent(
            string nupkgDigest, 
            string configDigest, 
            string metadataDigest, 
            long nupkgFileSize, 
            string fileName, 
            string jsonString)
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
            jsonWriter.WriteValue($"sha256:{configDigest}");
            jsonWriter.WritePropertyName("size");
            jsonWriter.WriteValue(0);
            jsonWriter.WriteEndObject();

            jsonWriter.WritePropertyName("layers");
            jsonWriter.WriteStartArray();

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("mediaType");
            jsonWriter.WriteValue("application/vnd.oci.image.layer.nondistributable.v1.tar+gzip'");
            jsonWriter.WritePropertyName("digest");
            jsonWriter.WriteValue($"sha256:{nupkgDigest}");
            jsonWriter.WritePropertyName("size");
            jsonWriter.WriteValue(nupkgFileSize);
            jsonWriter.WritePropertyName("annotations");
            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("org.opencontainers.image.title");
            jsonWriter.WriteValue(fileName);
            jsonWriter.WritePropertyName("metadata");
            jsonWriter.WriteValue(jsonString);
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

        private string CreateMetadataContent(string manifestFilePath, Hashtable parsedMetadata, out ErrorRecord metadataCreationError)
        {
            metadataCreationError = null;
            Hashtable parsedMetadataHash = null;
            string jsonString = string.Empty;

            // A script will already have the metadata parsed into the parsedMetadatahash,
            // a module will still need the module manifest to be parsed.
            if (parsedMetadata == null || parsedMetadata.Count == 0)
            {
                // Use the parsed module manifest data as 'parsedMetadataHash' instead of the passed-in data.
                if (!Utils.TryReadManifestFile(
                    manifestFilePath: manifestFilePath,
                    manifestInfo: out parsedMetadataHash,
                    error: out Exception manifestReadError))
                {
                    metadataCreationError = new ErrorRecord(
                        manifestReadError,
                        "ManifestFileReadParseForACRPublishError",
                        ErrorCategory.ReadError,
                        _cmdletPassedIn);

                    return jsonString;
                }
            }

            if (parsedMetadataHash == null)
            {
                metadataCreationError = new ErrorRecord(
                    new InvalidOperationException("Error parsing package metadata into hashtable."),
                    "PackageMetadataHashEmptyError",
                    ErrorCategory.InvalidData,
                    _cmdletPassedIn);

                return jsonString;
            }

            _cmdletPassedIn.WriteVerbose("Serialize JSON into string.");
            jsonString = System.Text.Json.JsonSerializer.Serialize(parsedMetadataHash, new JsonSerializerOptions { WriteIndented = true });

            return jsonString;
        }

        #endregion
    }
}
