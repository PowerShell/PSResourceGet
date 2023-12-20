// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    /// <summary>
    /// The Unregister-PSResourceRepository cmdlet replaces the Unregister-PSRepository cmdlet from V2.
    /// It unregisters a repository for the current user.
    /// </summary>

    [Cmdlet(VerbsLifecycle.Unregister,
        "PSResourceRepository",
        SupportsShouldProcess = true)]
    public sealed
    class UnregisterPSResourceRepository : PSCmdlet
    {
        #region Parameters

        /// <summary>
        /// Specifies the desired name for the repository to be registered.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Name(s) of the repository to uninstall.")]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; } = Utils.EmptyStrArray;

        /// <summary>
        /// When specified, displays the repositories that were just unregistered
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        #endregion

        #region Method overrides

        protected override void BeginProcessing()
        {
            RepositorySettings.CheckRepositoryStore();
        }
        protected override void ProcessRecord()
        {
            Name = Utils.ProcessNameWildcards(Name, removeWildcardEntries:false, out string[] _, out bool nameContainsWildcard);
            if (nameContainsWildcard)
            {
                WriteError(new ErrorRecord(
                    new ArgumentException($"Name: '{String.Join(", ", Name)}' cannot contain wildcards"),
                    "nameContainsWildCardError",
                    ErrorCategory.ReadError,
                    this));

                return;
            }

            string nameArrayAsString = string.Join(", ", Name);
            WriteVerbose($"Removing repository '{nameArrayAsString}'");
            if (!ShouldProcess(nameArrayAsString, "Unregister repositories from repository store"))
            {
                return;
            }

            List<PSRepositoryInfo> removedRepositories = RepositorySettings.Remove(Name, out string[] errorList);

            // handle non-terminating errors
            foreach (string error in errorList)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(error),
                    "ErrorUnregisteringSpecifiedRepo",
                    ErrorCategory.InvalidOperation,
                    this));
            }

            if (PassThru)
            {
                foreach (PSRepositoryInfo repository in removedRepositories)
                {
                    WriteObject(repository);
                }
            }
        }

        #endregion
    }
}
