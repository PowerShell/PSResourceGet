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
    internal static class FindACRModule
    {
        internal static List<PSResourceInfo> Find(PSRepositoryInfo repo, string pkgName, string pkgVersion, PSCmdlet callingCmdlet)
        {
            List<PSResourceInfo> foundPkgs = new List<PSResourceInfo>();
            string accessToken = string.Empty;
            string tenantID = string.Empty;

            // Need to set up secret management vault before hand
            var repositoryCredentialInfo = repo.CredentialInfo;
            if (repositoryCredentialInfo != null)
            {
                accessToken = Utils.GetACRAccessTokenFromSecretManagement(
                    repo.Name,
                    repositoryCredentialInfo,
                    callingCmdlet);

                callingCmdlet.WriteVerbose("Access token retrieved.");

                tenantID = Utils.GetSecretInfoFromSecretManagement(
                    repo.Name,
                    repositoryCredentialInfo,
                    callingCmdlet);
            }

            // Call asynchronous network methods in a try/catch block to handle exceptions.
            string registry = repo.Uri.Host;

            callingCmdlet.WriteVerbose("Getting acr refresh token");
            var acrRefreshToken = AcrHttpHelper.GetAcrRefreshTokenAsync(registry, tenantID, accessToken).Result;
            callingCmdlet.WriteVerbose("Getting acr access token");
            var acrAccessToken = AcrHttpHelper.GetAcrAccessTokenAsync(registry, acrRefreshToken).Result;

            callingCmdlet.WriteVerbose("Getting tags");
            var foundTags = AcrHttpHelper.FindAcrImageTags(registry, pkgName, pkgVersion, acrAccessToken).Result;

            if (foundTags != null)
            {
                if (string.Equals(pkgVersion, "*", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var item in foundTags["tags"])
                    {
                        string tagVersion = item["name"].ToString();
                        string info = $"{pkgName} - {tagVersion} - {item["digest"]}";
                        foundPkgs.Add(new PSResourceInfo(name: pkgName, version: tagVersion));
                        callingCmdlet.WriteObject(info);
                    }
                }
                else
                {
                    // pkgVersion was used in the API call (same as foundTags["name"])
                    string info = $"{pkgName} - {pkgVersion} - {foundTags["tag"]["digest"]}";
                    foundPkgs.Add(new PSResourceInfo(name: pkgName, version: pkgVersion));
                    callingCmdlet.WriteObject(info);
                }
            }

            return foundPkgs;
        }
    }
}
