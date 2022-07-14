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
    public sealed class PSScriptFileInfo2
    {
        #region Properties
        public PSScriptMetadata ScriptMetadataCommment { get; set; }

        public PSScriptHelp ScriptHelpComment { get; set; }

        public PSScriptRequires ScriptRequiresComment { get; set; }

        public PSScriptContents ScriptContent { get; set; }

        #endregion

        #region Constructor

        public PSScriptFileInfo2(
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

            this.ScriptMetadataCommment = scriptMetadataComment;
            this.ScriptHelpComment = scriptHelpComment;
            this.ScriptRequiresComment = scriptRequiresComment;
            this.ScriptContent = scriptRemainingContent;
        }

        public PSScriptFileInfo2(
            PSScriptMetadata scriptMetadataComment,
            PSScriptHelp scriptHelpComment,
            PSScriptRequires scriptRequiresComment,
            PSScriptContents scriptRemainingContent
        )
        {
            this.ScriptMetadataCommment = scriptMetadataComment;
            this.ScriptHelpComment = scriptHelpComment;
            this.ScriptRequiresComment = scriptRequiresComment;
            this.ScriptContent = scriptRemainingContent;
        }

        #endregion

        #region Internal Static Methods

        internal static bool TryParseScriptFile(
            string scriptFileInfoPath,
            out PSScriptFileInfo2 parsedScript,
            out ErrorRecord[] errors,
            out string[] msgs
        )
        {
            // parse -> create object -> validate
            parsedScript = null;
            errors = null;
            msgs = new string[]{};



            
            return true;
        }

        internal static bool TryParseScriptFile2(
            string scriptFileInfoPath,
            // out Hashtable parsedScriptMetadata,
            out PSScriptFileInfo2 parsedScript,
            out ErrorRecord error
        )
        {
            error = null;
            parsedScript = null;

            string[] fileContents = File.ReadAllLines(scriptFileInfoPath);

            List<string> psScriptInfoCommentContent = new List<string>();
            List<string> helpInfoCommentContent = new List<string>();
            List<string> requiresContent = new List<string>();



            PSScriptContents currentScriptContents;
            PSScriptRequires currentRequiresComment;

            string[] remainingFileContentArray = new string[]{};

            bool gotEndToPSSCriptInfoContent = false;
            bool gotEndToHelpInfoContent = false;

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
                        error = new ErrorRecord(ex, "MissingEndBracketToPSScriptInfoParseError", ErrorCategory.ParserError, null);
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
                        error = new ErrorRecord(ex, "MissingEndBracketToHelpInfoCommentParseError", ErrorCategory.ParserError, null);
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
                // from this line to fileContents.Length is the endOfFileContents
                // save it to append to end of file during Update
                remainingFileContentArray = new string[fileContents.Length - endOfFileContentsStartIndex];
                Array.Copy(fileContents, endOfFileContentsStartIndex, remainingFileContentArray, 0, (fileContents.Length - endOfFileContentsStartIndex));
            }


            // now populate PSScriptFileInfo object
            // first create instances for the property objects

            PSScriptMetadata currentMetadata = new PSScriptMetadata();
            if (!currentMetadata.ParseContentIntoObj(commentLines: psScriptInfoCommentContent.ToArray(),
                out ErrorRecord[] metadataErrors,
                out string[] verboseMsgs))
            {
                // set errors and return false
                // also perhaps verbose msgs?
            }

            PSScriptHelp currentHelpInfo = new PSScriptHelp();
            if (!currentHelpInfo.ParseContentIntoObj(commentLines: helpInfoCommentContent.ToArray()))
            {
                // write error
                // todo: why doesn't this return error? maybe parse level validation?
            }

            PSScriptRequires requiresComment = new PSScriptRequires();
            if (!requiresComment.ParseContent(commentLines: requiresContent.ToArray(),
                out ErrorRecord[] requiresErrors))
            {
                // set errors and return false
            }

            PSScriptContents endOfFileContents = new PSScriptContents();
            endOfFileContents.ParseContent(commentLines: remainingFileContentArray);

            return true;
        }

        internal bool TryCreateScriptFileInfoString(
            out string psScriptFileString,
            out ErrorRecord[] errors
        )
        {
            psScriptFileString = String.Empty;
            errors = new ErrorRecord[]{};
            List<ErrorRecord> errorsList = new List<ErrorRecord>();

            bool fileContentsSuccessfullyCreated = true;

            // step 1: validate
            if (!ScriptMetadataCommment.ValidateContent(out ErrorRecord[] metadataValidationErrors))
            {
                errorsList.AddRange(metadataValidationErrors);
                fileContentsSuccessfullyCreated = false;
            }

            if (!ScriptHelpComment.ValidateContent(out ErrorRecord helpValidationError))
            {
                errorsList.Add(helpValidationError);
                fileContentsSuccessfullyCreated = false;
            }

            // if (!ScriptContent.ValidateContent())
            // {
                // todo: validate endofffilecontents here
                // perhaps here ValidateContent will just check ContainsSignature is false.
                // on Update cmdlet side, when we have PSScriptFileInfo.ScriptContent.ContainsSignature then can call
                // PSScriptFileInfo.ScriptContent.RemoveSignature too. Otherwise will need to pass that param into this class.
            // }

            if (!fileContentsSuccessfullyCreated)
            {
                errors = errorsList.ToArray();
                return fileContentsSuccessfullyCreated;
            }

            // step 2: create string that will be used to write later
            psScriptFileString = ScriptMetadataCommment.EmitContent();

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


        internal static bool TryUpdateScriptFileContents(
            PSScriptFileInfo2 scriptInfo,
            out string updatedPSScriptFileContents,
            out ErrorRecord[] errors
        )
        {
            updatedPSScriptFileContents = string.Empty;
            errors = null;
            return true;
        }

        #endregion
    }
}
