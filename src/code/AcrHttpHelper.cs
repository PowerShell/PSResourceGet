// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


using Dbg = System.Diagnostics.Debug;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    internal static class AcrHttpHelper
    {
        const string acrRefreshTokenTemplate = "grant_type=access_token&service={0}&tenant={1}&access_token={2}"; // 0 - registry, 1 - tenant, 2 - access token
        const string acrAccessTokenTemplate = "grant_type=refresh_token&service={0}&scope=repository:*:*&refresh_token={1}"; // 0 - registry, 1 - refresh token
        const string acrOAuthExchangeUrlTemplate = "https://{0}/oauth2/exchange"; // 0 - registry
        const string acrOAuthTokenUrlTemplate = "https://{0}/oauth2/token"; // 0 - registry
        const string acrManifestUrlTemplate = "https://{0}/v2/{1}/manifests/{2}"; // 0 - registry, 1 - repo(modulename), 2 - tag(version)
        const string acrBlobDownloadUrlTemplate = "https://{0}/v2/{1}/blobs/{2}"; // 0 - registry, 1 - repo(modulename), 2 - layer digest
        const string acrFindImageVersionUrlTemplate = "https://{0}/acr/v1/{1}/_tags{2}"; // 0 - registry, 1 - repo(modulename), 2 - /tag(version)

        private static readonly HttpClient s_client = new HttpClient();

        internal static async Task<string> GetAcrRefreshTokenAsync(string registry, string tenant, string accessToken)
        {
            string content = string.Format(acrRefreshTokenTemplate, registry, tenant, accessToken);
            var contentHeaders = new Collection<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Content-Type", "application/x-www-form-urlencoded") };
            string exchangeUrl = string.Format(acrOAuthExchangeUrlTemplate, registry);
            return (await GetHttpResponseJObject(exchangeUrl, HttpMethod.Post, content, contentHeaders))["refresh_token"].ToString();
        }

        internal static async Task<string> GetAcrAccessTokenAsync(string registry, string refreshToken)
        {
            string content = string.Format(acrAccessTokenTemplate, registry, refreshToken);
            var contentHeaders = new Collection<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Content-Type", "application/x-www-form-urlencoded") };
            string tokenUrl = string.Format(acrOAuthTokenUrlTemplate, registry);
            return (await GetHttpResponseJObject(tokenUrl, HttpMethod.Post, content, contentHeaders))["access_token"].ToString();
        }

        internal static async Task<JObject> GetAcrRepositoryManifestAsync(string registry, string repositoryName, string version, string acrAccessToken)
        {
            string manifestUrl = string.Format(acrManifestUrlTemplate, registry, repositoryName, version);
            var defaultHeaders = GetDefaultHeaders(acrAccessToken);
            return await GetHttpResponseJObject(manifestUrl, HttpMethod.Get, defaultHeaders);
        }

        internal static async Task<HttpContent> GetAcrBlobAsync(string registry, string repositoryName, string digest, string acrAccessToken)
        {
            string blobUrl = string.Format(acrBlobDownloadUrlTemplate, registry, repositoryName, digest);
            var defaultHeaders = GetDefaultHeaders(acrAccessToken);
            return await GetHttpContentResponseJObject(blobUrl, defaultHeaders);
        }

        internal static async Task<JObject> FindAcrImageTags(string registry, string repositoryName, string version, string acrAccessToken)
        {
            try
            {
                string resolvedVersion = string.Equals(version, "*", StringComparison.OrdinalIgnoreCase) ? null : $"/{version}";
                string findImageUrl = string.Format(acrFindImageVersionUrlTemplate, registry, repositoryName, resolvedVersion);
                var defaultHeaders = GetDefaultHeaders(acrAccessToken);
                return await GetHttpResponseJObject(findImageUrl, HttpMethod.Get, defaultHeaders);
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error finding ACR artifact: " + e.Message);
            }
        }

        internal static async Task<HttpContent> GetHttpContentResponseJObject(string url, Collection<KeyValuePair<string, string>> defaultHeaders)
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                SetDefaultHeaders(defaultHeaders);
                return await SendContentRequestAsync(request);
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to retrieve response: " + e.Message);
            }
        }

        internal static async Task<JObject> GetHttpResponseJObject (string url, HttpMethod method, Collection<KeyValuePair<string, string>> defaultHeaders)
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(method, url);
                SetDefaultHeaders(defaultHeaders);
                return await SendRequestAsync(request);
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to retrieve response: " + e.Message);
            }
        }

        internal static async Task<JObject> GetHttpResponseJObject (string url, HttpMethod method, string content, Collection<KeyValuePair<string, string>> contentHeaders)
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(method, url);

                if (string.IsNullOrEmpty(content))
                {
                    throw new ArgumentNullException("content");
                }

                request.Content = new StringContent(content);
                request.Content.Headers.Clear();
                if (contentHeaders != null)
                {
                    foreach (var header in contentHeaders)
                    {
                        request.Content.Headers.Add(header.Key, header.Value);
                    }
                }

                return await SendRequestAsync(request);
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to retrieve response: " + e.Message);
            }
        }

        private static void SetDefaultHeaders(Collection<KeyValuePair<string, string>> defaultHeaders)
        {
            s_client.DefaultRequestHeaders.Clear();
            if (defaultHeaders != null)
            {
                foreach (var header in defaultHeaders)
                {
                    if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        s_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", header.Value);
                    }
                    else if (string.Equals(header.Key, "Accept", StringComparison.OrdinalIgnoreCase))
                    {
                        s_client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(header.Value));
                    }
                    else
                    {
                        s_client.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }
            }
        }

        private static async Task<HttpContent> SendContentRequestAsync(HttpRequestMessage message)
        {
            try
            {
                HttpResponseMessage response = await s_client.SendAsync(message);
                response.EnsureSuccessStatusCode();
                return response.Content;
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to retrieve response: " + e.Message);
            }
        }

        private static async Task<JObject> SendRequestAsync(HttpRequestMessage message)
        {
            try
            {
                HttpResponseMessage response = await s_client.SendAsync(message);
                response.EnsureSuccessStatusCode();
                return JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync());
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Error occured while trying to retrieve response: " + e.Message);
            }
        }

        private static Collection<KeyValuePair<string, string>> GetDefaultHeaders(string acrAccessToken)
        {
            return new Collection<KeyValuePair<string, string>> {
                    new KeyValuePair<string, string>("Authorization", acrAccessToken),
                    new KeyValuePair<string, string>("Accept", "application/vnd.oci.image.manifest.v1+json")
                };
        }
    }
}