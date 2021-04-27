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
    [Cmdlet(VerbsLifecycle.Uninstall, "PSResource", DefaultParameterSetName = NameParameterSet, SupportsShouldProcess = true, HelpUri = "<add>")]
    public sealed class UninstallPSResource : PSCmdlet
    {
        #region Parameters
        /// <summary>
        /// Specifies the exact names of resources to uninstall.
        /// A comma-separated list of module names is accepted. The resource name must match the resource name in the repository.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; }
        
        /// <summary>
        /// Specifies the version or version range of the package to be uninstalled.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }

        /// <summary>
        /// Used for pipeline input.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = InputObjectSet)]
        [ValidateNotNullOrEmpty]
        public PSResourceInfo[] InputObject { get; set; }

        /// <summary>
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter Force { get; set; }
        #endregion

        #region Members
        private const string NameParameterSet = "NameParameterSet";
        private const string InputObjectSet = "InputObjectSet";
        public static readonly string OsPlatform = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        private CancellationTokenSource _source;
        private CancellationToken _cancellationToken;
        VersionRange _versionRange;
        List<string> _pathsToSearch;
        bool deleteAllVersions;
        #endregion

        #region Methods
        protected override void BeginProcessing()
        {
            _source = new CancellationTokenSource();
            _cancellationToken = _source.Token;

            // validate that if a -Version param is passed in that it can be parsed into a NuGet version range. 
            // an exact version will be formatted into a version range.
            if (ParameterSetName.Equals("NameParameterSet") && !Utils.TryParseVersionOrVersionRange(Version, out _versionRange, out deleteAllVersions, this))
            {
                var exMessage = String.Format("Argument for -Version parameter is not in the proper format.");
                var ex = new ArgumentException(exMessage);
                var IncorrectVersionFormat = new ErrorRecord(ex, "IncorrectVersionFormat", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(IncorrectVersionFormat);
            }

            var PSVersion6 = new Version(6, 0);
            var isCorePS = Host.Version >= PSVersion6;
            string myDocumentsPath;
            string programFilesPath;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string powerShellType = isCorePS ? "PowerShell" : "WindowsPowerShell";
                
                myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.MyDocuments), powerShellType);
                programFilesPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.ProgramFiles), powerShellType);
            }
            else
            {
                // paths are the same for both Linux and MacOS
                myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "Powershell");
                programFilesPath = Path.Combine("usr", "local", "share", "Powershell");
            }

            //// at this point we have all potential resource paths
            _pathsToSearch.Add(Path.Combine(myDocumentsPath, "Modules"));
            _pathsToSearch.Add(Path.Combine(programFilesPath, "Modules"));
            _pathsToSearch.Add(Path.Combine(myDocumentsPath, "Scripts"));
            _pathsToSearch.Add(Path.Combine(programFilesPath, "Scripts"));
        }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case NameParameterSet:
                    foreach (var pkgName in Name)
                    {
                        if (!ShouldProcess(string.Format("Uninstall resource '{0}' from the machine.", pkgName)))
                        {
                            return;
                        }

                        if (!String.IsNullOrWhiteSpace(pkgName) && !UninstallPkgHelper(pkgName))   /// pass in version?
                        {
                            // specific errors will be displayed lower in the stack
                            var exMessage = String.Format(string.Format("Did not successfully uninstall package {0}", pkgName));
                            var ex = new ArgumentException(exMessage);
                            var UninstallResourceError = new ErrorRecord(ex, "UninstallResourceError", ErrorCategory.InvalidOperation, null);
                            WriteError(UninstallResourceError);
                        }
                    }

                    break;
                case InputObjectSet:
                    // the for loop will use type PSObject in order to pull the properties from the pkg object
                    foreach (PSResourceInfo pkg in InputObject)
                    {
                        if (!ShouldProcess(string.Format("Uninstall resource '{0}' from the machine.", pkg.Name)))
                        {
                            return;
                        }

                        if (pkg != null)
                        {
                            // attempt to parse version
                            if (!Utils.TryParseVersionOrVersionRange(pkg.Version.ToString(), out VersionRange _versionRange, out bool deleteAllVersions, this))
                            {
                                var exMessage = String.Format("Version '{0}' for resource '{1}' cannot be parsed.", pkg.Version.ToString(), pkg.Name);
                                var ex = new ArgumentException(exMessage);
                                var ErrorParsingVersion = new ErrorRecord(ex, "ErrorParsingVersion", ErrorCategory.ParserError, null);
                                WriteError(ErrorParsingVersion);
                            }

                            if (!String.IsNullOrWhiteSpace(pkg.Name) && !UninstallPkgHelper(pkg.Name))
                            {
                                // specific errors will be displayed lower in the stack
                                var exMessage = String.Format(string.Format("Did not successfully uninstall package {0}", pkg.Name));
                                var ex = new ArgumentException(exMessage);
                                var UninstallResourceError = new ErrorRecord(ex, "UninstallResourceError", ErrorCategory.InvalidOperation, null);
                                    WriteError(UninstallResourceError);
                            }
                        }
                    }
                    break;
                default:
                    WriteDebug("Invalid parameter set");
                    break;
            }
        }
        
        /* uninstalls a single resource */
        private bool UninstallPkgHelper(string pkgName)
        {
            var successfullyUninstalled = false;
            List<string> dirsToDelete = new List<string>();
            var isScript = false;

            // Checking if module or script
            // a module path will look like:
            // ./Modules/TestModule/0.0.1
            // note that the xml file is located in this path, eg: ./Modules/TestModule/0.0.1/PSModuleInfo.xml 
            // a script path will look like:
            // ./Scripts/TestScript.ps1
            // note that the xml file is located in ./Scripts/InstalledScriptInfos, eg: ./Scripts/InstalledScriptInfos/TestScript_InstalledScriptInfo.xml
            foreach (var path in _pathsToSearch)
            {
                var dirName = Path.Combine(path, pkgName);
                var pathName = Path.Combine(path, pkgName + ".ps1");
                string[] versionDirs = null;
                if (Directory.Exists(dirName))
                {
                    // returns all the version directories
                    // eg:  TestModule/0.0.1, TestModule/0.0.2
                    versionDirs = Directory.GetDirectories(dirName);

                    // check if the version matches
                    if (_versionRange != null)
                    {
                        foreach (var versionDirPath in versionDirs)
                        {
                            if (deleteAllVersions)
                            {
                                dirsToDelete.Add(path);
                            }
                            else
                            {
                                var nameOfDir = Path.GetFileName(versionDirPath);
                                var nugVersion = NuGetVersion.Parse(nameOfDir);
                                if (_versionRange.Satisfies(nugVersion))
                                {
                                    dirsToDelete.Add(versionDirPath);
                                }
                            }
                        }
                    }
                    else
                    {
                        // if no version is specified, just delete the latest version
                        Array.Sort(versionDirs);

                        dirsToDelete.Add(versionDirs[versionDirs.Length - 1]);
                    }

                }
                else if (File.Exists(pathName))
                {
                    isScript = true;
                    // check if the version matches
                    if (_versionRange != null)
                    {
                        // Use Paul's deserialization method in utils
                        var xmlFileName = string.Concat(pkgName, "_InstalledScriptInfo.xml");
                        var scriptXMLPath = Path.Combine(path, "InstalledScriptInfos", xmlFileName);

                        ReadOnlyPSMemberInfoCollection<PSPropertyInfo> versionInfo;
                        using (StreamReader sr = new StreamReader(scriptXMLPath))
                        {
                            string text = sr.ReadToEnd();
                            // use Paul's deserialization here
                            var deserializedObj = (PSObject)PSSerializer.Deserialize(text);

                            versionInfo = deserializedObj.Properties.Match("Version");
                        };

                        if (NuGetVersion.TryParse(Version, out NuGetVersion scriptVersion) &&
                            _versionRange.Satisfies(scriptVersion))
                        {
                            // if version satisfies the condition, add it to the list
                            dirsToDelete.Add(pathName);
                        }
                    }
                    else {
                        // otherwise just delete whatever version the script is
                        dirsToDelete.Add(pathName);
                    }
                }
            }

            // if we can't find the resource, write non-terminating error and return
            if (!dirsToDelete.Any())
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(string.Format("Could not find the resource '{0}' in any path", pkgName)),
                    "ErrorRetrievingSpecifiedResource",
                    ErrorCategory.ObjectNotFound,
                    this));

                return successfullyUninstalled;
            }

            if (isScript)
            {
                successfullyUninstalled = UninstallScriptHelper(pkgName, dirsToDelete);
            }
            else 
            {
                successfullyUninstalled = UninstallModuleHelper(pkgName, dirsToDelete);
            }

            return successfullyUninstalled;
        }

        /* uninstalls a module */
        private bool UninstallModuleHelper(string pkgName, List<string> dirsToDelete)
        {
            var successfullyUninstalledPkg = false;

            // if -Force is not specified and the pkg is a dependency for another package, 
            // an error will be written and we return false
            if (!Force && CheckIfDependency(pkgName))
            {
                return false;
            }

            foreach (var pathToDelete in dirsToDelete)
            {
                var dir = new DirectoryInfo(pathToDelete.ToString());
                var parent = dir.Parent;
                dir.Attributes = dir.Attributes & ~FileAttributes.ReadOnly;

                try
                {
                    // delete recursively
                    dir.Delete(true);
                    WriteVerbose(string.Format("Successfully uninstalled '{0}' from path '{1}'", pkgName, dir.FullName));

                    successfullyUninstalledPkg = true;

                    // finally: check to see if there's anything left in the parent directory, if not, delete that as well
                    try
                    {
                        if (Directory.GetDirectories(dir.Parent.FullName).Length == 0)
                        {
                            Directory.Delete(dir.Parent.FullName, true);
                        }
                    }
                    catch (Exception e) {
                        // write error
                        var exMessage = String.Format("Parent directory '{0}' could not be deleted: {1}", dir.Parent.FullName, e.Message);
                        var ex = new ArgumentException(exMessage);
                        var ErrorDeletingParentDirectory = new ErrorRecord(ex, "ErrorDeletingParentDirectory", ErrorCategory.InvalidArgument, null);
                        WriteError(ErrorDeletingParentDirectory);
                    }
                }
                catch (Exception err) {
                    // write error
                    var exMessage = String.Format("Directory '{0}' could not be deleted: {1}", dir.FullName, err.Message);
                    var ex = new ArgumentException(exMessage);
                    var ErrorDeletingDirectory = new ErrorRecord(ex, "ErrorDeletingDirectory", ErrorCategory.PermissionDenied, null);
                    WriteError(ErrorDeletingDirectory);
                }
            }
                 
            return successfullyUninstalledPkg;
        }

        /* uninstalls a script */
        private bool UninstallScriptHelper(string pkgName, List<string> dirsToDelete)
        {
            var successfullyUninstalledPkg = false;

            // if -Force is not specified and the pkg is a dependency for another package, 
            // an error will be written and we return false
            if (!Force && CheckIfDependency(pkgName))
            {
                return false;
            }

            foreach (var scriptPath in dirsToDelete)
            {
                // delete the appropriate file
                try
                {
                    File.Delete(scriptPath);
                    successfullyUninstalledPkg = true;

                    string scriptXML = string.Empty;
                    try
                    {
                        // finally: Delete the xml from the InstalledModulesInfo directory
                        scriptXML = Path.Combine(scriptPath, "InstalledScriptInfos", pkgName + "_InstalledScriptInfo.xml");
                        if (File.Exists(scriptXML))
                        {
                            File.Delete(scriptXML);
                        }
                    }
                    catch (Exception e)
                    {
                        var exMessage = String.Format("Script metadata file '{0}' could not be deleted: {1}", scriptXML, e.Message);
                        var ex = new ArgumentException(exMessage);
                        var ErrorDeletingScriptMetadataFile = new ErrorRecord(ex, "ErrorDeletingScriptMetadataFile", ErrorCategory.PermissionDenied, null);
                        WriteError(ErrorDeletingScriptMetadataFile);
                    }
                }
                catch (Exception err){
                    var exMessage = String.Format("Script '{0}' could not be deleted: {1}", scriptPath, err.Message);
                    var ex = new ArgumentException(exMessage);
                    var ErrorDeletingScript = new ErrorRecord(ex, "ErrorDeletingScript", ErrorCategory.PermissionDenied, null);
                    WriteError(ErrorDeletingScript);
                }
            }

            return successfullyUninstalledPkg;
        }

        private bool CheckIfDependency(string pkgName)
        {
            // this is a primitive implementation
            // TODO:  implement a dependencies database for querying dependency info
            // cannot uninstall a module if another module is dependent on it 
            using (System.Management.Automation.PowerShell pwsh = System.Management.Automation.PowerShell.Create())
            {
                // Check all modules for dependencies
                var results = pwsh.AddCommand("Get-Module").AddParameter("ListAvailable").Invoke();

                // Structure of LINQ call:
                // Results is a collection of PSModuleInfo objects that contain a property listing module dependencies, "RequiredModules".
                // RequiredModules is collection of PSModuleInfo objects that need to be iterated through to see if any of them are the pkg we're trying to uninstall
                // If we anything from the final call gets returned, there is a dependency on this pkg.
                IEnumerable<PSObject> pkgsWithRequiredModules = new List<PSObject>();
                try
                {
                    pkgsWithRequiredModules = results.Where(p => ((ReadOnlyCollection<PSModuleInfo>)p.Properties["RequiredModules"].Value).Where(rm => rm.Name.Equals(pkgName, StringComparison.InvariantCultureIgnoreCase)).Any());
                }
                catch (Exception e) {
                    var exMessage = String.Format("Error checking if resource is a dependency: {0}. If you would still like to uninstall, rerun the command with -Force", e.Message);
                    var ex = new ArgumentException(exMessage);
                    var DependencyCheckError = new ErrorRecord(ex, "DependencyCheckError", ErrorCategory.OperationStopped, null);
                    WriteError(DependencyCheckError);
                }

                if (pkgsWithRequiredModules.Any())
                {
                    var uniquePkgNames = pkgsWithRequiredModules.Select(p => p.Properties["Name"].Value).Distinct().ToArray();
                    var strUniquePkgNames = string.Join(",", uniquePkgNames);

                    var exMessage = String.Format("Cannot uninstall '{0}', the following package(s) take a dependency on this package: {1}. If you would still like to uninstall, rerun the command with -Force", pkgName, strUniquePkgNames);
                    var ex = new ArgumentException(exMessage);
                    var PackageIsaDependency = new ErrorRecord(ex, "PackageIsaDependency", ErrorCategory.OperationStopped, null);
                    WriteError(PackageIsaDependency);
                    return true;
                }
            }
            return false;
        }
        #endregion
    }
}
