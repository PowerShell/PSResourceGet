// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Management.Automation;
using Microsoft.PowerShell.PowerShellGet.RepositorySettings;



namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Register-PSResourceRepository cmdlet.
    /// It retrieves a repository that was registered with Register-PSResourceRepository
    /// Returns a single repository or multiple repositories.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "PSResourceRepository", SupportsShouldProcess = true,
        HelpUri = "<add>", RemotingCapability = RemotingCapability.None)]
    public sealed
    class OLDGetPSResourceRepository : PSCmdlet
    {
        /// <summary>
        /// Specifies the desired name for the repository to be registered.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true,
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
        private string[] _name = new string[0];


        /// <summary>
        /// </summary>
        protected override void ProcessRecord()
        {
             var r = new RespositorySettings();

             var listOfRepositories = r.Read(_name);

            /// Print out repos
            foreach (var repo in listOfRepositories)
            {
                WriteObject(repo);
            }
        }
    }
}
