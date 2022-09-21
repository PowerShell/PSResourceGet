// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
//using Microsoft.Azure.Commands.Profile.Models;
//using Microsoft.IdentityModel.Clients.ActiveDirectory;

using Dbg = System.Diagnostics.Debug;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Find-PSResource cmdlet combines the Find-Module, Find-Script, Find-DscResource, Find-Command cmdlets from V2.
    /// It performs a search on a repository (local or remote) based on the -Name parameter argument.
    /// It returns PSResourceInfo objects which describe each resource item found.
    /// Other parameters allow the returned results to be filtered by item Type and Tag.
    /// </summary>
    [Cmdlet(VerbsCommon.Find,
        "ACR")]
    [OutputType(typeof(PSResourceInfo), typeof(PSCommandResourceInfo))]
    public sealed class FindACR : PSCmdlet
    {
        static readonly HttpClient client = new HttpClient();
        System.Management.Automation.PowerShell pwsh;

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

        [Parameter()]
        public string Path { get; set; }

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

                    AcrSearchHelper(repo);
                }
            }
        }

        #endregion

        #region Private Methods

        private void AcrSearchHelper(PSRepositoryInfo repository)
        {
            // Call asynchronous network methods in a try/catch block to handle exceptions.
            string registry = repository.Uri.Host;
            string tenant = "72f988bf-86f1-41af-91ab-2d7cd011db47";
            string aad_access_token = String.Empty;

            // Setting up the PowerShell runspace
            var defaultSS = System.Management.Automation.Runspaces.InitialSessionState.CreateDefault2();
            defaultSS.ExecutionPolicy = ExecutionPolicy.Unrestricted;
            pwsh = System.Management.Automation.PowerShell.Create(defaultSS);

            Collection<PSObject> results;
            try
            {
                results = pwsh.AddScript("(Get-AzAccessToken).Token").Invoke();
            }
            catch (Exception e)
            {
                WriteVerbose($"Error occured while running 'Test-ModuleManifest': {e.Message}");

                return;
            }

            if (!pwsh.HadErrors)
            {
                if (results.Count() != 0)
                {
                    aad_access_token = results[0].BaseObject as string;
                }
            }

            WriteVerbose("Getting acr refresh token");
            var acrRefreshToken = AcrHttpHelper.GetAcrRefreshTokenAsync(registry, tenant, aad_access_token).Result;
            WriteVerbose("Getting acr access token");
            var acrAccessToken = AcrHttpHelper.GetAcrAccessTokenAsync(registry, acrRefreshToken).Result;
            WriteVerbose($"Getting manifest for {Name} - {Version}");
            var manifest = AcrHttpHelper.GetAcrRepositoryManifestAsync(registry, Name, Version, acrAccessToken).Result;
            var digest = manifest["layers"].FirstOrDefault()["digest"].ToString();
            WriteVerbose($"Downloading blob for {Name} - {Version}");
            var responseContent = AcrHttpHelper.GetAcrBlobAsync(registry, Name, digest, acrAccessToken).Result;

            using var content = responseContent.ReadAsStreamAsync().Result;
            using var fs = File.Create(Path);
            content.Seek(0, System.IO.SeekOrigin.Begin);
            content.CopyTo(fs);
        }

        #endregion
    }
}
