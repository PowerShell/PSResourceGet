// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

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
        public string[] EndOfFileContents { get; private set; } = Utils.EmptyStrArray;

        /// <summary>
        /// End of file contents for the .ps1 file.
        /// </summary>
        public bool ContainsSignature { get; set; } = false;

        #endregion

        #region Private Members

        private const string signatureStartString = "# SIG # Begin signature block";
        private int _signatureStartIndex = -1;

        #endregion

        #region Constructor

        /// <summary>
        /// This constructor takes end of file contents as a string and checks if it has a signature.
        /// </summary>
        public PSScriptContents(string[] endOfFileContents)
        {
            EndOfFileContents = endOfFileContents;
            ContainsSignature = CheckForSignature();
        }

        /// <summary>
        /// This constructor creates a PSScriptContents instance with default values for its properties.
        /// The calling method, like PSScriptContents.ParseContent() could then populate the properties.
        /// </summary>
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
                EndOfFileContents = commentLines;
                ContainsSignature = CheckForSignature();
            }
        }

        /// <summary>
        /// This function is called by PSScriptFileInfo.TryCreateScriptFileInfoString(),
        /// by the New-PSScriptFileInfo cmdlet (in which case EndOfFileContents is an empty string so there's no signature that'll get removed)
        /// or by Update-PSScriptFileInfo cmdlet (in which case EndOfFileContents may not be empty and may contain a signature.
        /// When emitting contents, any file signature is always removed because it is invalidated when the content is updated.
        /// </summary>
        internal string[] EmitContent()
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
            for (int i = 0; i < EndOfFileContents.Length; i++)
            {
                if (String.Equals(EndOfFileContents[i], signatureStartString, StringComparison.InvariantCultureIgnoreCase))
                {
                    _signatureStartIndex = i;
                }
            }

            return _signatureStartIndex != -1;
        }

        /// <summary>
        /// Removes the signature from EndOfFileContents property
        /// as the signature would be invalidated during update.
        /// </summary>
        private void RemoveSignatureString()
        {
            if (ContainsSignature)
            {
                string[] newEndOfFileContents = new string[_signatureStartIndex];
                Array.Copy(EndOfFileContents, newEndOfFileContents, _signatureStartIndex);
                EndOfFileContents = newEndOfFileContents;

                ContainsSignature = false;
            }
        }
        #endregion
    }
}
