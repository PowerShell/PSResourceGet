using System.Reflection;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections;
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
    [Cmdlet(VerbsLifecycle.Uninstall, "PSResource", DefaultParameterSetName = NameParameterSet, SupportsShouldProcess = true)]
    public sealed class UninstallPSResource : PSCmdlet
    {
        #region Parameters
        /// <summary>
        /// Specifies the exact names of resources to uninstall.
        /// A comma-separated list of module names is accepted. The resource name must match the resource name in the repository.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = NameParameterSet)]
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
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = InputObjectParameterSet)]
        [ValidateNotNullOrEmpty]
        public PSResourceInfo InputObject { get; set; }

        /// <summary>
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public SwitchParameter Force { get; set; }
        #endregion

        #region Members
        private const string NameParameterSet = "NameParameterSet";
        private const string InputObjectParameterSet = "InputObjectParameterSet";
        public static readonly string OsPlatform = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        VersionRange _versionRange;
        List<string> _pathsToSearch = new List<string>();
        string[] _prereleaseLabels = new string[]{};
        #endregion

        #region Methods
        protected override void BeginProcessing()
        {
            _pathsToSearch = Utils.GetAllResourcePaths(this);
        }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case NameParameterSet:
                    // if no Version specified, uninstall all versions for the package.
                    // validate that if a -Version param is passed in that it can be parsed into a NuGet version range. 
                    // an exact version will be formatted into a version range.
                    if (Version == null)
                    {
                        _versionRange = VersionRange.All;
                    }
                    else if (!Utils.TryParseVersionOrVersionRange(version: Utils.GetVersionWithoutPrerelease(Version, out _prereleaseLabels),
                        versionRange: out _versionRange))
                    {
                        var exMessage = "Argument for -Version parameter is not in the proper format.";
                        var ex = new ArgumentException(exMessage);
                        var IncorrectVersionFormat = new ErrorRecord(ex, "IncorrectVersionFormat", ErrorCategory.InvalidArgument, null);
                        ThrowTerminatingError(IncorrectVersionFormat);
                    }

                    Name = Utils.ProcessNameWildcards(Name, out string[] errorMsgs, out bool _);
                    
                    foreach (string error in errorMsgs)
                    {
                        WriteError(new ErrorRecord(
                            new PSInvalidOperationException(error),
                            "ErrorFilteringNamesForUnsupportedWildcards",
                            ErrorCategory.InvalidArgument,
                            this));
                    }

                    // this catches the case where Name wasn't passed in as null or empty,
                    // but after filtering out unsupported wildcard names there are no elements left in Name
                    if (Name.Length == 0)
                    {
                        return;
                    }

                    if (!UninstallPkgHelper())
                    {
                        // any errors should be caught lower in the stack, this debug statement will let us know if there was an unusual failure
                        WriteVerbose("Did not successfully uninstall all packages");
                    }
                    break;

                case InputObjectParameterSet:
                    if (!Utils.TryParseVersionOrVersionRange(version: Utils.GetVersionWithoutPrerelease(InputObject.Version.ToString(), out _prereleaseLabels),
                        versionRange: out _versionRange))
                    {
                        var exMessage = String.Format("Version '{0}' for resource '{1}' cannot be parsed.", InputObject.Version.ToString(), InputObject.Name);
                        var ex = new ArgumentException(exMessage);
                        var ErrorParsingVersion = new ErrorRecord(ex, "ErrorParsingVersion", ErrorCategory.ParserError, null);
                        WriteError(ErrorParsingVersion);
                    }

                    Name = new string[] { InputObject.Name };
                    if (!String.IsNullOrWhiteSpace(InputObject.Name) && !UninstallPkgHelper())
                    {
                        // specific errors will be displayed lower in the stack
                        var exMessage = String.Format(string.Format("Did not successfully uninstall package {0}", InputObject.Name));
                        var ex = new ArgumentException(exMessage);
                        var UninstallResourceError = new ErrorRecord(ex, "UninstallResourceError", ErrorCategory.InvalidOperation, null);
                            WriteError(UninstallResourceError);
                    }
                
                    break;

                default:
                    WriteVerbose("Invalid parameter set");
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
                    WriteVerbose("ShouldProcess is set to false.");
                    continue;
                }

                ErrorRecord errRecord = null;
                if (pkgPath.EndsWith(".ps1"))
                {
                    successfullyUninstalled = UninstallScriptHelper(pkgPath, pkgName, out errRecord);
                }
                else
                {
                    successfullyUninstalled = UninstallModuleHelper(pkgPath, pkgName, out errRecord);
                }

                // if we can't find the resource, write non-terminating error and return
                if (!successfullyUninstalled || errRecord != null)
                {
                    if (errRecord == null)
                    {
                        string message = Version == null || Version.Trim().Equals("*") ?
                            string.Format("Could not find any version of the resource '{0}' in any path", pkgName) :
                            string.Format("Could not find verison '{0}' of the resource '{1}' in any path", Version, pkgName);

                        errRecord = new ErrorRecord(
                            new PSInvalidOperationException(message),
                            "ErrorRetrievingSpecifiedResource",
                            ErrorCategory.ObjectNotFound,
                            this);
                    }
                    
                    WriteError(errRecord);
                }
            }

            return successfullyUninstalled;
        }

        /* uninstalls a module */
        private bool UninstallModuleHelper(string pkgPath, string pkgName, out ErrorRecord errRecord)
        {
            errRecord = null;
            var successfullyUninstalledPkg = false;

            // TODO: remove
            // if Version provided by user contains prerelease label and installed package contains same prerelease label, then uninstall
            // if (!String.IsNullOrEmpty(_prereleaseLabel) && !CheckIfPrerelease(isModule: true,
            //     pkgPath: pkgPath,
            //     pkgName: pkgName,
            //     expectedPrereleaseLabel: _prereleaseLabel))
            // {
            //     return false;
            // }
            if (!CheckIfPrerelease(isModule: true, pkgName: pkgName, pkgPath: pkgPath))
            {
                return false;
            }

            // if -Force is not specified and the pkg is a dependency for another package, 
            // an error will be written and we return false
            if (!Force && CheckIfDependency(pkgName, out errRecord))
            {
                return false;
            }

            DirectoryInfo dir = new DirectoryInfo(pkgPath);
            dir.Attributes &= ~FileAttributes.ReadOnly;

            try
            {
                Utils.DeleteDirectory(pkgPath);
                WriteVerbose(string.Format("Successfully uninstalled '{0}' from path '{1}'", pkgName, dir.FullName));

                successfullyUninstalledPkg = true;

                // finally: check to see if there's anything left in the parent directory, if not, delete that as well
                try
                {
                    if (Utils.GetSubDirectories(dir.Parent.FullName).Length == 0)
                    {
                        Directory.Delete(dir.Parent.FullName, true);
                    }
                }
                catch (Exception e) {
                    // write error
                    var exMessage = String.Format("Parent directory '{0}' could not be deleted: {1}", dir.Parent.FullName, e.Message);
                    var ex = new ArgumentException(exMessage);
                    var ErrorDeletingParentDirectory = new ErrorRecord(ex, "ErrorDeletingParentDirectory", ErrorCategory.InvalidArgument, null);
                    errRecord = ErrorDeletingParentDirectory;
                }
            }
            catch (Exception err) {
                // write error
                var exMessage = String.Format("Directory '{0}' could not be deleted: {1}", dir.FullName, err.Message);
                var ex = new ArgumentException(exMessage);
                var ErrorDeletingDirectory = new ErrorRecord(ex, "ErrorDeletingDirectory", ErrorCategory.PermissionDenied, null);
                errRecord = ErrorDeletingDirectory;
            }

            return successfullyUninstalledPkg;
        }

        /* uninstalls a script */
        private bool UninstallScriptHelper(string pkgPath, string pkgName, out ErrorRecord errRecord)
        {
            errRecord = null;
            var successfullyUninstalledPkg = false;

            // TODO: remove
            // if Version provided by user contains prerelease label and installed package contains same prerelease label, then uninstall
            // if (!String.IsNullOrEmpty(_prereleaseLabel) && !CheckIfPrerelease(isModule: true,
            //     pkgPath: pkgPath,
            //     pkgName: pkgName,
            //     expectedPrereleaseLabel: _prereleaseLabel))
            // {
            //     return false;
            // }
            if (!CheckIfPrerelease(isModule: false, pkgName: pkgName, pkgPath: pkgPath))
            {
                return false;
            }

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
                    errRecord = ErrorDeletingScriptMetadataFile;
                }
            }
            catch (Exception err){
                var exMessage = String.Format("Script '{0}' could not be deleted: {1}", pkgPath, err.Message);
                var ex = new ArgumentException(exMessage);
                var ErrorDeletingScript = new ErrorRecord(ex, "ErrorDeletingScript", ErrorCategory.PermissionDenied, null);
                errRecord = ErrorDeletingScript;
            }

            return successfullyUninstalledPkg;
        }

        private bool CheckIfDependency(string pkgName, out ErrorRecord errRecord)
        {
            errRecord = null;
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
                    errRecord = DependencyCheckError;
                }

                if (pkgsWithRequiredModules.Any())
                {
                    var uniquePkgNames = pkgsWithRequiredModules.Select(p => p.Properties["Name"].Value).Distinct().ToArray();
                    var strUniquePkgNames = string.Join(",", uniquePkgNames);

                    var exMessage = String.Format("Cannot uninstall '{0}', the following package(s) take a dependency on this package: {1}. If you would still like to uninstall, rerun the command with -Force", pkgName, strUniquePkgNames);
                    var ex = new ArgumentException(exMessage);
                    var PackageIsaDependency = new ErrorRecord(ex, "PackageIsaDependency", ErrorCategory.OperationStopped, null);
                    errRecord = PackageIsaDependency;

                    return true;
                }
            }
            return false;
        }
        
        private bool CheckIfPrerelease(bool isModule, string pkgPath, string pkgName)
        {

            string PSGetModuleInfoFilePath = isModule ? Path.Combine(pkgPath, "PSGetModuleInfo.xml") : Path.Combine(Path.GetDirectoryName(pkgPath), "InstalledScriptInfos", pkgName + "_InstalledScriptInfo.xml");
            WriteVerbose("pkgPath: " + PSGetModuleInfoFilePath);
            if (!PSResourceInfo.TryRead(PSGetModuleInfoFilePath, out PSResourceInfo psGetInfo, out string errorMsg))
            {
                return false;
            }

            string[] versionRangeParts = _versionRange.ToString().Trim(new char []{'[', ']', '(', ')'}).Split(',');
            if ((_versionRange != VersionRange.All) &&
                (psGetInfo.Version.ToString().StartsWith(versionRangeParts[0]) && !String.Equals(psGetInfo.PrereleaseLabel, _prereleaseLabels[0], StringComparison.InvariantCultureIgnoreCase)) ||
                (psGetInfo.Version.ToString().StartsWith(versionRangeParts[1]) && !String.Equals(psGetInfo.PrereleaseLabel, _prereleaseLabels[1], StringComparison.InvariantCultureIgnoreCase)))
            {
                return false;
            }
            return true;
            
            // // get module manifest path, same for modules and scripts:
            // // ./Modules/TestModule/0.0.1/TestModule.psd1
            // // ./Scripts/TestScript/0.0.1/TestScript.psd1
            // string moduleManifestPath = Path.Combine(pkgPath, pkgName + ".psd1");

            // if (!Utils.TryParseModuleManifest(moduleManifestPath, this, out Hashtable parsedMetadataHashtable))
            // {
            //     WriteError(new ErrorRecord(
            //         new PSInvalidOperationException("Module manifest could not be parsed for package: " + pkgName),
            //         "ErrorParsingModuleManifest",
            //         ErrorCategory.InvalidData,
            //         this));
            //     return false;
            // }

            // if (String.Equals(parsedPrereleaseLabel, expectedPrereleaseLabel, StringComparison.InvariantCultureIgnoreCase))
            // {
            //     return true;
            // }
        }
        #endregion
    }
}
