
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Dbg = System.Diagnostics.Debug;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Update-PSResource cmdlet replaces the Update-Module and Update-Script cmdlets from V2.
    /// It updates an already installed package based on the -Name parameter argument.
    /// It does not return an object. Other parameters allow the package to be updated to be further filtered.
    /// </summary>

    [Cmdlet(VerbsData.Update,
        "PSResource",
        DefaultParameterSetName = NameParameterSet,
        SupportsShouldProcess = true)]
    public sealed class UpdatePSResource : PSCmdlet
    {
        #region Members

        private const string NameParameterSet = "NameParameterSet";
        private List<string> _pathsToInstallPkg;

        #endregion

        #region Parameters

        /// <summary>
        /// Specifies name of a resource or resources to update.
        /// Accepts wildcard characters.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set ; } = new string[] {"*"};

        /// <summary>
        /// Specifies the version the resource is to be updated to.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }

        /// <summary>
        /// When specified, allows updating to a prerelease version.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter Prerelease { get; set; }

        /// <summary>
        /// Specifies one or more repository names to update packages from.
        /// If not specified, search will include all currently registered repositories in order of highest priority.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        /// <summary>
        /// Specifies the scope of the resource to update.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public ScopeType Scope { get; set; }

        /// <summary>
        /// When specified, supresses being prompted for untrusted sources.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter TrustRepository { get; set; }

        /// <summary>
        /// Specifies optional credentials to be used when accessing a private repository.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// Supresses progress information.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter Quiet { get; set; }

        /// <summary>
        /// For resources that require a license, AcceptLicense automatically accepts the license agreement during the update.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter AcceptLicense { get; set; }

        /// <summary>
        /// When specified, bypasses checks for TrustRepository and AcceptLicense and updates the package.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter Force { get; set; }

        #endregion

        #region Methods

        protected override void BeginProcessing()
        {
            _pathsToInstallPkg = Utils.GetAllInstallationPaths(this, Scope);
        }

        private string[] ProcessNames(string[] namesToProcess, VersionRange versionRange)
        {
            namesToProcess = Utils.ProcessNameWildcards(namesToProcess, out string[] errorMsgs, out bool nameContainsWildcard);
            
            foreach (string error in errorMsgs)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(error),
                    "ErrorFilteringNamesForUnsupportedWildcards",
                    ErrorCategory.InvalidArgument,
                    this));
            }
            
            // this catches the case where Name wasn't input as null or empty,
            // but after filtering out unsupported wildcard names there are no elements left in Name
            if (Name.Length == 0)
            {
                 return namesToProcess;
            }

            if (String.Equals(namesToProcess[0], "*", StringComparison.InvariantCultureIgnoreCase))
            {
                WriteVerbose("Name was detected to be (or contain an element equal to): '*', so all packages will be updated");
            }

            if (nameContainsWildcard)
            {
                // any of the Name entries contains a supported wildcard
                // then we need to use GetHelper (Get-InstalledPSResource logic) to find which packages are installed that match
                // the wildcard pattern name for each package name with wildcard

                GetHelper getHelper = new GetHelper(
                    cmdletPassedIn: this);

                namesToProcess = getHelper.FilterPkgPaths(
                    name: Name,
                    versionRange: versionRange,
                    pathsToSearch: Utils.GetAllResourcePaths(this)).Select(p => p.Name).ToArray();
            }

            return namesToProcess;

        }

        protected override void ProcessRecord()
        {
            VersionRange versionRange;

            // handle case where Version == null
            if (Version == null) { 
                versionRange = VersionRange.All;
            }
            else if (!Utils.TryParseVersionOrVersionRange(Version, out versionRange))
            {
                // Only returns false if the range was incorrectly formatted and couldn't be parsed.
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException("Cannot parse Version parameter provided into VersionRange"),
                    "ErrorParsingVersionParamIntoVersionRange",
                    ErrorCategory.InvalidArgument,
                    this));
                return;
            }

            Name = ProcessNames(Name, versionRange);

            if (!ShouldProcess(string.Format("package to update: '{0}'", String.Join(", ", Name))))
            {
                WriteVerbose("ShouldProcess was set to false.");
                return;
            }

            InstallHelper installHelper = new InstallHelper(
                updatePkg: true,
                savePkg: false,
                cmdletPassedIn: this);

            installHelper.InstallPackages(
                names: Name,
                versionRange: versionRange,
                prerelease: Prerelease,
                repository: Repository,
                acceptLicense: AcceptLicense,
                quiet: Quiet,
                reinstall: false,
                force: Force,
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
        }

        #endregion
    }
}