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
        public bool ContainsSignature { get; set; }

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

        #endregion

        #region Public Methods

        public void ParseContent(string[] commentLines, out ErrorRecord[] errors, bool removeSignature)
        {
            errors = null;
            EndOfFileContents = String.Join("", commentLines);
            ContainsSignature = CheckForSignature();
        }

        /// <summary>
        /// Checks if end of file contents contains a Signature
        /// </summary>
        public bool ValidateContent()
        {
            // if (ContainsSignature)
            // {
            //     // todo: write warning somewhere, change state of ContainsSignature or should it reflect original file state?
            //     // RemoveSignatureString();

            // }

            // return true;
            return !ContainsSignature;
        }

        public string EmitContent()
        {
            return EndOfFileContents;
        }

        

        #endregion

        #region Private Methods
        
        /// <summary>
        /// Checks if the end of file contents contain a signature
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
            }

            // TODO: should I set ContainsSignature to false now?
        }
        #endregion
    }
}