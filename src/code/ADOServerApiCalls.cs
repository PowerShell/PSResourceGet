
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections;
using System.Runtime.ExceptionServices;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    internal class ADOServerApiCalls : ServerApiCall
    {
        #region Members
        public override PSRepositoryInfo Repository { get; set; }
        private HttpClient _sessionClient { get; set; }
        public FindResponseType v3FindResponseType = FindResponseType.ResponseString;
        private static readonly Hashtable[] emptyHashResponses = new Hashtable[]{};
        private static readonly string resourcesName = "resources";
        private static readonly string registrationsBaseUrlName = "RegistrationsBaseUrl";
        private static readonly string itemsName = "items";
        private static readonly string countName = "count";
        private static readonly string versionName = "version";
        private static readonly string tagsName = "tags";
        private static readonly string catalogEntryProperty = "catalogEntry";
        private static readonly string packageContentProperty = "packageContent";

        #endregion

        #region Constructor

        public ADOServerApiCalls(PSRepositoryInfo repository, NetworkCredential networkCredential) : base(repository, networkCredential)
        {
            this.Repository = repository;

            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                Credentials = networkCredential
            };

            _sessionClient = new HttpClient(handler);
        }

        #endregion

        #region Overriden Methods

        /// <summary>
        /// Find method which allows for searching for all packages from a repository and returns latest version for each.
        /// Not supported for ADO repository.
        /// </summary>
        public override FindResults FindAll(bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            string errMsg = $"Find all is not supported for the repository {Repository.Uri}";
            edi = ExceptionDispatchInfo.Capture(new InvalidOperationException(errMsg));

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag(s) from a repository and returns latest version for each.
        /// Not supported for ADO repository.
        /// </summary>
        public override FindResults FindTags(string[] tags, bool includePrerelease, ResourceType _type, out ExceptionDispatchInfo edi)
        {
            string errMsg = $"Find by Tags is not supported for the repository {Repository.Uri}";
            edi = ExceptionDispatchInfo.Capture(new InvalidOperationException(errMsg));

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for packages with specified Command or DSCResource name.
        /// Not supported for ADO repository.
        /// </summary>
        public override FindResults FindCommandOrDscResource(string[] tags, bool includePrerelease, bool isSearchingForCommands, out ExceptionDispatchInfo edi)
        {
            string errMsg = $"Find by CommandName or DSCResource is not supported for {Repository.Name} as it uses the V3 server protocol";
            edi = ExceptionDispatchInfo.Capture(new InvalidOperationException(errMsg));

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "Newtonsoft.Json"
        /// We use the latest RegistrationBaseUrl version resource we can find and check if contains an entry with the package name.
        /// </summary>
        public override FindResults FindName(string packageName, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            return FindNameHelper(packageName, tags: Utils.EmptyStrArray, includePrerelease, type, out edi);
        }

        /// <summary>
        /// Find method which allows for searching for single name and specified tag(s) and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "Newtonsoft.Json" -Tag "json"
        /// We use the latest RegistrationBaseUrl version resource we can find and check if contains an entry with the package name.
        /// </summary>
        public override FindResults FindNameWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            return FindNameHelper(packageName, tags, includePrerelease, type, out edi);
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and returns latest version.
        /// This is not supported for ADO repository.
        /// </summary>
        public override FindResults FindNameGlobbing(string packageName, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            string errMsg = $"Find with Name containing wildcards is not supported for the repository {Repository.Uri}";
            edi = ExceptionDispatchInfo.Capture(new InvalidOperationException(errMsg));

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and tag and returns latest version.
        /// This is not supported for ADO repository.
        /// </summary>
        public override FindResults FindNameGlobbingWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            string errMsg = $"Find with Name containing wildcards is not supported for the repository {Repository.Uri}";
            edi = ExceptionDispatchInfo.Capture(new InvalidOperationException(errMsg));

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with version range.
        /// Name: no wildcard support
        /// Version: supports wildcards
        /// Examples: Search "NuGet.Server.Core" "[1.0.0.0, 5.0.0.0]"
        ///           Search "NuGet.Server.Core" "3.*"
        /// We use the latest RegistrationBaseUrl version resource we can find and check if contains an entry with the package name, then get all versions and match to satisfying versions.
        /// </summary>
        public override FindResults FindVersionGlobbing(string packageName, VersionRange versionRange, bool includePrerelease, ResourceType type, bool getOnlyLatest, out ExceptionDispatchInfo edi)
        {
            string registrationsBaseUrl = FindRegistrationsBaseUrl(out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string[] versionedResponses = GetVersionedResponses(registrationsBaseUrl, packageName, catalogEntryProperty, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            List<string> satisfyingVersions = new List<string>();
            foreach (string response in versionedResponses)
            {
                JsonElement pkgVersionElement;
                try
                {
                    JsonDocument pkgVersionEntry = JsonDocument.Parse(response);
                    JsonElement rootDom = pkgVersionEntry.RootElement;
                    if (!rootDom.TryGetProperty("version", out pkgVersionElement))
                    {
                        edi = ExceptionDispatchInfo.Capture(new InvalidOrEmptyResponse($"Response does not contain 'version' element."));
                        return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                    }
                }
                catch (Exception e)
                {
                    edi = ExceptionDispatchInfo.Capture(e);
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                }

                if (NuGetVersion.TryParse(pkgVersionElement.ToString(), out NuGetVersion pkgVersion) && versionRange.Satisfies(pkgVersion))
                {
                    if (!pkgVersion.IsPrerelease || includePrerelease)
                    {
                        satisfyingVersions.Add(response);
                    }
                }
            }

            return new FindResults(stringResponse: satisfyingVersions.ToArray(), hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "NuGet.Server.Core" "3.0.0-beta"
        /// We use the latest RegistrationBaseUrl version resource we can find and check if contains an entry with the package name, then match to the specified version.
        /// </summary>
        public override FindResults FindVersion(string packageName, string version, ResourceType type, out ExceptionDispatchInfo edi)
        {
            return FindVersionHelper(packageName, version, tags: Utils.EmptyStrArray, type, out edi);
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version and tag(s).
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "NuGet.Server.Core" "3.0.0-beta" -Tag "core"
        /// We use the latest RegistrationBaseUrl version resource we can find and check if contains an entry with the package name, then match to the specified version.
        /// </summary>     
        public override FindResults FindVersionWithTag(string packageName, string version, string[] tags, ResourceType type, out ExceptionDispatchInfo edi)
        {
            return FindVersionHelper(packageName, version, tags: tags, type, out edi);
        }

        /**  INSTALL APIS **/

        /// <summary>
        /// Installs specific package.
        /// Name: no wildcard support.
        /// Examples: Install "Newtonsoft.json"
        /// </summary>
        public override Stream InstallName(string packageName, bool includePrerelease, out ExceptionDispatchInfo edi)
        {
            Stream pkgStream = null;
            string registrationsBaseUrl = FindRegistrationsBaseUrl(out edi);
            if (edi != null)
            {
                return pkgStream;
            }

            string[] versionedResponses = GetVersionedResponses(registrationsBaseUrl, packageName, packageContentProperty, out edi);
            if (edi != null)
            {
                return pkgStream;
            }

            if (versionedResponses.Length == 0)
            {
                string errorMsg = $"Package with Name {packageName} could not be found in repository {Repository.Name}";
                edi = ExceptionDispatchInfo.Capture(new Exception(errorMsg));
                return pkgStream;
            }

            // get the Url for the latest version only
            string pkgContentUrl = versionedResponses[0];
            var content = HttpRequestCallForContent(pkgContentUrl, out edi);
            if (edi != null)
            {
                string errorMsg = $"Package with Name {packageName} could not be found in repository {Repository.Name}";
                edi = ExceptionDispatchInfo.Capture(new Exception(errorMsg));
                return null;
            }

            pkgStream = content.ReadAsStreamAsync().Result;
            return pkgStream;
        }

        /// <summary>
        /// Installs package with specific name and version.
        /// Name: no wildcard support.
        /// Version: no wildcard support.
        /// Examples: Install "Newtonsoft.json" -Version "1.0.0.0"
        ///           Install "Newtonsoft.json" -Version "2.5.0-beta"
        /// </summary>    
        public override Stream InstallVersion(string packageName, string version, out ExceptionDispatchInfo edi)
        {
            Stream pkgStream = null;
            if (!NuGetVersion.TryParse(version, out NuGetVersion requiredVersion))
            {
                edi = ExceptionDispatchInfo.Capture(new ArgumentException($"Version {version} to be installed is not a valid NuGet version."));
                return pkgStream;
            }

            string registrationsBaseUrl = FindRegistrationsBaseUrl(out edi);
            if (edi != null)
            {
                return pkgStream;
            }

            string[] versionedResponses = GetVersionedResponses(registrationsBaseUrl, packageName, packageContentProperty, out edi);
            if (edi != null)
            {
                return pkgStream;
            }

            if (versionedResponses.Length == 0)
            {
                string errorMsg = $"Package with Name {packageName} and Version {version} could not be found in repository {Repository.Name}";
                edi = ExceptionDispatchInfo.Capture(new Exception(errorMsg));
                return null;
            }

            string pkgContentUrl = String.Empty;
            foreach (string response in versionedResponses)
            {
                // Response will be "packageContent" element value that looks like: "{packageBaseAddress}/{packageName}/{normalizedVersion}/{packageName}.{normalizedVersion}.nupkg"
                if (response.Contains(requiredVersion.ToNormalizedString()))
                {
                    pkgContentUrl = response;
                    break;
                }
            }

            if (String.IsNullOrEmpty(pkgContentUrl))
            {
                string errorMsg = $"Package with Name {packageName} and Version {version} could not be found in repository {Repository.Name}";
                edi = ExceptionDispatchInfo.Capture(new Exception(errorMsg));
                return null;
            }

            var content = HttpRequestCallForContent(pkgContentUrl, out edi);
            if (edi != null)
            {
                return null;
            }

            pkgStream = content.ReadAsStreamAsync().Result;
            return pkgStream;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Helper method called by FindName() and FindNameWithTag()
        /// <summary>
        private FindResults FindNameHelper(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            string registrationsBaseUrl = FindRegistrationsBaseUrl(out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string[] versionedResponses = GetVersionedResponses(registrationsBaseUrl, packageName, catalogEntryProperty, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string latestVersionResponse = String.Empty;
            foreach (string response in versionedResponses)
            {
                JsonElement pkgVersionElement;
                try
                {
                    JsonDocument pkgVersionEntry = JsonDocument.Parse(response);
                    JsonElement rootDom = pkgVersionEntry.RootElement;
                    if (!rootDom.TryGetProperty(versionName, out pkgVersionElement))
                    {
                        edi = ExceptionDispatchInfo.Capture(new InvalidOrEmptyResponse($"Response does not contain '{versionName}' element."));
                        return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                    }
                }
                catch (Exception e)
                {
                    edi = ExceptionDispatchInfo.Capture(e);
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                }

                if (NuGetVersion.TryParse(pkgVersionElement.ToString(), out NuGetVersion pkgVersion))
                {
                    if (!pkgVersion.IsPrerelease || includePrerelease)
                    {
                        // versions are reported in descending order i.e 5.0.0, 3.0.0, 1.0.0 so grabbing the first match suffices
                        latestVersionResponse = response;
                        break;
                    }
                } 
            }

            if (String.IsNullOrEmpty(latestVersionResponse))
            {
                string errMsg = $"FindName(): Package with Name {packageName} was not found in repository {Repository.Name}.";
                edi = ExceptionDispatchInfo.Capture(new SpecifiedTagsNotFoundException(errMsg));
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            bool isTagMatch = DetermineTagsPresent(response: latestVersionResponse, tags: tags, out edi);
            if (!isTagMatch)
            {
                if (edi == null)
                {
                    string errMsg = $"FindName(): Package with Name {packageName} and Tags {String.Join(", ", tags)} was not found in repository {Repository.Name}.";
                    edi = ExceptionDispatchInfo.Capture(new SpecifiedTagsNotFoundException(errMsg));
                }

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            return new FindResults(stringResponse: new string[] { latestVersionResponse }, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }
        
        /// <summary>
        /// Helper method called by FindVersion() and FindVersionWithTag()
        /// </summary>
        private FindResults FindVersionHelper(string packageName, string version, string[] tags, ResourceType type, out ExceptionDispatchInfo edi)
        {
            if (!NuGetVersion.TryParse(version, out NuGetVersion requiredVersion))
            {
                edi = ExceptionDispatchInfo.Capture(new ArgumentException($"Version {version} to be found is not a valid NuGet version."));
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string registrationsBaseUrl = FindRegistrationsBaseUrl(out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string[] versionedResponses = GetVersionedResponses(registrationsBaseUrl, packageName, catalogEntryProperty, out edi);
            if (edi != null)
            {
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            string latestVersionResponse = String.Empty;
            foreach (string response in versionedResponses)
            {
                // this assumes latest versions are reported first i.e 5.0.0, 3.0.0, 1.0.0
                JsonElement pkgVersionElement;
                try
                {
                    JsonDocument pkgVersionEntry = JsonDocument.Parse(response);
                    JsonElement rootDom = pkgVersionEntry.RootElement;
                    if (!rootDom.TryGetProperty(versionName, out pkgVersionElement))
                    {
                        edi = ExceptionDispatchInfo.Capture(new InvalidOrEmptyResponse($"Response does not contain '{versionName}' element for search with Name {packageName} and Version {version}."));
                        return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                    }
                }
                catch (Exception e)
                {
                    edi = ExceptionDispatchInfo.Capture(e);
                    return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
                }

                if (NuGetVersion.TryParse(pkgVersionElement.ToString(), out NuGetVersion pkgVersion))
                {
                    if (pkgVersion == requiredVersion)
                    {
                        latestVersionResponse = response;
                        break;
                    }
                }
            }

            if (String.IsNullOrEmpty(latestVersionResponse))
            {
                edi = ExceptionDispatchInfo.Capture(new InvalidOrEmptyResponse($"FindVersion(): Package with Name {packageName}, Version {version} was not found in repository {Repository.Name}"));
                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            bool isTagMatch = DetermineTagsPresent(response: latestVersionResponse, tags: tags, out edi);
            if (!isTagMatch)
            {
                if (edi == null)
                {
                    string errMsg = $"FindVersion(): Package with Name {packageName}, Version {version} and Tags {String.Join(", ", tags)} was not found in repository {Repository.Name}.";
                    edi = ExceptionDispatchInfo.Capture(new SpecifiedTagsNotFoundException(errMsg));
                }

                return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
            }

            return new FindResults(stringResponse: new string[] { latestVersionResponse }, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Helper method that makes the HTTP request for the V3 server protocol url passed in for find APIs.
        /// </summary>
        private string HttpRequestCall(string requestUrlV3, out ExceptionDispatchInfo edi)
        {
            edi = null;
            string response = string.Empty;

            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV3);

                response = SendV3RequestAsync(request, _sessionClient).GetAwaiter().GetResult();
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
            catch (Exception e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
            }

            return response;
        }

        /// <summary>
        /// Helper method that makes the HTTP request for the V3 server protocol url passed in for install APIs.
        /// </summary>
        private HttpContent HttpRequestCallForContent(string requestUrlV3, out ExceptionDispatchInfo edi)
        {
            edi = null;
            HttpContent content = null;

            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV3);

                content = SendV3RequestForContentAsync(request, _sessionClient).GetAwaiter().GetResult();
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

        /// <summary>
        /// Gets the registrationsBaseUrl resource from the service index
        /// With ADO, older versions can support different versions of this resource, so that's why we find the latest version of this resource
        /// </summary>
        private string FindRegistrationsBaseUrl(out ExceptionDispatchInfo edi)
        {
            Hashtable resourceHash = new Hashtable();
            NuGetVersion latestRegistrationsVersion = new NuGetVersion("0.0.0.0");
            string latestRegistrationsUrl = String.Empty;

            // Get all the resources for the service index
            JsonElement[] resources = GetJsonElementArr($"{Repository.Uri}", resourcesName, out edi);
            if (edi != null)
            {
                return String.Empty;
            }

            foreach (JsonElement resource in resources)
            {
                try
                {
                    if (resource.TryGetProperty("@type", out JsonElement typeElement) && typeElement.ToString().Contains(registrationsBaseUrlName))
                    {
                        // Get Version and keep if it's latest
                        string resourceType = typeElement.ToString();
                        string[] resourceTypeParts = resourceType.Split(new char[]{'/'}, StringSplitOptions.RemoveEmptyEntries);
                        if (resourceTypeParts.Length < 2)
                        {
                            // TODO: write error?
                            continue;
                        }

                        string resourceVersion = resourceTypeParts[1];
                        if (resourceVersion.Equals("Versioned", StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue; // TODO: skip right?
                        }

                        if (!NuGetVersion.TryParse(resourceVersion, out NuGetVersion registrationsVersion))
                        {
                            edi = ExceptionDispatchInfo.Capture(new ArgumentException($"Version {resourceVersion} is not a valid NuGet version."));
                            return String.Empty;
                        }

                        if (registrationsVersion > latestRegistrationsVersion)
                        {
                            latestRegistrationsVersion = registrationsVersion;
                            if (resource.TryGetProperty("@id", out JsonElement idElement))
                            {
                                latestRegistrationsUrl = idElement.ToString();
                            }
                            else
                            {
                                string errMsg = $"@type element was found but @id element not found in service index '{Repository.Uri}' for {resourceType}.";
                                edi = ExceptionDispatchInfo.Capture(new V3ResourceNotFoundException(errMsg));
                                return String.Empty;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    string errMsg = $"Exception parsing JSON for respository {Repository.Name} with error: {e.Message}";
                    edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                    return String.Empty;
                }
            }

            return latestRegistrationsUrl;
        }

        /// <summary>
        /// Helper method iterates through the entries in the registrationsUrl for a specific package and all its versions.
        /// This contains an inner items element (containing the package metadata) and the packageContent element (containing URI through which the .nupkg can be downloaded)
        /// <param name="property"> This can be the "catalogEntry" or "packageContent" property.
        ///     The "catalogEntry" property is used for search, and the value is package metadata.
        ///     The "packageContent" property is used for download, and the value is a URI for the .nupkg file.
        /// </param>
        /// <summary>
        private string[] GetVersionedResponses(string registrationsBaseUrl, string packageName, string property, out ExceptionDispatchInfo edi)
        {
            List<string> versionedResponses = new List<string>();
            var requestPkgMapping = $"{registrationsBaseUrl}{packageName.ToLower()}/index.json";
            string pkgMappingResponse = HttpRequestCall(requestPkgMapping, out edi);
            if (edi != null)
            {
                return Utils.EmptyStrArray;
            }

            try
            {
                // parse out JSON response we get from RegistrationsUrl
                JsonDocument pkgVersionEntry = JsonDocument.Parse(pkgMappingResponse);

                // The response has a "items" array element, which only has useful 1st element
                JsonElement rootDom = pkgVersionEntry.RootElement;
                rootDom.TryGetProperty(itemsName, out JsonElement itemsElement);
                if (itemsElement.GetArrayLength() == 0)
                {
                    edi = ExceptionDispatchInfo.Capture(new ArgumentException($"Response does not contain '{itemsName}' element, for package with Name {packageName}."));
                    return Utils.EmptyStrArray;
                }

                JsonElement firstItem = itemsElement[0];

                // The "items" property contains an inner "items" element and a "count" element
                // The inner "items" property is the metadata array for each version of the package.
                // The "count" property represents how many versions are present for that package, (i.e how many elements are in the inner "items" array)
                // JsonElement innerItemsElements = firstItem.GetProperty(itemsName);
                if (!firstItem.TryGetProperty(itemsName, out JsonElement innerItemsElements))
                {
                    edi = ExceptionDispatchInfo.Capture(new ArgumentException($"Response does not contain inner '{itemsName}' element, for package with Name {packageName}."));
                    return Utils.EmptyStrArray;
                }
                
                if (!firstItem.TryGetProperty(countName, out JsonElement countElement) || !countElement.TryGetInt32(out int count))
                {
                    edi = ExceptionDispatchInfo.Capture(new ArgumentException($"Response does not contain inner '{countName}' element or it is not a valid integer, for package with Name {packageName}."));
                    return Utils.EmptyStrArray;
                }

                for (int i = 0; i < count; i++)
                {
                    // Get the specific entry for each package version
                    JsonElement versionedItem = innerItemsElements[i];
                    if (!versionedItem.TryGetProperty(property, out JsonElement metadataElement))
                    {
                        // TODO: return or parse through rest of the version entries?
                        edi = ExceptionDispatchInfo.Capture(new ArgumentException($"Response does not contain inner '{property}' element, for package with Name {packageName}."));
                        return Utils.EmptyStrArray;
                    }
                    
                    versionedResponses.Add(metadataElement.ToString());
                }
            }
            catch (Exception e)
            {
                edi = ExceptionDispatchInfo.Capture(e);
                return Utils.EmptyStrArray;
            }

            return versionedResponses.ToArray();
        }

        /// <summary>
        /// Helper method that determines if specified tags are present in response representing package(s).
        /// </summary>
        private bool DetermineTagsPresent(string response, string[] tags, out ExceptionDispatchInfo edi)
        {
            edi = null;
            string[] pkgTags = Utils.EmptyStrArray;

            try
            {
                JsonDocument pkgMappingDom = JsonDocument.Parse(response);
                JsonElement rootPkgMappingDom = pkgMappingDom.RootElement;

                if (!rootPkgMappingDom.TryGetProperty(tagsName, out JsonElement tagsElement))
                {
                    string errMsg = $"FindNameWithTag(): Tags element could not be found in response or was empty.";
                    edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                    return false;
                }

                pkgTags = GetTagsFromJsonElement(tagsElement: tagsElement);
            }
            catch (Exception e)
            {
                string errMsg = $"DetermineTagsPresent(): Exception parsing JSON for respository {Repository.Uri} with error: {e.Message}";
                edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                return false;
            }

            bool isTagMatch = DeterminePkgTagsSatisfyRequiredTags(pkgTags: pkgTags, requiredTags: tags);

            return isTagMatch;
        }

        /// <summary>
        /// Helper method that finds all tags for package with the given tags JSonElement.
        /// </summary>
        private string[] GetTagsFromJsonElement(JsonElement tagsElement)
        {
            List<string> tagsFound = new List<string>();
            JsonElement[] pkgTagElements = tagsElement.EnumerateArray().ToArray();
            foreach (JsonElement tagItem in pkgTagElements)
            {
                tagsFound.Add(tagItem.ToString().ToLower());
            }

            return tagsFound.ToArray();
        }

        /// <summary>
        /// Helper method that compares the tags requests to be present to the tags present in the package.
        /// </summary>
        private bool DeterminePkgTagsSatisfyRequiredTags(string[] pkgTags, string[] requiredTags)
        {
            bool isTagMatch = true;

            foreach (string tag in requiredTags)
            {
                if (!pkgTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    isTagMatch = false;
                    break;
                }
            }
            
            return isTagMatch;
        }

        /// <summary>
        /// Helper method that parses response for given property and returns result for that property as a JsonElement array.
        /// </summary>
        private JsonElement[] GetJsonElementArr(string request, string propertyName, out ExceptionDispatchInfo edi)
        {
            JsonElement[] pkgsArr = new JsonElement[0];
            try
            { 
                string response = HttpRequestCall(request, out edi);
                if (edi != null)
                {
                    return new JsonElement[]{};
                }

                JsonDocument pkgsDom = JsonDocument.Parse(response);

                pkgsDom.RootElement.TryGetProperty(propertyName, out JsonElement pkgs);

                pkgsArr = pkgs.EnumerateArray().ToArray();
            }
            catch (Exception e)
            {
                string errMsg = $"Exception parsing JSON for respository {Repository.Uri} with error: {e.Message}";
                edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
            }

            return pkgsArr;
        }

        /// <summary>
        /// Helper method called by HttpRequestCall() that makes the HTTP request for string response.
        /// </summary>
        public static async Task<string> SendV3RequestAsync(HttpRequestMessage message, HttpClient s_client)
        {
            string errMsg = "SendV3RequestAsync(): Error occured while trying to retrieve response: ";

            try
            {
                HttpResponseMessage response = await s_client.SendAsync(message);
                response.EnsureSuccessStatusCode();

                var responseStr = await response.Content.ReadAsStringAsync();

                return responseStr;
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
        /// Helper method called by HttpRequestCallForContent() that makes the HTTP request for string response.
        /// </summary>
        public static async Task<HttpContent> SendV3RequestForContentAsync(HttpRequestMessage message, HttpClient s_client)
        {
            string errMsg = "SendV3RequestForContentAsync(): Error occured while trying to retrieve response for content: ";

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
