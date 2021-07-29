using System.Collections.Specialized;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using Dbg = System.Diagnostics.Debug;
using System.Management.Automation;
using System.Threading;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Install-PSResource cmdlet installs a resource.
    /// It returns nothing.
    /// </summary>

    [Cmdlet(VerbsLifecycle.Install, "PSResource", DefaultParameterSetName = "NameParameterSet", SupportsShouldProcess = true)]
    public sealed
    class InstallPSResource : PSCmdlet
    {
        #region Parameters 

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
        public SwitchParameter Prerelease { get; set; }

        /// <summary>
        /// Specifies the repositories from which to search for the resource to be installed.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        /// <summary>
        /// Specifies a user account that has rights to find a resource from a specific repository.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = NameParameterSet)]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// Specifies the scope of installation.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public ScopeType Scope { get; set; }

        /// <summary>
        /// Suppresses being prompted for untrusted sources.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter TrustRepository { get; set; }

        /// <summary>
        /// Overwrites a previously installed resource with the same name and version.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter Reinstall { get; set; }

        /// <summary>
        /// Suppresses progress information.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter Quiet { get; set; }

        /// <summary>
        /// For modules that require a license, AcceptLicense automatically accepts the license agreement during installation.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter AcceptLicense { get; set; }

        #endregion

        #region Members

        private const string NameParameterSet = "NameParameterSet";
        private const string RequiredResourceFileParameterSet = "RequiredResourceFileParameterSet";
        private const string RequiredResourceParameterSet = "RequiredResourceParameterSet";
        List<string> _pathsToInstallPkg;
        VersionRange _versionRange;

        #endregion

        #region Method overrides

        protected override void BeginProcessing()
        {
            // validate that if a -Version param is passed in that it can be parsed into a NuGet version range. 
            // An exact version will be formatted into a version range.
            if (ParameterSetName.Equals(NameParameterSet) && Version != null && !Utils.TryParseVersionOrVersionRange(Version, out _versionRange))

            {
                var exMessage = "Argument for -Version parameter is not in the proper format.";
                var ex = new ArgumentException(exMessage);
                var IncorrectVersionFormat = new ErrorRecord(ex, "IncorrectVersionFormat", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(IncorrectVersionFormat);
            }

            // if no Version specified, install latest version for the package
            if (Version == null)
            {
                _versionRange = VersionRange.All;
            }

            _pathsToInstallPkg = Utils.GetAllInstallationPaths(this, Scope);
        }

        protected override void ProcessRecord()
        {
            if (!ShouldProcess(string.Format("package to install: '{0}'", String.Join(", ", Name))))
            {
                WriteVerbose(string.Format("Install operation cancelled by user for packages: {0}", String.Join(", ", Name)));
                return;
            }

            var installHelper = new InstallHelper(updatePkg: false, savePkg: false, cmdletPassedIn: this);

            switch (ParameterSetName)
            {
                case NameParameterSet:
                    installHelper.InstallPackages(
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
                    ThrowTerminatingError(new ErrorRecord(
                                           new PSNotImplementedException("RequiredResourceFileParameterSet is not yet implemented. Please rerun cmdlet with other parameter set."),
                                           "CommandParameterSetNotImplementedYet",
                                           ErrorCategory.NotImplemented,
                                           this));
                    break;

                case RequiredResourceParameterSet:
                    ThrowTerminatingError(new ErrorRecord(
                                           new PSNotImplementedException("RequiredResourceParameterSet is not yet implemented. Please rerun cmdlet with other parameter set."),
                                           "CommandParameterSetNotImplementedYet",
                                           ErrorCategory.NotImplemented,
                                           this));
                    break;

                default:
                    Dbg.Assert(false, "Invalid parameter set");
                    break;
            }
        }
        
        #endregion
    }
}
