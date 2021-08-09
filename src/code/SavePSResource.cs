// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using Dbg = System.Diagnostics.Debug;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;

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
        private const string InputObjectSet = "InputObjectSet";
        VersionRange _versionRange;
        
        #endregion

        #region Parameters 

        /// <summary>
        /// Specifies the exact names of resources to save from a repository.
        /// A comma-separated list of module names is accepted. The resource name must match the resource name in the repository.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true, ParameterSetName = NameParameterSet)]
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
        // todo: add tab completion (look at get-psresourcerepository at the name parameter)
        [ValidateNotNullOrEmpty]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        public string[] Repository { get; set; }

        /// <summary>
        /// Specifies a user account that has rights to save a resource from a specific repository.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = NameParameterSet)]
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
        [Parameter(ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
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
        [Parameter()]
        public SwitchParameter TrustRepository { get; set; }
        
        /// <summary>
        /// Used for pipeline input.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = InputObjectSet)]
        [ValidateNotNullOrEmpty]
        public object[] InputObject { set; get; }

        #endregion

        #region Method overrides

        protected override void BeginProcessing()
        {
            // Create a respository story (the PSResourceRepository.xml file) if it does not already exist
            // This is to create a better experience for those who have just installed v3 and want to get up and running quickly
            RepositorySettings.CheckRepositoryStore();

            // validate that if a -Version param is passed in that it can be parsed into a NuGet version range. 
            // an exact version will be formatted into a version range.
            if (ParameterSetName.Equals("NameParameterSet") && 
                Version != null && 
                !Utils.TryParseVersionOrVersionRange(Version, out _versionRange))
            {
                var exMessage = "Argument for -Version parameter is not in the proper format.";
                var ex = new ArgumentException(exMessage);
                var IncorrectVersionFormat = new ErrorRecord(ex, "IncorrectVersionFormat", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(IncorrectVersionFormat);
            }

            // If the user does not specify a path to save to, use the user's current working directory
            if (string.IsNullOrWhiteSpace(_path))
            {
                _path = SessionState.Path.CurrentLocation.Path;
            }
        }

        protected override void ProcessRecord()
        {
            if (!ShouldProcess(string.Format("Resources to save: '{0}'", String.Join(", ", Name))))
            {
                WriteVerbose(string.Format("Save operation cancelled by user for resources: {0}", String.Join(", ", Name)));
                return;
            }

            var installHelper = new InstallHelper(updatePkg: false, savePkg: true, cmdletPassedIn: this);

            switch (ParameterSetName)
            {
                case NameParameterSet:
                    var namesToSave = Utils.ProcessNameWildcards(Name, out string[] errorMsgs, out bool nameContainsWildcard);
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

                    installHelper.InstallPackages(
                        names: namesToSave, 
                        versionRange: _versionRange, 
                        prerelease: Prerelease, 
                        repository: Repository, 
                        acceptLicense: true, 
                        quiet: true, 
                        reinstall: true, 
                        force: false, 
                        trustRepository: TrustRepository, 
                        noClobber: false, 
                        credential: Credential, 
                        requiredResourceFile: null,
                        requiredResourceJson: null, 
                        requiredResourceHash: null, 
                        specifiedPath: _path, 
                        asNupkg: false, 
                        includeXML: false, 
                        pathsToInstallPkg: new List<string> { _path } );
                    break;

                default:
                    Dbg.Assert(false, "Invalid parameter set");
                    break;
            }
        }

        #endregion
    }
}
