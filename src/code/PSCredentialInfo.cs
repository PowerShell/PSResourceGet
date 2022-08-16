// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Management.Automation;
using System.Net;
using System.Security;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    /// <summary>
    /// This class contains information for a repository's authentication credential.
    /// </summary>
    public sealed class PSCredentialInfo
    {
        #region Constructor

        /// <summary>
        /// Initializes a new instance of the PSCredentialInfo class with
        /// vaultName and secretName of type string
        /// </summary>
        /// <param name="vaultName"></param>
        /// <param name="secretName"></param>
        public PSCredentialInfo(string vaultName, string secretName)
        {
            VaultName = vaultName;
            SecretName = secretName;
            Credential = null;
        }

        /// <summary>
        /// Initializes a new instance of the PSCredentialInfo class with
        /// vaultName and secretName of type string, and
        /// credential of type PSCredential.
        /// </summary>
        /// <param name="vaultName"></param>
        /// <param name="secretName"></param>
        /// <param name="credential"></param>
        public PSCredentialInfo(string vaultName, string secretName, PSCredential credential)
        {
            VaultName = vaultName;
            SecretName = secretName;
            Credential = credential;
        }

        /// <summary>
        /// Initializes a new instance of the PSCredentialInfo class with
        /// vaultName and secretName of type string, and
        /// credential of type String.
        /// </summary>
        /// <param name="vaultName"></param>
        /// <param name="secretName"></param>
        /// <param name="credential"></param>
        public PSCredentialInfo(string vaultName, string secretName, string credential)
        {
            VaultName = vaultName;
            SecretName = secretName;
            Credential = new PSCredential("PSGetUser", new NetworkCredential("", credential).SecurePassword);
        }

        /// <summary>
        /// Initializes a new instance of the PSCredentialInfo class with
        /// vaultName and secretName of type string, and
        /// credential of type SecureString.
        /// </summary>
        /// <param name="vaultName"></param>
        /// <param name="secretName"></param>
        /// <param name="credential"></param>
        public PSCredentialInfo(string vaultName, string secretName, SecureString credential)
        {
            VaultName = vaultName;
            SecretName = secretName;
            Credential = new PSCredential("PSGetUser", credential);
        }

        /// <summary>
        /// Initializes a new instance of the PSCredentialInfo class with
        /// vaultName and secretName of type string, and
        /// (optionally) credential of type PSCredential from a PSObject.
        /// </summary>
        /// <param name="psObject"></param>
        public PSCredentialInfo(PSObject psObject)
        {
            if (psObject == null)
            {
                throw new ArgumentNullException(nameof(psObject));
            }

            VaultName = (string) psObject.Properties[PSCredentialInfo.VaultNameAttribute]?.Value;
            SecretName = (string) psObject.Properties[PSCredentialInfo.SecretNameAttribute]?.Value;
            Credential = (PSCredential) psObject.Properties[PSCredentialInfo.CredentialAttribute]?.Value;
        }

        #endregion

        #region Members

        private string _vaultName;
        /// <summary>
        /// the Name of the SecretManagement Vault
        /// </summary>
        public string VaultName {
            get
            {
                return _vaultName;
            }

            private set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException($"Invalid CredentialInfo, {PSCredentialInfo.VaultNameAttribute} must be a non-empty string");
                }

                _vaultName = value;
            }
        }

        private string _secretName;
        /// <summary>
        /// the Name of the Secret
        /// </summary>
        public string SecretName {
            get
            {
                return _secretName;
            }

            private set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException($"Invalid CredentialInfo, {PSCredentialInfo.SecretNameAttribute} must be a non-empty string");
                }

                _secretName = value;
            }
        }

        /// <summary>
        /// optional Credential object to save in a SecretManagement Vault
        /// for authenticating to repositories
        /// </summary>
        public PSCredential Credential { get; private set; }

        internal static readonly string VaultNameAttribute = nameof(VaultName);
        internal static readonly string SecretNameAttribute = nameof(SecretName);
        internal static readonly string CredentialAttribute = nameof(Credential);

        #endregion
    }
}
