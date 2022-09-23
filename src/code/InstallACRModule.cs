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
    internal static class InstallACRModule
    {
        internal static PSResourceInfo Install(PSRepositoryInfo repo, string moduleName, string moduleVersion, PSCmdlet callingCmdlet)
        {
            string accessToken = string.Empty;
            string tenantID = string.Empty;
            string tempPath = Path.GetTempPath();

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
            callingCmdlet.WriteVerbose($"Getting manifest for {moduleName} - {moduleVersion}");
            var manifest = AcrHttpHelper.GetAcrRepositoryManifestAsync(registry, moduleName, moduleVersion, acrAccessToken).Result;
            var digest = manifest["layers"].FirstOrDefault()["digest"].ToString();
            callingCmdlet.WriteVerbose($"Downloading blob for {moduleName} - {moduleVersion}");
            var responseContent = AcrHttpHelper.GetAcrBlobAsync(registry, moduleName, digest, acrAccessToken).Result;

            callingCmdlet.WriteVerbose($"Writing module zip to temp path: {tempPath}");

            // download the module
            var pathToFile = Path.Combine(tempPath, moduleName + ".zip");
            using var content = responseContent.ReadAsStreamAsync().Result;
            using var fs = File.Create(pathToFile);
            content.Seek(0, System.IO.SeekOrigin.Begin);
            content.CopyTo(fs);
            fs.Close();

            callingCmdlet.WriteVerbose($"Expanding module to temp path: {tempPath}");

            // Expand the zip file
            System.IO.Compression.ZipFile.ExtractToDirectory(pathToFile, tempPath);

            callingCmdlet.WriteVerbose("Exapnding completed");

            File.Delete(pathToFile);

            return new PSResourceInfo(moduleName, moduleVersion);
        }
    }
}
