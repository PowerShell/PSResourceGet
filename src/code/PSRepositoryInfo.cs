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

        public PSRepositoryInfo(string name, Uri url, int priority, bool trusted)
        {
            Name = name;
            Url = url;
            Priority = priority;
            Trusted = trusted;
        }

        #endregion

        #region Properties

        /// <summary>
        /// the Name of the repository
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// the Url for the repository
        /// </summary>
        public Uri Url { get; }

        /// <summary>
        /// whether the repository is trusted
        public bool Trusted { get; }

        /// <summary>
        /// the priority of the repository
        /// </summary>
        [ValidateRange(0, 50)]
        public int Priority { get; }

        #endregion
    }
}
