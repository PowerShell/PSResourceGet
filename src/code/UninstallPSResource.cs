
using System;
using System.Management.Automation;
using System.Threading;
using NuGet.Versioning;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Collections.ObjectModel;
using static System.Environment;
using System.Security.Principal;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{


    /// <summary>
    /// Uninstall 
    /// </summary>

    [Cmdlet(VerbsLifecycle.Uninstall, "PSResource", DefaultParameterSetName = "NameParameterSet", SupportsShouldProcess = true,
    HelpUri = "<add>", RemotingCapability = RemotingCapability.None)]
    public sealed
    class UninstallPSResource : PSCmdlet
    {
        //  private string PSGalleryRepoName = "PSGallery";

        /// <summary>
        /// Specifies the exact names of resources to install from a repository.
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


        /// <summary>
        /// Specifies the version or version range of the package to be installed
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

      
        /// <summary>
        /// Specifies to allow ONLY prerelease versions to be uninstalled
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        public SwitchParameter PrereleaseOnly
        {
            get
            { return _prereleaseOnly; }

            set
            { _prereleaseOnly = value; }
        }
        private SwitchParameter _prereleaseOnly;
       

        /// <summary>
        /// Overrides warning messages about resource installation conflicts.
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

        public static readonly string OsPlatform = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        private string programFilesPath;
        private string myDocumentsPath;
        List<string> dirsToDelete;

        private CancellationTokenSource source;
        private CancellationToken cancellationToken;

        NuGetVersion nugetVersion;
        VersionRange versionRange;


        /// <summary>
        /// </summary>
        protected override void ProcessRecord()
        {
            source = new CancellationTokenSource();
            cancellationToken = source.Token;



            NuGetVersion.TryParse(_version, out nugetVersion);


            if (nugetVersion == null)
            {
                VersionRange.TryParse(_version, out versionRange); 
            }

            var consoleIsElevated = false;

#if NET472
            // WindowsPS
            var id = System.Security.Principal.WindowsIdentity.GetCurrent();
            consoleIsElevated = (id.Owner != id.User);
            myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.MyDocuments), "WindowsPowerShell");
            programFilesPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.ProgramFiles), "WindowsPowerShell");
#else
            // If Windows OS (PS6+)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var id = System.Security.Principal.WindowsIdentity.GetCurrent();
                consoleIsElevated = (id.Owner != id.User);

                myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.MyDocuments), "PowerShell");
                programFilesPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.ProgramFiles), "PowerShell");
            }
            else
            {
                // Paths are the same for both Linux and MacOS
                myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "Powershell");
                programFilesPath = Path.Combine("usr", "local", "share", "Powershell");

                using (System.Management.Automation.PowerShell pwsh = System.Management.Automation.PowerShell.Create())
                {
                    var results = pwsh.AddCommand("id").AddParameter("u").Invoke();
                }
            }
