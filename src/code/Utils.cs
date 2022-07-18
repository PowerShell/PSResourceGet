// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Win32.SafeHandles;
using NuGet.Versioning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.PowerShell.Commands;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    #region Utils

    internal static class Utils
    {
        #region String fields

        public static readonly string[] EmptyStrArray = Array.Empty<string>();
        public const string PSDataFileExt = ".psd1";
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

        public static string[] GetStringArrayFromString(char[] delimeter, string stringToConvertToArray)
        {
            // this will be a string where entries are separated by space
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
            for (int i=0; i < list.Count; i++)
            {
                strArray[i] = list[i] as string;
            }

            return strArray;
        }

        public static string[] ProcessNameWildcards(
            string[] pkgNames,
            out string[] errorMsgs,
            out bool isContainWildcard)
        {
            List<string> namesWithSupportedWildcards = new List<string>();
            List<string> errorMsgsList = new List<string>();

            if (pkgNames == null)
            {
                isContainWildcard = true;
                errorMsgs = errorMsgsList.ToArray();
                return new string[] {"*"};
            }

            isContainWildcard = false;
            foreach (string name in pkgNames)
            {
                if (WildcardPattern.ContainsWildcardCharacters(name))
                {
                    if (String.Equals(name, "*", StringComparison.InvariantCultureIgnoreCase))
                    {
                        isContainWildcard = true;
                        errorMsgs = new string[] {};
                        return new string[] {"*"};
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

        #endregion

        #region Version methods

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

            // parse as Version range
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

            string version = psGetInfo.Version.ToString();
            string prerelease = psGetInfo.Prerelease;

            if (!NuGetVersion.TryParse(
                    value: String.IsNullOrEmpty(prerelease) ? version : GetNormalizedVersionString(version, prerelease),
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
                var filePath = cmdletPassedIn.SessionState.Path.GetResolvedPSPathFromPSPath(uriString)[0].Path;
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
                new PSArgumentException(
                    $"The provided Uri is not valid: {uriString}. It must be of Uri Scheme: HTTP, HTTPS, FTP or a file path",
                    ex),
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
                if (!string.IsNullOrEmpty((string) credentialInfoCandidate.Properties[PSCredentialInfo.VaultNameAttribute]?.Value)
                    && !string.IsNullOrEmpty((string) credentialInfoCandidate.Properties[PSCredentialInfo.SecretNameAttribute]?.Value))
                {
                    PSCredential credential = null;
                    if (credentialInfoCandidate.Properties[PSCredentialInfo.CredentialAttribute] != null)
                    {
                        try
                        {
                            credential = (PSCredential) credentialInfoCandidate.Properties[PSCredentialInfo.CredentialAttribute].Value;
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
                        (string) credentialInfoCandidate.Properties[PSCredentialInfo.VaultNameAttribute].Value,
                        (string) credentialInfoCandidate.Properties[PSCredentialInfo.SecretNameAttribute].Value,
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
                        "RepositoryCredentialCannotGetSecretFromVault",
                        ErrorCategory.InvalidOperation,
                        cmdletPassedIn));
            }

            if (secretValue is PSCredential secretCredential)
            {
                return secretCredential;
            }

            cmdletPassedIn.ThrowTerminatingError(
                new ErrorRecord(
                    new PSNotSupportedException($"Secret \"{repositoryCredentialInfo.SecretName}\" from vault \"{repositoryCredentialInfo.VaultName}\" has an invalid type. The only supported type is PSCredential."),
                    "RepositoryCredentialInvalidSecretType",
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

            PowerShellInvoker.InvokeScriptWithHost(
                cmdlet: cmdletPassedIn,
                script: @"
                    param (
                        [string] $VaultName,
                        [string] $SecretName,
                        [object] $SecretValue
                    )
                    $module = Microsoft.PowerShell.Core\Import-Module -Name Microsoft.PowerShell.SecretManagement -PassThru
                    if ($null -eq $module) {
                        return
                    }
                    & $module ""Set-Secret"" -Name $SecretName -Vault $VaultName -Secret $SecretValue
                ",
                args: new object[] { repositoryCredentialInfo.VaultName, repositoryCredentialInfo.SecretName, repositoryCredentialInfo.Credential },
                out Exception terminatingError);

            if (terminatingError != null)
            {
                cmdletPassedIn.ThrowTerminatingError(
                    new ErrorRecord(
                        new PSInvalidOperationException(
                            message: $"Microsoft.PowerShell.SecretManagement\\Set-Secret encountered an error while adding secret \"{repositoryCredentialInfo.SecretName}\" to vault \"{repositoryCredentialInfo.VaultName}\" for PSResourceRepository ({repositoryName}) authentication.",
                            innerException: terminatingError),
                        "RepositoryCredentialCannotAddSecretToVault",
                        ErrorCategory.InvalidOperation,
                        cmdletPassedIn));
            }
        }

        public static bool IsSecretManagementModuleAvailable(
            string repositoryName,
            PSCmdlet cmdletPassedIn)
        {
            var results = PowerShellInvoker.InvokeScriptWithHost<int>(
                cmdlet: cmdletPassedIn,
                script: @"
                    $module = Microsoft.PowerShell.Core\Get-Module -Name Microsoft.PowerShell.SecretManagement -ErrorAction Ignore
                    if ($null -eq $module) {
                        $module = Microsoft.PowerShell.Core\Import-Module -Name Microsoft.PowerShell.SecretManagement -PassThru -ErrorAction Ignore
                    }
                    if ($null -eq $module) {
                        return 1
                    }
                    return 0
                ",
                args: new object[] {},
                out Exception terminatingError);

            if (terminatingError != null)
            {
                cmdletPassedIn.ThrowTerminatingError(
                    new ErrorRecord(
                        new PSInvalidOperationException(
                            message: $"Cannot validate Microsoft.PowerShell.SecretManagement module setup for PSResourceRepository ({repositoryName}) authentication.",
                            innerException: terminatingError),
                        "RepositoryCredentialSecretManagementInvalidModule",
                        ErrorCategory.InvalidOperation,
                        cmdletPassedIn));
            }

            int result = (results.Count > 0) ? results[0] : 1;
            return result == 0;
        }

        public static bool IsSecretManagementVaultAccessible(
            string repositoryName,
            PSCredentialInfo repositoryCredentialInfo,
            PSCmdlet cmdletPassedIn)
        {
            var results = PowerShellInvoker.InvokeScriptWithHost<bool>(
                cmdlet: cmdletPassedIn,
                script: @"
                    param (
                        [string] $VaultName
                    )
                    $module = Microsoft.PowerShell.Core\Import-Module -Name Microsoft.PowerShell.SecretManagement -PassThru
                    if ($null -eq $module) {
                        return
                    }
                    & $module ""Test-SecretVault"" -Name $VaultName
                ",
                args: new object[] { repositoryCredentialInfo.VaultName },
                out Exception terminatingError);

            if (terminatingError != null)
            {
                cmdletPassedIn.ThrowTerminatingError(
                    new ErrorRecord(
                        new PSInvalidOperationException(
                            message: $"Microsoft.PowerShell.SecretManagement\\Test-SecretVault encountered an error while validating the vault \"{repositoryCredentialInfo.VaultName}\" for PSResourceRepository ({repositoryName}) authentication.",
                            innerException: terminatingError),
                        "RepositoryCredentialSecretManagementInvalidVault",
                        ErrorCategory.InvalidOperation,
                        cmdletPassedIn));
            }

            bool result = (results.Count > 0) ? results[0] : false;
            return result;
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

        public static List<string> GetAllResourcePaths(
            PSCmdlet psCmdlet,
            ScopeType? scope = null)
        {
            GetStandardPlatformPaths(
                psCmdlet,
                out string myDocumentsPath,
                out string programFilesPath);

            List<string> resourcePaths = new List<string>();

            // Path search order is PSModulePath paths first, then default paths.
            if (scope is null)
            {
                string psModulePath = Environment.GetEnvironmentVariable("PSModulePath");
                resourcePaths.AddRange(psModulePath.Split(Path.PathSeparator).ToList());
            }

            if (scope is null || scope.Value is ScopeType.CurrentUser)
            {
                resourcePaths.Add(Path.Combine(myDocumentsPath, "Modules"));
                resourcePaths.Add(Path.Combine(myDocumentsPath, "Scripts"));
            }

            if (scope is null || scope.Value is ScopeType.AllUsers)
            {
                resourcePaths.Add(Path.Combine(programFilesPath, "Modules"));
                resourcePaths.Add(Path.Combine(programFilesPath, "Scripts"));
            }

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
            ScopeType scope)
        {
            GetStandardPlatformPaths(
                psCmdlet,
                out string myDocumentsPath,
                out string programFilesPath);

            // The default user scope is CurrentUser
            var installationPaths = new List<string>();
            if (scope == ScopeType.AllUsers)
            {
                installationPaths.Add(Path.Combine(programFilesPath, "Modules"));
                installationPaths.Add(Path.Combine(programFilesPath, "Scripts"));
            }
            else
            {
                installationPaths.Add(Path.Combine(myDocumentsPath, "Modules"));
                installationPaths.Add(Path.Combine(myDocumentsPath, "Scripts"));
            }

            installationPaths = installationPaths.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
            installationPaths.ForEach(dir => psCmdlet.WriteVerbose(string.Format("All paths to search: '{0}'", dir)));

            return installationPaths;
        }

        private readonly static Version PSVersion6 = new Version(6, 0);
        private static void GetStandardPlatformPaths(
            PSCmdlet psCmdlet,
            out string myDocumentsPath,
            out string programFilesPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string powerShellType = (psCmdlet.Host.Version >= PSVersion6) ? "PowerShell" : "WindowsPowerShell";
                myDocumentsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), powerShellType);
                programFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), powerShellType);
            }
            else
            {
                // paths are the same for both Linux and macOS
                myDocumentsPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "powershell");
                programFilesPath = System.IO.Path.Combine("/usr", "local", "share", "powershell");
            }
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
                allowEnvironmentVariables: false,
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

                dataFileInfo = (Hashtable) result;
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

        public static void ValidateModuleManifest(string moduleManifestPath, out string[] errorMsgs)
        {
            List<string> errorMsgsList = new List<string>();
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
                    errorMsgsList.Add($"Error occured while running 'Test-ModuleManifest': {e.Message}");

                    errorMsgs = errorMsgsList.ToArray();
                    return; 
                }

                if (pwsh.HadErrors)
                {
                    var message = string.Empty;

                    if (results.Any())
                    {
                        PSModuleInfo psModuleInfoObj = results[0].BaseObject as PSModuleInfo;
                        if (string.IsNullOrWhiteSpace(psModuleInfoObj.Author))
                        {
                            message = "No author was provided in the module manifest. The module manifest must specify a version, author and description. Run 'Test-ModuleManifest' to validate the file.";
                        }
                        else if (string.IsNullOrWhiteSpace(psModuleInfoObj.Description))
                        {
                            message = "No description was provided in the module manifest. The module manifest must specify a version, author and description. Run 'Test-ModuleManifest' to validate the file.";
                        }
                        else if (psModuleInfoObj.Version == null)
                        {
                            message = "No version or an incorrectly formatted version was provided in the module manifest. The module manifest must specify a version, author and description. Run 'Test-ModuleManifest' to validate the file.";
                        }
                    }

                    if (string.IsNullOrEmpty(message) && pwsh.Streams.Error.Count > 0)
                    {
                        // This will handle version errors
                        message = $"{pwsh.Streams.Error[0].ToString()} Run 'Test-ModuleManifest' to validate the module manifest.";
                    }

                    errorMsgsList.Add(message);
                }
            }
            errorMsgs = errorMsgsList.ToArray();

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
            validatedModuleSpecs = new ModuleSpecification[]{};
            List<ModuleSpecification> moduleSpecsList = new List<ModuleSpecification>();

            foreach(Hashtable moduleSpec in moduleSpecHashtables)
            {
                // ModuleSpecification(string) constructor for creating a ModuleSpecification when only ModuleName is provided.
                if (!moduleSpec.ContainsKey("ModuleName") || String.IsNullOrEmpty((string) moduleSpec["ModuleName"]))
                {
                    var exMessage = $"RequiredModules Hashtable entry {moduleSpec.ToString()} is missing a key 'ModuleName' and associated value, which is required for each module specification entry";
                    var ex = new ArgumentException(exMessage);
                    var NameMissingModuleSpecError = new ErrorRecord(ex, "NameMissingInModuleSpecification", ErrorCategory.InvalidArgument, null);
                    errorList.Add(NameMissingModuleSpecError);
                    moduleSpecCreatedSuccessfully = false;
                    continue;
                }

                // at this point it must contain ModuleName key.
                string moduleSpecName = (string) moduleSpec["ModuleName"];
                ModuleSpecification currentModuleSpec = null;
                if (!moduleSpec.ContainsKey("MaximumVersion") && !moduleSpec.ContainsKey("ModuleVersion") && !moduleSpec.ContainsKey("RequiredVersion"))
                {
                    // pass to ModuleSpecification(string) constructor
                    // This constructor method would only throw for a null/empty string, which we've already validated against above.
                    currentModuleSpec = new ModuleSpecification(moduleSpecName);

                    if (currentModuleSpec != null)
                    {
                        moduleSpecsList.Add(currentModuleSpec);
                    }
                    else
                    {
                        var exMessage = $"ModuleSpecification object was not able to be created for {moduleSpecName}";
                        var ex = new ArgumentException(exMessage);
                        var ModuleSpecNotCreatedError = new ErrorRecord(ex, "ModuleSpecificationNotCreated", ErrorCategory.InvalidArgument, null);
                        errorList.Add(ModuleSpecNotCreatedError);
                        moduleSpecCreatedSuccessfully = false;
                        continue;
                    }
                }
                else
                {
                    // ModuleSpecification(Hashtable) constructor for when ModuleName + {Required,Maximum,Module}Version value is also provided.
                    string moduleSpecMaxVersion = moduleSpec.ContainsKey("MaximumVersion") ? (string) moduleSpec["MaximumVersion"] : String.Empty;
                    string moduleSpecModuleVersion = moduleSpec.ContainsKey("ModuleVersion") ? (string) moduleSpec["ModuleVersion"] : String.Empty;
                    string moduleSpecRequiredVersion = moduleSpec.ContainsKey("RequiredVersion") ? (string) moduleSpec["RequiredVersion"] : String.Empty;
                    Guid moduleSpecGuid = moduleSpec.ContainsKey("Guid") ? (Guid) moduleSpec["Guid"] : Guid.Empty;

                    if (String.IsNullOrEmpty(moduleSpecMaxVersion) && String.IsNullOrEmpty(moduleSpecModuleVersion) && String.IsNullOrEmpty(moduleSpecRequiredVersion))
                    {
                        var exMessage = $"ModuleSpecification hashtable requires one of the following keys: MaximumVersion, ModuleVersion, RequiredVersion and failed to be created for {moduleSpecName}";
                        var ex = new ArgumentException(exMessage);
                        var MissingModuleSpecificationMemberError = new ErrorRecord(ex, "MissingModuleSpecificationMember", ErrorCategory.InvalidArgument, null);
                        errorList.Add(MissingModuleSpecificationMemberError);
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
                        var ex = new ArgumentException($"ModuleSpecification instance was not able to be created with hashtable constructor due to: {e.Message}");
                        var ModuleSpecNotCreatedError = new ErrorRecord(ex, "ModuleSpecificationNotCreated", ErrorCategory.InvalidArgument, null);
                        errorList.Add(ModuleSpecNotCreatedError);
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

        /// <summary>
        /// Parses metadata out of a comment block's lines (which are passed in) into a hashtable.
        /// </summary>
        public static Hashtable ParseCommentBlockContent(string[] commentLines)
        {
            /**
            Comment lines can look like this:

            .KEY1 value

            .KEY2 value

            .KEY3
            value

            .KEY4 value
            value continued

            */

            Hashtable parsedHelpMetadata = new Hashtable();
            string keyName = "";
            string value = "";

            for (int i = 1; i < commentLines.Count(); i++)
            {
                string line = commentLines[i];

                // scenario where line is: .KEY VALUE
                // this line contains a new metadata property.
                if (line.Trim().StartsWith("."))
                {
                    // check if keyName was previously populated, if so add this key value pair to the metadata hashtable
                    if (!String.IsNullOrEmpty(keyName))
                    {
                        parsedHelpMetadata.Add(keyName, value);
                    }

                    string[] parts = line.Trim().TrimStart('.').Split();
                    keyName = parts[0];
                    value = parts.Count() > 1 ? String.Join(" ", parts.Skip(1)) : String.Empty;
                }
                else if (!String.IsNullOrEmpty(line))
                {
                    // scenario where line contains text that is a continuation of value from previously recorded key
                    // this line does not starting with .KEY, and is also not an empty line.
                    if (value.Equals(String.Empty))
                    {
                        value += line;
                    }
                    else
                    {
                        value += Environment.NewLine + line;
                    }
                }
            }

            // this is the case where last key value had multi-line value.
            // and we've captured it, but still need to add it to hashtable.
            if (!String.IsNullOrEmpty(keyName) && !parsedHelpMetadata.ContainsKey(keyName))
            {
                // only add this key value if it hasn't already been added
                parsedHelpMetadata.Add(keyName, value);
            }

            return parsedHelpMetadata;
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
            foreach (var dirFilePath in Directory.GetFiles(dirPath))
            {
                if (File.GetAttributes(dirFilePath).HasFlag(FileAttributes.ReadOnly))
                {
                    File.SetAttributes(dirFilePath, (File.GetAttributes(dirFilePath) & ~FileAttributes.ReadOnly));
                }

                File.Delete(dirFilePath);
            }

            foreach (var dirSubPath in Directory.GetDirectories(dirPath))
            {
                DeleteDirectory(dirSubPath);
            }

            Directory.Delete(dirPath);
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
                    script: "(Get-Module -Name PowerShellGet).Path");
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

            // First check if the files are catalog signed.
            string catalogFilePath = Path.Combine(tempDirNameVersion, pkgName + ".cat");
            if (File.Exists(catalogFilePath))
            {
                // Run catalog validation.
                Collection<PSObject> TestFileCatalogResult;
                string moduleBasePath = tempDirNameVersion;
                try
                {
                    // By default "Test-FileCatalog will look through all files in the provided directory, -FilesToSkip allows us to ignore specific files.
                    TestFileCatalogResult = cmdletPassedIn.InvokeCommand.InvokeScript(
                        script: @"param (
                                      [string] $moduleBasePath, 
                                      [string] $catalogFilePath
                                 ) 
                                $catalogValidation = Test-FileCatalog -Path $moduleBasePath -CatalogFilePath $CatalogFilePath `
                                                 -FilesToSkip '*.nupkg','*.nuspec', '*.nupkg.metadata', '*.nupkg.sha512' `
                                                 -Detailed -ErrorAction SilentlyContinue
        
                                if ($catalogValidation.Status.ToString() -eq 'valid' -and $catalogValidation.Signature.Status -eq 'valid') {
                                    return $true
                                }
                                else {
                                    return $false
                                }
                        ",
                        useNewScope: true,
                        writeToPipeline: System.Management.Automation.Runspaces.PipelineResultTypes.None,
                        input: null,
                        args: new object[] { moduleBasePath, catalogFilePath });
                }
                catch (Exception e)
                {
                    errorRecord = new ErrorRecord(new ArgumentException(e.Message), "TestFileCatalogError", ErrorCategory.InvalidResult, cmdletPassedIn);
                    return false;
                }

                bool catalogValidation = TestFileCatalogResult.Count > 0 ? (bool)TestFileCatalogResult[0].BaseObject : false;
                if (!catalogValidation)
                {
                    var exMessage = String.Format("The catalog file '{0}' is invalid.", pkgName + ".cat");
                    var ex = new ArgumentException(exMessage);

                    errorRecord = new ErrorRecord(ex, "TestFileCatalogError", ErrorCategory.InvalidResult, cmdletPassedIn);
                    return false;
                }

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
                errorRecord = new ErrorRecord(new ArgumentException(e.Message), "GetAuthenticodeSignatureError", ErrorCategory.InvalidResult, cmdletPassedIn);
                return false;
            }

            // If any file authenticode signatures are not valid, return false.
            foreach (var signatureObject in authenticodeSignatures)
            {
                Signature signature = (Signature)signatureObject.BaseObject;
                if (!signature.Status.Equals(SignatureStatus.Valid))
                {
                    var exMessage = String.Format("The signature for '{0}' is '{1}.", pkgName, signature.Status.ToString());
                    var ex = new ArgumentException(exMessage);
                    errorRecord = new ErrorRecord(ex, "GetAuthenticodeSignatureError", ErrorCategory.InvalidResult, cmdletPassedIn);

                    return false;
                }
            }

            return true;
        }
       
        #endregion
    }

    #endregion
}
