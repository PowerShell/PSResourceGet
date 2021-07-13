
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Dbg = System.Diagnostics.Debug;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Save-PSResource cmdlet combines the Save-Module, Save-Script cmdlets from V2.
    /// It saves from a package found from a repository (local or remote) based on the -Name parameter argument.
    /// It does not return an object. Other parameters allow the returned results to be further filtered.
    /// </summary>

    [Cmdlet(VerbsData.Update,
        "PSResource",
        DefaultParameterSetName = NameParameterSet,
        SupportsShouldProcess = true,
        HelpUri = "<add>")]
    [OutputType(typeof(PSResourceInfo))]
    public sealed
    class UpdatePSResource : PSCmdlet
    {
        #region Members

        private const string NameParameterSet = "NameParameterSet";
        private const string InputObjectParameterSet = "InputObjectParameterSet";
        private CancellationTokenSource _source;
        private CancellationToken _cancellationToken;

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
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        /// <summary>
        /// Specifies the scope of the resource to update.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public ScopeType Scope { get; set; }

        /// <summary>
        /// When specified, supresses being prompted for untrusted sources.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
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
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public SwitchParameter Quiet { get; set; }

        /// <summary>
        /// For resources that require a license, AcceptLicense automatically accepts the license agreement during the update.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public SwitchParameter AcceptLicense { get; set; }

        /// <summary>
        /// When specified, bypasses checks for TrustRepository and AcceptLicense and updates the package.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Used to pass in an object via pipeline to update.
        /// </summary>
        [Parameter(ValueFromPipeline = true, ParameterSetName = NameParameterSet)]
        public object[] InputObject { get; set; }

        #endregion

        #region Methods

        protected override void BeginProcessing()
        {
            _source = new CancellationTokenSource();
            _cancellationToken = _source.Token;
        }

        protected override void StopProcessing()
        {
            _source.Cancel();
        }

        protected override void ProcessRecord()
        {
            Name = Utils.FilterWildcards(Name, out string[] errorMsgs, out bool isContainWildcard);

            foreach (string error in errorMsgs)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(error),
                    "ErrorFilteringNamesForUnsupportedWildcards",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            if (Name.Length == 1 && String.Equals(Name[0], "*", StringComparison.InvariantCultureIgnoreCase))
            {
                WriteVerbose("Name was detected to be (or contain an element equal to): '*', so all packages will be updated");
            }

            // this catches the case where Name wasn't input as null or empty,
            // but after filtering out unsupported wildcard names there are no elements left in Name
            if (Name.Length == 0)
            {
                 return;
            }

            VersionRange versionRange = new VersionRange();

            if (Version !=null && !Utils.TryParseVersionOrVersionRange(Version, out versionRange))
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException("Cannot parse Version parameter provided into VersionRange"),
                    "ErrorParsingVersionParamIntoVersionRange",
                    ErrorCategory.InvalidArgument,
                    this));
                return;
            }

            if (isContainWildcard)
            {
                // any of the Name entries contains a supported wildcard
                // then we need to use GetHelper (Get-InstalledPSResource logic) to find which packages are installed that match
                // the wildcard pattern name for each package name with wildcard

                GetHelper getHelper = new GetHelper(
                    cmdletPassedIn: this);

                Name = getHelper.FilterPkgPaths(
                    name: Name,
                    versionRange: versionRange,
                    pathsToSearch: Utils.GetAllResourcePaths(this)).Select(p => p.Name).ToArray();
            }

            InstallHelper installHelper = new InstallHelper(
                update: true,
                save: false,
                cancellationToken: _cancellationToken,
                cmdletPassedIn: this);

            switch (ParameterSetName)
            {
                case NameParameterSet:
                    installHelper.ProcessInstallParams(
                        names: Name,
                        versionRange: versionRange,
                        prerelease: Prerelease,
                        repository: Repository,
                        scope: String.Equals(Scope.ToString(), "None", StringComparison.InvariantCultureIgnoreCase) ? "" : Scope.ToString(),
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
                        pathsToInstallPkg: Utils.GetAllInstallationPaths(
                            psCmdlet: this,
                            scope: String.Equals(Scope.ToString(), "None", StringComparison.InvariantCultureIgnoreCase) ? "" : Scope.ToString()));
                    break;

                case InputObjectParameterSet:
                    // TODO
                    break;

                default:
                    Dbg.Assert(false, "Invalid parameter set");
                    break;
            }
        }

        #endregion
    }
}