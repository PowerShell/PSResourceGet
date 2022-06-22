// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Text;
using Microsoft.PowerShell.Commands;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// Updates a .ps1 file with specified properties
    /// </summary>
    [Cmdlet(VerbsData.Update, "PSScriptFileInfo")]
    public sealed class UpdatePSScriptFileInfo : PSCmdlet
    {
        #region Parameters

        /// <summary>
        /// The author of the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string Author { get; set; }

        /// <summary>
        /// The name of the company owning the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string CompanyName { get; set; }

        /// <summary>
        /// The copyright information for the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string Copyright { get; set; }

        /// <summary>
        /// The description of the script
        /// </summary>
        [Parameter()]
        [ValidateNotNullOrEmpty()]
        public string Description { get; set; }

        /// <summary>
        /// The list of external module dependencies taken by this script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string[] ExternalModuleDependencies { get; set; }

        /// <summary>
        /// The list of external script dependencies taken by this script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string[] ExternalScriptDependencies { get; set; }

        /// <summary>
        /// If used with Path parameter and .ps1 file specified at the path exists, it rewrites the file
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// The GUID for the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public Guid Guid { get; set; }

        /// <summary>
        /// The Uri for the icon associated with the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string IconUri { get; set; }

        /// <summary>
        /// The Uri for the license associated with the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string LicenseUri { get; set; }

        /// <summary>
        /// The path the .ps1 script info file will be created at
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string FilePath { get; set; }

        /// <summary>
        /// The private data associated with the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string PrivateData { get; set; }

        /// <summary>
        /// The Uri for the project associated with the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string ProjectUri { get; set; }

        /// <summary>
        /// The release notes for the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string[] ReleaseNotes { get; set; }

        /// <summary>
        /// Remove signature from signed .ps1 (if present) thereby allowing update of script to happen
        /// User should re-sign the updated script afterwards.
        /// </summary>
        [Parameter]
        public SwitchParameter RemoveSignature { get; set; }

        /// <summary>
        /// The list of modules required by the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public Hashtable[] RequiredModules { get; set; }

        /// <summary>
        /// The list of scripts required by the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string[] RequiredScripts { get; set; }

        /// <summary>
        /// The tags associated with the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string[] Tags { get; set; }

        /// <summary>
        /// The version of the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string Version { get; set; }

        #endregion

        #region Private Members

        private const string signatureStartString = "# SIG # Begin signature block";

        #endregion

        #region Methods

        protected override void ProcessRecord()
        {
            Uri projectUri = null;
            if (!String.IsNullOrEmpty(ProjectUri) && !Utils.TryCreateValidUri(uriString: ProjectUri,
                cmdletPassedIn: this,
                uriResult: out projectUri,
                errorRecord: out ErrorRecord projectErrorRecord))
            {
                ThrowTerminatingError(projectErrorRecord);
            }

            Uri licenseUri = null;
            if (!String.IsNullOrEmpty(LicenseUri) && !Utils.TryCreateValidUri(uriString: LicenseUri,
                cmdletPassedIn: this,
                uriResult: out licenseUri,
                errorRecord: out ErrorRecord licenseErrorRecord))
            {
                ThrowTerminatingError(licenseErrorRecord);
            }

            Uri iconUri = null;
            if (!String.IsNullOrEmpty(IconUri) && !Utils.TryCreateValidUri(uriString: IconUri,
                cmdletPassedIn: this,
                uriResult: out iconUri,
                errorRecord: out ErrorRecord iconErrorRecord))
            {
                ThrowTerminatingError(iconErrorRecord);
            }

            if (!FilePath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                    var exMessage = "File path needs to end with a .ps1 extension. Example: C:/Users/john/x/MyScript.ps1";
                    var ex = new ArgumentException(exMessage);
                    var InvalidOrNonExistantPathError = new ErrorRecord(ex, "InvalidOrNonExistantPath", ErrorCategory.InvalidArgument, null);
                    ThrowTerminatingError(InvalidOrNonExistantPathError);   
            }

            var resolvedPaths = SessionState.Path.GetResolvedPSPathFromPSPath(FilePath);
            if (resolvedPaths.Count != 1)
            {
                var exMessage = "Error: Could not resolve provided Path argument into a single path.";
                var ex = new PSArgumentException(exMessage);
                var InvalidPathArgumentError = new ErrorRecord(ex, "InvalidPathArgumentError", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(InvalidPathArgumentError);
            }

            string resolvedFilePath = resolvedPaths[0].Path;

            if (!File.Exists(resolvedFilePath))
            {
                var exMessage = "A file does not exist at the location specified";
                var ex = new ArgumentException(exMessage);
                var FileDoesNotExistError = new ErrorRecord(ex, "FileDoesNotExistAtPath", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(FileDoesNotExistError);
            }
            
            ModuleSpecification[] validatedRequiredModuleSpecifications = new ModuleSpecification[]{};
            if (RequiredModules != null && RequiredModules.Length > 0)
            {
                if (!Utils.TryCreateModuleSpecification(
                    moduleSpecHashtables: RequiredModules,
                    out validatedRequiredModuleSpecifications,
                    out ErrorRecord[] moduleSpecErrors))
                {
                    foreach (ErrorRecord err in moduleSpecErrors)
                    {
                        WriteError(err);
                    }

                    return;
                }
            }

            if (!PSScriptFileInfo.TryParseScriptIntoPSScriptInfo(
                scriptFileInfoPath: resolvedFilePath,
                parsedScript: out PSScriptFileInfo parsedScriptInfo,
                errors: out ErrorRecord[] errors,
                out string[] verboseMsgs))
            {
                foreach (string msg in verboseMsgs)
                {
                    WriteVerbose(msg);
                }

                WriteWarning("The .ps1 script file passed in was not valid due to the following error(s) listed below");
                foreach (ErrorRecord error in errors)
                {
                    WriteError(error);
                }

                return; 
            }

            if (parsedScriptInfo.EndOfFileContents.Contains(signatureStartString))
            {
                WriteWarning("This script contains a signature and cannot be updated without invalidating the current script signature");
                if (!RemoveSignature)
                {
                    var exMessage = "Cannot update script as the .ps1 contains a signature. Either use -RemoveSignature paramter or manaully remove signature block and re-run cmdlet.";
                    var ex = new PSInvalidOperationException(exMessage);
                    var ScriptToBeUpdatedContainsSignatureError = new ErrorRecord(ex, "ScriptToBeUpdatedContainsSignature", ErrorCategory.InvalidOperation, null);
                    ThrowTerminatingError(ScriptToBeUpdatedContainsSignatureError);
                }
            }
            
            if (!PSScriptFileInfo.TryUpdateScriptFileContents(
                scriptInfo: parsedScriptInfo,
                updatedPSScriptFileContents: out string updatedPSScriptFileContents,
                errors: out ErrorRecord[] updateErrors,
                version: Version,
                guid: Guid,
                author: Author,
                companyName: CompanyName,
                copyright: Copyright,
                tags: Tags,
                licenseUri: licenseUri,
                projectUri: projectUri,
                iconUri: iconUri,
                requiredModules: validatedRequiredModuleSpecifications,
                externalModuleDependencies: ExternalModuleDependencies,
                requiredScripts: RequiredScripts,
                externalScriptDependencies: ExternalScriptDependencies,
                releaseNotes: ReleaseNotes,
                privateData: PrivateData,
                description: Description))
            {
                WriteWarning("Updating the specified script file failed due to the following error(s):");
                foreach (ErrorRecord error in updateErrors)
                {
                    WriteError(error);
                }

                return;
            }
                  
            string tempScriptFilePath = null;
            try
            {
                tempScriptFilePath = Path.GetTempFileName();

                File.WriteAllText(tempScriptFilePath, updatedPSScriptFileContents); 
                File.Copy(tempScriptFilePath, resolvedFilePath, overwrite: true);     
            }
            catch(Exception e)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException($"Could not update .ps1 file due to: {e.Message}"),
                    "FileIOErrorDuringUpdate",
                    ErrorCategory.InvalidArgument,
                    this));
            }
            finally
            {
                if (tempScriptFilePath != null)
                {
                    File.Delete(tempScriptFilePath);
                }
            }    
        }

        #endregion
    }
}
