// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using static System.Environment;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using MoreLinq.Extensions;
using Newtonsoft.Json;
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
        private CancellationToken cancellationToken;
        private readonly bool update;
        private readonly bool save;
        private readonly PSCmdlet cmdletPassedIn;
        List<string> _pathsToInstallPkg;
        VersionRange _versionRange;
        bool _prerelease;
        string _scope;
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

        public InstallHelper(bool update, bool save, CancellationToken cancellationToken, PSCmdlet cmdletPassedIn)
        {
            this.update = update;
            this.save = save;
            this.cancellationToken = cancellationToken;
            this.cmdletPassedIn = cmdletPassedIn;
        }

        // TODO:  add passthru
        public void ProcessInstallParams(
            string[] names, 
            VersionRange versionRange, 
            bool prerelease, 
            string[] repository, 
            string scope, 
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
            cmdletPassedIn.WriteDebug(string.Format("Parameters passed in >>> Name: '{0}'; Version: '{1}'; Prerelease: '{2}'; Repository: '{3}'; Scope: '{4}'; " +
                "AcceptLicense: '{5}'; Quiet: '{6}'; Reinstall: '{7}'; TrustRepository: '{8}'; NoClobber: '{9}';", 
                string.Join(",", names), (_versionRange != null ? _versionRange.OriginalString : string.Empty), prerelease.ToString(), repository != null ? string.Join(",", repository) : string.Empty,
                scope != null ? scope : string.Empty, acceptLicense.ToString(), quiet.ToString(), reinstall.ToString(), trustRepository.ToString(), noClobber.ToString()));

            _versionRange = versionRange;
            _prerelease = prerelease;
            _scope = scope;
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

            IEnumerable<PSResourceInfo> pkgsAlreadyInstalled = new List<PSResourceInfo>();
            
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
                cmdletPassedIn.WriteDebug(string.Format("Attempting to search for packages in '{0}'", repoName));
                
                // Source is only trusted if it's set at the repository level to be trusted, -TrustRepository flag is true, -Force flag is true
                // OR the user issues trust interactively via console.
                if (repo.Trusted == false && !trustRepository && !_force)
                {
                    cmdletPassedIn.WriteDebug("Checking if untrusted repository should be used");

                    if (!(yesToAll || noToAll))
                    {
                        // Prompt for installation of package from untrusted repository
                        var message = string.Format(CultureInfo.InvariantCulture, queryInstallUntrustedPackage, repoName);
                        sourceTrusted = cmdletPassedIn.ShouldContinue(message, repositoryIsNotTrusted, true, ref yesToAll, ref noToAll);
                    }
                }
                else {
                    sourceTrusted = true;
                }

                if (sourceTrusted || yesToAll)
                {
                    cmdletPassedIn.WriteDebug("Untrusted repository accepted as trusted source.");

                    // If it can't find the pkg in one repository, it'll look for it in the next repo in the list
                    // TODO: make sure to write a test for this scenario
                    var isLocalRepo = repo.Url.AbsoluteUri.StartsWith(Uri.UriSchemeFile + Uri.SchemeDelimiter);

                    var cancellationToken = new CancellationToken();
                    var findHelper = new FindHelper(cancellationToken, cmdletPassedIn);
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

                    //var test = pkgsFromRepoToInstall.FirstOrDefault();

                    // Deduplicate any packages
                    pkgsFromRepoToInstall.GroupBy(
                        m => new { m.Name, m.Version }).Select(
                            group => group.First()).ToList();
                    
                    if (!pkgsFromRepoToInstall.Any())
                    {
                        cmdletPassedIn.WriteVerbose(string.Format("None of the specified resources were found in the '{0}' repository.", repoName));
                        // Check in the next repository
                        continue;
                    }

                    // Check to see if the pkgs (including dependencies) are already installed (ie the pkg is installed and the version satisfies the version range provided via param)
                    // If reinstall is specified, we will skip this check            
                    if (!_reinstall)
                    {
                        // Removes all of the names that are already installed from the list of names to search for
                        pkgsFromRepoToInstall = FilterByInstalledPkgs(pkgsFromRepoToInstall);
                    }

                    if (!pkgsFromRepoToInstall.Any()) continue;

                    List<string> pkgsInstalled = InstallPackage(pkgsFromRepoToInstall, repoName, repo.Url.AbsoluteUri, credential, isLocalRepo);

                    foreach (string name in pkgsInstalled)
                    {
                        if (packagesToInstall.Contains(name))
                        {
                            packagesToInstall.Remove(name);
                        }
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

            GetHelper getHelper = new GetHelper(cancellationToken, this);
            // _pathsToInstallPkg will only contain the paths specified within the -Scope param (if applicable)
            IEnumerable<PSResourceInfo> pkgsAlreadyInstalled = getHelper.ProcessGetParams(pkgNames.ToArray(), _versionRange, _pathsToInstallPkg);

            // If any pkg versions are already installed, write a message saying it is already installed and continue processing other pkg names
            if (pkgsAlreadyInstalled.Any())
            {
                foreach (PSResourceInfo pkg in pkgsAlreadyInstalled)
                {
                    this.WriteWarning(string.Format("Resource '{0}' with version '{1}' is already installed.  If you would like to reinstall, please run the cmdlet again with the -Reinstall parameter", pkg.Name, pkg.Version));

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
                                                                           //TODO:  are there Linux accommodations we need to consider here?
                    dir.Attributes = dir.Attributes & ~FileAttributes.ReadOnly;

                    cmdletPassedIn.WriteVerbose(string.Format("Begin installing package: '{0}'", p.Name));

                    if (!_quiet && !save)
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
                        cmdletPassedIn.WriteDebug("Error parsing version into a NuGetVersion");
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
                             token: CancellationToken.None).GetAwaiter().GetResult();


                        if (_asNupkg) // this is Save functinality
                        {
                            DirectoryInfo nupkgPath = new DirectoryInfo(((System.IO.FileStream)result.PackageStream).Name);
                            File.Copy(nupkgPath.FullName, Path.Combine(tempInstallPath, pkgIdentity.Id + pkgIdentity.Version + ".nupkg"));

                            continue;
                        }
                        else
                        {
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
                                token: CancellationToken.None);
                            result.Dispose();
                        }
                    }
                    else
                    {
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
                                CancellationToken.None).GetAwaiter().GetResult();
                        }
                        catch (Exception e)
                        {
                            cmdletPassedIn.WriteDebug(string.Format("Error attempting download: '{0}'", e.Message));
                        }
                        finally
                        {
                            // Need to close the .nupkg
                            if (result != null) result.Dispose();
                        }

                        if (_asNupkg)  // Save functionality
                        {
                            /*
                            // Simply move the .nupkg from the temp installation path to the specified path (the path passed in via param value)
                            var tempPkgIdPath = System.IO.Path.Combine(tempInstallPath, p.Identity.Id, p.Identity.Version.ToString());
                            var tempPkgVersionPath = System.IO.Path.Combine(tempPkgIdPath, p.Identity.Id.ToLower() + "." + p.Identity.Version + ".nupkg");
                            var newSavePath = System.IO.Path.Combine(_specifiedPath, p.Identity.Id + "." + p.Identity.Version + ".nupkg");

                            // TODO: path should be preprocessed/resolved
                            File.Move(tempPkgVersionPath, _specifiedPath);

                            //packagesToInstall.Remove(pkgName);

                            continue;
                            */
                        }
                    }

                    cmdletPassedIn.WriteDebug(string.Format("Successfully able to download package from source to: '{0}'", tempInstallPath));

                    // Prompt if module requires license acceptance (need to read info license acceptance info from the module manifest)
                    // pkgIdentity.Version.Version gets the version without metadata or release labels.
                    string newVersion; 
                    if (pkgIdentity.Version.IsPrerelease)
                    {
                        newVersion = pkgIdentity.Version.ToNormalizedString().Substring(0, pkgIdentity.Version.ToNormalizedString().IndexOf('-'));
                    }
                    else {
                        newVersion = pkgIdentity.Version.ToNormalizedString();
                    }

                    var versionWithoutPrereleaseTag = pkgIdentity.Version.Version.ToString();
                    var modulePath = Path.Combine(tempInstallPath, pkgIdentity.Id.ToLower(), newVersion);
                    var moduleManifest = Path.Combine(modulePath, pkgIdentity.Id + ".psd1");

                    // Accept License verification
                    if (!save) CallAcceptLicense(p, moduleManifest, tempInstallPath, newVersion);
                    
                    string dirNameVersion = Path.Combine(tempInstallPath, p.Name.ToLower(), newVersion);

                    // Delete the extra nupkg related files that are not needed and not part of the module/script
                    DeleteExtraneousFiles(tempInstallPath, pkgIdentity, dirNameVersion);

                    if (!Directory.Exists(dirNameVersion))
                    {
                        cmdletPassedIn.WriteDebug(string.Format("Directory does not exist, creating directory: '{0}'", dirNameVersion));
                        Directory.CreateDirectory(dirNameVersion);
                    }

                    var scriptPath = Path.Combine(dirNameVersion, (p.Name + ".ps1"));
                    var isScript = File.Exists(scriptPath) ? true : false;

                    if (_includeXML) CreateMetadataXMLFile(dirNameVersion, repoName, p, isScript);

                    if (save)
                    {
                        //TODO:  SavePackage();
                    }
                    else
                    {
                        MoveFilesIntoInstallPath(p, isScript, dirNameVersion, tempInstallPath, newVersion, versionWithoutPrereleaseTag, scriptPath);
                    }


                    cmdletPassedIn.WriteVerbose(String.Format("Successfully installed package '{0}'", p.Name));
                    pkgsSuccessfullyInstalled.Add(p.Name);
                }
                catch (Exception e)
                {
                    cmdletPassedIn.WriteDebug(string.Format("Unable to successfully install package '{0}': '{1}'", p.Name, e.Message));
                }
                finally
                {
                    // Delete the temp directory and all its contents
                    cmdletPassedIn.WriteDebug(string.Format("Attempting to delete '{0}'", tempInstallPath));
                    if (Directory.Exists(tempInstallPath))
                    {
                        Directory.Delete(tempInstallPath, true);
                    }
                }
            }

            return pkgsSuccessfullyInstalled;
        }




        // IGNORE FOR INSTALL
        private void SavePackage(PackageIdentity pkgIdentity, string tempInstallPath, string dirNameVersion, bool isScript, bool isLocalRepo)
        {
            // I don't believe we should ever be getting to this _asNupkg
            if (isScript)
            {
                var tempScriptPath = Path.Combine(tempInstallPath, pkgIdentity.Id, pkgIdentity.Version.ToNormalizedString());
                var scriptName = pkgIdentity.Id + ".ps1";
                File.Copy(Path.Combine(tempScriptPath, scriptName), Path.Combine(_specifiedPath, scriptName));

                if (_includeXML)
                {
                    // else if save and including XML
                    var scriptXML = pkgIdentity.Id + "_InstalledScriptInfo.xml";
                    cmdletPassedIn.WriteDebug(string.Format("Moving '{0}' to '{1}'", Path.Combine(dirNameVersion, scriptXML), Path.Combine(_specifiedPath, scriptXML)));
                    File.Move(Path.Combine(dirNameVersion, scriptXML), Path.Combine(_specifiedPath, scriptXML));
                }
            }
            else
            {
                var fullTempInstallpath = Path.Combine(tempInstallPath, pkgIdentity.Id, pkgIdentity.Version.ToString()); // localRepo ? Path.Combine(tempInstallPath, pkgIdentity.Version.ToString()) : Path.Combine(tempInstallPath, pkgIdentity.Id, pkgIdentity.Version.ToString());
                var fullPermanentNewPath = isLocalRepo ? Path.Combine(_specifiedPath, pkgIdentity.Id, pkgIdentity.Version.ToString()) 
                                                        : Path.Combine(_specifiedPath, pkgIdentity.Id);

                if (isLocalRepo && !Directory.Exists(Path.Combine(_specifiedPath, pkgIdentity.Id)))
                {
                    Directory.CreateDirectory(Path.Combine(_specifiedPath, pkgIdentity.Id));
                }

                if (isLocalRepo)
                {
                    Directory.Move(tempInstallPath, fullPermanentNewPath);
                }
                else
                {
                    Directory.Move(Path.Combine(tempInstallPath, pkgIdentity.Id), fullPermanentNewPath);
                    fullPermanentNewPath = Path.Combine(fullPermanentNewPath, pkgIdentity.Version.ToString());
                }
                var tempPSGetModuleInfoXML = Path.Combine(Path.Combine(fullPermanentNewPath, pkgIdentity.Id, pkgIdentity.Version.ToString()), "PSGetModuleInfo.xml");
                if (File.Exists(tempPSGetModuleInfoXML))
                {
                    File.Copy(tempPSGetModuleInfoXML, Path.Combine(fullPermanentNewPath, "PSGetModuleInfo.xml"));
                }

                DeleteExtraneousSaveFiles(pkgIdentity, fullPermanentNewPath);
            }
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
            cmdletPassedIn.WriteProgress(progressRecord);
        }

        private void CallAcceptLicense(PSResourceInfo p, string moduleManifest, string tempInstallPath, string newVersion)
        {
            var requireLicenseAcceptance = false;

            if (File.Exists(moduleManifest))
            {
                using (StreamReader sr = new StreamReader(moduleManifest))
                {
                    var text = sr.ReadToEnd();

                    var pattern = "RequireLicenseAcceptance\\s*=\\s*\\$true";
                    var patternToSkip1 = "#\\s*RequireLicenseAcceptance\\s*=\\s*\\$true";
                    var patternToSkip2 = "\\*\\s*RequireLicenseAcceptance\\s*=\\s*\\$true";

                    Regex rgx = new Regex(pattern);

                    if (rgx.IsMatch(pattern) && !rgx.IsMatch(patternToSkip1) && !rgx.IsMatch(patternToSkip2))
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
                            var ex = new ArgumentException(exMessage);  // System.ArgumentException vs PSArgumentException
                            var acceptLicenseError = new ErrorRecord(ex, "LicenseTxtNotFound", ErrorCategory.ObjectNotFound, null);

                            // TODO: update this to write error
                            cmdletPassedIn.ThrowTerminatingError(acceptLicenseError);
                        }

                        // Otherwise read LicenseFile
                        string licenseText = System.IO.File.ReadAllText(LicenseFilePath);
                        var acceptanceLicenseQuery = $"Do you accept the license terms for module '{p.Name}'.";
                        var message = licenseText + "`r`n" + acceptanceLicenseQuery;

                        var title = "License Acceptance";
                        var yesToAll = false;
                        var noToAll = false;
                        var shouldContinueResult = ShouldContinue(message, title, true, ref yesToAll, ref noToAll);

                        if (yesToAll)
                        {
                            _acceptLicense = true;
                        }
                    }

                    // Check if user agreed to license terms, if they didn't then throw error, otherwise continue to install
                    if (!_acceptLicense)
                    {
                        var message = $"License Acceptance is required for module '{p.Name}'. Please specify '-AcceptLicense' to perform this operation.";
                        var ex = new ArgumentException(message);  // System.ArgumentException vs PSArgumentException
                        var acceptLicenseError = new ErrorRecord(ex, "ForceAcceptLicense", ErrorCategory.InvalidArgument, null);

                        // TODO: update to write error
                        cmdletPassedIn.ThrowTerminatingError(acceptLicenseError);
                    }
                }
            }
        }

        private void CreateMetadataXMLFile(string dirNameVersion, string repoName, PSResourceInfo pkg, bool isScript)
        {
            // Script will have a metadata file similar to:  "TestScript_InstalledScriptInfo.xml"
            // Modules will have the metadata file: "PSGetModuleInfo.xml"
            var metadataXMLPath = isScript ? Path.Combine(dirNameVersion, (pkg.Name + "_InstalledScriptInfo.xml"))
                : Path.Combine(dirNameVersion, "PSGetModuleInfo.xml");

            
            // TODO: now need to add the extra properties like 'installation date' and 'installation path'

            // Write all metadata into metadataXMLPath
            if (!pkg.TryWrite(metadataXMLPath, out string error))
            {
                var message = string.Format("Error parsing metadata into XML: '{0}'", error);
                var ex = new ArgumentException(message);
                var ErrorParsingMetadata = new ErrorRecord(ex, "ErrorParsingMetadata", ErrorCategory.ParserError, null);
                WriteError(ErrorParsingMetadata);
            }
            
        }
    

        private void DeleteExtraneousFiles(string tempInstallPath, PackageIdentity pkgIdentity, string dirNameVersion)
        {
            /// test this!!!!!!!
            // Deleting .nupkg SHA file, .nuspec, and .nupkg after unpacking the module
            var pkgIdString = pkgIdentity.ToString();
            var nupkgSHAToDelete = Path.Combine(dirNameVersion, (pkgIdString + ".nupkg.sha512").ToLower());
            var nuspecToDelete = Path.Combine(dirNameVersion, (pkgIdentity.Id + ".nuspec").ToLower());
            var nupkgToDelete = Path.Combine(dirNameVersion, (pkgIdString + ".nupkg").ToLower());

            // unforunately have to check if each file exists because it may or may not be there
            if (File.Exists(nupkgSHAToDelete))
            {
                cmdletPassedIn.WriteDebug(string.Format("Deleting '{0}'", nupkgSHAToDelete));
                File.Delete(nupkgSHAToDelete);
            }
            if (File.Exists(nuspecToDelete))
            {
                cmdletPassedIn.WriteDebug(string.Format("Deleting '{0}'", nuspecToDelete));
                File.Delete(nuspecToDelete);
            }
            if (File.Exists(nupkgToDelete))
            {
                cmdletPassedIn.WriteDebug(string.Format("Deleting '{0}'", nupkgToDelete));
                File.Delete(nupkgToDelete);
            }

            // TODO: write debug messaging here
        }

        private void DeleteExtraneousSaveFiles(PackageIdentity pkgIdentity, string fullPermanentNewPath)
        {
            var relsPath = Path.Combine(fullPermanentNewPath, "_rels");
            if (Directory.Exists(relsPath))
            {
                Directory.Delete(relsPath, true);
            }

            var packagePath = Path.Combine(fullPermanentNewPath, "package");
            if (Directory.Exists(packagePath))
            {
                Directory.Delete(packagePath, true);
            }

            var pkgIdPath = Path.Combine(fullPermanentNewPath, pkgIdentity.Id);
            if (Directory.Exists(pkgIdPath))
            {
                Directory.Delete(pkgIdPath, true);
            }

            var pkgVersionPath = Path.Combine(Path.Combine(_specifiedPath, pkgIdentity.Id, pkgIdentity.Version.ToString()), pkgIdentity.Version.ToString());
            if (Directory.Exists(pkgVersionPath))
            {
                Directory.Delete(Path.Combine(pkgVersionPath), true);
            }

            var contentTypesXMLPath = Path.Combine(fullPermanentNewPath, "[Content_Types].xml");
            if (File.Exists(contentTypesXMLPath))
            {
                File.Delete(contentTypesXMLPath);
            }

            var nuspecPath = Path.Combine(fullPermanentNewPath, pkgIdentity.Id + ".nuspec");
            if (File.Exists(nuspecPath))
            {
                File.Delete(nuspecPath);
            }

            var nupkgMetadata = Path.Combine(fullPermanentNewPath, ".nupkg.metadata");
            if (File.Exists(nupkgMetadata))
            {
                File.Delete(nupkgMetadata);
            }
        }



        private void MoveFilesIntoInstallPath(PSResourceInfo p, bool isScript, string dirNameVersion, string tempInstallPath, string newVersion, string versionWithoutPrereleaseTag, string scriptPath)
        {
            // PSModules: 
            /// ./Modules
            /// ./Scripts
            /// _pathsToInstallPkg is sorted by desirability, Find will pick the pick the first Script or Modules path found in the list
            var installPath = isScript ? _pathsToInstallPkg.Find(path => path.EndsWith("Scripts", StringComparison.InvariantCultureIgnoreCase))
                                        : _pathsToInstallPkg.Find(path => path.EndsWith("Modules", StringComparison.InvariantCultureIgnoreCase));

            // Creating the proper installation path depending on whether pkg is a module or script
            var newPathParent = isScript ? installPath : Path.Combine(installPath, p.Name);
            var newPath = isScript ? installPath : Path.Combine(installPath, p.Name, newVersion);
            cmdletPassedIn.WriteDebug(string.Format("Installation path is: '{0}'", newPath));

            // If script, just move the files over, if module, move the version directory over
            var tempModuleVersionDir = isScript ? dirNameVersion   //Path.Combine(tempInstallPath, p.Identity.Id, p.Identity.Version.ToNormalizedString())
                : Path.Combine(tempInstallPath, p.Name.ToLower(), newVersion);
            cmdletPassedIn.WriteVerbose(string.Format("Full installation path is: '{0}'", tempModuleVersionDir));

            if (isScript)
            {
                // Need to delete old xml files because there can only be 1 per script
                var scriptXML = p.Name + "_InstalledScriptInfo.xml";
                cmdletPassedIn.WriteDebug(string.Format("Checking if path '{0}' exists: ", File.Exists(Path.Combine(installPath, "InstalledScriptInfos", scriptXML))));
                if (File.Exists(Path.Combine(installPath, "InstalledScriptInfos", scriptXML)))
                {
                    cmdletPassedIn.WriteDebug(string.Format("Deleting script metadata XML"));
                    File.Delete(Path.Combine(installPath, "InstalledScriptInfos", scriptXML));
                }

                cmdletPassedIn.WriteDebug(string.Format("Moving '{0}' to '{1}'", Path.Combine(dirNameVersion, scriptXML), Path.Combine(installPath, "InstalledScriptInfos", scriptXML)));
                File.Move(Path.Combine(dirNameVersion, scriptXML), Path.Combine(installPath, "InstalledScriptInfos", scriptXML));

                // Need to delete old script file, if that exists
                cmdletPassedIn.WriteDebug(string.Format("Checking if path '{0}' exists: ", File.Exists(Path.Combine(newPath, p.Name + ".ps1"))));
                if (File.Exists(Path.Combine(newPath, p.Name + ".ps1")))
                {
                    cmdletPassedIn.WriteDebug(string.Format("Deleting script file"));
                    File.Delete(Path.Combine(newPath, p.Name + ".ps1"));
                }

                cmdletPassedIn.WriteDebug(string.Format("Moving '{0}' to '{1}'", scriptPath, Path.Combine(newPath, p.Name + ".ps1")));
                File.Move(scriptPath, Path.Combine(newPath, p.Name + ".ps1"));
            }
            else
            {
                // If new path does not exist
                if (!Directory.Exists(newPathParent))
                {
                    cmdletPassedIn.WriteDebug(string.Format("Attempting to move '{0}' to '{1}'", tempModuleVersionDir, newPath));
                    Directory.CreateDirectory(newPathParent);
                    Directory.Move(tempModuleVersionDir, newPath);
                }
                else
                {
                    tempModuleVersionDir = Path.Combine(tempModuleVersionDir, newVersion);
                    var finalModuleVersionDir = Path.Combine(newPath, versionWithoutPrereleaseTag);
                    cmdletPassedIn.WriteDebug(string.Format("Temporary module version directory is: '{0}'", tempModuleVersionDir));

                    var newVersionPath = Path.Combine(newPath, newVersion);
                    cmdletPassedIn.WriteDebug(string.Format("Path for module version directory installation is: '{0}'", newVersionPath));


                    if (Directory.Exists(newVersionPath))
                    {
                        // Delete the directory path before replacing it with the new module
                        cmdletPassedIn.WriteDebug(string.Format("Attempting to delete '{0}'", newVersionPath));
                        Directory.Delete(newVersionPath, true);
                    }

                    cmdletPassedIn.WriteDebug(string.Format("Attempting to move '{0}' to '{1}'", tempModuleVersionDir, finalModuleVersionDir));
                    Directory.Move(tempModuleVersionDir, finalModuleVersionDir);

                }
            }

        }
        
    }



}