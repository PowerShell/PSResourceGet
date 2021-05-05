// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using NuGet.Versioning;
using System;
using System.Management.Automation;
using System.Collections;
using System.Management.Automation.Language;

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

        public static bool TryParseVersionOrVersionRange(string Version, out VersionRange versionRange, out bool allVersions)
        {
            var successfullyParsed = false;
            NuGetVersion nugetVersion = null;
            versionRange = null;
            allVersions = false;
            if (Version != null)
            {
                if (Version.Trim().Equals("*"))
                {
                    allVersions = true;
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
