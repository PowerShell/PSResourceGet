// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Management.Automation;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// Retrieve the contents of a .ps1 file
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "PSScriptFileInfo")]
    public sealed class GetPSScriptFileInfo : PSCmdlet
    {
        #region Parameters

        /// <summary>
        /// The path to the .ps1 file to retrieve.
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; }

        #endregion

        #region Methods

        protected override void EndProcessing()
        {            
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

            if (!File.Exists(resolvedPath))
            {
                var exMessage = "A .ps1 file does not exist at the location specified.";
                var ex = new ArgumentException(exMessage);
                var FileDoesNotExistError = new ErrorRecord(ex, "FileDoesNotExistAtPath", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(FileDoesNotExistError);
            }

            bool isValidScript = PSScriptFileInfo.TryTestPSScriptFile(
                scriptFileInfoPath: resolvedPath,
                parsedScript: out PSScriptFileInfo psScriptFileInfo,
                errors: out ErrorRecord[] errors,
                out string[] verboseMsgs);

            if (!isValidScript)
            {
                foreach (ErrorRecord error in errors)
                {
                    WriteVerbose("The .ps1 script file passed in was not valid due to: " + error.Exception.Message);
                }
            }

            foreach (string msg in verboseMsgs)
            {
                WriteVerbose(msg);
            }

            PSObject psScriptFileInfoWithName = new PSObject(psScriptFileInfo);

            string Name = System.IO.Path.GetFileNameWithoutExtension(Path);
            psScriptFileInfoWithName.Properties.Add(new PSNoteProperty(nameof(Name), Name));

            WriteObject(psScriptFileInfoWithName);
        }

        #endregion
    }
}
