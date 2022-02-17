using System.Runtime.Serialization;
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

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    /// <summary>
    /// This class contains information for a repository item.
    /// </summary>
    public sealed class PSScriptFileInfo
    {        
        #region Properties

        /// <summary>
        /// the Version of the script
        /// </summary>
        public Version Version { get; set; }

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
        /// Hashtable keys: GUID, MaxVersion, Name (Required), RequiredVersion, Version
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
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
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string PrivateData { get; set; }

        /// <summary>
        /// The description of the script
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string Description { get; set; }

        // TODO: seem to be optional keys for help comment, where would they be passed in from? constructor/New-X cmdlet?
        /// <summary>
        /// The synopsis of the script
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string Synopsis { get; set; }

        /// <summary>
        /// The example(s) relating to the script's usage
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string[] Example { get; set; } = new string[]{};

        /// <summary>
        /// The inputs to the script
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string[] Inputs { get; set; } = new string[]{};

        /// <summary>
        /// The outputs to the script
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string[] Outputs { get; set; } = new string[]{};

        /// <summary>
        /// The notes for the script
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string[] Notes { get; set; } = new string[]{};

        /// <summary>
        /// The links for the script
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string[] Links { get; set; } = new string[]{};

        /// <summary>
        /// TODO: what is this?
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string[] Component { get; set; } = new string[]{};

        /// <summary>
        /// TODO: what is this?
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string[] Role { get; set; } = new string[]{};

        /// <summary>
        /// TODO: what is this?
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
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
            string description
        )
        {
            if (String.IsNullOrEmpty(author))
            {
                author = Environment.UserName;
            }

            Version = !String.IsNullOrEmpty(version) ? new Version (version) : new Version("1.0.0.0");
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
        }

        #endregion

        #region Public Static Methods

        /// <summary>
        /// Tests the contents of the .ps1 file at the provided path
        /// </summary>
        public static bool TryParseScriptFileInfo(
            string scriptFileInfoPath,
            out PSScriptFileInfo parsedScript,
            out ErrorRecord[] errors)
        {
            parsedScript = null;
            errors = new ErrorRecord[]{};
            List<ErrorRecord> errorsList = new List<ErrorRecord>();
            bool successfullyParsed = false;

            if (scriptFileInfoPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                // Parse the script file
                var ast = Parser.ParseFile(
                    scriptFileInfoPath,
                    out Token[] tokens,
                    out ParseError[] parserErrors);

                if (parserErrors.Length > 0 && !String.Equals(parserErrors[0].ErrorId, "WorkflowNotSupportedInPowerShellCore", StringComparison.OrdinalIgnoreCase))
                {
                    // TODO: do we want to completely ignore WorkFlowNotSupportedInPowerShellCore error (not even write)
                    // or do we want to write, but not return if it's just that error?
                    foreach (ParseError err in parserErrors)
                    {
                        var message = String.Format("Could not parse '{0}' as a PowerShell script file due to {1}.", scriptFileInfoPath, err.Message);
                        var ex = new ArgumentException(message);
                        var psScriptFileParseError = new ErrorRecord(ex, err.ErrorId, ErrorCategory.ParserError, null);
                        errorsList.Add(psScriptFileParseError);  
                    }

                    return successfullyParsed;
                }
                else if (ast != null)
                {
                    // Get the block/group comment beginning with <#PSScriptInfo
                    Hashtable parsedPSScriptInfoHashtable = new Hashtable();
                    List<Token> commentTokens = tokens.Where(a => String.Equals(a.Kind.ToString(), "Comment", StringComparison.OrdinalIgnoreCase)).ToList();
                    string commentPattern = "<#PSScriptInfo";
                    Regex rg = new Regex(commentPattern);
                    List<Token> psScriptInfoCommentTokens = commentTokens.Where(a => rg.IsMatch(a.Extent.Text)).ToList();

                    if (psScriptInfoCommentTokens.Count() == 0 || psScriptInfoCommentTokens[0] == null)
                    {
                        var message = String.Format("PSScriptInfo comment was missing or could not be parsed");
                        var ex = new ArgumentException(message);
                        var psCommentMissingError = new ErrorRecord(ex, "psScriptInfoCommentMissingError", ErrorCategory.ParserError, null);
                        errorsList.Add(psCommentMissingError);

                        errors = errorsList.ToArray();
                        return successfullyParsed;  
                    }

                    string[] commentLines = Regex.Split(psScriptInfoCommentTokens[0].Text, "[\r\n]");
                    string keyName = String.Empty;
                    string value = String.Empty;

                    /**
                    If comment line count is not more than two, it doesn't have the any metadata property
                    comment block would look like:

                    <#PSScriptInfo
                    #>
                    */

                    if (commentLines.Count() > 2)
                    {
                        // TODO: is it an error if the metadata property is empty?
                        for (int i = 1; i < commentLines.Count(); i++)
                        {
                            string line = commentLines[i];
                            if (String.IsNullOrEmpty(line))
                            {
                                continue;
                            }

                            // A line is starting with . conveys a new metadata property
                            if (line.Trim().StartsWith("."))
                            {
                                string[] parts = line.Trim().TrimStart('.').Split();
                                keyName = parts[0];
                                value = parts.Count() > 1 ? String.Join(" ", parts.Skip(1)) : String.Empty;
                                parsedPSScriptInfoHashtable.Add(keyName, value);
                            }
                        }

                        successfullyParsed = true;
                    }

                    // get .DESCRIPTION comment
                    CommentHelpInfo scriptCommentInfo = ast.GetHelpContent();
                    if (scriptCommentInfo != null)
                    {
                        if (!String.IsNullOrEmpty(scriptCommentInfo.Description))
                        {
                            parsedPSScriptInfoHashtable.Add("DESCRIPTION", scriptCommentInfo.Description);
                        }
                        else
                        {
                            var message = String.Format("PSScript is missing the required Description property");
                            var ex = new ArgumentException(message);
                            var psScriptMissingDescriptionPropertyError = new ErrorRecord(ex, "psScriptDescriptionMissingDescription", ErrorCategory.ParserError, null);
                            errorsList.Add(psScriptMissingDescriptionPropertyError);
                            successfullyParsed = false;
                            return successfullyParsed;
                        }
                    }

                    // get RequiredModules
                    ScriptRequirements parsedScriptRequirements = ast.ScriptRequirements;
                    ReadOnlyCollection<ModuleSpecification> parsedModules = new List<ModuleSpecification>().AsReadOnly();

                    if (parsedScriptRequirements != null && parsedScriptRequirements.RequiredModules != null)
                    {
                        // ReadOnlyCollection<Commands.ModuleSpecification> parsedRequiredModules = parsedScriptRequirements.RequiredModules;
                        parsedModules = parsedScriptRequirements.RequiredModules;
                        // TODO: Anam was there any importance for this?
                        // if (parsedPSScriptInfoHashtable.ContainsKey("RequiredModules"))
                        // {
                        //     parsedModules = (ReadOnlyCollection<ModuleSpecification>) parsedPSScriptInfoHashtable["RequiredModules"];
                        // }
                        parsedPSScriptInfoHashtable.Add("RequiredModules", parsedModules);
                    }

                    // get all defined functions and populate DefinedCommands, DefinedFunctions, DefinedWorkflow
                    // TODO: DefinedWorkflow is no longer supported as of PowerShellCore 6+, do we ignore error or reject script?
                    var parsedFunctionAst = ast.FindAll(a => a is FunctionDefinitionAst, true);
                    List<FunctionDefinitionAst> allCommands = new List<FunctionDefinitionAst>();
                    if (allCommands.Count() > 0)
                    {
                        foreach (var function in parsedFunctionAst)
                        {
                            allCommands.Add((FunctionDefinitionAst) function);
                        }

                        List<string> allCommandNames = allCommands.Select(a => a.Name).Distinct().ToList();
                        parsedPSScriptInfoHashtable.Add("DefinedCommands", allCommandNames);

                        // b.Body.Extent.Text to get actual content. So "Function b.Name c.Body.Extent.Text" is that whole line
                        List<string> allFunctionNames = allCommands.Where(a => !a.IsWorkflow).Select(b => b.Name).Distinct().ToList();
                        parsedPSScriptInfoHashtable.Add("DefinedFunctions", allFunctionNames);

                        List<string> allWorkflowNames = allCommands.Where(a => a.IsWorkflow).Select(b => b.Name).Distinct().ToList();
                        parsedPSScriptInfoHashtable.Add("DefinedWorkflows", allWorkflowNames);
                    }

                    string parsedVersion = (string) parsedPSScriptInfoHashtable["VERSION"];
                    string parsedAuthor = (string) parsedPSScriptInfoHashtable["AUTHOR"];
                    Guid parsedGuid = String.IsNullOrEmpty((string)parsedPSScriptInfoHashtable["GUID"]) ? Guid.NewGuid() : new Guid((string) parsedPSScriptInfoHashtable["GUID"]);
                    if (String.IsNullOrEmpty(parsedVersion) || String.IsNullOrEmpty(parsedAuthor) || parsedGuid == Guid.Empty)
                    {
                        var message = String.Format("PSScript file is missing one of the following required properties: Version, Author, Guid");
                        var ex = new ArgumentException(message);
                        var psScriptMissingRequiredPropertyError = new ErrorRecord(ex, "psScriptMissingRequiredProperty", ErrorCategory.ParserError, null);
                        errorsList.Add(psScriptMissingRequiredPropertyError);
                        successfullyParsed = false;
                        return successfullyParsed;
                    }

                    try
                    {
                        char[] spaceDelimeter = new char[]{' '};
                        string[] parsedTags = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedPSScriptInfoHashtable["TAGS"]);
                        string[] parsedExternalModuleDependencies = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedPSScriptInfoHashtable["EXTERNALMODULEDEPENDENCIES"]);
                        string[] parsedRequiredScripts = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedPSScriptInfoHashtable["REQUIREDSCRIPTS"]);
                        string[] parsedExternalScriptDependencies = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedPSScriptInfoHashtable["EXTERNALSCRIPTDEPENDENCIES"]);
                        string[] parsedReleaseNotes = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedPSScriptInfoHashtable["RELEASENOTES"]);
                        Uri parsedLicenseUri = null;
                        Uri parsedProjectUri = null;
                        Uri parsedIconUri = null;

                        if (!String.IsNullOrEmpty((string) parsedPSScriptInfoHashtable["LICENSEURI"]))
                        {
                            Uri.TryCreate((string) parsedPSScriptInfoHashtable["LICENSEURI"], UriKind.Absolute, out parsedLicenseUri);
                        }

                        if (!String.IsNullOrEmpty((string) parsedPSScriptInfoHashtable["PROJECTURI"]))
                        {
                            Uri.TryCreate((string) parsedPSScriptInfoHashtable["PROJECTURI"], UriKind.Absolute, out parsedProjectUri);
                        }

                        if (!String.IsNullOrEmpty((string) parsedPSScriptInfoHashtable["ICONURI"]))
                        {
                            Uri.TryCreate((string) parsedPSScriptInfoHashtable["ICONURI"], UriKind.Absolute, out parsedProjectUri);
                        }

                        // parsedPSScriptInfoHashtable should contain all keys, but values may be empty (i.e empty array, String.empty)
                        parsedScript = new PSScriptFileInfo(
                            version: parsedVersion,
                            guid: parsedGuid,
                            author: parsedAuthor,
                            companyName: (string) parsedPSScriptInfoHashtable["COMPANYNAME"],
                            copyright: (string) parsedPSScriptInfoHashtable["COPYRIGHT"],
                            tags: parsedTags,
                            licenseUri: parsedLicenseUri,
                            projectUri: parsedProjectUri,
                            iconUri: parsedIconUri,
                            requiredModules: parsedModules.ToArray(),
                            externalModuleDependencies: parsedExternalModuleDependencies,
                            requiredScripts: parsedRequiredScripts,
                            externalScriptDependencies: parsedExternalScriptDependencies,
                            releaseNotes: parsedReleaseNotes,
                            privateData: (string) parsedPSScriptInfoHashtable["PRIVATEDATA"],
                            description: scriptCommentInfo.Description);
                    }
                    catch (Exception e)
                    {
                        var message = String.Format("PSScriptFileInfo object could not be created from passed in file due to {0}", e.Message);
                        var ex = new ArgumentException(message);
                        var PSScriptFileInfoObjectNotCreatedFromFileError = new ErrorRecord(ex, "PSScriptFileInfoObjectNotCreatedFromFile", ErrorCategory.ParserError, null);
                        errorsList.Add(PSScriptFileInfoObjectNotCreatedFromFileError);
                        successfullyParsed = false;
                    }
                }
                else
                {
                    var message = String.Format(".ps1 file was parsed but AST was null");
                    var ex = new ArgumentException(message);
                    var astCouldNotBeCreatedError = new ErrorRecord(ex, "ASTCouldNotBeCreated", ErrorCategory.ParserError, null);
                    errorsList.Add(astCouldNotBeCreatedError);
                    return successfullyParsed; 
                }
            }

            return successfullyParsed;
        }

        /// <summary>
        /// Updates the contents of the .ps1 file at the provided path with the properties provided
        /// and writes new updated script file contents to a string and updates the original PSScriptFileInfo object
        /// </summary>        
        public static bool TryUpdateRequestedFields(
            ref PSScriptFileInfo originalScript,
            out string updatedPSScriptFileContents,
            string filePath,
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
            bool successfullyUpdated = false;
            // updatedScript = null;
            updatedPSScriptFileContents = String.Empty;
            errors = new ErrorRecord[]{};
            List<ErrorRecord> errorsList = new List<ErrorRecord>();

            if (originalScript == null)
            {
                var message = String.Format("PSScriptFileInfo object to update is null.");
                var ex = new ArgumentException(message);
                var nullPSScriptFileInfoObjectToUpdateError = new ErrorRecord(ex, "NullPSScriptFileInfoObjectToUpdate", ErrorCategory.ParserError, null);
                errorsList.Add(nullPSScriptFileInfoObjectToUpdateError);  
                errors = errorsList.ToArray();
                return successfullyUpdated;
            }

            PSScriptFileInfo updatedScript = originalScript;
            
            // create new PSScriptFileInfo with updated fields
            try
            {
                if (!String.IsNullOrEmpty(version))
                {
                    if (!System.Version.TryParse(version, out Version updatedVersion))
                    {
                        updatedScript.Version = new Version("2.0.0.0");
                    }
                    else
                    {
                        updatedScript.Version = updatedVersion;
                    }
                }

                if (guid != Guid.Empty)
                {
                    updatedScript.Guid = guid;
                }

                if (!String.IsNullOrEmpty(author))
                {
                    updatedScript.Author = author;
                }

                if (!String.IsNullOrEmpty(companyName)){
                    updatedScript.CompanyName = companyName;
                }

                if (!String.IsNullOrEmpty(copyright)){
                    updatedScript.Copyright = copyright;
                }

                if (tags != null && tags.Length != 0){
                    updatedScript.Tags = tags;
                }

                if (licenseUri != null && !licenseUri.Equals(default(Uri))){
                    updatedScript.LicenseUri = licenseUri;
                }

                if (projectUri != null && !projectUri.Equals(default(Uri))){
                    updatedScript.ProjectUri = projectUri;
                }

                if (iconUri != null && !iconUri.Equals(default(Uri))){
                    updatedScript.IconUri = iconUri;
                }

                if (requiredModules != null && requiredModules.Length != 0){
                    updatedScript.RequiredModules = requiredModules;
                }

                if (externalModuleDependencies != null && externalModuleDependencies.Length != 0){
                    updatedScript.ExternalModuleDependencies = externalModuleDependencies;                
                }

                if (requiredScripts != null && requiredScripts.Length != 0)
                {
                    updatedScript.RequiredScripts = requiredScripts;
                }

                if (externalScriptDependencies != null && externalScriptDependencies.Length != 0){
                    updatedScript.ExternalScriptDependencies = externalScriptDependencies;                
                }

                if (releaseNotes != null && releaseNotes.Length != 0)
                {
                    updatedScript.ReleaseNotes = releaseNotes;
                }

                if (!String.IsNullOrEmpty(privateData))
                {
                    updatedScript.PrivateData = privateData;
                }

                if (!String.IsNullOrEmpty(description))
                {
                    updatedScript.Description = description;
                }

                originalScript = updatedScript;
            }
            catch (Exception exception)
            {
                var message = String.Format(".ps1 file and associated PSScriptFileInfo object's field could not be updated due to {0}.", exception.Message);
                var ex = new ArgumentException(message);
                var PSScriptFileInfoFieldCouldNotBeUpdatedError = new ErrorRecord(ex, "PSScriptFileInfoFieldCouldNotBeUpdated", ErrorCategory.ParserError, null);
                errorsList.Add(PSScriptFileInfoFieldCouldNotBeUpdatedError);  
                errors = errorsList.ToArray();
                return successfullyUpdated;
            }


            // create string contents for .ps1 file
            if (!updatedScript.TryCreateScriptFileInfoString(
                pSScriptFileString: out string psScriptFileContents,
                filePath: filePath,
                errors: out ErrorRecord[] createFileContentErrors))
            {
                errorsList.AddRange(createFileContentErrors);
                errors = errorsList.ToArray();
                return successfullyUpdated;
            }

            successfullyUpdated = true;
            updatedPSScriptFileContents = psScriptFileContents;
            return successfullyUpdated;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Create .ps1 file contents with PSScriptFileInfo object's properties and output content as a string
        /// </summary>
        public bool TryCreateScriptFileInfoString(
            string filePath,
            out string pSScriptFileString,
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

            pSScriptFileString += "\n" + psHelpInfo;

            Console.WriteLine("made it here");
            GetEndOfFileLinesContent(
                filePath: filePath,
                endOfFileContent: out string endOfFileAstContent);
            if (!String.IsNullOrEmpty(endOfFileAstContent))
            {
                pSScriptFileString += "\n" + endOfFileAstContent;
            }

            fileContentsSuccessfullyCreated = true;
            return fileContentsSuccessfullyCreated;
        }

        /// <summary>
        /// Used when creating .ps1 file's contents.
        /// This creates the <#PSScriptInfo ... #> comment string
        /// </summary>
        public bool GetPSScriptInfoString(
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
            psScriptInfoLines.Add("#>");

            pSScriptInfoString = String.Join("\n\n", psScriptInfoLines);
            return pSScriptInfoSuccessfullyCreated;
        }

        /// <summary>
        /// Used when creating .ps1 file's contents.
        /// This creates the #Requires comment string
        /// </summary>
        public void GetRequiresString(
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
        public bool GetScriptCommentHelpInfo(
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


        public void GetEndOfFileLinesContent(
            string filePath,
            out string endOfFileContent)
        {
            endOfFileContent = String.Empty;

            if (String.IsNullOrEmpty(filePath) || !filePath.EndsWith(".ps1"))
            {
                return;
            }

            string[] totalFileContents = File.ReadAllLines(filePath);
            var contentAfterAndIncludingDescription = totalFileContents.SkipWhile(x => !x.Contains(".DESCRIPTION")).ToList();

            var contentAfterDescription = contentAfterAndIncludingDescription.SkipWhile(x => !x.Contains("#>")).Skip(1).ToList();

            if (contentAfterDescription.Count() > 0)
            {
                endOfFileContent = String.Join("\n", contentAfterDescription);
            }
        }

        #endregion
    }
}