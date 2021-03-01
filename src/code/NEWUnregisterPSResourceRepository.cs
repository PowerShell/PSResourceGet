using System;
using System.Collections;
using System.Management.Automation;
using System.Globalization;
using Microsoft.PowerShell.PowerShellGet.NEWRepositorySettings;
using Microsoft.PowerShell.PowerShellGet.NEWPSRepositoryItem;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// todo: fill
    /// </summary>

    [Cmdlet(VerbsLifecycle.Unregister,
        "NEWPSResourceRepository",
        DefaultParameterSetName = "NameParameterSet",
        SupportsShouldProcess = true,
        HelpUri = "<add>",
        RemotingCapability = RemotingCapability.None)]
    public sealed
    class NEWUnregisterPSResourceRepository : PSCmdlet
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
            var r = new NEWRespositorySettings();
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