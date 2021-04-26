// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using NuGet.Versioning;
using System;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    #region Utils

    internal static class Utils
    {
        #region Members

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

        public static bool TryParseVersionOrVersionRange(string Version, out NuGetVersion nugetVersion, out VersionRange versionRange, PSCmdlet cmdletPassedIn)
        {
            var successfullyParsed = false;
            nugetVersion = null;
            versionRange = null;
            if (Version != null)
            {
                successfullyParsed = NuGetVersion.TryParse(Version, out nugetVersion);
                if (!successfullyParsed)
                {
                    successfullyParsed = VersionRange.TryParse(Version, out versionRange);
                }
            }
            return successfullyParsed;
        }

        #endregion
    }

    #endregion
}
