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
    [Cmdlet(VerbsCommon.New, "PSScriptFileInfo")]
    public sealed class NewPSScriptFileInfo : PSCmdlet
    {
        #region Members
        private Uri _projectUri;
        private Uri _licenseUri;
        private Uri _iconUri;

        #endregion

        #region Parameters

        /// <summary>
        /// The path the .ps1 script info file will be created at
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string FilePath { get; set; }

        /// <summary>
        /// The version of the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string Version { get; set; }

        /// <summary>
        /// The author of the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string Author { get; set; }

        /// <summary>
        /// The description of the script
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string Description { get; set; }

        /// <summary>
        /// The GUID for the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public Guid Guid { get; set; }

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
        /// The list of modules required by the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public Hashtable[] RequiredModules { get; set; }

        /// <summary>
        /// The list of external module dependencies taken by this script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string[] ExternalModuleDependencies { get; set; }

        /// <summary>
        /// The list of scripts required by the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string[] RequiredScripts { get; set; }

        /// <summary>
        /// The list of external script dependencies taken by this script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string[] ExternalScriptDependencies { get; set; }

        /// <summary>
        /// The tags associated with the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string[] Tags { get; set; }

        /// <summary>
        /// The Uri for the project associated with the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string ProjectUri { get; set; }

        /// <summary>
        /// The Uri for the license associated with the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string LicenseUri { get; set; }

        /// <summary>
        /// The Uri for the icon associated with the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string IconUri { get; set; }

        /// <summary>
        /// The release notes for the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string[] ReleaseNotes { get; set; }

        /// <summary>
        /// The private data associated with the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string PrivateData { get; set; }

        /// <summary>
        /// If specified, the .ps1 file contents are additionally written out to the console
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        /// <summary>
        /// If used with Path parameter and .ps1 file specified at the path exists, it rewrites the file
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

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

            if (!FilePath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                var exMessage = "Path needs to end with a .ps1 file. Example: C:/Users/john/x/MyScript.ps1";
                var ex = new ArgumentException(exMessage);
                var InvalidPathError = new ErrorRecord(ex, "InvalidPath", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(InvalidPathError);   
            }
            else if (File.Exists(FilePath) && !Force)
            {
                // .ps1 file at specified location already exists and Force parameter isn't used to rewrite the file
                var exMessage = ".ps1 file at specified path already exists. Specify a different location or use -Force parameter to overwrite the .ps1 file.";
                var ex = new ArgumentException(exMessage);
                var ScriptAtPathAlreadyExistsError = new ErrorRecord(ex, "ScriptAtPathAlreadyExists", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(ScriptAtPathAlreadyExistsError);
            }

            var resolvedPaths = SessionState.Path.GetResolvedPSPathFromPSPath(FilePath);
            if (resolvedPaths.Count != 1)
            {
                var exMessage = "Error: Could not resolve provided Path argument into a single path.";
                var ex = new PSArgumentException(exMessage);
                var InvalidPathArgumentError = new ErrorRecord(ex, "InvalidPathArgumentError", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(InvalidPathArgumentError);
            }

            var resolvedFilePath = resolvedPaths[0].Path;

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

            PSScriptFileInfo currentScriptInfo = new PSScriptFileInfo(
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
                description: Description);

            if (!currentScriptInfo.TryCreateScriptFileInfoString(
                filePath: resolvedFilePath,
                pSScriptFileString: out string psScriptFileContents,
                errors: out ErrorRecord[] errors))
            {
                foreach (ErrorRecord err in errors)
                {
                    WriteError(err);
                }

                return;
            }

            using(FileStream fs = File.Create(resolvedFilePath))
            {
                byte[] info = new UTF8Encoding(true).GetBytes(psScriptFileContents);
                fs.Write(info, 0, info.Length);
            }

            if (PassThru)
            {
                WriteObject(psScriptFileContents);
            }            
        }

        #endregion
    }
}
