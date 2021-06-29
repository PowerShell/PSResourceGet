using Microsoft.PowerShell.PowerShellGet.UtilClasses;
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

    [Cmdlet(VerbsLifecycle.Install, "PSResource", DefaultParameterSetName = "NameParameterSet", SupportsShouldProcess = true, HelpUri = "<add>")]
    public sealed
    class InstallPSResource : PSCmdlet
    {
        #region parameters 
        /// <summary>
        /// Specifies the exact names of resources to install from a repository.
        /// A comma-separated list of module names is accepted. The resource name must match the resource name in the repository.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true, ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; }

        /// <summary>
        /// Used for pipeline input.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = InputObjectSet)]
        [ValidateNotNullOrEmpty]
        public object[] InputObject { get; set; }
        
        /// <summary>
        /// Specifies the version or version range of the package to be installed
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }
        
        /// <summary>
        /// Specifies to allow installation of prerelease versions
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        //[Parameter(ParameterSetName = RequiredResourceFileParameterSet)]
        public SwitchParameter Prerelease { get; set; }

        /// <summary>
        /// Specifies a user account that has rights to find a resource from a specific repository.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        // todo: add tab completion (look at get-psresourcerepository at the name parameter)
        //[Parameter(ParameterSetName = RequiredResourceFileParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        /// <summary>
        /// Specifies a user account that has rights to find a resource from a specific repository.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = NameParameterSet)]
        //[Parameter(ParameterSetName = RequiredResourceFileParameterSet)]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// Specifies to return any dependency packages.
        /// Currently only used when name param is specified.
        /// </summary>
        [ValidateSet("CurrentUser", "AllUsers")]
        [Parameter(ParameterSetName = NameParameterSet)]
        //[Parameter(ParameterSetName = RequiredResourceFileParameterSet)]
        public string Scope { get; set; }

        /// <summary>
        /// Overrides warning messages about installation conflicts about existing commands on a computer.
        /// Overwrites existing commands that have the same name as commands being installed by a module. AllowClobber and Force can be used together in an Install-Module command.
        /// Prevents installing modules that have the same cmdlets as a differently named module already
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        //[Parameter(ParameterSetName = RequiredResourceFileParameterSet)]
        public SwitchParameter NoClobber { get; set; }

        /// <summary>
        /// Suppresses being prompted for untrusted sources.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        //[Parameter(ParameterSetName = RequiredResourceFileParameterSet)]
        public SwitchParameter TrustRepository { get; set; }

        /// <summary>
        /// Overwrites a previously installed resource with the same name and version.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        //[Parameter(ParameterSetName = RequiredResourceFileParameterSet)]
        public SwitchParameter Reinstall { get; set; }

        /// <summary>
        /// Suppresses progress information.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        //[Parameter(ParameterSetName = RequiredResourceFileParameterSet)]
        public SwitchParameter Quiet { get; set; }

        /// <summary>
        /// For modules that require a license, AcceptLicense automatically accepts the license agreement during installation.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        //[Parameter(ParameterSetName = RequiredResourceFileParameterSet)]
        public SwitchParameter AcceptLicense { get; set; }

        /// <summary>
        ///  
        /// </summary>
        [Parameter(ParameterSetName = RequiredResourceFileParameterSet)]
        public String RequiredResourceFile { get; set; }

        /// <summary>
        ///  
        /// </summary>
        [Parameter(ParameterSetName = RequiredResourceParameterSet)]
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
        #endregion

        #region members
        private const string NameParameterSet = "NameParameterSet";
        private const string InputObjectSet = "InputObjectSet";
        private const string RequiredResourceFileParameterSet = "RequiredResourceFileParameterSet";
        private const string RequiredResourceParameterSet = "RequiredResourceParameterSet";
        List<string> _pathsToInstallPkg;
        VersionRange _versionRange;
        #endregion

        #region Methods
        protected override void BeginProcessing()
        {
            // validate that if a -Version param is passed in that it can be parsed into a NuGet version range. 
            // an exact version will be formatted into a version range.
            if (ParameterSetName.Equals("NameParameterSet") && Version != null && !Utils.TryParseVersionOrVersionRange(Version, out _versionRange))
            {
                var exMessage = "Argument for -Version parameter is not in the proper format.";
                var ex = new ArgumentException(exMessage);
                var IncorrectVersionFormat = new ErrorRecord(ex, "IncorrectVersionFormat", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(IncorrectVersionFormat);
            }

            _pathsToInstallPkg = Utils.GetAllInstallationPaths(this, Scope);
        }

        protected override void ProcessRecord()
        {
            // Define the cancellation token.
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken cancellationToken = source.Token;

            var installHelper = new InstallHelper(update: false, save: false, cancellationToken, this);

            switch (ParameterSetName)
            {
                case NameParameterSet:
                    installHelper.ProcessInstallParams(Name, _versionRange, Prerelease, Repository, Scope, AcceptLicense, Quiet, Reinstall, force: false, TrustRepository, NoClobber, Credential, RequiredResourceFile, _requiredResourceJson, _requiredResourceHash, specifiedPath: null, asNupkg: false, includeXML: true, _pathsToInstallPkg);
                    break;

                // TODO: make sure InputObject types are correct
                // TODO: Consider switch statement of object type to clean up a bit
                case InputObjectSet:
                    if (InputObject[0].GetType().Name.Equals("PSModuleInfo"))
                    {
                        foreach (PSModuleInfo pkg in InputObject)
                        {
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

                            // Need to explicitly assign inputObjVersionRange in order to pass to ProcessInstallParams
                            VersionRange inputObjVersionRange = new VersionRange();
                            if (pkg.Version != null && !Utils.TryParseVersionOrVersionRange(pkg.Version.ToString(), out inputObjVersionRange))
                            {
                                var exMessage = "Argument for version parameter is not in the proper format.";
                                var ex = new ArgumentException(exMessage);
                                var InputObjIncorrectVersionFormat = new ErrorRecord(ex, "InputObjIncorrectVersionFormat", ErrorCategory.InvalidArgument, null);
                                ThrowTerminatingError(InputObjIncorrectVersionFormat);
                            }

                            installHelper.ProcessInstallParams(new[] { pkg.Name }, inputObjVersionRange, prerelease, Repository, Scope, AcceptLicense, Quiet, Reinstall, force: false, TrustRepository, NoClobber, Credential, RequiredResourceFile, _requiredResourceJson, _requiredResourceHash, specifiedPath: null, asNupkg: false, includeXML: true, _pathsToInstallPkg);
                        }
                    }
                    else if (InputObject[0].GetType().Name.Equals("PSModuleInfo"))
                    {
                        foreach (PSObject pkg in InputObject)
                        {
                            if (pkg != null)
                            {
                                var name = (string)pkg.Properties["Name"].Value;
                                var version = (NuGetVersion)pkg.Properties["Version"].Value;
                                var prerelease = version.IsPrerelease;

                                VersionRange inputObjVersionRange = new VersionRange();
                                if (version != null && !Utils.TryParseVersionOrVersionRange(version.ToString(), out inputObjVersionRange))
                                {
                                    var exMessage = "Argument for version parameter is not in the proper format.";
                                    var ex = new ArgumentException(exMessage);
                                    var InputObjIncorrectVersionFormat = new ErrorRecord(ex, "InputObjIncorrectVersionFormat", ErrorCategory.InvalidArgument, null);
                                    ThrowTerminatingError(InputObjIncorrectVersionFormat);
                                }

                                installHelper.ProcessInstallParams(new[] { name }, inputObjVersionRange, prerelease, Repository, Scope, AcceptLicense, Quiet, Reinstall, force: false, TrustRepository, NoClobber, Credential, RequiredResourceFile, _requiredResourceJson, _requiredResourceHash, specifiedPath: null, asNupkg: false, includeXML: true, _pathsToInstallPkg);
                            }
                        } 
                    }
                    break;

                case RequiredResourceFileParameterSet:
                    // TODO: throw PSNotImplementedException
                    WriteDebug("Not yet implemented");
                    break;

                case RequiredResourceParameterSet:
                    // TODO: throw PSNotImplementedException
                    WriteDebug("Not yet implemented");
                    break;

                default:
                    // TODO: throw some kind of terminating error (unrecognized parameter set)
                    // TODO: change to debug assert
                    WriteDebug("Invalid parameter set");
                    break;
            }
        }
        #endregion
    }
}