#endif


            foreach (var pkgName in _name)
            {
                var successfullyUninstalledPkg = UninstallPkgHelper(pkgName, cancellationToken);
                if (successfullyUninstalledPkg)
                {
                    Console.WriteLine("Successfully uninstalled {0}", pkgName);
                }
                else
                {
                    Console.WriteLine("Did not successfully uninstall {0}", pkgName);
                }
            }
        }



        /// just uninstall module, not dependencies
        private bool UninstallPkgHelper(string pkgName, CancellationToken cancellationToken)
        {
            var successfullyUninstalled = false;

            dirsToDelete = new List<string>();

            if (String.IsNullOrWhiteSpace(pkgName))
            {
                return successfullyUninstalled;
            }

            var psModulesPathMyDocuments = Path.Combine(myDocumentsPath, "Modules");
            var psModulesPathProgramFiles = Path.Combine(programFilesPath, "Modules");

            var psScriptPathMyDocuments = Path.Combine(myDocumentsPath, "Scripts");
            var psScriptsPathProgramFiles = Path.Combine(programFilesPath, "Scripts");


            /* Modules */
            // My Documents
            var dirNameMyDocuments = Path.Combine(psModulesPathMyDocuments, pkgName);
            var versionDirsMyDocuments = (Directory.Exists(dirNameMyDocuments)) ? Directory.GetDirectories(dirNameMyDocuments) : null;
            var parentDirFilesMyDocuments = (Directory.Exists(dirNameMyDocuments)) ? Directory.GetFiles(dirNameMyDocuments) : null;
            // Program Files
            var dirNameProgramFiles = Path.Combine(psModulesPathProgramFiles, pkgName);
            var versionDirsProgramFiles = (Directory.Exists(dirNameProgramFiles)) ? Directory.GetDirectories(dirNameProgramFiles) : null;
            var parentDirFilesProgramFiles = (Directory.Exists(dirNameProgramFiles)) ? Directory.GetFiles(dirNameProgramFiles) : null;




            /* Scripts */
            // My Documents
            var scriptPathMyDocuments = Path.Combine(psScriptPathMyDocuments, pkgName + ".ps1");
            // Program Files
            var scriptPathProgramFiles = Path.Combine(psScriptsPathProgramFiles, pkgName + ".ps1");


            var psModulesPathAllDirs = new List<string>();
            if (Directory.Exists(psModulesPathMyDocuments))
            {
                psModulesPathAllDirs.AddRange(Directory.GetDirectories(psModulesPathMyDocuments).ToList());
            }
            if (Directory.Exists(psModulesPathProgramFiles))
            {
                psModulesPathAllDirs.AddRange(Directory.GetDirectories(psModulesPathProgramFiles).ToList());
            }

            var psScriptsPathAllFiles = new List<string>();
            if (Directory.Exists(psScriptPathMyDocuments))
            {
                psScriptsPathAllFiles.AddRange(Directory.GetFiles(psScriptPathMyDocuments).ToList());  /// may need to change this to get files
            }
            if (Directory.Exists(psModulesPathMyDocuments))
            {
                psScriptsPathAllFiles.AddRange(Directory.GetFiles(psModulesPathMyDocuments).ToList());
            }


            var foundInMyDocuments = (Directory.Exists(dirNameMyDocuments) && (versionDirsMyDocuments.Any() || parentDirFilesMyDocuments.Any())) || File.Exists(scriptPathMyDocuments); // check for scripts
            var foundInProgramFiles = (Directory.Exists(dirNameProgramFiles) && (versionDirsProgramFiles.Any() || parentDirFilesProgramFiles.Any())) || File.Exists(scriptPathProgramFiles);

            // First check if module or script is installed by looking in the specified modules path and scripts path
            var foundResourceObj = foundInMyDocuments || foundInProgramFiles || File.Exists(scriptPathMyDocuments) 
                                    || File.Exists(scriptPathProgramFiles) ? true : false;
            

            var isScript = (File.Exists(scriptPathMyDocuments) || File.Exists(scriptPathProgramFiles)) ? true : false;


            // If we can't find the resource, just return
            if (!foundResourceObj)
            {
                return successfullyUninstalled;
            }


            if (!isScript)
            {
                // Try removing from my documents
                if (foundInMyDocuments)
                {
                    successfullyUninstalled = UninstallModuleHelper(pkgName, dirNameMyDocuments, versionDirsMyDocuments, parentDirFilesMyDocuments, cancellationToken);
                }
                else if (foundInProgramFiles)
                {
                    // try removing from program files
                    successfullyUninstalled = UninstallModuleHelper(pkgName, dirNameProgramFiles, versionDirsProgramFiles, parentDirFilesProgramFiles, cancellationToken);
                }
            }
            else 
            {
                // Try removing from my documents
                if (foundInMyDocuments)
                {
                    successfullyUninstalled = UninstallScriptHelper(pkgName, psScriptPathMyDocuments, scriptPathMyDocuments, cancellationToken);
                }
                else if (foundInProgramFiles)
                {
                    // try removing from program files
                    successfullyUninstalled = UninstallScriptHelper(pkgName, psScriptsPathProgramFiles, scriptPathProgramFiles, cancellationToken);
                }
            }




            return successfullyUninstalled;

        }







        /* Uninstall Module */
        private bool UninstallModuleHelper(string pkgName, string dirName, string[] versionDirs, string[] parentDirFiles, CancellationToken cancellationToken)
        {
            var successfullyUninstalledPkg = false;
            

            // If prereleaseOnly is specified, we'll only take into account prerelease versions of pkgs
            if (_prereleaseOnly)
            {
                List<string> prereleaseOnlyVersionDirs = new List<string>();
                foreach (var dir in versionDirs)
                {
                    var nameOfDir = Path.GetFileName(dir);
                    var nugVersion = NuGetVersion.Parse(nameOfDir);

                    if (nugVersion.IsPrerelease)
                    {
                        prereleaseOnlyVersionDirs.Add(dir);
                    }
                }
                versionDirs = prereleaseOnlyVersionDirs.ToArray();
            }


            // if the version specificed is a version range
            if (versionRange != null)
            {

                foreach (var versionDirPath in versionDirs)
                {
                    var nameOfDir = Path.GetFileName(versionDirPath);
                    var nugVersion = NuGetVersion.Parse(nameOfDir);

                    if (versionRange.Satisfies(nugVersion))
                    {
                        dirsToDelete.Add(versionDirPath);
                    }
                }
            }
            else if (nugetVersion != null)
            {
                // if the version specified is a version

                dirsToDelete.Add(nugetVersion.ToNormalizedString());
            }
            else
            {
                // if no version is specified, just delete the latest version
                Array.Sort(versionDirs);

                dirsToDelete.Add(versionDirs[versionDirs.Length - 1]);
            }




            // if dirsToDelete is empty... meaning we didn't find any modules, it's possible it's a script
            if (dirsToDelete.Any())
            {
                /// This is a primitive implementation
                /// TODO:  implement a dependencies database for querying dependency info
                /// Cannot uninstall a module if another module is dependent on it 

                using (System.Management.Automation.PowerShell pwsh = System.Management.Automation.PowerShell.Create())
                {
                    // Check all modules for dependencies
                    var results = pwsh.AddCommand("Get-Module").AddParameter("ListAvailable").Invoke();

                    // Structure of LINQ call:
                    // Results is a collection of PSModuleInfo objects that contain a property listing module dependencies, "RequiredModules".
                    // RequiredModules is collection of PSModuleInfo objects that need to be iterated through to see if any of them are the pkg we're trying to uninstall
                    // If we anything from the final call gets returned, there is a dependency on this pkg.
                    var pkgsWithRequiredModules = results.Where(p => ((ReadOnlyCollection<PSModuleInfo>)p.Properties["RequiredModules"].Value).Where(rm => rm.Name.Equals(pkgName, StringComparison.InvariantCultureIgnoreCase)).Any());


                    //.Select(p => (p.Properties.Match("Name"), p.Properties.Match("Version")));

                    if (pkgsWithRequiredModules.Any())
                    {
                        var uniquePkgNames = pkgsWithRequiredModules.Select(p => p.Properties["Name"].Value).Distinct().ToArray();

                        var strUniquePkgNames = string.Join(",", uniquePkgNames);

                        throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Cannot uninstall {0}, the following package(s) take a dependency on this package: {1}", pkgName, strUniquePkgNames));

                    }
                }


                // Delete the appropriate directories
                foreach (var dirVersion in dirsToDelete)
                {
                    var dirNameVersion = Path.Combine(dirName, dirVersion);

                    // we know it's installed because it has an xml
                    if (Directory.Exists(dirNameVersion))
                    {
                        var dir = new DirectoryInfo(dirNameVersion.ToString());
                        dir.Attributes = dir.Attributes & ~FileAttributes.ReadOnly;
                        // Delete recursively
                        dir.Delete(true);
                        successfullyUninstalledPkg = true;
                    }
                }



                // Finally:
                // Check to see if there's anything left in the parent directory, if not, delete that as well
                if (Directory.GetDirectories(dirName).Length == 0)
                {
                    Directory.Delete(dirName, true);
                }

            }


          

            return successfullyUninstalledPkg;
        }






        /* Uninstall script helper */
        private bool UninstallScriptHelper(string pkgName, string scriptsPath, string fullScriptPath, CancellationToken cancellationToken)
        {
            /* Currently the way PSGet operates is that only one script can be installed at a time.
             * I think it's worth seeing if we want allow for multiple scripts to be instlled at a time,
             * and if so, we need to rethink the architecture of the scripts installation path. */
             
            var successfullyUninstalledPkg = false;

            // TODO:  open xml and read from it 
            var xmlFileName = string.Concat(pkgName, "_InstalledScriptInfo.xml");
            var scriptXMLPath = Path.Combine(scriptsPath, "InstalledScriptInfos", xmlFileName);

            ReadOnlyPSMemberInfoCollection<PSPropertyInfo> versionInfo;
            NuGetVersion nugetVersion;
            using (StreamReader sr = new StreamReader(scriptXMLPath))
            {

                string text = sr.ReadToEnd();
                var deserializedObj = (PSObject)PSSerializer.Deserialize(text);

                versionInfo = deserializedObj.Properties.Match("Version");
            };

            
            NuGetVersion.TryParse(versionInfo.FirstOrDefault().Value.ToString(), out nugetVersion);



            // If prereleaseOnly is specified, we'll only take into account prerelease versions of pkgs
            if (_prereleaseOnly)
            {
                // If the installed script is a prerelease, we can continue processing it
                if (nugetVersion.IsPrerelease)
                {
                    dirsToDelete.Add(fullScriptPath);
                }
                else
                {
                    return successfullyUninstalledPkg;
                }
            }


            if (_version == null)
            {
                // if no version is specified, just delete the latest version (right now the only version)
                dirsToDelete.Add(fullScriptPath);
            }
            // if the version specificed is a version range
            else 
            {
                // Parse the version passed in and compare it to the script version
                NuGetVersion argNugetVersion;
                NuGetVersion.TryParse(_version, out argNugetVersion);

                VersionRange versionRange;
                if (argNugetVersion != null)
                {
                    // exact version
                    versionRange = new VersionRange(argNugetVersion, true, argNugetVersion, true, null, null);
                }
                else
                {
                    // check if version range
                    versionRange = VersionRange.Parse(_version);
                }



                if (versionRange.Satisfies(nugetVersion))
                {
                    dirsToDelete.Add(fullScriptPath);
                }
            }




            // if dirsToDelete is empty... meaning we didn't find any scripts
            if (dirsToDelete.Any())
            {
                /// This is a primitive implementation
                /// TODO:  implement a dependencies database for querying dependency info
                /// Cannot uninstall a package if another module is dependent on it 

                using (System.Management.Automation.PowerShell pwsh = System.Management.Automation.PowerShell.Create())
                {
                    // Check all modules for dependencies
                    var results = pwsh.AddCommand("Get-Module").AddParameter("ListAvailable").Invoke();

                    // Structure of LINQ call:
                    // Results is a collection of PSModuleInfo objects that contain a property listing module dependencies, "RequiredModules".
                    // RequiredModules is collection of PSModuleInfo objects that need to be iterated through to see if any of them are the pkg we're trying to uninstall
                    // If we anything from the final call gets returned, there is a dependency on this pkg.

                    // check for nested modules as well
                    var pkgsWithRequiredModules = results.Where(p => ((ReadOnlyCollection<PSModuleInfo>)p.Properties["RequiredModules"].Value).Where(rm => rm.Name.Equals(pkgName, StringComparison.InvariantCultureIgnoreCase)).Any());

                    if (pkgsWithRequiredModules.Any())
                    {
                        var uniquePkgNames = pkgsWithRequiredModules.Select(p => p.Properties["Name"].Value).Distinct().ToArray();

                        var strUniquePkgNames = string.Join(",", uniquePkgNames);

                        throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Cannot uninstall {0}, the following package(s) take a dependency on this package: {1}", pkgName, strUniquePkgNames));

                    }
                }






                // Delete the appropriate file
                if (File.Exists(fullScriptPath))
                {
                    File.Delete(fullScriptPath);
                    successfullyUninstalledPkg = true;
                }

                // Finally:
                // Delete the xml from the InstalledModulesInfo directory
                var scriptXML = Path.Combine(scriptsPath, "InstalledScriptInfos", pkgName + "_InstalledScriptInfo.xml");
                if (File.Exists(scriptXML))
                {
                    File.Delete(scriptXML);
                }


            }



            return successfullyUninstalledPkg;
        }


    }
}
