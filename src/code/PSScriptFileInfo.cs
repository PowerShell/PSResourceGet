// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Linq;
using Microsoft.PowerShell.Commands;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    /// <summary>
    /// This class contains information for a PSScriptFileInfo (representing a .ps1 file contents).
    /// </summary>
    public sealed class PSScriptFileInfo
    {
        #region Properties
        public PSScriptMetadata ScriptMetadataComment { get; set; }

        public PSScriptHelp ScriptHelpComment { get; set; }

        public PSScriptRequires ScriptRequiresComment { get; set; }

        public PSScriptContents ScriptContent { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// This constructor takes metadata values that could have been passed in by the calling cmdlet
        /// and uses those to create associated script class properties (PSScriptMetadata, PSScriptHelp, PSScriptRequires, PSScriptContents)
        /// </summary>
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
            string releaseNotes,
            string privateData,
            string description)
        {
            PSScriptMetadata scriptMetadataComment = new PSScriptMetadata(
                version,
                guid,
                author,
                companyName,
                copyright,
                tags,
                licenseUri,
                projectUri,
                iconUri,
                externalModuleDependencies,
                requiredScripts,
                externalScriptDependencies,
                releaseNotes,
                privateData);

            PSScriptHelp scriptHelpComment = new PSScriptHelp(description);
            PSScriptRequires scriptRequiresComment = new PSScriptRequires(requiredModules);
            PSScriptContents scriptRemainingContent = new PSScriptContents(Utils.EmptyStrArray);

            ScriptMetadataComment = scriptMetadataComment;
            ScriptHelpComment = scriptHelpComment;
            ScriptRequiresComment = scriptRequiresComment;
            ScriptContent = scriptRemainingContent;
        }

        /// <summary>
        /// This constructor takes script class properties' values that could have been passed in by the calling internal methods.
        /// </summary>
        public PSScriptFileInfo(
            PSScriptMetadata scriptMetadataComment,
            PSScriptHelp scriptHelpComment,
            PSScriptRequires scriptRequiresComment,
            PSScriptContents scriptRemainingContent
        )
        {
            ScriptMetadataComment = scriptMetadataComment;
            ScriptHelpComment = scriptHelpComment;
            ScriptRequiresComment = scriptRequiresComment;
            ScriptContent = scriptRemainingContent;
        }

        #endregion

        #region Internal Static Methods

        /// <summary>
        /// Parses .ps1 file contents for PSScriptInfo, PSHelpInfo, Requires comments
        /// </summary>
        internal static bool TryParseScriptFileContents(
            string scriptFileInfoPath,
            ref List<string> psScriptInfoCommentContent,
            ref List<string> helpInfoCommentContent,
            ref List<string> requiresCommentContent,
            ref string[] remainingFileContent,
            out ErrorRecord error)
        {
            error= null;

            psScriptInfoCommentContent = new List<string>();
            helpInfoCommentContent = new List<string>();
            requiresCommentContent = new List<string>();
            remainingFileContent = Utils.EmptyStrArray;

            string[] fileContents = File.ReadAllLines(scriptFileInfoPath);

            bool reachedPSScriptInfoCommentEnd = false;
            bool reachedHelpInfoCommentEnd = false;

            int i = 0;
            int endOfFileContentsStartIndex = 0;
            while (i < fileContents.Length)
            {
                string line = fileContents[i];
                
                if (line.StartsWith("<#PSScriptInfo"))
                {
                    int j = i + 1; // start at the next line
                    // keep grabbing lines until we get to closing #>
                    while (j < fileContents.Length)
                    {
                        string blockLine = fileContents[j];
                        if (blockLine.StartsWith("#>"))
                        {
                            reachedPSScriptInfoCommentEnd = true;
                            i = j + 1;
                            break;
                        }
                        
                        psScriptInfoCommentContent.Add(blockLine);
                        j++;
                    }

                    if (!reachedPSScriptInfoCommentEnd)
                    {
                        var message = String.Format("Could not parse '{0}' as a PowerShell script file due to missing the closing '#>' for <#PSScriptInfo comment block", scriptFileInfoPath);
                        var ex = new InvalidOperationException(message);
                        error = new ErrorRecord(ex, "MissingEndBracketToPSScriptInfoParseError", ErrorCategory.ParserError, null);
                        return false;
                    }
                }
                else if (line.StartsWith("<#"))
                {
                    // The next comment block must be the help comment block (containing description)
                    // keep grabbing lines until we get to closing #>
                    int j = i + 1;
                    while (j < fileContents.Length)
                    {
                        string blockLine = fileContents[j];
                        if (blockLine.StartsWith("#>"))
                        {
                            reachedHelpInfoCommentEnd = true;
                            i = j + 1;
                            endOfFileContentsStartIndex = i;
                            break;
                        }
                        
                        helpInfoCommentContent.Add(blockLine);
                        j++;
                    }

                    if (!reachedHelpInfoCommentEnd)
                    {
                        var message = String.Format("Could not parse '{0}' as a PowerShell script file due to missing the closing '#>' for HelpInfo comment block", scriptFileInfoPath);
                        var ex = new InvalidOperationException(message);
                        error = new ErrorRecord(ex, "MissingEndBracketToHelpInfoCommentParseError", ErrorCategory.ParserError, null);
                        return false;
                    }
                }
                else if (line.StartsWith("#Requires"))
                {
                    requiresCommentContent.Add(line);
                    i++;
                }
                else if (endOfFileContentsStartIndex != 0)
                {
                    break;
                }
                else
                {
                    // this would be newlines between blocks, or if there was other (unexpected) data between PSScriptInfo, Requires, and HelpInfo blocks
                    i++;
                }
            }

            if (endOfFileContentsStartIndex != 0 && (endOfFileContentsStartIndex < fileContents.Length))
            {
                // from this line to fileContents.Length is the endOfFileContents, if any
                remainingFileContent = new string[fileContents.Length - endOfFileContentsStartIndex];

                Array.Copy(fileContents, endOfFileContentsStartIndex, remainingFileContent, 0, (fileContents.Length - endOfFileContentsStartIndex));
            }

            if (psScriptInfoCommentContent.Count() == 0)
            {
                // check for file not containing '<#PSScriptInfo ... #>' comment
                var message = String.Format("Could not parse '{0}' as a PowerShell script due to it missing '<#PSScriptInfo #> block", scriptFileInfoPath);
                var ex = new InvalidOperationException(message);
                error = new ErrorRecord(ex, "MissingEndBracketToHelpInfoCommentParseError", ErrorCategory.ParserError, null);
                return false;
            }

            if (helpInfoCommentContent.Count() == 0)
            {
                // check for file not containing HelpInfo comment
                var message = String.Format("Could not parse '{0}' as a PowerShell script due to it missing HelpInfo comment block", scriptFileInfoPath);
                var ex = new InvalidOperationException(message);
                error = new ErrorRecord(ex, "missingHelpInfoCommentError", ErrorCategory.ParserError, null);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Populates script info classes (PSScriptMetadata, PSScriptHelp, PSScriptRequires, PSScriptContents) with previosuly
        /// parsed metadata from the ps1 file.
        /// </summary>
        internal static bool TryPopulateScriptClassesWithParsedContent(
            List<string> psScriptInfoCommentContent,
            List<string> helpInfoCommentContent,
            List<string> requiresCommentContent,
            string[] remainingFileContent,
            out PSScriptMetadata currentMetadata,
            out PSScriptHelp currentHelpInfo,
            out PSScriptRequires currentRequiresComment,
            out PSScriptContents currentEndOfFileContents,
            out ErrorRecord[] errors,
            out string[] verboseMsgs)
        {
            List<ErrorRecord> errorsList = new List<ErrorRecord>();

            bool parsedContentSuccessfully = true;

            currentMetadata = new PSScriptMetadata();
            if (!currentMetadata.ParseContentIntoObj(
                commentLines: psScriptInfoCommentContent.ToArray(),
                out ErrorRecord[] metadataErrors,
                out verboseMsgs))
            {
                errorsList.AddRange(metadataErrors);
                parsedContentSuccessfully = false;
            }

            currentHelpInfo = new PSScriptHelp();
            if (!currentHelpInfo.ParseContentIntoObj(
                commentLines: helpInfoCommentContent.ToArray(),
                out ErrorRecord helpError))
            {
                errorsList.Add(helpError);
                parsedContentSuccessfully = false;
            }

            currentRequiresComment = new PSScriptRequires();
            if (!currentRequiresComment.ParseContentIntoObj(
                commentLines: requiresCommentContent.ToArray(),
                out ErrorRecord[] requiresErrors))
            {
                errorsList.AddRange(requiresErrors);
                parsedContentSuccessfully = false;
            }

            currentEndOfFileContents = new PSScriptContents();
            currentEndOfFileContents.ParseContent(commentLines: remainingFileContent);

            errors = errorsList.ToArray();
            return parsedContentSuccessfully;
        }

        /// <summary>
        /// Tests .ps1 file for validity
        /// </summary>
        internal static bool TryTestPSScriptFile(
            string scriptFileInfoPath,
            out PSScriptFileInfo parsedScript,
            out ErrorRecord[] errors,
            // this is for Uri errors, which aren't required by script but we check if those in the script aren't valid Uri's.
            out string[] verboseMsgs)
        {
            verboseMsgs = Utils.EmptyStrArray;
            List<ErrorRecord> errorsList = new List<ErrorRecord>();
            parsedScript = null;

            List<string> psScriptInfoCommentContent = new List<string>();
            List<string> helpInfoCommentContent = new List<string>();
            List<string> requiresCommentContent = new List<string>();
            string[] remainingFileContent = Utils.EmptyStrArray;

            // Parse .ps1 contents out of file into list objects
            if (!TryParseScriptFileContents(
                scriptFileInfoPath: scriptFileInfoPath,
                psScriptInfoCommentContent: ref psScriptInfoCommentContent,
                helpInfoCommentContent: ref helpInfoCommentContent,
                requiresCommentContent: ref requiresCommentContent,
                remainingFileContent: ref remainingFileContent,
                out ErrorRecord parseError))
            {
                errors = new ErrorRecord[]{parseError};
                return false;
            }

            // Populate PSScriptFileInfo object by first creating instances for the property objects
            // i.e (PSScriptMetadata, PSScriptHelp, PSScriptRequires, PSScriptContents)
            if (!TryPopulateScriptClassesWithParsedContent(
                psScriptInfoCommentContent: psScriptInfoCommentContent,
                helpInfoCommentContent: helpInfoCommentContent,
                requiresCommentContent: requiresCommentContent,
                remainingFileContent: remainingFileContent,
                currentMetadata: out PSScriptMetadata currentMetadata,
                currentHelpInfo: out PSScriptHelp currentHelpInfo,
                currentRequiresComment: out PSScriptRequires currentRequiresComment,
                currentEndOfFileContents: out PSScriptContents currentEndOfFileContents,
                errors: out errors,
                out verboseMsgs))
            {
                return false;
            }

            // Create PSScriptFileInfo instance with script metadata class instances (PSScriptMetadata, PSScriptHelp, PSScriptRequires, PSScriptContents)
            try
            {
                parsedScript = new PSScriptFileInfo(
                    scriptMetadataComment: currentMetadata,
                    scriptHelpComment: currentHelpInfo,
                    scriptRequiresComment: currentRequiresComment,
                    scriptRemainingContent: currentEndOfFileContents);
            }
            catch (Exception e)
            {
                var message = String.Format("PSScriptFileInfo object could not be created from passed in file due to {0}", e.Message);
                var ex = new ArgumentException(message);
                var PSScriptFileInfoObjectNotCreatedFromFileError = new ErrorRecord(ex, "PSScriptFileInfoObjectNotCreatedFromFileError", ErrorCategory.ParserError, null);
                errors = new ErrorRecord[]{PSScriptFileInfoObjectNotCreatedFromFileError};
                return false;
            }

            errors = errorsList.ToArray();
            return true;
        }

        /// <summary>
        /// Updates .ps1 file.
        /// Caller must check that the file to update doesn't have a signature or if it does permission to remove signature has been granted
        /// as this method will remove original signature, as updating would have invalidated it.
        /// </summary>
        internal static bool TryUpdateScriptFileContents(
            PSScriptFileInfo scriptInfo,
            out string[] updatedPSScriptFileContents,
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
            string releaseNotes,
            string privateData,
            string description)
        {
            updatedPSScriptFileContents = Utils.EmptyStrArray;
            List<ErrorRecord> errorsList = new List<ErrorRecord>();
            bool successfullyUpdated = true;

            if (scriptInfo == null)
            {
                throw new ArgumentNullException(nameof(scriptInfo));
            }

            if (!scriptInfo.ScriptMetadataComment.UpdateContent(
                version: version,
                guid: guid,
                author: author,
                companyName: companyName,
                copyright: copyright,
                tags: tags,
                licenseUri: licenseUri,
                projectUri: projectUri,
                iconUri: iconUri,
                externalModuleDependencies: externalModuleDependencies,
                requiredScripts: requiredScripts,
                externalScriptDependencies: externalScriptDependencies,
                releaseNotes: releaseNotes,
                privateData: privateData,
                out ErrorRecord metadataUpdateError))
            {
                errorsList.Add(metadataUpdateError);
                successfullyUpdated = false;
            }

            if (!scriptInfo.ScriptHelpComment.UpdateContent(
                description: description,
                out ErrorRecord helpUpdateError))
            {
                errorsList.Add(helpUpdateError);
                successfullyUpdated = false;
            }

            // this doesn't produce errors, as ModuleSpecification creation is already validated before param passed in
            // and user can't update endOfFileContents
            scriptInfo.ScriptRequiresComment.UpdateContent(requiredModules: requiredModules);

            if (!successfullyUpdated)
            {
                errors = errorsList.ToArray();
                return successfullyUpdated;
            }

            // create string contents for .ps1 file
            if (!scriptInfo.TryCreateScriptFileInfoString(
                psScriptFileContents: out updatedPSScriptFileContents,
                errors: out ErrorRecord[] createUpdatedFileContentErrors))
            {
                errorsList.AddRange(createUpdatedFileContentErrors);
                successfullyUpdated = false;
            }

            errors = errorsList.ToArray();
            return successfullyUpdated;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Creates .ps1 file content string representation for the PSScriptFileInfo object this called upon, which is used by the caller to write the .ps1 file.
        /// </summary>
        internal bool TryCreateScriptFileInfoString(
            out string[] psScriptFileContents,
            out ErrorRecord[] errors
        )
        {
            psScriptFileContents = Utils.EmptyStrArray;
            List<string> fileContentsList = new List<string>();
            errors = new ErrorRecord[]{};
            List<ErrorRecord> errorsList = new List<ErrorRecord>();

            bool fileContentsSuccessfullyCreated = true;

            // Step 1: validate object properties for required script properties.
            if (!ScriptMetadataComment.ValidateContent(out ErrorRecord[] metadataValidationErrors))
            {
                errorsList.AddRange(metadataValidationErrors);
                fileContentsSuccessfullyCreated = false;
            }

            if (!ScriptHelpComment.ValidateContent(out ErrorRecord helpValidationError))
            {
                errorsList.Add(helpValidationError);
                fileContentsSuccessfullyCreated = false;
            }

            if (!fileContentsSuccessfullyCreated)
            {
                errors = errorsList.ToArray();
                return fileContentsSuccessfullyCreated;
            }

            // Step 2: create string [] that will be used to write to file later
            fileContentsList.AddRange(ScriptMetadataComment.EmitContent());

            // string psRequiresCommentBlock = ScriptRequiresComment.EmitContent();
            fileContentsList.AddRange(ScriptRequiresComment.EmitContent());
            fileContentsList.AddRange(ScriptHelpComment.EmitContent());
            fileContentsList.AddRange(ScriptContent.EmitContent());

            psScriptFileContents = fileContentsList.ToArray();
            return fileContentsSuccessfullyCreated;
        }

        #endregion
    }
}
