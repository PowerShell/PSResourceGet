// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
using System.IO;
using System.Net.Http;
using NuGet.Versioning;
using System.Net;
using System.Text;
using System.Runtime.ExceptionServices;
using System.Management.Automation;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    internal abstract class ServerApiCall : IServerAPICalls
    {
        #region Members

        public abstract PSRepositoryInfo Repository { get; set; }
        private HttpClient _sessionClient { get; set; }

        #endregion

        #region Constructor

        public ServerApiCall(PSRepositoryInfo repository, NetworkCredential networkCredential)
        {
            this.Repository = repository;

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

        }

        #endregion

        #region Methods
        // High level design: Find-PSResource >>> IFindPSResource (loops, version checks, etc.) >>> IServerAPICalls (call to repository endpoint/url)

        /// <summary>
        /// Find method which allows for searching for all packages from a repository and returns latest version for each.
        /// </summary>
        public abstract FindResults FindAll(bool includePrerelease, ResourceType type, out ErrorRecord errRecord);

        /// <summary>
        /// Find method which allows for searching for packages with tag from a repository and returns latest version for each.
        /// Examples: Search -Tag "JSON" -Repository PSGallery
        /// </summary>
        public abstract FindResults FindTags(string[] tags, bool includePrerelease, ResourceType _type, out ErrorRecord errRecord);

        /// <summary>
        /// Find method which allows for searching for all packages that have specified Command or DSCResource name.
        /// </summary>
        public abstract FindResults FindCommandOrDscResource(string[] tags, bool includePrerelease, bool isSearchingForCommands, out ErrorRecord errRecord);

        /// <summary>
        /// Find method which allows for searching for package by single name and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "PowerShellGet"
        /// </summary>
        public abstract FindResults FindName(string packageName, bool includePrerelease, ResourceType type, out ErrorRecord errRecord);

        /// <summary>
        /// Find method which allows for searching for package by single name and tag and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "PowerShellGet" -Tag "provider"
        /// </summary>
        public abstract FindResults FindNameWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord);

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "PowerShell*"
        /// </summary>
        public abstract FindResults FindNameGlobbing(string packageName, bool includePrerelease, ResourceType type, out ErrorRecord errRecord);

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and tag and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "PowerShell*"
        /// </summary>
        public abstract FindResults FindNameGlobbingWithTag(string packageName, string[] tags, bool includePrerelease, ResourceType type, out ErrorRecord errRecord);
        /// <summary>
        /// Find method which allows for searching for single name with version range.
        /// Name: no wildcard support
        /// Version: supports wildcards
        /// Examples: Search "PowerShellGet" "[3.0.0.0, 5.0.0.0]"
        ///           Search "PowerShellGet" "3.*"
        /// </summary>
        public abstract FindResults FindVersionGlobbing(string packageName, VersionRange versionRange, bool includePrerelease, ResourceType type, bool getOnlyLatest, out ErrorRecord errRecord);

        /// <summary>
        /// Find method which allows for searching for single name with specific version.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5"
        /// </summary>
        public abstract FindResults FindVersion(string packageName, string version, ResourceType type, out ErrorRecord errRecord);

        /// <summary>
        /// Find method which allows for searching for single name with specific version.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5" -Tag "Provider"
        /// </summary>
        public abstract FindResults FindVersionWithTag(string packageName, string version, string[] tags, ResourceType type, out ErrorRecord errRecord);

        /**  INSTALL APIS **/

        /// <summary>
        /// Installs specific package.
        /// Name: no wildcard support.
        /// Examples: Install "PowerShellGet"
        ///           Install "PowerShellGet" -Version "3.0.0"
        ///           Install "PowerShellGet" -Version "3.0.0-beta24"
        /// Implementation Note:   if not prerelease: https://www.powershellgallery.com/api/v2/package/powershellget (Returns latest stable)
        ///                        if prerelease, the calling method should first call IFindPSResource.FindName(),
        ///                             then find the exact version to install, then call into install version
        /// </summary>
        public abstract Stream InstallPackage(string packageName, string packageVersion, bool includePrerelease, out ErrorRecord errRecord);

        #endregion

    }
}
