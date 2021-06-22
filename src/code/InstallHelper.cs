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
    /// Find helper class
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

        /// 1: Check to see if the pkgs are already installed (ie the pkg is installed and the version satisfies the version range provided via param)
        /// 2: If pkg version is NOT installed, continue the installation process 
        ///    2a: Go through the repositories and see which one has the pkg version availble  (ie search call)  
        ///        Use find helper to search for the exact pkg version we want to install
        ///    2b: Proceed to find all dependencies for the package availale  
        /// 3:  Once all the pkg versions and dependency pkg versions are found, proceed to install each
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
                string.Join(",", names), versionRange.OriginalString, prerelease.ToString(), repository != null ? string.Join(",", repository) : string.Empty,
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

            // Check to see if the pkgs are already installed (ie the pkg is installed and the version satisfies the version range provided via param)
            // If reinstall is specified, we will skip this check            
            if (!_reinstall)
            {
                // Removes all of the names that are already installed from the list of names to search for
                names = CheckPkgsInstalled(names);
            }
            
            // If pkg version is NOT installed, continue the installation process 
            // Go through the repositories and see which one has the pkg version available (ie search call)  
            ProcessRepositories(names, repository, _trustRepository, _credential);             
            // Once all the pkg versions and dependency pkg versions are found, proceed to install each
        }


        // done 
        // Check if any of the pkg versions are already installed
        public string[] CheckPkgsInstalled(string[] names)
        {
            GetHelper getHelper = new GetHelper(cancellationToken, this);
            // _pathsToInstallPkg will only contain the paths specified within the -Scope param (if applicable)
            IEnumerable<PSResourceInfo> pkgsAlreadyInstalled = getHelper.ProcessGetParams(names, _versionRange, _pathsToInstallPkg);
            
            // If any pkg versions are already installed, write a message saying it is already installed and continue processing other pkg names
            // In this case we will NOT be checking any dependencies (the assumption is that the dependencies are already available).
            if (pkgsAlreadyInstalled.Any())
            {
                foreach (PSResourceInfo pkg in pkgsAlreadyInstalled)
                {
                    this.WriteWarning(string.Format("Resource '{0}' with version '{1}' is already installed.  If you would like to reinstall, please run the cmdlet again with the -Reinstall parameter", pkg.Name, pkg.Version));

                    // remove this pkg from the list of pkg names install
                    names.ToList().Remove(pkg.Name);
                }
            }

            return names;
        }

     
        // This method calls into the proper repository to search for the pkgs to install
        public void ProcessRepositories(string[] packageNames, string[] repository, bool trustRepository, PSCredential credential)
        {
            var listOfRepositories = RepositorySettings.Read(repository, out string[] _);
            // TODO: change name to 'packagesToInstall'
            var pkgsLeftToInstall = packageNames.ToList();
            var yesToAll = false;
            var noToAll = false;
            var repositoryIsNotTrusted = "Untrusted repository";
            var queryInstallUntrustedPackage = "You are installing the modules from an untrusted repository. If you trust this repository, change its Trusted value by running the Set-PSResourceRepository cmdlet. Are you sure you want to install the PSresource from '{0}' ?";

            foreach (var repo in listOfRepositories)
            {
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
                    // Search for pkgs
                    var isLocalRepo = repo.Url.AbsoluteUri.StartsWith(Uri.UriSchemeFile + Uri.SchemeDelimiter);

                    var findHelper = new FindHelper();
                    //List<IPackageSearchMetadata> pkgsToInstall = FindPkgsToInstall(repoName, repo.Url.AbsoluteUri, credential, isLocalRepo, packageNames, pkgsLeftToInstall);
                    List<IPackageSearchMetadata> pkgsToInstall = beginFindHelper(name: packageNames, Type: null, _version: _versionRange, _prerelease: _prerelease, null, null, repository: repoName, _credential: credential, _includeDependencies: true, writeToConsole: false);
                   
                    if (!pkgsToInstall.Any())
                    {
                        // TODO: messaging
                        return;
                    }

                    // Install pkgs
                    InstallPackage(pkgsToInstall, repoName, repo.Url.AbsoluteUri, credential, isLocalRepo);
                    //pkgsLeftToInstall = returnedPkgsNotInstalled;
                }
            }
        }













        private void InstallPackage(List<IPackageSearchMetadata> pkgsToInstall, string repoName, string repoUrl, PSCredential credential, bool isLocalRepo)
        {
            /*** INSTALL PACKAGES ***/
            foreach (IPackageSearchMetadata p in pkgsToInstall)
            {
                // Create a temp directory to install to
                var tempInstallPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                var dir = Directory.CreateDirectory(tempInstallPath);  // should check it gets created properly
                                                                       // To delete file attributes from the existing ones get the current file attributes first and use AND (&) operator
                                                                       // with a mask (bitwise complement of desired attributes combination).
                // TODO: check the attributes and if it's read only then set it 
                // attribute may be inherited from the parent
                dir.Attributes = dir.Attributes & ~FileAttributes.ReadOnly;
                //TODO:  are there Linux accommodations we need to consider here?

                cmdletPassedIn.WriteVerbose(string.Format("Begin installing package: '{0}'", p.Identity.Id));

                // TODO: move into a private helper function
                if (!_quiet && !save)
                {
                    int i = 1;
                    int j = 1;
                    /****************************
                    * START PACKAGE INSTALLATION -- start progress bar
                    *****************************/
                    // Write-Progress -Activity "Search in Progress" - Status "$i% Complete:" - PercentComplete $i

                    int activityId = 0;
                    string activity = "";
                    string statusDescription = "";
                    
                    // If the pkg exists in one of the names passed in, then we wont include it as a dependent package
                    activityId = 0;
                    activity = string.Format("Installing {0}...", p.Identity.Id);
                    statusDescription = string.Format("{0}% Complete:", i++);

                    j = 1;
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

                // Create PackageIdentity in order to download
                var pkgIdentity = new PackageIdentity(p.Identity.Id, p.Identity.Version);
                var cacheContext = new SourceCacheContext();

                if (isLocalRepo)
                {
                    /* Download from a local repository -- this is slightly different process than from a server */
                    var localResource = new FindLocalPackagesResourceV2(repoUrl);
                    var resource = new LocalDownloadResource(repoUrl, localResource);

                    // Actually downloading the .nupkg from a local repo
                    var result = resource.GetDownloadResourceResultAsync(
                         pkgIdentity,
                         new PackageDownloadContext(cacheContext),
                         tempInstallPath,
                         logger: NullLogger.Instance,
                         CancellationToken.None).GetAwaiter().GetResult();

                    PackageExtractionContext packageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Nupkg,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        null,
                        logger: NullLogger.Instance);

                    
                    if (_asNupkg) // this is Save functinality
                    {
                        DirectoryInfo nupkgPath = new DirectoryInfo(((System.IO.FileStream)result.PackageStream).Name);
                        File.Copy(nupkgPath.FullName, Path.Combine(tempInstallPath, pkgIdentity.Id + pkgIdentity.Version + ".nupkg"));
                        
                        continue;
                    }
                    else
                    {
                        // extracting from .nupkg and placing files into tempInstallPath
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
                    try
                    {
                        var result = downloadResource.GetDownloadResourceResultAsync(
                            identity: pkgIdentity,
                            downloadContext: new PackageDownloadContext(cacheContext),
                            tempInstallPath,
                            logger: NullLogger.Instance,
                            CancellationToken.None).GetAwaiter().GetResult();
                    }
                    catch { }
                    finally
                    {
                        // Need to close the .nupkg
                        result.Dispose();
                    }


                    if (_asNupkg)  // Save functionality
                    {
                        // Simply move the .nupkg from the temp installation path to the specified path (the path passed in via param value)
                        var tempPkgIdPath = System.IO.Path.Combine(tempInstallPath, p.Identity.Id, p.Identity.Version.ToString());
                        var tempPkgVersionPath = System.IO.Path.Combine(tempPkgIdPath, p.Identity.Id.ToLower() + "." + p.Identity.Version + ".nupkg");
                        var newSavePath = System.IO.Path.Combine(_specifiedPath, p.Identity.Id + "." + p.Identity.Version + ".nupkg");

                        // TODO: path should be preprocessed/resolved
                        File.Move(tempPkgVersionPath, _specifiedPath);

                        //pkgsLeftToInstall.Remove(pkgName);

                        continue;
                    }
                }

                cmdletPassedIn.WriteDebug(string.Format("Successfully able to download package from source to: '{0}'", tempInstallPath));

                // Prompt if module requires license acceptance
                // Need to read from .psd1
                var newVersion = p.Identity.Version.ToString();
                if (p.Identity.Version.IsPrerelease)
                {
                    newVersion = p.Identity.Version.ToString().Substring(0, p.Identity.Version.ToString().IndexOf('-'));
                }

                var modulePath = Path.Combine(tempInstallPath, pkgIdentity.Id, newVersion);
                var moduleManifest = Path.Combine(modulePath, pkgIdentity.Id + ".psd1");
                var requireLicenseAcceptance = false;

                // TODO: move into a helper method
                // Accept License
                if (!save)
                {
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
                                var PkgTempInstallPath = Path.Combine(tempInstallPath, p.Identity.Id, newVersion);
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
                                var acceptanceLicenseQuery = $"Do you accept the license terms for module '{p.Identity.Id}'.";
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
                                var message = $"License Acceptance is required for module '{p.Identity.Id}'. Please specify '-AcceptLicense' to perform this operation.";
                                var ex = new ArgumentException(message);  // System.ArgumentException vs PSArgumentException
                                var acceptLicenseError = new ErrorRecord(ex, "ForceAcceptLicense", ErrorCategory.InvalidArgument, null);

                                // TODO: update to write error
                                cmdletPassedIn.ThrowTerminatingError(acceptLicenseError);
                            }
                        }
                    }
                }


                string dirNameVersion = Path.Combine(tempInstallPath, p.Identity.Id.ToLower(), p.Identity.Version.ToNormalizedString().ToLower());

                // Delete the extra nupkg related files that are not needed and not part of the module/script
                // TODO: consider adding try/catch
                DeleteExtraneousFiles(tempInstallPath, p, dirNameVersion);
               

                var scriptPath = Path.Combine(dirNameVersion, (p.Identity.Id.ToString() + ".ps1"));
                var isScript = File.Exists(scriptPath) ? true : false;

                if (!Directory.Exists(dirNameVersion))
                {
                    cmdletPassedIn.WriteDebug(string.Format("Directory does not exist, creating directory: '{0}'", dirNameVersion));
                    Directory.CreateDirectory(dirNameVersion);
                }

                if (_includeXML)
                {
                    CreateMetadataXMLFile(dirNameVersion, repoName, p, isScript);
                }

                if (save)
                {
                   //TODO:  SavePackage();
                }
                else
                {
                    // Copy to proper path
                    // TODO: resolve this issue;
                    // todo: create helper function 'findscriptspath... find modules path'
                    // PICK A PATH TO INSTALL TO

                    // TODO:  move into helper method (move and delete method)
                    // PSModules: 
                    /// ./Modules
                    /// ./Scripts
                    var installPath = isScript ? psScriptsPath : psModulesPath;

                    // Creating the proper installation path depending on whether pkg is a module or script
                    var newPath = isScript ? installPath
                        : Path.Combine(installPath, p.Identity.Id.ToString());
                    cmdletPassedIn.WriteDebug(string.Format("Installation path is: '{0}'", newPath));

                    // If script, just move the files over, if module, move the version directory over
                    var tempModuleVersionDir = isScript ? dirNameVersion //Path.Combine(tempInstallPath, p.Identity.Id, p.Identity.Version.ToNormalizedString())
                        : Path.Combine(tempInstallPath, p.Identity.Id.ToLower());
                    cmdletPassedIn.WriteVerbose(string.Format("Full installation path is: '{0}'", tempModuleVersionDir));

                    if (isScript)
                    {
                        // Todo: 
                        // Need to delete old xml files because there can only be 1 per script
                        var scriptXML = p.Identity.Id + "_InstalledScriptInfo.xml";
                        cmdletPassedIn.WriteDebug(string.Format("Checking if path '{0}' exists: ", File.Exists(Path.Combine(psScriptsPath, "InstalledScriptInfos", scriptXML))));
                        if (File.Exists(Path.Combine(psScriptsPath, "InstalledScriptInfos", scriptXML)))
                        {
                            cmdletPassedIn.WriteDebug(string.Format("Deleting script metadata XML"));
                            File.Delete(Path.Combine(psScriptsPath, "InstalledScriptInfos", scriptXML));
                        }

                        cmdletPassedIn.WriteDebug(string.Format("Moving '{0}' to '{1}'", Path.Combine(dirNameVersion, scriptXML), Path.Combine(psScriptsPath, "InstalledScriptInfos", scriptXML)));
                        File.Move(Path.Combine(dirNameVersion, scriptXML), Path.Combine(psScriptsPath, "InstalledScriptInfos", scriptXML));

                        // Need to delete old script file, if that exists
                        cmdletPassedIn.WriteDebug(string.Format("Checking if path '{0}' exists: ", File.Exists(Path.Combine(newPath, p.Identity.Id + ".ps1"))));
                        if (File.Exists(Path.Combine(newPath, p.Identity.Id + ".ps1")))
                        {
                            cmdletPassedIn.WriteDebug(string.Format("Deleting script file"));
                            File.Delete(Path.Combine(newPath, p.Identity.Id + ".ps1"));
                        }

                        cmdletPassedIn.WriteDebug(string.Format("Moving '{0}' to '{1}'", scriptPath, Path.Combine(newPath, p.Identity.Id + ".ps1")));
                        File.Move(scriptPath, Path.Combine(newPath, p.Identity.Id + ".ps1"));
                    }
                    else
                    {
                        // TODO: update variables to be more specific
                        // If new path does not exist
                        if (!Directory.Exists(newPath))
                        {
                            cmdletPassedIn.WriteDebug(string.Format("Attempting to move '{0}' to '{1}", tempModuleVersionDir, newPath));
                            Directory.Move(tempModuleVersionDir, newPath);
                        }
                        else
                        {
                            tempModuleVersionDir = Path.Combine(tempModuleVersionDir, p.Identity.Version.ToString());
                            cmdletPassedIn.WriteDebug(string.Format("Temporary module version directory is: '{0}'", tempModuleVersionDir));

                            var newVersionPath = Path.Combine(newPath, newVersion);
                            cmdletPassedIn.WriteDebug(string.Format("Path for module version directory installation is: '{0}'", newVersionPath));


                            if (Directory.Exists(newVersionPath))
                            {
                                // Delete the directory path before replacing it with the new module
                                cmdletPassedIn.WriteDebug(string.Format("Attempting to delete '{0}'", newVersionPath));
                                Directory.Delete(newVersionPath, true);
                            }

                            cmdletPassedIn.WriteDebug(string.Format("Attempting to move '{0}' to '{1}", newPath, newVersion));
                            Directory.Move(tempModuleVersionDir, Path.Combine(newPath, newVersion));
                            
                        }
                    }
                }



                
                cmdletPassedIn.WriteVerbose(String.Format("Successfully installed package {0}", p.Identity.Id));

                // TODO: add finally statement here, consider wrapping in try/catch > should have a debug write, not fail/throw
                // Delete the temp directory and all its contents
                cmdletPassedIn.WriteDebug(string.Format("Attempting to delete '{0}'", tempInstallPath));
                if (Directory.Exists(tempInstallPath))
                {
                    Directory.Delete(tempInstallPath, true);
                }

                // TODO: consider some kind of "unable to install messaging"
            }
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
                    var scriptXML = p.Identity.Id + "_InstalledScriptInfo.xml";
                    cmdletPassedIn.WriteDebug(string.Format("Moving '{0}' to '{1}'", Path.Combine(dirNameVersion, scriptXML), Path.Combine(_specifiedPath, scriptXML)));
                    File.Move(Path.Combine(dirNameVersion, scriptXML), Path.Combine(_specifiedPath, scriptXML));
                }
            }
            else
            {
                var fullTempInstallpath = Path.Combine(tempInstallPath, pkgIdentity.Id, pkgIdentity.Version.ToString()); // localRepo ? Path.Combine(tempInstallPath, pkgIdentity.Version.ToString()) : Path.Combine(tempInstallPath, pkgIdentity.Id, pkgIdentity.Version.ToString());
                var fullPermanentNewPath = isLocalRepo ? Path.Combine(_specifiedPath, pkgIdentity.Id, pkgIdentity.Version.ToString()) : Path.Combine(_specifiedPath, pkgIdentity.Id);

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


        private void CreateMetadataXMLFile(string dirNameVersion, string repoName, IPackageSearchMetadata pkg, bool isScript)
        {
            // Create PSGetModuleInfo.xml
            // Script will have a metadata file similar to:  "TestScript_InstalledScriptInfo.xml"
            // Modules will have a metadata file: "PSGetModuleInfo.xml"
            var metadataXMLPath = isScript ? Path.Combine(dirNameVersion, (pkg.Identity.Id + "_InstalledScriptInfo.xml"))
                : Path.Combine(dirNameVersion, "PSGetModuleInfo.xml");


            // TODO: this may not be needed anymore, depending on what findHelper returns
            // TODO: Can use PSResourceInfo obj to find out if isScript
            // first we need to put the pkg into a PSResoourceInfo object, then try to write the xml
            // Try to convert the IPackageSearchMetadata into a PSResourceInfo object so that we can then easily create the metadata xml
            if (!PSResourceInfo.TryConvert(
                metadataToParse: pkg,
                psGetInfo: out PSResourceInfo pkgInstalled,
                repositoryName: repoName,
                type: isScript ? ResourceType.Script : ResourceType.Module,
                errorMsg: out string errorMsg))
            {
                cmdletPassedIn.WriteError(new ErrorRecord(
                    new PSInvalidOperationException("Error parsing IPackageSearchMetadata to PSResourceInfo with message: " + errorMsg),
                    "IPackageSearchMetadataToPSResourceInfoParsingError",
                    ErrorCategory.InvalidResult,
                    this));
                return;
            }

            // TODO: now need to add the extra properties like 'installation date' and 'installation path'
            // Write all metadata into metadataXMLPath
            if (!pkgInstalled.TryWrite(metadataXMLPath, out string error))
            {
                // TODO: write error 
            }
            
        }
    

        private void DeleteExtraneousFiles(string tempInstallPath, IPackageSearchMetadata p, string dirNameVersion)
        {
            // Deleting .nupkg SHA file, .nuspec, and .nupkg after unpacking the module
            var nupkgSHAToDelete = Path.Combine(dirNameVersion, (p.Identity.ToString() + ".nupkg.sha512").ToLower());
            var nuspecToDelete = Path.Combine(dirNameVersion, (p.Identity.Id + ".nuspec").ToLower());
            var nupkgToDelete = Path.Combine(dirNameVersion, (p.Identity.ToString() + ".nupkg").ToLower());

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



        // Finds the exact version that we need to search for or install
        // If a user provides a version range we need to make sure version is available
        //// search for the pkg in the repository to see if that version is available.
        private IPackageSearchMetadata SearchForPkgVersion(string pkgName, PackageMetadataResource pkgMetadataResource, SourceCacheContext srcContext, string repositoryName, VersionRange versionRange, bool prerelease)
        {
            IPackageSearchMetadata filteredFoundPkgs = null;

            if (versionRange == null)
            {
                // ensure that the latst version is returned first (the ordering of versions differ
                // TODO: proper error handling
                try
                {
                    // searchin the repository
                    filteredFoundPkgs = (pkgMetadataResource.GetMetadataAsync(pkgName, prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult()
                        .OrderByDescending(p => p.Identity.Version, VersionComparer.VersionRelease)
                        .FirstOrDefault());
                }
                catch
                {
                    // TODO: catch exception here
                }

                if (filteredFoundPkgs == null)
                {
                    cmdletPassedIn.WriteVerbose(String.Format("Could not find package '{0}' in repository '{1}'", pkgName, repositoryName));

                    return null;
                }
            }
            else
            {
                // Search for packages within a version range
                // ensure that the latest version is returned first (the ordering of versions differ
                filteredFoundPkgs = (pkgMetadataResource.GetMetadataAsync(pkgName, prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult()
                    .Where(p => versionRange.Satisfies(p.Identity.Version))
                    .OrderByDescending(p => p.Identity.Version, VersionComparer.VersionRelease)
                    .FirstOrDefault());
            }

            return filteredFoundPkgs;
        }


        
        // TODO: FindHelper will return dependencies as well
        private List<IPackageSearchMetadata> FindDependenciesFromSource(IPackageSearchMetadata pkg, PackageMetadataResource pkgMetadataResource, SourceCacheContext srcContext, bool prerelease, bool reinstall, string _path, string repositoryUrl)
        {
            // Dependency resolver
            // This function is recursively called
            // Call the findpackages from source helper (potentially generalize this so it's finding packages from source or cache)
            List<IPackageSearchMetadata> foundDependencies = new List<IPackageSearchMetadata>();

            // 1) Check the dependencies of this pkg
            // 2) For each dependency group, search for the appropriate name and version
            // A dependency group includes all the dependencies for a particular framework
            foreach (var dependencyGroup in pkg.DependencySets)
            {
                foreach (var pkgDependency in dependencyGroup.Packages)
                {
                    IEnumerable<IPackageSearchMetadata> dependencies = null;
                    // a) Check that the appropriate pkg dependencies exist
                    // Returns all versions from a single package id.
                    try
                    {
                        dependencies = pkgMetadataResource.GetMetadataAsync(pkgDependency.Id, prerelease, true, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();
                    }
                    catch
                    { }
                    // b) Check if the appropriate verion range exists  (if version exists, then add it to the list to return)
                    VersionRange versionRange = null;
                    try
                    {
                        versionRange = VersionRange.Parse(pkgDependency.VersionRange.OriginalString);
                    }
                    catch
                    {
                        var exMessage = String.Format("Error parsing version range");
                        var ex = new ArgumentException(exMessage);
                        var ErrorParsingVersionRange = new ErrorRecord(ex, "ErrorParsingVersionRange", ErrorCategory.ParserError, null);

                        cmdletPassedIn.ThrowTerminatingError(ErrorParsingVersionRange);
                    }

                    // If no version/version range is specified the we just return the latest version
                    IPackageSearchMetadata depPkgToReturn = (versionRange == null ?
                        dependencies.FirstOrDefault() :
                        dependencies.Where(v => versionRange.Satisfies(v.Identity.Version)).FirstOrDefault());




                    // TODO: figure out paths situation
                    // If the pkg already exists on the system, don't add it to the list of pkgs that need to be installed
                    var dirName = save ? cmdletPassedIn.SessionState.Path.GetResolvedPSPathFromPSPath(_path).FirstOrDefault().Path :  Path.Combine(psModulesPath, pkgDependency.Id);
                    var dependencyAlreadyInstalled = false;

                    // Check to see if the package dir exists in the path
                    // If save we only check the -path passed in
                    if (save || _pathsToInstallPkg.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                    {
                        // Then check to see if the package exists in the path
                        if (Directory.Exists(dirName))
                        {
                            var pkgDirVersion = (Directory.GetDirectories(dirName)).ToList();
                            List<string> pkgVersion = new List<string>();
                            foreach (var path in pkgDirVersion)
                            {
                                pkgVersion.Add(Path.GetFileName(path));
                            }

                            // These are all the packages already installed
                            NuGetVersion ver;
                            var pkgsAlreadyInstalled = pkgVersion.FindAll(p => NuGetVersion.TryParse(p, out ver) && versionRange.Satisfies(ver));

                            if (pkgsAlreadyInstalled.Any() && !reinstall)
                            {
                                // Don't add the pkg to the list of pkgs that need to be installed
                                dependencyAlreadyInstalled = true;
                            }
                        }
                    }

                    if (!dependencyAlreadyInstalled)
                    {
                        foundDependencies.Add(depPkgToReturn);
                    }

                    // Recursively search for any dependencies the pkg has
                    foundDependencies.AddRange(FindDependenciesFromSource(depPkgToReturn, pkgMetadataResource, srcContext, prerelease, reinstall, _path, repositoryUrl));
                }
            }

            return foundDependencies;
        }
    }



}