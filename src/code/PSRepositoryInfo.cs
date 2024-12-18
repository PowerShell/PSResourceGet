// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Management.Automation;

namespace Microsoft.PowerShell.PSResourceGet.UtilClasses
{
    /// <summary>
    /// This class contains information for a repository item.
    /// </summary>
    public sealed class PSRepositoryInfo
    {
        #region constants
        internal const string MARPrefix = "psresource/";
        #endregion

        #region Enums

        public enum APIVersion
        {
            Unknown,
            V2,
            V3,
            Local,
            NugetServer,
            ContainerRegistry
        }

        public enum CredentialProviderType
        {
            None,
            AzArtifacts
        }

        #endregion

        #region Constructor

        public PSRepositoryInfo(string name, Uri uri, int priority, bool trusted, PSCredentialInfo credentialInfo, CredentialProviderType credentialProvider, APIVersion apiVersion, bool allowed)
        {
            Name = name;
            Uri = uri;
            Priority = priority;
            Trusted = trusted;
            CredentialInfo = credentialInfo;
            CredentialProvider = credentialProvider;
            ApiVersion = apiVersion;
            IsAllowedByPolicy = allowed;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The Name of the repository.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The Uri for the repository.
        /// </summary>
        public Uri Uri { get; }

        /// <summary>
        /// Whether the repository is trusted.
        /// </summary>
        public bool Trusted { get; }

        /// <summary>
        /// The priority of the repository.
        /// </summary>
        [ValidateRange(0, 100)]
        public int Priority { get; }

        /// <summary>
        /// The credential information for repository authentication.
        /// </summary>
        public PSCredentialInfo CredentialInfo { get; set; }

        /// <summary>
        /// Specifies which credential provider to use.
        /// </summary>
        public CredentialProviderType CredentialProvider { get; set; }

        /// <summary>
        /// The API protocol version for the repository.
        /// </summary>
        public APIVersion ApiVersion { get; }

        // <summary>
        /// Specifies whether the repository is allowed by policy.
        /// </summary>
        public bool IsAllowedByPolicy { get; set; }

        #endregion

        #region Methods

        internal bool IsMARRepository()
        {
            return (ApiVersion == APIVersion.ContainerRegistry && Uri.Host.Contains("mcr.microsoft.com"));
        }

        #endregion
    }
}
