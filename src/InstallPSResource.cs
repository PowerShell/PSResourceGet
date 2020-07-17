
using NuGet.Versioning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Threading;
using static System.Environment;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{


    /// <summary>
    /// The Install-PSResource cmdlet installs a resource.
    /// It returns nothing.
    /// </summary>

    [Cmdlet(VerbsLifecycle.Install, "PSResource", DefaultParameterSetName = "NameParameterSet", SupportsShouldProcess = true,
    HelpUri = "<add>", RemotingCapability = RemotingCapability.None)]
    public sealed
    class InstallPSResource : PSCmdlet
    {
        /// <summary>
        /// Specifies the exact names of resources to install from a repository.
        /// A comma-separated list of module names is accepted. The resource name must match the resource name in the repository.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string[] Name
        {
            get
            { return _name; }

            set
            { _name = value; }
        }
        private string[] _name; // = new string[0];

        /// <summary>
        /// Used for pipeline input.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "InputObjectSet")]
        [ValidateNotNullOrEmpty]
        public object[] InputObject
        {
            get
            { return _inputObject; }

            set
            { _inputObject = value; }
        }
        private object[] _inputObject;

        /// <summary>
        /// Specifies the version or version range of the package to be installed
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
        /// Specifies to allow installation of prerelease versions
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [Parameter(ParameterSetName = "RequiredResourceFileParameterSet")]
        public SwitchParameter Prerelease
        {
            get
            { return _prerelease; }

            set
            { _prerelease = value; }
        }
        private SwitchParameter _prerelease;

        /// <summary>
        /// Specifies a user account that has rights to find a resource from a specific repository.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [Parameter(ParameterSetName = "RequiredResourceFileParameterSet")]
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
        /// Specifies a user account that has rights to find a resource from a specific repository.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [Parameter(ParameterSetName = "RequiredResourceFileParameterSet")]
        public PSCredential Credential
        {
            get
            { return _credential; }

            set
            { _credential = value; }
        }
        private PSCredential _credential;

        /// <summary>
        /// Specifies to return any dependency packages.
        /// Currently only used when name param is specified.
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
        /// Overrides warning messages about installation conflicts about existing commands on a computer.
        /// Overwrites existing commands that have the same name as commands being installed by a module. AllowClobber and Force can be used together in an Install-Module command.
        /// Prevents installing modules that have the same cmdlets as a differently named module already
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [Parameter(ParameterSetName = "RequiredResourceFileParameterSet")]
        public SwitchParameter NoClobber
        {
            get { return _noClobber; }

            set { _noClobber = value; }
        }
        private SwitchParameter _noClobber;

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
        /// Overwrites a previously installed resource with the same name and version.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [Parameter(ParameterSetName = "RequiredResourceFileParameterSet")]
        public SwitchParameter Reinstall
        {
            get { return _reinstall; }

            set { _reinstall = value; }
        }
        private SwitchParameter _reinstall;

        /// <summary>
        /// Suppresses progress information.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [Parameter(ParameterSetName = "RequiredResourceFileParameterSet")]
        public SwitchParameter Quiet
        {
            get { return _quiet; }

            set { _quiet = value; }
        }
        private SwitchParameter _quiet;
        
        /// <summary>
        /// For modules that require a license, AcceptLicense automatically accepts the license agreement during installation.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [Parameter(ParameterSetName = "RequiredResourceFileParameterSet")]
        public SwitchParameter AcceptLicense
        {
            get { return _acceptLicense; }

            set { _acceptLicense = value; }
        }
        private SwitchParameter _acceptLicense;

        /// <summary>
        ///  
        /// </summary>
        [Parameter(ParameterSetName = "RequiredResourceFileParameterSet")]
        public String RequiredResourceFile
        {
            get { return _requiredResourceFile; }

            set { _requiredResourceFile = value; }
        }
        private String _requiredResourceFile;

        /// <summary>
        ///  
        /// </summary>
        [Parameter(ParameterSetName = "RequiredResourceParameterSet")]
        public Object RequiredResource  // takes either string (json) or hashtable
        {
            get { return _requiredResourceHash != null ? (Object)_requiredResourceHash : (Object)_requiredResourceJson; }

            set {
                if (value.GetType().Name.Equals("String"))
                {
                    _requiredResourceJson = (String) value;
                }
                else if (value.GetType().Name.Equals("Hashtable"))
                {
                    _requiredResourceHash = (Hashtable) value;
                }
                else
                {
                    throw new ParameterBindingException("Object is not a JSON or Hashtable");
                }
            }
        }
        private string _requiredResourceJson;
        private Hashtable _requiredResourceHash;


        protected override void ProcessRecord()
        {
            // Define the cancellation token.
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken cancellationToken = source.Token;

            // If PSModuleInfo object 
            if (_inputObject != null && _inputObject[0].GetType().Name.Equals("PSModuleInfo"))
            {
                foreach (PSModuleInfo pkg in _inputObject)
                {
                    var installHelp = new InstallHelper(update: false, cancellationToken, this);
                    var prerelease = false;

                    if (pkg.PrivateData != null)
                    {
                        Hashtable privateData = (Hashtable)pkg.PrivateData;
                        if (privateData.ContainsKey("PSData"))
                        {
                            Hashtable psData = (Hashtable)privateData["PSData"];

                            if (psData.ContainsKey("Prerelease") && !string.IsNullOrEmpty((string)psData["Prerelease"]))
                            {
                                prerelease = true;
                            }
                        }
                    }

                    installHelp.ProcessInstallParams(new[] { pkg.Name }, pkg.Version.ToString(), prerelease, _repository, _scope, _acceptLicense, _quiet, _reinstall, _force: false, _trustRepository, _noClobber, _credential, _requiredResourceFile, _requiredResourceJson, _requiredResourceHash);
                }
            }
            else if (_inputObject != null && _inputObject[0].GetType().Name.Equals("PSObject"))
            {
                // If PSObject 
                foreach (PSObject pkg in _inputObject)
                {
                    var installHelp = new InstallHelper(update: false, cancellationToken, this);

                    if (pkg != null)
                    {
                        var name = (string) pkg.Properties["Name"].Value;
                        var version = (NuGetVersion) pkg.Properties["Version"].Value;
                        var prerelease = version.IsPrerelease;

                        installHelp.ProcessInstallParams(new[] { name }, version.ToString(), prerelease, _repository, _scope, _acceptLicense, _quiet, _reinstall, _force: false, _trustRepository, _noClobber, _credential, _requiredResourceFile, _requiredResourceJson, _requiredResourceHash);
                    }
                }
            }

            var installHelper = new InstallHelper(update: false, cancellationToken, this);
            installHelper.ProcessInstallParams(_name, _version, _prerelease, _repository, _scope, _acceptLicense, _quiet, _reinstall, _force:false, _trustRepository, _noClobber, _credential, _requiredResourceFile, _requiredResourceJson, _requiredResourceHash);
        }
    }
}
