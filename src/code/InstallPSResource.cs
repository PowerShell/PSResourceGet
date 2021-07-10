// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;

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
        /// Specifies the version or version range of the package to be installed
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }
        
        /// <summary>
        /// Specifies to allow installation of prerelease versions
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        //[Parameter(ParameterSetName = RequiredResourceFileParameterSet)]
        public SwitchParameter Prerelease { get; set; }

        /// <summary>
        /// Specifies the repositories from which to search for the resource to be installed.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        //[Parameter(ParameterSetName = RequiredResourceFileParameterSet)]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        /// <summary>
        /// Specifies a user account that has rights to find a resource from a specific repository.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = NameParameterSet)]
        //[Parameter(ParameterSetName = RequiredResourceFileParameterSet)]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// Specifies the scope of installation.
        /// </summary>
        [ValidateSet("CurrentUser", "AllUsers")]
        [Parameter(ParameterSetName = NameParameterSet)]
        //[Parameter(ParameterSetName = RequiredResourceFileParameterSet)]
        public ScopeType Scope { get; set; }

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

        /*
        /// <summary>
        /// Prevents installation conflicts with modules that contain existing commands on a computer.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        //[Parameter(ParameterSetName = RequiredResourceFileParameterSet)]
        public SwitchParameter NoClobber { get; set; }
        */
           
        /*
        /// <summary>
        /// </summary>
        [Parameter(ParameterSetName = RequiredResourceFileParameterSet)]
        public String RequiredResourceFile { get; set; }
        */

        /*
        /// <summary> 
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
        */
        #endregion

        #region members
        private const string NameParameterSet = "NameParameterSet";
        private const string RequiredResourceFileParameterSet = "RequiredResourceFileParameterSet";
        private const string RequiredResourceParameterSet = "RequiredResourceParameterSet";
        List<string> _pathsToInstallPkg;
        VersionRange _versionRange;
        #endregion

        #region Methods
        protected override void BeginProcessing()
        {
            // validate that if a -Version param is passed in that it can be parsed into a NuGet version range. 
            // An exact version will be formatted into a version range.
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

            var installHelper = new InstallHelper(update: false, save: false, cancellationToken: cancellationToken, cmdletPassedIn: this);

            switch (ParameterSetName)
            {
                case NameParameterSet:
                    installHelper.ProcessInstallParams(
                        names: Name,
                        versionRange: _versionRange,
                        prerelease: Prerelease,
                        repository: Repository,
                        acceptLicense: AcceptLicense,
                        quiet: Quiet,
                        reinstall: Reinstall,
                        force: false,
                        trustRepository: TrustRepository,
                        noClobber: false,
                        credential: Credential,
                        requiredResourceFile: null, 
                        requiredResourceJson: null, 
                        requiredResourceHash: null, 
                        specifiedPath: null, 
                        asNupkg: false, 
                        includeXML: true, 
                        pathsToInstallPkg: _pathsToInstallPkg);
                    break;
                    
                case RequiredResourceFileParameterSet:
                    WriteDebug("Not yet implemented");
                    break;

                case RequiredResourceParameterSet:
                    WriteDebug("Not yet implemented");
                    break;

                default:
                    WriteDebug("Invalid parameter set");
                    break;
            }
        }
        #endregion
    }
}
