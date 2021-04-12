// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using NuGet.Versioning;
using System;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Xml;

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
            if (version != null)
            {
                NuGetVersion.TryParse(version, out NuGetVersion specificVersion);

                if (specificVersion != null)
                {
                    // check if exact version
                    versionRange = new VersionRange(specificVersion, true, specificVersion, true, null, null);
                    cmdletPassedIn.WriteDebug(string.Format("A specific version, '{0}', is specified", versionRange.ToString()));
                }
                else
                {
                    // check if version range
                    if (!VersionRange.TryParse(version, out versionRange))
                    {
                        cmdletPassedIn.WriteError(new ErrorRecord(
                            new ParseException(),
                            "ErrorParsingVersion",
                            ErrorCategory.ParserError,
                            cmdletPassedIn));
                        return false;
                    }
                    cmdletPassedIn.WriteDebug(string.Format("A version range, '{0}', is specified", versionRange.ToString()));
                }
            }

            return true;
        }

        public static NuGetVersion ReadScriptVersionFromXML(string xmlPath, PSCmdlet cmdletPassedIn)
        {
            XmlDocument doc = new XmlDocument();
            try
            {
                // open file
                doc.Load(xmlPath);
            }
            catch (Exception e)
            {
                // throw non-terminating error 
                throw new PSInvalidOperationException(String.Format("Loading script XML failed: {0}", e.Message));
            }

            // Find script version
            XmlNode versionNode = doc.DocumentElement.SelectSingleNode("Version");
            //XmlNode IPnode = doc.DocumentElement["Ip"];

            string version = versionNode.InnerText;

            // try to parse into a specific NuGet version
            NuGetVersion nugetVersion = null;
            if (string.IsNullOrEmpty(version) && NuGetVersion.TryParse(version, out nugetVersion))
            { 
                // exact version
                cmdletPassedIn.WriteDebug(string.Format("Version '{0}' is parsed from the script metadata file '{1}'", nugetVersion.ToString(), xmlPath));
            }
            else 
            {
                // if unable to parse as a specific version, throw non-terminating error
                cmdletPassedIn.WriteError(new ErrorRecord(
                    new ParseException(),
                    "ErrorParsingVersion",
                    ErrorCategory.ParserError,
                    cmdletPassedIn));
            }

            return nugetVersion;
        }

        #endregion
    }

    #endregion
}
