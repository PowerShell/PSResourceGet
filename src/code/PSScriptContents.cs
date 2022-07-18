// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.ObjectModel;
using Microsoft.PowerShell.Commands;
using NuGet.Versioning;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    /// <summary>
    /// This class contains information for a PSScriptFileInfo (representing a .ps1 file contents).
    /// </summary>
    public sealed class PSScriptContents
    {
        #region Properties

        /// <summary>
        /// End of file contents for the .ps1 file.
        /// </summary>
        public string EndOfFileContents { get; private set; } = String.Empty;

        /// <summary>
        /// End of file contents for the .ps1 file.
        /// </summary>
        public bool ContainsSignature { get; set; } = false;

        #endregion

        #region Private Members

        private const string signatureStartString = "# SIG # Begin signature block";

        #endregion

        #region Constructor

        public PSScriptContents(string endOfFileContents)
        {
            this.EndOfFileContents = endOfFileContents;
            this.ContainsSignature = CheckForSignature();
        }

        internal PSScriptContents() {}

        #endregion

        #region Internal Methods

        /// <summary>
        /// Parses end of file contents as a string from the file lines passed in
        /// and sets property indicating whether those contents contain a signature.
        /// </summary>
        internal void ParseContent(string[] commentLines)
        {
            if (commentLines.Length != 0)
            {
                EndOfFileContents = String.Join("", commentLines);
                ContainsSignature = CheckForSignature();
            }
        }

        /// <summary>
        /// This function would be called by PSScriptFileInfo.TryCreateScriptFileInfoString(),
        /// by the New-PSScriptFileInfo cmdlet (in which case EndOfFileContents is an empty string so there's no signature that'll get removed)
        /// or by Update-PSScriptFileInfo cmdlet (in which case EndOfFileContents may not be empty and may contain a signature.
        /// The Update cmdlet checks for -RemoveSignature before control reaches this method).
        /// </summary>
        internal string EmitContent()
        {
            RemoveSignatureString();
            return EndOfFileContents;
        }

        #endregion

        #region Private Methods
        
        /// <summary>
        /// Checks if the end of file contents contain a signature.
        /// </summary>
        private bool CheckForSignature()
        {
            return (EndOfFileContents.Contains(signatureStartString));
        }

        /// <summary>
        /// Removes the signature from EndOfFileContents property
        /// as the signature would be invalidated during update.
        /// </summary>
        private void RemoveSignatureString()
        {
            if (ContainsSignature)
            {
                int signatureStartIndex = EndOfFileContents.IndexOf(signatureStartString);
                EndOfFileContents = EndOfFileContents.Substring(0, signatureStartIndex);
                ContainsSignature = false;
            }
        }
        #endregion
    }
}