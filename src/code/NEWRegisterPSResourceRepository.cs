using System;
using System.Collections;
using System.Management.Automation;
using System.Globalization;
using Microsoft.PowerShell.PowerShellGet.RepositorySettings;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// todo: fill
    /// </summary>

    [Cmdlet(VerbsLifecycle.Register,
        "NEWPSResourceRepository",
        DefaultParameterSetName = "NameParameterSet",
        SupportsShouldProcess = true,
        HelpUri = "<add",
        RemotingCapability = RemotingCapability.None)]
    public sealed
    class NEWRegisterPSResourceRepository : PSCmdlet
    {
        private string PSGalleryRepoName = "PSGallery";
        private string PSGalleryRepoURL = "https://www.powershellgallery.com/api/v2";

        #region Parameters
        /// <summary>
        /// Specifies name for the repository to be registered.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string Name
        {
            get
            { return _name; }

            set
            { _name = value; }
        }
        private string _name;

        /// <summary>
        /// Specifies the location of the repository to be registered.
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public Uri URL
        {
            get
            { return _url; }

            set
            { _url = value; } //todo: actually set code here
        }
        private Uri _url;

        /// <summary>
        /// When specified, registers PSGallery repository.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "PSGalleryParameterSet")]
        public SwitchParameter PSGallery
        {
            get
            { return _psgallery; }

            set
            { _psgallery = value; }
        }
        private SwitchParameter _psgallery;

        /// <summary>
        /// Specifies a hashtable of repositories and is used to register multiple repositories at once.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "RepositoriesParameterSet")]
        [ValidateNotNullOrEmpty]
        public List<Hashtable> Repositories
        {
            get
            { return _repositories; }

            set
            { _repositories = value; }
        }
        private List<Hashtable> _repositories;

        /// <summary>
        /// Specifies whether the repository should be trusted.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [Parameter(ParameterSetName = "PSGalleryParameterSet")]
        public SwitchParameter Trusted
        {
            get
            { return _trusted; }

            set
            { _trusted = value; }
        }
        private SwitchParameter _trusted;

        /// <summary>
        /// Specifies the priority ranking of the repository, such that repositories with higher ranking priority are searched
        /// before a lower ranking priority one, when searching for a repository item across multiple registered repositories.
        /// Valid priority values range from 0 to 50, such that a lower numeric value (i.e 10) corresponds
        /// to a higher priority ranking than a higher numeric value (i.e 40). Has default value of 50.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [Parameter(ParameterSetName = "PSGalleryParameterSet")]
        public int Priority
        {
            get
            { return _priority; }

            set
            { _priority = value; }
        }
        private int _priority = 50;

        /// <summary>
        /// Specifies a proxy server for the request, rather than a direct connection to the internet resource.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public Uri Proxy
        {
            get
            { return _proxy; }

            set
            { _proxy = value; }
        }
        private Uri _proxy;

        /// <summary>
        /// Specifies a user account that has permission to use the proxy server that is specified by the Proxy parameter.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        public PSCredential ProxyCredential
        {
            get
            { return _proxycredential; }

            set
            { _proxycredential = value; }
        }
        private PSCredential _proxycredential;
        #endregion



    }
}