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
                // requiredModules,
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

            // if (!ScriptRequiresComment.ValidateContent())
            // {
            //     fileContentsSuccessfullyCreated = false;
            //     // store error and return
            //     // I think the method had a way of checking its not empty if it attemps to write
            // }

            // if (!ScriptContent.ValidateContent())
            // {

            // }

            if (!fileContentsSuccessfullyCreated)
            {
                errors = errorsList.ToArray();
                return fileContentsSuccessfullyCreated;
            }

            // todo: validate endofffilecontents here?

            // step: try to write
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
                // todo: remove signature here?
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
