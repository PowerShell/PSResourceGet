// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using Dbg = System.Diagnostics.Debug;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Save-PSResource cmdlet saves a resource to a machine.
    /// It returns nothing.
    /// </summary>
    [Cmdlet(VerbsData.Save, "PSResource", DefaultParameterSetName = "IncludeXmlParameterSet", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Low)]
    public sealed class SavePSResource : PSCmdlet
    {
        #region Members

        private const string InputObjectParameterSet = "InputObjectParameterSet";
        private const string AsNupkgParameterSet = "AsNupkgParameterSet";
        private const string IncludeXmlParameterSet = "IncludeXmlParameterSet";
        InstallHelper _installHelper;

        #endregion

        #region Parameters

        /// <summary>
        /// Specifies the exact names of resources to save from a repository.
        /// A comma-separated list of module names is accepted. The resource name must match the resource name in the repository.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true, ParameterSetName = AsNupkgParameterSet, HelpMessage = "Name of the package(s) to save.")]
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true, ParameterSetName = IncludeXmlParameterSet, HelpMessage = "Name of the package(s) to save.")]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; }

        /// <summary>
        /// Specifies the version or version range of the package to be saved
        /// </summary>
        [Parameter(ParameterSetName = AsNupkgParameterSet, ValueFromPipelineByPropertyName = true)]
        [Parameter(ParameterSetName = IncludeXmlParameterSet, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }

        /// <summary>
        /// Specifies to allow saving of prerelease versions
        /// </summary>
        [Parameter(ParameterSetName = AsNupkgParameterSet, ValueFromPipelineByPropertyName = true)]
        [Parameter(ParameterSetName = IncludeXmlParameterSet, ValueFromPipelineByPropertyName = true)]
        [Alias("IsPrerelease")]
        public SwitchParameter Prerelease { get; set; }

        /// <summary>
        /// Specifies the specific repositories to search within.
        /// </summary>
        [SupportsWildcards]
        [Parameter(ValueFromPipelineByPropertyName = true)]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        /// <summary>
        /// Specifies a user account that has rights to save a resource from a specific repository.
        /// </summary>
        [Parameter]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// Saves the resource as a .nupkg
        /// </summary>
        [Parameter(ParameterSetName = AsNupkgParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public SwitchParameter AsNupkg { get; set; }

        /// <summary>
        /// Saves the metadata XML file with the resource
        /// </summary>
        [Parameter(ParameterSetName = IncludeXmlParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public SwitchParameter IncludeXml { get; set; }

        /// <summary>
        /// The destination where the resource is to be installed. Works for all resource types.
        /// </summary>
        [Parameter(Mandatory = true, HelpMessage = "Path to save the package to.")]
        [ValidateNotNullOrEmpty]
        public string Path
        {
            get
            { return _path; }

            set
            {
                if (WildcardPattern.ContainsWildcardCharacters(value))
                {
                    throw new PSArgumentException("Wildcard characters are not allowed in the path.");
                }

                // This will throw if path cannot be resolved
                _path = SessionState.Path.GetResolvedPSPathFromPSPath(value).First().Path;
            }
        }
        private string _path;

        /// <summary>
        /// The destination where the resource is to be temporarily saved to.

        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string TemporaryPath
        {
            get
            { return _tmpPath; }

            set
            {
                if (WildcardPattern.ContainsWildcardCharacters(value))
                {
                    throw new PSArgumentException("Wildcard characters are not allowed in the temporary path.");
                }

                // This will throw if path cannot be resolved
                _tmpPath = SessionState.Path.GetResolvedPSPathFromPSPath(value).First().Path;
            }
        }
        private string _tmpPath;

        /// <summary>
        /// Suppresses being prompted for untrusted sources.
        /// </summary>
        [Parameter]
        public SwitchParameter TrustRepository { get; set; }

        /// <summary>
        /// Passes the resource saved to the console.
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        /// <summary>
        /// Used for pipeline input.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true, ValueFromPipeline = true, ParameterSetName = InputObjectParameterSet, HelpMessage = "PSResourceInfo object representing the package to save.")]
        [ValidateNotNullOrEmpty]
        [Alias("ParentResource")]
        public PSResourceInfo InputObject { get; set; }

        /// <summary>
        /// Skips the check for resource dependencies, so that only found resources are saved,
        /// and not any resources the found resource depends on.
        /// </summary>
        [Parameter]
        public SwitchParameter SkipDependencyCheck { get; set; }

        /// <summary>
        /// Check validation for signed and catalog files

        /// </summary>
        [Parameter]
        public SwitchParameter AuthenticodeCheck { get; set; }

        /// <summary>
        /// Suppresses progress information.
        /// </summary>
        public SwitchParameter Quiet { get; set; }

        #endregion

        #region Method overrides

        protected override void BeginProcessing()
        {
            // Create a repository story (the PSResourceRepository.xml file) if it does not already exist
            // This is to create a better experience for those who have just installed v3 and want to get up and running quickly
            RepositorySettings.CheckRepositoryStore();

            var networkCred = Credential != null ? new NetworkCredential(Credential.UserName, Credential.Password) : null;

            _installHelper = new InstallHelper(cmdletPassedIn: this, networkCredential: networkCred);
        }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case AsNupkgParameterSet:
                case IncludeXmlParameterSet:
                    ProcessSaveHelper(
                        pkgNames: Name,
                        pkgVersion: Version,
                        pkgPrerelease: Prerelease,
                        pkgRepository: Repository);
                    break;

                case InputObjectParameterSet:
                    string normalizedVersionString = Utils.GetNormalizedVersionString(InputObject.Version.ToString(), InputObject.Prerelease);
                    ProcessSaveHelper(
                        pkgNames: new string[] { InputObject.Name },
                        pkgVersion: normalizedVersionString,
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

        private void ProcessSaveHelper(string[] pkgNames, string pkgVersion, bool pkgPrerelease, string[] pkgRepository)
        {
            var namesToSave = Utils.ProcessNameWildcards(pkgNames, removeWildcardEntries:false, out string[] errorMsgs, out bool nameContainsWildcard);
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

            // parse Version
            if (!Utils.TryGetVersionType(
                version: pkgVersion,
                nugetVersion: out NuGetVersion nugetVersion,
                versionRange: out VersionRange versionRange,
                versionType: out VersionType versionType,
                out string versionParseError))
            {
                var ex = new ArgumentException(versionParseError);
                var IncorrectVersionFormat = new ErrorRecord(ex, "IncorrectVersionFormat", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(IncorrectVersionFormat);
            }

            // figure out if version is a prerelease or not.
            // if condition is not met, prerelease is the value passed in via the parameter.
            if (!string.IsNullOrEmpty(pkgVersion) && pkgVersion.Contains('-')) {
                pkgPrerelease = true;
            }

            if (!ShouldProcess(string.Format("Resources to save: '{0}'", namesToSave)))
            {
                WriteVerbose(string.Format("Save operation cancelled by user for resources: {0}", namesToSave));
                return;
            }

            var installedPkgs = _installHelper.InstallPackages(
                names: namesToSave, 
                versionRange: versionRange,
                nugetVersion: nugetVersion,
                versionType: versionType,
                versionString: pkgVersion,
                prerelease: pkgPrerelease, 
                repository: pkgRepository, 
                acceptLicense: true, 
                quiet: Quiet, 
                reinstall: true, 
                force: false, 
                trustRepository: TrustRepository,
                noClobber: false, 
                asNupkg: AsNupkg, 
                includeXml: IncludeXml, 
                skipDependencyCheck: SkipDependencyCheck,
                authenticodeCheck: AuthenticodeCheck,
                savePkg: true,
                pathsToInstallPkg: new List<string> { _path },
                scope: null,
                tmpPath: _tmpPath,
                pkgsInstalled: new HashSet<string>(StringComparer.InvariantCultureIgnoreCase));

            if (PassThru)
            {
                foreach (PSResourceInfo pkg in installedPkgs)
                {
                    WriteObject(pkg);
                }
            }
        }

        #endregion
    }
}
