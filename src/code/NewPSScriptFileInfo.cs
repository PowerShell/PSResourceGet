// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.IO;
using System.Management.Automation;
using Microsoft.PowerShell.Commands;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// Creates a new .ps1 file with script information required for publishing a script.
    /// </summary>
    [Cmdlet(VerbsCommon.New, "PSScriptFileInfo")]
    public sealed class NewPSScriptFileInfo : PSCmdlet
    {
        #region Parameters

        /// <summary>
        /// The path the .ps1 script info file will be created at.
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string FilePath { get; set; }

        /// <summary>
        /// The version of the script.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string Version { get; set; }

        /// <summary>
        /// The author of the script.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string Author { get; set; }

        /// <summary>
        /// The description of the script.
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string Description { get; set; }

        /// <summary>
        /// A unique identifier for the script. The GUID can be used to distinguish among scripts with the same name.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public Guid Guid { get; set; }

        /// <summary>
        /// The name of the company owning the script.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string CompanyName { get; set; }

        /// <summary>
        /// The copyright statement for the script.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string Copyright { get; set; }

        /// <summary>
        /// The list of modules required by the script.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public Hashtable[] RequiredModules { get; set; }

        /// <summary>
        /// The list of external module dependencies taken by this script.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string[] ExternalModuleDependencies { get; set; }

        /// <summary>
        /// The list of scripts required by the script.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string[] RequiredScripts { get; set; }

        /// <summary>
        /// The list of external script dependencies taken by this script.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string[] ExternalScriptDependencies { get; set; }

        /// <summary>
        /// The tags associated with the script.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string[] Tags { get; set; }

        /// <summary>
        /// The Uri for the project associated with the script.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string ProjectUri { get; set; }

        /// <summary>
        /// The Uri for the license associated with the script.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string LicenseUri { get; set; }

        /// <summary>
        /// The Uri for the icon associated with the script.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string IconUri { get; set; }

        /// <summary>
        /// The release notes for the script.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string ReleaseNotes { get; set; }

        /// <summary>
        /// The private data associated with the script.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string PrivateData { get; set; }

        /// <summary>
        /// If used with Path parameter and .ps1 file specified at the path exists, it rewrites the file.
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        #endregion

        #region Methods

        protected override void EndProcessing()
        {
            // validate Uri related parameters passed in as strings
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
                var exMessage = "Path needs to end with a .ps1 file. Example: C:/Users/john/x/MyScript.ps1";
                var ex = new ArgumentException(exMessage);
                var InvalidPathError = new ErrorRecord(ex, "InvalidPath", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(InvalidPathError);   
            }

            var resolvedFilePath = SessionState.Path.GetUnresolvedProviderPathFromPSPath(FilePath);
            if (String.IsNullOrEmpty(resolvedFilePath))
            {
                var exMessage = "Error: Could not resolve provided Path argument into a single path.";
                var ex = new PSArgumentException(exMessage);
                var InvalidPathArgumentError = new ErrorRecord(ex, "InvalidPathArgumentError", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(InvalidPathArgumentError);
            }
            
            if (File.Exists(resolvedFilePath) && !Force)
            {
                // .ps1 file at specified location already exists and Force parameter isn't used to rewrite the file
                var exMessage = ".ps1 file at specified path already exists. Specify a different location or use -Force parameter to overwrite the .ps1 file.";
                var ex = new ArgumentException(exMessage);
                var ScriptAtPathAlreadyExistsError = new ErrorRecord(ex, "ScriptAtPathAlreadyExists", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(ScriptAtPathAlreadyExistsError);
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

            PSScriptFileInfo scriptInfo = new PSScriptFileInfo(
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
                description: Description);

            if (!scriptInfo.TryCreateScriptFileInfoString(
                psScriptFileContents: out string[] psScriptFileContents,
                errors: out ErrorRecord[] errors))
            {
                foreach (ErrorRecord err in errors)
                {
                    WriteError(err);
                }

                return;
            }

            File.WriteAllLines(resolvedFilePath, psScriptFileContents);       
        }

        #endregion
    }
}
