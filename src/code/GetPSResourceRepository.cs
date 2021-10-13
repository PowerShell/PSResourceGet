// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Get-PSResourceRepository cmdlet replaces the Get-PSRepository cmdlet from V2.
    /// It searches for the PowerShell module repositories that are registered for the current user.
    /// By default it will return all registered repositories, or if the -Name parameter argument is specified then it wil return the repository with that name.
    /// It returns PSRepositoryInfo objects which describe each resource item found.
    /// </summary>

    [Cmdlet(VerbsCommon.Get,
        "PSResourceRepository",
        HelpUri = "<add>")]
    public sealed
    class GetPSResourceRepository : PSCmdlet
    {
        #region Parameters

        /// <summary>
        /// Specifies the name(s) of a registered repository to find.
        /// Supports wild card characters.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true)]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; } = Utils.EmptyStrArray;

        #endregion

        #region Methods

        protected override void BeginProcessing()
        {
            RepositorySettings.CheckRepositoryStore();
        }
        protected override void ProcessRecord()
        {
            string nameArrayAsString = (Name == null || !Name.Any() || string.Equals(Name[0], "*") || Name[0] == null) ? "all" : string.Join(", ", Name);
            WriteVerbose(String.Format("reading repository: {0}. Calling Read() API now", nameArrayAsString));
            List<PSRepositoryInfo> items = RepositorySettings.Read(Name, out string[] errorList);

            // handle non-terminating errors
            foreach (string error in errorList)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(error),
                    "ErrorGettingSpecifiedRepo",
                    ErrorCategory.InvalidOperation,
                    this));
            }

            foreach (PSRepositoryInfo repo in items)
            {
                WriteObject(repo);
            }
        }

        #endregion
    }
}
