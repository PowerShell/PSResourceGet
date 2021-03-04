// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Get-PSResourceRepository cmdlet replaces the Get-PSRepository cmdlet from V2.
    /// It searches for the PowerShell module repositories that are registered for the current user.
    /// By default it will return all registered repositories, or if the -Name parameter argument is specified then it wil return the repository with that name.
    /// It returns PSRepositoryItemInfo objects which describe each resource item found.
    /// </summary>

    using RepositorySettings = Microsoft.PowerShell.PowerShellGet.RepositorySettings.RepositorySettings;
    using PSRepositoryItem = Microsoft.PowerShell.PowerShellGet.PSRepositoryItem.PSRepositoryItem;

    [Cmdlet(VerbsCommon.Get,
        "PSResourceRepository",
        SupportsShouldProcess = true,
        HelpUri = "<add>")]
    public sealed
    class GetPSResourceRepository : PSCmdlet
    {
        #region Parameters
        /// <summary>
        /// Specifies the name(s) of a registered repository to find.
        /// Does not support wild card characters.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string[] Name
        {
            get
            { return _name; }

            set
            { _name = value; }
        }
        private string[] _name = new string[0];
        #endregion

        #region Methods
        protected override void ProcessRecord()
        {
            List<PSRepositoryItem> items = RepositorySettings.Read(_name);

            foreach (PSRepositoryItem repo in items)
            {
                WriteObject(repo);
            }
        }
        #endregion

    }
}
