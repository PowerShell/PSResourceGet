// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Find-PSResource cmdlet combines the Find-Module, Find-Script, Find-DscResource, Find-Command cmdlets from V2.
    /// It performs a search on a repository (local or remote) based on the -Name parameter argument.
    /// It returns PSResourceInfo objects which describe each resource item found.
    /// Other parameters allow the returned results to be filtered by item Type and Tag.
    /// </summary>
    [Cmdlet(VerbsCommon.Find,
        "PSACR")]
    [OutputType(typeof(PSResourceInfo), typeof(PSCommandResourceInfo))]
    public sealed class FindPSACR : PSCmdlet
    {
        static readonly HttpClient client = new HttpClient();

        #region Parameters

        /// <summary>
        /// Specifies name of a resource or resources to find. Accepts wild card characters.
        /// </summary>
        [Parameter(Position = 0,
                   ValueFromPipeline = true)]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        /// <summary>
        /// Specifies the version of the resource to be found and returned.
        /// </summary>
        [Parameter()]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }

        /// <summary>
        /// Specifies one or more repository names to search. If not specified, search will include all currently registered repositories.
        /// </summary>
        [Parameter()]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        #endregion

        #region Method overrides

        protected override void BeginProcessing()
        {

            // Create a repository story (the PSResourceRepository.xml file) if it does not already exist
            // This is to create a better experience for those who have just installed v3 and want to get up and running quickly
            RepositorySettings.CheckRepositoryStore();
        }

        protected override void ProcessRecord()
        {
            List<PSRepositoryInfo> repositoriesToSearch = new List<PSRepositoryInfo>();
            try
            {
                repositoriesToSearch = RepositorySettings.Read(Repository, out string[] errorList);
                foreach (string error in errorList)
                {
                    WriteError(new ErrorRecord(
                        new PSInvalidOperationException(error),
                        "ErrorGettingSpecifiedRepo",
                        ErrorCategory.InvalidOperation,
                        this));
                }
                WriteVerbose("Repository to search: " + String.Join(",", repositoriesToSearch));
            }
            catch (Exception e)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new PSInvalidOperationException(e.Message),
                    "ErrorLoadingRepositoryStoreFile",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            foreach (PSRepositoryInfo repo in repositoriesToSearch)
            {
                if (repo.RepositoryProvider == PSRepositoryInfo.RepositoryProviderType.ACR)
                {
                    string accessToken = string.Empty;
                    string tenantID = string.Empty;

                    // Need to set up secret management vault before hand
                    var repositoryCredentialInfo = repo.CredentialInfo;
                    if (repositoryCredentialInfo != null)
                    {
                        accessToken = Utils.GetACRAccessTokenFromSecretManagement(
                            repo.Name,
                            repositoryCredentialInfo,
                            this);
                        WriteVerbose("Access token retrieved.");

                        tenantID = Utils.GetSecretInfoFromSecretManagement(
                            repo.Name,
                            repositoryCredentialInfo,
                            this);
                    }

                    AcrSearchHelper(repo, accessToken, tenantID);
                }
            }
        }

        #endregion

        #region Private Methods

        private void AcrSearchHelper(PSRepositoryInfo repository, string aadAccessToken, string tenant)
        {
            // Call asynchronous network methods in a try/catch block to handle exceptions.
            string registry = repository.Uri.Host;

            try
            {
                WriteVerbose("Getting refresh token for ACR");
                var acrRefreshToken = AcrHttpHelper.GetAcrRefreshTokenAsync(registry, tenant, aadAccessToken).Result;
                WriteVerbose("Getting acr access token");
                var acrAccessToken = AcrHttpHelper.GetAcrAccessTokenAsync(registry, acrRefreshToken).Result;
                WriteVerbose("Getting tags");
                var foundTags = AcrHttpHelper.FindAcrImageTags(registry, Name, Version, acrAccessToken).Result;

                if (foundTags != null)
                {
                    if (string.Equals(Version, "*", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var item in foundTags["tags"])
                        {
                            string info = $"{Name} - {item["name"]} - {item["digest"]}";
                            WriteObject(info);
                        }
                    }
                    else
                    {
                        string info = $"{Name} - {Version} - {foundTags["tag"]["digest"]}";
                        WriteObject(info);
                    }
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ", e.Message);
            }
        }

        #endregion
    }
}