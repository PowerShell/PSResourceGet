// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Management.Automation;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// Retrieve the contents of a .ps1 file
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "PSScriptFileInfo")]
    [OutputType(typeof(PSScriptFileInfo))]
    public sealed class GetPSScriptFileInfo : PSCmdlet
    {
        #region Parameters

        /// <summary>
        /// The path to the .ps1 file to retrieve.
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, HelpMessage = "Path (including file name) to the script file (.ps1 file) to retrieve and view.")]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; }

        #endregion

        #region Methods

        protected override void EndProcessing()
        {            
            if (!Path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                var exMessage = "The script file pathname must end with a .ps1 file extension. Example: C:/Users/john/x/MyScript.ps1";
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
            bool isValidScript = PSScriptFileInfo.TryTestPSScriptFile(
                scriptFileInfoPath: resolvedPath,
                parsedScript: out PSScriptFileInfo psScriptFileInfo,
                errors: out ErrorRecord[] errors,
                out string[] verboseMsgs);

            if (!isValidScript)
            {
                string fileName = System.IO.Path.GetFileName(resolvedPath);

                var exMessage = $"Error: '{fileName}' script file is invalid. The script file must include Version, Guid, Description and Author properties.";
                foreach (ErrorRecord error in errors)
                {
                    exMessage += Environment.NewLine + error.Exception.Message;
                }
                
                var ex = new PSArgumentException(exMessage);
                var InvalidPSScriptFileError = new ErrorRecord(ex, "InvalidPSScriptFile", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(InvalidPSScriptFileError);
            }

            PSObject psScriptFileInfoWithName = new PSObject(psScriptFileInfo);

            string Name = System.IO.Path.GetFileNameWithoutExtension(resolvedPath);
            psScriptFileInfoWithName.Properties.Add(new PSNoteProperty(nameof(Name), Name));

            foreach (string msg in verboseMsgs)
            {
                WriteVerbose(msg);
            }

            WriteObject(psScriptFileInfoWithName);
        }

        #endregion
    }
}
