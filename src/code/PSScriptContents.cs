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
        public string[] ScriptContents { get; private set; } = Utils.EmptyStrArray;

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
            ScriptContents = endOfFileContents;
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
                ScriptContents = commentLines;
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
            return ScriptContents;
        }

        #endregion

        #region Private Methods
        
        /// <summary>
        /// Checks if the end of file contents contain a signature.
        /// </summary>
        private bool CheckForSignature()
        {
            for (int i = 0; i < ScriptContents.Length; i++)
            {
                if (String.Equals(ScriptContents[i], signatureStartString, StringComparison.InvariantCultureIgnoreCase))
                {
                    _signatureStartIndex = i;
                    break;
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
                // The script signature comment block always appears at the end of the script file, 
                // so its start location becomes the end of the content section after the signature 
                // comment block is removed, and is also the length of the content section minus the 
                // signature block.
                string[] contentsWithoutSignature = new string[_signatureStartIndex];
                Array.Copy(ScriptContents, contentsWithoutSignature, _signatureStartIndex);
                ScriptContents = contentsWithoutSignature;

                ContainsSignature = false;
            }
        }
        #endregion
    }
}
