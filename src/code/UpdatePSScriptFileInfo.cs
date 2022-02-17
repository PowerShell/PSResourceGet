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
    /// It retrieves a resource that was installed with Install-PSResource
    /// Returns a single resource or multiple resource.
    /// </summary>
    [Cmdlet(VerbsData.Update, "PSScriptFileInfo")]
    public sealed class UpdatePSScriptFileInfo : PSCmdlet
    {
        #region Members
        private Uri _projectUri;
        private Uri _licenseUri;
        private Uri _iconUri;

        #endregion

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
        /// If specified, passes the contents of the created .ps1 file to the console
        /// If -Path is not specified, then .ps1 contents will just be written out for the user
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

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
        /// If specified, it validates the updated script
        /// </summary>
        [Parameter]
        public SwitchParameter Validate { get; set; }

        /// <summary>
        /// The version of the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string Version { get; set; }

        #endregion

        #region Methods

        protected override void ProcessRecord()
        {
            // validate Uri related parameters passed in as strings
            if (!String.IsNullOrEmpty(ProjectUri) && !Utils.TryCreateValidUri(uriString: ProjectUri,
                cmdletPassedIn: this,
                uriResult: out _projectUri,
                errorRecord: out ErrorRecord projectErrorRecord))
            {
                ThrowTerminatingError(projectErrorRecord);
            }

            if (!String.IsNullOrEmpty(LicenseUri) && !Utils.TryCreateValidUri(uriString: LicenseUri,
                cmdletPassedIn: this,
                uriResult: out _licenseUri,
                errorRecord: out ErrorRecord licenseErrorRecord))
            {
                ThrowTerminatingError(licenseErrorRecord);
            }

            if (!String.IsNullOrEmpty(IconUri) && !Utils.TryCreateValidUri(uriString: IconUri,
                cmdletPassedIn: this,
                uriResult: out _iconUri,
                errorRecord: out ErrorRecord iconErrorRecord))
            {
                ThrowTerminatingError(iconErrorRecord);
            }

            if (!FilePath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) || !File.Exists(FilePath))
            {
                    var exMessage = "Path needs to exist and end with a .ps1 file. Example: C:/Users/john/x/MyScript.ps1";
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
            
            List<ModuleSpecification> validatedRequiredModuleSpecifications = new List<ModuleSpecification>();
            if (RequiredModules != null && RequiredModules.Length > 0)
            {
                // TODO: ANAM have this return array not list for mod specs
                Utils.CreateModuleSpecification(
                    moduleSpecHashtables: RequiredModules,
                    out validatedRequiredModuleSpecifications,
                    out ErrorRecord[] moduleSpecErrors);
                if (moduleSpecErrors.Length > 0)
                {
                    foreach (ErrorRecord err in moduleSpecErrors)
                    {
                        WriteError(err);
                    }
                }
            }

            // get PSScriptFileInfo object for current script contents
            if (!PSScriptFileInfo.TryParseScriptFile(
                scriptFileInfoPath: resolvedFilePath,
                out PSScriptFileInfo parsedScriptFileInfo,
                out ErrorRecord[] errors))
            {
                WriteWarning("The .ps1 script file passed in was not valid due to the following error(s) listed below");
                foreach (ErrorRecord error in errors)
                {
                    WriteError(error);
                }

                return; // TODO: should this be a terminating error instead?
            }
            else
            {
                if (!PSScriptFileInfo.TryUpdateScriptFile(
                    originalScript: ref parsedScriptFileInfo,
                    updatedPSScriptFileContents: out string updatedPSScriptFileContents,
                    filePath: resolvedFilePath,
                    errors: out ErrorRecord[] updateErrors,
                    version: Version,
                    guid: Guid,
                    author: Author,
                    companyName: CompanyName,
                    copyright: Copyright,
                    tags: Tags,
                    licenseUri: _licenseUri,
                    projectUri: _projectUri,
                    iconUri: _iconUri,
                    requiredModules: validatedRequiredModuleSpecifications.ToArray(),
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
                }
                else
                {                    
                    // write string of file contents to a temp file
                    var tempScriptDirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                    var tempScriptFilePath = Path.Combine(tempScriptDirPath, "tempScript.ps1");
                    if (!Directory.Exists(tempScriptDirPath))
                    {
                        Directory.CreateDirectory(tempScriptDirPath);
                    }

                    using(FileStream fs = File.Create(tempScriptFilePath))
                    {
                        byte[] info = new UTF8Encoding(true).GetBytes(updatedPSScriptFileContents);
                        fs.Write(info, 0, info.Length);
                    }

                    if (Validate)
                    {
                        if (!PSScriptFileInfo.TryParseScriptFile(
                            scriptFileInfoPath: tempScriptFilePath,
                            out parsedScriptFileInfo,
                            out ErrorRecord[] testErrors))
                        {
                            WriteWarning("Validating the updated script file failed due to the following error(s):");
                            foreach (ErrorRecord error in testErrors)
                            {
                                WriteError(error);
                            }

                            return; // TODO: Anam do we need this
                        }
                    }

                    File.Copy(tempScriptFilePath, resolvedFilePath, true);
                    Utils.DeleteDirectory(tempScriptDirPath);

                    if (PassThru)
                    {
                        WriteObject(parsedScriptFileInfo);
                    }
                }
            }         
        }

        #endregion
    }
}
