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
    internal static class ACRHelper
    {
        internal static PSResourceInfo Install(
            PSRepositoryInfo repo, 
            string moduleName, 
            string moduleVersion, 
            bool savePkg,
            bool asZip,
            List<string> installPath,
            PSCmdlet callingCmdlet)
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
            var pathToFile = Path.Combine(tempPath, $"{moduleName}.{moduleVersion}.zip");
            using var content = responseContent.ReadAsStreamAsync().Result;
            using var fs = File.Create(pathToFile);
            content.Seek(0, System.IO.SeekOrigin.Begin);
            content.CopyTo(fs);
            fs.Close();

            var pkgInfo = new PSResourceInfo(moduleName, moduleVersion, repo.Name);

            // If saving the package as a zip
            if  (savePkg && asZip) 
            {
                // Just move to the zip to the proper path
                Utils.MoveFiles(pathToFile, Path.Combine(installPath.FirstOrDefault(), $"{moduleName}.{moduleVersion}.zip"));

            }
            // If saving the package and unpacking OR installing the package
            else 
            {
                callingCmdlet.WriteVerbose($"Expanding module to temp path: {tempPath}");
                // Expand the zip file
                System.IO.Compression.ZipFile.ExtractToDirectory(pathToFile, tempPath);

                callingCmdlet.WriteVerbose("Expanding completed");
                File.Delete(pathToFile);

                Utils.MoveFilesIntoInstallPath(
                            pkgInfo,
                            isModule: true,
                            isLocalRepo: false,
                            savePkg,
                            moduleVersion,
                            tempPath,
                            installPath.FirstOrDefault(),
                            moduleVersion,
                            moduleVersion,
                            scriptPath: null,
                            callingCmdlet);

                        var expandedFolder = System.IO.Path.Combine(tempPath, moduleName);
                        callingCmdlet.WriteVerbose($"Expanded folder is: {expandedFolder}");
                        Directory.Delete(expandedFolder);
            }

            return pkgInfo;
        }
    }
}
