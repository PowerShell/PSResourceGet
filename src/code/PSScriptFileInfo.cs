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
    public sealed class PSScriptFileInfo
    {
        #region Properties
        public PSScriptMetadata ScriptMetadataComment { get; set; }

        public PSScriptHelp ScriptHelpComment { get; set; }

        public PSScriptRequires ScriptRequiresComment { get; set; }

        public PSScriptContents ScriptContent { get; set; }

        #endregion

        #region Constructor

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
            PSScriptContents scriptRemainingContent = new PSScriptContents(String.Empty);

            this.ScriptMetadataComment = scriptMetadataComment;
            this.ScriptHelpComment = scriptHelpComment;
            this.ScriptRequiresComment = scriptRequiresComment;
            this.ScriptContent = scriptRemainingContent;
        }

        public PSScriptFileInfo(
            PSScriptMetadata scriptMetadataComment,
            PSScriptHelp scriptHelpComment,
            PSScriptRequires scriptRequiresComment,
            PSScriptContents scriptRemainingContent
        )
        {
            this.ScriptMetadataComment = scriptMetadataComment;
            this.ScriptHelpComment = scriptHelpComment;
            this.ScriptRequiresComment = scriptRequiresComment;
            this.ScriptContent = scriptRemainingContent;
        }

        #endregion

        #region Internal Static Methods

        /// <summary>
        /// Tests .ps1 file for validity
        /// </summary>
        // tODO: separate out where can into 4
        internal static bool TryParseScriptFile(
            string scriptFileInfoPath,
            out PSScriptFileInfo parsedScript,
            out ErrorRecord[] errors,
            out string[] verboseMsgs // this is for Uri errors, which aren't required by script but we check if those in the script aren't valid Uri's.
        )
        {
            verboseMsgs = new string[]{};
            List<ErrorRecord> errorsList = new List<ErrorRecord>();
            parsedScript = null;

            string[] fileContents = File.ReadAllLines(scriptFileInfoPath);

            List<string> psScriptInfoCommentContent = new List<string>();
            List<string> helpInfoCommentContent = new List<string>();
            List<string> requiresContent = new List<string>();
            string[] remainingFileContentArray = new string[]{};

            bool gotEndToPSSCriptInfoContent = false;
            bool gotEndToHelpInfoContent = false;
            bool parsedContentSuccessfully = true;

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
                            gotEndToPSSCriptInfoContent = true;
                            i = j + 1;
                            break;
                        }
                        
                        psScriptInfoCommentContent.Add(blockLine);
                        j++;
                    }

                    if (!gotEndToPSSCriptInfoContent)
                    {
                        var message = String.Format("Could not parse '{0}' as a PowerShell script file due to missing the closing '#>' for <#PSScriptInfo comment block", scriptFileInfoPath);
                        var ex = new InvalidOperationException(message);
                        var missingEndBracketToPSScriptInfoParseError = new ErrorRecord(ex, "MissingEndBracketToPSScriptInfoParseError", ErrorCategory.ParserError, null);
                        errors = new ErrorRecord[]{missingEndBracketToPSScriptInfoParseError};
                        return false;
                    }
                }
                else if (line.StartsWith("<#"))
                {
                    // we assume the next comment block should be the help comment block (containing description)
                    // keep grabbing lines until we get to closing #>
                    int j = i + 1;
                    while (j < fileContents.Length)
                    {
                        string blockLine = fileContents[j];
                        if (blockLine.StartsWith("#>"))
                        {
                            gotEndToHelpInfoContent = true;
                            i = j + 1;
                            endOfFileContentsStartIndex = i;
                            break;
                        }
                        
                        helpInfoCommentContent.Add(blockLine);
                        j++;
                    }

                    if (!gotEndToHelpInfoContent)
                    {
                        var message = String.Format("Could not parse '{0}' as a PowerShell script file due to missing the closing '#>' for HelpInfo comment block", scriptFileInfoPath);
                        var ex = new InvalidOperationException(message);
                        var missingEndBracketToHelpInfoCommentParseError = new ErrorRecord(ex, "MissingEndBracketToHelpInfoCommentParseError", ErrorCategory.ParserError, null);
                        errors = new ErrorRecord[]{missingEndBracketToHelpInfoCommentParseError};
                        return false;
                    }
                }
                else if (line.StartsWith("#Requires"))
                {
                    requiresContent.Add(line);
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
                remainingFileContentArray = new string[fileContents.Length - endOfFileContentsStartIndex];
                Array.Copy(fileContents, endOfFileContentsStartIndex, remainingFileContentArray, 0, (fileContents.Length - endOfFileContentsStartIndex));
            }

            if (psScriptInfoCommentContent.Count() == 0)
            {
                // check for file not containing '<#PSScriptInfo ... #>' comment
                var message = String.Format("Could not parse '{0}' as a PowerShell script due to it missing '<#PSScriptInfo #> block", scriptFileInfoPath);
                var ex = new InvalidOperationException(message);
                var missingPSScriptInfoCommentError = new ErrorRecord(ex, "MissingEndBracketToHelpInfoCommentParseError", ErrorCategory.ParserError, null);
                errors = new ErrorRecord[]{missingPSScriptInfoCommentError};
                return false;
            }

            if (helpInfoCommentContent.Count() == 0)
            {
                // check for file not containing HelpInfo comment
                var message = String.Format("Could not parse '{0}' as a PowerShell script due to it missing HelpInfo comment block", scriptFileInfoPath);
                var ex = new InvalidOperationException(message);
                var missingHelpInfoCommentError = new ErrorRecord(ex, "missingHelpInfoCommentError", ErrorCategory.ParserError, null);
                errors = new ErrorRecord[]{missingHelpInfoCommentError};
                return false;
            }

            // now populate PSScriptFileInfo object by first creating instances for the property objects
            PSScriptMetadata currentMetadata = new PSScriptMetadata();
            if (!currentMetadata.ParseContentIntoObj(
                commentLines: psScriptInfoCommentContent.ToArray(),
                out ErrorRecord[] metadataErrors,
                out verboseMsgs))
            {
                errorsList.AddRange(metadataErrors);
                parsedContentSuccessfully = false;
            }

            PSScriptHelp currentHelpInfo = new PSScriptHelp();
            if (!currentHelpInfo.ParseContentIntoObj(
                commentLines: helpInfoCommentContent.ToArray(),
                out ErrorRecord helpError))
            {
                errorsList.Add(helpError);
                parsedContentSuccessfully = false;
            }

            PSScriptRequires currentRequiresComment = new PSScriptRequires();
            if (!currentRequiresComment.ParseContentIntoObj(
                commentLines: requiresContent.ToArray(),
                out ErrorRecord[] requiresErrors))
            {
                errorsList.AddRange(requiresErrors);
                parsedContentSuccessfully = false;
            }

            PSScriptContents currentEndOfFileContents = new PSScriptContents();
            currentEndOfFileContents.ParseContent(commentLines: remainingFileContentArray);

            if (!parsedContentSuccessfully)
            {
                errors = errorsList.ToArray();
                return parsedContentSuccessfully;
            }

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
                errorsList.Add(PSScriptFileInfoObjectNotCreatedFromFileError);
                parsedContentSuccessfully = false;
            }

            errors = errorsList.ToArray();
            return parsedContentSuccessfully;
        }

        /// <summary>
        /// Updates .ps1 file.
        /// Caller must check that the file to update doesn't have a signature or if it does permission to remove signature has been granted
        /// as this method will remove original signature, as updating would have invalidated it
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
            string releaseNotes,
            string privateData,
            string description)
        {
            updatedPSScriptFileContents = String.Empty;
            List<ErrorRecord> errorsList = new List<ErrorRecord>();
            bool successfullyUpdated = true;

            if (scriptInfo == null)
            {
                var message = String.Format("Could not update .ps1 file as PSScriptFileInfo object created for it was null");
                var ex = new ArgumentException(message);
                var nullPSScriptFileInfoError = new ErrorRecord(ex, "NullPSScriptFileInfoError", ErrorCategory.ParserError, null);
                errors = new ErrorRecord[]{nullPSScriptFileInfoError};

                return false;
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
                psScriptFileString: out updatedPSScriptFileContents,
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
            out string psScriptFileString,
            out ErrorRecord[] errors
        )
        {
            psScriptFileString = String.Empty;
            errors = new ErrorRecord[]{};
            List<ErrorRecord> errorsList = new List<ErrorRecord>();

            bool fileContentsSuccessfullyCreated = true;

            // step 1: validate object properties for required script properties
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

            // step 2: create string that will be used to write later
            psScriptFileString = ScriptMetadataComment.EmitContent();

            string psRequiresCommentBlock = ScriptRequiresComment.EmitContent();
            if (!String.IsNullOrEmpty(psRequiresCommentBlock))
            {
                psScriptFileString += "\n";
                psScriptFileString += psRequiresCommentBlock;
            }

            psScriptFileString += "\n"; // need a newline after last #> and before <# for script comment block, TODO: try removing
            // or else not recongnized as a valid comment help info block when parsing the created ps1 later
            psScriptFileString += "\n" + ScriptHelpComment.EmitContent();

            string psEndOfFileContent = ScriptContent.EmitContent();
            if (!String.IsNullOrEmpty(psEndOfFileContent))
            {
                psScriptFileString += "\n" + psEndOfFileContent;
            }

            return fileContentsSuccessfullyCreated;
        }

        #endregion
    }
}
