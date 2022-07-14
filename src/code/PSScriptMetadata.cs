using System.Net.Http.Headers;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.ObjectModel;
using Microsoft.PowerShell.Commands;
using NuGet.Versioning;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    /// <summary>
    /// This class contains information for a PSScriptFileInfo (representing a .ps1 file contents).
    /// </summary>
    public sealed class PSScriptMetadata
    {
        #region Properties

        /// <summary>
        /// the version of the script.
        /// </summary>
        public NuGetVersion Version { get; private set; }

        /// <summary>
        /// the GUID for the script.
        /// </summary>
        public Guid Guid { get; private set; }

        /// <summary>
        /// the author for the script.
        /// </summary>
        public string Author { get; private set; }

        /// <summary>
        /// the name of the company owning the script.
        /// </summary>
        public string CompanyName { get; private set; }

        /// <summary>
        /// the copyright statement for the script.
        /// </summary>
        public string Copyright { get; private set; }

        /// <summary>
        /// the tags for the script.
        /// </summary>
        public string[] Tags { get; private set; }

        /// <summary>
        /// the Uri for the license of the script.
        /// </summary>
        public Uri LicenseUri { get; private set; }

        /// <summary>
        /// the Uri for the project relating to the script.
        /// </summary>
        public Uri ProjectUri { get; private set; }

        /// <summary>
        /// the Uri for the icon relating to the script.
        /// </summary>
        public Uri IconUri { get; private set; }

        /// <summary>
        /// the list of external module dependencies for the script.
        /// </summary>
        public string[] ExternalModuleDependencies { get; private set; } = new string[]{};

        /// <summary>
        /// the list of required scripts for the parent script.
        /// </summary>
        public string[] RequiredScripts { get; private set; } = new string[]{};

        /// <summary>
        /// the list of external script dependencies for the script.
        /// </summary>
        public string[] ExternalScriptDependencies { get; private set; } = new string[]{};

        /// <summary>
        /// the release notes relating to the script.
        /// </summary>
        public string ReleaseNotes { get; private set; } = String.Empty;

        /// <summary>
        /// The private data associated with the script.
        /// </summary>
        public string PrivateData { get; private set; }

        #endregion

        #region Constructor

        public PSScriptMetadata(
            string version,
            Guid guid,
            string author,
            string companyName,
            string copyright,
            string[] tags,
            Uri licenseUri,
            Uri projectUri,
            Uri iconUri,
            string[] externalModuleDependencies,
            string[] requiredScripts,
            string[] externalScriptDependencies,
            string releaseNotes,
            string privateData)
        {
            if (String.IsNullOrEmpty(author))
            {
                author = Environment.UserName;
            }

            Version = !String.IsNullOrEmpty(version) ? new NuGetVersion (version) : new NuGetVersion("1.0.0.0");
            Guid = (guid == null || guid == Guid.Empty) ? Guid.NewGuid() : guid;
            Author = !String.IsNullOrEmpty(author) ? author : Environment.UserName;
            CompanyName = companyName;
            Copyright = copyright;
            Tags = tags ?? Utils.EmptyStrArray;
            LicenseUri = licenseUri;
            ProjectUri = projectUri;
            IconUri = iconUri;
            ExternalModuleDependencies = externalModuleDependencies ?? Utils.EmptyStrArray;
            RequiredScripts = requiredScripts ?? Utils.EmptyStrArray;
            ExternalScriptDependencies = externalScriptDependencies ?? Utils.EmptyStrArray;
            ReleaseNotes = releaseNotes;
            PrivateData = privateData;
        }

        internal PSScriptMetadata() {}

        #endregion

        #region Internal Methods

        /// <summary>
        /// Parses script metadata comment (passed in as its lines) into PSScriptMetadata instance's properties
        /// Also validates that this metadata has required script properties
        /// </summary>
        internal bool ParseContentIntoObj(string[] commentLines, out ErrorRecord[] errors, out string[] msgs)
        {
            msgs = new string[]{};
            List<string> msgsList = new List<string>();

            // parse content into a hashtable
            Hashtable parsedMetadata = ParseContent(commentLines);

            if (parsedMetadata.Count == 0)
            {
                var message = String.Format("PowerShell script '<#PSScriptInfo .. #>' comment block contains no metadata");
                var ex = new InvalidOperationException(message);
                var psScriptInfoBlockMissingMetadata = new ErrorRecord(ex, "psScriptInfoBlockMissingMetadataError", ErrorCategory.ParserError, null);
                errors = new ErrorRecord[]{psScriptInfoBlockMissingMetadata};
                return false;
            }

            // check parsed metadata contains required Author, Version, Guid key values
            if (!ValidateParsedContent(parsedMetadata, out errors))
            {
                return false;
            }

            // now populate the object instance
            char[] spaceDelimeter = new char[]{' '};

            Uri parsedLicenseUri = null;
            if (!String.IsNullOrEmpty((string) parsedMetadata["LICENSEURI"]))
            {
                if (!Uri.TryCreate((string) parsedMetadata["LICENSEURI"], UriKind.Absolute, out parsedLicenseUri))
                {
                    msgsList.Add($"LicenseUri property {(string) parsedMetadata["LICENSEURI"]} could not be created as a Uri");   
                }
            }

            Uri parsedProjectUri = null;
            if (!String.IsNullOrEmpty((string) parsedMetadata["PROJECTURI"]))
            {
                if (!Uri.TryCreate((string) parsedMetadata["PROJECTURI"], UriKind.Absolute, out parsedProjectUri))
                {
                    msgsList.Add($"ProjectUri property {(string) parsedMetadata["PROJECTURI"]} could not be created as Uri");
                }
            }

            Uri parsedIconUri = null;
            if (!String.IsNullOrEmpty((string) parsedMetadata["ICONURI"]))
            {
                if (!Uri.TryCreate((string) parsedMetadata["ICONURI"], UriKind.Absolute, out parsedIconUri))
                {
                    msgsList.Add($"IconUri property {(string) parsedMetadata["ICONURI"]} could not be created as Uri");
                }
            }

            // now populate PSScriptMetadata object properties with parsed metadata
            Author = (string) parsedMetadata["AUTHOR"];
            Version = new NuGetVersion((string) parsedMetadata["VERSION"]);
            Guid = new Guid((string) parsedMetadata["GUID"]);

            CompanyName = (string) parsedMetadata["COMPANYNAME"] ?? String.Empty;
            Copyright = (string) parsedMetadata["COPYRIGHT"] ?? String.Empty;

            LicenseUri = parsedLicenseUri;
            ProjectUri = parsedProjectUri;
            IconUri = parsedIconUri;
            
            Tags = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedMetadata["TAGS"]);;
            ExternalModuleDependencies = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedMetadata["EXTERNALMODULEDEPENDENCIES"]);
            RequiredScripts = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedMetadata["REQUIREDSCRIPTS"]);
            ExternalScriptDependencies = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedMetadata["EXTERNALSCRIPTDEPENDENCIES"]);
            ReleaseNotes = (string) parsedMetadata["RELEASENOTES"] ?? String.Empty;
            PrivateData = (string) parsedMetadata["PRIVATEDATA"] ?? String.Empty;

            msgs = msgsList.ToArray();
            return true;
        }

        /// <summary>
        /// Helper method that parses metadata out of of comment block's lines (which are passed in) into a hashtable
        /// </summary>
        internal Hashtable ParseContent(string[] commentLines)
        {
            /**
            comment lines will look like this:

            .VERSION 1.0

            .GUID 3951be04-bd06-4337-8dc3-a620bf539fbd

            .AUTHOR

            .COMPANYNAME

            .COPYRIGHT

            .TAGS

            .LICENSEURI

            .PROJECTURI

            .ICONURI

            .EXTERNALMODULEDEPENDENCIES

            .REQUIREDSCRIPTS

            .EXTERNALSCRIPTDEPENDENCIES

            .RELEASENOTES
            some notes

            .PRIVATEDATA
            some data

            */

            Hashtable parsedMetadata = new Hashtable();
        
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
                        parsedMetadata.Add(keyName, value);
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
            if (!String.IsNullOrEmpty(keyName) && !parsedMetadata.ContainsKey(keyName))
            {
                // only add this key value if it hasn't already been added
                parsedMetadata.Add(keyName, value);
            }

            return parsedMetadata;
        }


        internal bool ValidateParsedContent(Hashtable parsedMetadata, out ErrorRecord[] errors)
        {
            List<ErrorRecord> errorsList = new List<ErrorRecord>();

            if (!parsedMetadata.ContainsKey("VERSION") || String.IsNullOrEmpty((string) parsedMetadata["VERSION"]) || String.Equals(((string) parsedMetadata["VERSION"]).Trim(), String.Empty))
            {
                var message = String.Format("PSScript file is missing the required Version property");
                var ex = new ArgumentException(message);
                var psScriptMissingVersionError = new ErrorRecord(ex, "psScriptMissingVersion", ErrorCategory.ParserError, null);
                errorsList.Add(psScriptMissingVersionError);
            }

            if (!parsedMetadata.ContainsKey("AUTHOR") || String.IsNullOrEmpty((string) parsedMetadata["AUTHOR"]) || String.Equals(((string) parsedMetadata["AUTHOR"]).Trim(), String.Empty))
            {
                var message = String.Format("PSScript file is missing the required Author property");
                var ex = new ArgumentException(message);
                var psScriptMissingAuthorError = new ErrorRecord(ex, "psScriptMissingAuthor", ErrorCategory.ParserError, null);
                errorsList.Add(psScriptMissingAuthorError);
            }

            if (!parsedMetadata.ContainsKey("GUID") || String.IsNullOrEmpty((string) parsedMetadata["GUID"]) || String.Equals(((string) parsedMetadata["GUID"]).Trim(), String.Empty))
            {
                var message = String.Format("PSScript file is missing the required Guid property");
                var ex = new ArgumentException(message);
                var psScriptMissingGuidError = new ErrorRecord(ex, "psScriptMissingGuid", ErrorCategory.ParserError, null);
                errorsList.Add(psScriptMissingGuidError);
            }

            errors = errorsList.ToArray();
            return errors.Length == 0;
        }
        /// <summary>
        /// Validates metadata content parsed from .ps1 is valid and contains required script properties
        /// i.e Author, Version, Guid
        /// </summary>
        internal bool ValidateContent(out ErrorRecord[] errors)
        {
            bool validPSScriptInfo = true;
            List<ErrorRecord> errorsList = new List<ErrorRecord>();

            if (Version == null || String.IsNullOrEmpty(Version.ToString()))
            {
                var message = String.Format("PSScript file is missing the required Version property");
                var ex = new ArgumentException(message);
                var psScriptMissingVersionError = new ErrorRecord(ex, "psScriptMissingVersion", ErrorCategory.ParserError, null);
                errorsList.Add(psScriptMissingVersionError);
                validPSScriptInfo = false;
            }

            if (String.IsNullOrEmpty(Author))
            {
                var message = String.Format("PSScript file is missing the required Author property");
                var ex = new ArgumentException(message);
                var psScriptMissingAuthorError = new ErrorRecord(ex, "psScriptMissingAuthor", ErrorCategory.ParserError, null);
                errorsList.Add(psScriptMissingAuthorError);
                validPSScriptInfo = false;
            }

            if (Guid == Guid.Empty)
            {
                var message = String.Format("PSScript file is missing the required Guid property");
                var ex = new ArgumentException(message);
                var psScriptMissingGuidError = new ErrorRecord(ex, "psScriptMissingGuid", ErrorCategory.ParserError, null);
                errorsList.Add(psScriptMissingGuidError);
                validPSScriptInfo = false;
            }

            errors = errorsList.ToArray();
            return validPSScriptInfo;
        }

        /// <summary>
        /// Emits string representation of '<#PSScriptInfo ... #>' comment and its metadata contents
        /// </summary>
        internal string EmitContent()
        {
            /**
            PSScriptInfo comment will be in following format:
            <#PSScriptInfo
                .VERSION 1.0
                .GUID 544238e3-1751-4065-9227-be105ff11636
                .AUTHOR manikb
                .COMPANYNAME Microsoft Corporation
                .COPYRIGHT (c) 2015 Microsoft Corporation. All rights reserved.
                .TAGS Tag1 Tag2 Tag3
                .LICENSEURI https://contoso.com/License
                .PROJECTURI https://contoso.com/
                .ICONURI https://contoso.com/Icon
                .EXTERNALMODULEDEPENDENCIES ExternalModule1
                .REQUIREDSCRIPTS Start-WFContosoServer,Stop-ContosoServerScript
                .EXTERNALSCRIPTDEPENDENCIES Stop-ContosoServerScript
                .RELEASENOTES
                contoso script now supports following features
                Feature 1
                Feature 2
                Feature 3
                Feature 4
                Feature 5
                .PRIVATEDATA
                #>
            */

            List<string> psScriptInfoLines = new List<string>();

            psScriptInfoLines.Add("<#PSScriptInfo");
            psScriptInfoLines.Add(String.Format(".VERSION {0}", Version.ToString()));
            psScriptInfoLines.Add(String.Format(".GUID {0}", Guid.ToString()));
            psScriptInfoLines.Add(String.Format(".AUTHOR {0}", Author));
            psScriptInfoLines.Add(String.Format(".COMPANYNAME {0}", CompanyName));
            psScriptInfoLines.Add(String.Format(".COPYRIGHT {0}", Copyright));
            psScriptInfoLines.Add(String.Format(".TAGS {0}", String.Join(" ", Tags)));
            psScriptInfoLines.Add(String.Format(".LICENSEURI {0}", LicenseUri == null ? String.Empty : LicenseUri.ToString()));
            psScriptInfoLines.Add(String.Format(".PROJECTURI {0}", ProjectUri == null ? String.Empty : ProjectUri.ToString()));
            psScriptInfoLines.Add(String.Format(".ICONURI {0}", IconUri == null ? String.Empty : IconUri.ToString()));
            psScriptInfoLines.Add(String.Format(".EXTERNALMODULEDEPENDENCIES {0}", String.Join(" ", ExternalModuleDependencies)));
            psScriptInfoLines.Add(String.Format(".REQUIREDSCRIPTS {0}", String.Join(" ", RequiredScripts)));
            psScriptInfoLines.Add(String.Format(".EXTERNALSCRIPTDEPENDENCIES {0}", String.Join(" ", ExternalScriptDependencies)));
            psScriptInfoLines.Add(String.Format(".RELEASENOTES\n{0}", ReleaseNotes));
            psScriptInfoLines.Add(String.Format(".PRIVATEDATA\n{0}", PrivateData));
            psScriptInfoLines.Add("#>");

            return String.Join("\n\n", psScriptInfoLines);
        }


        internal bool UpdateContent(
            string version,
            Guid guid,
            string author,
            string companyName,
            string copyright,
            string[] tags,
            Uri licenseUri,
            Uri projectUri,
            Uri iconUri,
            string[] externalModuleDependencies,
            string[] requiredScripts,
            string[] externalScriptDependencies,
            string releaseNotes,
            string privateData,
            out ErrorRecord error)
        {
            error = null;
            if (!String.IsNullOrEmpty(version))
            {
                if (!NuGetVersion.TryParse(version, out NuGetVersion updatedVersion))
                {
                    var message = String.Format("Version provided for update could not be parsed successfully into NuGetVersion");
                    var ex = new ArgumentException(message);
                    var versionParseIntoNuGetVersionError = new ErrorRecord(ex, "VersionParseIntoNuGetVersion", ErrorCategory.ParserError, null);
                    error = versionParseIntoNuGetVersionError;
                    return false;
                }

                Version = updatedVersion;
            }

            if (guid != Guid.Empty)
            {
                Guid = guid;
            }

            if (!String.IsNullOrEmpty(author))
            {
                Author = author;
            }

            if (!String.IsNullOrEmpty(companyName)){
                CompanyName = companyName;
            }

            if (!String.IsNullOrEmpty(copyright)){
                Copyright = copyright;
            }

            if (tags != null && tags.Length != 0){
                Tags = tags;
            }

            if (licenseUri != null && !licenseUri.Equals(default(Uri))){
                LicenseUri = licenseUri;
            }

            if (projectUri != null && !projectUri.Equals(default(Uri))){
                ProjectUri = projectUri;
            }

            if (iconUri != null && !iconUri.Equals(default(Uri))){
                IconUri = iconUri;
            }

            if (externalModuleDependencies != null && externalModuleDependencies.Length != 0){
                ExternalModuleDependencies = externalModuleDependencies;                
            }

            if (requiredScripts != null && requiredScripts.Length != 0)
            {
                RequiredScripts = requiredScripts;
            }

            if (externalScriptDependencies != null && externalScriptDependencies.Length != 0){
                ExternalScriptDependencies = externalScriptDependencies;                
            }

            if (!String.IsNullOrEmpty(releaseNotes))
            {
                ReleaseNotes = releaseNotes;
            }

            if (!String.IsNullOrEmpty(privateData))
            {
                PrivateData = privateData;
            }

            return true;
        }

        #endregion
    }
}