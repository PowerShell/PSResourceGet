// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Management.Automation;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
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
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException("The script file pathname must end with a .ps1 file extension. Example: C:/Users/john/x/MyScript.ps1"),
                    "InvalidPath",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            var resolvedPaths = GetResolvedProviderPathFromPSPath(Path, out ProviderInfo provider);
            if (resolvedPaths.Count != 1)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new PSArgumentException("Error: Could not resolve provided Path argument into a single path."),
                    "InvalidPathArgumentError",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            var resolvedPath = resolvedPaths[0];
            bool isValidScript = PSScriptFileInfo.TryTestPSScriptFileInfo(
                scriptFileInfoPath: resolvedPath,
                parsedScript: out PSScriptFileInfo psScriptFileInfo,
                errors: out ErrorRecord[] errors,
                out string[] debugMsgs);

            if (!isValidScript)
            {
                string fileName = System.IO.Path.GetFileName(resolvedPath);

                var exMessage = $"Error: '{fileName}' script file is invalid. The script file must include Version, Guid, Description and Author properties.";
                foreach (ErrorRecord error in errors)
                {
                    exMessage += Environment.NewLine + error.Exception.Message;
                }
                
                ThrowTerminatingError(new ErrorRecord(
                    new PSArgumentException(exMessage),
                    "InvalidPSScriptFile",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            PSObject psScriptFileInfoWithName = new PSObject(psScriptFileInfo);

            string Name = System.IO.Path.GetFileNameWithoutExtension(resolvedPath);
            psScriptFileInfoWithName.Properties.Add(new PSNoteProperty(nameof(Name), Name));

            foreach (string msg in debugMsgs)
            {
                WriteDebug(msg);
            }

            WriteObject(psScriptFileInfoWithName);
        }

        #endregion
    }
}
