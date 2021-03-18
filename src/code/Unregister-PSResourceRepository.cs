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
        protected override void ProcessRecord()
        {
            try
            {
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

            string[] errorMsgs;
            try
            {
                RepositorySettings.Remove(Name, out errorMsgs);
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Unable to successfully unregister repository. {0}", e.Message));
            }
            foreach (string error in errorMsgs)
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
