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
        #region Enums

        public enum APIVersion
        {
            Unknown,
            v2,
            v3,
            v4
        }

        #endregion

        #region Constructor

        public PSRepositoryInfo(string name, Uri uri, int priority, bool trusted, PSCredentialInfo credentialInfo, APIVersion apiVersion)
        {
            Name = name;
            Uri = uri;
            Priority = priority;
            Trusted = trusted;
            CredentialInfo = credentialInfo;
            ApiVersion = apiVersion;
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
        /// the credential information for repository authentication
        /// </summary>
        public PSCredentialInfo CredentialInfo { get; }

        /// <summary>
        /// the API protocol version for the repository
        /// </summary>
        public APIVersion ApiVersion { get; }

        #endregion
    }
}
