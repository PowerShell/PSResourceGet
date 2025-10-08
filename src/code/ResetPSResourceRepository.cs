// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
using System.Management.Automation;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    /// <summary>
    /// The Reset-PSResourceRepository cmdlet creates a fresh repository store by deleting 
    /// the existing PSResourceRepository.xml file and creating a new one with only PSGallery registered.
    /// This is useful when the repository store becomes corrupted.
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
        /// When specified, displays the PSGallery repository that was registered.
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        #endregion

        #region Method overrides

        protected override void ProcessRecord()
        {
            string repositoryStorePath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PSResourceGet",
                "PSResourceRepository.xml");

            string actionMessage = $"Reset repository store at '{repositoryStorePath}'. This will delete all registered repositories and register only PSGallery.";
            
            if (!ShouldProcess(repositoryStorePath, actionMessage))
            {
                return;
            }

            try
            {
                WriteVerbose("Resetting repository store");
                PSRepositoryInfo psGallery = RepositorySettings.ResetRepositoryStore();
                WriteVerbose($"Repository store has been reset. PSGallery registered at: {repositoryStorePath}");

                if (PassThru)
                {
                    WriteObject(psGallery);
                }
            }
            catch (Exception e)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new PSInvalidOperationException($"Failed to reset repository store: {e.Message}", e),
                    "ResetRepositoryStoreFailed",
                    ErrorCategory.InvalidOperation,
                    this));
            }
        }

        #endregion
    }
}
