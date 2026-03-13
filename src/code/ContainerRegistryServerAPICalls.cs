// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.PowerShell.PSResourceGet.Cmdlets;
using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using NuGet.Versioning;
using OrasProject.Oras;
using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;
using OrasRegistry = OrasProject.Oras.Registry.Remote.Registry;
using OrasRepository = OrasProject.Oras.Registry.Remote.Repository;

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
        private static readonly Hashtable[] emptyHashResponses = new Hashtable[] { };
        private static FindResponseType containerRegistryFindResponseType = FindResponseType.ResponseString;
        private static readonly FindResults emptyResponseResults = new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: containerRegistryFindResponseType);

        // ORAS SDK objects
        private readonly HttpClient _httpClient;
        private readonly IClient _orasClient;
        private readonly ICredentialProvider _credentialProvider;
        private readonly IMemoryCache _memoryCache;

        #endregion

        #region Constructor

        public ContainerRegistryServerAPICalls(PSRepositoryInfo repository, PSCmdlet cmdletPassedIn, NetworkCredential networkCredential, string userAgentString) : base(repository, networkCredential)
        {
            Repository = repository;
            Registry = Repository.Uri.Host;
            _cmdletPassedIn = cmdletPassedIn;

            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(10);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgentString);

            _credentialProvider = new PSResourceGetCredentialProvider(repository, cmdletPassedIn);
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _orasClient = new Client(_httpClient, _credentialProvider, new Cache(_memoryCache));
        }

        #endregion

        #region Overridden Methods

        /// <summary>
        /// Find method which allows for searching for all packages from a repository and returns latest version for each.
        /// </summary>
        public override FindResults FindAll(bool includePrerelease, ResourceType type, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindAll()");
            var findResult = FindPackages("*", includePrerelease, out errRecord);
            if (errRecord != null)
            {
                return emptyResponseResults;
            }

            return findResult;
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
        /// Implementation Note: Need to filter further for latest version (prerelease or non-prerelease depending on user preference)
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
            var findResult = FindPackages(packageName, includePrerelease, out errRecord);
            if (errRecord != null)
            {
                return emptyResponseResults;
            }

            return findResult;
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

            string packageNameForInstall = PrependMARPrefix(packageName);
            results = InstallVersion(packageNameForInstall, packageVersion, out errRecord);
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

            try
            {
                // Create ORAS repository for the specific package
                var repo = CreateOrasRepository(packageNameLowercase);

                _cmdletPassedIn.WriteVerbose($"Fetching manifest for {packageNameLowercase} - {packageVersion}");

                // Fetch the manifest by version tag
                var (manifestDescriptor, manifestStream) = repo.FetchAsync(packageVersion).GetAwaiter().GetResult();
                byte[] manifestBytes;
                using (manifestStream)
                {
                    manifestBytes = manifestStream.ReadAllAsync(manifestDescriptor).GetAwaiter().GetResult();
                }

                // Parse the manifest to get the layer descriptor (contains the nupkg blob)
                var manifest = System.Text.Json.JsonSerializer.Deserialize<Manifest>(manifestBytes);
                if (manifest == null || manifest.Layers == null || manifest.Layers.Count == 0)
                {
                    errRecord = new ErrorRecord(
                        exception: new InvalidOperationException($"Manifest for {packageNameLowercase} version {packageVersion} has no layers."),
                        "ManifestNoLayersError",
                        ErrorCategory.InvalidResult,
                        _cmdletPassedIn);
                    return null;
                }

                // The first layer contains the nupkg
                var nupkgLayer = manifest.Layers[0];
                _cmdletPassedIn.WriteVerbose($"Downloading blob for {packageNameLowercase} - {packageVersion} (digest: {nupkgLayer.Digest})");

                // Fetch the blob content
                using var blobStream = repo.FetchAsync(nupkgLayer).GetAwaiter().GetResult();
                var resultStream = new MemoryStream();
                blobStream.CopyTo(resultStream);
                resultStream.Position = 0;
                return resultStream;
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "InstallVersionOrasError",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);

                return null;
            }
        }

        #endregion

        #region ORAS Helper Methods

        /// <summary>
        /// Creates an ORAS Repository instance for the given package name.
        /// </summary>
        private OrasRepository CreateOrasRepository(string packageName)
        {
            string reference = $"{Registry}/{packageName}";
            return new OrasRepository(new RepositoryOptions
            {
                Reference = Reference.Parse(reference),
                Client = _orasClient,
            });
        }

        /// <summary>
        /// Creates an ORAS Registry instance for catalog operations.
        /// </summary>
        private OrasRegistry CreateOrasRegistry()
        {
            return new OrasRegistry(Registry, _orasClient);
        }

        /// <summary>
        /// Lists all tags for a given package using ORAS.
        /// </summary>
        internal List<string> ListImageTags(string packageName, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::ListImageTags()");
            errRecord = null;
            var tags = new List<string>();

            try
            {
                var repo = CreateOrasRepository(packageName);
                var tagsEnumerable = repo.ListTagsAsync("");
                // Collect all tags synchronously
                var enumerator = tagsEnumerable.GetAsyncEnumerator();
                try
                {
                    while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
                    {
                        tags.Add(enumerator.Current);
                    }
                }
                finally
                {
                    enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "ListImageTagsOrasError",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);
            }

            return tags;
        }

        /// <summary>
        /// Lists all repositories in the registry using ORAS.
        /// </summary>
        internal List<string> ListAllRepositories(out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::ListAllRepositories()");
            errRecord = null;
            var repositories = new List<string>();

            try
            {
                var registry = CreateOrasRegistry();
                var repoEnumerable = registry.ListRepositoriesAsync("");
                var enumerator = repoEnumerable.GetAsyncEnumerator();
                try
                {
                    while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
                    {
                        repositories.Add(enumerator.Current);
                    }
                }
                finally
                {
                    enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "ListAllRepositoriesOrasError",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);
            }

            return repositories;
        }

        /// <summary>
        /// Fetches the manifest for a specific package version and parses it.
        /// Returns the manifest as a parsed OCI Manifest object.
        /// </summary>
        internal Manifest FetchManifest(string packageName, string version, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FetchManifest()");
            errRecord = null;

            try
            {
                var repo = CreateOrasRepository(packageName);
                var (descriptor, stream) = repo.FetchAsync(version).GetAwaiter().GetResult();
                byte[] manifestBytes;
                using (stream)
                {
                    manifestBytes = stream.ReadAllAsync(descriptor).GetAwaiter().GetResult();
                }

                var manifest = System.Text.Json.JsonSerializer.Deserialize<Manifest>(manifestBytes);
                return manifest;
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    exception: e,
                    "FetchManifestOrasError",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);

                return null;
            }
        }

        /// <summary>
        /// Get metadata for a package version by fetching its manifest and reading annotations.
        /// </summary>
        internal Hashtable GetContainerRegistryMetadata(string packageName, string exactTagVersion, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::GetContainerRegistryMetadata()");
            Hashtable requiredVersionResponse = new();

            var manifest = FetchManifest(packageName, exactTagVersion, out errRecord);
            if (errRecord != null)
            {
                return requiredVersionResponse;
            }

            ContainerRegistryInfo serverPkgInfo = GetMetadataProperty(manifest, packageName, out errRecord);
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
                    else if (rootDom.TryGetProperty("Version", out pkgVersionElement) || rootDom.TryGetProperty("version", out pkgVersionElement))
                    {
                        // script metadata will have "Version" property, but nupkg only based .nuspec will have lowercase "version" property and JsonElement.TryGetProperty() is case sensitive
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
                            new ArgumentException($"Version {pkgVersionString} to be parsed from metadata is not a valid NuGet version for package '{packageName}'."),
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
        /// Get metadata for the package by parsing its OCI manifest annotations.
        /// </summary>
        internal ContainerRegistryInfo GetMetadataProperty(Manifest manifest, string packageName, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::GetMetadataProperty()");
            errRecord = null;
            ContainerRegistryInfo serverPkgInfo = null;

            if (manifest == null || manifest.Layers == null || manifest.Layers.Count == 0 || manifest.Layers[0] == null)
            {
                errRecord = new ErrorRecord(
                    new InvalidOrEmptyResponse($"Response does not contain 'layers' element in manifest for package '{packageName}' in '{Repository.Name}'."),
                    "GetMetadataPropertyLayersError",
                    ErrorCategory.InvalidData,
                    this);

                return serverPkgInfo;
            }

            var annotations = manifest.Layers[0].Annotations;
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
            if (!annotations.TryGetValue("org.opencontainers.image.title", out string metadataPkgName) || string.IsNullOrWhiteSpace(metadataPkgName))
            {
                errRecord = new ErrorRecord(
                    new InvalidOrEmptyResponse($"Response does not contain or has empty 'org.opencontainers.image.title' element for package '{packageName}' in '{Repository.Name}'."),
                    "GetMetadataPropertyOCITitleError",
                    ErrorCategory.InvalidData,
                    this);

                return serverPkgInfo;
            }

            // Check for package metadata
            if (!annotations.TryGetValue("metadata", out string metadata) || metadata == null)
            {
                errRecord = new ErrorRecord(
                    new InvalidOrEmptyResponse($"Response does not contain 'metadata' element in manifest for package '{packageName}' in '{Repository.Name}'."),
                    "GetMetadataPropertyMetadataError",
                    ErrorCategory.InvalidData,
                    this);

                return serverPkgInfo;
            }

            // Check for package artifact type
            annotations.TryGetValue("resourceType", out string resourceType);
            resourceType = resourceType ?? "None";

            return new ContainerRegistryInfo(metadataPkgName, metadata, resourceType);
        }

        #endregion

        #region Publish Methods

        /// <summary>
        /// Helper method that publishes a package to the container registry.
        /// This gets called from Publish-PSResource.
        /// </summary>
        internal bool PushNupkgContainerRegistry(
            string outputNupkgDir,
            string packageName,
            string modulePrefix,
            NuGetVersion packageVersion,
            ResourceType resourceType,
            Hashtable parsedMetadataHash,
            Hashtable dependencies,
            bool isNupkgPathSpecified,
            string originalNupkgPath,
            out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::PushNupkgContainerRegistry()");
            errRecord = null;

            // if isNupkgPathSpecified, then we need to publish the original .nupkg file, as it may be signed
            string fullNupkgFile = isNupkgPathSpecified ? originalNupkgPath : System.IO.Path.Combine(outputNupkgDir, packageName + "." + packageVersion.ToNormalizedString() + ".nupkg");

            string pkgNameForUpload = string.IsNullOrEmpty(modulePrefix) ? packageName : modulePrefix + "/" + packageName;
            string packageNameLowercase = pkgNameForUpload.ToLower();

            try
            {
                var repo = CreateOrasRepository(packageNameLowercase);

                // Read the nupkg file bytes
                _cmdletPassedIn.WriteVerbose($"Reading .nupkg file: {fullNupkgFile}");
                byte[] nupkgBytes = File.ReadAllBytes(fullNupkgFile);

                // Create metadata JSON string
                _cmdletPassedIn.WriteVerbose("Create package version metadata as JSON string");
                string metadataJson = CreateMetadataContent(resourceType, parsedMetadataHash, out errRecord);
                if (errRecord != null)
                {
                    return false;
                }

                var fileName = System.IO.Path.GetFileName(fullNupkgFile);

                // Create layer descriptor for the nupkg with annotations
                var nupkgDescriptor = Descriptor.Create(nupkgBytes, OrasProject.Oras.Oci.MediaType.ImageLayerGzip);
                nupkgDescriptor.Annotations = new Dictionary<string, string>
                {
                    ["org.opencontainers.image.title"] = packageName,
                    ["org.opencontainers.image.description"] = fileName,
                    ["metadata"] = metadataJson,
                    ["resourceType"] = resourceType.ToString()
                };

                // Push the nupkg layer
                _cmdletPassedIn.WriteVerbose($"Pushing .nupkg blob for {packageNameLowercase}");
                repo.PushAsync(nupkgDescriptor, new MemoryStream(nupkgBytes)).GetAwaiter().GetResult();

                // Create config descriptor
                byte[] configBytes = Array.Empty<byte>();
                var configDescriptor = Descriptor.Create(configBytes, OrasProject.Oras.Oci.MediaType.ImageConfig);

                // Pack and push the manifest using Packer
                _cmdletPassedIn.WriteVerbose("Packing and pushing manifest");
                var packOptions = new PackManifestOptions
                {
                    Config = configDescriptor,
                    Layers = new List<Descriptor> { nupkgDescriptor }
                };

                var manifestDescriptor = Packer.PackManifestAsync(repo, Packer.ManifestVersion.Version1_1, "", packOptions).GetAwaiter().GetResult();

                // Tag the manifest with the version
                _cmdletPassedIn.WriteVerbose($"Tagging manifest with version: {packageVersion.OriginalVersion}");
                repo.TagAsync(manifestDescriptor, packageVersion.OriginalVersion).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                errRecord = new ErrorRecord(
                    new UploadBlobException($"Error occurred while publishing package to ContainerRegistry: {e.GetType()} '{e.Message}'", e),
                    "PackagePublishOrasError",
                    ErrorCategory.InvalidResult,
                    _cmdletPassedIn);

                return false;
            }

            return true;
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

        #endregion

        #region Find Helper Methods

        /// <summary>
        /// Helper method for find scenarios.
        /// </summary>
        private Hashtable[] FindPackagesWithVersionHelper(string packageName, VersionType versionType, VersionRange versionRange, NuGetVersion requiredVersion, bool includePrerelease, bool getOnlyLatest, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindPackagesWithVersionHelper()");
            string packageNameLowercase = packageName.ToLower();

            string packageNameForFind = PrependMARPrefix(packageNameLowercase);

            var allVersionsList = ListImageTags(packageNameForFind, out errRecord);
            if (errRecord != null || allVersionsList == null)
            {
                return emptyHashResponses;
            }

            List<Hashtable> latestVersionResponse = new List<Hashtable>();

            SortedDictionary<NuGet.Versioning.SemanticVersion, string> sortedQualifyingPkgs = GetPackagesWithRequiredVersion(allVersionsList, versionType, versionRange, requiredVersion, packageNameForFind, includePrerelease, out errRecord);
            if (errRecord != null && sortedQualifyingPkgs?.Count == 0)
            {
                _cmdletPassedIn.WriteDebug("No qualifying packages found for the specified criteria.");
                return emptyHashResponses;
            }

            var pkgsInDescendingOrder = sortedQualifyingPkgs.Reverse();

            foreach (var pkgVersionTag in pkgsInDescendingOrder)
            {
                string exactTagVersion = pkgVersionTag.Value.ToString();
                Hashtable metadata = GetContainerRegistryMetadata(packageNameForFind, exactTagVersion, out errRecord);
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
        private SortedDictionary<NuGet.Versioning.SemanticVersion, string> GetPackagesWithRequiredVersion(List<string> allPkgVersions, VersionType versionType, VersionRange versionRange, NuGetVersion specificVersion, string packageName, bool includePrerelease, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::GetPackagesWithRequiredVersion()");
            errRecord = null;
            SortedDictionary<NuGet.Versioning.SemanticVersion, string> sortedPkgs = new SortedDictionary<SemanticVersion, string>(VersionComparer.Default);
            bool isSpecificVersionSearch = versionType == VersionType.SpecificVersion;

            foreach (var pkgVersionString in allPkgVersions)
            {
                // determine if the package version that is a repository tag is a valid NuGetVersion
                if (!NuGetVersion.TryParse(pkgVersionString, out NuGetVersion pkgVersion))
                {
                    errRecord = new ErrorRecord(
                        new ArgumentException($"Version {pkgVersionString} to be parsed from metadata is not a valid NuGet version for package '{packageName}'."),
                        "FindNameFailure",
                        ErrorCategory.InvalidArgument,
                        this);

                    _cmdletPassedIn.WriteError(errRecord);
                    _cmdletPassedIn.WriteDebug($"Skipping package '{packageName}' with version '{pkgVersionString}' as it is not a valid NuGet version.");
                    continue; // skip this version and continue with the next one
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

            return sortedPkgs;
        }

        private string PrependMARPrefix(string packageName)
        {
            string prefix = string.IsNullOrEmpty(InternalHooks.MARPrefix) ? PSRepositoryInfo.MARPrefix : InternalHooks.MARPrefix;

            // If the repostitory is MAR and its not a wildcard search, we need to prefix the package name with MAR prefix.
            string updatedPackageName = Repository.IsMARRepository() && packageName.Trim() != "*"
                                            ? packageName.StartsWith(prefix) ? packageName : string.Concat(prefix, packageName)
                                            : packageName;

            return updatedPackageName;
        }

        private FindResults FindPackages(string packageName, bool includePrerelease, out ErrorRecord errRecord)
        {
            _cmdletPassedIn.WriteDebug("In ContainerRegistryServerAPICalls::FindPackages()");
            errRecord = null;

            var repositoryNames = ListAllRepositories(out errRecord);
            if (errRecord != null)
            {
                return emptyResponseResults;
            }

            List<Hashtable> repositoriesList = new List<Hashtable>();
            var isMAR = Repository.IsMARRepository();

            // Convert the list of repositories to a list of hashtables
            foreach (var repositoryName in repositoryNames)
            {
                if (isMAR && !repositoryName.StartsWith(PSRepositoryInfo.MARPrefix))
                {
                    continue;
                }

                // This remove the 'psresource/' prefix from the repository name for comparison with wildcard.
                string moduleName = repositoryName.StartsWith("psresource/") ? repositoryName.Substring(11) : repositoryName;

                WildcardPattern wildcardPattern = new WildcardPattern(packageName, WildcardOptions.IgnoreCase);

                if (!wildcardPattern.IsMatch(moduleName))
                {
                    continue;
                }

                _cmdletPassedIn.WriteDebug($"Found repository: {repositoryName}");

                repositoriesList.AddRange(FindPackagesWithVersionHelper(repositoryName, VersionType.VersionRange, versionRange: VersionRange.All, requiredVersion: null, includePrerelease, getOnlyLatest: true, out errRecord));
                if (errRecord != null)
                {
                    return emptyResponseResults;
                }
            }

            return new FindResults(stringResponse: new string[] { }, hashtableResponse: repositoriesList.ToArray(), responseType: containerRegistryFindResponseType);
        }

        #endregion
    }
}
