// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management.Automation;
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
        VersionRange _versionRange;
        List<string> _pathsToSearch = new List<string>();
        #endregion

        #region Methods
        protected override void BeginProcessing()
        {
            // validate that if a -Version param is passed in that it can be parsed into a NuGet version range. 
            // an exact version will be formatted into a version range.
            if (ParameterSetName.Equals("NameParameterSet") && Version != null && !Utils.TryParseVersionOrVersionRange(Version, out _versionRange))
            {
                var exMessage = "Argument for -Version parameter is not in the proper format.";
                var ex = new ArgumentException(exMessage);
                var IncorrectVersionFormat = new ErrorRecord(ex, "IncorrectVersionFormat", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(IncorrectVersionFormat);
            }

            _pathsToSearch = Utils.GetAllResourcePaths(this);
        }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case NameParameterSet:
                    if (!UninstallPkgHelper())
                    {
                        // any errors should be caught lower in the stack, this debug statement will let us know if there was an unusual failure
                        WriteDebug("Did not successfully uninstall all packages");
                    }
                    break;

                case InputObjectSet:
                    // the for loop will use type PSObject in order to pull the properties from the pkg object
                    foreach (PSResourceInfo pkg in InputObject)
                    {
                        if (pkg == null)
                        {
                            continue;
                        }

                        // attempt to parse version
                        if (!Utils.TryParseVersionOrVersionRange(pkg.Version.ToString(), out VersionRange _versionRange))
                        {
                            var exMessage = String.Format("Version '{0}' for resource '{1}' cannot be parsed.", pkg.Version.ToString(), pkg.Name);
                            var ex = new ArgumentException(exMessage);
                            var ErrorParsingVersion = new ErrorRecord(ex, "ErrorParsingVersion", ErrorCategory.ParserError, null);
                            WriteError(ErrorParsingVersion);
                        }

                        Name = new string[] { pkg.Name };
                        if (!String.IsNullOrWhiteSpace(pkg.Name) && !UninstallPkgHelper())
                        {
                            // specific errors will be displayed lower in the stack
                            var exMessage = String.Format(string.Format("Did not successfully uninstall package {0}", pkg.Name));
                            var ex = new ArgumentException(exMessage);
                            var UninstallResourceError = new ErrorRecord(ex, "UninstallResourceError", ErrorCategory.InvalidOperation, null);
                                WriteError(UninstallResourceError);
                        }
                    }
                    break;

                default:
                    WriteDebug("Invalid parameter set");
                    break;
            }
        }
        

        private bool UninstallPkgHelper()
        {
            var successfullyUninstalled = false;

            GetHelper getHelper = new GetHelper(this);
            List<string>  dirsToDelete = getHelper.FilterPkgPathsByName(Name, _pathsToSearch);

            // Checking if module or script
            // a module path will look like:
            // ./Modules/TestModule/0.0.1
            // note that the xml file is located in this path, eg: ./Modules/TestModule/0.0.1/PSModuleInfo.xml 
            // a script path will look like:
            // ./Scripts/TestScript.ps1
            // note that the xml file is located in ./Scripts/InstalledScriptInfos, eg: ./Scripts/InstalledScriptInfos/TestScript_InstalledScriptInfo.xml

            string pkgName = string.Empty;

            foreach (string pkgPath in getHelper.FilterPkgPathsByVersion(_versionRange, dirsToDelete))
            {
                pkgName = Utils.GetInstalledPackageName(pkgPath);

                if (!ShouldProcess(string.Format("Uninstall resource '{0}' from the machine.", pkgName)))
                {
                    this.WriteDebug("ShouldProcess is set to false.");
                    continue;
                }

                if (pkgPath.EndsWith(".ps1"))
                {
                    successfullyUninstalled = UninstallScriptHelper(pkgPath, pkgName);
                }
                else
                {
                    successfullyUninstalled = UninstallModuleHelper(pkgPath, pkgName);
                }

                // if we can't find the resource, write non-terminating error and return
                if (!successfullyUninstalled)
                {
                    string message = Version == null || Version.Trim().Equals("*") ?
                        string.Format("Could not find any version of the resource '{0}' in any path", pkgName) :
                        string.Format("Could not find verison '{0}' of the resource '{1}' in any path", Version, pkgName);

                    WriteError(new ErrorRecord(
                        new PSInvalidOperationException(message),
                        "ErrorRetrievingSpecifiedResource",
                        ErrorCategory.ObjectNotFound,
                        this));
                }
            }

            return successfullyUninstalled;
        }

        /* uninstalls a module */
        private bool UninstallModuleHelper(string pkgPath, string pkgName)
        {
            var successfullyUninstalledPkg = false;

            // if -Force is not specified and the pkg is a dependency for another package, 
            // an error will be written and we return false
            if (!Force && CheckIfDependency(pkgName))
            {
                return false;
            }
            
            DirectoryInfo dir = new DirectoryInfo(pkgPath);
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
            
            return successfullyUninstalledPkg;
        }

        /* uninstalls a script */
        private bool UninstallScriptHelper(string pkgPath, string pkgName)
        {
            var successfullyUninstalledPkg = false;

            // delete the appropriate file
            try
            {
                File.Delete(pkgPath);
                successfullyUninstalledPkg = true;

                string scriptXML = string.Empty;
                try
                {
                    // finally: Delete the xml from the InstalledModulesInfo directory
                    DirectoryInfo dir = new DirectoryInfo(pkgPath);
                    DirectoryInfo parentDir = dir.Parent;
                    scriptXML = Path.Combine(parentDir.FullName, "InstalledScriptInfos", pkgName + "_InstalledScriptInfo.xml");
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
                var exMessage = String.Format("Script '{0}' could not be deleted: {1}", pkgPath, err.Message);
                var ex = new ArgumentException(exMessage);
                var ErrorDeletingScript = new ErrorRecord(ex, "ErrorDeletingScript", ErrorCategory.PermissionDenied, null);
                WriteError(ErrorDeletingScript);
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
                    pkgsWithRequiredModules = results.Where(
                        p => ((ReadOnlyCollection<PSModuleInfo>)p.Properties["RequiredModules"].Value).Where(
                            rm => rm.Name.Equals(pkgName, StringComparison.InvariantCultureIgnoreCase)).Any());
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
