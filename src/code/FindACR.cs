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
                    AcrSearchHelper(repo);
                }
            }
        }

        #endregion

        #region Private Methods

        private void AcrDownloadBlob(string url, List<KeyValuePair<string, string>> defaultHeaders)
        {
            try
            {
                if (defaultHeaders != null)
                {
                    foreach (var header in defaultHeaders)
                    {
                        if (header.Key == "Authorization")
                        {
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", header.Value);
                        }
                        else if (header.Key == "Accept")
                        {
                            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(header.Value));
                        }
                        else
                        {
                            client.DefaultRequestHeaders.Add(header.Key, header.Value);
                        }
                    }
                }

                HttpRequestMessage request = new HttpRequestMessage();
                request.RequestUri = new Uri(url);
                request.Method = HttpMethod.Get;

                var response = client.SendAsync(request).GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    using var content = response.Content.ReadAsStreamAsync().Result;
                    using var fs = File.Create(Path);
                    content.Seek(0, System.IO.SeekOrigin.Begin);
                    content.CopyTo(fs);
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ", e.Message);
            }
        }

        private JObject GetResponse(
            List<KeyValuePair<string, string>> defaultHeaders,
            string url,
            string content,
            List<KeyValuePair<string, string>> contentHeaders,
            HttpMethod method)
        {
            try
            {
                if (defaultHeaders != null)
                {
                    foreach (var header in defaultHeaders)
                    {
                        if (header.Key == "Authorization")
                        {
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", header.Value);
                        }
                        else if (header.Key == "Accept")
                        {
                            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(header.Value));
                        }
                        else
                        {
                            client.DefaultRequestHeaders.Add(header.Key, header.Value);
                        }
                    }
                }

                HttpRequestMessage request = new HttpRequestMessage();
                if (!string.IsNullOrEmpty(content))
                {
                    request.Content = new StringContent(content);
                    request.Content.Headers.Clear();
                    if (contentHeaders != null)
                    {
                        foreach (var header in contentHeaders)
                        {
                            request.Content.Headers.Add(header.Key, header.Value);
                        }
                    }
                }

                request.RequestUri = new Uri(url);
                request.Method = method;

                HttpResponseMessage response = client.SendAsync(request).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
                return JsonConvert.DeserializeObject<JObject>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ", e.Message);
            }

            return null;
        }

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
                    WriteVerbose("Access Token: " + aad_access_token);
                }
            }

            try
            {
                WriteVerbose("Getting token for ACR");
                string content = $"grant_type=access_token&service={registry}&tenant={tenant}&access_token={aad_access_token}";
                var acrRefreshTokenJson = GetResponse(
                    null,
                    $"https://{registry}/oauth2/exchange",
                    content,
                    new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Content-Type", "application/x-www-form-urlencoded") },
                    HttpMethod.Post
                );

                string acr_refresh_token = acrRefreshTokenJson["refresh_token"].ToString();

                WriteVerbose($"ACR Refresh token {acr_refresh_token}");

                WriteVerbose("Getting access token for ACR");
                string scope = "repository:*:*";

                var acrAccessTokenJson = GetResponse(
                    null,
                    $"https://{registry}/oauth2/token",
                    $"grant_type=refresh_token&service={registry}&scope={scope}&refresh_token={acr_refresh_token}",
                    new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Content-Type", "application/x-www-form-urlencoded") },
                    HttpMethod.Post
                );

                string acr_access_token = acrAccessTokenJson["access_token"].ToString();

                WriteVerbose($"ACR access token {acr_access_token}");

                WriteVerbose("Getting ACR Manifests");

                var defaultHeaders = new List<KeyValuePair<string, string>> {
                    new KeyValuePair<string, string>("Authorization", acr_access_token),
                    new KeyValuePair<string, string>("Accept", "application/vnd.oci.image.manifest.v1+json")
                };

                var acrManifestJson = GetResponse(
                    defaultHeaders,
                    $"https://{registry}/v2/{Name}/manifests/{Version}",
                    null,
                    null,
                    HttpMethod.Get
                );

                string moduleBlobDigest = acrManifestJson["layers"][0]["digest"].ToString();

                WriteVerbose($"{Name} Module Blob Digest for {Version} {moduleBlobDigest}");

                string downloadUrl = $"https://{registry}/v2/{Name}/blobs/{moduleBlobDigest}";

                AcrDownloadBlob(downloadUrl, defaultHeaders);

                WriteVerbose("Downloaded module blob");

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
