// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

using Dbg = System.Diagnostics.Debug;

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
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; }

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
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// Specifies the scope of installation.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public ScopeType Scope { get; set; }

        /// <summary>
        /// Suppresses being prompted for untrusted sources.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public SwitchParameter TrustRepository { get; set; }
        
        /// <summary>
        /// Overwrites a previously installed resource with the same name and version.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public SwitchParameter Reinstall { get; set; }

        /// <summary>
        /// Suppresses progress information.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public SwitchParameter Quiet { get; set; }

        /// <summary>
        /// For modules that require a license, AcceptLicense automatically accepts the license agreement during installation.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public SwitchParameter AcceptLicense { get; set; }

        /// <summary>
        /// Prevents installing a package that contains cmdlets that already exist on the machine.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter NoClobber { get; set; }

        /// <summary>
        /// Skips the check for resource dependencies, so that only found resources are installed,
        /// and not any resources the found resource depends on.
        /// </summary>
        [Parameter]
        public SwitchParameter SkipDependencyCheck { get; set; }

        /// <summary>
        /// Passes the resource installed to the console.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public SwitchParameter PassThru { get; set; }

        /// <summary>
        /// Used for pipeline input.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = InputObjectParameterSet)]
        [ValidateNotNullOrEmpty]
        public PSResourceInfo InputObject { get; set; }

        #endregion

        #region Members

        private const string NameParameterSet = "NameParameterSet";
        private const string InputObjectParameterSet = "InputObjectParameterSet";
        private const string RequiredResourceFileParameterSet = "RequiredResourceFileParameterSet";
        private const string RequiredResourceParameterSet = "RequiredResourceParameterSet";
        List<string> _pathsToInstallPkg;
        VersionRange _versionRange;
        InstallHelper _installHelper;

        #endregion

        #region Method overrides

        protected override void BeginProcessing()
        {
            // Create a repository story (the PSResourceRepository.xml file) if it does not already exist
            // This is to create a better experience for those who have just installed v3 and want to get up and running quickly
            RepositorySettings.CheckRepositoryStore();

            _pathsToInstallPkg = Utils.GetAllInstallationPaths(this, Scope);

            _installHelper = new InstallHelper(cmdletPassedIn: this);
        }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case NameParameterSet:
                    // If no Version specified, install latest version for the package.
                    // Otherwise validate Version can be parsed out successfully.
                    if (Version == null)
                    {
                        _versionRange = VersionRange.All;
                    }
                    else if (!Utils.TryParseVersionOrVersionRange(Version, out _versionRange))
                    {
                        var exMessage = "Argument for -Version parameter is not in the proper format.";
                        var ex = new ArgumentException(exMessage);
                        var IncorrectVersionFormat = new ErrorRecord(ex, "IncorrectVersionFormat", ErrorCategory.InvalidArgument, null);
                        ThrowTerminatingError(IncorrectVersionFormat);
                    }

                    ProcessInstallHelper(
                        pkgNames: Name,
                        pkgPrerelease: Prerelease,
                        pkgRepository: Repository);
                    break;
                    
                case InputObjectParameterSet:
                    string normalizedVersionString = Utils.GetNormalizedVersionString(InputObject.Version.ToString(), InputObject.PrereleaseLabel);
                    if (!Utils.TryParseVersionOrVersionRange(normalizedVersionString, out _versionRange))
                    {
                        var exMessage = String.Format("Version '{0}' for resource '{1}' cannot be parsed.", normalizedVersionString, InputObject.Name);
                        var ex = new ArgumentException(exMessage);
                        var ErrorParsingVersion = new ErrorRecord(ex, "ErrorParsingVersion", ErrorCategory.ParserError, null);
                        WriteError(ErrorParsingVersion);
                    }

                    ProcessInstallHelper(
                        pkgNames: new string[] { InputObject.Name },
                        pkgPrerelease: InputObject.IsPrerelease,
                        pkgRepository: new string[]{ InputObject.Repository });
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

        #region Methods

        private void ProcessInstallHelper(string[] pkgNames, bool pkgPrerelease, string[] pkgRepository)
        {
            var inputNameToInstall = Utils.ProcessNameWildcards(pkgNames, out string[] errorMsgs, out bool nameContainsWildcard);
            if (nameContainsWildcard)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException("Name with wildcards is not supported for Install-PSResource cmdlet"),
                    "NameContainsWildcard",
                    ErrorCategory.InvalidArgument,
                    this));
                return;
            }
            
            foreach (string error in errorMsgs)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(error),
                    "ErrorFilteringNamesForUnsupportedWildcards",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            // this catches the case where Name wasn't passed in as null or empty,
            // but after filtering out unsupported wildcard names there are no elements left in namesToInstall
            if (inputNameToInstall.Length == 0)
            {
                return;
            }

            if (!ShouldProcess(string.Format("package to install: '{0}'", String.Join(", ", inputNameToInstall))))
            {
                WriteVerbose(string.Format("Install operation cancelled by user for packages: {0}", String.Join(", ", inputNameToInstall)));
                return;
            }

            var installedPkgs = _installHelper.InstallPackages(
                names: pkgNames,
                versionRange: _versionRange,
                prerelease: pkgPrerelease,
                repository: pkgRepository,
                acceptLicense: AcceptLicense,
                quiet: Quiet,
                reinstall: Reinstall,
                force: false,
                trustRepository: TrustRepository,
                noClobber: NoClobber,
                credential: Credential,
                asNupkg: false,
                includeXML: true,
                skipDependencyCheck: SkipDependencyCheck,
                savePkg: false,
                pathsToInstallPkg: _pathsToInstallPkg,
                out List<ErrorRecord> errorRecords);

            List<string> installedPkgNames = installedPkgs.Select(x => x.Name).ToList();
            foreach(string expectedPkgName in pkgNames)
            {
                if (!installedPkgNames.Contains(expectedPkgName))
                {
                    // WriteWarning(String.Format("Package '{0}' was not installed. Please run the cmdlet with -Verbose for more information", expectedPkgName));
                    var message = String.Format("Package {0} could not be installed with error: resource could not be found in any registered repositories", expectedPkgName);
                    var ex = new ArgumentException(message);
                    var ResourceNotFoundError = new ErrorRecord(ex, "resourceNotFoundError", ErrorCategory.ObjectNotFound, null);
                    errorRecords.Add(ResourceNotFoundError);
                }
            }

            foreach (ErrorRecord error in errorRecords)
            {
                WriteError(error);
            }

            if (PassThru)
            {
                foreach (PSResourceInfo pkg in installedPkgs)
                {
                    WriteObject(pkg);
                }
            }
        }

        #endregion
    }
}
