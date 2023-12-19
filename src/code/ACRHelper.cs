// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;

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
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

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
                string expandedPath = Path.Combine(tempPath, moduleName.ToLower(), moduleVersion);
                Directory.CreateDirectory(expandedPath);
                callingCmdlet.WriteVerbose($"Expanding module to temp path: {expandedPath}");
                // Expand the zip file
                System.IO.Compression.ZipFile.ExtractToDirectory(pathToFile, expandedPath);
                Utils.DeleteExtraneousFiles(callingCmdlet, moduleName, expandedPath);

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

                if (Directory.Exists(tempPath))
                {
                    try
                    {
                        Utils.DeleteDirectory(tempPath);
                        callingCmdlet.WriteVerbose(String.Format("Successfully deleted '{0}'", tempPath));
                    }
                    catch (Exception e)
                    {
                        ErrorRecord TempDirCouldNotBeDeletedError = new ErrorRecord(e, "errorDeletingTempInstallPath", ErrorCategory.InvalidResult, null);
                        callingCmdlet.WriteError(TempDirCouldNotBeDeletedError);
                    }
                }
            }

            return pkgInfo;
        }
    }
}
