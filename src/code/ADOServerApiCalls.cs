using System.Security.Cryptography;
using System.Reflection.Emit;
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
        // private static readonly string packageBaseAddressName = "PackageBaseAddress/3.0.0";
        // private static readonly string searchQueryServiceName = "SearchQueryService/3.0.0-beta";
        // private static readonly string registrationsBaseUrlName = "RegistrationsBaseUrl/Versioned";
        // private static readonly string dataName = "data";
        // private static readonly string idName = "id";
        // private static readonly string versionName = "version";
        private static readonly string tagsName = "tags";
        // private static readonly string versionsName = "versions";
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
        /// Not supported
        /// </summary>
        public override FindResults FindAll(bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            string errMsg = $"Find all is not supported for the repository {Repository.Uri}";
            edi = ExceptionDispatchInfo.Capture(new InvalidOperationException(errMsg));

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag from a repository and returns latest version for each.
        /// Examples: Search -Tag "Redis" -Repository PSGallery
        /// API call: 
        /// https://azuresearch-ussc.nuget.org/query?q=tags:redis&prerelease=False&semVerLevel=2.0.0
        /// 
        /// Azure Artifacts does not support querying on tags, so if support this scenario we need to search on the term and then filter
        /// </summary>
        public override FindResults FindTags(string[] tags, bool includePrerelease, ResourceType _type, out ExceptionDispatchInfo edi)
        {
            string errMsg = $"Find by Tags is not supported for the repository {Repository.Uri}";
            edi = ExceptionDispatchInfo.Capture(new InvalidOperationException(errMsg));

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// This functionality is not supported for V3 protocol server.
        /// Find method which allows for searching for packages with specified Command or DSCResource name.
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
        /// API call: 
        ///               https://api.nuget.org/v3/registration5-gz-semver2/nuget.server/index.json
        ///               https://msazure.pkgs.visualstudio.com/One/_packaging/testfeed/nuget/v3/registrations2-semver2/newtonsoft.json/index.json
        ///               https://msazure.pkgs.visualstudio.com/999aa88e-7ed7-41b2-9d77-5bc261222004/_packaging/0d5429e2-c871-4347-bdc9-d1cbbac5eb3b/nuget/v3/registrations2-semver2/newtonsoft.json/index.json
        /// The RegistrationBaseUrl that we're using is "RegistrationBaseUrl/Versioned"
        /// This type points to the url to use (ex above)
        /// Implementation Note: Need to filter further for latest version (prerelease or non-prerelease dependening on user preference)
        /// </summary>
        public override FindResults FindName(string packageName, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            return FindNameHelper(packageName, tags: Utils.EmptyStrArray, includePrerelease, type, out edi);
        }

        /// <summary>
        /// Find method which allows for searching for single name and tag and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "Newtonsoft.Json" - Tag "json"
        /// </summary>
        public override FindResults FindNameWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            return FindNameHelper(packageName, tags, includePrerelease, type, out edi);
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "Nuget.Server*"
        /// API call: 
        /// - No prerelease: https://api-v2v3search-0.nuget.org/autocomplete?q=storage&prerelease=false
        /// - Prerelease:  https://api-v2v3search-0.nuget.org/autocomplete?q=storage&prerelease=true  
        /// 
        /// https://msazure.pkgs.visualstudio.com/b32aa71e-8ed2-41b2-9d77-5bc261222004/_packaging/0d5429e2-c871-4347-bdc9-d1cbbac5eb3b/nuget/v3/query2?q=Newtonsoft&prerelease=false&semVerLevel=2.0.0
        ///         
        ///        Note:  response only returns names
        ///        
        ///        Make another query to get the latest version of each package  (ie call "FindVersionGlobbing")
        /// </summary>
        public override FindResults FindNameGlobbing(string packageName, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            string errMsg = $"Find with Name containing wildcards is not supported for the repository {Repository.Uri}";
            edi = ExceptionDispatchInfo.Capture(new InvalidOperationException(errMsg));

            return new FindResults(stringResponse: Utils.EmptyStrArray, hashtableResponse: emptyHashResponses, responseType: v3FindResponseType);
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and tag and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "Nuget.Server*" -Tag "nuget"
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
        /// API Call: 
        ///           then, find all versions for a pkg
        ///           for nuget:
        ///               this contains all pkg version info: https://api.nuget.org/v3/registration5-gz-semver2/nuget.server/index.json
        ///               However, we will use the flattened version list: https://api.nuget.org/v3-flatcontainer/newtonsoft.json/index.json
        ///           for Azure Artifacts:
        ///               https://msazure.pkgs.visualstudio.com/b32aa71e-8ed2-41b2-9d77-5bc261222004/_packaging/0d5429e2-c871-4347-bdc9-d1cbbac5eb3b/nuget/v3/flat2/newtonsoft.json/index.json
        ///            (azure artifacts)
        ///            
        ///             Note:  very different responses for nuget vs azure artifacts
        ///            
        ///            After we figure out what version we want, call "FindVersion" (or some helper method)
        /// need to filter client side
        /// Implementation note: Returns all versions, including prerelease ones. Later (in the API client side) we'll do filtering on the versions to satisfy what user provided.
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
        /// API call: 
        ///     first find the RegistrationBaseUrl
        ///     https://api.nuget.org/v3/registration5-gz-semver2/nuget.server/index.json
        ///     
        ///     https://msazure.pkgs.visualstudio.com/One/_packaging/testfeed/nuget/v3/registrations2-semver2/newtonsoft.json/index.json
        ///     https://msazure.pkgs.visualstudio.com/999aa88e-7ed7-41b2-9d77-5bc261222004/_packaging/0d5429e2-c871-4347-bdc9-d1cbbac5eb3b/nuget/v3/registrations2-semver2/newtonsoft.json/index.json
        ///         The RegistrationBaseUrl that we're using is "RegistrationBaseUrl/Versioned"
        ///         This type points to the url to use (ex above)
        ///         
        ///     then we can make a call for the specific version  
        ///     https://api.nuget.org/v3/registration5-gz-semver2/nuget.server.core/3.0.0-beta
        ///     (alternative url for nuget gallery):  https://api.nuget.org/v3/registration5-gz-semver2/nuget.server.core/index.json#page/3.0.0-beta/3.0.0-beta
        ///     https://msazure.pkgs.visualstudio.com/b32aa71e-8ed2-41b2-9d77-5bc261222004/_packaging/0d5429e2-c871-4347-bdc9-d1cbbac5eb3b/nuget/v3/registrations2/newtonsoft.json/13.0.2.json 
        ///     
        /// </summary>
        
        public override FindResults FindVersion(string packageName, string version, ResourceType type, out ExceptionDispatchInfo edi)
        {
            return FindVersionHelper(packageName, version, tags: Utils.EmptyStrArray, type, out edi);
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version and tag.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "NuGet.Server.Core" -Version "3.0.0-beta" -Tag "nuget"
        /// API call: 
        ///     first find the RegistrationBaseUrl
        ///     https://api.nuget.org/v3/registration5-gz-semver2/nuget.server/index.json
        ///     
        ///     https://msazure.pkgs.visualstudio.com/One/_packaging/testfeed/nuget/v3/registrations2-semver2/newtonsoft.json/index.json
        ///     https://msazure.pkgs.visualstudio.com/999aa88e-7ed7-41b2-9d77-5bc261222004/_packaging/0d5429e2-c871-4347-bdc9-d1cbbac5eb3b/nuget/v3/registrations2-semver2/newtonsoft.json/index.json
        ///         The RegistrationBaseUrl that we're using is "RegistrationBaseUrl/Versioned"
        ///         This type points to the url to use (ex above)
        ///         
        ///     then we can make a call for the specific version  
        ///     https://api.nuget.org/v3/registration5-gz-semver2/nuget.server.core/3.0.0-beta
        ///     (alternative url for nuget gallery):  https://api.nuget.org/v3/registration5-gz-semver2/nuget.server.core/index.json#page/3.0.0-beta/3.0.0-beta
        ///     https://msazure.pkgs.visualstudio.com/b32aa71e-8ed2-41b2-9d77-5bc261222004/_packaging/0d5429e2-c871-4347-bdc9-d1cbbac5eb3b/nuget/v3/registrations2/newtonsoft.json/13.0.2.json 
        ///     
        /// </summary>        
        public override FindResults FindVersionWithTag(string packageName, string version, string[] tags, ResourceType type, out ExceptionDispatchInfo edi)
        {
            return FindVersionHelper(packageName, version, tags: tags, type, out edi);
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
                return null;
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
        /// Examples: Install "PowerShellGet" -Version "3.0.0.0"
        ///           Install "PowerShellGet" -Version "3.0.0-beta16"
        ///           
        ///  https://api.nuget.org/v3-flatcontainer/newtonsoft.json/9.0.1/newtonsoft.json.9.0.1.nupkg
        /// API Call: 
        /// </summary>    
        public override Stream InstallVersion(string packageName, string version, out ExceptionDispatchInfo edi)
        {
            NuGetVersion.TryParse(version, out NuGetVersion requiredVersion);

            // {registrationUrl} (minus last "/X") + "/flat2/" + {name}/{version}/{name}.{version}.nupkg
            // https://pkgs.dev.azure.com/powershell-rel/8abad6f9-c150-4f52-8adb-5438eaafd645/_packaging/d7ed2d91-9949-4cad-8b55-f46e225426dd/nuget/v3/registrations2-semver2/
            // https://pkgs.dev.azure.com/powershell-rel/8abad6f9-c150-4f52-8adb-5438eaafd645/_packaging/d7ed2d91-9949-4cad-8b55-f46e225426dd/nuget/v3/flat2/test_local_mod/5.0.0/test_local_mod.5.0.0.nupkg
            // https://pkgs.dev.azure.com/powershell-rel/_packaging/fae8154e-72fd-4f73-86aa-795f6620404c@bf3de586-7353-4d8e-aa07-30fe2569ce2d/nuget/v3/flat2/

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
                string errorMsg = $"Package with Name {packageName} and Version {version} could not be found in repository {Repository.Name}";
                edi = ExceptionDispatchInfo.Capture(new Exception(errorMsg));
                return null;
            }

            string pkgContentUrl = String.Empty;
            foreach (string response in versionedResponses)
            {
                // JsonDocument pkgVersionEntry = JsonDocument.Parse(response);
                // JsonElement rootDom = pkgVersionEntry.RootElement;
                // rootDom.TryGetProperty("version", out JsonElement versionElement);
                if (response.Contains(requiredVersion.ToNormalizedString()))
                {
                    pkgContentUrl = response;
                    break;
                }
                // if (NuGetVersion.TryParse(versionElement.ToString(), out NuGetVersion pkgNuGetVersion))
                // {
                //     if (pkgNuGetVersion == requiredVersion)
                //     {
                        
                //     }
                // }
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
        
        private FindResults FindVersionHelper(string packageName, string version, string[] tags, ResourceType type, out ExceptionDispatchInfo edi)
        {
            NuGetVersion.TryParse(version, out NuGetVersion requiredVersion);

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
        /// Helper method that makes finds the specified V3 server protocol resources from the service index.
        /// </summary>
        private Hashtable FindResourceType(string[] resourceTypeName, out ExceptionDispatchInfo edi)
        {
            Hashtable resourceHash = new Hashtable();
            JsonElement[] resources = GetJsonElementArr($"{Repository.Uri}", resourcesName, out edi);
            if (edi != null)
            {
                return resourceHash;
            }

            foreach (JsonElement resource in resources)
            {
                try
                {
                    if (resource.TryGetProperty("@type", out JsonElement typeElement) && resourceTypeName.Contains(typeElement.ToString()))
                    {
                        // check if key already present in hastable, as there can be resources with same type but primary/secondary instances
                        if (!resourceHash.ContainsKey(typeElement.ToString()))
                        {
                            if (resource.TryGetProperty("@id", out JsonElement idElement))
                            {
                                // add name of the resource and its url
                                resourceHash.Add(typeElement.ToString(), idElement.ToString());
                            }
                            else
                            {
                                string errMsg = $"@type element was found but @id element not found in service index '{Repository.Uri}' for {resourceTypeName}.";
                                edi = ExceptionDispatchInfo.Capture(new V3ResourceNotFoundException(errMsg));
                                return resourceHash;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    string errMsg = $"Exception parsing JSON for respository {Repository.Uri} with error: {e.Message}";
                    edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                    return resourceHash;
                }

                if (resourceHash.Count == resourceTypeName.Length)
                {
                    break;
                }
            }

            foreach (string resourceType in resourceTypeName)
            {
                if (!resourceHash.ContainsKey(resourceType))
                {
                    string errMsg = $"FindResourceType(): Could not find resource type {resourceType} from the service index.";
                    edi = ExceptionDispatchInfo.Capture(new V3ResourceNotFoundException(errMsg));
                    break;
                }
            }

            return resourceHash;
        }

        private string FindRegistrationsBaseUrl(out ExceptionDispatchInfo edi)
        {
            edi = null;
            Hashtable resourceHash = new Hashtable();
            NuGetVersion latestRegistrationsVersion = new NuGetVersion("0.0.0.0");
            string latestRegistrationsUrl = String.Empty;
            JsonElement[] resources = GetJsonElementArr($"{Repository.Uri}", resourcesName, out edi);
            if (edi != null)
            {
                return String.Empty;
            }

            foreach (JsonElement resource in resources)
            {
                try
                {
                    if (resource.TryGetProperty("@type", out JsonElement typeElement) && typeElement.ToString().Contains("RegistrationsBaseUrl"))
                    {
                        // Get Version and keep if it's latest
                        string resourceType = typeElement.ToString();
                        string[] resourceTypeParts = resourceType.Split(new char[]{'/'}, StringSplitOptions.RemoveEmptyEntries);
                        if (resourceTypeParts.Length < 2)
                        {
                            // TODO: some error
                        }

                        string resourceVersion = resourceTypeParts[1];
                        if (resourceVersion.Equals("Versioned", StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue; // skip right?
                        }

                        if (!NuGetVersion.TryParse(resourceVersion, out NuGetVersion registrationsVersion))
                        {
                            // TODO: some error
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
                    string errMsg = $"Exception parsing JSON for respository {Repository.Uri} with error: {e.Message}";
                    edi = ExceptionDispatchInfo.Capture(new JsonParsingException(errMsg));
                    return String.Empty;
                }
            }

            return latestRegistrationsUrl;
        }

        /// <summary>
        /// Helper method finds package with name and specified version
        /// <summary>
        private string[] GetVersionedResponses(string registrationsBaseUrl, string packageName, string property, out ExceptionDispatchInfo edi)
        {
            List<string> versionedResponses = new List<string>();

            // https://pkgs.dev.azure.com/powershell-rel/8abad6f9-c150-4f52-8adb-5438eaafd645/_packaging/d7ed2d91-9949-4cad-8b55-f46e225426dd/nuget/v3/registrations2/test_local_mod/index.json
            var requestPkgMapping = $"{registrationsBaseUrl}{packageName.ToLower()}/index.json";
            string pkgMappingResponse = HttpRequestCall(requestPkgMapping, out edi);
            if (edi != null)
            {
                return Utils.EmptyStrArray;
            }

            // parse out JSON response we get from RegistrationsUrl
            JsonDocument pkgVersionEntry = JsonDocument.Parse(pkgMappingResponse);

            // The response has a "items" array element, which only has useful 1st element
            JsonElement rootDom = pkgVersionEntry.RootElement;
            rootDom.TryGetProperty("items", out JsonElement itemsElement);
            JsonElement firstItem = itemsElement[0];

            // The "items" property has a "items" element as well as a "count" element
            JsonElement innerItemsElements = firstItem.GetProperty("items"); // this is the item for each version of the package
            JsonElement countElement = firstItem.GetProperty("count"); // this is the count representing how many versions are present for that package.
            bool parsedCount = countElement.TryGetInt32(out int count);

            for (int i = 0; i < count; i++)
            {
                JsonElement versionedItem = innerItemsElements[i]; // the specific entry for a package version
                JsonElement metadataElement = versionedItem.GetProperty(property);
                versionedResponses.Add(metadataElement.ToString());
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
