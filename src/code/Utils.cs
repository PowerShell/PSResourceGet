// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using NuGet.Versioning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    internal static class Utils
    {
        #region String fields

        public static readonly string[] EmptyStrArray = Array.Empty<string>();

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

        #region Url methods

        public static bool TryCreateValidUrl(
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
                // This is needed for a relative path urlstring. Does not throw error for an absolute path.
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

        #region Manifest methods

        public static bool TryParseModuleManifest(
            string moduleFileInfo,
            PSCmdlet cmdletPassedIn,
            out Hashtable parsedMetadataHashtable)
        {
            parsedMetadataHashtable = new Hashtable();
            bool successfullyParsed = false;

            // A script will already  have the metadata parsed into the parsedMetadatahash,
            // a module will still need the module manifest to be parsed.
            if (moduleFileInfo.EndsWith(".psd1", StringComparison.OrdinalIgnoreCase))
            {
                // Parse the module manifest 
                var ast = Parser.ParseFile(
                    moduleFileInfo,
                    out Token[] tokens,
                    out ParseError[] errors);

                if (errors.Length > 0)
                {
                    var message = String.Format("Could not parse '{0}' as a PowerShell data file.", moduleFileInfo);
                    var ex = new ArgumentException(message);
                    var psdataParseError = new ErrorRecord(ex, "psdataParseError", ErrorCategory.ParserError, null);
                    cmdletPassedIn.WriteError(psdataParseError);
                    return successfullyParsed;
                }
                else
                {
                    var data = ast.Find(a => a is HashtableAst, false);
                    if (data != null)
                    {
                        parsedMetadataHashtable = (Hashtable)data.SafeGetValue();
                        successfullyParsed = true;
                    }
                    else
                    {
                        var message = String.Format("Could not parse as PowerShell data file-- no hashtable root for file '{0}'", moduleFileInfo);
                        var ex = new ArgumentException(message);
                        var psdataParseError = new ErrorRecord(ex, "psdataParseError", ErrorCategory.ParserError, null);
                        cmdletPassedIn.WriteError(psdataParseError);
                    }
                }
            }

            return successfullyParsed;
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
}
