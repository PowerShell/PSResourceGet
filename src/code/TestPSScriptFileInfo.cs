using System.Net;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Microsoft.PowerShell.Commands;
using System.Collections.ObjectModel;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// It retrieves a resource that was installed with Install-PSResource
    /// Returns a single resource or multiple resource.
    /// </summary>
    [Cmdlet(VerbsDiagnostic.Test, "PSScriptFileInfo")]
    [OutputType(typeof(bool))]
    public sealed class TestPSScriptFileInfo : PSCmdlet
    {
        #region Parameters

        /// <summary>
        /// The path to the .ps1 file to test
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string FilePath { get; set; }

        #endregion

        #region Methods

        protected override void ProcessRecord()
        {
            if (!FilePath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                var exMessage = "Path needs to end with a .ps1 file. Example: C:/Users/john/x/MyScript.ps1";
                var ex = new ArgumentException(exMessage);
                var InvalidPathError = new ErrorRecord(ex, "InvalidPath", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(InvalidPathError);   
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

            if (!File.Exists(resolvedFilePath))
            {
                var exMessage = "A file does not exist at the location specified";
                var ex = new ArgumentException(exMessage);
                var FileDoesNotExistError = new ErrorRecord(ex, "FileDoesNotExistAtPath", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(FileDoesNotExistError);
            }

            bool isValidScript = PSScriptFileInfo.TryParseScriptFile(
                scriptFileInfoPath: resolvedFilePath,
                parsedScript: out PSScriptFileInfo parsedScriptInfo,
                errors: out ErrorRecord[] errors);

            if (!isValidScript)
            {
                foreach (ErrorRecord error in errors)
                {
                    WriteWarning("The .ps1 script file passed in was not valid due to: " + error.Exception.Message);
                }
            }

            WriteObject(isValidScript);      
        }

        #endregion
    }
}
