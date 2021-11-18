// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;

using Dbg = System.Diagnostics.Debug;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Save-PSResource cmdlet saves a resource to a machine.
    /// It returns nothing.
    /// </summary>
    [Cmdlet(VerbsData.Save, "PSResource", DefaultParameterSetName = "NameParameterSet", SupportsShouldProcess = true)]
    public sealed class SavePSResource : PSCmdlet
    {
        #region Members

        private const string NameParameterSet = "NameParameterSet";
        private const string InputObjectParameterSet = "InputObjectParameterSet";
        VersionRange _versionRange;
        InstallHelper _installHelper;
        
        #endregion

        #region Parameters 

        /// <summary>
        /// Specifies the exact names of resources to save from a repository.
        /// A comma-separated list of module names is accepted. The resource name must match the resource name in the repository.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; }

        /// <summary>
        /// Specifies the version or version range of the package to be saved
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }

        /// <summary>
        /// Specifies to allow saveing of prerelease versions
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter Prerelease { get; set; }

        /// <summary>
        /// Specifies the specific repositories to search within.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        /// <summary>
        /// Specifies a user account that has rights to save a resource from a specific repository.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public PSCredential Credential { get; set; }
        
        /*
        /// <summary>
        /// Saves as a .nupkg
        /// </summary>
        [Parameter()]
        public SwitchParameter AsNupkg { get; set; }

        /// <summary>
        /// Saves the metadata XML file with the resource
        /// </summary>
        [Parameter()]
        public SwitchParameter IncludeXML { get; set; }
        */

        /// <summary>
        /// The destination where the resource is to be installed. Works for all resource types.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Path
        {
            get
            { return _path; }

            set
            {
                string resolvedPath = string.Empty;
                if (!string.IsNullOrEmpty(value))
                {
                    resolvedPath = SessionState.Path.GetResolvedPSPathFromPSPath(value).First().Path;
                }

                // Path where resource is saved must be a directory
                if (Directory.Exists(resolvedPath))
                {
                    _path = resolvedPath;
                }
            }
        }
        private string _path;

        /// <summary>
        /// Suppresses being prompted for untrusted sources.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public SwitchParameter TrustRepository { get; set; }
        
        /// <summary>
        /// Used for pipeline input.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = InputObjectParameterSet)]
        [ValidateNotNullOrEmpty]
        public PSResourceInfo InputObject { get; set; }

        /// <summary>
        /// Skips the check for resource dependencies, so that only found resources are saved,
        /// and not any resources the found resource depends on.
        /// </summary>
        [Parameter]
        public SwitchParameter SkipDependencyCheck { get; set; }

        #endregion

        #region Method overrides

        protected override void BeginProcessing()
        {
            // Create a repository story (the PSResourceRepository.xml file) if it does not already exist
            // This is to create a better experience for those who have just installed v3 and want to get up and running quickly
            RepositorySettings.CheckRepositoryStore();

            // If the user does not specify a path to save to, use the user's current working directory
            if (string.IsNullOrWhiteSpace(_path))
            {
                _path = SessionState.Path.CurrentLocation.Path;
            }

            _installHelper = new InstallHelper(savePkg: true, cmdletPassedIn: this);
        }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case NameParameterSet:
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

                    ProcessSaveHelper(
                        pkgNames: Name,
                        pkgPrerelease: Prerelease,
                        pkgRepository: Repository);
                    break;

                case InputObjectParameterSet:
                    string normalizedVersionString = Utils.GetNormalizedVersionString(InputObject.Version.ToString(), InputObject.PrereleaseLabel);
                    if (!Utils.TryParseVersionOrVersionRange(normalizedVersionString, out _versionRange))
                    {
                        var exMessage = String.Format("Version '{0}' for resource '{1}' cannot be parsed.", normalizedVersionString, InputObject.Name);
                        var ex = new ArgumentException(exMessage);
                        var IncorrectVersionFormat = new ErrorRecord(ex, "IncorrectVersionFormat", ErrorCategory.InvalidArgument, null);
                        ThrowTerminatingError(IncorrectVersionFormat);
                    }
                    
                    ProcessSaveHelper(
                        pkgNames: new string[] { InputObject.Name },
                        pkgPrerelease: InputObject.IsPrerelease,
                        pkgRepository: new string[] { InputObject.Repository });
                
                    break;

                default:
                    Dbg.Assert(false, "Invalid parameter set");
                    break;
            }
        }

        #endregion

        #region Private methods

        private void ProcessSaveHelper(string[] pkgNames, bool pkgPrerelease, string[] pkgRepository)
        {
            var namesToSave = Utils.ProcessNameWildcards(pkgNames, out string[] errorMsgs, out bool nameContainsWildcard);
            if (nameContainsWildcard)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException("Name with wildcards is not supported for Save-PSResource cmdlet"),
                    "NameContainsWildcard",
                    ErrorCategory.InvalidArgument,
                    this));
                return;
            }
            
            foreach (string error in errorMsgs)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(error),
                    "ErrorFilteringNamesForUnsupportedWildcards",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            // this catches the case where Name wasn't passed in as null or empty,
            // but after filtering out unsupported wildcard names there are no elements left in namesToSave
            if (namesToSave.Length == 0)
            {
                return;
            }

            if (!ShouldProcess(string.Format("Resources to save: '{0}'", namesToSave)))
            {
                WriteVerbose(string.Format("Save operation cancelled by user for resources: {0}", namesToSave));
                return;
            }

            _installHelper.InstallPackages(
                names: namesToSave, 
                versionRange: _versionRange, 
                prerelease: pkgPrerelease, 
                repository: pkgRepository, 
                acceptLicense: true, 
                quiet: true, 
                reinstall: true, 
                force: false, 
                trustRepository: TrustRepository,
                credential: Credential, 
                noClobber: false, 
                specifiedPath: _path, 
                asNupkg: false, 
                includeXML: false,
                skipDependencyCheck: SkipDependencyCheck,
                pathsToInstallPkg: new List<string> { _path } );
        }
        
        #endregion
    }
}
