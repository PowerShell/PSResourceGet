// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using MoreLinq.Extensions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.PackageExtraction;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// Install helper class
    /// </summary>
    internal class InstallHelper : PSCmdlet
    {
        #region Members

        public const string PSDataFileExt = ".psd1";
        public const string PSScriptFileExt = ".ps1";
        private const string MsgRepositoryNotTrusted = "Untrusted repository";
        private const string MsgInstallUntrustedPackage = "You are installing the modules from an untrusted repository. If you trust this repository, change its Trusted value by running the Set-PSResourceRepository cmdlet. Are you sure you want to install the PSresource from '{0}' ?";

        private CancellationToken _cancellationToken;
        private readonly PSCmdlet _cmdletPassedIn;
        private List<string> _pathsToInstallPkg;
        private VersionRange _versionRange;
        private bool _prerelease;
        private bool _acceptLicense;
        private bool _quiet;
        private bool _reinstall;
        private bool _force;
        private bool _trustRepository;
        private PSCredential _credential;
        private bool _asNupkg;
        private bool _includeXML;
        private bool _noClobber;
        private bool _savePkg;
        List<string> _pathsToSearch;
        List<string> _pkgNamesToInstall;

        #endregion

        #region Public methods

        public InstallHelper(PSCmdlet cmdletPassedIn)
        {
            CancellationTokenSource source = new CancellationTokenSource();
            _cancellationToken = source.Token;
            _cmdletPassedIn = cmdletPassedIn;
        }

        public List<PSResourceInfo> InstallPackages(
            string[] names,
            VersionRange versionRange,
            bool prerelease,
            string[] repository,
            bool acceptLicense,
            bool quiet,
            bool reinstall,
            bool force,
            bool trustRepository,
            bool noClobber,
            PSCredential credential,
            bool asNupkg,
            bool includeXML,
            bool skipDependencyCheck,
            bool savePkg,
            List<string> pathsToInstallPkg)
        {
            _cmdletPassedIn.WriteVerbose(string.Format("Parameters passed in >>> Name: '{0}'; Version: '{1}'; Prerelease: '{2}'; Repository: '{3}'; " +
                "AcceptLicense: '{4}'; Quiet: '{5}'; Reinstall: '{6}'; TrustRepository: '{7}'; NoClobber: '{8}'; AsNupkg: '{9}'; IncludeXML '{10}'; SavePackage '{11}'",
                string.Join(",", names),
                versionRange != null ? (versionRange.OriginalString != null ? versionRange.OriginalString : string.Empty) : string.Empty,
                prerelease.ToString(),
                repository != null ? string.Join(",", repository) : string.Empty,
                acceptLicense.ToString(),
                quiet.ToString(),
                reinstall.ToString(),
                trustRepository.ToString(),
                noClobber.ToString(),
                asNupkg.ToString(),
                includeXML.ToString(),
                savePkg.ToString()));

            _versionRange = versionRange;
            _prerelease = prerelease;
            _acceptLicense = acceptLicense || force;
            _quiet = quiet;
            _reinstall = reinstall;
            _force = force;
            _trustRepository = trustRepository || force;
            _noClobber = noClobber;
            _credential = credential;
            _asNupkg = asNupkg;
            _includeXML = includeXML;
            _savePkg = savePkg;
            _pathsToInstallPkg = pathsToInstallPkg;

            // Create list of installation paths to search.
            _pathsToSearch = new List<string>();
            _pkgNamesToInstall = names.ToList();

            // _pathsToInstallPkg will only contain the paths specified within the -Scope param (if applicable)
            // _pathsToSearch will contain all resource package subdirectories within _pathsToInstallPkg path locations
            // e.g.:
            // ./InstallPackagePath1/PackageA
            // ./InstallPackagePath1/PackageB
            // ./InstallPackagePath2/PackageC
            // ./InstallPackagePath3/PackageD
            foreach (var path in _pathsToInstallPkg)
            {
                _pathsToSearch.AddRange(Utils.GetSubDirectories(path));
            }

            // Go through the repositories and see which is the first repository to have the pkg version available
            return ProcessRepositories(
                repository: repository,
                trustRepository: _trustRepository,
                credential: _credential,
                skipDependencyCheck: skipDependencyCheck);
        }

        #endregion

        #region Private methods

        // This method calls iterates through repositories (by priority order) to search for the pkgs to install
        private List<PSResourceInfo> ProcessRepositories(
            string[] repository,
            bool trustRepository,
            PSCredential credential,
            bool skipDependencyCheck)
        {
            var listOfRepositories = RepositorySettings.Read(repository, out string[] _);
            var yesToAll = false;
            var noToAll = false;

            var findHelper = new FindHelper(_cancellationToken, _cmdletPassedIn);
            List<PSResourceInfo> allPkgsInstalled = new List<PSResourceInfo>();

            foreach (var repo in listOfRepositories)
            {
                // If no more packages to install, then return
                if (!_pkgNamesToInstall.Any()) return allPkgsInstalled;

                string repoName = repo.Name;
                _cmdletPassedIn.WriteVerbose(string.Format("Attempting to search for packages in '{0}'", repoName));

                // Source is only trusted if it's set at the repository level to be trusted, -TrustRepository flag is true, -Force flag is true
                // OR the user issues trust interactively via console.
                var sourceTrusted = true;
                if (repo.Trusted == false && !trustRepository && !_force)
                {
                    _cmdletPassedIn.WriteVerbose("Checking if untrusted repository should be used");

                    if (!(yesToAll || noToAll))
                    {
                        // Prompt for installation of package from untrusted repository
                        var message = string.Format(CultureInfo.InvariantCulture, MsgInstallUntrustedPackage, repoName);
                        sourceTrusted = _cmdletPassedIn.ShouldContinue(message, MsgRepositoryNotTrusted, true, ref yesToAll, ref noToAll);
                    }
                }

                if (!sourceTrusted && !yesToAll)
                {
                    continue;
                }

                _cmdletPassedIn.WriteVerbose("Untrusted repository accepted as trusted source.");

                // If it can't find the pkg in one repository, it'll look for it in the next repo in the list
                var isLocalRepo = repo.Uri.AbsoluteUri.StartsWith(Uri.UriSchemeFile + Uri.SchemeDelimiter, StringComparison.OrdinalIgnoreCase);

                // Finds parent packages and dependencies
                IEnumerable<PSResourceInfo> pkgsFromRepoToInstall = findHelper.FindByResourceName(
                    name: _pkgNamesToInstall.ToArray(),
                    type: ResourceType.None,
                    version: _versionRange != null ? _versionRange.OriginalString : null,
                    prerelease: _prerelease,
                    tag: null,
                    repository: new string[] { repoName },
                    credential: credential,
                    includeDependencies: !skipDependencyCheck);

                if (!pkgsFromRepoToInstall.Any())
                {
                    _cmdletPassedIn.WriteVerbose(string.Format("None of the specified resources were found in the '{0}' repository.", repoName));
                    // Check in the next repository
                    continue;
                }

                // Select the first package from each name group, which is guaranteed to be the latest version.
                // We should only have one version returned for each package name
                // e.g.:
                // PackageA (version 1.0)
                // PackageB (version 2.0)
                // PackageC (version 1.0)
                pkgsFromRepoToInstall = pkgsFromRepoToInstall.GroupBy(
                    m => new { m.Name }).Select(
                        group => group.First()).ToList();

                // Check to see if the pkgs (including dependencies) are already installed (ie the pkg is installed and the version satisfies the version range provided via param)
                if (!_reinstall)
                {
                    pkgsFromRepoToInstall = FilterByInstalledPkgs(pkgsFromRepoToInstall);
                }

                if (!pkgsFromRepoToInstall.Any())
                {
                    continue;
                }

                List<PSResourceInfo> pkgsInstalled = InstallPackage(
                    pkgsFromRepoToInstall,
                    repoName,
                    repo.Uri.AbsoluteUri,
                    repo.CredentialInfo,
                    credential,
                    isLocalRepo);

                foreach (PSResourceInfo pkg in pkgsInstalled)
                {
                    _pkgNamesToInstall.RemoveAll(x => x.Equals(pkg.Name, StringComparison.InvariantCultureIgnoreCase));
                }

                allPkgsInstalled.AddRange(pkgsInstalled);
            }

            // At this only package names left were those which could not be found in registered repositories
            foreach (string pkgName in _pkgNamesToInstall)
            {
                var message = String.Format("Package '{0}' with requested version range {1} could not be installed as it was not found in any registered repositories",
                    pkgName,
                    _versionRange.ToString());
                var ex = new ArgumentException(message);
                var ResourceNotFoundError = new ErrorRecord(ex, "ResourceNotFoundError", ErrorCategory.ObjectNotFound, null);
                _cmdletPassedIn.WriteError(ResourceNotFoundError);
            }

            return allPkgsInstalled;
        }

        // Check if any of the pkg versions are already installed, if they are we'll remove them from the list of packages to install
        private IEnumerable<PSResourceInfo> FilterByInstalledPkgs(IEnumerable<PSResourceInfo> packages)
        {
            // Create list of installation paths to search.
            List<string> _pathsToSearch = new List<string>();
            // _pathsToInstallPkg will only contain the paths specified within the -Scope param (if applicable)
            // _pathsToSearch will contain all resource package subdirectories within _pathsToInstallPkg path locations
            // e.g.:
            // ./InstallPackagePath1/PackageA
            // ./InstallPackagePath1/PackageB
            // ./InstallPackagePath2/PackageC
            // ./InstallPackagePath3/PackageD
            foreach (var path in _pathsToInstallPkg)
            {
                _pathsToSearch.AddRange(Utils.GetSubDirectories(path));
            }

            var filteredPackages = new Dictionary<string, PSResourceInfo>();
            foreach (var pkg in packages)
            {
                filteredPackages.Add(pkg.Name, pkg);
            }

            GetHelper getHelper = new GetHelper(_cmdletPassedIn);
            // Get currently installed packages.
            // selectPrereleaseOnly is false because even if Prerelease is true we want to include both stable and prerelease, never select prerelease only.
            IEnumerable<PSResourceInfo> pkgsAlreadyInstalled = getHelper.GetPackagesFromPath(
                name: filteredPackages.Keys.ToArray(),
                versionRange: _versionRange,
                pathsToSearch: _pathsToSearch,
                selectPrereleaseOnly: false);
            if (!pkgsAlreadyInstalled.Any())
            {
                return packages;
            }

            // Remove from list package versions that are already installed.
            foreach (PSResourceInfo pkg in pkgsAlreadyInstalled)
            {
                _cmdletPassedIn.WriteWarning(
                    string.Format("Resource '{0}' with version '{1}' is already installed.  If you would like to reinstall, please run the cmdlet again with the -Reinstall parameter",
                    pkg.Name,
                    pkg.Version));

                filteredPackages.Remove(pkg.Name);
                _pkgNamesToInstall.RemoveAll(x => x.Equals(pkg.Name, StringComparison.InvariantCultureIgnoreCase));
            }

            return filteredPackages.Values.ToArray();
        }

        private List<PSResourceInfo> InstallPackage(
            IEnumerable<PSResourceInfo> pkgsToInstall, // those found to be required to be installed (includes Dependency packages as well)
            string repoName,
            string repoUri,
            PSCredentialInfo repoCredentialInfo,
            PSCredential credential,
            bool isLocalRepo)
        {
            List<PSResourceInfo> pkgsSuccessfullyInstalled = new List<PSResourceInfo>();
            int totalPkgs = pkgsToInstall.Count();

            // Counters for tracking current package out of total
            int totalInstalledPkgCount = 0;
            foreach (PSResourceInfo pkg in pkgsToInstall)
            {
                totalInstalledPkgCount++;
                var tempInstallPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                try
                {
                    // Create a temp directory to install to
                    var dir = Directory.CreateDirectory(tempInstallPath);  // should check it gets created properly
                                                                           // To delete file attributes from the existing ones get the current file attributes first and use AND (&) operator
                                                                           // with a mask (bitwise complement of desired attributes combination).
                                                                           // TODO: check the attributes and if it's read only then set it
                                                                           // attribute may be inherited from the parent
                                                                           // TODO:  are there Linux accommodations we need to consider here?
                    dir.Attributes &= ~FileAttributes.ReadOnly;

                    _cmdletPassedIn.WriteVerbose(string.Format("Begin installing package: '{0}'", pkg.Name));

                    if (!_quiet)
                    {
                        int activityId = 0;
                        int percentComplete = ((totalInstalledPkgCount * 100) / totalPkgs);
                        string activity = string.Format("Installing {0}...", pkg.Name);
                        string statusDescription = string.Format("{0}% Complete", percentComplete);
                        _cmdletPassedIn.WriteProgress(
                            new ProgressRecord(activityId, activity, statusDescription));
                    }

                    // Create PackageIdentity in order to download
                    string createFullVersion = pkg.Version.ToString();
                    if (pkg.IsPrerelease)
                    {
                        createFullVersion = pkg.Version.ToString() + "-" + pkg.Prerelease;
                    }

                    if (!NuGetVersion.TryParse(createFullVersion, out NuGetVersion pkgVersion))
                    {
                        var message = String.Format("{0} package could not be installed with error: could not parse package '{0}' version '{1} into a NuGetVersion",
                            pkg.Name,
                            pkg.Version.ToString());
                        var ex = new ArgumentException(message);
                        var packageIdentityVersionParseError = new ErrorRecord(ex, "psdataFileNotExistError", ErrorCategory.ReadError, null);
                        _cmdletPassedIn.WriteError(packageIdentityVersionParseError);
                        _pkgNamesToInstall.RemoveAll(x => x.Equals(pkg.Name, StringComparison.InvariantCultureIgnoreCase));
                        continue;
                    }

                    var pkgIdentity = new PackageIdentity(pkg.Name, pkgVersion);
                    var cacheContext = new SourceCacheContext();

                    if (isLocalRepo)
                    {
                        /* Download from a local repository -- this is slightly different process than from a server */
                        var localResource = new FindLocalPackagesResourceV2(repoUri);
                        var resource = new LocalDownloadResource(repoUri, localResource);

                        // Actually downloading the .nupkg from a local repo
                        var result = resource.GetDownloadResourceResultAsync(
                             identity: pkgIdentity,
                             downloadContext: new PackageDownloadContext(cacheContext),
                             globalPackagesFolder: tempInstallPath,
                             logger: NullLogger.Instance,
                             token: _cancellationToken).GetAwaiter().GetResult();

                        // Create the package extraction context
                        PackageExtractionContext packageExtractionContext = new PackageExtractionContext(
                                packageSaveMode: PackageSaveMode.Nupkg,
                                xmlDocFileSaveMode: PackageExtractionBehavior.XmlDocFileSaveMode,
                                clientPolicyContext: null,
                                logger: NullLogger.Instance);

                        // Extracting from .nupkg and placing files into tempInstallPath
                        result.PackageReader.CopyFiles(
                            destination: tempInstallPath,
                            packageFiles: result.PackageReader.GetFiles(),
                            extractFile: new PackageFileExtractor(
                                result.PackageReader.GetFiles(),
                                packageExtractionContext.XmlDocFileSaveMode).ExtractPackageFile,
                            logger: NullLogger.Instance,
                            token: _cancellationToken);
                        result.Dispose();
                    }
                    else
                    {
                        /* Download from a non-local repository */
                        // Set up NuGet API resource for download
                        PackageSource source = new PackageSource(repoUri);

                        // Explicitly passed in Credential takes precedence over repository CredentialInfo
                        if (credential != null)
                        {
                            string password = new NetworkCredential(string.Empty, credential.Password).Password;
                            source.Credentials = PackageSourceCredential.FromUserInput(repoUri, credential.UserName, password, true, null);
                        }
                        else if (repoCredentialInfo != null)
                        {
                            PSCredential repoCredential = Utils.GetRepositoryCredentialFromSecretManagement(
                                repoName,
                                repoCredentialInfo,
                                _cmdletPassedIn);

                            string password = new NetworkCredential(string.Empty, repoCredential.Password).Password;
                            source.Credentials = PackageSourceCredential.FromUserInput(repoUri, repoCredential.UserName, password, true, null);
                        }
                        var provider = FactoryExtensionsV3.GetCoreV3(NuGet.Protocol.Core.Types.Repository.Provider);
                        SourceRepository repository = new SourceRepository(source, provider);

                        /* Download from a non-local repository -- ie server */
                        var downloadResource = repository.GetResourceAsync<DownloadResource>().GetAwaiter().GetResult();
                        DownloadResourceResult result = null;
                        try
                        {
                            result = downloadResource.GetDownloadResourceResultAsync(
                                identity: pkgIdentity,
                                downloadContext: new PackageDownloadContext(cacheContext),
                                globalPackagesFolder: tempInstallPath,
                                logger: NullLogger.Instance,
                                token: _cancellationToken).GetAwaiter().GetResult();
                        }
                        catch (Exception e)
                        {
                            _cmdletPassedIn.WriteVerbose(string.Format("Error attempting download: '{0}'", e.Message));
                        }
                        finally
                        {
                            // Need to close the .nupkg
                            if (result != null) result.Dispose();
                        }
                    }

                    _cmdletPassedIn.WriteVerbose(string.Format("Successfully able to download package from source to: '{0}'", tempInstallPath));

                    // pkgIdentity.Version.Version gets the version without metadata or release labels.
                    string newVersion = pkgIdentity.Version.ToNormalizedString();
                    string normalizedVersionNoPrerelease = newVersion;
                    if (pkgIdentity.Version.IsPrerelease)
                    {
                        // eg: 2.0.2
                        normalizedVersionNoPrerelease = pkgIdentity.Version.ToNormalizedString().Substring(0, pkgIdentity.Version.ToNormalizedString().IndexOf('-'));
                    }

                    string tempDirNameVersion = isLocalRepo ? tempInstallPath : Path.Combine(tempInstallPath, pkgIdentity.Id.ToLower(), newVersion);
                    var version4digitNoPrerelease = pkgIdentity.Version.Version.ToString();
                    string moduleManifestVersion = string.Empty;
                    var scriptPath = Path.Combine(tempDirNameVersion, pkg.Name + PSScriptFileExt);
                    var modulePath = Path.Combine(tempDirNameVersion, pkg.Name + PSDataFileExt);
                    // Check if the package is a module or a script
                    var isModule = File.Exists(modulePath);

                    string installPath;
                    if (_savePkg)
                    {
                        // For save the installation path is what is passed in via -Path
                        installPath = _pathsToInstallPkg.FirstOrDefault();

                        // If saving as nupkg simply copy the nupkg and move onto next iteration of loop
                        // asNupkg functionality only applies to Save-PSResource
                        if (_asNupkg)
                        {
                            var nupkgFile = pkgIdentity.ToString().ToLower() + ".nupkg";
                            File.Copy(Path.Combine(tempDirNameVersion, nupkgFile), Path.Combine(installPath, nupkgFile));

                            _cmdletPassedIn.WriteVerbose(string.Format("'{0}' moved into file path '{1}'", nupkgFile, installPath));
                            pkgsSuccessfullyInstalled.Add(pkg);

                            continue;
                        }
                    }
                    else
                    {
                        // PSModules:
                        /// ./Modules
                        /// ./Scripts
                        /// _pathsToInstallPkg is sorted by desirability, Find will pick the pick the first Script or Modules path found in the list
                        installPath = isModule ? _pathsToInstallPkg.Find(path => path.EndsWith("Modules", StringComparison.InvariantCultureIgnoreCase))
                                : _pathsToInstallPkg.Find(path => path.EndsWith("Scripts", StringComparison.InvariantCultureIgnoreCase));
                    }

                    if (isModule)
                    {
                        var moduleManifest = Path.Combine(tempDirNameVersion, pkgIdentity.Id + PSDataFileExt);
                        if (!File.Exists(moduleManifest))
                        {
                            var message = String.Format("{0} package could not be installed with error: Module manifest file: {1} does not exist. This is not a valid PowerShell module.", pkgIdentity.Id, moduleManifest);

                            var ex = new ArgumentException(message);
                            var psdataFileDoesNotExistError = new ErrorRecord(ex, "psdataFileNotExistError", ErrorCategory.ReadError, null);
                            _cmdletPassedIn.WriteError(psdataFileDoesNotExistError);
                            _pkgNamesToInstall.RemoveAll(x => x.Equals(pkg.Name, StringComparison.InvariantCultureIgnoreCase));
                            continue;
                        }

                        if (!Utils.TryParsePSDataFile(moduleManifest, _cmdletPassedIn, out Hashtable parsedMetadataHashtable))
                        {
                            // Ran into errors parsing the module manifest file which was found in Utils.ParseModuleManifest() and written.
                            continue;
                        }

                        moduleManifestVersion = parsedMetadataHashtable["ModuleVersion"] as string;

                        // Accept License verification
                        if (!_savePkg && !CallAcceptLicense(pkg, moduleManifest, tempInstallPath, newVersion))
                        {
                            continue;
                        }

                        // If NoClobber is specified, ensure command clobbering does not happen
                        if (_noClobber && !DetectClobber(pkg.Name, parsedMetadataHashtable))
                        {
                            continue;
                        }
                    }

                    // Delete the extra nupkg related files that are not needed and not part of the module/script
                    DeleteExtraneousFiles(pkgIdentity, tempDirNameVersion);

                    if (_includeXML)
                    {
                        CreateMetadataXMLFile(tempDirNameVersion, installPath, pkg, isModule);
                    }

                    MoveFilesIntoInstallPath(
                        pkg,
                        isModule,
                        isLocalRepo,
                        tempDirNameVersion,
                        tempInstallPath,
                        installPath,
                        newVersion,
                        moduleManifestVersion,
                        scriptPath);

                    _cmdletPassedIn.WriteVerbose(String.Format("Successfully installed package '{0}' to location '{1}'", pkg.Name, installPath));
                    pkgsSuccessfullyInstalled.Add(pkg);
                }
                catch (Exception e)
                {
                    _cmdletPassedIn.WriteError(
                        new ErrorRecord(
                            new PSInvalidOperationException(
                                message: $"Unable to successfully install package '{pkg.Name}': '{e.Message}'",
                                innerException: e),
                            "InstallPackageFailed",
                            ErrorCategory.InvalidOperation,
                            _cmdletPassedIn));
                    _pkgNamesToInstall.RemoveAll(x => x.Equals(pkg.Name, StringComparison.InvariantCultureIgnoreCase));
                }
                finally
                {
                    // Delete the temp directory and all its contents
                    _cmdletPassedIn.WriteVerbose(string.Format("Attempting to delete '{0}'", tempInstallPath));

                    if (Directory.Exists(tempInstallPath))
                    {
                        if (!TryDeleteDirectory(tempInstallPath, out ErrorRecord errorMsg))
                        {
                            _cmdletPassedIn.WriteError(errorMsg);
                        }
                        else
                        {
                            _cmdletPassedIn.WriteVerbose(String.Format("Successfully deleted '{0}'", tempInstallPath));
                        }
                    }
                }
            }

            return pkgsSuccessfullyInstalled;
        }

        private bool CallAcceptLicense(PSResourceInfo p, string moduleManifest, string tempInstallPath, string newVersion)
        {
            var requireLicenseAcceptance = false;
            var success = true;

            if (File.Exists(moduleManifest))
            {
                using (StreamReader sr = new StreamReader(moduleManifest))
                {
                    var text = sr.ReadToEnd();

                    var pattern = "RequireLicenseAcceptance\\s*=\\s*\\$true";
                    var patternToSkip1 = "#\\s*RequireLicenseAcceptance\\s*=\\s*\\$true";
                    var patternToSkip2 = "\\*\\s*RequireLicenseAcceptance\\s*=\\s*\\$true";

                    Regex rgx = new Regex(pattern);
                    Regex rgxComment1 = new Regex(patternToSkip1);
                    Regex rgxComment2 = new Regex(patternToSkip2);
                    if (rgx.IsMatch(text) && !rgxComment1.IsMatch(text) && !rgxComment2.IsMatch(text))
                    {
                        requireLicenseAcceptance = true;
                    }
                }

                // Licesnse agreement processing
                if (requireLicenseAcceptance)
                {
                    // If module requires license acceptance and -AcceptLicense is not passed in, display prompt
                    if (!_acceptLicense)
                    {
                        var PkgTempInstallPath = Path.Combine(tempInstallPath, p.Name, newVersion);
                        var LicenseFilePath = Path.Combine(PkgTempInstallPath, "License.txt");

                        if (!File.Exists(LicenseFilePath))
                        {
                            var exMessage = String.Format("{0} package could not be installed with error: License.txt not found. License.txt must be provided when user license acceptance is required.", p.Name);
                            var ex = new ArgumentException(exMessage);
                            var acceptLicenseError = new ErrorRecord(ex, "LicenseTxtNotFound", ErrorCategory.ObjectNotFound, null);

                            _cmdletPassedIn.WriteError(acceptLicenseError);
                            _pkgNamesToInstall.RemoveAll(x => x.Equals(p.Name, StringComparison.InvariantCultureIgnoreCase));
                            success = false;
                        }

                        // Otherwise read LicenseFile
                        string licenseText = System.IO.File.ReadAllText(LicenseFilePath);
                        var acceptanceLicenseQuery = $"Do you accept the license terms for module '{p.Name}'.";
                        var message = licenseText + "`r`n" + acceptanceLicenseQuery;

                        var title = "License Acceptance";
                        var yesToAll = false;
                        var noToAll = false;
                        var shouldContinueResult = _cmdletPassedIn.ShouldContinue(message, title, true, ref yesToAll, ref noToAll);

                        if (shouldContinueResult || yesToAll)
                        {
                            _acceptLicense = true;
                        }
                    }

                    // Check if user agreed to license terms, if they didn't then throw error, otherwise continue to install
                    if (!_acceptLicense)
                    {
                        var message = String.Format("{0} package could not be installed with error: License Acceptance is required for module '{0}'. Please specify '-AcceptLicense' to perform this operation.", p.Name);
                        var ex = new ArgumentException(message);
                        var acceptLicenseError = new ErrorRecord(ex, "ForceAcceptLicense", ErrorCategory.InvalidArgument, null);

                        _cmdletPassedIn.WriteError(acceptLicenseError);
                        _pkgNamesToInstall.RemoveAll(x => x.Equals(p.Name, StringComparison.InvariantCultureIgnoreCase));
                        success = false;
                    }
                }
            }

            return success;
        }

        private bool DetectClobber(string pkgName, Hashtable parsedMetadataHashtable)
        {
            // Get installed modules, then get all possible paths
            bool foundClobber = false;
            GetHelper getHelper = new GetHelper(_cmdletPassedIn);
            // selectPrereleaseOnly is false because even if Prerelease is true we want to include both stable and prerelease, never select prerelease only.
            IEnumerable<PSResourceInfo> pkgsAlreadyInstalled = getHelper.GetPackagesFromPath(
                name: new string[] { "*" },
                versionRange: VersionRange.All,
                pathsToSearch: _pathsToSearch,
                selectPrereleaseOnly: false);
            // user parsed metadata hash
            List<string> listOfCmdlets = new List<string>();
            foreach (var cmdletName in parsedMetadataHashtable["CmdletsToExport"] as object[])
            {
                listOfCmdlets.Add(cmdletName as string);

            }

            foreach (var pkg in pkgsAlreadyInstalled)
            {
                List<string> duplicateCmdlets = new List<string>();
                List<string> duplicateCmds = new List<string>();
                // See if any of the cmdlets or commands in the pkg we're trying to install exist within a package that's already installed
                if (pkg.Includes.Cmdlet != null && pkg.Includes.Cmdlet.Any())
                {
                    duplicateCmdlets = listOfCmdlets.Where(cmdlet => pkg.Includes.Cmdlet.Contains(cmdlet)).ToList();

                }

                if (pkg.Includes.Command != null && pkg.Includes.Command.Any())
                {
                    duplicateCmds = listOfCmdlets.Where(commands => pkg.Includes.Command.Contains(commands, StringComparer.InvariantCultureIgnoreCase)).ToList();
                }

                if (duplicateCmdlets.Any() || duplicateCmds.Any())
                {

                    duplicateCmdlets.AddRange(duplicateCmds);

                    var errMessage = string.Format(
                        "{1} package could not be installed with error: The following commands are already available on this system: '{0}'. This module '{1}' may override the existing commands. If you still want to install this module '{1}', remove the -NoClobber parameter.",
                        String.Join(", ", duplicateCmdlets), pkgName);

                    var ex = new ArgumentException(errMessage);
                    var noClobberError = new ErrorRecord(ex, "CommandAlreadyExists", ErrorCategory.ResourceExists, null);

                    _cmdletPassedIn.WriteError(noClobberError);
                    _pkgNamesToInstall.RemoveAll(x => x.Equals(pkgName, StringComparison.InvariantCultureIgnoreCase));
                    foundClobber = true;

                    return foundClobber;
                }
            }

            return foundClobber;
        }

        private void CreateMetadataXMLFile(string dirNameVersion, string installPath, PSResourceInfo pkg, bool isModule)
        {
            // Script will have a metadata file similar to:  "TestScript_InstalledScriptInfo.xml"
            // Modules will have the metadata file: "PSGetModuleInfo.xml"
            var metadataXMLPath = isModule ? Path.Combine(dirNameVersion, "PSGetModuleInfo.xml")
                : Path.Combine(dirNameVersion, (pkg.Name + "_InstalledScriptInfo.xml"));

            pkg.InstalledDate = DateTime.Now;
            pkg.InstalledLocation = installPath;

            // Write all metadata into metadataXMLPath
            if (!pkg.TryWrite(metadataXMLPath, out string error))
            {
                var message = string.Format("{0} package could not be installed with error: Error parsing metadata into XML: '{1}'", pkg.Name, error);
                var ex = new ArgumentException(message);
                var ErrorParsingMetadata = new ErrorRecord(ex, "ErrorParsingMetadata", ErrorCategory.ParserError, null);

                _cmdletPassedIn.WriteError(ErrorParsingMetadata);
                _pkgNamesToInstall.RemoveAll(x => x.Equals(pkg.Name, StringComparison.InvariantCultureIgnoreCase));
            }
        }

        private void DeleteExtraneousFiles(PackageIdentity pkgIdentity, string dirNameVersion)
        {
            // Deleting .nupkg SHA file, .nuspec, and .nupkg after unpacking the module
            var pkgIdString = pkgIdentity.ToString();
            var nupkgSHAToDelete = Path.Combine(dirNameVersion, pkgIdString + ".nupkg.sha512");
            var nuspecToDelete = Path.Combine(dirNameVersion, pkgIdentity.Id + ".nuspec");
            var nupkgToDelete = Path.Combine(dirNameVersion, pkgIdString + ".nupkg");
            var nupkgMetadataToDelete = Path.Combine(dirNameVersion, pkgIdString + ".nupkg.metadata");
            var contentTypesToDelete = Path.Combine(dirNameVersion, "[Content_Types].xml");
            var relsDirToDelete = Path.Combine(dirNameVersion, "_rels");
            var packageDirToDelete = Path.Combine(dirNameVersion, "package");

            // Unforunately have to check if each file exists because it may or may not be there
            if (File.Exists(nupkgSHAToDelete))
            {
                _cmdletPassedIn.WriteVerbose(string.Format("Deleting '{0}'", nupkgSHAToDelete));
                File.Delete(nupkgSHAToDelete);
            }
            if (File.Exists(nuspecToDelete))
            {
                _cmdletPassedIn.WriteVerbose(string.Format("Deleting '{0}'", nuspecToDelete));
                File.Delete(nuspecToDelete);
            }
            if (File.Exists(nupkgToDelete))
            {
                _cmdletPassedIn.WriteVerbose(string.Format("Deleting '{0}'", nupkgToDelete));
                File.Delete(nupkgToDelete);
            }
            if (File.Exists(nupkgMetadataToDelete))
            {
                _cmdletPassedIn.WriteVerbose(string.Format("Deleting '{0}'", nupkgMetadataToDelete));
                File.Delete(nupkgMetadataToDelete);
            }
            if (File.Exists(contentTypesToDelete))
            {
                _cmdletPassedIn.WriteVerbose(string.Format("Deleting '{0}'", contentTypesToDelete));
                File.Delete(contentTypesToDelete);
            }
            if (Directory.Exists(relsDirToDelete))
            {
                _cmdletPassedIn.WriteVerbose(string.Format("Deleting '{0}'", relsDirToDelete));
                Utils.DeleteDirectory(relsDirToDelete);
            }
            if (Directory.Exists(packageDirToDelete))
            {
                _cmdletPassedIn.WriteVerbose(string.Format("Deleting '{0}'", packageDirToDelete));
                Utils.DeleteDirectory(packageDirToDelete);
            }
        }

        private bool TryDeleteDirectory(
            string tempInstallPath,
            out ErrorRecord errorMsg)
        {
            errorMsg = null;

            try
            {
                Utils.DeleteDirectory(tempInstallPath);
            }
            catch (Exception e)
            {
                var TempDirCouldNotBeDeletedError = new ErrorRecord(e, "errorDeletingTempInstallPath", ErrorCategory.InvalidResult, null);
                errorMsg = TempDirCouldNotBeDeletedError;
                return false;
            }

            return true;
        }

        private void MoveFilesIntoInstallPath(
            PSResourceInfo pkgInfo,
            bool isModule,
            bool isLocalRepo,
            string dirNameVersion,
            string tempInstallPath,
            string installPath,
            string newVersion,
            string moduleManifestVersion,
            string scriptPath)
        {
            // Creating the proper installation path depending on whether pkg is a module or script
            var newPathParent = isModule ? Path.Combine(installPath, pkgInfo.Name) : installPath;
            var finalModuleVersionDir = isModule ? Path.Combine(installPath, pkgInfo.Name, moduleManifestVersion) : installPath;

            // If script, just move the files over, if module, move the version directory over
            var tempModuleVersionDir = (!isModule || isLocalRepo) ? dirNameVersion
                : Path.Combine(tempInstallPath, pkgInfo.Name.ToLower(), newVersion);

            _cmdletPassedIn.WriteVerbose(string.Format("Installation source path is: '{0}'", tempModuleVersionDir));
            _cmdletPassedIn.WriteVerbose(string.Format("Installation destination path is: '{0}'", finalModuleVersionDir));

            if (isModule)
            {
                // If new path does not exist
                if (!Directory.Exists(newPathParent))
                {
                    _cmdletPassedIn.WriteVerbose(string.Format("Attempting to move '{0}' to '{1}'", tempModuleVersionDir, finalModuleVersionDir));
                    Directory.CreateDirectory(newPathParent);
                    Utils.MoveDirectory(tempModuleVersionDir, finalModuleVersionDir);
                }
                else
                {
                    _cmdletPassedIn.WriteVerbose(string.Format("Temporary module version directory is: '{0}'", tempModuleVersionDir));

                    if (Directory.Exists(finalModuleVersionDir))
                    {
                        // Delete the directory path before replacing it with the new module.
                        // If deletion fails (usually due to binary file in use), then attempt restore so that the currently
                        // installed module is not corrupted.
                        _cmdletPassedIn.WriteVerbose(string.Format("Attempting to delete with restore on failure.'{0}'", finalModuleVersionDir));
                        Utils.DeleteDirectoryWithRestore(finalModuleVersionDir);
                    }

                    _cmdletPassedIn.WriteVerbose(string.Format("Attempting to move '{0}' to '{1}'", tempModuleVersionDir, finalModuleVersionDir));
                    Utils.MoveDirectory(tempModuleVersionDir, finalModuleVersionDir);
                }
            }
            else
            {
                if (!_savePkg)
                {
                    // Need to delete old xml files because there can only be 1 per script
                    var scriptXML = pkgInfo.Name + "_InstalledScriptInfo.xml";
                    _cmdletPassedIn.WriteVerbose(string.Format("Checking if path '{0}' exists: ", File.Exists(Path.Combine(installPath, "InstalledScriptInfos", scriptXML))));
                    if (File.Exists(Path.Combine(installPath, "InstalledScriptInfos", scriptXML)))
                    {
                        _cmdletPassedIn.WriteVerbose(string.Format("Deleting script metadata XML"));
                        File.Delete(Path.Combine(installPath, "InstalledScriptInfos", scriptXML));
                    }

                    _cmdletPassedIn.WriteVerbose(string.Format("Moving '{0}' to '{1}'", Path.Combine(dirNameVersion, scriptXML), Path.Combine(installPath, "InstalledScriptInfos", scriptXML)));
                    Utils.MoveFiles(Path.Combine(dirNameVersion, scriptXML), Path.Combine(installPath, "InstalledScriptInfos", scriptXML));

                    // Need to delete old script file, if that exists
                    _cmdletPassedIn.WriteVerbose(string.Format("Checking if path '{0}' exists: ", File.Exists(Path.Combine(finalModuleVersionDir, pkgInfo.Name + PSScriptFileExt))));
                    if (File.Exists(Path.Combine(finalModuleVersionDir, pkgInfo.Name + PSScriptFileExt)))
                    {
                        _cmdletPassedIn.WriteVerbose(string.Format("Deleting script file"));
                        File.Delete(Path.Combine(finalModuleVersionDir, pkgInfo.Name + PSScriptFileExt));
                    }
                }

                _cmdletPassedIn.WriteVerbose(string.Format("Moving '{0}' to '{1}'", scriptPath, Path.Combine(finalModuleVersionDir, pkgInfo.Name + PSScriptFileExt)));
                Utils.MoveFiles(scriptPath, Path.Combine(finalModuleVersionDir, pkgInfo.Name + PSScriptFileExt));
            }
        }

        #endregion
    }
}
