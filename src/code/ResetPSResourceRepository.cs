// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
using System.Management.Automation;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    /// <summary>
    /// The Reset-PSResourceRepository cmdlet resets the repository store by creating a new PSRepositories.xml file.
    /// This is useful when the repository store becomes corrupted.
    /// It will create a new repository store with only the PSGallery repository registered.
    /// </summary>
    [Cmdlet(VerbsCommon.Reset,
        "PSResourceRepository",
        SupportsShouldProcess = true,
        ConfirmImpact = ConfirmImpact.High)]
    [OutputType(typeof(PSRepositoryInfo))]
    public sealed class ResetPSResourceRepository : PSCmdlet
    {
        #region Parameters

        /// <summary>
        /// When specified, displays the PSGallery repository that was registered after reset
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        #endregion

        #region Methods

        protected override void ProcessRecord()
        {
            string repositoryStorePath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PSResourceGet",
                "PSResourceRepository.xml");

            WriteVerbose($"Resetting repository store at: {repositoryStorePath}");
            
            if (!ShouldProcess(repositoryStorePath, "Reset repository store and create new PSRepositories.xml file with PSGallery registered"))
            {
                return;
            }

            PSRepositoryInfo psGalleryRepo = RepositorySettings.Reset(out string errorMsg);

            if (!string.IsNullOrEmpty(errorMsg))
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(errorMsg),
                    "ErrorResettingRepositoryStore",
                    ErrorCategory.InvalidOperation,
                    this));
                return;
            }

            WriteVerbose("Repository store reset successfully. PSGallery has been registered.");

            if (PassThru)
            {
                WriteObject(psGalleryRepo);
            }
        }

        #endregion
    }
}
