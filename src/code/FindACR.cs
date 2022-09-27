// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.ObjectModel;
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
        public string[] Name { get; set; }

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
                    AcrSearchHelper(repo); 
                }
            }
        }

        #endregion

        #region Private Methods

        private void AcrSearchHelper(PSRepositoryInfo repository) {
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
                    WriteVerbose("Access Token: " + aad_access_token);
                }
            }
            
            
            try	
            {
                var header = new AuthenticationHeaderValue("Bearer", aad_access_token);
                client.DefaultRequestHeaders.Authorization = header;
                string url = $"https://{registry}/oauth2/exchange";

                HttpRequestMessage request = new HttpRequestMessage();
                request.Content = new StringContent($"grant_type=access_token&service={registry}&tenant={tenant}&access_token={aad_access_token}");
                request.Content.Headers.Clear();
                request.Content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                request.RequestUri = new Uri(url);
                request.Method = HttpMethod.Post;
                
                HttpResponseMessage response = client.SendAsync(request).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
                string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                // Above three lines can be replaced with new helper method below
                // string responseBody = await client.GetStringAsync(uri);

                Console.WriteLine(responseBody);
            }
            catch(HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");	
                Console.WriteLine("Message :{0} ",e.Message);
            }
        }

        #endregion
    }
}
