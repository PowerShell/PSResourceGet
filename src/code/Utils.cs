// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using NuGet.Versioning;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    internal static class Utils
    {
        public static string[] FilterOutWildcardNames(
            string[] pkgNames,
            out string[] errorMsgs)
        {
            List<string> temp = new List<string>();
            List<string> errorMsgList = new List<string>();

            foreach (string n in pkgNames)
            {
                bool isNameErrorProne = false;
                if (WildcardPattern.ContainsWildcardCharacters(n))
                {
                    if (String.Equals(n, "*", StringComparison.InvariantCultureIgnoreCase))
                    {
                        errorMsgList = new List<string>(); // clear prior error messages
                        errorMsgList.Add("-Name '*' is not supported for Find-PSResource so all Name entries will be discarded. Please use the TODO-cmdlet");
                        temp = new List<string>();
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
                    temp.Add(n);
                }
            }

            errorMsgs = errorMsgList.ToArray();
            return temp.ToArray();
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
            for (int i=0; i<list.Count; i++)
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
                    originalString: null);
                return true;
            }

            // parse as Version range
            return VersionRange.TryParse(version, out versionRange);
        }

        #endregion
    }
}
