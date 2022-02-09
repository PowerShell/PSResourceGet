using System.Net;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Microsoft.PowerShell.Commands;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// It retrieves a resource that was installed with Install-PSResource
    /// Returns a single resource or multiple resource.
    /// </summary>
    [Cmdlet(VerbsDiagnostic.Test, "PSScriptFileInfo")]
    public sealed class TestPSScriptFileInfo : PSCmdlet
    {
        #region Parameters

        /// <summary>
        /// The path to the .ps1 file to test
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; }

        #endregion

        #region Methods

        protected override void ProcessRecord()
        {
            if (!File.Exists(Path))
            {
                // .ps1 file at specified location already exists and Force parameter isn't used to rewrite the file
                var exMessage = "A file does not exist at the location specified";
                var ex = new ArgumentException(exMessage);
                var FileDoesNotExistError = new ErrorRecord(ex, "FileDoesNotExistAtPath", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(FileDoesNotExistError);
            }

            if (!Path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                var exMessage = "Path needs to end with a .ps1 file. Example: C:/Users/john/x/MyScript.ps1";
                var ex = new ArgumentException(exMessage);
                var InvalidPathError = new ErrorRecord(ex, "InvalidPath", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(InvalidPathError);   
            }

            var resolvedPaths = SessionState.Path.GetResolvedPSPathFromPSPath(Path);
            if (resolvedPaths.Count != 1)
            {
                var exMessage = "Error: Could not resolve provided Path argument into a single path.";
                var ex = new PSArgumentException(exMessage);
                var InvalidPathArgumentError = new ErrorRecord(ex, "InvalidPathArgumentError", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(InvalidPathArgumentError);
            }

            var resolvedPath = resolvedPaths[0].Path;

            bool isValidPSScriptFile = PSScriptFileInfo.TryParseScriptFileInfo(
                scriptFileInfo: resolvedPath,
                parsedScript: out PSScriptFileInfo parsedScriptInfo,
                moduleSpecErrors: out ErrorRecord[] errors,
                parsedPSScriptInfoHashtable: out Hashtable parsedHash,
                cmdletPassedIn: this);

            WriteVerbose("is valid: " + isValidPSScriptFile);







            // PSScriptFileInfo currentScriptInfo = new PSScriptFileInfo(
            //     version: Version,
            //     guid: Guid,
            //     author: Author,
            //     companyName: CompanyName,
            //     copyright: Copyright,
            //     tags: Tags,
            //     licenseUri: _licenseUri,
            //     projectUri: _projectUri,
            //     iconUri: _iconUri,
            //     requiredModules: RequiredModules,
            //     externalModuleDependencies: ExternalModuleDependencies,
            //     requiredScripts: RequiredScripts,
            //     externalScriptDependencies: ExternalScriptDependencies,
            //     releaseNotes: ReleaseNotes,
            //     privateData: PrivateData,
            //     description: Description,
            //     cmdletPassedIn: this);

            // if (!currentScriptInfo.TryCreateScriptFileInfoString(
            //     pSScriptFileString: out string psScriptFileContents,
            //     errors: out ErrorRecord[] errors))
            // {
            //     foreach (ErrorRecord err in errors)
            //     {
            //         WriteError(err);
            //     }

            //     return;
            //     // TODO: Anam, currently only one error and you return. So maybe this shouldn't be a list?
            //     // But for extensability makes sense.
            // }            
        }

        #endregion
    }
}
