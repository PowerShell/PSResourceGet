// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Management.Automation;

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
    class GetPSResourceRepository : PSCmdlet
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
            var listOfRepositories = RepositorySettings.Read(_name);
            foreach (var repo in listOfRepositories)
            {
                WriteObject(repo);
            }
        }
    }
}
