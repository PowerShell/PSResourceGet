// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Management.Automation;
using System.Collections;
using System.Management.Automation.Language;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static System.Environment;
using System.IO;
using System.Linq;
using NuGet.Versioning;


namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    internal static class Utils
    {
        #region Public methods

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

        public static bool TryParseVersionOrVersionRange(string Version, out VersionRange versionRange)
        {
            var successfullyParsed = false;
            NuGetVersion nugetVersion = null;
            versionRange = null;

            if (Version != null)
            {
                if (Version.Trim().Equals("*"))
                {
                    successfullyParsed = true;
                    versionRange = VersionRange.All;
                }
                else
                {
                    successfullyParsed = NuGetVersion.TryParse(Version, out nugetVersion);
                    if (successfullyParsed)
                    {
                        versionRange = new VersionRange(nugetVersion, true, nugetVersion, true, null, null);

                    }
                    else
                    {
                        successfullyParsed = VersionRange.TryParse(Version, out versionRange);
                    }
                }
            }
            return successfullyParsed;
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

        #endregion
    }
}
