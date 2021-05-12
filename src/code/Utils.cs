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
            string[] modulePaths = psModulePath.Split(';');
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

            // add all potential resource paths 
            // will search first in PSModulePath, then will search in default paths
            foreach (string modulePath in modulePaths)
            {
                psCmdlet.WriteDebug(string.Format("Retrieving directories in the '{0}' module path", modulePath));
                try
                {
                    pathsToSearch.AddRange(Directory.GetDirectories(modulePath));
                }
                catch (Exception e)
                {
                    psCmdlet.WriteDebug(string.Format("Error retrieving directories from '{0}': '{1)'", modulePath, e.Message));
                }
            }
            pathsToSearch.Add(System.IO.Path.Combine(myDocumentsPath, "Modules"));
            pathsToSearch.Add(System.IO.Path.Combine(programFilesPath, "Modules"));
            pathsToSearch.Add(System.IO.Path.Combine(myDocumentsPath, "Scripts"));
            pathsToSearch.Add(System.IO.Path.Combine(programFilesPath, "Scripts"));

            // need to use .ToList() to cast the IEnumerable<string> to type List<string>
            pathsToSearch = pathsToSearch.Distinct().ToList();
            pathsToSearch.ForEach(dir => psCmdlet.WriteDebug(string.Format("All directories to search: '{0}'", dir)));

            return pathsToSearch;
        }

        /// <summary>
        /// Converts an ArrayList of object types to a string array.
        /// </summary>
        public static string[] GetStringArray(ArrayList list)
        {
            if (list == null) { return null; }

            var strArray = new string[list.Count];
            for (int i=0; i<list.Count; i++)
            {
                strArray[i] = list[i] as string;
            }

            return strArray;
        }

        #endregion
    }
}
