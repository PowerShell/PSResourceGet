// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Management.Automation;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    /// <summary>
    /// This class contains information for a repository item.
    /// </summary>
    public sealed class PSRepositoryInfo
    {
        #region Constructor

        public PSRepositoryInfo(
            string name, 
            Uri uri, 
            int priority, 
            bool trusted, 
            RepositoryProviderType repositoryProvider, 
            PSCredentialInfo credentialInfo)
        {
            Name = name;
            Uri = uri;
            Priority = priority;
            Trusted = trusted;
            RepositoryProvider = repositoryProvider;
            CredentialInfo = credentialInfo;
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
        /// the type of repository provider (eg, AzureDevOps, ACR, etc.)
        /// </summary>
        public RepositoryProviderType RepositoryProvider { get; }

        /// <summary>
        /// the credential information for repository authentication
        /// </summary>
        public PSCredentialInfo CredentialInfo { get; }

        #endregion
    }
}
