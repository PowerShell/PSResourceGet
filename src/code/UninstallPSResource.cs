// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using NuGet.Versioning;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management.Automation;

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
        [SupportsWildcards]
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
        /// When specified, only uninstalls prerelease versions.
        /// </summary>
        [Parameter]
        public SwitchParameter Prerelease { get; set; }

        /// <summary>
        /// Used for pipeline input.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = InputObjectParameterSet)]
        [ValidateNotNullOrEmpty]
        public PSResourceInfo InputObject { get; set; }

        /// <summary>
        /// Skips check to see if other resources are dependent on the resource being uninstalled.
        /// </summary>
        [Parameter]
        public SwitchParameter SkipDependencyCheck { get; set; }

        /// <summary>
        /// Specifies the scope of installation.
        /// </summary>
        [Parameter]
        public ScopeType Scope { get; set; }

        #endregion

        #region Members

        private const string NameParameterSet = "NameParameterSet";
        private const string InputObjectParameterSet = "InputObjectParameterSet";
        public const string PSScriptFileExt = ".ps1";
        public static readonly string OsPlatform = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        VersionRange _versionRange;
        List<string> _pathsToSearch = new List<string>();

        #endregion

        #region Method overrides

        protected override void BeginProcessing()
        {
            _pathsToSearch = Utils.GetAllResourcePaths(this, Scope);
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
                    else if (!Utils.TryParseVersionOrVersionRange(Version, out _versionRange))
                    {
                        var exMessage = "Argument for -Version parameter is not in the proper format.";
                        var ex = new ArgumentException(exMessage);
                        var IncorrectVersionFormat = new ErrorRecord(ex, "IncorrectVersionFormat", ErrorCategory.InvalidArgument, null);
                        ThrowTerminatingError(IncorrectVersionFormat);
                    }

                    Name = Utils.ProcessNameWildcards(Name, removeWildcardEntries:false, out string[] errorMsgs, out bool _);
                    
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
                    string inputObjectPrerelease = InputObject.Prerelease;
                    string inputObjectVersion = String.IsNullOrEmpty(inputObjectPrerelease) ? InputObject.Version.ToString() : Utils.GetNormalizedVersionString(versionString: InputObject.Version.ToString(), prerelease: inputObjectPrerelease);
                    if (!Utils.TryParseVersionOrVersionRange(
                        version: inputObjectVersion,
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

        #endregion

        #region Private methods

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

            string pkgName;
            foreach (string pkgPath in getHelper.FilterPkgPathsByVersion(_versionRange, dirsToDelete, selectPrereleaseOnly: Prerelease))
            {
                pkgName = Utils.GetInstalledPackageName(pkgPath);

                if (!ShouldProcess(string.Format("Uninstall resource '{0}' from the machine.", pkgName)))
                {
                    WriteVerbose("ShouldProcess is set to false.");
                    continue;
                }

                ErrorRecord errRecord = null;
                if (pkgPath.EndsWith(PSScriptFileExt))
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

            // if -SkipDependencyCheck is not specified and the pkg is a dependency for another package, 
            // an error will be written and we return false
            if (!SkipDependencyCheck && CheckIfDependency(pkgName, out errRecord))
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
                        Utils.DeleteDirectory(dir.Parent.FullName);
                    }
                }
                catch (Exception e)
                {
                    // write error
                    var exMessage = String.Format("Parent directory '{0}' could not be deleted: {1}", dir.Parent.FullName, e.Message);
                    var ex = new ArgumentException(exMessage);
                    var ErrorDeletingParentDirectory = new ErrorRecord(ex, "ErrorDeletingParentDirectory", ErrorCategory.InvalidArgument, null);
                    errRecord = ErrorDeletingParentDirectory;
                }
            }
            catch (Exception err)
            {
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
            catch (Exception err)
            {
                var exMessage = String.Format("Script '{0}' could not be deleted: {1}", pkgPath, err.Message);
                var ex = new ArgumentException(exMessage);
                var ErrorDeletingScript = new ErrorRecord(ex, "ErrorDeletingScript", ErrorCategory.PermissionDenied, null);
                errRecord = ErrorDeletingScript;
            }

            return successfullyUninstalledPkg;
        }

        private bool CheckIfDependency(string pkgName, out ErrorRecord errorRecord)
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
                errorRecord = null;
                try
                {
                    pkgsWithRequiredModules = results.Where(
                        pkg => ((ReadOnlyCollection<PSModuleInfo>)pkg.Properties["RequiredModules"].Value).Where(
                            rm => rm.Name.Equals(pkgName, StringComparison.InvariantCultureIgnoreCase)).Any());
                }
                catch (Exception e)
                {
                    errorRecord = new ErrorRecord(
                        new PSInvalidOperationException(
                            $"Error checking if resource is a dependency: {e.Message}."),
                        "UninstallPSResourceDependencyCheckError",
                        ErrorCategory.InvalidOperation,
                        null);
                }

                if (pkgsWithRequiredModules.Any())
                {
                    var uniquePkgNames = pkgsWithRequiredModules.Select(p => p.Properties["Name"].Value).Distinct().ToArray();
                    var strUniquePkgNames = string.Join(",", uniquePkgNames);

                    errorRecord = new ErrorRecord(
                        new PSInvalidOperationException(
                            $"Cannot uninstall '{pkgName}'. The following package(s) take a dependency on this package: {strUniquePkgNames}. If you would still like to uninstall, rerun the command with -SkipDependencyCheck"),
                        "UninstallPSResourcePackageIsaDependency",
                        ErrorCategory.InvalidOperation,
                        null);

                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
