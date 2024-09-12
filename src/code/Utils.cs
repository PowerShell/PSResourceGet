using System.Net;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using NuGet.Versioning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Runtime.InteropServices;
using Microsoft.PowerShell.Commands;
using Microsoft.PowerShell.PSResourceGet.Cmdlets;
using System.Net.Http;
using System.Globalization;
using System.Security;
using Azure.Core;
using Azure.Identity;
using System.Threading.Tasks;
using System.Threading;

namespace Microsoft.PowerShell.PSResourceGet.UtilClasses
{
    #region Utils

    internal static class Utils
    {
        #region Enums

        public enum MetadataFileType
        {
            ModuleManifest,
            ScriptFile,
            Nuspec,
            None
        }

        # endregion

        #region String fields

        public static readonly string[] EmptyStrArray = Array.Empty<string>();
        public static readonly char[] WhitespaceSeparator = new char[] { ' ' };
        public const string PSDataFileExt = ".psd1";
        public const string PSScriptFileExt = ".ps1";
        private const string ConvertJsonToHashtableScript = @"
            param (
                [string] $json
            )

            function ConvertToHash
            {
                param (
                    [pscustomobject] $object
                )

                $output = @{}
                $object | Microsoft.PowerShell.Utility\Get-Member -MemberType NoteProperty | ForEach-Object {
                    $name = $_.Name
                    $value = $object.($name)

                    if ($value -is [object[]])
                    {
                        $array = @()
                        $value | ForEach-Object {
                            $array += (ConvertToHash $_)
                        }
                        $output.($name) = $array
                    }
                    elseif ($value -is [pscustomobject])
                    {
                        $output.($name) = (ConvertToHash $value)
                    }
                    else
                    {
                        $output.($name) = $value
                    }
                }

                $output
            }

            $customObject = Microsoft.PowerShell.Utility\ConvertFrom-Json -InputObject $json
            return ConvertToHash $customObject
        ";

        #endregion

        #region Path fields

        private static string s_tempHome = null;

        #endregion

        #region String methods

        public static string TrimQuotes(string name)
        {
            return name.Trim('\'', '"');
        }

        public static string QuoteName(string name)
        {
            bool quotesNeeded = false;
            foreach (var c in name)
            {
                if (Char.IsWhiteSpace(c))
                {
                    quotesNeeded = true;
                    break;
                }
            }

            if (!quotesNeeded)
            {
                return name;
            }

            return "'" + CodeGeneration.EscapeSingleQuotedStringContent(name) + "'";
        }

