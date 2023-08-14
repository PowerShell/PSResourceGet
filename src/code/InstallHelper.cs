// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    /// <summary>
    /// Install helper class
    /// </summary>
    internal class InstallHelper
    {
        #region Members

        public const string PSDataFileExt = ".psd1";
        public const string PSScriptFileExt = ".ps1";
        private const string MsgRepositoryNotTrusted = "Untrusted repository";
        private const string MsgInstallUntrustedPackage = "You are installing the modules from an untrusted repository. If you trust this repository, change its Trusted value by running the Set-PSResourceRepository cmdlet. Are you sure you want to install the PSResource from '{0}'?";
        private const string ScriptPATHWarning = "The installation path for the script does not currently appear in the {0} path environment variable. To make the script discoverable, add the script installation path, {1}, to the environment PATH variable.";
        private CancellationToken _cancellationToken;
        private readonly PSCmdlet _cmdletPassedIn;
        private List<string> _pathsToInstallPkg;
        private VersionRange _versionRange;
        private NuGetVersion _nugetVersion;
        private VersionType _versionType;
        private string _versionString;
        private bool _prerelease;
        private bool _acceptLicense;
        private bool _quiet;
        private bool _reinstall;
        private bool _force;
        private bool _trustRepository;
        private bool _asNupkg;
        private bool _includeXml;
        private bool _noClobber;
        private bool _authenticodeCheck;
        private bool _savePkg;
        List<string> _pathsToSearch;
        List<string> _pkgNamesToInstall;
        private string _tmpPath;
        private NetworkCredential _networkCredential;
        private HashSet<string> _packagesOnMachine;

        #endregion

        #region Public Methods

        public InstallHelper(PSCmdlet cmdletPassedIn, NetworkCredential networkCredential)
        {
            CancellationTokenSource source = new CancellationTokenSource();
            _cancellationToken = source.Token;
            _cmdletPassedIn = cmdletPassedIn;
            _networkCredential = networkCredential;
        }

        /// <summary>
        /// This method calls is the starting point for install processes and is called by Install, Update and Save cmdlets.
        /// </summary>
        public IEnumerable<PSResourceInfo> BeginInstallPackages(
            string[] names,
            VersionRange versionRange,
            NuGetVersion nugetVersion,
            VersionType versionType,
            string versionString,
            bool prerelease,
            string[] repository,
            bool acceptLicense,
            bool quiet,
            bool reinstall,
            bool force,
            bool trustRepository,
            bool noClobber,
            bool asNupkg,
            bool includeXml,
            bool skipDependencyCheck,
            bool authenticodeCheck,
            bool savePkg,
            List<string> pathsToInstallPkg,
            ScopeType? scope,
            string tmpPath,
            HashSet<string> pkgsInstalled)
        {
            _cmdletPassedIn.WriteVerbose(string.Format("Parameters passed in >>> Name: '{0}'; VersionRange: '{1}'; NuGetVersion: '{2}'; VersionType: '{3}'; Version: '{4}'; Prerelease: '{5}'; Repository: '{6}'; " +
                "AcceptLicense: '{7}'; Quiet: '{8}'; Reinstall: '{9}'; TrustRepository: '{10}'; NoClobber: '{11}'; AsNupkg: '{12}'; IncludeXml '{13}'; SavePackage '{14}'; TemporaryPath '{15}'; SkipDependencyCheck: '{16}'; " + 
                "AuthenticodeCheck: '{17}'; PathsToInstallPkg: '{18}'; Scope '{19}'",
                string.Join(",", names),
                versionRange != null ? (versionRange.OriginalString != null ? versionRange.OriginalString : string.Empty) : string.Empty,
                nugetVersion != null ? nugetVersion.ToString() : string.Empty,
                versionType.ToString(),
                versionString != null ? versionString : String.Empty,
                prerelease.ToString(),
                repository != null ? string.Join(",", repository) : string.Empty,
                acceptLicense.ToString(),
                quiet.ToString(),
                reinstall.ToString(),
                trustRepository.ToString(),
                noClobber.ToString(),
                asNupkg.ToString(),
                includeXml.ToString(),
                savePkg.ToString(),
                tmpPath ?? string.Empty,
                skipDependencyCheck,
                authenticodeCheck,
                pathsToInstallPkg != null ? string.Join(",", pathsToInstallPkg) : string.Empty,
                scope?.ToString() ?? string.Empty));


            _versionRange = versionRange;
            _nugetVersion = nugetVersion;
            _versionType = versionType;
            _versionString = versionString ?? String.Empty;
            _prerelease = prerelease;
            _acceptLicense = acceptLicense || force;
            _authenticodeCheck = authenticodeCheck;
            _quiet = quiet;
            _reinstall = reinstall;
            _force = force;
            _trustRepository = trustRepository || force;
            _noClobber = noClobber;
            _asNupkg = asNupkg;
            _includeXml = includeXml;
            _savePkg = savePkg;
            _pathsToInstallPkg = pathsToInstallPkg;
            _tmpPath = tmpPath ?? Path.GetTempPath();

            if (_versionRange == VersionRange.All)
            {
                _versionType = VersionType.NoVersion;
            }

            // Create list of installation paths to search.
            _pathsToSearch = new List<string>();
            _pkgNamesToInstall = names.ToList();
            _packagesOnMachine = pkgsInstalled;

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
            List<PSResourceInfo> installedPkgs = ProcessRepositories(
                repository: repository,
                trustRepository: _trustRepository,
                skipDependencyCheck: skipDependencyCheck,
                scope: scope ?? ScopeType.CurrentUser);

            return installedPkgs;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// This method calls iterates through repositories (by priority order) to search for the packages to install.
        /// It calls HTTP or NuGet API based install helper methods, according to repository type.
        /// </summary>
        private List<PSResourceInfo> ProcessRepositories(
            string[] repository,
            bool trustRepository,
            bool skipDependencyCheck,
            ScopeType scope)
        {
            List<PSResourceInfo> allPkgsInstalled = new List<PSResourceInfo>();
            if (repository != null && repository.Length != 0)
            {
                // Write error and disregard repository entries containing wildcards.
                repository = Utils.ProcessNameWildcards(repository, removeWildcardEntries:false, out string[] errorMsgs, out _);
                foreach (string error in errorMsgs)
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                                new PSInvalidOperationException(error),
                                "ErrorFilteringNamesForUnsupportedWildcards",
                                ErrorCategory.InvalidArgument,
                                this));
                }

                // If repository entries includes wildcards and non-wildcard names, write terminating error
                // Ex: -Repository *Gallery, localRepo
                bool containsWildcard = false;
                bool containsNonWildcard = false;
                foreach (string repoName in repository)
                {
                    if (repoName.Contains("*"))
                    {
                        containsWildcard = true;
                    }
                    else
                    {
                        containsNonWildcard = true;
                    }
                }

                if (containsNonWildcard && containsWildcard)
                {
                    string message = "Repository name with wildcard is not allowed when another repository without wildcard is specified.";
                    _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                        new PSInvalidOperationException(message),
                        "RepositoryNamesWithWildcardsAndNonWildcardUnsupported",
                        ErrorCategory.InvalidArgument,
                        this));
                }
            }

            // Get repositories to search.
            List<PSRepositoryInfo> repositoriesToSearch;
            try
            {
                repositoriesToSearch = RepositorySettings.Read(repository, out string[] errorList);
                if (repositoriesToSearch != null && repositoriesToSearch.Count == 0)
                {
                    _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                                new PSArgumentException ("Cannot resolve -Repository name. Run 'Get-PSResourceRepository' to view all registered repositories."),
                                "RepositoryNameIsNotResolved",
                                ErrorCategory.InvalidArgument,
                                this));
                }

                foreach (string error in errorList)
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                                new PSInvalidOperationException(error),
                                "ErrorRetrievingSpecifiedRepository",
                                ErrorCategory.InvalidOperation,
                                this));
                }
            }
            catch (Exception e)
            {
                _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                            new PSInvalidOperationException(e.Message),
                            "ErrorLoadingRepositoryStoreFile",
                            ErrorCategory.InvalidArgument,
                            this));

                return allPkgsInstalled;
            }

            var listOfRepositories = RepositorySettings.Read(repository, out string[] _);
            var yesToAll = false;
            var noToAll = false;

            var findHelper = new FindHelper(_cancellationToken, _cmdletPassedIn, _networkCredential);
            bool sourceTrusted = false;

            // Loop through all the repositories provided (in priority order) until there no more packages to install. 
            for (int i=0; i < listOfRepositories.Count && _pkgNamesToInstall.Count > 0; i++)
            {
                PSRepositoryInfo repo = listOfRepositories[i];
                sourceTrusted = repo.Trusted || trustRepository;

                _networkCredential = Utils.SetNetworkCredential(repo, _networkCredential, _cmdletPassedIn);
                ServerApiCall currentServer = ServerFactory.GetServer(repo, _networkCredential);

                if (currentServer == null)
                {
                    // this indicates that PSRepositoryInfo.APIVersion = PSRepositoryInfo.APIVersion.unknown
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                    new PSInvalidOperationException($"Repository '{repo.Name}' is not a known repository type that is supported. Please file an issue for support at https://github.com/PowerShell/PSResourceGet/issues"),
                    "RepositoryApiVersionUnknown",
                    ErrorCategory.InvalidArgument,
                    this));

                    continue;
                }

                ResponseUtil currentResponseUtil = ResponseUtilFactory.GetResponseUtil(repo);
                bool installDepsForRepo = skipDependencyCheck;

                // If no more packages to install, then return
                if (_pkgNamesToInstall.Count == 0) {
                    return allPkgsInstalled;
                }

                string repoName = repo.Name;
                _cmdletPassedIn.WriteVerbose(string.Format("Attempting to search for packages in '{0}'", repoName));

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

                if ((repo.ApiVersion == PSRepositoryInfo.APIVersion.v3) && (!installDepsForRepo))
                {
                    _cmdletPassedIn.WriteWarning("Installing dependencies is not currently supported for V3 server protocol repositories. The package will be installed without installing dependencies.");
                    installDepsForRepo = true;
                }

                var installedPkgs = InstallPackages(_pkgNamesToInstall.ToArray(), repo, currentServer, currentResponseUtil, scope, skipDependencyCheck, findHelper);
                foreach (var pkg in installedPkgs)
                {
                    _pkgNamesToInstall.RemoveAll(x => x.Equals(pkg.Name, StringComparison.InvariantCultureIgnoreCase));
                }

                allPkgsInstalled.AddRange(installedPkgs);
            }

            if (_pkgNamesToInstall.Count > 0) {
                _cmdletPassedIn.WriteError(new ErrorRecord(
                        new ResourceNotFoundException($"Package(s) '{string.Join(", ", _pkgNamesToInstall)}' could not be installed from an."),
                        "InstallPackageFailure",
                        ErrorCategory.InvalidData,
                        this));
            }

            return allPkgsInstalled;
        }

        /// <summary>
        /// Checks if any of the package versions are already installed and if they are removes them from the list of packages to install.
        /// </summary>
        private List<PSResourceInfo> FilterByInstalledPkgs(List<PSResourceInfo> packages)
        {
            // Package install paths.
            // _pathsToInstallPkg will only contain the paths specified within the -Scope param (if applicable).
            // _pathsToSearch will contain all resource package subdirectories within _pathsToInstallPkg path locations.
            // e.g.:
            // ./InstallPackagePath1/PackageA
            // ./InstallPackagePath1/PackageB
            // ./InstallPackagePath2/PackageC
            // ./InstallPackagePath3/PackageD

            // Get currently installed packages.
            var getHelper = new GetHelper(_cmdletPassedIn);
            var installedPackageNames = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (var installedPkg in getHelper.GetInstalledPackages(
                pkgs: packages,
                pathsToSearch: _pathsToSearch))
            {
                installedPackageNames.Add(installedPkg.Name);
            }

            if (installedPackageNames.Count is 0)
            {
                return packages;
            }

            // Return only packages that are not already installed.
            var filteredPackages = new List<PSResourceInfo>();
            foreach (var pkg in packages)
            {
                if (!installedPackageNames.Contains(pkg.Name))
                {
                    // Add packages that still need to be installed.
                    filteredPackages.Add(pkg);
                }
                else
                {
                    // Remove from tracking list of packages to install.
                    pkg.AdditionalMetadata.TryGetValue("NormalizedVersion", out string normalizedVersion);
                    _cmdletPassedIn.WriteWarning(
                        string.Format("Resource '{0}' with version '{1}' is already installed.  If you would like to reinstall, please run the cmdlet again with the -Reinstall parameter",
                        pkg.Name,
                        normalizedVersion));

                    // Remove from tracking list of packages to install.
                    _pkgNamesToInstall.RemoveAll(x => x.Equals(pkg.Name, StringComparison.InvariantCultureIgnoreCase));
                }
            }

            return filteredPackages;
        }

        /// <summary>
        /// Deletes temp directory and is called at end of install process.
        /// </summary>
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

        /// <summary>
        /// Moves file from the temp install path to desination path for install.
        /// </summary>
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
            else if (_asNupkg)
            {
                foreach (string file in Directory.GetFiles(tempInstallPath))
                {
                    string fileName = Path.GetFileName(file);
                    string newFileName = string.Equals(Path.GetExtension(file), ".zip", StringComparison.OrdinalIgnoreCase) ?
                        $"{Path.GetFileNameWithoutExtension(file)}.nupkg" : fileName;

                    Utils.MoveFiles(Path.Combine(tempInstallPath, fileName), Path.Combine(installPath, newFileName));
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


        /// <summary>
        /// Iterates through package names passed in and calls method to install each package and their dependencies.
        /// </summary>
        private List<PSResourceInfo> InstallPackages(
            string[] pkgNamesToInstall,
            PSRepositoryInfo repository,
            ServerApiCall currentServer,
            ResponseUtil currentResponseUtil,
            ScopeType scope,
            bool skipDependencyCheck,
            FindHelper findHelper)
        {
            List<PSResourceInfo> pkgsSuccessfullyInstalled = new List<PSResourceInfo>();

            // Install parent package to the temp directory,
            // Get the dependencies from the installed package,
            // Install all dependencies to temp directory.
            // If a single dependency fails to install, roll back by deleting the temp directory.
            foreach (var parentPackage in pkgNamesToInstall)
            {
                string tempInstallPath = CreateInstallationTempPath();

                try
                {
                    // Hashtable has the key as the package name
                    // and value as a Hashtable of specific package info:
                    //     packageName, { version = "", isScript = "", isModule = "", pkg = "", etc. }
                    // Install parent package to the temp directory.
                    Hashtable packagesHash = InstallPackage(
                                                        searchVersionType: _versionType,
                                                        specificVersion: _nugetVersion,
                                                        versionRange: _versionRange,
                                                        pkgNameToInstall: parentPackage,
                                                        repository: repository,
                                                        currentServer: currentServer,
                                                        currentResponseUtil: currentResponseUtil,
                                                        tempInstallPath: tempInstallPath,
                                                        packagesHash: new Hashtable(StringComparer.InvariantCultureIgnoreCase),
                                                        errRecord: out ErrorRecord errRecord);

                    // At this point parent package is installed to temp path.
                    if (errRecord != null)
                    {
                        // TODO:  Anam working on fix, this may need to be updated
                        if (errRecord.FullyQualifiedErrorId.Equals("PackageNotFound"))
                        {
                            _cmdletPassedIn.WriteVerbose(errRecord.Exception.Message);
                        }
                        else
                        {
                            _cmdletPassedIn.WriteError(errRecord);
                        }

                        continue;
                    }

                    if (packagesHash.Count == 0)
                    {
                        continue;
                    }

                    Hashtable parentPkgInfo = packagesHash[parentPackage] as Hashtable;
                    PSResourceInfo parentPkgObj = parentPkgInfo["psResourceInfoPkg"] as PSResourceInfo;

                    if (!skipDependencyCheck)
                    {
                        if (currentServer.Repository.ApiVersion == PSRepositoryInfo.APIVersion.v3)
                        {
                            _cmdletPassedIn.WriteWarning("Installing dependencies is not currently supported for V3 server protocol repositories. The package will be installed without installing dependencies.");
                        }

                        HashSet<string> myHash = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        // Get the dependencies from the installed package.
                        if (parentPkgObj.Dependencies.Length > 0)
                        {
                            bool depFindFailed = false;
                            foreach (PSResourceInfo depPkg in findHelper.FindDependencyPackages(currentServer, currentResponseUtil, parentPkgObj, repository, myHash))
                            {
                                if (depPkg == null)
                                {
                                    depFindFailed = true;
                                    continue;
                                }
                                
                                if (String.Equals(depPkg.Name, parentPkgObj.Name, StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                NuGetVersion depVersion = null;
                                if (depPkg.AdditionalMetadata.ContainsKey("NormalizedVersion"))
                                {
                                    if (!NuGetVersion.TryParse(depPkg.AdditionalMetadata["NormalizedVersion"] as string, out depVersion))
                                    {
                                        NuGetVersion.TryParse(depPkg.Version.ToString(), out depVersion);
                                    }
                                }

                                packagesHash = InstallPackage(
                                            searchVersionType: VersionType.SpecificVersion,
                                            specificVersion: depVersion,
                                            versionRange: null,
                                            pkgNameToInstall: depPkg.Name,
                                            repository: repository,
                                            currentServer: currentServer,
                                            currentResponseUtil: currentResponseUtil,
                                            tempInstallPath: tempInstallPath,
                                            packagesHash: packagesHash,
                                            errRecord: out ErrorRecord installPkgErrRecord);

                                if (installPkgErrRecord != null)
                                {
                                    _cmdletPassedIn.WriteError(installPkgErrRecord);
                                    continue;
                                }
                            }

                            if (depFindFailed)
                            {
                                continue;
                            }
                        }
                    }

                    // If -WhatIf is passed in, early out.
                    if (!_cmdletPassedIn.ShouldProcess("Exit ShouldProcess"))
                    {
                        return pkgsSuccessfullyInstalled;
                    }

                    // Parent package and dependencies are now installed to temp directory.
                    // Try to move all package directories from temp directory to final destination.
                    if (!TryMoveInstallContent(tempInstallPath, scope, packagesHash))
                    {
                        _cmdletPassedIn.WriteError(new ErrorRecord(new InvalidOperationException(), "InstallPackageTryMoveContentFailure", ErrorCategory.InvalidOperation, this));
                    }
                    else
                    {
                        foreach (string pkgName in packagesHash.Keys)
                        {
                            Hashtable pkgInfo = packagesHash[pkgName] as Hashtable;
                            pkgsSuccessfullyInstalled.Add(pkgInfo["psResourceInfoPkg"] as PSResourceInfo);

                            // Add each pkg to _packagesOnMachine (ie pkgs fully installed on the machine).
                            _packagesOnMachine.Add(String.Format("{0}{1}", pkgName, pkgInfo["pkgVersion"].ToString()));
                        }
                    }
                }
                finally
                {
                    DeleteInstallationTempPath(tempInstallPath);
                }
            }

            return pkgsSuccessfullyInstalled;
        }

        /// <summary>
        /// Installs a single package to the temporary path.
        /// </summary>
        private Hashtable InstallPackage(
            VersionType searchVersionType,
            NuGetVersion specificVersion,
            VersionRange versionRange,
            string pkgNameToInstall,
            PSRepositoryInfo repository,
            ServerApiCall currentServer,
            ResponseUtil currentResponseUtil,
            string tempInstallPath,
            Hashtable packagesHash,
            out ErrorRecord errRecord)
        {
            FindResults responses = null;
            errRecord = null;

            switch (searchVersionType)
            {
                case VersionType.VersionRange:
                    responses = currentServer.FindVersionGlobbing(pkgNameToInstall, versionRange, _prerelease, ResourceType.None, getOnlyLatest: true, out ErrorRecord findVersionGlobbingErrRecord);
                    // Server level globbing API will not populate errRecord for empty response, so must check for empty response and early out
                    if (findVersionGlobbingErrRecord != null || responses.IsFindResultsEmpty())
                    {
                        errRecord = findVersionGlobbingErrRecord;
                        return packagesHash;
                    }

                   break;

                case VersionType.SpecificVersion:
                    string nugetVersionString = specificVersion.ToNormalizedString(); // 3.0.17-beta

                    responses = currentServer.FindVersion(pkgNameToInstall, nugetVersionString, ResourceType.None, out ErrorRecord findVersionErrRecord);
                    if (findVersionErrRecord != null)
                    {
                        errRecord = findVersionErrRecord;
                        return packagesHash;
                    }

                    break;

                default:
                    // VersionType.NoVersion
                    responses = currentServer.FindName(pkgNameToInstall, _prerelease, ResourceType.None, out ErrorRecord findNameErrRecord);
                    if (findNameErrRecord != null)
                    {
                        errRecord = findNameErrRecord;
                        return packagesHash;
                    }

                    break;
            }

            PSResourceInfo pkgToInstall = null;
            foreach (PSResourceResult currentResult in currentResponseUtil.ConvertToPSResourceResult(responses))
            {
                if (currentResult.exception != null && !currentResult.exception.Message.Equals(string.Empty))
                {
                    // V2Server API calls will return non-empty response when package is not found but fail at conversion time
                    errRecord = new ErrorRecord(
                                new InvalidOrEmptyResponse($"Package '{pkgNameToInstall}' could not be installed", currentResult.exception),
                                "InstallPackageFailure",
                                ErrorCategory.InvalidData,
                                this);                   
                }
                else if (searchVersionType == VersionType.VersionRange)
                {
                    // Check to see if version falls within version range 
                    PSResourceInfo foundPkg = currentResult.returnedObject;
                    string versionStr = $"{foundPkg.Version}";
                    if (foundPkg.IsPrerelease)
                    {
                        versionStr += $"-{foundPkg.Prerelease}";
                    }

                    if (NuGetVersion.TryParse(versionStr, out NuGetVersion version)
                           && _versionRange.Satisfies(version))
                    {
                        pkgToInstall = foundPkg;

                        break;
                    }
                } else {
                    pkgToInstall = currentResult.returnedObject;

                    break;
                }
            }

            if (pkgToInstall == null)
            {
                return packagesHash;
            }

            pkgToInstall.RepositorySourceLocation = repository.Uri.ToString();
            pkgToInstall.AdditionalMetadata.TryGetValue("NormalizedVersion", out string pkgVersion);

            // Check to see if the pkg is already installed (ie the pkg is installed and the version satisfies the version range provided via param)
            if (!_reinstall)
            {
                string currPkgNameVersion = String.Format("{0}{1}", pkgToInstall.Name, pkgToInstall.Version.ToString());
                if (_packagesOnMachine.Contains(currPkgNameVersion))
                {
                    _cmdletPassedIn.WriteWarning(
                        string.Format("Resource '{0}' with version '{1}' is already installed.  If you would like to reinstall, please run the cmdlet again with the -Reinstall parameter",
                        pkgToInstall.Name,
                        pkgVersion));
                    return packagesHash;
                }
            }

            if (packagesHash.ContainsKey(pkgToInstall.Name))
            {
                return packagesHash;
            }


            Hashtable updatedPackagesHash = packagesHash;

            // -WhatIf processing.
            if (_savePkg && !_cmdletPassedIn.ShouldProcess($"Package to save: '{pkgToInstall.Name}', version: '{pkgVersion}'"))
            {
                if (!updatedPackagesHash.ContainsKey(pkgToInstall.Name))
                {
                    updatedPackagesHash.Add(pkgToInstall.Name, new Hashtable(StringComparer.InvariantCultureIgnoreCase)
                    {
                        { "isModule", "" },
                        { "isScript", "" },
                        { "psResourceInfoPkg", pkgToInstall },
                        { "tempDirNameVersionPath", tempInstallPath },
                        { "pkgVersion", "" },
                        { "scriptPath", ""  },
                        { "installPath", "" }
                    });
                }
            }
            else if (!_cmdletPassedIn.ShouldProcess($"Package to install: '{pkgToInstall.Name}', version: '{pkgVersion}'"))
            {
                if (!updatedPackagesHash.ContainsKey(pkgToInstall.Name))
                {
                    updatedPackagesHash.Add(pkgToInstall.Name, new Hashtable(StringComparer.InvariantCultureIgnoreCase)
                    {
                        { "isModule", "" },
                        { "isScript", "" },
                        { "psResourceInfoPkg", pkgToInstall },
                        { "tempDirNameVersionPath", tempInstallPath },
                        { "pkgVersion", "" },
                        { "scriptPath", ""  },
                        { "installPath", "" }
                    });
                }
            }
            else
            {
                // Download the package.
                string pkgName = pkgToInstall.Name;
                Stream responseStream;

                if (searchVersionType == VersionType.NoVersion && !_prerelease)
                {
                    responseStream = currentServer.InstallName(pkgName, _prerelease, out ErrorRecord installNameErrRecord);
                    if (installNameErrRecord != null)
                    {
                        errRecord = installNameErrRecord;
                        return packagesHash;
                    }
                }
                else
                {
                    responseStream = currentServer.InstallVersion(pkgName, pkgVersion, out ErrorRecord installVersionErrRecord);
                    if (installVersionErrRecord != null)
                    {
                        errRecord = installVersionErrRecord;
                        return packagesHash;
                    }
                }

                bool installedToTempPathSuccessfully = _asNupkg ? TrySaveNupkgToTempPath(responseStream, tempInstallPath, pkgName, pkgVersion, pkgToInstall, packagesHash, out updatedPackagesHash, out errRecord) :
                    TryInstallToTempPath(responseStream, tempInstallPath, pkgName, pkgVersion, pkgToInstall, packagesHash, out updatedPackagesHash, out errRecord);

                if (!installedToTempPathSuccessfully)
                {
                    return packagesHash;
                }
            }

            return updatedPackagesHash;
        }

        /// <summary>
        /// Creates a temporary path used for installation before moving package to its final location.
        /// </summary>
        private string CreateInstallationTempPath()
        {
            var tempInstallPath = Path.Combine(_tmpPath, Guid.NewGuid().ToString());

            try
            {
                var dir = Directory.CreateDirectory(tempInstallPath);  // should check it gets created properly
                                                                        // To delete file attributes from the existing ones get the current file attributes first and use AND (&) operator
                                                                        // with a mask (bitwise complement of desired attributes combination).
                                                                        // TODO: check the attributes and if it's read only then set it
                                                                        // attribute may be inherited from the parent
                                                                        // TODO:  are there Linux accommodations we need to consider here?
                dir.Attributes &= ~FileAttributes.ReadOnly;
            }
            catch (Exception e)
            {
                // catch more specific exception first
                _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException($"Temporary folder for installation could not be created or set due to: {e.Message}"),
                    "TempFolderCreationError",
                    ErrorCategory.InvalidOperation,
                    this));
            }

            return tempInstallPath;
        }

        /// <summary>
        /// Deletes the temporary path used for intermediary installation.
        /// </summary>
        private void DeleteInstallationTempPath(string tempInstallPath)
        {
            if (Directory.Exists(tempInstallPath))
            {
                // Delete the temp directory and all its contents
                _cmdletPassedIn.WriteVerbose(string.Format("Attempting to delete '{0}'", tempInstallPath));
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

        /// <summary>
        /// Attempts to take installed HTTP response content and move it into a temporary install path on the machine.
        /// </summary>
        private bool TryInstallToTempPath(
            Stream responseStream,
            string tempInstallPath,
            string pkgName,
            string normalizedPkgVersion,
            PSResourceInfo pkgToInstall,
            Hashtable packagesHash,
            out Hashtable updatedPackagesHash,
            out ErrorRecord error)
        {
            error = null;
            updatedPackagesHash = packagesHash;
            try
            {
                var pathToFile = Path.Combine(tempInstallPath, $"{pkgName}.{normalizedPkgVersion}.zip");
                using var fs = File.Create(pathToFile);
                responseStream.Seek(0, System.IO.SeekOrigin.Begin);
                responseStream.CopyTo(fs);
                fs.Close();

                // Expand the zip file
                var pkgVersion = pkgToInstall.Version.ToString();
                var tempDirNameVersion = Path.Combine(tempInstallPath, pkgName.ToLower(), pkgVersion);
                Directory.CreateDirectory(tempDirNameVersion);
                System.IO.Compression.ZipFile.ExtractToDirectory(pathToFile, tempDirNameVersion);

                File.Delete(pathToFile);

                var moduleManifest = Path.Combine(tempDirNameVersion, pkgName + PSDataFileExt);
                var scriptPath = Path.Combine(tempDirNameVersion, pkgName + PSScriptFileExt);

                bool isModule = File.Exists(moduleManifest);
                bool isScript = File.Exists(scriptPath);

                if (!isModule && !isScript) {
                    scriptPath = "";
                }

                // TODO: add pkg validation when we figure out consistent/defined way to do so
                if (_authenticodeCheck && !AuthenticodeSignature.CheckAuthenticodeSignature(
                    pkgName,
                    tempDirNameVersion,
                    _cmdletPassedIn,
                    out error))
                {
                    return false;
                }

                string installPath = string.Empty;
                if (isModule)
                {
                    installPath = _pathsToInstallPkg.Find(path => path.EndsWith("Modules", StringComparison.InvariantCultureIgnoreCase));

                    if (!File.Exists(moduleManifest))
                    {
                        var message = String.Format("{0} package could not be installed with error: Module manifest file: {1} does not exist. This is not a valid PowerShell module.", pkgName, moduleManifest);
                        var ex = new ArgumentException(message);
                        error = new ErrorRecord(ex, "psdataFileNotExistError", ErrorCategory.ReadError, null);

                        return false;
                    }

                    if (!Utils.TryReadManifestFile(
                        manifestFilePath: moduleManifest,
                        manifestInfo: out Hashtable parsedMetadataHashtable,
                        error: out Exception manifestReadError))
                    {
                        error = new ErrorRecord(
                            exception: manifestReadError,
                            errorId: "ManifestFileReadParseError",
                            errorCategory: ErrorCategory.ReadError,
                            this);

                        return false;
                    }

                    // Accept License verification
                    if (!_savePkg && !CallAcceptLicense(pkgToInstall, moduleManifest, tempInstallPath, pkgVersion, out error))
                    {
                        _pkgNamesToInstall.RemoveAll(x => x.Equals(pkgToInstall.Name, StringComparison.InvariantCultureIgnoreCase));
                        return false;
                    }

                    // If NoClobber is specified, ensure command clobbering does not happen
                    if (_noClobber && DetectClobber(pkgName, parsedMetadataHashtable, out error))
                    {
                        _pkgNamesToInstall.RemoveAll(x => x.Equals(pkgName, StringComparison.InvariantCultureIgnoreCase));
                        return false;
                    }
                }
                else if (isScript)
                {
                    installPath = _pathsToInstallPkg.Find(path => path.EndsWith("Scripts", StringComparison.InvariantCultureIgnoreCase));

                    // is script
                    if (!PSScriptFileInfo.TryTestPSScriptFileInfo(
                        scriptFileInfoPath: scriptPath,
                        parsedScript: out PSScriptFileInfo scriptToInstall,
                        out ErrorRecord[] parseScriptFileErrors,
                        out string[] _))
                    {
                        foreach (ErrorRecord parseError in parseScriptFileErrors)
                        {
                            _cmdletPassedIn.WriteError(parseError);
                        }

                        var ex = new InvalidOperationException($"PSScriptFile could not be parsed");
                        error = new ErrorRecord(ex, "psScriptParseError", ErrorCategory.ReadError, null);

                        return false;
                    }
                }
                else
                {
                    // This package is not a PowerShell package (eg a resource from the NuGet Gallery).
                    installPath = _pathsToInstallPkg.Find(path => path.EndsWith("Modules", StringComparison.InvariantCultureIgnoreCase));

                    _cmdletPassedIn.WriteVerbose($"This resource is not a PowerShell package and will be installed to the modules path: {installPath}.");
                    isModule = true;
                }

                installPath = _savePkg ? _pathsToInstallPkg.First() : installPath;

                DeleteExtraneousFiles(pkgName, tempDirNameVersion);

                if (_includeXml)
                {
                    if (!CreateMetadataXMLFile(tempDirNameVersion, installPath, pkgToInstall, isModule, out error))
                    {
                        return false;
                    }
                }

                if (!updatedPackagesHash.ContainsKey(pkgName))
                {
                    // Add pkg info to hashtable.
                    updatedPackagesHash.Add(pkgName, new Hashtable(StringComparer.InvariantCultureIgnoreCase)
                    {
                        { "isModule", isModule },
                        { "isScript", isScript },
                        { "psResourceInfoPkg", pkgToInstall },
                        { "tempDirNameVersionPath", tempDirNameVersion },
                        { "pkgVersion", pkgVersion },
                        { "scriptPath", scriptPath  },
                        { "installPath", installPath }
                    });
                }

                return true;
            }
            catch (Exception e)
            {
                error = new ErrorRecord(
                    new PSInvalidOperationException(
                        message: $"Unable to successfully install package '{pkgName}': '{e.Message}' to temporary installation path.",
                        innerException: e),
                    "InstallPackageFailed",
                    ErrorCategory.InvalidOperation,
                    _cmdletPassedIn);

                return false;
            }
        }

        /// <summary>
        /// Attempts to take Http response content and move the .nupkg into a temporary install path on the machine.
        /// </summary>
        private bool TrySaveNupkgToTempPath(
            Stream responseStream,
            string tempInstallPath,
            string pkgName,
            string normalizedPkgVersion,
            PSResourceInfo pkgToInstall,
            Hashtable packagesHash,
            out Hashtable updatedPackagesHash,
            out ErrorRecord error)
        {
            error = null;
            updatedPackagesHash = packagesHash;

            try
            {
                var pathToFile = Path.Combine(tempInstallPath, $"{pkgName}.{normalizedPkgVersion}.zip");
                using var fs = File.Create(pathToFile);
                responseStream.Seek(0, System.IO.SeekOrigin.Begin);
                responseStream.CopyTo(fs);
                fs.Close();

                string installPath = _pathsToInstallPkg.First();
                if (_includeXml)
                {
                    if (!CreateMetadataXMLFile(tempInstallPath, installPath, pkgToInstall, isModule: true, out error))
                    {
                        return false;
                    }
                }

                if (!updatedPackagesHash.ContainsKey(pkgName))
                {
                    // Add pkg info to hashtable.
                    updatedPackagesHash.Add(pkgName, new Hashtable(StringComparer.InvariantCultureIgnoreCase)
                    {
                        { "isModule", "" },
                        { "isScript", "" },
                        { "psResourceInfoPkg", pkgToInstall },
                        { "tempDirNameVersionPath", tempInstallPath },
                        { "pkgVersion", "" },
                        { "scriptPath", ""  },
                        { "installPath", installPath }
                    });
                }

                return true;
            }
            catch (Exception e)
            {
                error = new ErrorRecord(
                            new PSInvalidOperationException(
                                message: $"Unable to successfully save .nupkg '{pkgName}': '{e.Message}' to temporary installation path.",
                                innerException: e),
                        "SaveNupkgFailed",
                        ErrorCategory.InvalidOperation,
                        _cmdletPassedIn);

                return false;
            }
        }

        /// <summary>
        /// Moves package files/directories from the temp install path into the final install path location.
        /// </summary>
        private bool TryMoveInstallContent(string tempInstallPath, ScopeType scope, Hashtable packagesHash)
        {
            foreach (string pkgName in packagesHash.Keys)
            {
                Hashtable pkgInfo = packagesHash[pkgName] as Hashtable;
                bool isModule = (pkgInfo["isModule"] as bool?) ?? false;
                bool isScript = (pkgInfo["isScript"] as bool?) ?? false;
                PSResourceInfo pkgToInstall = pkgInfo["psResourceInfoPkg"] as PSResourceInfo;
                string tempDirNameVersion = pkgInfo["tempDirNameVersionPath"] as string;
                string pkgVersion = pkgInfo["pkgVersion"] as string;
                string scriptPath = pkgInfo["scriptPath"] as string;
                string installPath = pkgInfo["installPath"] as string;

                try
                {
                    MoveFilesIntoInstallPath(
                        pkgToInstall,
                        isModule,
                        isLocalRepo: false, // false for HTTP repo
                        tempDirNameVersion,
                        tempInstallPath,
                        installPath,
                        newVersion: pkgVersion, // would not have prerelease label in this string
                        moduleManifestVersion: pkgVersion,
                        scriptPath);

                    _cmdletPassedIn.WriteVerbose(String.Format("Successfully installed package '{0}' to location '{1}'", pkgName, installPath));

                    if (!_savePkg && isScript)
                    {
                        string installPathwithBackSlash = installPath + "\\";
                        string envPATHVarValue = String.Empty;
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            envPATHVarValue = Environment.GetEnvironmentVariable("PATH",
                            scope == ScopeType.CurrentUser ? EnvironmentVariableTarget.User : EnvironmentVariableTarget.Machine);
                        }
                        else
                        {
                            // .NET on Unix-based systems does not support per-user and per-machine environment variables, only EnvironmentVariableTarget.Process successfully store an environment variable to the process environment block.
                            envPATHVarValue = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
                        }

                        if (!String.IsNullOrEmpty(envPATHVarValue) && !envPATHVarValue.Contains(installPath) && !envPATHVarValue.Contains(installPathwithBackSlash))
                        {
                            _cmdletPassedIn.WriteWarning(String.Format(ScriptPATHWarning, scope, installPath));
                        }
                    }
                }
                catch (Exception e)
                {
                    _cmdletPassedIn.WriteError(
                        new ErrorRecord(
                            new PSInvalidOperationException(
                                message: $"Unable to successfully install package '{pkgName}': '{e.Message}'",
                                innerException: e),
                            "InstallPackageFailed",
                            ErrorCategory.InvalidOperation,
                            _cmdletPassedIn));

                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// If the package requires license to be accepted, checks if the user has accepted it.
        /// </summary>
        private bool CallAcceptLicense(PSResourceInfo p, string moduleManifest, string tempInstallPath, string newVersion, out ErrorRecord error)
        {
            error = null;
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
                            error = acceptLicenseError;
                            success = false;
                            return success;
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
                        error = acceptLicenseError;
                        success = false;
                    }
                }
            }

            return success;
        }

        /// <summary>
        /// If the option for no clobber is specified, ensures that commands or cmdlets are not being clobbered.
        /// </summary>
        private bool DetectClobber(string pkgName, Hashtable parsedMetadataHashtable, out ErrorRecord error)
        {
            error = null;
            bool foundClobber = false;

            // Get installed modules, then get all possible paths
            // selectPrereleaseOnly is false because even if Prerelease is true we want to include both stable and prerelease, would never select prerelease only.
            GetHelper getHelper = new GetHelper(_cmdletPassedIn);
            IEnumerable<PSResourceInfo> pkgsAlreadyInstalled = getHelper.GetPackagesFromPath(
                name: new string[] { "*" },
                versionRange: VersionRange.All,
                pathsToSearch: _pathsToSearch,
                selectPrereleaseOnly: false);

            List<string> listOfCmdlets = new List<string>();
            if (parsedMetadataHashtable.ContainsKey("CmdletsToExport"))
            {
                if (parsedMetadataHashtable["CmdletsToExport"] is object[] cmdletsToExport)
                {
                    foreach (var cmdletName in cmdletsToExport)
                    {
                        listOfCmdlets.Add(cmdletName as string);
                    }
                }
            }

            foreach (var pkg in pkgsAlreadyInstalled)
            {
                List<string> duplicateCmdlets = new List<string>();
                List<string> duplicateCmds = new List<string>();
                // See if any of the cmdlets or commands in the pkg we're trying to install exist within a package that's already installed
                if (pkg.Includes.Cmdlet != null && pkg.Includes.Cmdlet.Length != 0)
                {
                    duplicateCmdlets = listOfCmdlets.Where(cmdlet => pkg.Includes.Cmdlet.Contains(cmdlet)).ToList();
                }

                if (pkg.Includes.Command != null && pkg.Includes.Command.Any())
                {
                    duplicateCmds = listOfCmdlets.Where(commands => pkg.Includes.Command.Contains(commands, StringComparer.InvariantCultureIgnoreCase)).ToList();
                }

                if (duplicateCmdlets.Count != 0 || duplicateCmds.Count != 0)
                {
                    duplicateCmdlets.AddRange(duplicateCmds);

                    var errMessage = string.Format(
                        "{1} package could not be installed with error: The following commands are already available on this system: '{0}'. This module '{1}' may override the existing commands. If you still want to install this module '{1}', remove the -NoClobber parameter.",
                        String.Join(", ", duplicateCmdlets), pkgName);

                    var ex = new ArgumentException(errMessage);
                    var noClobberError = new ErrorRecord(ex, "CommandAlreadyExists", ErrorCategory.ResourceExists, null);
                    error = noClobberError;
                    foundClobber = true;

                    return foundClobber;
                }
            }

            return foundClobber;
        }

        /// <summary>
        /// Creates metadata XML file for either module or script package.
        /// </summary>
        private bool CreateMetadataXMLFile(string dirNameVersion, string installPath, PSResourceInfo pkg, bool isModule, out ErrorRecord error)
        {
            error = null;
            bool success = true;
            // Script will have a metadata file similar to:  "TestScript_InstalledScriptInfo.xml"
            // Modules will have the metadata file: "PSGetModuleInfo.xml"
            var metadataXMLPath = isModule ? Path.Combine(dirNameVersion, "PSGetModuleInfo.xml")
                : Path.Combine(dirNameVersion, (pkg.Name + "_InstalledScriptInfo.xml"));

            pkg.InstalledDate = DateTime.Now;
            pkg.InstalledLocation = installPath;

            // Write all metadata into metadataXMLPath
            if (!pkg.TryWrite(metadataXMLPath, out string writeError))
            {
                var message = string.Format("{0} package could not be installed with error: Error parsing metadata into XML: '{1}'", pkg.Name, writeError);
                var ex = new ArgumentException(message);
                var errorParsingMetadata = new ErrorRecord(ex, "ErrorParsingMetadata", ErrorCategory.ParserError, null);
                error = errorParsingMetadata;
                success = false;
            }

            return success;
        }

        /// <summary>
        /// Clean up and delete extraneous files found from the package during install.
        /// </summary>
        private void DeleteExtraneousFiles(string packageName, string dirNameVersion)
        {
            // Deleting .nupkg SHA file, .nuspec, and .nupkg after unpacking the module
            // since we download as .zip for HTTP calls, we shouldn't have .nupkg* files
            // var nupkgSHAToDelete = Path.Combine(dirNameVersion, pkgIdString + ".nupkg.sha512");
            // var nupkgToDelete = Path.Combine(dirNameVersion, pkgIdString + ".nupkg");
            // var nupkgMetadataToDelete =  Path.Combine(dirNameVersion, ".nupkg.metadata");
            var nuspecToDelete = Path.Combine(dirNameVersion, packageName + ".nuspec");
            var contentTypesToDelete = Path.Combine(dirNameVersion, "[Content_Types].xml");
            var relsDirToDelete = Path.Combine(dirNameVersion, "_rels");
            var packageDirToDelete = Path.Combine(dirNameVersion, "package");

            if (File.Exists(nuspecToDelete))
            {
                _cmdletPassedIn.WriteVerbose(string.Format("Deleting '{0}'", nuspecToDelete));
                File.Delete(nuspecToDelete);
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

        #endregion
    }
}
