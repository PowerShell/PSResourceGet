using System;
using System.Management.Automation;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Unregister-PSResourceRepository cmdlet replaces the Unregister-PSRepository cmdlet from V2.
    /// It unregisters a repository for the current user.
    /// </summary>

    using RepositorySettings = Microsoft.PowerShell.PowerShellGet.RepositorySettings.RepositorySettings;

    [Cmdlet(VerbsLifecycle.Unregister,
        "PSResourceRepository",
        DefaultParameterSetName = "NameParameterSet",
        SupportsShouldProcess = true,
        HelpUri = "<add>",
        RemotingCapability = RemotingCapability.None)]
    public sealed
    class UnregisterPSResourceRepository : PSCmdlet
    {
        #region Parameters
        /// <summary>
        /// Specifies the desired name for the repository to be registered.
        /// </summary>
        [Parameter(Mandatory= true, Position = 0, ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string[] Name
        {
            get
            { return _name; }

            set
            { _name = value; }
        }
        private string[] _name;
        #endregion

        #region Methods
        protected override void ProcessRecord()
        {
            var r = new RepositorySettings();
            try{
                r.Remove(_name);
            }
            catch(Exception e)
            {
                throw new Exception(string.Format("Unable to successfully unregister repository: {0}", e.Message));
            }
        }
        #endregion
    }
}
