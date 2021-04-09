using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using static System.Environment;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Threading;
using NuGet.Versioning;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>   
    /// Uninstall-PSResource uninstalls a package found in a module or script installation path.
    /// </summary>

        [Cmdlet(VerbsLifecycle.Uninstall, "PSResource", DefaultParameterSetName = "NameParameterSet", SupportsShouldProcess = true,
    HelpUri = "<add>", RemotingCapability = RemotingCapability.None)]
    public sealed
    class UninstallPSResource : PSCmdlet
    {
        /// <summary>
        /// Specifies the exact names of resources to uninstall.
        /// A comma-separated list of module names is accepted. The resource name must match the resource name in the repository.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; }
        
        /// <summary>
        /// Specifies the version or version range of the package to be uninstalled.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }
        
        /// <summary>
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        public SwitchParameter Force { get; set; }


        public static readonly string OsPlatform = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        private string _programFilesPath;
        private string _myDocumentsPath;
        private CancellationTokenSource _source;
        private CancellationToken _cancellationToken;

        NuGetVersion _nugetVersion;
        VersionRange _versionRange;

        protected override void BeginProcessing()
        {
            _source = new CancellationTokenSource();
            _cancellationToken = _source.Token;

            Utils.TryParseVersionOrVersionRange(Version, out NuGetVersion _nugetVersion, out VersionRange _versionRange, this);
           
            var PSVersion6 = new Version(6, 0);
            var isCorePS = Host.Version >= PSVersion6;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // If PowerShell 6+
                if (isCorePS)
                {
                    _myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.MyDocuments), "PowerShell");
                    _programFilesPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.ProgramFiles), "PowerShell");
                }
                else
                {
                    _myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.MyDocuments), "WindowsPowerShell");
                    _programFilesPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.ProgramFiles), "WindowsPowerShell");
                }
            }
            else
            {
                // Paths are the same for both Linux and MacOS
                _myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "Powershell");
                _programFilesPath = Path.Combine("usr", "local", "share", "Powershell");
            }
        }



        protected override void ProcessRecord()
        {
            foreach (var pkgName in Name)
            {
                if (!UninstallPkgHelper(pkgName))
                { 
                    WriteVerbose("Did not successfully uninstall " + pkgName);
                }
            }
        }



        // Uninstalls a single resource
        private bool UninstallPkgHelper(string pkgName)
        {
            var successfullyUninstalled = false;

            List<string> dirsToDelete = new List<string>();

            if (String.IsNullOrWhiteSpace(pkgName))
            {
                return successfullyUninstalled;
            }

            var psModulesPathMyDocuments = Path.Combine(_myDocumentsPath, "Modules");
            var psModulesPathProgramFiles = Path.Combine(_programFilesPath, "Modules");

            var psScriptPathMyDocuments = Path.Combine(_myDocumentsPath, "Scripts");
            var psScriptsPathProgramFiles = Path.Combine(_programFilesPath, "Scripts");


            /* Modules */
            // My Documents
            var dirNameMyDocuments = Path.Combine(psModulesPathMyDocuments, pkgName);
            string[] versionDirsMyDocuments = null;
            /// modules can either be TestModule/m
            if (Directory.Exists(dirNameMyDocuments))
            {
                // returns all the version directories
                // eg:  TestModule/0.0.1
                versionDirsMyDocuments = Directory.GetDirectories(dirNameMyDocuments);
            }
            // Program Files
            var dirNameProgramFiles = Path.Combine(psModulesPathProgramFiles, pkgName);
            string[] versionDirsProgramFiles = null;
            if (Directory.Exists(dirNameProgramFiles))
            {
                versionDirsProgramFiles = Directory.GetDirectories(dirNameProgramFiles);
            }

            /* Scripts */
            // My Documents
            var scriptPathMyDocuments = Path.Combine(psScriptPathMyDocuments, pkgName + ".ps1");
            // Program Files
            var scriptPathProgramFiles = Path.Combine(psScriptsPathProgramFiles, pkgName + ".ps1");
            
            var psModulesPathAllDirs = new List<string>();
            bool foundInMyDocuments = false;
            bool foundInProgramFiles = false;
            bool isScript = false;
            if (Directory.Exists(psModulesPathMyDocuments))
            {
                psModulesPathAllDirs.AddRange(Directory.GetDirectories(psModulesPathMyDocuments).ToList());

                // First check if module or script is installed by looking in the specified modules path and scripts path
                foundInMyDocuments = versionDirsMyDocuments.Any();
                if (File.Exists(scriptPathMyDocuments))
                {
                    isScript = true;
                    foundInMyDocuments = true;
                }
            }
            if (Directory.Exists(psModulesPathProgramFiles))
            {
                psModulesPathAllDirs.AddRange(Directory.GetDirectories(psModulesPathProgramFiles).ToList());
                foundInProgramFiles = versionDirsProgramFiles.Any();
                if (File.Exists(scriptPathProgramFiles))
                {
                    isScript = true;
                    foundInProgramFiles = true;
                }
            }
            bool foundResourceObj = foundInMyDocuments || foundInProgramFiles;

            // If we can't find the resource, write non-terminating error and return
            if (!foundResourceObj)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(string.Format("Could not find the resource '{0}'", pkgName)),
                    "ErrorRetrievingSpecifiedResource",
                    ErrorCategory.ObjectNotFound,
                    this));

                return successfullyUninstalled;
            }

            
            if (!isScript)
            {
                if (foundInMyDocuments)
                {
                    successfullyUninstalled = UninstallModuleHelper(pkgName, dirNameMyDocuments, versionDirsMyDocuments, dirsToDelete);
                }
                else if (foundInProgramFiles)
                {
                    successfullyUninstalled = UninstallModuleHelper(pkgName, dirNameProgramFiles, versionDirsProgramFiles, dirsToDelete);
                }
            }
            else 
            {
                if (foundInMyDocuments)
                {
                    successfullyUninstalled = UninstallScriptHelper(pkgName, psScriptPathMyDocuments, scriptPathMyDocuments, dirsToDelete);
                }
                else if (foundInProgramFiles)
                {
                    successfullyUninstalled = UninstallScriptHelper(pkgName, psScriptsPathProgramFiles, scriptPathProgramFiles, dirsToDelete);
                }
            }
            
            return successfullyUninstalled;
        }
        

        /* Uninstall Module */
        private bool UninstallModuleHelper(string pkgName, string dirName, string[] versionDirs, List<string> dirsToDelete)
        {
            var successfullyUninstalledPkg = false;

            // if the version specificed is a version range
            if (_versionRange != null)
            {

                foreach (var versionDirPath in versionDirs)
                {
                    var nameOfDir = Path.GetFileName(versionDirPath);
                    var nugVersion = NuGetVersion.Parse(nameOfDir);

                    if (_versionRange.Satisfies(nugVersion))
                    {
                        dirsToDelete.Add(versionDirPath);
                    }
                }
            }
            else if (_nugetVersion != null)
            {
                // if the version specified is a version
                dirsToDelete.Add(_nugetVersion.ToNormalizedString());
            }
            else
            {
                // if no version is specified, just delete the latest version
                Array.Sort(versionDirs);

                dirsToDelete.Add(versionDirs[versionDirs.Length - 1]);
            }
            
            // if dirsToDelete is empty, meaning we didn't find any modules, it's possible it's a script
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

                    if (pkgsWithRequiredModules.Any() && !Force)
                    {
                        var uniquePkgNames = pkgsWithRequiredModules.Select(p => p.Properties["Name"].Value).Distinct().ToArray();
                        var strUniquePkgNames = string.Join(",", uniquePkgNames);

                        throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Cannot uninstall {0}, the following package(s) take a dependency on this package: {1}.  If you would still like to uninstall, rerun the command with -Force", pkgName, strUniquePkgNames));
                    }
                }


                if (ShouldProcess("Uninstall-PSResource"))
                {
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

                            WriteVerbose(string.Format("Successfully uninstalled '{0}' from path '{1}'", pkgName, dir.FullName));


                            successfullyUninstalledPkg = true;
                        }
                    }

                    // Finally: Check to see if there's anything left in the parent directory, if not, delete that as well
                    if (Directory.GetDirectories(dirName).Length == 0)
                    {
                        Directory.Delete(dirName, true);
                    }
                }
            }
            return successfullyUninstalledPkg;
        }
        

        /* Uninstall script helper */
        private bool UninstallScriptHelper(string pkgName, string scriptsPath, string fullScriptPath, List<string> dirsToDelete)
        {
            /* Currently the way PSGet operates is that only one script can be installed at a time.
             * I think it's worth seeing if we want allow for multiple scripts to be instlled at a time,
             * and if so, we need to rethink the architecture of the scripts installation path. */
             
            var successfullyUninstalledPkg = false;

            // TODO:  open xml and read from it 
            var xmlFileName = string.Concat(pkgName, "_InstalledScriptInfo.xml");
            var scriptXMLPath = Path.Combine(scriptsPath, "InstalledScriptInfos", xmlFileName);

            ReadOnlyPSMemberInfoCollection<PSPropertyInfo> versionInfo;
            using (StreamReader sr = new StreamReader(scriptXMLPath))
            {

                string text = sr.ReadToEnd();
                var deserializedObj = (PSObject)PSSerializer.Deserialize(text);

                versionInfo = deserializedObj.Properties.Match("Version");
            };
            
            NuGetVersion.TryParse(versionInfo.FirstOrDefault().Value.ToString(), out NuGetVersion nugetVersion);

            if (Version == null)
            {
                // if no version is specified, just delete the latest version (right now the only version)
                dirsToDelete.Add(fullScriptPath);
            }
            // if the version specificed is a version range
            else 
            {
                // Parse the version passed in and compare it to the script version
                NuGetVersion argNugetVersion;
                NuGetVersion.TryParse(Version, out argNugetVersion);

                VersionRange versionRange;
                if (argNugetVersion != null)
                {
                    // exact version
                    versionRange = new VersionRange(argNugetVersion, true, argNugetVersion, true, null, null);
                }
                else
                {
                    // check if version range
                    versionRange = VersionRange.Parse(Version);
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

                // Finally: Delete the xml from the InstalledModulesInfo directory
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
