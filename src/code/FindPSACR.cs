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

        private void AcrSearchHelper(PSRepositoryInfo repository, string aadAccessToken, string tenant)
        {
            // Call asynchronous network methods in a try/catch block to handle exceptions.
            string registry = repository.Uri.Host;

            try
            {
                WriteVerbose("Getting refresh token for ACR");

                // Get ACR refresh token.
                string content = $"grant_type=access_token&service={registry}&tenant={tenant}&access_token={aadAccessToken}";
                var acrRefreshTokenJson = GetResponse(
                    null,
                    $"https://{registry}/oauth2/exchange",
                    content,
                    new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Content-Type", "application/x-www-form-urlencoded") },
                    HttpMethod.Post
                );

                string acrRefreshToken = acrRefreshTokenJson["refresh_token"].ToString();
                WriteVerbose($"ACR Refresh token {acrRefreshToken}");


                // Get ACR access token.
                WriteVerbose("Getting access token for ACR");
                string scope = "repository:*:*";

                var acrAccessTokenJson = GetResponse(
                    null,
                    $"https://{registry}/oauth2/token",
                    $"grant_type=refresh_token&service={registry}&scope={scope}&refresh_token={acrRefreshToken}",
                    new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Content-Type", "application/x-www-form-urlencoded") },
                    HttpMethod.Post
                );

                string acrAccessToken = acrAccessTokenJson["access_token"].ToString();
                WriteVerbose($"ACR access token {acrAccessToken}");

                // Call Find APIs.

                // Scenario: Find package without version
                if (!Name.Contains("*"))
                {
                    // if (String.IsNullOrEmpty(Version)) // TODO: return latest version
                    if (Version.Equals("*"))
                    {
                        SearchAllVersionsHelper(acrAccessToken, registry);
                    }
                    else if (!Version.Contains("*"))
                    {
                        SearchSpecificVersionHelper(acrAccessToken, registry);
                    }
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ", e.Message);
            }
        }

        private void SearchAllVersionsHelper(string acrAccessToken, string registry)
    {
        // Invoke-RestMethod -uri "https://psgexp.azurecr.io/acr/v1/pester/_tags"

            var defaultHeaders = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("Authorization", acrAccessToken)
            };

            var searchResultJson = GetResponse(
                defaultHeaders,
                $"https://{registry}/acr/v1/{Name}/_tags",
                null,
                null,
                HttpMethod.Get
            );

            Console.WriteLine(searchResultJson["tags"].ToString());
            foreach (JToken tag in searchResultJson["tags"])
            {
                Console.WriteLine(tag["name"].ToString());
                Console.WriteLine(tag["digest"].ToString());
            }
        }

        private void SearchSpecificVersionHelper(string acrAccessToken, string registry)
        {
            // Invoke-RestMethod -uri "https://psgexp.azurecr.io/acr/v1/pester/_tags/5.3.3" -Headers $header -Method Get
            var defaultHeaders = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("Authorization", acrAccessToken)

            };

            System.Version.TryParse(Version, out System.Version pkgVersion);

            string pkgVersionAsString = pkgVersion.ToString();

            var searchResultJson = GetResponse(
                defaultHeaders,
                $"https://{registry}/acr/v1/{Name}/_tags/{pkgVersionAsString}",
                null,
                null,
                HttpMethod.Get

            );

            Console.WriteLine(searchResultJson["tag"]["name"].ToString());
            Console.WriteLine(searchResultJson["tag"]["digest"].ToString());

            //Console.WriteLine(searchResultJson.Property("tag"));
        }

        #endregion
    }
}