// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// It retrieves a resource that was installed with Install-PSResource
    /// Returns a single resource or multiple resource.
    /// </summary>
    [Cmdlet(VerbsCommon.New, "ScriptFileInfo")]
    public sealed class NewScriptFileInfo : PSCmdlet
    {
        #region Members

        private VersionRange _versionRange;
        private List<string> _pathsToSearch;

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
        public Uri ProjectUri { get; set; }

        /// <summary>
        /// The Uri for the license associated with the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public Uri LicenseUri { get; set; }

        /// <summary>
        /// The Uri for the icon associated with the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public Uri IconUri { get; set; }

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

        #endregion

        #region Methods

        protected override void BeginProcessing()
        {
            // do Uri's come in as string or Uri?
            // validate if it comes in as string

            // validate string           
        }

        protected override void ProcessRecord()
        {
            // if not Path, is PassThru provided?
            // what does PassThru do in lieu of Path?

            // for all non mandatory params, set default value

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
