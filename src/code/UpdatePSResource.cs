using System.Management.Automation;
using System.Threading;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Update-PSResource cmdlet updates a previously installed resource.
    /// It returns nothing.
    /// </summary>
    [Cmdlet(VerbsData.Update, "PSResource", DefaultParameterSetName = "NameParameterSet", SupportsShouldProcess = true,
    HelpUri = "<add>", RemotingCapability = RemotingCapability.None)]
    public sealed
    class UpdatePSResource : PSCmdlet
    {
        /// <summary>
        /// Specifies the exact names of resources to update.
        /// A comma-separated list of module names is accepted. The resource name must match the resource name in the repository.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string[] Name
        {
            get
            { return _name; }

            set
            { _name = value; }
        }
        private string[] _name;

        /*
        /// <summary>
        /// Used for pipeline input.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "InputObjectSet")]
        [ValidateNotNullOrEmpty]
        public PSCustomObject[] InputObject
        {
            get
            { return _inputObject; }

            set
            { _inputObject = value; }
        }
        private PSCustomObject[] _inputObject; 
        */

        /// <summary>
        /// Specifies the version or version range of the package to update.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string Version
        {
            get
            { return _version; }
            set
            { _version = value; }
        }
        private string _version;

        /// <summary>
        /// Specifies to allow updates to prerelease versions.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        public SwitchParameter Prerelease
        {
            get
            { return _prerelease; }

            set
            { _prerelease = value; }
        }
        private SwitchParameter _prerelease;

        /// <summary>
        /// Specifies the repositories from which to update the resource.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string[] Repository
        {
            get
            { return _repository; }

            set
            { _repository = value; }
        }
        private string[] _repository;

        /// <summary>
        /// Specifies the scope of the resource to update.
        /// </summary>
        [ValidateSet("CurrentUser", "AllUsers")]
        [Parameter(ParameterSetName = "NameParameterSet")]
        [Parameter(ParameterSetName = "RequiredResourceFileParameterSet")]
        public string Scope
        {
            get { return _scope; }

            set { _scope = value; }
        }
        private string _scope;

        /// <summary>
        /// Suppresses being prompted for untrusted sources.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [Parameter(ParameterSetName = "RequiredResourceFileParameterSet")]
        public SwitchParameter TrustRepository
        {
            get { return _trustRepository; }

            set { _trustRepository = value; }
        }
        private SwitchParameter _trustRepository;

        /// <summary>
        /// Specifies a user account that has rights to find a resource from a specific repository.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        public PSCredential Credential
        {
            get
            { return _credential; }

            set
            { _credential = value; }
        }
        private PSCredential _credential;

        /// <summary>
        /// Suppresses progress information.
        /// </summary>
        [Parameter()]
        public SwitchParameter Quiet
        {
            get { return _quiet; }

            set { _quiet = value; }
        }
        private SwitchParameter _quiet;

        /// <summary>
        /// For modules that require a license, AcceptLicense automatically accepts the license agreement during update.
        /// </summary>
        [Parameter()]
        public SwitchParameter AcceptLicense
        {
            get { return _acceptLicense; }

            set { _acceptLicense = value; }
        }
        private SwitchParameter _acceptLicense;

        /// <summary>
        /// Overrides warning messages about installation conflicts about existing commands on a computer.
        /// Overwrites existing commands that have the same name as commands being installed by a module. AllowClobber and Force can be used together in an Install-Module command.
        /// Prevents installing modules that have the same cmdlets as a differently named module already
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        public SwitchParameter NoClobber
        {
            get { return _noClobber; }

            set { _noClobber = value; }
        }
        private SwitchParameter _noClobber;


        protected override void ProcessRecord()
        {
            // Define the cancellation token.
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken cancellationToken = source.Token;

            var installHelper = new InstallHelper(update:true, save:false, cancellationToken, this);
            installHelper.ProcessInstallParams(_name, _version, _prerelease, _repository, _scope, _acceptLicense, _quiet, _reinstall: false, _force: false, _trustRepository, _noClobber, _credential, _requiredResourceFile: null, _requiredResourceJson: null, _requiredResourceHash: null, _path:null, _asNupkg:false, _includeXML:true);
        }
    }
}