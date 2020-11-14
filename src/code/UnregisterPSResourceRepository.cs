
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.PowerShell.PowerShellGet.RepositorySettings;
using System;
using System.Management.Automation;


namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{

    /// <summary>
    /// The Register-PSResourceRepository cmdlet registers the default repository for PowerShell modules.
    /// After a repository is registered, you can reference it from the Find-PSResource, Install-PSResource, and Publish-PSResource cmdlets.
    /// The registered repository becomes the default repository in Find-Module and Install-Module.
    /// It returns nothing.
    /// </summary>

    [Cmdlet(VerbsLifecycle.Unregister, "PSResourceRepository", DefaultParameterSetName = "NameParameterSet", SupportsShouldProcess = true,
        HelpUri = "<add>", RemotingCapability = RemotingCapability.None)]
    public sealed
    class UnregisterPSResourceRepository : PSCmdlet
    {
       // private string PSGalleryRepoName = "PSGallery";

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




        /// <summary>
        /// </summary>
        protected override void ProcessRecord()
        {
            var r = new RespositorySettings();

            // need to check if name is null?
            try
            {
                r.Remove(_name);
            }
            catch (Exception e){
                throw new Exception(string.Format("Unable to successfully unregister repository: {0}", e.Message));
            }
        }

    }
}
