
using System;
using System.Collections;
using System.Management.Automation;
using System.Collections.Generic;
using NuGet.Configuration;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System.Threading;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System.Net;
using System.Linq;
using MoreLinq.Extensions;
using System.IO;
using Microsoft.PowerShell.PowerShellGet.RepositorySettings;
using System.Globalization;
using System.Security.Principal;
using static System.Environment;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{


    /// <summary>
    /// The Install-PSResource cmdlet installs a resource.
    /// It returns nothing.
    /// </summary>

    [Cmdlet(VerbsData.Update, "PSResource", DefaultParameterSetName = "NameParameterSet", SupportsShouldProcess = true,
    HelpUri = "<add>", RemotingCapability = RemotingCapability.None)]
    public sealed
    class UpdatePSResource : PSCmdlet
    {
        //  private string PSGalleryRepoName = "PSGallery";

        /// <summary>
        /// Specifies the exact names of resources to update.
        /// A comma-separated list of module names is accepted. The resource name must match the resource name in the repository.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string[] Name
        {
            get
            { return _name; }

            set
            { _name = value; }
        }
        private string[] _name; // = new string[0];

        /*
        /// <summary>
        /// Used for pipeline input.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "InputObjectSet")]
        [ValidateNotNullOrEmpty]
        public PSCustomObject[] InputObject
        {
            get
            { return _inputObject; }

            set
            { _inputObject = value; }
        }
        private PSCustomObject[] _inputObject; 
        */

        /// <summary>
        /// Specifies the version or version range of the package to be updated
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string Version
        {
            get
            { return _version; }

            set
            { _version = value; }
        }
        private string _version;


        /// /// <summary>
        /// Allows updating to latest path version, minor version, or major version (cannot use this parameter with the MaximumVersion or RequiredVersion parameters).
        /// </summary>
        //[Parameter(ValueFromPipelineByPropertyName = true)]
        [ValidateSet(new string[] { "PatchVersion", "MinorVersion", "MajorVersion" })]
        public string UpdateTo
        {
            get
            { return _updateTo; }

            set
            { _updateTo = value; }
        }
        private string _updateTo;


        /// <summary>
        /// Specifies to allow installation of prerelease versions
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        public SwitchParameter Prerelease
        {
            get
            { return _prerelease; }

            set
            { _prerelease = value; }
        }
        private SwitchParameter _prerelease;

        /// <summary>
        /// Specifies a user account that has rights to find a resource from a specific repository.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string[] Repository
        {
            get
            { return _repository; }

            set
            { _repository = value; }
        }
        private string[] _repository;


     
        /// <summary>
        /// Specifies a user account that has rights to find a resource from a specific repository.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        public PSCredential Credential
        {
            get
            { return _credential; }

            set
            { _credential = value; }
        }
        private PSCredential _credential;


        /// <summary>
        /// Overrides warning messages about resource conflicts.
        /// If a resource with the same name already exists on the computer, Force allows for multiple versions to be installed.
        /// If there is an existing resource with the same name and version, Force does NOT overwrite that version.
        /// </summary>
        [Parameter()]
        public SwitchParameter Force
        {
            get { return _force; }

            set { _force = value; }
        }
        private SwitchParameter _force;


        /*
        /// <summary>
        /// Suppresses progress information.
        /// </summary>
        [Parameter()]
        public SwitchParameter Quiet
        {
            get { return _quiet; }

            set { _quiet = value; }
        }
        private SwitchParameter _quiet;
        */

        /*
        /// <summary>
        /// For modules that require a license, AcceptLicense automatically accepts the license agreement during installation.
        /// </summary>
        [Parameter()]
        public SwitchParameter AcceptLicense
        {
            get { return _acceptLicense; }

            set { _acceptLicense = value; }
        }
        private SwitchParameter _acceptLicense;
        */

        /*
        /// <summary>
        /// Returns the resource as an object to the console.
        /// </summary>
        [Parameter()]
        public SwitchParameter PassThru
        {
            get { return _passThru; }

            set { _passThru = value; }
        }
        private SwitchParameter _passThru;
        */

        // This will be a list of all the repository caches
        public static readonly List<string> RepoCacheFileName = new List<string>();
        public static readonly string RepositoryCacheDir = Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "PowerShellGet", "RepositoryCache");
        public static readonly string OsPlatform = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

        private string programFilesPath;
        private string myDocumentsPath;

        private string psPath;
        private string psModulesPath;
        private string psScriptsPath;
        private List<string> psModulesPathAllDirs;
        private List<string> psScriptsPathAllDirs;
        private List<string> pkgsLeftToInstall;
        // Define the cancellation token.
        CancellationTokenSource source;
        CancellationToken cancellationToken;


        /// <summary>
        /// </summary>
        protected override void ProcessRecord()
        {
            source = new CancellationTokenSource();
            cancellationToken = source.Token;

   
            var id = WindowsIdentity.GetCurrent();
            var consoleIsElevated = (id.Owner != id.User);



            // TODO:  Test this!           
            // if not core CLR
            var isWindowsPS = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory().ToLower().Contains("windows") ? true : false;

            if (isWindowsPS)
            {
                programFilesPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.ProgramFiles), "WindowsPowerShell");
                /// TODO:  Come back to this
                var userENVpath = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), "Documents");


                myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.MyDocuments), "WindowsPowerShell");
            }
            else
            {
                programFilesPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.ProgramFiles), "PowerShell");
                myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.MyDocuments), "PowerShell");
            }





            // if Scope is AllUsers and there is no console elevation
            if (!string.IsNullOrEmpty(_scope) && _scope.Equals("AllUsers") && !consoleIsElevated)
            {
                // throw an error when Install-PSResource is used as a non-admin user and '-Scope AllUsers'
                throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Install-PSResource requires admin privilege for AllUsers scope."));
            }

            // if no scope is specified (whether or not the console is elevated) default installation will be to CurrentUser
            // If running under admin on Windows with PowerShell less than PS6, default will be AllUsers
            if (string.IsNullOrEmpty(_scope))
            {
                _scope = "CurrentUser";

                //If Windows and elevated default scope will be all users 
                // If non-Windows or non-elevated default scope will be current user
               

                // * TODO:  TEST Come back here! Add is Elevated
               // if (!Platform.IsCoreCLR && consoleIsElevated)  
               if (isWindowsPS)
                {
                    _scope = "AllUsers";
                }
            }
            // if scope is Current user & (no elevation or elevation)
            // install to current user path




            psPath = string.Equals(_scope, "AllUsers") ? programFilesPath : myDocumentsPath;
            psModulesPath = Path.Combine(psPath, "Modules");
            psScriptsPath = Path.Combine(psPath, "Scripts");
            
            
            psModulesPathAllDirs = (Directory.GetDirectories(psModulesPath)).ToList();
            psScriptsPathAllDirs = (Directory.GetDirectories(psScriptsPath)).ToList();

            var r = new RespositorySettings();
            var listOfRepositories = r.Read(_repository);


            if (string.Equals(listOfRepositories[0].Properties["Trusted"].Value.ToString(), "false", StringComparison.InvariantCultureIgnoreCase) && !_trustRepository && !_force)
            {
                // throw error saying repository is not trusted
                // throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "This repository is not trusted"));  /// we should prompt for user input to accept 

                /*
                 * Untrusted repository
                 * You are installing the modules from an untrusted repository.  If you trust this repository, change its InstallationPolicy value by running the Set-PSResourceRepository cmdlet.
                 * Are you sure you want to install the modules from '<repo name >'?
                 * [Y]  Yes  [A]  Yes to ALl   [N]  No  [L]  No to all  [s]  suspendd [?] Help  (default is "N"):
                 */

            }



            pkgsLeftToInstall = _name.ToList();
            foreach (var repoName in listOfRepositories)
            {
                // if it can't find the pkg in one repository, it'll look in the next one in the list 
                // returns any pkgs that weren't found
                var returnedPkgsNotInstalled = InstallHelper(repoName.Properties["Url"].Value.ToString(), pkgsLeftToInstall, cancellationToken);
                if (!pkgsLeftToInstall.Any())
                {
                    return;
                }
                pkgsLeftToInstall = returnedPkgsNotInstalled;
            }





        }













        // Installing a package will have a transactional behavior:
        // Package and its dependencies will be saved into a tmp folder
        // and will only be properly installed if all dependencies are found successfully
        // Once package is installed, we want to resolve and install all dependencies
        // Installing


        public List<string> InstallHelper(string repositoryUrl, List<string> pkgsLeftToInstall, CancellationToken cancellationToken)
        {




            PackageSource source = new PackageSource(repositoryUrl);

            if (_credential != null)
            {
                string password = new NetworkCredential(string.Empty, _credential.Password).Password;
                source.Credentials = PackageSourceCredential.FromUserInput(repositoryUrl, _credential.UserName, password, true, null);
            }

            var provider = FactoryExtensionsV3.GetCoreV3(NuGet.Protocol.Core.Types.Repository.Provider);

            SourceRepository repository = new SourceRepository(source, provider);

            SearchFilter filter = new SearchFilter(_prerelease);
            //SourceCacheContext context = new SourceCacheContext();




            // I think we'll likely need to make a call to find first so that we can ensure we have the right version (if no version is specified)
            // Call Find

            // things we know:  at least one name,  we don't know versions though

            // returnedPkgs

            // IDEA:  use find to find all dependencies...



            //////////////////////  packages from source
            ///
            PackageSource source2 = new PackageSource(repositoryUrl);
            if (_credential != null)
            {
                string password = new NetworkCredential(string.Empty, _credential.Password).Password;
                source2.Credentials = PackageSourceCredential.FromUserInput(repositoryUrl, _credential.UserName, password, true, null);
            }
            var provider2 = FactoryExtensionsV3.GetCoreV3(NuGet.Protocol.Core.Types.Repository.Provider);

            SourceRepository repository2 = new SourceRepository(source2, provider2);
            // TODO:  proper error handling here
            PackageMetadataResource resourceMetadata2 = null;
            try
            {
                resourceMetadata2 = repository.GetResourceAsync<PackageMetadataResource>().GetAwaiter().GetResult();
            }
            catch
            { }

            SearchFilter filter2 = new SearchFilter(_prerelease);
            SourceCacheContext context2 = new SourceCacheContext();



            foreach (var n in _name)
            {
                // returnedPkgs.AddRange(FindPackagesFromSourceHelper(repositoryUrl, n, resourceSearch, resourceMetadata, filter, context));

                //functionality here
                //////////////////    packages from source helper

                IPackageSearchMetadata filteredFoundPkgs = null;

                // Check version first to narrow down the number of pkgs before potential searching through tags


                VersionRange versionRange = null;
                if (_version == null)
                {
                    // ensure that the latst version is returned first (the ordering of versions differ
                    // TODO: proper error handling
                    try
                    {
                        filteredFoundPkgs = (resourceMetadata2.GetMetadataAsync(n, _prerelease, false, context2, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult()
                            .OrderByDescending(p => p.Identity.Version, VersionComparer.VersionRelease)
                            .FirstOrDefault());
                    }
                    catch { }
                }
                else
                {
                    // check if exact version
                    NuGetVersion nugetVersion;

                    //VersionRange versionRange = VersionRange.Parse(version);
                    NuGetVersion.TryParse(_version, out nugetVersion);
                    // throw

                    if (nugetVersion != null)
                    {
                        // exact version
                        versionRange = new VersionRange(nugetVersion, true, nugetVersion, true, null, null);
                    }
                    else
                    {
                        // check if version range
                        versionRange = VersionRange.Parse(_version);
                    }



                    // Search for packages within a version range
                    // ensure that the latst version is returned first (the ordering of versions differ
                    filteredFoundPkgs = (resourceMetadata2.GetMetadataAsync(n, _prerelease, false, context2, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult()
                        .Where(p => versionRange.Satisfies(p.Identity.Version))
                        .OrderByDescending(p => p.Identity.Version, VersionComparer.VersionRelease)
                        .FirstOrDefault());

                }


                List<IPackageSearchMetadata> foundDependencies = new List<IPackageSearchMetadata>();



                // found a package to install and looking for dependencies
                // Search for dependencies
                if (filteredFoundPkgs != null)
                {


                    // need to parse the depenency and version and such

                    // need to improve this later
                    // this function recursively finds all dependencies
                    // might need to do add instead of AddRange
                    foundDependencies.AddRange(FindDependenciesFromSource(filteredFoundPkgs, resourceMetadata2, context2));


                }  /// end dep conditional



                // check which pkgs you actually need to install




                List<IPackageSearchMetadata> pkgsToInstall = new List<IPackageSearchMetadata>();
                // install pkg, then install any dependencies to a temp directory

                pkgsToInstall.Add(filteredFoundPkgs);
                pkgsToInstall.AddRange(foundDependencies);



                /*** NEED TO CHECK IF PKGS ARE ALREADY INSTALLED ***/
                // - we have a list of everything that needs to be installed (dirsToDelete)
                // - we check the system to see if that particular package AND package version is there (PSModulesPath)
                // - if it is, we remove it from the list of pkgs to install

                if (versionRange != null)
                {
                    // for each package name passed in
                    foreach (var name in _name)
                    {
                        var pkgDirName = Path.Combine(psModulesPath, name);
                        var pkgDirNameScript = Path.Combine(psScriptsPath, name);

                        // Check to see if the package dir exists in the path
                        if (psModulesPathAllDirs.Contains(pkgDirName, StringComparer.OrdinalIgnoreCase)
                            || psScriptsPathAllDirs.Contains(pkgDirNameScript, StringComparer.OrdinalIgnoreCase))
                        {


                            // then check to see if the package version exists in the path
                            var pkgDirVersion = (Directory.GetDirectories(pkgDirName)).ToList();
                            // check scripts path too
                            // TODO:  check if script is installed???
                            // pkgDirVersion.AddRange((Directory.GetDirectories(pkgDirNameScript)).ToList());


                            List<string> pkgVersion = new List<string>();
                            foreach (var path in pkgDirVersion)
                            {
                                pkgVersion.Add(Path.GetFileName(path));
                            }



                            // these are all the packages already installed
                            var pkgsAlreadyInstalled = pkgVersion.FindAll(p => versionRange.Satisfies(NuGetVersion.Parse(p)));


                            if (pkgsAlreadyInstalled.Any() && !_reinstall)
                            {
                                // remove the pkg from the list of pkgs that need to be installed
                                var pkgsToRemove = pkgsToInstall.Find(p => string.Equals(p.Identity.Id, name, StringComparison.CurrentCultureIgnoreCase));

                                pkgsToInstall.Remove(pkgsToRemove);
                                pkgsLeftToInstall.Remove(name);
                            }

                        }
                    }
                } // exact version installation
                else // if (versionRange != null)
                {
                    // for each package name passed in
                    foreach (var name in _name)
                    {
                        // case sensitivity issues here!

                        var dirName = Path.Combine(psModulesPath, name);
                        var dirNameScript = Path.Combine(psModulesPath, name);


                        // Check to see if the package dir exists in the path
                        if (psModulesPathAllDirs.Contains(dirName, StringComparer.OrdinalIgnoreCase)
                             || psScriptsPathAllDirs.Contains(dirNameScript, StringComparer.OrdinalIgnoreCase))
                        {
                            // then check to see if the package exists in the path

                            if (Directory.Exists(dirName) || Directory.Exists(dirNameScript) && !_reinstall)
                            {
                                // remove the pkg from the list of pkgs that need to be installed
                                //case sensitivity here 
                                var pkgsToRemove = pkgsToInstall.Find(p => string.Equals(p.Identity.Id, name, StringComparison.CurrentCultureIgnoreCase));

                                pkgsToInstall.Remove(pkgsToRemove);
                                pkgsLeftToInstall.Remove(name);
                                //Directory.Delete(dirNameVersion.ToString(), true);

                            }

                        }
                    }
                }

                var tempInstallPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                var dir = Directory.CreateDirectory(tempInstallPath);  // should check it gets created properly
                //dir.SetAccessControl(new DirectorySecurity(dir.FullName, AccessControlSections.Owner));
                // To delete file attributes from the existing ones get the current file attributes first and use AND (&) operator
                // with a mask (bitwise complement of desired attributes combination).
                dir.Attributes = dir.Attributes & ~FileAttributes.ReadOnly;

                //remove any null pkgs
                pkgsToInstall.Remove(null);

                // install everything to a temp path
                foreach (var p in pkgsToInstall)
                {
                    var pkgIdentity = new PackageIdentity(p.Identity.Id, p.Identity.Version);


                    var resource = new DownloadResourceV2FeedProvider();
                    var resource2 = resource.TryCreate(repository, cancellationToken);

                   
                    var cacheContext = new SourceCacheContext();


                    var downloadResource = repository.GetResourceAsync<DownloadResource>().GetAwaiter().GetResult();


                    var result = downloadResource.GetDownloadResourceResultAsync(
                        pkgIdentity,
                        new PackageDownloadContext(cacheContext),
                        tempInstallPath,
                        logger: NullLogger.Instance,
                        CancellationToken.None).GetAwaiter().GetResult();

                    // need to close the .nupkg
                    result.Dispose();


                    // create a download count to see if everything was installed properly

                    // 1) remove the *.nupkg file

                    // may need to modify due to capitalization
                    var dirNameVersion = Path.Combine(tempInstallPath, p.Identity.Id, p.Identity.Version.ToNormalizedString());
                    var nupkgMetadataToDelete = Path.Combine(dirNameVersion, ".nupkg.metadata");
                    var nupkgToDelete = Path.Combine(dirNameVersion, (p.Identity.ToString() + ".nupkg").ToLower());
                    var nupkgSHAToDelete = Path.Combine(dirNameVersion, (p.Identity.ToString() + ".nupkg.sha512").ToLower());
                    var nuspecToDelete = Path.Combine(dirNameVersion, (p.Identity.Id + ".nuspec").ToLower());


                    File.Delete(nupkgMetadataToDelete);
                    File.Delete(nupkgSHAToDelete);
                    File.Delete(nuspecToDelete);
                    File.Delete(nupkgToDelete);


                    // if it's not a script, do the following:
                    var scriptPath = Path.Combine(dirNameVersion, (p.Identity.Id.ToString() + ".ps1").ToLower());
                    var isScript = File.Exists(scriptPath) ? true : false;

                    // 3) create xml
                    //Create PSGetModuleInfo.xml
                    //Set attribute as hidden [System.IO.File]::SetAttributes($psgetItemInfopath, [System.IO.FileAttributes]::Hidden)

                    
                    
                    var fullinstallPath = isScript ? Path.Combine(dirNameVersion, (p.Identity.Id + "_InstalledScriptInfo.xml"))
                        : Path.Combine(dirNameVersion, "PSGetModuleInfo.xml");

                      
                    // Create XMLs
                    using (StreamWriter sw = new StreamWriter(fullinstallPath))
                    {
                        var psModule = "PSModule";

                        var tags = p.Tags.Split(' ');


                        var module = tags.Contains("PSModule") ? "Module" : null;
                        var script = tags.Contains("PSScript") ? "Script" : null;


                        List<string> includesDscResource = new List<string>();
                        List<string> includesCommand = new List<string>();
                        List<string> includesFunction = new List<string>();
                        List<string> includesRoleCapability = new List<string>();
                        List<string> filteredTags = new List<string>();

                        var psDscResource = "PSDscResource_";
                        var psCommand = "PSCommand_";
                        var psFunction = "PSFunction_";
                        var psRoleCapability = "PSRoleCapability_";



                        foreach (var tag in tags)
                        {
                            if (tag.StartsWith(psDscResource))
                            {
                                includesDscResource.Add(tag.Remove(0, psDscResource.Length));
                            }
                            else if (tag.StartsWith(psCommand))
                            {
                                includesCommand.Add(tag.Remove(0, psCommand.Length));
                            }
                            else if (tag.StartsWith(psFunction))
                            {
                                includesFunction.Add(tag.Remove(0, psFunction.Length));
                            }
                            else if (tag.StartsWith(psRoleCapability))
                            {
                                includesRoleCapability.Add(tag.Remove(0, psRoleCapability.Length));
                            }
                            else if (!tag.StartsWith("PSWorkflow_") && !tag.StartsWith("PSCmdlet_") && !tag.StartsWith("PSIncludes_")
                                && !tag.Equals("PSModule") && !tag.Equals("PSScript"))
                            {
                                filteredTags.Add(tag);
                            }
                        }

                        Dictionary<string, List<string>> includes = new Dictionary<string, List<string>>() {
                            { "DscResource", includesDscResource },
                            { "Command", includesCommand },
                            { "Function", includesFunction },
                            { "RoleCapability", includesRoleCapability }
                        };


                        Dictionary<string, VersionRange> dependencies = new Dictionary<string, VersionRange>();
                        foreach (var depGroup in p.DependencySets)
                        {
                            PackageDependency depPkg = depGroup.Packages.FirstOrDefault();
                            dependencies.Add(depPkg.Id, depPkg.VersionRange);
                        }


                        var psGetModuleInfoObj = new PSObject();
                        // TODO:  Add release notes
                        psGetModuleInfoObj.Members.Add(new PSNoteProperty("Name", p.Identity.Id));
                        psGetModuleInfoObj.Members.Add(new PSNoteProperty("Version", p.Identity.Version));
                        psGetModuleInfoObj.Members.Add(new PSNoteProperty("Type", module != null ? module : (script != null ? script : null)));
                        psGetModuleInfoObj.Members.Add(new PSNoteProperty("Description", p.Description));
                        psGetModuleInfoObj.Members.Add(new PSNoteProperty("Author", p.Authors));
                        psGetModuleInfoObj.Members.Add(new PSNoteProperty("CompanyName", p.Owners));
                        psGetModuleInfoObj.Members.Add(new PSNoteProperty("PublishedDate", p.Published));
                        psGetModuleInfoObj.Members.Add(new PSNoteProperty("InstalledDate", System.DateTime.Now));
                        psGetModuleInfoObj.Members.Add(new PSNoteProperty("LicenseUri", p.LicenseUrl));
                        psGetModuleInfoObj.Members.Add(new PSNoteProperty("ProjectUri", p.ProjectUrl));
                        psGetModuleInfoObj.Members.Add(new PSNoteProperty("IconUri", p.IconUrl));
                        psGetModuleInfoObj.Members.Add(new PSNoteProperty("Includes", includes.ToList()));    // TODO: check if getting deserialized properly
                        psGetModuleInfoObj.Members.Add(new PSNoteProperty("PowerShellGetFormatVersion", "3"));
                        psGetModuleInfoObj.Members.Add(new PSNoteProperty("Dependencies", dependencies.ToList()));
                        psGetModuleInfoObj.Members.Add(new PSNoteProperty("RepositorySourceLocation", repositoryUrl));
                        psGetModuleInfoObj.Members.Add(new PSNoteProperty("Repository", repositoryUrl));
                        psGetModuleInfoObj.Members.Add(new PSNoteProperty("InstalledLocation", null));         // TODO:  add installation location



                        psGetModuleInfoObj.TypeNames.Add("Microsoft.PowerShell.Commands.PSRepositoryItemInfo");


                        var serializedObj = PSSerializer.Serialize(psGetModuleInfoObj);


                        sw.Write(serializedObj);

                        // set the xml attribute to hidden
                        //System.IO.File.SetAttributes("c:\\code\\temp\\installtestpath\\PSGetModuleInfo.xml", FileAttributes.Hidden);
                    }
                    


                    // 4) copy to proper path

                    // TODO: test installing a script when it already exists
                    // or move to script path
                    // check for failures
                    // var newPath = Directory.CreateDirectory(Path.Combine(psModulesPath, p.Identity.Id, p.Identity.Version.ToNormalizedString()));

                    var installPath = isScript ? psScriptsPath : psModulesPath;
                    var newPath = isScript ? installPath
                        : Path.Combine(installPath, p.Identity.Id.ToString());
                    // when we move the directory over, we'll change the casing of the module directory name from lower case to proper casing.
                    
                    // if script, just move the files over, if module, move the version directory overp
                    var tempModuleVersionDir = isScript ? Path.Combine(tempInstallPath, p.Identity.Id.ToLower(), p.Identity.Version.ToNormalizedString())
                        : Path.Combine(tempInstallPath, p.Identity.Id.ToLower());

                    if (isScript)
                    {
                        var scriptXML = p.Identity.Id + "_InstalledScriptInfo.xml";
                        File.Move(Path.Combine(tempModuleVersionDir, scriptXML), Path.Combine(psScriptsPath, "InstalledScriptInfos", scriptXML));
                        File.Move(Path.Combine(tempModuleVersionDir, p.Identity.Id.ToLower() + ".ps1"), Path.Combine(newPath, p.Identity.Id + ".ps1"));
                    }
                    else
                    {
                        if (!Directory.Exists(newPath))
                        {                                   
                            Directory.Move(tempModuleVersionDir, newPath);
                        }
                        else
                        {
                            // If the module directory path already exists, Directory.Move throws an exception, so we'll just move the version directory over instead 
                            tempModuleVersionDir = Path.Combine(tempModuleVersionDir, p.Identity.Version.ToNormalizedString());
                            Directory.Move(tempModuleVersionDir, Path.Combine(newPath, p.Identity.Version.ToNormalizedString()));
                        }
                    }


                    // 2) TODO: Verify that all the proper modules installed correctly 
                    // remove temp directory recursively
                    Directory.Delete(tempInstallPath, true);

                    pkgsLeftToInstall.Remove(n);

                }


            }
            ////////////////////////////////////////








            return pkgsLeftToInstall;

        }



        private List<IPackageSearchMetadata> FindDependenciesFromSource(IPackageSearchMetadata pkg, PackageMetadataResource pkgMetadataResource, SourceCacheContext srcContext)
        {
            /// dependency resolver
            ///
            /// this function will be recursively called
            ///
            /// call the findpackages from source helper (potentially generalize this so it's finding packages from source or cache)
            ///
            List<IPackageSearchMetadata> foundDependencies = new List<IPackageSearchMetadata>();

            // 1)  check the dependencies of this pkg
            // 2) for each dependency group, search for the appropriate name and version
            // a dependency group are all the dependencies for a particular framework
            foreach (var dependencyGroup in pkg.DependencySets)
            {

                //dependencyGroup.TargetFramework
                //dependencyGroup.

                foreach (var pkgDependency in dependencyGroup.Packages)
                {

                    // 2.1) check that the appropriate pkg dependencies exist
                    // returns all versions from a single package id.
                    var dependencies = pkgMetadataResource.GetMetadataAsync(pkgDependency.Id, _prerelease, true, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();

                    // then 2.2) check if the appropriate verion range exists  (if version exists, then add it to the list to return)

                    VersionRange versionRange = null;
                    try
                    {
                        versionRange = VersionRange.Parse(pkgDependency.VersionRange.OriginalString);
                    }
                    catch
                    {
                        Console.WriteLine("Error parsing version range");
                    }



                    // if no version/version range is specified the we just return the latest version

                    IPackageSearchMetadata depPkgToReturn = (versionRange == null ?
                        dependencies.FirstOrDefault() :
                        dependencies.Where(v => versionRange.Satisfies(v.Identity.Version)).FirstOrDefault());




                    // if the pkg already exists on the system, don't add it to the list of pkgs that need to be installed 
                    var dirName = Path.Combine(psModulesPath, pkgDependency.Id);

                    var dependencyAlreadyInstalled = false;
                    
                    // Check to see if the package dir exists in the path
                    if (psModulesPathAllDirs.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                    {
                        // then check to see if the package exists in the path

                        if (Directory.Exists(dirName))
                        {
                            // then check to see if the package version exists in the path
                            var pkgDirVersion = (Directory.GetDirectories(dirName)).ToList();
                            List<string> pkgVersion = new List<string>();
                            foreach (var path in pkgDirVersion)
                            {
                                pkgVersion.Add(Path.GetFileName(path));
                            }


                            // these are all the packages already installed
                            var pkgsAlreadyInstalled = pkgVersion.FindAll(p => versionRange.Satisfies(NuGetVersion.Parse(p)));  // dont think ive tested this path

                            if (pkgsAlreadyInstalled.Any() && !_reinstall)
                            {
                                // don't add the pkg to the list of pkgs that need to be installed
                                dependencyAlreadyInstalled = true;
                            }
                        }

                    }

                    if (!dependencyAlreadyInstalled)
                    {
                        foundDependencies.Add(depPkgToReturn);
                    }

                    // 3) search for any dependencies the pkg has
                    foundDependencies.AddRange(FindDependenciesFromSource(depPkgToReturn, pkgMetadataResource, srcContext));
                }
            }




            // flatten after returning
            return foundDependencies;
        }












    }
}
