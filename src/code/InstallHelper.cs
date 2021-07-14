// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
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
using MoreLinq.Extensions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.PackageExtraction;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// Install helper class
    /// </summary>
    internal class InstallHelper : PSCmdlet
    {
        private CancellationToken _cancellationToken;
        private readonly bool _updatePkg;
        private readonly bool _savePkg;
        private readonly PSCmdlet _cmdletPassedIn;
        List<string> _pathsToInstallPkg;
        VersionRange _versionRange;
        bool _prerelease;
        bool _acceptLicense;
        bool _quiet;
        bool _reinstall;
        bool _force;
        bool _trustRepository;
        bool _noClobber;
        PSCredential _credential;
        string _specifiedPath;
        bool _asNupkg;
        bool _includeXML;

        public InstallHelper(bool updatePkg, bool savePkg, CancellationToken cancellationToken, PSCmdlet cmdletPassedIn)
        {
            this._updatePkg = updatePkg;
            this._savePkg = savePkg;
            this._cancellationToken = cancellationToken;
            this._cmdletPassedIn = cmdletPassedIn;
        }

        public void InstallPackages(
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
            string requiredResourceFile,
            string requiredResourceJson,
            Hashtable requiredResourceHash,
            string specifiedPath,
            bool asNupkg,
            bool includeXML,
            List<string> pathsToInstallPkg)
        {
            _cmdletPassedIn.WriteDebug(string.Format("Parameters passed in >>> Name: '{0}'; Version: '{1}'; Prerelease: '{2}'; Repository: '{3}'; " +
                "AcceptLicense: '{4}'; Quiet: '{5}'; Reinstall: '{6}'; TrustRepository: '{7}'; NoClobber: '{8}';",
                string.Join(",", names),
                (_versionRange != null ? _versionRange.OriginalString : string.Empty),
                prerelease.ToString(),
                repository != null ? string.Join(",", repository) : string.Empty,
                acceptLicense.ToString(),
                quiet.ToString(),
                reinstall.ToString(),
                trustRepository.ToString(),
                noClobber.ToString()));

            _versionRange = versionRange;
            _prerelease = prerelease;
            _acceptLicense = acceptLicense;
            _quiet = quiet;
            _reinstall = reinstall;
            _force = force;
            _trustRepository = trustRepository;
            _noClobber = noClobber;
            _credential = credential;
            _specifiedPath = specifiedPath;
            _asNupkg = asNupkg;
            _includeXML = includeXML;
            _pathsToInstallPkg = pathsToInstallPkg;

            // Go through the repositories and see which is the first repository to have the pkg version available
            ProcessRepositories(names, repository, _trustRepository, _credential);
        }

        // This method calls iterates through repositories (by priority order) to search for the pkgs to install
        public void ProcessRepositories(string[] packageNames, string[] repository, bool trustRepository, PSCredential credential)
        {
            var listOfRepositories = RepositorySettings.Read(repository, out string[] _);
            List<string> packagesToInstall = packageNames.ToList();
            var yesToAll = false;
            var noToAll = false;
            var repositoryIsNotTrusted = "Untrusted repository";
            var queryInstallUntrustedPackage = "You are installing the modules from an untrusted repository. If you trust this repository, change its Trusted value by running the Set-PSResourceRepository cmdlet. Are you sure you want to install the PSresource from '{0}' ?";

            foreach (var repo in listOfRepositories)
            {
                // If no more packages to install, then return
                if (!packagesToInstall.Any()) return;

                var sourceTrusted = false;
                string repoName = repo.Name;
                _cmdletPassedIn.WriteDebug(string.Format("Attempting to search for packages in '{0}'", repoName));

                // Source is only trusted if it's set at the repository level to be trusted, -TrustRepository flag is true, -Force flag is true
                // OR the user issues trust interactively via console.
                if (repo.Trusted == false && !trustRepository && !_force)
                {
                    _cmdletPassedIn.WriteDebug("Checking if untrusted repository should be used");

                    if (!(yesToAll || noToAll))
                    {
                        // Prompt for installation of package from untrusted repository
                        var message = string.Format(CultureInfo.InvariantCulture, queryInstallUntrustedPackage, repoName);
                        sourceTrusted = _cmdletPassedIn.ShouldContinue(message, repositoryIsNotTrusted, true, ref yesToAll, ref noToAll);
                    }
                }
                else
                {
                    sourceTrusted = true;
                }

                if (sourceTrusted || yesToAll)
                {
                    _cmdletPassedIn.WriteDebug("Untrusted repository accepted as trusted source.");

                    // If it can't find the pkg in one repository, it'll look for it in the next repo in the list
                    var isLocalRepo = repo.Url.AbsoluteUri.StartsWith(Uri.UriSchemeFile + Uri.SchemeDelimiter, StringComparison.OrdinalIgnoreCase);


                    var findHelper = new FindHelper(_cancellationToken, _cmdletPassedIn);
                    // Finds parent packages and dependencies
                    IEnumerable<PSResourceInfo> pkgsFromRepoToInstall = findHelper.FindByResourceName(
                        name: packageNames,
                        type: ResourceType.None,
                        version: _versionRange != null ? _versionRange.OriginalString : null,
                        prerelease: _prerelease,
                        tag: null,
                        repository: new string[] { repoName },
                        credential: credential,
                        includeDependencies: true);

                    // Select the first package from each name group, which is guaranteed to be the latest version.
                    // We should only have one version returned for each package name
                    // e.g.:
                    // PackageA (version 1.0)
                    // PackageB (version 2.0)
                    // PackageC (version 1.0)
                    pkgsFromRepoToInstall = pkgsFromRepoToInstall.GroupBy(
                        m => new { m.Name }).Select(
                            group => group.First()).ToList();

                    if (!pkgsFromRepoToInstall.Any())
                    {
                        _cmdletPassedIn.WriteVerbose(string.Format("None of the specified resources were found in the '{0}' repository.", repoName));
                        // Check in the next repository
                        continue;
                    }

                    // Check to see if the pkgs (including dependencies) are already installed (ie the pkg is installed and the version satisfies the version range provided via param)
                    if (!_reinstall)
                    {
                        // Removes all of the names that are already installed from the list of names to search for
                        pkgsFromRepoToInstall = FilterByInstalledPkgs(pkgsFromRepoToInstall);
                    }

                    if (!pkgsFromRepoToInstall.Any())
                    {
                        continue;
                    }

                    List<string> pkgsInstalled = InstallPackage(pkgsFromRepoToInstall, repoName, repo.Url.AbsoluteUri, credential, isLocalRepo);

                    foreach (string name in pkgsInstalled)
                    {
                        packagesToInstall.Remove(name);
                    }
                }
            }
        }

        // Check if any of the pkg versions are already installed, if they are we'll remove them from the list of packages to install
        public IEnumerable<PSResourceInfo> FilterByInstalledPkgs(IEnumerable<PSResourceInfo> packagesToInstall)
        {
            List<string> pkgNames = new List<string>();
            foreach (var pkg in packagesToInstall)
            {
                pkgNames.Add(pkg.Name);
            }

            List<string> _pathsToSearch = new List<string>();
            GetHelper getHelper = new GetHelper(_cmdletPassedIn);
            // _pathsToInstallPkg will only contain the paths specified within the -Scope param (if applicable)
            // _pathsToSearch will contain all resource package subdirectories within _pathsToInstallPkg path locations
            // e.g.:
            // ./InstallPackagePath1/PackageA
            // ./InstallPackagePath1/PackageB
            // ./InstallPackagePath2/PackageC
            // ./InstallPackagePath3/PackageD
            foreach (var path in _pathsToInstallPkg)
            {
                _pathsToSearch.AddRange(Directory.GetDirectories(path));
            }

            IEnumerable<PSResourceInfo> pkgsAlreadyInstalled = getHelper.FilterPkgPaths(pkgNames.ToArray(), _versionRange, _pathsToSearch);

            // If any pkg versions are already installed, write a message saying it is already installed and continue processing other pkg names
            if (pkgsAlreadyInstalled.Any())
            {
                foreach (PSResourceInfo pkg in pkgsAlreadyInstalled)
                {
                    _cmdletPassedIn.WriteWarning(string.Format("Resource '{0}' with version '{1}' is already installed.  If you would like to reinstall, please run the cmdlet again with the -Reinstall parameter", pkg.Name, pkg.Version));

                    // remove this pkg from the list of pkg names install
                    packagesToInstall.ToList().Remove(pkg);
                }
            }

            return packagesToInstall;
        }

        private List<string> InstallPackage(IEnumerable<PSResourceInfo> pkgsToInstall, string repoName, string repoUrl, PSCredential credential, bool isLocalRepo)
        {
            List<string> pkgsSuccessfullyInstalled = new List<string>();
            foreach (PSResourceInfo p in pkgsToInstall)
            {
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
                    dir.Attributes = dir.Attributes & ~FileAttributes.ReadOnly;

                    _cmdletPassedIn.WriteVerbose(string.Format("Begin installing package: '{0}'", p.Name));

                    if (!_quiet && !_savePkg)
                    {
                        // todo: UPDATE PROGRESS BAR
                        CallProgressBar(p);
                    }

                    // Create PackageIdentity in order to download
                    string createFullVersion = p.Version.ToString();
                    if (p.IsPrerelease)
                    {
                        createFullVersion = p.Version.ToString() + "-" + p.PrereleaseLabel;
                    }

                    if (!NuGetVersion.TryParse(createFullVersion, out NuGetVersion pkgVersion))
                    {
                        _cmdletPassedIn.WriteDebug(string.Format("Error parsing package '{0}' version '{1}' into a NuGetVersion", p.Name, p.Version.ToString()));
                        continue;
                    }
                    var pkgIdentity = new PackageIdentity(p.Name, pkgVersion);
                    var cacheContext = new SourceCacheContext();

                    if (isLocalRepo)
                    {
                        /* Download from a local repository -- this is slightly different process than from a server */
                        var localResource = new FindLocalPackagesResourceV2(repoUrl);
                        var resource = new LocalDownloadResource(repoUrl, localResource);

                        // Actually downloading the .nupkg from a local repo
                        var result = resource.GetDownloadResourceResultAsync(
                             identity: pkgIdentity,
                             downloadContext: new PackageDownloadContext(cacheContext),
                             globalPackagesFolder: tempInstallPath,
                             logger: NullLogger.Instance,
                             token: _cancellationToken).GetAwaiter().GetResult();

                        if (_asNupkg) // this is Save functionality
                        {
                            DirectoryInfo nupkgPath = new DirectoryInfo(((System.IO.FileStream)result.PackageStream).Name);
                            File.Copy(nupkgPath.FullName, Path.Combine(tempInstallPath, pkgIdentity.Id + pkgIdentity.Version + ".nupkg"));

                            continue;
                        }

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
                            extractFile: (new PackageFileExtractor(result.PackageReader.GetFiles(), packageExtractionContext.XmlDocFileSaveMode)).ExtractPackageFile,
                            logger: NullLogger.Instance,
                            token: _cancellationToken);
                        result.Dispose();
                    }
                    else
                    {
                        /* Download from a non-local repository */
                        // Set up NuGet API resource for download
                        PackageSource source = new PackageSource(repoUrl);
                        if (credential != null)
                        {
                            string password = new NetworkCredential(string.Empty, credential.Password).Password;
                            source.Credentials = PackageSourceCredential.FromUserInput(repoUrl, credential.UserName, password, true, null);
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
                            _cmdletPassedIn.WriteDebug(string.Format("Error attempting download: '{0}'", e.Message));
                        }
                        finally
                        {
                            // Need to close the .nupkg
                            if (result != null) result.Dispose();
                        }
                    }

                    _cmdletPassedIn.WriteDebug(string.Format("Successfully able to download package from source to: '{0}'", tempInstallPath));

                    // Prompt if module requires license acceptance (need to read info license acceptance info from the module manifest)
                    // pkgIdentity.Version.Version gets the version without metadata or release labels.

                    string newVersion = pkgIdentity.Version.ToNormalizedString();
                    string version3digitNoPrerelease = newVersion;
                    if (pkgIdentity.Version.IsPrerelease)
                    {
                        version3digitNoPrerelease = pkgIdentity.Version.ToNormalizedString().Substring(0, pkgIdentity.Version.ToNormalizedString().IndexOf('-'));
                    }

                    string tempDirNameVersion = isLocalRepo ? tempInstallPath : Path.Combine(tempInstallPath, pkgIdentity.Id.ToLower(), newVersion);
                    var version4digitNoPrerelease = pkgIdentity.Version.Version.ToString();
                    string moduleManifestVersion = string.Empty;
                    var scriptPath = Path.Combine(tempDirNameVersion, (p.Name + ".ps1"));
                    var isScript = File.Exists(scriptPath) ? true : false;

                    if (!isScript)
                    {
                        var moduleManifest = Path.Combine(tempDirNameVersion, pkgIdentity.Id + ".psd1");

                        var parsedMetadataHashtable = Utils.ParseModuleManifest(moduleManifest, this);
                        moduleManifestVersion = parsedMetadataHashtable["ModuleVersion"] as string;

                        // Accept License verification
                        if (!_savePkg && !CallAcceptLicense(p, moduleManifest, tempInstallPath, newVersion))
                        {
                            continue;
                        }
                    }

                    // Delete the extra nupkg related files that are not needed and not part of the module/script
                    DeleteExtraneousFiles(tempInstallPath, pkgIdentity, tempDirNameVersion);

                    string installPath;
                    if (_savePkg)
                    {
                        // For save the installation path is what is passed in via -Path
                        installPath = _pathsToInstallPkg.FirstOrDefault();
                    }
                    else {
                        // PSModules:
                        /// ./Modules
                        /// ./Scripts
                        /// _pathsToInstallPkg is sorted by desirability, Find will pick the pick the first Script or Modules path found in the list
                        installPath = isScript ? _pathsToInstallPkg.Find(path => path.EndsWith("Scripts", StringComparison.InvariantCultureIgnoreCase))
                                : _pathsToInstallPkg.Find(path => path.EndsWith("Modules", StringComparison.InvariantCultureIgnoreCase));
                    }

                    if (_includeXML)
                    {
                        CreateMetadataXMLFile(tempDirNameVersion, installPath, repoName, p, isScript);
                    }

                    MoveFilesIntoInstallPath(p, isScript, isLocalRepo, tempDirNameVersion, tempInstallPath, installPath, newVersion, moduleManifestVersion, version3digitNoPrerelease, version4digitNoPrerelease, scriptPath);

                    _cmdletPassedIn.WriteVerbose(String.Format("Successfully installed package '{0}'", p.Name));
                    pkgsSuccessfullyInstalled.Add(p.Name);
                }
                catch (Exception e)
                {
                    _cmdletPassedIn.WriteDebug(string.Format("Unable to successfully install package '{0}': '{1}'", p.Name, e.Message));
                }
                finally
                {
                    // Delete the temp directory and all its contents
                    _cmdletPassedIn.WriteDebug(string.Format("Attempting to delete '{0}'", tempInstallPath));
                    if (Directory.Exists(tempInstallPath))
                    {
                        Directory.Delete(tempInstallPath, true);
                    }
                }
            }

            return pkgsSuccessfullyInstalled;
        }

        private void CallProgressBar(PSResourceInfo p)
        {
            int i = 1;
            //int j = 1;
            /****************************
            * START PACKAGE INSTALLATION -- start progress bar
            *****************************/
            // Write-Progress -Activity "Search in Progress" - Status "$i% Complete:" - PercentComplete $i

            int activityId = 0;
            string activity = "";
            string statusDescription = "";

            // If the pkg exists in one of the names passed in, then we wont include it as a dependent package
            activityId = 0;
            activity = string.Format("Installing {0}...", p.Name);
            statusDescription = string.Format("{0}% Complete:", i++);

            // j = 1;
            /*
            if (packageNames.ToList().Contains(p.Identity.Id))
            {
                // If the pkg exists in one of the names passed in, then we wont include it as a dependent package
                activityId = 0;
                activity = string.Format("Installing {0}...", p.Identity.Id);
                statusDescription = string.Format("{0}% Complete:", i++);

                j = 1;
            }
            else
            {
                // Child process
                // Installing dependent package
                activityId = 1;
                activity = string.Format("Installing dependent package {0}...", p.Identity.Id);
                statusDescription = string.Format("{0}% Complete:", j);
            }
            */
            var progressRecord = new ProgressRecord(activityId, activity, statusDescription);
            _cmdletPassedIn.WriteProgress(progressRecord);
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
                            var exMessage = "License.txt not Found. License.txt must be provided when user license acceptance is required.";
                            var ex = new ArgumentException(exMessage);
                            var acceptLicenseError = new ErrorRecord(ex, "LicenseTxtNotFound", ErrorCategory.ObjectNotFound, null);

                            _cmdletPassedIn.WriteError(acceptLicenseError);
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
                        var message = $"License Acceptance is required for module '{p.Name}'. Please specify '-AcceptLicense' to perform this operation.";
                        var ex = new ArgumentException(message);
                        var acceptLicenseError = new ErrorRecord(ex, "ForceAcceptLicense", ErrorCategory.InvalidArgument, null);

                        _cmdletPassedIn.WriteError(acceptLicenseError);
                        success = false;
                    }
                }
            }

            return success;
        }

        private void CreateMetadataXMLFile(string dirNameVersion, string installPath, string repoName, PSResourceInfo pkg, bool isScript)
        {
            // Script will have a metadata file similar to:  "TestScript_InstalledScriptInfo.xml"
            // Modules will have the metadata file: "PSGetModuleInfo.xml"
            var metadataXMLPath = isScript ? Path.Combine(dirNameVersion, (pkg.Name + "_InstalledScriptInfo.xml"))
                : Path.Combine(dirNameVersion, "PSGetModuleInfo.xml");

            pkg.InstalledDate = DateTime.Now;
            pkg.InstalledLocation = installPath;

            // Write all metadata into metadataXMLPath
            if (!pkg.TryWrite(metadataXMLPath, out string error))
            {
                var message = string.Format("Error parsing metadata into XML: '{0}'", error);
                var ex = new ArgumentException(message);
                var ErrorParsingMetadata = new ErrorRecord(ex, "ErrorParsingMetadata", ErrorCategory.ParserError, null);
                _cmdletPassedIn.WriteError(ErrorParsingMetadata);
            }
        }

        private void DeleteExtraneousFiles(string tempInstallPath, PackageIdentity pkgIdentity, string dirNameVersion)
        {
            // Deleting .nupkg SHA file, .nuspec, and .nupkg after unpacking the module
            var pkgIdString = pkgIdentity.ToString();
            var nupkgSHAToDelete = Path.Combine(dirNameVersion, (pkgIdString + ".nupkg.sha512").ToLower());
            var nuspecToDelete = Path.Combine(dirNameVersion, (pkgIdentity.Id + ".nuspec").ToLower());
            var nupkgToDelete = Path.Combine(dirNameVersion, (pkgIdString + ".nupkg").ToLower());
            var contentTypesToDelete = Path.Combine(dirNameVersion, "[Content_Types].xml");
            var relsDirToDelete = Path.Combine(dirNameVersion, "_rels");
            var packageDirToDelete = Path.Combine(dirNameVersion, "package");

            // Unforunately have to check if each file exists because it may or may not be there
            if (File.Exists(nupkgSHAToDelete))
            {
                _cmdletPassedIn.WriteDebug(string.Format("Deleting '{0}'", nupkgSHAToDelete));
                File.Delete(nupkgSHAToDelete);
            }
            if (File.Exists(nuspecToDelete))
            {
                _cmdletPassedIn.WriteDebug(string.Format("Deleting '{0}'", nuspecToDelete));
                File.Delete(nuspecToDelete);
            }
            if (File.Exists(nupkgToDelete))
            {
                _cmdletPassedIn.WriteDebug(string.Format("Deleting '{0}'", nupkgToDelete));
                File.Delete(nupkgToDelete);
            }
            if (File.Exists(contentTypesToDelete))
            {
                _cmdletPassedIn.WriteDebug(string.Format("Deleting '{0}'", contentTypesToDelete));
                File.Delete(contentTypesToDelete);
            }
            if (Directory.Exists(relsDirToDelete))
            {
                _cmdletPassedIn.WriteDebug(string.Format("Deleting '{0}'", relsDirToDelete));
                Directory.Delete(relsDirToDelete, true);
            }
            if (Directory.Exists(packageDirToDelete))
            {
                _cmdletPassedIn.WriteDebug(string.Format("Deleting '{0}'", packageDirToDelete));
                Directory.Delete(packageDirToDelete, true);
            }
        }

        private void MoveFilesIntoInstallPath(PSResourceInfo p, bool isScript, bool isLocalRepo, string dirNameVersion, string tempInstallPath, string installPath, string newVersion, string moduleManifestVersion, string nupkgVersion, string versionWithoutPrereleaseTag, string scriptPath)
        {
            // Creating the proper installation path depending on whether pkg is a module or script
            var newPathParent = isScript ? installPath : Path.Combine(installPath, p.Name);
            var finalModuleVersionDir = isScript ? installPath : Path.Combine(installPath, p.Name, moduleManifestVersion);  // versionWithoutPrereleaseTag
            _cmdletPassedIn.WriteDebug(string.Format("Installation path is: '{0}'", finalModuleVersionDir));

            // If script, just move the files over, if module, move the version directory over
            var tempModuleVersionDir = (isScript || isLocalRepo) ? dirNameVersion
                : Path.Combine(tempInstallPath, p.Name.ToLower(), newVersion);
            _cmdletPassedIn.WriteVerbose(string.Format("Full installation path is: '{0}'", tempModuleVersionDir));

            if (isScript)
            {
                if (!_savePkg)
                {
                    // Need to delete old xml files because there can only be 1 per script
                    var scriptXML = p.Name + "_InstalledScriptInfo.xml";
                    _cmdletPassedIn.WriteDebug(string.Format("Checking if path '{0}' exists: ", File.Exists(Path.Combine(installPath, "InstalledScriptInfos", scriptXML))));
                    if (File.Exists(Path.Combine(installPath, "InstalledScriptInfos", scriptXML)))
                    {
                        _cmdletPassedIn.WriteDebug(string.Format("Deleting script metadata XML"));
                        File.Delete(Path.Combine(installPath, "InstalledScriptInfos", scriptXML));
                    }

                    _cmdletPassedIn.WriteDebug(string.Format("Moving '{0}' to '{1}'", Path.Combine(dirNameVersion, scriptXML), Path.Combine(installPath, "InstalledScriptInfos", scriptXML)));
                    File.Move(Path.Combine(dirNameVersion, scriptXML), Path.Combine(installPath, "InstalledScriptInfos", scriptXML));

                    // Need to delete old script file, if that exists
                    _cmdletPassedIn.WriteDebug(string.Format("Checking if path '{0}' exists: ", File.Exists(Path.Combine(finalModuleVersionDir, p.Name + ".ps1"))));
                    if (File.Exists(Path.Combine(finalModuleVersionDir, p.Name + ".ps1")))
                    {
                        _cmdletPassedIn.WriteDebug(string.Format("Deleting script file"));
                        File.Delete(Path.Combine(finalModuleVersionDir, p.Name + ".ps1"));
                    }
                }

                _cmdletPassedIn.WriteDebug(string.Format("Moving '{0}' to '{1}'", scriptPath, Path.Combine(finalModuleVersionDir, p.Name + ".ps1")));
                File.Move(scriptPath, Path.Combine(finalModuleVersionDir, p.Name + ".ps1"));
            }
            else
            {
                // If new path does not exist
                if (!Directory.Exists(newPathParent))
                {
                    _cmdletPassedIn.WriteDebug(string.Format("Attempting to move '{0}' to '{1}'", tempModuleVersionDir, finalModuleVersionDir));
                    Directory.CreateDirectory(newPathParent);
                    Directory.Move(tempModuleVersionDir, finalModuleVersionDir);
                }
                else
                {
                    _cmdletPassedIn.WriteDebug(string.Format("Temporary module version directory is: '{0}'", tempModuleVersionDir));

                    // At this point if
                    if (Directory.Exists(finalModuleVersionDir))
                    {
                        // Delete the directory path before replacing it with the new module
                        _cmdletPassedIn.WriteDebug(string.Format("Attempting to delete '{0}'", finalModuleVersionDir));
                        Directory.Delete(finalModuleVersionDir, true);
                    }

                    _cmdletPassedIn.WriteDebug(string.Format("Attempting to move '{0}' to '{1}'", tempModuleVersionDir, finalModuleVersionDir));
                    Directory.Move(tempModuleVersionDir, finalModuleVersionDir);
                }
            }
        }
    }
}