// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
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
    internal class LocalServerAPICalls : ServerApiCall
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

        public override PSRepositoryInfo repository { get; set; }
        public override HttpClient s_client { get; set; }
        private static readonly string select = "$select=Id,Version,NormalizedVersion,Authors,Copyright,Dependencies,Description,IconUrl,IsPrerelease,Published,ProjectUrl,ReleaseNotes,Tags,LicenseUrl,CompanyName";

        #endregion

        #region Constructor

        public LocalServerAPICalls (PSRepositoryInfo repository, NetworkCredential networkCredential) : base (repository, networkCredential)
        {
            this.repository = repository;
            HttpClientHandler handler = new HttpClientHandler()
            {
                Credentials = networkCredential
            };

            s_client = new HttpClient(handler);
        }

        #endregion

        #region Overriden Methods 

        /// <summary>
        /// Find method which allows for searching for all packages from a repository and returns latest version for each.
        /// Examples: Search -Repository PSGallery
        /// API call: 
        /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion
        /// </summary>
        public override string[] FindAll(bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi) {
            edi = null;
            List<string> responses = new List<string>();

            // loop thru, for each unqiue packagename, return latest name

            return responses.ToArray();
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag from a repository and returns latest version for each.
        /// Examples: Search -Tag "JSON" -Repository PSGallery
        /// API call: 
        /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&searchTerm=tag:JSON&includePrerelease=true
        /// </summary>
        public override string[] FindTag(string tag, bool includePrerelease, ResourceType _type, out ExceptionDispatchInfo edi)
        {
            edi = null;
            List<string> responses = new List<string>();

            // call into FindAll() which returns string responses for all 
            // look at tags field for each string response

            return responses.ToArray();
        }

        /// <summary>
        /// Find method which allows for searching for all packages that have specified Command or DSCResource name.
        /// </summary>
        public override string[] FindCommandOrDscResource(string tag, bool includePrerelease, bool isSearchingForCommands, out ExceptionDispatchInfo edi)
        {
            List<string> responses = new List<string>();
            edi = null;

            // call into FindAll() which returns string responses for all 
            // look at tags field for each string response

            return responses.ToArray();
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
        public override string FindName(string packageName, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            edi = null;
            return String.Empty;

            // repository -> we know the search path
                // C:/MyLocalRepo/pkgA.1.0.0.0.nupkg
                // C:/MyLocalRepo/pkgB.1.0.0.0.nupkg

            // loop thru paths, and do wildcard matching with packagename (packageA1.0.0.0 matches "packageA")
            // get all packages that match, extract version from path (split on '.')
            // compare versions to find latest
            // handle prerelease:

            // extract the files. get the psd1/ps1, create a response string by filestream.ReadAsString
            // ensure script content is sanitized
            // for NuGet repo packages (that don't have psd1 or ps1 -> read nuspec)
            // read that file and put into string

            // ModuleManifest <PSParsing>
            // rest of file string
        
        }

        /// <summary>
        /// Find method which allows for searching for single name and tag and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "PowerShellGet" -Tag "Provider"
        /// Implementation Note: Need to filter further for latest version (prerelease or non-prerelease dependening on user preference)
        /// </summary>
        public override string FindNameWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            edi = null;
            return String.Empty;
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "PowerShell*"
        /// API call: 
        /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='az*'
        /// Implementation Note: filter additionally and verify ONLY package name was a match.
        /// </summary>
        public override string[] FindNameGlobbing(string packageName, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            List<string> responses = new List<string>();
            edi = null;

            
            // repository -> we know the search path
                // C:/MyLocalRepo/pkgA.1.0.0.0.nupkg
                // C:/MyLocalRepo/pkgB.1.0.0.0.nupkg

            // wildcard name possibilities: power*, *get, power*get

            // loop thru packages paths and extrapolate names for each

            // do wildcard search on names and see which match
            // if it matches, look at version and prerelease and return accordingly (first should be latest, check prerleease tho)

            // extract the files. get the psd1/ps1, create a response string by filestream.ReadAsString
            // ensure script content is sanitized
            // for NuGet repo packages (that don't have psd1 or ps1 -> read nuspec)
            // read that file and put into string

            // ModuleManifest <PSParsing>
            // rest of file string

            return responses.ToArray();
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and tag and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "PowerShell*" -Tag "Provider"
        /// Implementation Note: filter additionally and verify ONLY package name was a match.
        /// </summary>
        public override string[] FindNameGlobbingWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ExceptionDispatchInfo edi)
        {
            List<string> responses = new List<string>();
            edi = null;

            return responses.ToArray();
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
        public override string[] FindVersionGlobbing(string packageName, VersionRange versionRange, bool includePrerelease, ResourceType type, bool getOnlyLatest, out ExceptionDispatchInfo edi)
        {
            List<string> responses = new List<string>();
            edi = null;


            // repository -> we know the search path
                // C:/MyLocalRepo/pkgA.1.0.0.0.nupkg
                // C:/MyLocalRepo/pkgB.1.0.0.0.nupkg

            // iterate thru paths
            // extrapolate name, version
            // if name matches keep 
            // check if version falls within range -> keep

            // extract the files. get the psd1/ps1, create a response string by filestream.ReadAsString
            // ensure script content is sanitized
            // for NuGet repo packages (that don't have psd1 or ps1 -> read nuspec)
            // read that file and put into string

            // ModuleManifest <PSParsing>
            // rest of file string

            return responses.ToArray();
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5"
        /// API call: http://www.powershellgallery.com/api/v2/Packages(Id='PowerShellGet', Version='2.2.5')
        /// </summary>
        public override string FindVersion(string packageName, string version, ResourceType type, out ExceptionDispatchInfo edi) 
        {
            edi = null;
            return String.Empty;

            // repository -> we know the search path
                // C:/MyLocalRepo/pkgA.1.0.0.0.nupkg
                // C:/MyLocalRepo/pkgB.1.0.0.0.nupkg

            // our findings on how PSGallery are creating nupkgnames:
            // if last digit is 0 (i.e 2.2.0.0) -> 2.2.0 (trim 4th digit if 0, always have 3 digits)
            // if last digit is non-0 (i.e 1.1.1.1) -> 1.1.1.1 (keep all 4 digits)
            // if version published to gallery shows 2 digits (3.3) we add 3rd digit of 0 (3.3.0)

            // for NuGetGallery: always 3 if last digit is 0, otherwise 4 digits, no 2 digits

            // string nameWeExpect = packageName + "." + packageVersion.ToNormalizedString() + ".nupkg"
            // test that path with nupkg name

            // extract the files. get the psd1/ps1, create a response string by filestream.ReadAsString
            // ensure script content is sanitized
            // for NuGet repo packages (that don't have psd1 or ps1 -> read nuspec)
            // read that file and put into string

            // ModuleManifest <PSParsing>
            // rest of file string
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version and tag.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5" -Tag "Provider"
        /// </summary>
        public override string FindVersionWithTag(string packageName, string version, string[] tags, ResourceType type, out ExceptionDispatchInfo edi)
        {
            edi = null;
            return String.Empty;
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
            edi = null;
            return null;
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
            edi = null;

            // find packagename that matches our criteria -> gives us the nupkg
            // if we must return Stream:
            // 1) take nupkg -> zip file
            // read contents of zip into stream
            // return stream
            // in InstallHelper read out stream to get zip file
            // (redundant packing)

            // have separate interfaces for local versus remote repos

            // return path where zip file is present
            // for local this would be okay
            // for remote, we'd read stream contents into temp path, and need that passed in (seems messy)


            return null;
        }

        /// <summary>
        /// Helper method that makes the HTTP request for the V2 server protocol url passed in for find APIs.
        /// </summary>
        private string HttpRequestCall(string requestUrlV2, out ExceptionDispatchInfo edi)
        {
            edi = null;
            string response = string.Empty;

            // try
            // {
            //     HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV2);
                
            //     response = SendV2RequestAsync(request, s_client).GetAwaiter().GetResult();
            // }
            // catch (HttpRequestException e)
            // {
            //     edi = ExceptionDispatchInfo.Capture(e);
            // }
            // catch (ArgumentNullException e)
            // {
            //     edi = ExceptionDispatchInfo.Capture(e);
            // }
            // catch (InvalidOperationException e)
            // {
            //     edi = ExceptionDispatchInfo.Capture(e);
            // }

            return response;
        }

        /// <summary>
        /// Helper method that makes the HTTP request for the V2 server protocol url passed in for install APIs.
        /// </summary>
        private HttpContent HttpRequestCallForContent(string requestUrlV2, out ExceptionDispatchInfo edi) 
        {
            edi = null;
            HttpContent content = null;

            // try
            // {
            //     HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrlV2);
                
            //     content = SendV2RequestForContentAsync(request, s_client).GetAwaiter().GetResult();
            // }
            // catch (HttpRequestException e)
            // {
            //     edi = ExceptionDispatchInfo.Capture(e);
            // }
            // catch (ArgumentNullException e)
            // {
            //     edi = ExceptionDispatchInfo.Capture(e);
            // }
            // catch (InvalidOperationException e)
            // {
            //     edi = ExceptionDispatchInfo.Capture(e);
            // }

            return content;
        }

        #endregion

    }
}