// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using OrasProject.Oras.Registry.Remote.Auth;

namespace Microsoft.PowerShell.PSResourceGet
{
    /// <summary>
    /// Implements the ORAS ICredentialProvider interface for PSResourceGet.
    /// Handles three authentication pathways:
    /// 1. Credentials from SecretManagement vault (CredentialInfo provided)
    /// 2. Azure Identity via Utils.GetAzAccessToken (existing helper)
    /// 3. Anonymous/unauthenticated access
    /// </summary>
    internal class PSResourceGetCredentialProvider : ICredentialProvider
    {
        private readonly PSRepositoryInfo _repository;
        private readonly PSCmdlet _cmdletPassedIn;
        private readonly string _registryHost;
        private readonly HttpClient _httpClient;
        private Credential _cachedCredential;
        private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

        // Template for the ACR OAuth2 exchange endpoint
        private const string OAuthExchangeUrlTemplate = "https://{0}/oauth2/exchange";
        private const string RefreshTokenRequestBodyTemplate = "grant_type=access_token&service={0}&tenant={1}&access_token={2}";
        private const string RefreshTokenRequestBodyNoTenantTemplate = "grant_type=access_token&service={0}&access_token={1}";

        internal PSResourceGetCredentialProvider(PSRepositoryInfo repository, PSCmdlet cmdletPassedIn, HttpClient httpClient = null)
        {
            _repository = repository;
            _cmdletPassedIn = cmdletPassedIn;
            _registryHost = repository.Uri.Host;
            _httpClient = httpClient ?? new HttpClient();
            _cachedCredential = new Credential();
        }

        public async Task<Credential> ResolveCredentialAsync(string hostname, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(hostname))
            {
                throw new ArgumentException("Hostname cannot be null or empty.", nameof(hostname));
            }

            // Return cached credential if still valid
            if (!string.IsNullOrEmpty(_cachedCredential.RefreshToken) && DateTimeOffset.UtcNow < _tokenExpiry)
            {
                _cmdletPassedIn.WriteVerbose("Using cached ORAS credential.");
                return _cachedCredential;
            }

            string aadAccessToken;
            string tenantId;

            var repositoryCredentialInfo = _repository.CredentialInfo;
            if (repositoryCredentialInfo != null)
            {
                // Path 1: Credential from SecretsManagement vault
                _cmdletPassedIn.WriteVerbose("Retrieving access token from SecretManagement vault.");
                aadAccessToken = Utils.GetContainerRegistryAccessTokenFromSecretManagement(
                    _repository.Name,
                    repositoryCredentialInfo,
                    _cmdletPassedIn);

                if (string.IsNullOrEmpty(aadAccessToken))
                {
                    _cmdletPassedIn.WriteWarning("Failed to retrieve access token from SecretManagement vault.");
                    return new Credential();
                }

                tenantId = repositoryCredentialInfo.SecretName;
            }
            else
            {
                // Path 2: Azure Identity via existing Utils helper
                _cmdletPassedIn.WriteVerbose("Acquiring AAD access token via Utils.GetAzAccessToken.");
                aadAccessToken = Utils.GetAzAccessToken(_cmdletPassedIn);

                if (string.IsNullOrEmpty(aadAccessToken))
                {
                    // If Azure Identity fails, return empty credential for anonymous access
                    _cmdletPassedIn.WriteVerbose("No AAD token available; attempting anonymous access.");
                    return new Credential();
                }

                tenantId = null;
            }

            // Exchange AAD access token for ACR refresh token via OAuth2 exchange endpoint
            _cmdletPassedIn.WriteVerbose("Exchanging AAD access token for ACR refresh token.");
            try
            {
                string refreshToken = await ExchangeForAcrRefreshTokenAsync(aadAccessToken, tenantId, cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrEmpty(refreshToken))
                {
                    _cmdletPassedIn.WriteWarning("Failed to obtain ACR refresh token from exchange.");
                    return new Credential();
                }

                _cachedCredential = new Credential(RefreshToken: refreshToken);
                _tokenExpiry = DateTimeOffset.UtcNow.AddMinutes(55); // ACR tokens typically valid for ~60 min
                return _cachedCredential;
            }
            catch (Exception ex)
            {
                _cmdletPassedIn.WriteWarning($"Failed to exchange AAD token for ACR refresh token: {ex.Message}");
                return new Credential();
            }
        }

        /// <summary>
        /// Exchanges an AAD access token for an ACR refresh token via the OAuth2 exchange endpoint.
        /// </summary>
        private async Task<string> ExchangeForAcrRefreshTokenAsync(string aadAccessToken, string tenantId, CancellationToken cancellationToken)
        {
            string exchangeUrl = string.Format(OAuthExchangeUrlTemplate, _registryHost);
            string requestBody = string.IsNullOrEmpty(tenantId)
                ? string.Format(RefreshTokenRequestBodyNoTenantTemplate, _registryHost, aadAccessToken)
                : string.Format(RefreshTokenRequestBodyTemplate, _registryHost, tenantId, aadAccessToken);

            using var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
            using var response = await _httpClient.PostAsync(exchangeUrl, content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var jsonDoc = JsonDocument.Parse(responseBody);

            if (jsonDoc.RootElement.TryGetProperty("refresh_token", out JsonElement refreshTokenElement))
            {
                return refreshTokenElement.GetString();
            }

            return null;
        }
    }
}
