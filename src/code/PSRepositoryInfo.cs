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
        internal const string MARPrefix = "azure-powershell/";
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

        #endregion

        #region Constructor

        public PSRepositoryInfo(string name, Uri uri, int priority, bool trusted, PSCredentialInfo credentialInfo, APIVersion apiVersion, bool allowed)
        {
            Name = name;
            Uri = uri;
            Priority = priority;
            Trusted = trusted;
            CredentialInfo = credentialInfo;
            ApiVersion = apiVersion;
            IsAllowedByPolicy = allowed;
        }

        #endregion

        #region Enum

        public enum RepositoryProviderType
        {
            None,
            ACR,
            AzureDevOps
        }

        #endregion

        #region Properties

        /// <summary>
        /// the Name of the repository
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// the Uri for the repository
        /// </summary>
        public Uri Uri { get; }

        /// <summary>
        /// whether the repository is trusted
        /// </summary>
        public bool Trusted { get; }

        /// <summary>
        /// the priority of the repository
        /// </summary>
        [ValidateRange(0, 100)]
        public int Priority { get; }

        /// <summary>
        /// the type of repository provider (eg, AzureDevOps, ContainerRegistry, etc.)
        /// </summary>
        public RepositoryProviderType RepositoryProvider { get; }

        /// <summary>
        /// the credential information for repository authentication
        /// </summary>
        public PSCredentialInfo CredentialInfo { get; }

        /// <summary>
        /// the API protocol version for the repository
        /// </summary>
        public APIVersion ApiVersion { get; }

        // <summary>
        /// is it allowed by policy
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