        public static string[] GetStringArrayFromString(string[] delimeter, string stringToConvertToArray)
        {
            // This will be a string where entries are separated by space.
            if (String.IsNullOrEmpty(stringToConvertToArray))
            {
                return Utils.EmptyStrArray;
            }

            return stringToConvertToArray.Split(delimeter, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Converts an ArrayList of object types to a string array.
        /// </summary>
        public static string[] GetStringArray(ArrayList list)
        {
            if (list == null) { return null; }

            var strArray = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                strArray[i] = list[i] as string;
            }

            return strArray;
        }

        public static string[] ProcessNameWildcards(
            string[] pkgNames,
            bool removeWildcardEntries,
            out string[] errorMsgs,
            out bool isContainWildcard)
        {
            List<string> namesWithSupportedWildcards = new List<string>();
            List<string> errorMsgsList = new List<string>();

            if (pkgNames == null)
            {
                isContainWildcard = true;
                errorMsgs = errorMsgsList.ToArray();
                return new string[] { "*" };
            }

            isContainWildcard = false;
            foreach (string name in pkgNames)
            {
                if (WildcardPattern.ContainsWildcardCharacters(name))
                {
                    if (removeWildcardEntries)
                    {
                        // Tag   // CommandName  // DSCResourceName
                        errorMsgsList.Add($"{name} will be discarded from the provided entries.");
                        continue;
                    }

                    if (String.Equals(name, "*", StringComparison.InvariantCultureIgnoreCase))
                    {
                        isContainWildcard = true;
                        errorMsgs = new string[] { };
                        return new string[] { "*" };
                    }

                    if (name.Contains("?") || name.Contains("["))
                    {
                        errorMsgsList.Add(String.Format("-Name with wildcards '?' and '[' are not supported for this cmdlet so Name entry: {0} will be discarded.", name));
                        continue;
                    }

                    isContainWildcard = true;
                    namesWithSupportedWildcards.Add(name);
                }
                else
                {
                    namesWithSupportedWildcards.Add(name);
                }
            }

            errorMsgs = errorMsgsList.ToArray();
            return namesWithSupportedWildcards.ToArray();
        }

        public static string FormatRequestsExceptions(Exception exception, HttpRequestMessage request)
        {
            string exMsg = $"'{exception.Message}' Request sent: '{request.RequestUri.AbsoluteUri}'";
            if (exception.InnerException != null && !string.IsNullOrEmpty(exception.InnerException.Message))
            {
                exMsg += $" Inner exception: '{exception.InnerException.Message}'";
            }

            return exMsg;
        }

        public static string FormatCredentialRequestExceptions(Exception exception)
        {
            string exMsg = $"'{exception.Message}' Re-run the command with -Credential.";
            if (exception.InnerException != null && !string.IsNullOrEmpty(exception.InnerException.Message))
            {
                exMsg += $" Inner exception: '{exception.InnerException.Message}'";
            }

            return exMsg;
        }

        #endregion

        #region Version methods

        public static bool TryGetVersionType(
            string version,
            out NuGetVersion nugetVersion,
            out VersionRange versionRange,
            out VersionType versionType,
            out string error)
        {
            error = String.Empty;
            nugetVersion = null;
            versionRange = null;
            versionType = VersionType.NoVersion;

            if (String.IsNullOrEmpty(version))
            {
                return true;
            }

            if (version.Trim().Equals("*"))
            {
                // this method is called for find and install version parameter.
                // for find, version = "*" means VersionRange.All
                // for install, version = "*", means find latest version. This is handled in Install
                versionRange = VersionRange.All;
                versionType = VersionType.VersionRange;
                return true;
            }

            bool isVersionRange;
            if (version.Contains("*"))
            {
                string modifiedVersion;
                string[] versionSplit = version.Split(new string[] { "." }, StringSplitOptions.None);
                if (versionSplit.Length == 2 && versionSplit[1].Equals("*"))
                {
                    // eg: 2.* should translate to the version range "[2.0,2.99999]"
                    modifiedVersion = $"[{versionSplit[0]}.0,{versionSplit[0]}.999999]";
                }
                else if (versionSplit.Length == 3 && versionSplit[2].Equals("*"))
                {
                    // eg: 2.1.* should translate to the version range "[2.1.0,2.1.99999]"
                    modifiedVersion = $"[{versionSplit[0]}.{versionSplit[1]}.0,{versionSplit[0]}.{versionSplit[1]}.999999]";
                }
                else if (versionSplit.Length == 4 && versionSplit[3].Equals("*"))
                {
                    // eg: 2.8.8.* should translate to the version range "[2.1.3.0,2.1.3.99999]"
                    modifiedVersion = $"[{versionSplit[0]}.{versionSplit[1]}.{versionSplit[2]}.0,{versionSplit[0]}.{versionSplit[1]}.{versionSplit[2]}.999999]";
                }
                else
                {
                    error = "Argument for -Version parameter is not in the proper format";
                    return false;
                }

                VersionRange.TryParse(modifiedVersion, out versionRange);
                versionType = VersionType.VersionRange;

                return true;
            }

            bool isNugetVersion = NuGetVersion.TryParse(version, out nugetVersion);
            isVersionRange = VersionRange.TryParse(version, out versionRange);

            if (!isNugetVersion && !isVersionRange)
            {
                error = "Argument for -Version parameter is not in the proper format";
                return false;
            }

            if (isNugetVersion)
            {
                versionType = VersionType.SpecificVersion;
            }
            else if (isVersionRange)
            {
                versionType = VersionType.VersionRange;
            }

            return true;
        }

        public static string GetNormalizedVersionString(
            string versionString,
            string prerelease)
        {
            // versionString may be like 1.2.0.0 or 1.2.0
            // prerelease    may be      null    or "alpha1"
            // possible passed in examples:
            // versionString: "1.2.0"   prerelease: "alpha1"
            // versionString: "1.2.0"   prerelease: ""        <- doubtful though
            // versionString: "1.2.0.0" prerelease: "alpha1"
            // versionString: "1.2.0.0" prerelease: ""

            if (String.IsNullOrEmpty(prerelease))
            {
                return versionString;
            }

            int numVersionDigits = versionString.Split('.').Count();

            if (numVersionDigits == 3)
            {
                // versionString: "1.2.0" prerelease: "alpha1"
                return versionString + "-" + prerelease;
            }
            else if (numVersionDigits == 4)
            {
                // versionString: "1.2.0.0" prerelease: "alpha1"
                return versionString.Substring(0, versionString.LastIndexOf('.')) + "-" + prerelease;
            }

            return versionString;
        }

        public static bool TryParseVersionOrVersionRange(
           string version,
           out VersionRange versionRange)
        {
            versionRange = null;
            if (version == null) { return false; }

            if (version.Trim().Equals("*"))
            {
                versionRange = VersionRange.All;
                return true;
            }

            // parse as NuGetVersion
            if (NuGetVersion.TryParse(version, out NuGetVersion nugetVersion))
            {
                versionRange = new VersionRange(
                    minVersion: nugetVersion,
                    includeMinVersion: true,
                    maxVersion: nugetVersion,
                    includeMaxVersion: true,
                    floatRange: null,
                    originalString: version);
                return true;
            }

            return VersionRange.TryParse(version, out versionRange);
        }

        public static bool GetVersionForInstallPath(
            string installedPkgPath,
            bool isModule,
            PSCmdlet cmdletPassedIn,
            out NuGetVersion pkgNuGetVersion)
        {
            // this method returns false if the PSGetModuleInfo.xml or {pkgName}_InstalledScriptInfo.xml file
            // could not be parsed properly, or the version from it could not be parsed into a NuGetVersion.
            // In this case the caller method (i.e GetHelper.FilterPkgPathsByVersion()) should skip the current
            // installed package path or reassign NuGetVersion variable passed in to a non-null value as it sees fit.

            // for Modules, installedPkgPath will look like this:
            // ./PowerShell/Modules/test_module/3.0.0
            // for Scripts, installedPkgPath will look like this:
            // ./PowerShell/Scripts/test_script.ps1
            string pkgName = isModule ? String.Empty : Utils.GetInstalledPackageName(installedPkgPath);

            string packageInfoXMLFilePath = isModule ? Path.Combine(installedPkgPath, "PSGetModuleInfo.xml") : Path.Combine((new DirectoryInfo(installedPkgPath).Parent).FullName, "InstalledScriptInfos", $"{pkgName}_InstalledScriptInfo.xml");
            if (!PSResourceInfo.TryRead(packageInfoXMLFilePath, out PSResourceInfo psGetInfo, out string errorMsg))
            {
                cmdletPassedIn.WriteVerbose(String.Format(
                    "The {0} file found at location: {1} cannot be parsed due to {2}",
                    isModule ? "PSGetModuleInfo.xml" : $"{pkgName}_InstalledScriptInfo.xml",
                    packageInfoXMLFilePath,
                    errorMsg));
                pkgNuGetVersion = null;
                return false;
            }

            psGetInfo.AdditionalMetadata.TryGetValue("NormalizedVersion", out string normalizedVersion);

            if (!NuGetVersion.TryParse(
                    value: normalizedVersion,
                    version: out pkgNuGetVersion))
            {
                cmdletPassedIn.WriteVerbose(String.Format("Leaf directory in path '{0}' cannot be parsed into a version.", installedPkgPath));
                return false;
            }

            return true;
        }

        #endregion

        #region Uri methods

        public static bool TryCreateValidUri(
            string uriString,
            PSCmdlet cmdletPassedIn,
            out Uri uriResult,
            out ErrorRecord errorRecord)
        {
            errorRecord = null;
            if (Uri.TryCreate(uriString, UriKind.Absolute, out uriResult))
            {
                return true;
            }

            Exception ex;
            try
            {
                // This is needed for a relative path Uri string. Does not throw error for an absolute path.
                var filePath = cmdletPassedIn.GetResolvedProviderPathFromPSPath(uriString, out ProviderInfo provider).First();

                if (Uri.TryCreate(filePath, UriKind.Absolute, out uriResult))
                {
                    return true;
                }

                ex = new PSArgumentException($"Invalid Uri file path: {uriString}");
            }
            catch (Exception e)
            {
                ex = e;
            }

            errorRecord = new ErrorRecord(
                new PSArgumentException($"The provided Uri is not valid: {uriString}. It must be of Uri Scheme: HTTP, HTTPS, FTP or a file path", ex),
                "InvalidUri",
                ErrorCategory.InvalidArgument,
                cmdletPassedIn);

            return false;
        }

        #endregion

        #region PSCredentialInfo methods

        public static bool TryCreateValidPSCredentialInfo(
            PSObject credentialInfoCandidate,
            PSCmdlet cmdletPassedIn,
            out PSCredentialInfo repoCredentialInfo,
            out ErrorRecord errorRecord)
        {
            repoCredentialInfo = null;
            errorRecord = null;

            try
            {
                if (!string.IsNullOrEmpty((string)credentialInfoCandidate.Properties[PSCredentialInfo.VaultNameAttribute]?.Value)
                    && !string.IsNullOrEmpty((string)credentialInfoCandidate.Properties[PSCredentialInfo.SecretNameAttribute]?.Value))
                {
                    PSCredential credential = null;
                    if (credentialInfoCandidate.Properties[PSCredentialInfo.CredentialAttribute] != null)
                    {
                        try
                        {
                            credential = (PSCredential)credentialInfoCandidate.Properties[PSCredentialInfo.CredentialAttribute].Value;
                        }
                        catch (Exception e)
                        {
                            errorRecord = new ErrorRecord(
                                new PSArgumentException($"Invalid CredentialInfo {PSCredentialInfo.CredentialAttribute}", e),
                                "InvalidCredentialInfo",
                                ErrorCategory.InvalidArgument,
                                cmdletPassedIn);

                            return false;
                        }
                    }

                    repoCredentialInfo = new PSCredentialInfo(
                        (string)credentialInfoCandidate.Properties[PSCredentialInfo.VaultNameAttribute].Value,
                        (string)credentialInfoCandidate.Properties[PSCredentialInfo.SecretNameAttribute].Value,
                        credential
                    );

                    return true;
                }
                else
                {
                    errorRecord = new ErrorRecord(
                        new PSArgumentException($"Invalid CredentialInfo, must include non-empty {PSCredentialInfo.VaultNameAttribute} and {PSCredentialInfo.SecretNameAttribute}, and optionally a {PSCredentialInfo.CredentialAttribute}"),
                        "InvalidCredentialInfo",
                        ErrorCategory.InvalidArgument,
                        cmdletPassedIn);

                    return false;
                }
            }
            catch (Exception e)
            {
                errorRecord = new ErrorRecord(
                    new PSArgumentException("Invalid CredentialInfo values", e),
                    "InvalidCredentialInfo",
                    ErrorCategory.InvalidArgument,
                    cmdletPassedIn);

                return false;
            }
        }

        public static PSCredential GetRepositoryCredentialFromSecretManagement(
            string repositoryName,
            PSCredentialInfo repositoryCredentialInfo,
            PSCmdlet cmdletPassedIn)
        {
            if (!IsSecretManagementVaultAccessible(repositoryName, repositoryCredentialInfo, cmdletPassedIn))
            {
                cmdletPassedIn.ThrowTerminatingError(
                    new ErrorRecord(
                        new PSInvalidOperationException($"Cannot access Microsoft.PowerShell.SecretManagement vault \"{repositoryCredentialInfo.VaultName}\" for PSResourceRepository ({repositoryName}) authentication."),
                        "RepositoryCredentialSecretManagementInaccessibleVault",
                        ErrorCategory.ResourceUnavailable,
                        cmdletPassedIn));

                return null;
            }

            try
            {
                using (System.Management.Automation.PowerShell pwsh = System.Management.Automation.PowerShell.Create())
                {
                    var module = pwsh.AddCommand("Microsoft.PowerShell.Core\\Import-Module").AddParameters(
                            new Hashtable() {
                                { "Name", "Microsoft.PowerShell.SecretManagement"},
                                { "PassThru", true}
                            }).Invoke<PSModuleInfo>();

                    if (pwsh.HadErrors || pwsh.Streams.Error.Count > 0)
                    {
                        foreach (var err in pwsh.Streams.Error)
                        {
                            cmdletPassedIn.WriteError(err);
                        }

                        return null;
                    }

                    if (module == null)
                    {
                        cmdletPassedIn.ThrowTerminatingError(
                            new ErrorRecord(
                                new PSInvalidOperationException(
                                    message: $"Microsoft.PowerShell.SecretManagement module could not be imported for PSResourceRepository '{repositoryName}' authentication."),
                                "RepositoryCredentialCannotLoadSecretManagementModule",
                                ErrorCategory.InvalidOperation,
                                cmdletPassedIn));

                        return null;
                    }

                    pwsh.Commands.Clear();
                    var results = pwsh.AddCommand("Microsoft.PowerShell.SecretManagement\\Get-Secret").AddParameters(
                        new Hashtable() {
                            { "Vault", repositoryCredentialInfo.VaultName },
                            { "Name", repositoryCredentialInfo.SecretName }
                        }).Invoke<Object>();

                    if (pwsh.HadErrors || pwsh.Streams.Error.Count > 0)
                    {
                        foreach (var err in pwsh.Streams.Error)
                        {
                            cmdletPassedIn.WriteError(err);
                        }

                        return null;
                    }

                    var secretValue = (results?.Count == 1) ? results[0] : null;
                    if (secretValue == null)
                    {
                        cmdletPassedIn.ThrowTerminatingError(
                            new ErrorRecord(
                                new PSInvalidOperationException(
                                    message: $"Microsoft.PowerShell.SecretManagement\\Get-Secret encountered an error while reading secret '{repositoryCredentialInfo.SecretName}' from vault '{repositoryCredentialInfo.VaultName}' for PSResourceRepository '{repositoryName}' authentication."),
                                "RepositoryCredentialCannotGetSecretFromVault",
                                ErrorCategory.InvalidOperation,
                                cmdletPassedIn));

                        return null;
                    }

                    if (secretValue is PSObject secretObject)
                    {
                        if (secretObject.BaseObject is PSCredential secretCredential)
                        {
                            return secretCredential;
                        }
                        else if (secretObject.BaseObject is SecureString secretString)
                        {
                            return new PSCredential("token", secretString);
                        }
                    }

                    cmdletPassedIn.ThrowTerminatingError(
                        new ErrorRecord(
                            new PSNotSupportedException($"Secret '{repositoryCredentialInfo.SecretName}' from vault '{repositoryCredentialInfo.VaultName}' has an invalid type. The only supported type is PSCredential."),
                            "RepositoryCredentialInvalidSecretType",
                            ErrorCategory.InvalidType,
                            cmdletPassedIn));

                    return null;
                }
            }
            catch (Exception e)
            {
                cmdletPassedIn.ThrowTerminatingError(
                    new ErrorRecord(
                        new PSInvalidOperationException(
                            message: $"Microsoft.PowerShell.SecretManagement\\Get-Secret encountered an error while reading secret '{repositoryCredentialInfo.SecretName}' from vault '{repositoryCredentialInfo.VaultName}' for PSResourceRepository '{repositoryName}' authentication.",
                            innerException: e),
                        "RepositoryCredentialCannotGetSecretFromVault",
                        ErrorCategory.InvalidOperation,
                        cmdletPassedIn));

                return null;
            }
        }

        public static string GetAzAccessToken()
        {
            var credOptions = new DefaultAzureCredentialOptions
            {
                ExcludeEnvironmentCredential = true,
                ExcludeVisualStudioCodeCredential = true,
                ExcludeVisualStudioCredential = true,
                ExcludeWorkloadIdentityCredential = true,
                ExcludeManagedIdentityCredential = true, // ManagedIdentityCredential makes the experience slow
                ExcludeSharedTokenCacheCredential = true, // SharedTokenCacheCredential is not supported on macOS
                ExcludeAzureCliCredential = false,
                ExcludeAzurePowerShellCredential = false,
                ExcludeInteractiveBrowserCredential = false
            };

            var dCred = new DefaultAzureCredential(credOptions);
            var tokenRequestContext = new TokenRequestContext(new string[] { "https://management.azure.com/.default" });
            var token = dCred.GetTokenAsync(tokenRequestContext).Result;
            return token.Token;
        }

        public static string GetContainerRegistryAccessTokenFromSecretManagement(
            string repositoryName,
            PSCredentialInfo repositoryCredentialInfo,
            PSCmdlet cmdletPassedIn)
        {
            if (!IsSecretManagementVaultAccessible(repositoryName, repositoryCredentialInfo, cmdletPassedIn))
            {
                cmdletPassedIn.ThrowTerminatingError(
                    new ErrorRecord(
                        new PSInvalidOperationException($"Cannot access Microsoft.PowerShell.SecretManagement vault \"{repositoryCredentialInfo.VaultName}\" for PSResourceRepository ({repositoryName}) authentication."),
                        "RepositoryCredentialSecretManagementInaccessibleVault",
                        ErrorCategory.ResourceUnavailable,
                        cmdletPassedIn));
                return null;
            }

            var results = PowerShellInvoker.InvokeScriptWithHost<object>(
                cmdlet: cmdletPassedIn,
                script: @"
                    param (
                        [string] $VaultName,
                        [string] $SecretName
                    )
                    $module = Microsoft.PowerShell.Core\Import-Module -Name Microsoft.PowerShell.SecretManagement -PassThru
                    if ($null -eq $module) {
                        return
                    }
                    & $module ""Get-Secret"" -Name $SecretName -Vault $VaultName
                ",
                args: new object[] { repositoryCredentialInfo.VaultName, repositoryCredentialInfo.SecretName },
                out Exception terminatingError);

            var secretValue = (results.Count == 1) ? results[0] : null;
            if (secretValue == null)
            {
                cmdletPassedIn.ThrowTerminatingError(
                    new ErrorRecord(
                        new PSInvalidOperationException(
                            message: $"Microsoft.PowerShell.SecretManagement\\Get-Secret encountered an error while reading secret \"{repositoryCredentialInfo.SecretName}\" from vault \"{repositoryCredentialInfo.VaultName}\" for PSResourceRepository ({repositoryName}) authentication.",
                            innerException: terminatingError),
                        "ContainerRegistryRepositoryCannotGetSecretFromVault",
                        ErrorCategory.InvalidOperation,
                        cmdletPassedIn));
            }

            if (secretValue is SecureString secretSecureString)
            {
                string password = new NetworkCredential(string.Empty, secretSecureString).Password;
                return password;
            }
            else if (secretValue is PSCredential psCredSecret)
            {
                string password = new NetworkCredential(string.Empty, psCredSecret.Password).Password;
                return password;
            }

            cmdletPassedIn.ThrowTerminatingError(
                new ErrorRecord(
                    new PSNotSupportedException($"Secret \"{repositoryCredentialInfo.SecretName}\" from vault \"{repositoryCredentialInfo.VaultName}\" has an invalid type. The only supported type is PSCredential."),
                    "ContainerRegistryRepositoryTokenIsInvalidSecretType",
                    ErrorCategory.InvalidType,
                    cmdletPassedIn));

            return null;
        }

        public static void SaveRepositoryCredentialToSecretManagementVault(
            string repositoryName,
            PSCredentialInfo repositoryCredentialInfo,
            PSCmdlet cmdletPassedIn)
        {
            if (!IsSecretManagementVaultAccessible(repositoryName, repositoryCredentialInfo, cmdletPassedIn))
            {
                cmdletPassedIn.ThrowTerminatingError(
                    new ErrorRecord(
                        new PSInvalidOperationException($"Cannot access Microsoft.PowerShell.SecretManagement vault \"{repositoryCredentialInfo.VaultName}\" for PSResourceRepository ({repositoryName}) authentication."),
                        "RepositoryCredentialSecretManagementInaccessibleVault",
                        ErrorCategory.ResourceUnavailable,
                        cmdletPassedIn));
                return;
            }

            try
            {
                using (System.Management.Automation.PowerShell pwsh = System.Management.Automation.PowerShell.Create())
                {
                    var module = pwsh.AddCommand("Microsoft.PowerShell.Core\\Import-Module").AddParameters(
                            new Hashtable() {
                                { "Name", "Microsoft.PowerShell.SecretManagement"},
                                { "PassThru", true}
                            }).Invoke<PSModuleInfo>();

                    if (pwsh.HadErrors || pwsh.Streams.Error.Count > 0)
                    {
                        foreach (var err in pwsh.Streams.Error)
                        {
                            cmdletPassedIn.WriteError(err);
                        }

                        return;
                    }

                    if (module == null)
                    {
                        cmdletPassedIn.ThrowTerminatingError(
                            new ErrorRecord(
                                new PSInvalidOperationException(
                                    message: $"Microsoft.PowerShell.SecretManagement module could not be imported for PSResourceRepository '{repositoryName}' authentication."),
                                "RepositoryCredentialCannotLoadSecretManagementModule",
                                ErrorCategory.InvalidOperation,
                                cmdletPassedIn));

                        return;
                    }

                    pwsh.Commands.Clear();
                    var results = pwsh.AddCommand("Microsoft.PowerShell.SecretManagement\\Set-Secret").AddParameters(
                        new Hashtable() {
                            { "Secret", repositoryCredentialInfo.Credential},
                            { "Vault", repositoryCredentialInfo.VaultName },
                            { "Name", repositoryCredentialInfo.SecretName }
                        }).Invoke<Object>();

                    if (pwsh.HadErrors || pwsh.Streams.Error.Count > 0)
                    {
                        foreach (var err in pwsh.Streams.Error)
                        {
                            cmdletPassedIn.WriteError(err);
                        }
                    }

                    return;
                }
            }
            catch (Exception e)
            {
                cmdletPassedIn.ThrowTerminatingError(
                    new ErrorRecord(
                        new PSInvalidOperationException(
                            message: $"Microsoft.PowerShell.SecretManagement\\Set-Secret encountered an error while adding secret '{repositoryCredentialInfo.SecretName}' to vault '{repositoryCredentialInfo.VaultName}' for PSResourceRepository '{repositoryName}' authentication.",
                            innerException: e),
                        "RepositoryCredentialCannotAddSecretToVault",
                        ErrorCategory.InvalidOperation,
                        cmdletPassedIn));
            }
        }

        public static bool IsSecretManagementModuleAvailable(
            string repositoryName,
            PSCmdlet cmdletPassedIn)
        {
            try
            {
                using (System.Management.Automation.PowerShell pwsh = System.Management.Automation.PowerShell.Create())
                {
                    var module = pwsh.AddCommand("Microsoft.PowerShell.Core\\Import-Module").AddParameters(
                        new Hashtable() {
                            { "Name", "Microsoft.PowerShell.SecretManagement"},
                            { "PassThru", true},
                            { "ErrorAction", "Ignore"}
                        }).Invoke<PSModuleInfo>();

                    if (pwsh.HadErrors || pwsh.Streams.Error.Count > 0)
                    {
                        foreach (var err in pwsh.Streams.Error)
                        {
                            cmdletPassedIn.WriteError(err);
                        }

                        return false;
                    }

                    if (module == null)
                    {
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                cmdletPassedIn.ThrowTerminatingError(
                    new ErrorRecord(
                        new PSInvalidOperationException(
                            message: $"Cannot validate Microsoft.PowerShell.SecretManagement module setup for PSResourceRepository '{repositoryName}' authentication.",
                            innerException: e),
                        "RepositoryCredentialSecretManagementInvalidModule",
                        ErrorCategory.InvalidOperation,
                        cmdletPassedIn));
            }

            return true;
        }

        public static bool IsSecretManagementVaultAccessible(
            string repositoryName,
            PSCredentialInfo repositoryCredentialInfo,
            PSCmdlet cmdletPassedIn)
        {
            try
            {
                using (System.Management.Automation.PowerShell pwsh = System.Management.Automation.PowerShell.Create())
                {
                    var module = pwsh.AddCommand("Microsoft.PowerShell.Core\\Import-Module").AddParameters(
                        new Hashtable() {
                            { "Name", "Microsoft.PowerShell.SecretManagement"},
                            { "PassThru", true}
                        }).Invoke<PSModuleInfo>();

                    if (pwsh.HadErrors || pwsh.Streams.Error.Count > 0)
                    {
                        foreach (var err in pwsh.Streams.Error)
                        {
                            cmdletPassedIn.WriteError(err);
                        }

                        return false;
                    }

                    if (module == null)
                    {
                        return false;
                    }

                    pwsh.Commands.Clear();
                    var results = pwsh.AddCommand("Microsoft.PowerShell.SecretManagement\\Test-SecretVault").AddParameters(
                        new Hashtable() {
                            { "Name", repositoryCredentialInfo.VaultName }
                        }).Invoke<bool>();

                    if (pwsh.HadErrors || pwsh.Streams.Error.Count > 0)
                    {
                        foreach (var err in pwsh.Streams.Error)
                        {
                            cmdletPassedIn.WriteError(err);
                        }

                        return false;
                    }

                    if (results == null)
                    {
                        return false;
                    }

                    return results.Count > 0 ? results[0] : false;
                }
            }
            catch (Exception e)
            {
                cmdletPassedIn.ThrowTerminatingError(
                    new ErrorRecord(
                        new PSInvalidOperationException(
                            message: $"Microsoft.PowerShell.SecretManagement\\Test-SecretVault encountered an error while validating the vault '{repositoryCredentialInfo.VaultName}' for PSResourceRepository '{repositoryName}' authentication.",
                            innerException: e),
                        "RepositoryCredentialSecretManagementInvalidVault",
                        ErrorCategory.InvalidOperation,
                        cmdletPassedIn));

                return false;
            }
        }

        public static NetworkCredential SetNetworkCredential(
            PSRepositoryInfo repository,
            NetworkCredential networkCredential,
            PSCmdlet cmdletPassedIn)
        {
            // Explicitly passed in Credential takes precedence over repository CredentialInfo.
            if (networkCredential == null && repository.CredentialInfo != null)
            {
                PSCredential repoCredential = Utils.GetRepositoryCredentialFromSecretManagement(
                    repository.Name,
                    repository.CredentialInfo,
                    cmdletPassedIn);

                networkCredential = new NetworkCredential(repoCredential.UserName, repoCredential.Password);

                cmdletPassedIn.WriteVerbose("credential successfully read from vault and set for repository: " + repository.Name);
            }

            return networkCredential;
        }

        #endregion

        #region Path methods

        public static string[] GetSubDirectories(string dirPath)
        {
            try
            {
                return Directory.GetDirectories(dirPath);
            }
            catch
            {
                return EmptyStrArray;
            }
        }

        public static string[] GetDirectoryFiles(string dirPath)
        {
            try
            {
                return Directory.GetFiles(dirPath);
            }
            catch
            {
                return EmptyStrArray;
            }
        }

        public static string GetInstalledPackageName(string pkgPath)
        {
            if (string.IsNullOrEmpty(pkgPath))
            {
                return string.Empty;
            }

            if (File.Exists(pkgPath))
            {
                // ex: ./PowerShell/Scripts/TestScript.ps1
                return Path.GetFileNameWithoutExtension(pkgPath);
            }

            // expecting the full version module path
            // ex:  ./PowerShell/Modules/TestModule/1.0.0
            return new DirectoryInfo(pkgPath).Parent.Name;
        }

        // Find all potential resource paths
        public static List<string> GetPathsFromEnvVarAndScope(
            PSCmdlet psCmdlet,
            ScopeType? scope)
        {
            GetStandardPlatformPaths(
               psCmdlet,
               out string myDocumentsPath,
               out string programFilesPath);

            List<string> resourcePaths = new List<string>();
            if (scope is null || scope.Value is ScopeType.CurrentUser)
            {
                resourcePaths.Add(Path.Combine(myDocumentsPath, "Modules"));
                resourcePaths.Add(Path.Combine(myDocumentsPath, "Scripts"));
            }

            if (scope.Value is ScopeType.AllUsers)
            {
                resourcePaths.Add(Path.Combine(programFilesPath, "Modules"));
                resourcePaths.Add(Path.Combine(programFilesPath, "Scripts"));
            }

            return resourcePaths;
        }

        public static List<string> GetAllResourcePaths(
            PSCmdlet psCmdlet,
            ScopeType? scope = null)
        {
            List<String> resourcePaths = GetPathsFromEnvVarAndScope(psCmdlet, scope);

            // resourcePaths should now contain, eg:
            // ./PowerShell/Scripts
            // ./PowerShell/Modules
            // add all module directories or script files
            List<string> pathsToSearch = new List<string>();
            foreach (string path in resourcePaths)
            {
                psCmdlet.WriteVerbose(string.Format("Retrieving directories in the path '{0}'", path));

                if (path.EndsWith("Scripts"))
                {
                    try
                    {
                        pathsToSearch.AddRange(GetDirectoryFiles(path));
                    }
                    catch (Exception e)
                    {
                        psCmdlet.WriteVerbose(string.Format("Error retrieving files from '{0}': '{1}'", path, e.Message));
                    }
                }
                else
                {
                    try
                    {
                        pathsToSearch.AddRange(GetSubDirectories(path));
                    }
                    catch (Exception e)
                    {
                        psCmdlet.WriteVerbose(string.Format("Error retrieving directories from '{0}': '{1}'", path, e.Message));
                    }
                }
            }

            // resourcePaths should now contain eg:
            // ./PowerShell/Scripts/Test-Script.ps1
            // ./PowerShell/Modules/TestModule
            // need to use .ToList() to cast the IEnumerable<string> to type List<string>
            pathsToSearch = pathsToSearch.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
            pathsToSearch.ForEach(dir => psCmdlet.WriteVerbose(string.Format("All paths to search: '{0}'", dir)));

            return pathsToSearch;
        }

        // Find all potential installation paths given a scope
        public static List<string> GetAllInstallationPaths(
            PSCmdlet psCmdlet,
            ScopeType? scope)
        {
            List<String> installationPaths = GetPathsFromEnvVarAndScope(psCmdlet, scope);

            installationPaths = installationPaths.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
            installationPaths.ForEach(dir => psCmdlet.WriteVerbose(string.Format("All paths to search: '{0}'", dir)));

            return installationPaths;
        }

        private static string GetHomeOrCreateTempHome()
        {
            const string tempHomeFolderName = "psresourceget-{0}-98288ff9-5712-4a14-9a11-23693b9cd91a";

            string envHome = Environment.GetEnvironmentVariable("HOME") ?? s_tempHome;
            if (envHome is not null)
            {
                return envHome;
            }

            try
            {
                s_tempHome = Path.Combine(Path.GetTempPath(), string.Format(CultureInfo.CurrentCulture, tempHomeFolderName, Environment.UserName));
                Directory.CreateDirectory(s_tempHome);
            }
            catch (UnauthorizedAccessException)
            {
                // Directory creation may fail if the account doesn't have filesystem permission such as some service accounts.
                // Return an empty string in this case so the process working directory will be used.
                s_tempHome = string.Empty;
            }

            return s_tempHome;
        }

        private readonly static Version PSVersion6 = new Version(6, 0);
        private static void GetStandardPlatformPaths(
            PSCmdlet psCmdlet,
            out string localUserDir,
            out string allUsersDir)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string powerShellType = (psCmdlet.Host.Version >= PSVersion6) ? "PowerShell" : "WindowsPowerShell";
                localUserDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), powerShellType);
                allUsersDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), powerShellType);
            }
            else
            {
                // paths are the same for both Linux and macOS
                localUserDir = Path.Combine(GetHomeOrCreateTempHome(), ".local", "share", "powershell");
                // Create the default data directory if it doesn't exist.
                if (!Directory.Exists(localUserDir))
                {
                    Directory.CreateDirectory(localUserDir);
                }

                allUsersDir = System.IO.Path.Combine("/usr", "local", "share", "powershell");
            }
        }

        public static bool GetIsWindowsPowerShell(PSCmdlet psCmdlet)
        {
            return psCmdlet.Host.Version < PSVersion6;
        }

        /// <summary>
        /// Checks if any of the package versions are already installed and if they are removes them from the list of packages to install.
        /// </summary>
        internal static HashSet<string> GetInstalledPackages(List<string> pathsToSearch, PSCmdlet cmdletPassedIn)
        {
            // Package install paths.
            // _pathsToInstallPkg will only contain the paths specified within the -Scope param (if applicable).
            // _pathsToSearch will contain all resource package subdirectories within _pathsToInstallPkg path locations.
            // e.g.:
            // ./InstallPackagePath1/PackageA
            // ./InstallPackagePath1/PackageB
            // ./InstallPackagePath2/PackageC
            // ./InstallPackagePath3/PackageD

            // Get currently installed packages.
            var getHelper = new GetHelper(cmdletPassedIn);
            var pkgsInstalledOnMachine = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

            foreach (PSResourceInfo installedPkg in getHelper.GetPackagesFromPath(
                name: new string[] { "*" },
                versionRange: VersionRange.All,
                pathsToSearch: pathsToSearch,
                selectPrereleaseOnly: false))
            {
                string pkgNameVersion = String.Format("{0}{1}", installedPkg.Name, installedPkg.Version.ToString());
                if (!pkgsInstalledOnMachine.Contains(pkgNameVersion))
                {
                    pkgsInstalledOnMachine.Add(pkgNameVersion);
                }
            }

            return pkgsInstalledOnMachine;
        }

        #endregion

        #region PSDataFile parsing

        private static readonly string[] ManifestFileVariables = new string[] { "PSEdition", "PSScriptRoot" };

        /// <summary>
        /// Read psd1 manifest file contents and return as Hashtable object.
        /// </summary>
        /// <param name="manifestFilePath">File path to manfiest psd1 file.</param>
        /// <param name="manifestInfo">Hashtable of manifest file contents.</param>
        /// <param name="error">Error exception on failure.</param>
        /// <returns>True on success.</returns>
        public static bool TryReadManifestFile(
            string manifestFilePath,
            out Hashtable manifestInfo,
            out Exception error)
        {
            return TryReadPSDataFile(
                filePath: manifestFilePath,
                allowedVariables: ManifestFileVariables,
                allowedCommands: Utils.EmptyStrArray,
                allowEnvironmentVariables: true,
                out manifestInfo,
                out error);
        }

        /// <summary>
        /// Read psd1 required resource file contents and return as Hashtable object.
        /// </summary>
        /// <param name="resourceFilePath">File path to required resource psd1 file.</param>
        /// <param name="resourceInfo">Hashtable of required resource file contents.</param>
        /// <param name="error">Error exception on failure.</param>
        /// <returns>True on success.</returns>
        public static bool TryReadRequiredResourceFile(
            string resourceFilePath,
            out Hashtable resourceInfo,
            out Exception error)
        {
            return TryReadPSDataFile(
                filePath: resourceFilePath,
                allowedVariables: Utils.EmptyStrArray,
                allowedCommands: Utils.EmptyStrArray,
                allowEnvironmentVariables: false,
                out resourceInfo,
                out error);
        }

        private static bool TryReadPSDataFile(
            string filePath,
            string[] allowedVariables,
            string[] allowedCommands,
            bool allowEnvironmentVariables,
            out Hashtable dataFileInfo,
            out Exception error)
        {
            try
            {
                if (filePath is null)
                {
                    throw new PSArgumentNullException(nameof(filePath));
                }

                string contents = System.IO.File.ReadAllText(filePath);
                var scriptBlock = System.Management.Automation.ScriptBlock.Create(contents);

                // Ensure that the content script block is safe to convert into a PSDataFile Hashtable.
                // This will throw for unsafe content.
                scriptBlock.CheckRestrictedLanguage(
                    allowedCommands: allowedCommands,
                    allowedVariables: allowedVariables,
                    allowEnvironmentVariables: allowEnvironmentVariables);

                // Convert contents into PSDataFile Hashtable by executing content as script.
                object result = scriptBlock.InvokeReturnAsIs();
                if (result is PSObject psObject)
                {
                    result = psObject.BaseObject;
                }

                dataFileInfo = (Hashtable)result;
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                dataFileInfo = null;
                error = ex;
                return false;
            }
        }

        public static bool ValidateModuleManifest(string moduleManifestPath, out string errorMsg)
        {
            errorMsg = string.Empty;
            using (System.Management.Automation.PowerShell pwsh = System.Management.Automation.PowerShell.Create())
            {
                // use PowerShell cmdlet Test-ModuleManifest
                // TODO: Test-ModuleManifest will throw an error if RequiredModules specifies a module that does not exist
                // locally on the machine. Consider adding a -Syntax param to Test-ModuleManifest so that it only checks that
                // the syntax is correct. In build/release pipelines for example, the modules listed under RequiredModules may
                // not be locally available, but we still want to allow the user to publish.
                Collection<PSObject> results = null;
                try
                {
                    results = pwsh.AddCommand("Test-ModuleManifest").AddParameter("Path", moduleManifestPath).Invoke();
                }
                catch (Exception e)
                {
                    if (e.Message.EndsWith("Change the value of the ModuleVersion key to match the version folder name."))
                    {
                        return true;
                    }
                    else
                    {
                        errorMsg = $"Error occured while running 'Test-ModuleManifest': {e.Message}";
                        return false;
                    }
                }

                if (pwsh.HadErrors)
                {
                    if (results.Any())
                    {
                        PSModuleInfo psModuleInfoObj = results[0].BaseObject as PSModuleInfo;
                        if (string.IsNullOrWhiteSpace(psModuleInfoObj.Author))
                        {
                            errorMsg = "No author was provided in the module manifest. The module manifest must specify a version, author and description. Run 'Test-ModuleManifest' to validate the file.";
                        }
                        else if (string.IsNullOrWhiteSpace(psModuleInfoObj.Description))
                        {
                            errorMsg = "No description was provided in the module manifest. The module manifest must specify a version, author and description. Run 'Test-ModuleManifest' to validate the file.";
                        }
                        else if (psModuleInfoObj.Version == null)
                        {
                            errorMsg = "No version or an incorrectly formatted version was provided in the module manifest. The module manifest must specify a version, author and description. Run 'Test-ModuleManifest' to validate the file.";
                        }
                    }

                    if (string.IsNullOrEmpty(errorMsg))
                    {
                        // Surface any inner error messages
                        var innerErrorMsg = (pwsh.Streams.Error.Count > 0) ? pwsh.Streams.Error[0].ToString() : string.Empty;
                        errorMsg = $"Module manifest file validation failed with error: {innerErrorMsg}. Run 'Test-ModuleManifest' to validate the module manifest.";
                    }

                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Misc methods

        public static void WriteVerboseOnCmdlet(
            PSCmdlet cmdlet,
            string message)
        {
            try
            {
                cmdlet.InvokeCommand.InvokeScript(
                    script: $"param ([string] $message) Write-Verbose -Verbose -Message $message",
                    useNewScope: true,
                    writeToPipeline: System.Management.Automation.Runspaces.PipelineResultTypes.None,
                    input: null,
                    args: new object[] { message });
            }
            catch { }
        }

        /// <summary>
        /// Convert a json string into a hashtable object.
        /// This uses custom script to perform the PSObject -> Hashtable
        /// conversion, so that this works with WindowsPowerShell.
        /// </summary>
        public static Hashtable ConvertJsonToHashtable(
            PSCmdlet cmdlet,
            string json)
        {
            Collection<PSObject> results = cmdlet.InvokeCommand.InvokeScript(
                script: ConvertJsonToHashtableScript,
                useNewScope: true,
                writeToPipeline: PipelineResultTypes.Error,
                input: null,
                args: new object[] { json });

            return (results.Count == 1 && results[0] != null) ? (Hashtable)results[0].BaseObject : null;
        }

        public static bool TryCreateModuleSpecification(
            Hashtable[] moduleSpecHashtables,
            out ModuleSpecification[] validatedModuleSpecs,
            out ErrorRecord[] errors)
        {
            bool moduleSpecCreatedSuccessfully = true;
            List<ErrorRecord> errorList = new List<ErrorRecord>();
            validatedModuleSpecs = Array.Empty<ModuleSpecification>();
            List<ModuleSpecification> moduleSpecsList = new List<ModuleSpecification>();

            foreach (Hashtable moduleSpec in moduleSpecHashtables)
            {
                // ModuleSpecification(string) constructor for creating a ModuleSpecification when only ModuleName is provided.
                if (!moduleSpec.ContainsKey("ModuleName") || String.IsNullOrEmpty((string)moduleSpec["ModuleName"]))
                {
                    errorList.Add(new ErrorRecord(
                        new ArgumentException($"RequiredModules Hashtable entry {moduleSpec.ToString()} is missing a key 'ModuleName' and associated value, which is required for each module specification entry"),
                        "NameMissingInModuleSpecification",
                        ErrorCategory.InvalidArgument,
                        null));
                    moduleSpecCreatedSuccessfully = false;
                    continue;
                }

                // At this point it must contain ModuleName key.
                string moduleSpecName = (string)moduleSpec["ModuleName"];
                ModuleSpecification currentModuleSpec = null;
                if (!moduleSpec.ContainsKey("MaximumVersion") && !moduleSpec.ContainsKey("ModuleVersion") && !moduleSpec.ContainsKey("RequiredVersion"))
                {
                    // Pass to ModuleSpecification(string) constructor.
                    // This constructor method would only throw for a null/empty string, which we've already validated against above.
                    currentModuleSpec = new ModuleSpecification(moduleSpecName);

                    if (currentModuleSpec != null)
                    {
                        moduleSpecsList.Add(currentModuleSpec);
                    }
                    else
                    {
                        errorList.Add(new ErrorRecord(
                            new ArgumentException($"ModuleSpecification object was not able to be created for {moduleSpecName}"),
                            "ModuleSpecificationNotCreated",
                            ErrorCategory.InvalidArgument,
                            null));
                        moduleSpecCreatedSuccessfully = false;
                        continue;
                    }
                }
                else
                {
                    // ModuleSpecification(Hashtable) constructor for when ModuleName + {Required,Maximum,Module}Version value is also provided.
                    string moduleSpecMaxVersion = moduleSpec.ContainsKey("MaximumVersion") ? (string)moduleSpec["MaximumVersion"] : String.Empty;
                    string moduleSpecModuleVersion = moduleSpec.ContainsKey("ModuleVersion") ? (string)moduleSpec["ModuleVersion"] : String.Empty;
                    string moduleSpecRequiredVersion = moduleSpec.ContainsKey("RequiredVersion") ? (string)moduleSpec["RequiredVersion"] : String.Empty;
                    Guid moduleSpecGuid = moduleSpec.ContainsKey("Guid") ? (Guid)moduleSpec["Guid"] : Guid.Empty;

                    if (String.IsNullOrEmpty(moduleSpecMaxVersion) && String.IsNullOrEmpty(moduleSpecModuleVersion) && String.IsNullOrEmpty(moduleSpecRequiredVersion))
                    {
                        errorList.Add(new ErrorRecord(
                            new ArgumentException($"ModuleSpecification hashtable requires one of the following keys: MaximumVersion, ModuleVersion, RequiredVersion and failed to be created for {moduleSpecName}"),
                            "MissingModuleSpecificationMember",
                            ErrorCategory.InvalidArgument,
                            null));
                        moduleSpecCreatedSuccessfully = false;
                        continue;
                    }

                    Hashtable moduleSpecHash = new Hashtable();

                    moduleSpecHash.Add("ModuleName", moduleSpecName);
                    if (moduleSpecGuid != Guid.Empty)
                    {
                        moduleSpecHash.Add("Guid", moduleSpecGuid);
                    }

                    if (!String.IsNullOrEmpty(moduleSpecMaxVersion))
                    {
                        moduleSpecHash.Add("MaximumVersion", moduleSpecMaxVersion);
                    }

                    if (!String.IsNullOrEmpty(moduleSpecModuleVersion))
                    {
                        moduleSpecHash.Add("ModuleVersion", moduleSpecModuleVersion);
                    }

                    if (!String.IsNullOrEmpty(moduleSpecRequiredVersion))
                    {
                        moduleSpecHash.Add("RequiredVersion", moduleSpecRequiredVersion);
                    }

                    try
                    {
                        currentModuleSpec = new ModuleSpecification(moduleSpecHash);
                    }
                    catch (Exception e)
                    {
                        errorList.Add(new ErrorRecord(
                            new ArgumentException($"ModuleSpecification instance was not able to be created with hashtable constructor due to: {e.Message}"),
                            "ModuleSpecificationNotCreated",
                            ErrorCategory.InvalidArgument,
                            null));
                        moduleSpecCreatedSuccessfully = false;
                    }

                    if (currentModuleSpec != null)
                    {
                        moduleSpecsList.Add(currentModuleSpec);
                    }
                }
            }

            errors = errorList.ToArray();
            validatedModuleSpecs = moduleSpecsList.ToArray();
            return moduleSpecCreatedSuccessfully;
        }

        #endregion

        #region Directory and File

        /// <Summary>
        /// Deletes a directory and its contents.
        /// Attempts to restore the directory and contents if deletion fails.
        /// </Summary>
        public static void DeleteDirectoryWithRestore(string dirPath)
        {
            string tempDirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                // Create temporary directory for restore operation if needed.
                CopyDirContents(dirPath, tempDirPath, overwrite: true);

                try
                {
                    DeleteDirectory(dirPath);
                }
                catch (Exception ex)
                {
                    // Delete failed. Attempt to restore the saved directory content.
                    try
                    {
                        RestoreDirContents(tempDirPath, dirPath);
                    }
                    catch (Exception exx)
                    {
                        throw new PSInvalidOperationException(
                            $"Cannot remove package path {dirPath}. An attempt to restore the old package has failed with error: {exx.Message}",
                            ex);
                    }

                    throw new PSInvalidOperationException(
                        $"Cannot remove package path {dirPath}. The previous package contents have been restored.",
                        ex);
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                if (Directory.Exists(tempDirPath))
                {
                    DeleteDirectory(tempDirPath);
                }
            }
        }

        /// <Summary>
        /// Deletes a directory and its contents
        /// This is a workaround for .NET Directory.Delete(), which can fail with WindowsPowerShell
        /// on OneDrive with 'access denied' error.
        /// Later versions of .NET, with PowerShellCore, do not have this bug.
        /// </Summary>
        public static void DeleteDirectory(string dirPath)
        {
            // Remove read only file attributes first
            foreach (var dirFilePath in Directory.GetFiles(dirPath,"*",SearchOption.AllDirectories))
            {
                if (File.GetAttributes(dirFilePath).HasFlag(FileAttributes.ReadOnly))
                {
                    File.SetAttributes(dirFilePath, File.GetAttributes(dirFilePath) & ~FileAttributes.ReadOnly);
                }
            }
            // Delete directory recursive, try multiple times before throwing ( #1662 )
            int maxAttempts = 5;
            int msDelay = 5;
            for (int attempt = 1; attempt <= maxAttempts; ++attempt)
            {
                try
                {
                    Directory.Delete(dirPath,true);
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt < maxAttempts && (ex is IOException || ex is UnauthorizedAccessException))
                    {
                        Thread.Sleep(msDelay);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        /// <Summary>
        /// Moves files from source to destination locations.
        /// This is a workaround for .NET File.Move(), which fails over different file volumes.
        /// </Summary>
        public static void MoveFiles(
            string sourceFilePath,
            string destFilePath,
            bool overwrite = true)
        {
            File.Copy(sourceFilePath, destFilePath, overwrite);
            File.Delete(sourceFilePath);
        }

        /// <Summary>
        /// Moves the directory, including contents, from source to destination locations.
        /// This is a workaround for .NET Directory.Move(), which fails over different file volumes.
        /// </Summary>
        public static void MoveDirectory(
            string sourceDirPath,
            string destDirPath,
            bool overwrite = true)
        {
            CopyDirContents(sourceDirPath, destDirPath, overwrite);
            DeleteDirectory(sourceDirPath);
        }

        private static void CopyDirContents(
            string sourceDirPath,
            string destDirPath,
            bool overwrite)
        {
            if (Directory.Exists(destDirPath))
            {
                if (!overwrite)
                {
                    throw new PSInvalidOperationException(
                        $"Cannot move directory because destination directory already exists: '{destDirPath}'");
                }

                DeleteDirectory(destDirPath);
            }

            Directory.CreateDirectory(destDirPath);

            foreach (var filePath in Directory.GetFiles(sourceDirPath))
            {
                var destFilePath = Path.Combine(destDirPath, Path.GetFileName(filePath));
                File.Copy(filePath, destFilePath);
            }

            foreach (var srcSubDirPath in Directory.GetDirectories(sourceDirPath))
            {
                var destSubDirPath = Path.Combine(destDirPath, Path.GetFileName(srcSubDirPath));
                CopyDirContents(srcSubDirPath, destSubDirPath, overwrite);
            }
        }

        public static void DeleteExtraneousFiles(PSCmdlet callingCmdlet, string pkgName, string dirNameVersion)
        {
            // Deleting .nupkg SHA file, .nuspec, and .nupkg after unpacking the module
            var nuspecToDelete = Path.Combine(dirNameVersion, pkgName + ".nuspec");
            var contentTypesToDelete = Path.Combine(dirNameVersion, "[Content_Types].xml");
            var relsDirToDelete = Path.Combine(dirNameVersion, "_rels");
            var packageDirToDelete = Path.Combine(dirNameVersion, "package");

            // Unforunately have to check if each file exists because it may or may not be there
            if (File.Exists(nuspecToDelete))
            {
                callingCmdlet.WriteVerbose(string.Format("Deleting '{0}'", nuspecToDelete));
                File.Delete(nuspecToDelete);
            }
            if (File.Exists(contentTypesToDelete))
            {
                callingCmdlet.WriteVerbose(string.Format("Deleting '{0}'", contentTypesToDelete));
                File.Delete(contentTypesToDelete);
            }
            if (Directory.Exists(relsDirToDelete))
            {
                callingCmdlet.WriteVerbose(string.Format("Deleting '{0}'", relsDirToDelete));
                Utils.DeleteDirectory(relsDirToDelete);
            }
            if (Directory.Exists(packageDirToDelete))
            {
                callingCmdlet.WriteVerbose(string.Format("Deleting '{0}'", packageDirToDelete));
                Utils.DeleteDirectory(packageDirToDelete);
            }
        }

        public static void MoveFilesIntoInstallPath(
            PSResourceInfo pkgInfo,
            bool isModule,
            bool isLocalRepo,
            bool savePkg,
            string dirNameVersion,
            string tempInstallPath,
            string installPath,
            string newVersion,
            string moduleManifestVersion,
            string scriptPath,
            PSCmdlet cmdletPassedIn)
        {
            // Creating the proper installation path depending on whether pkg is a module or script
            var newPathParent = isModule ? Path.Combine(installPath, pkgInfo.Name) : installPath;
            var finalModuleVersionDir = isModule ? Path.Combine(installPath, pkgInfo.Name, moduleManifestVersion) : installPath;

            // If script, just move the files over, if module, move the version directory over
            var tempModuleVersionDir = (!isModule || isLocalRepo) ? dirNameVersion
                : Path.Combine(tempInstallPath, pkgInfo.Name.ToLower(), newVersion);

            cmdletPassedIn.WriteVerbose(string.Format("Installation source path is: '{0}'", tempModuleVersionDir));
            cmdletPassedIn.WriteVerbose(string.Format("Installation destination path is: '{0}'", finalModuleVersionDir));

            if (isModule)
            {
                // If new path does not exist
                if (!Directory.Exists(newPathParent))
                {
                    cmdletPassedIn.WriteVerbose(string.Format("Attempting to move '{0}' to '{1}'", tempModuleVersionDir, finalModuleVersionDir));
                    Directory.CreateDirectory(newPathParent);
                    Utils.MoveDirectory(tempModuleVersionDir, finalModuleVersionDir);
                }
                else
                {
                    cmdletPassedIn.WriteVerbose(string.Format("Temporary module version directory is: '{0}'", tempModuleVersionDir));

                    if (Directory.Exists(finalModuleVersionDir))
                    {
                        // Delete the directory path before replacing it with the new module.
                        // If deletion fails (usually due to binary file in use), then attempt restore so that the currently
                        // installed module is not corrupted.
                        cmdletPassedIn.WriteVerbose(string.Format("Attempting to delete with restore on failure.'{0}'", finalModuleVersionDir));
                        Utils.DeleteDirectoryWithRestore(finalModuleVersionDir);
                    }

                    cmdletPassedIn.WriteVerbose(string.Format("Attempting to move '{0}' to '{1}'", tempModuleVersionDir, finalModuleVersionDir));
                    Utils.MoveDirectory(tempModuleVersionDir, finalModuleVersionDir);
                }
            }
            else
            {
                if (!savePkg)
                {
                    // Need to delete old xml files because there can only be 1 per script
                    var scriptXML = pkgInfo.Name + "_InstalledScriptInfo.xml";
                    cmdletPassedIn.WriteVerbose(string.Format("Checking if path '{0}' exists: ", File.Exists(Path.Combine(installPath, "InstalledScriptInfos", scriptXML))));
                    if (File.Exists(Path.Combine(installPath, "InstalledScriptInfos", scriptXML)))
                    {
                        cmdletPassedIn.WriteVerbose(string.Format("Deleting script metadata XML"));
                        File.Delete(Path.Combine(installPath, "InstalledScriptInfos", scriptXML));
                    }

                    cmdletPassedIn.WriteVerbose(string.Format("Moving '{0}' to '{1}'", Path.Combine(dirNameVersion, scriptXML), Path.Combine(installPath, "InstalledScriptInfos", scriptXML)));
                    Utils.MoveFiles(Path.Combine(dirNameVersion, scriptXML), Path.Combine(installPath, "InstalledScriptInfos", scriptXML));

                    // Need to delete old script file, if that exists
                    cmdletPassedIn.WriteVerbose(string.Format("Checking if path '{0}' exists: ", File.Exists(Path.Combine(finalModuleVersionDir, pkgInfo.Name + PSScriptFileExt))));
                    if (File.Exists(Path.Combine(finalModuleVersionDir, pkgInfo.Name + PSScriptFileExt)))
                    {
                        cmdletPassedIn.WriteVerbose(string.Format("Deleting script file"));
                        File.Delete(Path.Combine(finalModuleVersionDir, pkgInfo.Name + PSScriptFileExt));
                    }
                }

                cmdletPassedIn.WriteVerbose(string.Format("Moving '{0}' to '{1}'", scriptPath, Path.Combine(finalModuleVersionDir, pkgInfo.Name + PSScriptFileExt)));
                Utils.MoveFiles(scriptPath, Path.Combine(finalModuleVersionDir, pkgInfo.Name + PSScriptFileExt));
            }
        }

        private static void RestoreDirContents(
            string sourceDirPath,
            string destDirPath)
        {
            if (!Directory.Exists(destDirPath))
            {
                Directory.CreateDirectory(destDirPath);
            }

            foreach (string filePath in Directory.GetFiles(sourceDirPath))
            {
                string destFilePath = Path.Combine(destDirPath, Path.GetFileName(filePath));
                if (!File.Exists(destFilePath))
                {
                    File.Copy(filePath, destFilePath);
                }
            }

            foreach (string srcSubDirPath in Directory.GetDirectories(sourceDirPath))
            {
                string destSubDirPath = Path.Combine(destDirPath, Path.GetFileName(srcSubDirPath));
                RestoreDirContents(srcSubDirPath, destSubDirPath);
            }
        }

        public static void CreateFile(string filePath)
        {
            FileStream fileStream = null;
            try
            {
                fileStream = File.Create(filePath);
            }
            catch (Exception e)
            {
                throw new Exception($"Error creating file '{filePath}': {e.Message}");
            }
            finally
            {
                if (fileStream != null)
                {
                    fileStream.Close();
                }
            }
        }

        #endregion

    }

    #endregion

    #region PowerShellInvoker

    internal static class PowerShellInvoker
    {
        #region Members

        private static bool _isHostDefault = false;
        private const string DefaultHost = "Default Host";

        private static Runspace _runspace;

        #endregion Members

        #region Methods

        public static Collection<PSObject> InvokeScriptWithHost(
            PSCmdlet cmdlet,
            string script,
            object[] args,
            out Exception terminatingError)
        {
            return InvokeScriptWithHost<PSObject>(
                cmdlet,
                script,
                args,
                out terminatingError);
        }

        public static Collection<T> InvokeScriptWithHost<T>(
            PSCmdlet cmdlet,
            string script,
            object[] args,
            out Exception terminatingError)
        {
            Collection<T> returnCollection = new Collection<T>();
            terminatingError = null;

            // Create the runspace if it
            //   doesn't exist
            //   is not in a workable state
            //   has a default host (no UI) when a non-default host is available
            if (_runspace == null ||
                _runspace.RunspaceStateInfo.State != RunspaceState.Opened ||
                _isHostDefault && !cmdlet.Host.Name.Equals(DefaultHost, StringComparison.InvariantCultureIgnoreCase))
            {
                if (_runspace != null)
                {
                    _runspace.Dispose();
                }

                _isHostDefault = cmdlet.Host.Name.Equals(DefaultHost, StringComparison.InvariantCultureIgnoreCase);

                var iss = InitialSessionState.CreateDefault2();
                // We are running trusted script.
                iss.LanguageMode = PSLanguageMode.FullLanguage;
                // Import the current PowerShellGet module.
                var modPathObjects = cmdlet.InvokeCommand.InvokeScript(
                    script: "(Microsoft.PowerShell.Core\\Get-Module -Name Microsoft.PowerShell.PSResourceGet).Path");
                string modPath = (modPathObjects.Count > 0 &&
                                  modPathObjects[0].BaseObject is string modPathStr)
                                  ? modPathStr : string.Empty;
                if (!string.IsNullOrEmpty(modPath))
                {
                    iss.ImportPSModule(new string[] { modPath });
                }

                try
                {
                    _runspace = RunspaceFactory.CreateRunspace(cmdlet.Host, iss);
                    _runspace.Open();
                }
                catch (Exception ex)
                {
                    terminatingError = ex;
                    return returnCollection;
                }
            }

            using (var ps = System.Management.Automation.PowerShell.Create())
            {
                ps.Runspace = _runspace;

                var cmd = new Command(
                    command: script,
                    isScript: true,
                    useLocalScope: true);
                cmd.MergeMyResults(
                    myResult: PipelineResultTypes.Error | PipelineResultTypes.Warning | PipelineResultTypes.Verbose | PipelineResultTypes.Debug | PipelineResultTypes.Information,
                    toResult: PipelineResultTypes.Output);
                ps.Commands.AddCommand(cmd);
                foreach (var arg in args)
                {
                    ps.Commands.AddArgument(arg);
                }

                try
                {
                    // Invoke the script.
                    var results = ps.Invoke();

                    // Extract expected output types from results pipeline.
                    foreach (var psItem in results)
                    {
                        if (psItem == null || psItem.BaseObject == null) { continue; }

                        switch (psItem.BaseObject)
                        {
                            case ErrorRecord error:
                                cmdlet.WriteError(error);
                                break;

                            case WarningRecord warning:
                                cmdlet.WriteWarning(warning.Message);
                                break;

                            case VerboseRecord verbose:
                                cmdlet.WriteVerbose(verbose.Message);
                                break;

                            case DebugRecord debug:
                                cmdlet.WriteDebug(debug.Message);
                                break;

                            case InformationRecord info:
                                cmdlet.WriteInformation(info);
                                break;

                            case T result:
                                returnCollection.Add(result);
                                break;

                            case T[] resultArray:
                                foreach (var item in resultArray)
                                {
                                    returnCollection.Add(item);
                                }
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    terminatingError = ex;
                }
            }

            return returnCollection;
        }

        #endregion Methods
    }

    #endregion

    #region AuthenticodeSignature

    internal static class AuthenticodeSignature
    {
        #region Methods

        internal static bool CheckAuthenticodeSignature(
            string pkgName,
            string tempDirNameVersion,
            PSCmdlet cmdletPassedIn,
            out ErrorRecord errorRecord)
        {
            errorRecord = null;

            // Because authenticode and catalog verifications are only applicable on Windows, we allow all packages by default to be installed on unix systems.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return true;
            }

            // Otherwise check for signatures on individual files.
            Collection<PSObject> authenticodeSignatures;
            try
            {
                string[] listOfExtensions = { "*.ps1", "*.psd1", "*.psm1", "*.mof", "*.cat", "*.ps1xml" };
                authenticodeSignatures = cmdletPassedIn.InvokeCommand.InvokeScript(
                    script: @"param (
                                      [string] $tempDirNameVersion,
                                      [string[]] $listOfExtensions
                                 )
                                 Get-ChildItem $tempDirNameVersion -Recurse -Include $listOfExtensions | Get-AuthenticodeSignature -ErrorAction SilentlyContinue",
                    useNewScope: true,
                    writeToPipeline: System.Management.Automation.Runspaces.PipelineResultTypes.None,
                    input: null,
                    args: new object[] { tempDirNameVersion, listOfExtensions });
            }
            catch (Exception e)
            {
                errorRecord = new ErrorRecord(
                    new ArgumentException(e.Message),
                    "GetAuthenticodeSignatureError",
                    ErrorCategory.InvalidResult,
                    cmdletPassedIn);

                return false;
            }

            // If any file authenticode signatures are not valid, return false.
            foreach (var signatureObject in authenticodeSignatures)
            {
                Signature signature = (Signature)signatureObject.BaseObject;
                if (!signature.Status.Equals(SignatureStatus.Valid))
                {
                    errorRecord = new ErrorRecord(
                        new ArgumentException($"The signature status for '{pkgName}' file '{Path.GetFileName(signature.Path)}' is '{signature.Status}'. Status message: '{signature.StatusMessage}'"),
                        "GetAuthenticodeSignatureError",
                        ErrorCategory.InvalidResult,
                        cmdletPassedIn);

                    return false;
                }
            }

            return true;
        }

        #endregion
    }

    #endregion
}
