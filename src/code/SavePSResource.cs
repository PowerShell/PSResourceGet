using System;
using System.Collections;
using System.Management.Automation;
using System.Collections.Generic;
using System.Threading;
using NuGet.Versioning;
using static System.Environment;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{


    /// <summary>
    /// The Save-PSResource cmdlet saves a resource, either packed or unpacked.
    /// It returns nothing.
    /// </summary>

    [Cmdlet(VerbsData.Save, "PSResource", DefaultParameterSetName = "NameParameterSet", SupportsShouldProcess = true,
    HelpUri = "<add>", RemotingCapability = RemotingCapability.None)]
    public sealed
    class SavePSResource : PSCmdlet
    {
        /// <summary>
        /// Specifies the exact names of resources to save from a repository.
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
        private string[] _name; // = new string[0];

        /// <summary>
        /// Specifies the version or version range of the package to be saved
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
        public PSCredential Credential
        {
            get
            { return _credential; }

            set
            { _credential = value; }
        }
        private PSCredential _credential;

        /// <summary>
        /// Saves as a .nupkg
        /// </summary>
        [Parameter()]
        public SwitchParameter AsNupkg
        {
            get { return _asNupkg; }

            set { _asNupkg = value; }
        }
        private SwitchParameter _asNupkg;

        /// <summary>
        /// Saves the metadata XML file with the resource
        /// </summary>
        [Parameter()]
        public SwitchParameter IncludeXML
        {
            get { return _includeXML; }

            set { _includeXML = value; }
        }
        private SwitchParameter _includeXML;

        /// <summary>
        /// The destination where the resource is to be installed. Works for all resource types.
        /// </summary>
        [Parameter(ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string Path
        {
            get
            { return _path; }

            set
            { _path = value; }
        }
        private string _path;

        /// <summary>
        /// Suppresses being prompted for untrusted sources.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        public SwitchParameter TrustRepository
        {
            get { return _trustRepository; }

            set { _trustRepository = value; }
        }
        private SwitchParameter _trustRepository;


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

        // This will be a list of all the repository caches
        public static readonly List<string> RepoCacheFileName = new List<string>();
        public static readonly string RepositoryCacheDir = System.IO.Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "PowerShellGet", "RepositoryCache");
        public static readonly string OsPlatform = System.Runtime.InteropServices.RuntimeInformation.OSDescription;


        /// <summary>
        /// </summary>
        protected override void ProcessRecord()
        { // Define the cancellation token.
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken cancellationToken = source.Token;

            // If PSModuleInfo object 
            if (_inputObject != null && _inputObject[0].GetType().Name.Equals("PSModuleInfo"))
            {
                foreach (PSModuleInfo pkg in _inputObject)
                {
                    //var prerelease = false;
                    if (pkg.PrivateData != null)
                    {
                        Hashtable privateData = (Hashtable)pkg.PrivateData;
                        if (privateData.ContainsKey("PSData"))
                        {
                            Hashtable psData = (Hashtable)privateData["PSData"];
                            if (psData.ContainsKey("Prerelease") && !string.IsNullOrEmpty((string)psData["Prerelease"]))
                            {
                                //prerelease = true;
                            }
                        }
                    }
                    var installHelp = new InstallHelper(update: false, save: true, cancellationToken, this);

                    installHelp.ProcessInstallParams(_name, _version, _prerelease, _repository, _scope: null, _acceptLicense: false, _quiet: false, _reinstall: false, _force: false, _trustRepository, _noClobber: false, _credential, _requiredResourceFile: null, _requiredResourceJson: null, _requiredResourceHash: null, _path, _asNupkg, _includeXML);
                }
            }
            else if (_inputObject != null && _inputObject[0].GetType().Name.Equals("PSObject"))
            {
                // If PSObject 
                foreach (PSObject pkg in _inputObject)
                {
                    var installHelp = new InstallHelper(update:false, save:true, cancellationToken, this);
                    if (pkg != null)
                    {
                        var name = (string)pkg.Properties["Name"].Value;
                        var version = (NuGetVersion)pkg.Properties["Version"].Value;
                        var prerelease = version.IsPrerelease;

                        installHelp.ProcessInstallParams(new[] { name }, version.ToString(), prerelease, _repository, _scope:null, _acceptLicense:false, _quiet:false, _reinstall:false, _force: false, _trustRepository, _noClobber:false, _credential, _requiredResourceFile:null, _requiredResourceJson:null, _requiredResourceHash:null, _path, _asNupkg, _includeXML);
                    }
                }
            }

            var installHelper = new InstallHelper(update:false, save:true, cancellationToken, this);
            installHelper.ProcessInstallParams(_name, _version, _prerelease, _repository, _scope:null, _acceptLicense: false, _quiet:false, _reinstall:false, _force: false, _trustRepository, _noClobber:false, _credential, _requiredResourceFile:null, _requiredResourceJson:null, _requiredResourceHash:null, _path, _asNupkg, _includeXML);
        }
    }
}