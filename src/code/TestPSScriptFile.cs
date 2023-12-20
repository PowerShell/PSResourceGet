// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Management.Automation;

namespace Microsoft.PowerShell.PSResourceGet
{
    /// <summary>
    /// Tests the contents of a .ps1 file to see if it has all properties and is in correct format
    /// for publishing the script with the file.
    /// </summary>
    [Cmdlet(VerbsDiagnostic.Test, "PSScriptFileInfo")]
    [OutputType(typeof(bool))]
    public sealed class TestPSScriptFileInfo : PSCmdlet
    {
        #region Parameters

        /// <summary>
        /// The path to the .ps1 file to test.
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, HelpMessage = "Path (including file name) to the script file (.ps1 file) to test.")]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; }

        #endregion

        #region Methods

        protected override void EndProcessing()
        {
            if (!Path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException("Path needs to end with a .ps1 file. Example: C:/Users/john/x/MyScript.ps1"),
                    "InvalidPath",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            var resolvedPaths = GetResolvedProviderPathFromPSPath(Path, out ProviderInfo provider);
            if (resolvedPaths.Count != 1)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new PSArgumentException("Could not resolve provided path argument to a single path."),
                    "InvalidPathArgumentError",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            var resolvedPath = resolvedPaths[0];

            if (!File.Exists(resolvedPath))
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException("A .ps1 file does not exist at the location specified."),
                    "FileDoesNotExistAtPath",
                    ErrorCategory.InvalidArgument,
                    this));
            }
            WriteDebug($"Resolved path is '{resolvedPath}'");

            bool isValidScript = PSScriptFileInfo.TryTestPSScriptFileInfo(
                scriptFileInfoPath: resolvedPath,
                parsedScript: out PSScriptFileInfo _,
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

            WriteObject(isValidScript);
        }

        #endregion
    }
}
