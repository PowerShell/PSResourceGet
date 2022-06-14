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
    /// This class contains information for a PSScriptFileInfo (representing a .ps1 file contents)
    /// </summary>
    public sealed class PSScriptFileInfo
    {        
        #region Members

        private string[] fileContents = new string[]{};

        #endregion

        #region Properties

        /// <summary>
        /// the Version of the script
        /// </summary>
        public NuGetVersion Version { get; set; }

        /// <summary>
        /// the GUID for the script
        /// </summary>
        public Guid Guid { get; set; }

        /// <summary>
        /// the author for the script
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// the name of the company owning the script
        /// </summary>
        public string CompanyName { get; set; }

        /// <summary>
        /// the copyright information for the script
        /// </summary>
        public string Copyright { get; set; }

        /// <summary>
        /// the tags for the script
        /// </summary>
        public string[] Tags { get; set; }

        /// <summary>
        /// the Uri for the license of the script
        /// </summary>
        public Uri LicenseUri { get; set; }

        /// <summary>
        /// the Uri for the project relating to the script
        /// </summary>
        public Uri ProjectUri { get; set; }

        /// <summary>
        /// the Uri for the icon relating to the script
        /// </summary>
        public Uri IconUri { get; set; }

        /// <summary>
        /// The list of modules required by the script
        /// Hashtable keys: GUID, MaxVersion, ModuleName (Required), RequiredVersion, Version
        /// </summary>
        public ModuleSpecification[] RequiredModules { get; set; } = new ModuleSpecification[]{};

        /// <summary>
        /// the list of external module dependencies for the script
        /// </summary>
        public string[] ExternalModuleDependencies { get; set; } = new string[]{};

        /// <summary>
        /// the list of required scripts for the parent script
        /// </summary>
        public string[] RequiredScripts { get; set; } = new string[]{};

        /// <summary>
        /// the list of external script dependencies for the script
        /// </summary>
        public string[] ExternalScriptDependencies { get; set; } = new string[]{};

        /// <summary>
        /// the release notes relating to the script
        /// </summary>
        public string[] ReleaseNotes { get; set; } = new string[]{};

        /// <summary>
        /// The private data associated with the script
        /// </summary>
        public string PrivateData { get; set; }

        /// <summary>
        /// The description of the script
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// End of file contents for the .ps1 file
        /// </summary>
        public string[] EndOfFileContents { get; set; } = new string[]{};        

        /// <summary>
        /// The synopsis of the script
        /// </summary>
        public string Synopsis { get; set; }

        /// <summary>
        /// The example(s) relating to the script's usage
        /// </summary>
        public string[] Example { get; set; } = new string[]{};

        /// <summary>
        /// The inputs to the script
        /// </summary>
        public string[] Inputs { get; set; } = new string[]{};

        /// <summary>
        /// The outputs to the script
        /// </summary>
        public string[] Outputs { get; set; } = new string[]{};

        /// <summary>
        /// The notes for the script
        /// </summary>
        public string[] Notes { get; set; } = new string[]{};

        /// <summary>
        /// The links for the script
        /// </summary>
        public string[] Links { get; set; } = new string[]{};

        /// <summary>
        /// TODO: what is this?
        /// </summary>
        public string[] Component { get; set; } = new string[]{};

        /// <summary>
        /// TODO: what is this?
        /// </summary>
        public string[] Role { get; set; } = new string[]{};

        /// <summary>
        /// TODO: what is this?
        /// </summary>
        public string[] Functionality { get; set; } = new string[]{};

        #endregion

        #region Constructor

        private PSScriptFileInfo() {}
        public PSScriptFileInfo(
            string version,
            Guid guid,
            string author,
            string companyName,
            string copyright,
            string[] tags,
            Uri licenseUri,
            Uri projectUri,
            Uri iconUri,
            ModuleSpecification[] requiredModules,
            string[] externalModuleDependencies,
            string[] requiredScripts,
            string[] externalScriptDependencies,
            string[] releaseNotes,
            string privateData,
            string description,
            string[] endOfFileContents
        )
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
            RequiredModules = requiredModules ?? new ModuleSpecification[]{};
            ExternalModuleDependencies = externalModuleDependencies ?? Utils.EmptyStrArray;
            RequiredScripts = requiredScripts ?? Utils.EmptyStrArray;
            ExternalScriptDependencies = externalScriptDependencies ?? Utils.EmptyStrArray;
            ReleaseNotes = releaseNotes ?? Utils.EmptyStrArray;
            PrivateData = privateData;
            Description = description;
            EndOfFileContents = endOfFileContents;
        }

        #endregion
        
        #region Internal Static Methods

        // one method that takes .ps1 file and creates hashtable
        /// <summary>
        /// Parses content of .ps1 file into a hashtable
        /// </summary>
        internal static bool TryParseScript(
            string scriptFileInfoPath,
            out Hashtable parsedScriptMetadata,
            out string[] endOfFileContents,
            out ErrorRecord[] errors
        )
        {
            errors = new ErrorRecord[]{};
            parsedScriptMetadata = new Hashtable();
            endOfFileContents = new string[]{};
            List<ErrorRecord> errorsList = new List<ErrorRecord>();

            // Parse the script file
            var ast = Parser.ParseFile(
                scriptFileInfoPath,
                out Token[] tokens,
                out ParseError[] parserErrors);
            
            if (parserErrors.Length > 0)
            {
                bool parseSuccessful = true;
                foreach (ParseError err in parserErrors)
                {
                    // we ignore WorkFlowNotSupportedInPowerShellCore errors, as this is common in scripts currently on PSGallery
                    if (!String.Equals(err.ErrorId, "WorkflowNotSupportedInPowerShellCore", StringComparison.OrdinalIgnoreCase))
                    {
                        var message = String.Format("Could not parse '{0}' as a PowerShell script file due to {1}.", scriptFileInfoPath, err.Message);
                        var ex = new InvalidOperationException(message);
                        var psScriptFileParseError = new ErrorRecord(ex, err.ErrorId, ErrorCategory.ParserError, null);
                        errorsList.Add(psScriptFileParseError);
                        parseSuccessful = false;
                    }
                }

                if (!parseSuccessful)
                {
                    errors = errorsList.ToArray();
                    return parseSuccessful;
                }
            }

            if (ast == null)
            {
                var parseFileException = new InvalidOperationException(
                    message: "Cannot parse .ps1 file", innerException: new ParseException(
                        message: "Parsed AST was null for .ps1 file"));
                var astCouldNotBeCreatedError = new ErrorRecord(parseFileException, "ASTCouldNotBeCreated", ErrorCategory.ParserError, null);

                errorsList.Add(astCouldNotBeCreatedError);
                errors = errorsList.ToArray();
                return false;
            }

            // Get .DESCRIPTION property (required property), by accessing the Help block which contains .DESCRIPTION
            CommentHelpInfo scriptCommentInfo = ast.GetHelpContent();
            if (scriptCommentInfo == null)
            {
                var message = String.Format("PSScript file is missing the required Description comment block in the script contents.");
                var ex = new ArgumentException(message);
                var psScriptMissingHelpContentCommentBlockError = new ErrorRecord(ex, "PSScriptMissingHelpContentCommentBlock", ErrorCategory.ParserError, null);
                errorsList.Add(psScriptMissingHelpContentCommentBlockError);
                errors = errorsList.ToArray();
                return false;
            }

            if (!String.IsNullOrEmpty(scriptCommentInfo.Description) && !scriptCommentInfo.Description.Contains("<#") && !scriptCommentInfo.Description.Contains("#>"))
            {
                parsedScriptMetadata.Add("DESCRIPTION", scriptCommentInfo.Description);
            }
            else
            {
                var message = String.Format("PSScript is missing the required Description property or Description value contains '<#' or '#>' which is invalid");
                var ex = new ArgumentException(message);
                var psScriptMissingDescriptionOrInvalidPropertyError = new ErrorRecord(ex, "psScriptDescriptionMissingOrInvalidDescription", ErrorCategory.ParserError, null);
                errorsList.Add(psScriptMissingDescriptionOrInvalidPropertyError);
                errors = errorsList.ToArray();
                return false;
            }

            // get .REQUIREDMODULES property, by accessing the ScriptBlockAst.ScriptRequirements.RequiredModules
            // ex of ScriptRequirements
            // ex of RequiredModules
            // TODO: (Anam) in comment include ex of ScriptRequirements and RequiredModules (above)
            ScriptRequirements parsedScriptRequirements = ast.ScriptRequirements;
            ReadOnlyCollection<ModuleSpecification> parsedModules = new List<ModuleSpecification>().AsReadOnly();

            if (parsedScriptRequirements != null && parsedScriptRequirements.RequiredModules != null)
            {
                parsedModules = parsedScriptRequirements.RequiredModules;
                parsedScriptMetadata.Add("REQUIREDMODULES", parsedModules);
            }

            // Get the block/group comment beginning with <#PSScriptInfo
            // this contains other script properties, including AUTHOR, VERSION, GUID (which are required properties)

            List<Token> commentTokens = tokens.Where(a => a.Kind == TokenKind.Comment).ToList();
            string commentPattern = "<#PSScriptInfo";
            Regex rg = new Regex(commentPattern);

            Token psScriptInfoCommentToken = commentTokens.Where(a => rg.IsMatch(a.Extent.Text)).FirstOrDefault();
            if (psScriptInfoCommentToken == null)
            {
                var message = String.Format("PSScriptInfo comment was missing or could not be parsed");
                var ex = new ArgumentException(message);
                var psCommentMissingError = new ErrorRecord(ex, "psScriptInfoCommentMissingError", ErrorCategory.ParserError, null);
                errorsList.Add(psCommentMissingError);

                errors = errorsList.ToArray();
                return false;  
            }

            // TODO: add ^
            // that would get each line that matches <#PSScriptInfo at the start, which is only line 1
            /**
            <#PSScriptInfo

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


            .PRIVATEDATA

            #>
            */

            string[] newlineDelimeters = new string[]{"\r\n", "\n"}; // TODO should I also have \n?
            string[] commentLines = psScriptInfoCommentToken.Text.Split(newlineDelimeters, StringSplitOptions.RemoveEmptyEntries);
            string keyName = String.Empty;
            string value = String.Empty;

            /**
            If comment line count is not more than two, it doesn't have the any metadata property
            comment block would look like:

            <#PSScriptInfo
            #>

            */

            if (commentLines.Count() <= 2)
            {
                var message = String.Format("PSScriptInfo comment block is empty and did not contain any metadata");
                var ex = new ArgumentException(message);
                var psScriptInfoCommentEmptyError = new ErrorRecord(ex, "psScriptInfoCommentEmpty", ErrorCategory.ParserError, null);
                errorsList.Add(psScriptInfoCommentEmptyError);

                errors = errorsList.ToArray();
                return false;  
            }

            for (int i = 1; i < commentLines.Count(); i++)
            {
                string line = commentLines[i];

                // A line is starting with . conveys a new metadata property
                if (line.Trim().StartsWith("."))
                {
                    // .KEY VALUE
                    string[] parts = line.Trim().TrimStart('.').Split();
                    keyName = parts[0];
                    value = parts.Count() > 1 ? String.Join(" ", parts.Skip(1)) : String.Empty;
                    parsedScriptMetadata.Add(keyName, value);
                }
            }

            // get end of file contents
            // TODO: (Anam) when updating write warning to user that the signature will be invalidated, needs to be regenerated
            string[] totalFileContents = File.ReadAllLines(scriptFileInfoPath);
            var contentAfterAndIncludingDescription = totalFileContents.SkipWhile(x => !x.Contains(".DESCRIPTION")).ToList();
            var contentAfterDescription = contentAfterAndIncludingDescription.SkipWhile(x => !x.Contains("#>")).Skip(1).ToList();
            if (contentAfterDescription.Count() > 0)
            {
                endOfFileContents = contentAfterDescription.ToArray();
            }

            return true;
        }

        /// <summary>
        /// Takes hashtable (containing parsed .ps1 file content properties) and validates required properties are present
        /// </summary>
        internal static bool TryValidateScript(
            Hashtable parsedScriptMetadata,
            out ErrorRecord[] errors
        )
        {
            // TODO: retuan all errors at once, wait til end

            // required properties for script file (.ps1 file) are: Author, Version, Guid, Description
            // Description gets validated in TryParseScript() when getting the property
            // TODO: I think hashtable keys should be lower cased

            List<ErrorRecord> errorsList = new List<ErrorRecord>();

            if (!parsedScriptMetadata.ContainsKey("VERSION") || String.IsNullOrEmpty((string) parsedScriptMetadata["VERSION"]))
            {
                var message = String.Format("PSScript file is missing the required Version property");
                var ex = new ArgumentException(message);
                var psScriptMissingVersionError = new ErrorRecord(ex, "psScriptMissingVersion", ErrorCategory.ParserError, null);
                errorsList.Add(psScriptMissingVersionError);
                errors = errorsList.ToArray();
                return false;
            }

            if (!parsedScriptMetadata.ContainsKey("AUTHOR") || String.IsNullOrEmpty((string) parsedScriptMetadata["AUTHOR"]))
            {
                var message = String.Format("PSScript file is missing the required Author property");
                var ex = new ArgumentException(message);
                var psScriptMissingAuthorError = new ErrorRecord(ex, "psScriptMissingAuthor", ErrorCategory.ParserError, null);
                errorsList.Add(psScriptMissingAuthorError);
                errors = errorsList.ToArray();
                return false;;
            }

            if (!parsedScriptMetadata.ContainsKey("GUID") || String.IsNullOrEmpty((string) parsedScriptMetadata["GUID"]))
            {
                var message = String.Format("PSScript file is missing the required Guid property");
                var ex = new ArgumentException(message);
                var psScriptMissingGuidError = new ErrorRecord(ex, "psScriptMissingGuid", ErrorCategory.ParserError, null);
                errorsList.Add(psScriptMissingGuidError);
                errors = errorsList.ToArray();
                return false;
            }

            errors = errorsList.ToArray();
            return true;
        }

        /// <summary>
        /// Tests the contents of the .ps1 file at the provided path
        /// </summary>
        internal static bool TryParseScriptIntoPSScriptInfo(
            string scriptFileInfoPath,
            out PSScriptFileInfo parsedScript,
            out ErrorRecord[] errors,
            out string[] verboseMsgs)
        {
            parsedScript = null;
            errors = new ErrorRecord[]{};
            verboseMsgs = new string[]{};
            List<ErrorRecord> errorsList = new List<ErrorRecord>();
            List<string> verboseMsgsList = new List<string>();

            if (!scriptFileInfoPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                var message = String.Format("File passed in: {0} does not have a .ps1 extension as required", scriptFileInfoPath);
                var ex = new ArgumentException(message);
                var psScriptFileParseError = new ErrorRecord(ex, "ps1FileRequiredError", ErrorCategory.ParserError, null);
                errorsList.Add(psScriptFileParseError);
                errors = errorsList.ToArray();
                return false;
            }

            if (!TryParseScript(
                scriptFileInfoPath: scriptFileInfoPath,
                parsedScriptMetadata: out Hashtable parsedScriptMetadata,
                endOfFileContents: out string[] endofFileContents,
                errors: out ErrorRecord[] parseErrors
            ))
            {
                errorsList.AddRange(parseErrors);
                errors = errorsList.ToArray();
                return false;
            }

            // at this point we've parsed into hashtable, now validate hashtable has properties we need:
            // Author, Version, Guid, Description (but Description is already validated)

            if (!TryValidateScript(
                parsedScriptMetadata: parsedScriptMetadata,
                errors: out ErrorRecord[] validationErrors))
            {
                errorsList.AddRange(validationErrors);
                errors = errorsList.ToArray();
                return false;
            }

            // at this point, we've parsed into hashtable AND validated we have all required properties for .ps1
            // now create instance of and populate PSScriptFileInfo
            try
            {
                char[] spaceDelimeter = new char[]{' '};

                Guid parsedGuid = new Guid((string) parsedScriptMetadata["GUID"]);
                string parsedVersion = (string) parsedScriptMetadata["VERSION"];
                string parsedAuthor = (string) parsedScriptMetadata["AUTHOR"];
                string parsedDescription = (string) parsedScriptMetadata["DESCRIPTION"];


                string parsedCompanyName = (string) parsedScriptMetadata["COMPANYNAME"] ?? String.Empty;
                string parsedCopyright = (string) parsedScriptMetadata["COPYRIGHT"] ?? String.Empty;
                string parsedPrivateData = (string) parsedScriptMetadata["PRIVATEDATA"] ?? String.Empty; // TODO: fix PrivateData bug? or already fixed?

                string[] parsedTags = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedScriptMetadata["TAGS"]);
                string[] parsedExternalModuleDependencies = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedScriptMetadata["EXTERNALMODULEDEPENDENCIES"]);
                string[] parsedRequiredScripts = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedScriptMetadata["REQUIREDSCRIPTS"]);
                string[] parsedExternalScriptDependencies = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedScriptMetadata["EXTERNALSCRIPTDEPENDENCIES"]);
                string[] parsedReleaseNotes = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedScriptMetadata["RELEASENOTES"]);

                ReadOnlyCollection<ModuleSpecification> parsedModules = (ReadOnlyCollection<ModuleSpecification>) parsedScriptMetadata["REQUIREDMODULES"] ??
                    new ReadOnlyCollection<ModuleSpecification>(new List<ModuleSpecification>());

                ModuleSpecification[] parsedModulesArray = parsedModules.Count() == 0 ? new ModuleSpecification[]{} : parsedModules.ToArray();

                Uri parsedLicenseUri = null;
                if (!String.IsNullOrEmpty((string) parsedScriptMetadata["LICENSEURI"]))
                {
                    if (!Uri.TryCreate((string) parsedScriptMetadata["LICENSEURI"], UriKind.Absolute, out parsedLicenseUri))
                    {
                        verboseMsgsList.Add("LicenseUri property could not be created as a Uri");   
                    }
                }

                Uri parsedProjectUri = null;
                if (!String.IsNullOrEmpty((string) parsedScriptMetadata["PROJECTURI"]))
                {
                    if (!Uri.TryCreate((string) parsedScriptMetadata["PROJECTURI"], UriKind.Absolute, out parsedProjectUri))
                    {
                        verboseMsgsList.Add("ProjectUri property could not be created as Uri");
                    }
                }

                Uri parsedIconUri = null;
                if (!String.IsNullOrEmpty((string) parsedScriptMetadata["ICONURI"]))
                {
                    if (!Uri.TryCreate((string) parsedScriptMetadata["ICONURI"], UriKind.Absolute, out parsedIconUri))
                    {
                        verboseMsgsList.Add("IconUri property could not be created as Uri");
                    }
                }

                // parsedScriptMetadata should contain all keys, but values may be empty (i.e empty array, String.empty)
                parsedScript = new PSScriptFileInfo(
                    version: parsedVersion,
                    guid: parsedGuid,
                    author: parsedAuthor,
                    companyName: parsedCompanyName,
                    copyright: parsedCopyright,
                    tags: parsedTags,
                    licenseUri: parsedLicenseUri,
                    projectUri: parsedProjectUri,
                    iconUri: parsedIconUri,
                    requiredModules: parsedModulesArray,
                    externalModuleDependencies: parsedExternalModuleDependencies,
                    requiredScripts: parsedRequiredScripts,
                    externalScriptDependencies: parsedExternalScriptDependencies,
                    releaseNotes: parsedReleaseNotes,
                    privateData: parsedPrivateData,
                    description: parsedDescription,
                    endOfFileContents: endofFileContents);
            }
            catch (Exception e)
            {
                var message = String.Format("PSScriptFileInfo object could not be created from passed in file due to {0}", e.Message);
                var ex = new ArgumentException(message);
                var PSScriptFileInfoObjectNotCreatedFromFileError = new ErrorRecord(ex, "PSScriptFileInfoObjectNotCreatedFromFile", ErrorCategory.ParserError, null);
                errorsList.Add(PSScriptFileInfoObjectNotCreatedFromFileError);
                errors = errorsList.ToArray();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Updates the contents of the .ps1 file at the provided path with the properties provided
        /// and writes new updated script file contents to a string and updates the original PSScriptFileInfo object
        /// </summary>        
        internal static bool TryUpdateScriptFileContents(
            PSScriptFileInfo scriptInfo,
            out string updatedPSScriptFileContents,
            out ErrorRecord[] errors,
            string version,
            Guid guid,
            string author,
            string companyName,
            string copyright,
            string[] tags,
            Uri licenseUri,
            Uri projectUri,
            Uri iconUri,
            ModuleSpecification[] requiredModules,
            string[] externalModuleDependencies,
            string[] requiredScripts,
            string[] externalScriptDependencies,
            string[] releaseNotes,
            string privateData,
            string description)
        {
            updatedPSScriptFileContents = String.Empty;
            errors = new ErrorRecord[]{};
            List<ErrorRecord> errorsList = new List<ErrorRecord>();

            if (scriptInfo == null)
            {
                throw new ArgumentNullException(nameof(scriptInfo));
            }
            
            // create new PSScriptFileInfo with updated fields
            if (!String.IsNullOrEmpty(version))
            {
                if (!NuGetVersion.TryParse(version, out NuGetVersion updatedVersion))
                {
                    var message = String.Format("Version provided for update could not be parsed successfully into NuGetVersion");
                    var ex = new ArgumentException(message);
                    var versionParseIntoNuGetVersionError = new ErrorRecord(ex, "VersionParseIntoNuGetVersion", ErrorCategory.ParserError, null);
                    errorsList.Add(versionParseIntoNuGetVersionError);  
                    errors = errorsList.ToArray();
                    return false;
                }
                scriptInfo.Version = updatedVersion;
            }

            if (guid != Guid.Empty)
            {
                scriptInfo.Guid = guid;
            }

            if (!String.IsNullOrEmpty(author))
            {
                scriptInfo.Author = author;
            }

            if (!String.IsNullOrEmpty(companyName)){
                scriptInfo.CompanyName = companyName;
            }

            if (!String.IsNullOrEmpty(copyright)){
                scriptInfo.Copyright = copyright;
            }

            if (tags != null && tags.Length != 0){
                scriptInfo.Tags = tags;
            }

            if (licenseUri != null && !licenseUri.Equals(default(Uri))){
                scriptInfo.LicenseUri = licenseUri;
            }

            if (projectUri != null && !projectUri.Equals(default(Uri))){
                scriptInfo.ProjectUri = projectUri;
            }

            if (iconUri != null && !iconUri.Equals(default(Uri))){
                scriptInfo.IconUri = iconUri;
            }

            if (requiredModules != null && requiredModules.Length != 0){
                scriptInfo.RequiredModules = requiredModules;
            }

            if (externalModuleDependencies != null && externalModuleDependencies.Length != 0){
                scriptInfo.ExternalModuleDependencies = externalModuleDependencies;                
            }

            if (requiredScripts != null && requiredScripts.Length != 0)
            {
                scriptInfo.RequiredScripts = requiredScripts;
            }

            if (externalScriptDependencies != null && externalScriptDependencies.Length != 0){
                scriptInfo.ExternalScriptDependencies = externalScriptDependencies;                
            }

            if (releaseNotes != null && releaseNotes.Length != 0)
            {
                scriptInfo.ReleaseNotes = releaseNotes;
            }

            if (!String.IsNullOrEmpty(privateData))
            {
                scriptInfo.PrivateData = privateData;
            }

            if (!String.IsNullOrEmpty(description))
            {
                scriptInfo.Description = description;
            }

            // create string contents for .ps1 file
            if (!scriptInfo.TryCreateScriptFileInfoString(
                pSScriptFileString: out string psScriptFileContents,
                errors: out ErrorRecord[] createFileContentErrors))
            {
                errorsList.AddRange(createFileContentErrors);
                errors = errorsList.ToArray();
                return false;
            }

            // TODO: move into TryCreateScriptFileInfoString, if it exists then add, if not you dont
            if (scriptInfo.EndOfFileContents.Length > 0)
            {
                psScriptFileContents += "\n" + String.Join("\n", scriptInfo.EndOfFileContents);
            }

            updatedPSScriptFileContents = psScriptFileContents;
            return true;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Create .ps1 file contents as a string from PSScriptFileInfo object's properties
        /// end of file contents are not yet added to the string contents of the file
        /// </summary>
        public bool TryCreateScriptFileInfoString(
            // string filePath,
            out string pSScriptFileString, // this is the string with the contents we want to put in the new ps1 file
            out ErrorRecord[] errors
        )
        {
            errors = new ErrorRecord[]{};
            List<ErrorRecord> errorsList = new List<ErrorRecord>();

            pSScriptFileString = String.Empty;
            bool fileContentsSuccessfullyCreated = false;

            // this string/block is required
            // this can only have one error (i.e Author or Version is missing)
            if (!GetPSScriptInfoString(
                pSScriptInfoString: out string psScriptInfoCommentString,
                out ErrorRecord scriptInfoError))
            {
                if (scriptInfoError != null)
                {
                    errorsList.Add(scriptInfoError);
                    errors = errorsList.ToArray();
                }

                return fileContentsSuccessfullyCreated;
            }

            pSScriptFileString = psScriptInfoCommentString;

            // populating this block is not required to fulfill .ps1 script requirements.
            // this won't report any errors.
            GetRequiresString(psRequiresString: out string psRequiresString);
            if (!String.IsNullOrEmpty(psRequiresString))
            {
                pSScriptFileString += "\n";
                pSScriptFileString += psRequiresString;
            }

            // this string/block will contain Description, which is required
            // this can only have one error (i.e Description is missing)
            if (!GetScriptCommentHelpInfo(
                psHelpInfo: out string psHelpInfo,
                error: out ErrorRecord commentHelpInfoError))
            {
                if (commentHelpInfoError != null)
                {
                    errorsList.Add(commentHelpInfoError);
                    errors = errorsList.ToArray();
                }

                pSScriptFileString = String.Empty;
                return fileContentsSuccessfullyCreated;
            }

            pSScriptFileString += "\n"; // need a newline after last #> and before <# for script comment block
            // or else not recongnized as a valid comment help info block when parsing the created ps1 later
            pSScriptFileString += "\n" + psHelpInfo;

            fileContentsSuccessfullyCreated = true;
            return fileContentsSuccessfullyCreated;
        }

        #endregion

        #region Private Methods
        /// <summary>
        /// Used when creating .ps1 file's contents.
        /// This creates the <#PSScriptInfo ... #> comment string
        /// </summary>
        private bool GetPSScriptInfoString(
            out string pSScriptInfoString,
            out ErrorRecord error
        )
        {
            error = null;
            bool pSScriptInfoSuccessfullyCreated = false;
            pSScriptInfoString = String.Empty;

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

            if (String.IsNullOrEmpty(Author) || Version == null)
            {
                var exMessage = "PSScriptInfo must contain values for Author and Version. Ensure both of these are present.";
                var ex = new ArgumentException(exMessage);
                var PSScriptInfoMissingAuthorOrVersionError = new ErrorRecord(ex, "PSScriptInfoMissingAuthorOrVersion", ErrorCategory.InvalidArgument, null);
                error = PSScriptInfoMissingAuthorOrVersionError;
                return pSScriptInfoSuccessfullyCreated;
            }

            pSScriptInfoSuccessfullyCreated = true;
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
            psScriptInfoLines.Add(String.Format(".RELEASENOTES\n{0}", String.Join("\n", ReleaseNotes)));
            psScriptInfoLines.Add(String.Format(".PRIVATEDATA\n{0}", PrivateData));
            psScriptInfoLines.Add("#>");

            pSScriptInfoString = String.Join("\n\n", psScriptInfoLines);
            return pSScriptInfoSuccessfullyCreated;
        }

        /// <summary>
        /// Used when creating .ps1 file's contents.
        /// This creates the #Requires comment string
        /// </summary>
        private void GetRequiresString(
            out string psRequiresString
        )
        {
            psRequiresString = String.Empty;

            if (RequiredModules.Length > 0)
            {
                List<string> psRequiresLines = new List<string>();
                psRequiresLines.Add("\n");
                foreach (ModuleSpecification moduleSpec in RequiredModules)
                {
                    psRequiresLines.Add(String.Format("#Requires -Module {0}", moduleSpec.ToString()));
                }

                psRequiresLines.Add("\n");
                psRequiresString = String.Join("\n", psRequiresLines);
                // TODO: Does the GUID come in for ModuleSpecification(string) constructed object's ToString() output?
            }
        }

        /// <summary>
        /// Used when creating .ps1 file's contents.
        /// This creates the help comment string: <# \n .DESCRIPTION #>
        /// </summary>
        private bool GetScriptCommentHelpInfo(
            out string psHelpInfo,
            out ErrorRecord error
        )
        {
            error = null;
            psHelpInfo = String.Empty;
            bool psHelpInfoSuccessfullyCreated = false;
            List<string> psHelpInfoLines = new List<string>();

            if (String.IsNullOrEmpty(Description))
            {
                var exMessage = "PSScript file must contain value for Description. Ensure value for Description is passed in and try again.";
                var ex = new ArgumentException(exMessage);
                var PSScriptInfoMissingDescriptionError = new ErrorRecord(ex, "PSScriptInfoMissingDescription", ErrorCategory.InvalidArgument, null);
                error = PSScriptInfoMissingDescriptionError;
                return psHelpInfoSuccessfullyCreated;
            }

            if (StringContainsComment(Description))
            {
                var exMessage = "PSScript file's value for Description cannot contain '<#' or '#>'. Pass in a valid value for Description and try again.";
                var ex = new ArgumentException(exMessage);
                var DescriptionContainsCommentError = new ErrorRecord(ex, "DescriptionContainsComment", ErrorCategory.InvalidArgument, null);
                error = DescriptionContainsCommentError;
                return psHelpInfoSuccessfullyCreated; 
            }

            psHelpInfoSuccessfullyCreated = true;
            psHelpInfoLines.Add("<#\n");
            psHelpInfoLines.Add(String.Format(".DESCRIPTION\n{0}", Description));

            if (!String.IsNullOrEmpty(Synopsis))
            {
                psHelpInfoLines.Add(String.Format(".SYNOPSIS\n{0}", Synopsis));
            }

            foreach (string currentExample in Example)
            {
                psHelpInfoLines.Add(String.Format(".EXAMPLE\n{0}", currentExample));
            }

            foreach (string input in Inputs)
            {
                psHelpInfoLines.Add(String.Format(".INPUTS\n{0}", input));
            }

            foreach (string output in Outputs)
            {
                psHelpInfoLines.Add(String.Format(".OUTPUTS\n{0}", output));
            }

            if (Notes.Length > 0)
            {
                psHelpInfoLines.Add(String.Format(".NOTES\n{0}", String.Join("\n", Notes)));
            }

            foreach (string link in Links)
            {
                psHelpInfoLines.Add(String.Format(".LINK\n{0}", link));
            }

            if (Component.Length > 0)
            {
                psHelpInfoLines.Add(String.Format(".COMPONENT\n{0}", String.Join("\n", Component)));
            }
            
            if (Role.Length > 0)
            {
                psHelpInfoLines.Add(String.Format(".ROLE\n{0}", String.Join("\n", Role)));
            }
            
            if (Functionality.Length > 0)
            {
                psHelpInfoLines.Add(String.Format(".FUNCTIONALITY\n{0}", String.Join("\n", Functionality)));
            }

            psHelpInfoLines.Add("#>");
            psHelpInfo = String.Join("\n", psHelpInfoLines);
            return psHelpInfoSuccessfullyCreated;
        }

        /// <summary>
        /// Ensure no fields (passed as stringToValidate) contains '<#' or '#>' (would break comment section)
        /// </summary>
        private bool StringContainsComment(string stringToValidate)
        {
            return stringToValidate.Contains("<#") || stringToValidate.Contains("#>");
        }

        #endregion
        
    }
}