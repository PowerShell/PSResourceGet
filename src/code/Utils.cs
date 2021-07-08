// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using static System.Environment;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Runtime.InteropServices;
using NuGet.Versioning;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    internal static class Utils
    {
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

        public static string[] FilterOutWildcardNames(
            string[] pkgNames,
            out string[] errorMsgs)
        {
            List<string> errorFreeNames = new List<string>();
            List<string> errorMsgList = new List<string>();

            foreach (string n in pkgNames)
            {
                bool isNameErrorProne = false;
                if (WildcardPattern.ContainsWildcardCharacters(n))
                {
                    if (String.Equals(n, "*", StringComparison.InvariantCultureIgnoreCase))
                    {
                        errorMsgList = new List<string>(); // clear prior error messages
                        errorMsgList.Add("-Name '*' is not supported for Find-PSResource so all Name entries will be discarded.");
                        errorFreeNames = new List<string>();
                        break;
                    }
                    else if (n.Contains("?") || n.Contains("["))
                    {
                        errorMsgList.Add(String.Format("-Name with wildcards '?' and '[' are not supported for Find-PSResource so Name entry: {0} will be discarded.", n));
                        isNameErrorProne = true;
                    }
                }

                if (!isNameErrorProne)
                {
                    errorFreeNames.Add(n);
                }
            }

            errorMsgs = errorMsgList.ToArray();
            return errorFreeNames.ToArray();
        }

        #region Public methods

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

        public static string GetInstalledPackageName(string pkgPath)
        {
            if (string.IsNullOrEmpty(pkgPath))
            {
                return string.Empty;
            }

            if (File.Exists(pkgPath))
            {
                // ex: ./PowerShell/Scripts/TestScript.ps1
                return System.IO.Path.GetFileNameWithoutExtension(pkgPath);
            }
            else
            {
                // expecting the full version module path
                // ex:  ./PowerShell/Modules/TestModule/1.0.0
                return new DirectoryInfo(pkgPath).Parent.Name;
            }
        }

        public static List<string> GetAllResourcePaths(PSCmdlet psCmdlet)
        {
            string psModulePath = Environment.GetEnvironmentVariable("PSModulePath");
            List<string> resourcePaths = psModulePath.Split(';').ToList();
            List<string> pathsToSearch = new List<string>();
            var PSVersion6 = new Version(6, 0);
            var isCorePS = psCmdlet.Host.Version >= PSVersion6;
            string myDocumentsPath;
            string programFilesPath;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string powerShellType = isCorePS ? "PowerShell" : "WindowsPowerShell";

                myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.MyDocuments), powerShellType);
                programFilesPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.ProgramFiles), powerShellType);
            }
            else
            {
                // paths are the same for both Linux and MacOS
                myDocumentsPath = System.IO.Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "Powershell");
                programFilesPath = System.IO.Path.Combine("usr", "local", "share", "Powershell");
            }

            // will search first in PSModulePath, then will search in default paths
            resourcePaths.Add(System.IO.Path.Combine(myDocumentsPath, "Modules"));
            resourcePaths.Add(System.IO.Path.Combine(programFilesPath, "Modules"));
            resourcePaths.Add(System.IO.Path.Combine(myDocumentsPath, "Scripts"));
            resourcePaths.Add(System.IO.Path.Combine(programFilesPath, "Scripts"));

            // resourcePaths should now contain, eg:
            // ./PowerShell/Scripts
            // ./PowerShell/Modules
            // add all module directories or script files
            foreach (string path in resourcePaths)
            {
                psCmdlet.WriteDebug(string.Format("Retrieving directories in the path '{0}'", path));

                if (path.EndsWith("Scripts"))
                {
                    try
                    {
                        pathsToSearch.AddRange(Directory.GetFiles(path));
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
                        pathsToSearch.AddRange(Directory.GetDirectories(path));
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
            pathsToSearch.ForEach(dir => psCmdlet.WriteDebug(string.Format("All paths to search: '{0}'", dir)));

            return pathsToSearch;
        }

        // Find all potential installation paths given a scope
        public static List<string> GetAllInstallationPaths(PSCmdlet psCmdlet, ScopeType scope)
        {
            List<string> installationPaths = new List<string>();
            var PSVersion6 = new Version(6, 0);
            var isCorePS = psCmdlet.Host.Version >= PSVersion6;
            string myDocumentsPath;
            string programFilesPath;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string powerShellType = isCorePS ? "PowerShell" : "WindowsPowerShell";

                myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.MyDocuments), powerShellType);
                programFilesPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.ProgramFiles), powerShellType);
            }
            else
            {
                // paths are the same for both Linux and MacOS
                myDocumentsPath = System.IO.Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "Powershell");
                programFilesPath = System.IO.Path.Combine("usr", "local", "share", "Powershell");
            }


            // If no explicit specification, will return PSModulePath, and then CurrentUser paths
            // Installation will search for a /Modules or /Scripts directory 
            // If they are not available within one of the paths in PSModulePath, the CurrentUser path will be used.
            if (scope == ScopeType.None)
            {
                string psModulePath = Environment.GetEnvironmentVariable("PSModulePath");
                installationPaths = psModulePath.Split(';').ToList();
                installationPaths.Add(System.IO.Path.Combine(myDocumentsPath, "Modules"));
                installationPaths.Add(System.IO.Path.Combine(myDocumentsPath, "Scripts"));
            }
            // If user explicitly specifies AllUsers
            if (scope == ScopeType.AllUsers)
            {
                installationPaths.Add(System.IO.Path.Combine(programFilesPath, "Modules"));
                installationPaths.Add(System.IO.Path.Combine(programFilesPath, "Scripts"));
            }
            // If user explicitly specifies CurrentUser
            else if (scope == ScopeType.CurrentUser)
            {
                installationPaths.Add(System.IO.Path.Combine(myDocumentsPath, "Modules"));
                installationPaths.Add(System.IO.Path.Combine(myDocumentsPath, "Scripts"));
            }
            else
            {
                psCmdlet.WriteDebug(string.Format("Invalid scope provided: '{0}'", scope));
            }

            installationPaths = installationPaths.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
            installationPaths.ForEach(dir => psCmdlet.WriteDebug(string.Format("All paths to search: '{0}'", dir)));

            return installationPaths;
        }

        private static string GetResourceNameFromPath(string path)
        {
            // Resource paths may end in a directory or script file name.
            // Directory name is the same as the resource name.
            // Script file name is the resource name without the file extension.
            // ./Modules/Microsoft.PowerShell.Test-Module     : Microsoft.PowerShell.Test-Module
            // ./Scripts/Microsoft.PowerShell.Test-Script.ps1 : Microsoft.PowerShell.Test-Script
            var resourceName = Path.GetFileName(path);
            return Path.GetExtension(resourceName).Equals(".ps1", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(resourceName) : resourceName;
        }

        public static Hashtable ParseModuleManifest(string moduleFileInfo, PSCmdlet cmdletPassedIn)
        {
            Hashtable parsedMetadataHash = new Hashtable();
            // A script will already  have the metadata parsed into the parsedMetadatahash,
            // a module will still need the module manifest to be parsed.
            if (moduleFileInfo.EndsWith(".psd1", StringComparison.OrdinalIgnoreCase))
            {
                // Parse the module manifest 
                System.Management.Automation.Language.Token[] tokens;
                ParseError[] errors;
                var ast = Parser.ParseFile(moduleFileInfo, out tokens, out errors);

                if (errors.Length > 0)
                {
                    var message = String.Format("Could not parse '{0}' as a PowerShell data file.", moduleFileInfo);
                    var ex = new ArgumentException(message);
                    var psdataParseError = new ErrorRecord(ex, "psdataParseError", ErrorCategory.ParserError, null);
                    cmdletPassedIn.WriteError(psdataParseError);
                }
                else
                {
                    var data = ast.Find(a => a is HashtableAst, false);
                    if (data != null)
                    {
                        parsedMetadataHash = (Hashtable)data.SafeGetValue();
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

            return parsedMetadataHash;
        }
        #endregion
    }
}
