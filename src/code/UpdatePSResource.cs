// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Threading;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    /// <summary>
    /// The Update-PSResource cmdlet replaces the Update-Module and Update-Script cmdlets from V2.
    /// It updates an already installed package based on the -Name parameter argument.
    /// It does not return an object. Other parameters allow the package to be updated to be further filtered.
    /// </summary>
    [Cmdlet(VerbsData.Update,
        "PSResource",
        SupportsShouldProcess = true)]
    public sealed class UpdatePSResource : PSCmdlet
    {
        #region Members
        private List<string> _pathsToInstallPkg;
        private CancellationTokenSource _cancellationTokenSource;
        private FindHelper _findHelper;
        private InstallHelper _installHelper;

        #endregion

        #region Parameters

        /// <summary>
        /// Specifies name of a resource or resources to update.
        /// Accepts wildcard characters.
        /// </summary>
        [SupportsWildcards]
        [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set ; } = new string[] {"*"};

        /// <summary>
        /// Specifies the version the resource is to be updated to.
        /// </summary>
        [SupportsWildcards]
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }

        /// <summary>
        /// When specified, allows updating to a prerelease version.
        /// </summary>
        [Parameter]
        public SwitchParameter Prerelease { get; set; }

        /// <summary>
        /// Specifies one or more repository names to update packages from.
        /// If not specified, search will include all currently registered repositories in order of highest priority.
        /// </summary>
        [SupportsWildcards]
        [Parameter]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        /// <summary>
        /// Specifies the scope of the resource to update.
        /// </summary>
        [Parameter]
        public ScopeType Scope { get; set; }

        /// <summary>
        /// The destination where the resource is to be temporarily installed to while updating.

        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string TemporaryPath
        {
            get
            { return _tmpPath; }

            set
            {
                if (WildcardPattern.ContainsWildcardCharacters(value)) 
                { 
                    throw new PSArgumentException("Wildcard characters are not allowed in the temporary path."); 
                } 
                
                // This will throw if path cannot be resolved
                _tmpPath = GetResolvedProviderPathFromPSPath(value, out ProviderInfo provider).First();
            }
        }
        private string _tmpPath;

        /// <summary>
        /// When specified, suppresses prompting for untrusted sources.
        /// </summary>
        [Parameter]
        public SwitchParameter TrustRepository { get; set; }

        /// <summary>
        /// Specifies optional credentials to be used when accessing a private repository.
        /// </summary>
        [Parameter]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// Suppresses progress information.
        /// </summary>
        [Parameter]
        public SwitchParameter Quiet { get; set; }

        /// <summary>
        /// For resources that require a license, AcceptLicense automatically accepts the license agreement during the update.
        /// </summary>
        [Parameter]
        public SwitchParameter AcceptLicense { get; set; }

        /// <summary>
        /// When specified, bypasses checks for TrustRepository and AcceptLicense and updates the package.
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Passes the resource updated to the console.
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        /// <summary>
        /// Skips the check for resource dependencies, so that only found resources are updated,
        /// and not any resources the found resource depends on.
        /// </summary>
        [Parameter]
        public SwitchParameter SkipDependencyCheck { get; set; }

        /// <summary>
        /// Check validation for signed and catalog files

        /// </summary>
        [Parameter]
        public SwitchParameter AuthenticodeCheck { get; set; }

        #endregion

        #region Override Methods

        protected override void BeginProcessing()
        {
            // Create a repository story (the PSResourceRepository.xml file) if it does not already exist
            // This is to create a better experience for those who have just installed v3 and want to get up and running quickly
            RepositorySettings.CheckRepositoryStore();

            _pathsToInstallPkg = Utils.GetAllInstallationPaths(this, Scope);
            _cancellationTokenSource = new CancellationTokenSource();
            var networkCred = Credential != null ? new NetworkCredential(Credential.UserName, Credential.Password) : null;

            _findHelper = new FindHelper(
                cancellationToken: _cancellationTokenSource.Token, 
                cmdletPassedIn: this,
                networkCredential: networkCred);

             _installHelper = new InstallHelper(cmdletPassedIn: this, networkCredential: networkCred);
        }

        protected override void ProcessRecord()
        {
            // determine/parse out Version param
            if (!Utils.TryGetVersionType(
                version: Version,
                nugetVersion: out NuGetVersion nugetVersion,
                versionRange: out VersionRange versionRange,
                versionType: out VersionType versionType,
                error: out string error))
            {
                WriteError(new ErrorRecord(
                    new ArgumentException(error),
                    "IncorrectVersionFormat",
                    ErrorCategory.InvalidArgument,
                    this));
                return;
            }

            var namesToUpdate = ProcessPackageNames(Name, versionRange, nugetVersion, versionType);

            if (namesToUpdate.Length == 0)
            {
                return;
            }

            if (!ShouldProcess(string.Format("package to update: '{0}'", String.Join(", ", Name))))
            {
                WriteVerbose(string.Format("Update is cancelled by user for: {0}", String.Join(", ", Name)));
                return;
            }

            var installedPkgs = _installHelper.BeginInstallPackages(
                names: namesToUpdate,
                versionRange: versionRange,
                nugetVersion: nugetVersion,
                versionType: versionType,
                versionString: Version,
                prerelease: Prerelease,
                repository: Repository,
                acceptLicense: AcceptLicense,
                quiet: Quiet,
                reinstall: true,
                force: Force,
                trustRepository: TrustRepository,
                noClobber: false,
                asNupkg: false,
                includeXml: true,
                skipDependencyCheck: SkipDependencyCheck,
                authenticodeCheck: AuthenticodeCheck,
                savePkg: false,
                pathsToInstallPkg: _pathsToInstallPkg,
                scope: Scope,
                tmpPath: _tmpPath,
                pkgsInstalled: new HashSet<string>(StringComparer.InvariantCultureIgnoreCase));

            if (PassThru)
            {
                foreach (PSResourceInfo pkg in installedPkgs)
                {
                    WriteObject(pkg);
                }
            }
        }

        protected override void StopProcessing()
        {
            _cancellationTokenSource?.Cancel();
        }

        protected override void EndProcessing()
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        #endregion

        #region Private Methods

        /// <Summary>
        /// This method performs a number of functions on the list of resource package names to update.
        ///  - Processes the name list for wild card characters.
        ///  - Writes errors for names with unsupported wild characters.
        ///  - Finds installed packages that match the names list.
        ///  - Finds repository packages that match the names list and update version.
        ///  - Compares installed packages and repository search results with name list.
        ///  - Returns a final list of packages for reinstall, that meet update criteria.
        /// </Summary>
        private string[] ProcessPackageNames(
            string[] namesToProcess,
            VersionRange versionRange,
            NuGetVersion nuGetVersion,
            VersionType versionType)
        {
            namesToProcess = Utils.ProcessNameWildcards(
                pkgNames: namesToProcess,
                removeWildcardEntries:false, 
                errorMsgs: out string[] errorMsgs,
                isContainWildcard: out bool _);
            
            foreach (string error in errorMsgs)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(error),
                    "ErrorFilteringNamesForUnsupportedWildcards",
                    ErrorCategory.InvalidArgument,
                    this));
            }
            
            // This catches the case where namesToProcess wasn't passed in as null or empty,
            // but after filtering out unsupported wildcard names there are no elements left in namesToProcess.
            if (namesToProcess.Length == 0)
            {
                 return Utils.EmptyStrArray;
            }

            if (String.Equals(namesToProcess[0], "*", StringComparison.InvariantCultureIgnoreCase))
            {
                WriteVerbose("Package names were detected to be (or contain an element equal to): '*', so all packages will be updated");
            }

            // Get all installed packages selected for updating.
            GetHelper getHelper = new GetHelper(cmdletPassedIn: this);
            var installedPackages = new Dictionary<string, PSResourceInfo>(StringComparer.InvariantCultureIgnoreCase);

            // selectPrereleaseOnly is false because even if Prerelease is true we want to include both stable and prerelease, not select prerelease only.
            foreach (var installedPackage in getHelper.GetPackagesFromPath(
                name: namesToProcess,
                versionRange: VersionRange.All,
                pathsToSearch: Utils.GetAllResourcePaths(this, Scope),
                selectPrereleaseOnly: false))
            {
                if (!installedPackages.ContainsKey(installedPackage.Name))
                {
                    installedPackages.Add(installedPackage.Name, installedPackage);
                }
            }

            if (installedPackages.Count is 0)
            {
                var message = $"No installed packages were found with name '{string.Join(", ", namesToProcess)}' in scope '{Scope}'. First install package using 'Install-PSResource'.";

                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(message),
                    "NoInstalledPackagesFoundWithNameProvided",
                    ErrorCategory.InvalidOperation,
                    this));
                    
                return Utils.EmptyStrArray;
            }

            // Find all packages selected for updating in provided repositories.
            var repositoryPackages = new Dictionary<string, PSResourceInfo>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var foundResource in _findHelper.FindByResourceName(
                name: installedPackages.Keys.ToArray(),
                type: ResourceType.None,
                versionRange: versionRange,
                nugetVersion: nuGetVersion,
                versionType: versionType,
                version: Version,
                prerelease: Prerelease,
                tag: null,
                repository: Repository,
                includeDependencies: !SkipDependencyCheck))
            {
                if (!repositoryPackages.ContainsKey(foundResource.Name))
                {
                    repositoryPackages.Add(foundResource.Name, foundResource);
                }
            }

            // Check if named package is installed or can be found in the repositories.
            foreach (var nameToProcess in namesToProcess)
            {
                if (!WildcardPattern.ContainsWildcardCharacters(nameToProcess))
                {
                    if (!installedPackages.ContainsKey(nameToProcess))
                    {
                        WriteWarning(
                            $"Package '{nameToProcess}' not installed in scope '{Scope}'. First install package using 'Install-PSResource'.");
                    }
                    else if (!repositoryPackages.ContainsKey(nameToProcess))
                    {
                        WriteWarning(
                            $"Installed package '{nameToProcess}':'{Version}' was not found in repositories and cannot be updated.");
                    }
                }
            }

            // Create list of packages to update.
            List<string> namesToUpdate = new List<string>();
            foreach (PSResourceInfo repositoryPackage in repositoryPackages.Values)
            {
                if (!installedPackages.TryGetValue(repositoryPackage.Name, out PSResourceInfo installedPackage))
                {
                    continue;
                }

                string repoNuGetVersionString = Utils.GetNormalizedVersionString(repositoryPackage.Version.ToString(), repositoryPackage.Prerelease);
                // If the current package is out of range, install it with the correct version.
                if (!NuGetVersion.TryParse(repoNuGetVersionString, out NuGetVersion repoVersion))
                {
                    WriteWarning($"Cannot parse nuget version for repository package '{repositoryPackage.Name}'. Cannot update package.");
                    continue;
                }

                // If the current package is out of range, install it with the correct version.
                if (!NuGetVersion.TryParse(installedPackage.Version.ToString(), out NuGetVersion installedVersion))
                {
                    WriteWarning($"Cannot parse nuget version in installed package '{installedPackage.Name}'. Cannot update package.");
                    continue;
                }

                // cases in which to update:
                // versionRange: null/*,        , repoVersion : 2.0.0-beta, installedVersion: 1.5.0
                // versionRange: [1.8.0, 2.1.0] , repoVersion: 2.0.0, installedVersion: 1.6.0 
                // versionRange: [, 2.1.0]      , repoVersion: 2.0.0, installedVersion: 1.5.0 (installedVersion satisfies requirement, but there's later version)
                if (((versionRange == null || versionRange == VersionRange.All) && repoVersion > installedVersion) ||
                    versionRange != null && repoVersion > installedVersion && versionRange.Satisfies(repoVersion))
                {
                    namesToUpdate.Add(repositoryPackage.Name);
                }
                else
                {
                    WriteVerbose($"Installed package {repositoryPackage.Name} {repositoryPackage.Version} is already up to date.");
                }
            }

            return namesToUpdate.ToArray();
        }

        #endregion
    }
}
