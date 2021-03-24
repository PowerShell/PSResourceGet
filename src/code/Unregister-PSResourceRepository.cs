using System;
using System.Management.Automation;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Unregister-PSResourceRepository cmdlet replaces the Unregister-PSRepository cmdlet from V2.
    /// It unregisters a repository for the current user.
    /// </summary>

    [Cmdlet(VerbsLifecycle.Unregister,
        "PSResourceRepository",
        SupportsShouldProcess = true,
        HelpUri = "<add>")]
    public sealed
    class UnregisterPSResourceRepository : PSCmdlet
    {
        #region Parameters

        /// <summary>
        /// Specifies the desired name for the repository to be registered.
        /// </summary>
        [Parameter(Mandatory= true, Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; } = new string[0];
        #endregion

        #region Methods
        protected override void BeginProcessing()
        {
            try
            {
                WriteDebug("Calling API to check repository store exists in non-corrupted state");
                RepositorySettings.CheckRepositoryStore();
            }
            catch (PSInvalidOperationException e)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new PSNotImplementedException(e.Message),
                    "RepositoryStoreException",
                    ErrorCategory.ReadError,
                    this));
            }
        }
        protected override void ProcessRecord()
        {
            WriteDebug(String.Format("removing repository {0}. Calling Remove() API now", Name));
            if (!ShouldProcess("Unregister repository"))
            {
                return;
            }
            RepositorySettings.Remove(Name, out string[] errorList);

            // handle non-terminating errors
            foreach (string error in errorList)
            {
                if (!String.IsNullOrEmpty(error))
                {
                    WriteError(new ErrorRecord(
                        new PSInvalidOperationException(error),
                        "ErrorUnregisteringSpecifiedRepo",
                        ErrorCategory.InvalidOperation,
                        this));
                }
            }
        }
        #endregion
    }
}
