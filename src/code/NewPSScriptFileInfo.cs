using System.Runtime.CompilerServices;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;

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
        [Parameter(Position = 0)]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; }

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
        /// TODO: in V2 this had type Object[]
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string[] RequiredModules { get; set; }

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
        /// If specified, passes the contents of the created .ps1 file to the console
        /// If -Path is not specified, then .ps1 contents will just be written out for the user
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
            WriteVerbose("In Anam's cmdlet!");
            // validate Uri related parameters passed in as strings
            if (!String.IsNullOrEmpty(ProjectUri) && !Utils.TryCreateValidUrl(uriString: ProjectUri,
                cmdletPassedIn: this,
                uriResult: out _projectUri,
                errorRecord: out ErrorRecord projectErrorRecord))
            {
                ThrowTerminatingError(projectErrorRecord);
            }

            if (!String.IsNullOrEmpty(LicenseUri) && !Utils.TryCreateValidUrl(uriString: LicenseUri,
                cmdletPassedIn: this,
                uriResult: out _licenseUri,
                errorRecord: out ErrorRecord licenseErrorRecord))
            {
                ThrowTerminatingError(licenseErrorRecord);
            }

            if (!String.IsNullOrEmpty(IconUri) && !Utils.TryCreateValidUrl(uriString: IconUri,
                cmdletPassedIn: this,
                uriResult: out _iconUri,
                errorRecord: out ErrorRecord iconErrorRecord))
            {
                ThrowTerminatingError(iconErrorRecord);
            }

            WriteVerbose("past Uri validation");

            // determine whether script contents will be written out to .ps1 file or to console
            // case 1: Path passed in. If path already exists Force required to overwrite. Else write error.
            // case 2: no Path passed in. PassThru required to print script file contents to console.
            if (!String.IsNullOrEmpty(Path) && !Path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                var exMessage = "Path needs to end with a .ps1 file. Example: C:/Users/john/x/MyScript.ps1";
                var ex = new ArgumentException(exMessage);
                var InvalidPathError = new ErrorRecord(ex, "InvalidPath", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(InvalidPathError);
            }
            
            if (!String.IsNullOrEmpty(Path) && File.Exists(Path) && !Force)
            {
                // .ps1 file at specified location already exists and Force parameter isn't used to rewrite the file
                var exMessage = ".ps1 file at specified path already exists. Specify a different location or use -Force parameter to overwrite the .ps1 file.";
                var ex = new ArgumentException(exMessage);
                var ScriptAtPathAlreadyExistsError = new ErrorRecord(ex, "ScriptAtPathAlreadyExists", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(ScriptAtPathAlreadyExistsError);
            }

            WriteVerbose("past path validation stuff");

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
                externalModuleDependencies: ExternalModuleDependencies,
                requiredScripts: RequiredScripts,
                externalScriptDependencies: ExternalScriptDependencies,
                releaseNotes: ReleaseNotes,
                privateData: PrivateData,
                description: Description);

            if (currentScriptInfo.TryCreatePSScriptInfoString(out string psScriptInfoString))
            {
                WriteVerbose(psScriptInfoString);
            }

            // get PSScriptInfo string --> take PSScriptInfo comment keys (from params passed in) and validate and turn into string
            // get Requires string --> from params passed in
            // get CommentHelpInfo string --> from params passed in
            
            // commpose totalMetadata string, which contains PSScriptInfo string + Requires string + CommentHelpInfo string

            // write to file at path. Call Test-ScriptFileInfo.
            // If PassThru write content of file
            
        }

        #endregion
    }
}
