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

        public static bool TryParseToNuGetVersionRange(string version, PSCmdlet cmdletPassedIn, out VersionRange versionRange)
        {
            // try to parse into a specific NuGet version
            versionRange = null;
            var success = false;
            if (version != null)
            {
                NuGetVersion.TryParse(version, out NuGetVersion specificVersion);

                if (specificVersion != null)
                {
                    // check if exact version
                    versionRange = new VersionRange(specificVersion, true, specificVersion, true, null, null);
                    cmdletPassedIn.WriteDebug(string.Format("A specific version, '{0}', is specified", versionRange.ToString()));
                    success = true;
                }
                else
                {
                    success = true;
                    // check if version range
                    if (!VersionRange.TryParse(version, out versionRange))
                    {
                        cmdletPassedIn.WriteError(new ErrorRecord(
                            new ParseException(),
                            "ErrorParsingVersion",
                            ErrorCategory.ParserError,
                            cmdletPassedIn));
                        success = false;
                    }
                    cmdletPassedIn.WriteDebug(string.Format("A version range, '{0}', is specified", versionRange.ToString()));
                }
            }

            return success;
        }

        #endregion
    }

    #endregion
}
