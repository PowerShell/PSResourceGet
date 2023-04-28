// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using Dbg = System.Diagnostics.Debug;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Install-PSResource cmdlet installs a resource.
    /// It returns nothing.
    /// </summary>
    [Cmdlet(VerbsLifecycle.Install, "PSResource", DefaultParameterSetName = "NameParameterSet", SupportsShouldProcess = true)]
    public sealed
    class InstallPSResource : PSCmdlet
    {
        #region Parameters 

        /// <summary>
        /// Specifies the exact names of resources to install from a repository.
        /// A comma-separated list of module names is accepted. The resource name must match the resource name in the repository.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true, ValueFromPipeline = true, ParameterSetName = NameParameterSet, HelpMessage = "Name(s) of the package(s) to install.")]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; }

        /// <summary>
        /// Specifies the version or version range of the package to be installed
        /// </summary>
        [SupportsWildcards]
        [Parameter(ParameterSetName = NameParameterSet, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }
        
        /// <summary>
        /// Specifies to allow installation of prerelease versions
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet, ValueFromPipelineByPropertyName = true)]
        [Alias("IsPrerelease")]
        public SwitchParameter Prerelease { get; set; }

        /// <summary>
        /// Specifies the repositories from which to search for the resource to be installed.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet, ValueFromPipelineByPropertyName = true)]
        [Parameter(ParameterSetName = InputObjectParameterSet, ValueFromPipelineByPropertyName = true)]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        /// <summary>
        /// Specifies a user account that has rights to find a resource from a specific repository.
        /// </summary>
        [Parameter]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// Specifies the scope of installation.
        /// </summary>
        [Parameter]
        public ScopeType Scope { get; set; }

        /// <summary>
        /// The destination where the resource is to be temporarily installed
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
        /// Overwrites a previously installed resource with the same name and version.
        /// </summary>
        [Parameter]
        public SwitchParameter Reinstall { get; set; }

        /// <summary>
        /// Suppresses progress information.
        /// </summary>
        [Parameter]
        public SwitchParameter Quiet { get; set; }

        /// <summary>
        /// For modules that require a license, AcceptLicense automatically accepts the license agreement during installation.
        /// </summary>
        [Parameter]
        public SwitchParameter AcceptLicense { get; set; }

        /// <summary>
        /// Prevents installing a package that contains cmdlets that already exist on the machine.
        /// </summary>
        [Parameter]
        public SwitchParameter NoClobber { get; set; }

        /// <summary>
        /// Skips the check for resource dependencies, so that only found resources are installed,
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
        /// Passes the resource installed to the console.
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        /// <summary>
        /// Used for pipeline input.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true, ValueFromPipeline = true, ParameterSetName = InputObjectParameterSet, HelpMessage = "PSResourceInfo object to install.")]
        [Alias("ParentResource")]
        [ValidateNotNullOrEmpty]
        public PSResourceInfo InputObject { get; set; }

        /// <summary>
        /// Installs resources based on input from a .psd1 (hashtable) or .json file.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = RequiredResourceFileParameterSet)]
        [ValidateNotNullOrEmpty]
        public String RequiredResourceFile
        {
            get
            {
                return _requiredResourceFile;
            }
            set
            {
                string resolvedPath = string.Empty;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    resolvedPath = SessionState.Path.GetResolvedPSPathFromPSPath(value).First().Path;
                }

                if (!File.Exists(resolvedPath))
                {
                    var exMessage = String.Format("The RequiredResourceFile does not exist.  Please try specifying a path to a valid .json or .psd1 file");
                    var ex = new ArgumentException(exMessage);
                    var RequiredResourceFileDoesNotExist = new ErrorRecord(ex, "RequiredResourceFileDoesNotExist", ErrorCategory.ObjectNotFound, null);

                    ThrowTerminatingError(RequiredResourceFileDoesNotExist);
                }

                if (resolvedPath.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase))
                {
                    _resourceFileType = ResourceFileType.JsonFile;
                }
                else if (resolvedPath.EndsWith(".psd1", StringComparison.InvariantCultureIgnoreCase))
                {
                    _resourceFileType = ResourceFileType.PSDataFile;
                }
                else
                {
                    // Throw here because no further processing can be done.
                    var exMessage = String.Format("The RequiredResourceFile must have either a '.json' or '.psd1' extension.  Please try specifying a path to a valid .json or .psd1 file");
                    var ex = new ArgumentException(exMessage);
                    var RequiredResourceFileNotValid = new ErrorRecord(ex, "RequiredResourceFileNotValid", ErrorCategory.ObjectNotFound, null);

                    ThrowTerminatingError(RequiredResourceFileNotValid);
                }

                _requiredResourceFile = resolvedPath;
            }
        }

        /// <summary>
        ///  Installs resources in a hashtable or JSON string format.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = RequiredResourceParameterSet)]
        public Object RequiredResource  // takes either string (json) or hashtable
        {
            get { return _requiredResourceHash != null ? _requiredResourceHash : (Object)_requiredResourceJson; }

            set
            {
                if (value is String jsonResource)
                {
                    _requiredResourceJson = jsonResource;
                }
                else if (value is Hashtable hashResource)
                {
                    _requiredResourceHash = hashResource;
                }
                else
                {
                    throw new ParameterBindingException("Object is not a JSON or Hashtable");
                }
            }
        }

        #endregion

        #region Enums

        private enum ResourceFileType
        {
            UnknownFile,
            JsonFile,
            PSDataFile
        }

        #endregion

        #region Members

        private const string NameParameterSet = "NameParameterSet";
        private const string InputObjectParameterSet = "InputObjectParameterSet";
        private const string RequiredResourceFileParameterSet = "RequiredResourceFileParameterSet";
        private const string RequiredResourceParameterSet = "RequiredResourceParameterSet";
        List<string> _pathsToInstallPkg;
        private string _requiredResourceFile;
        private string _requiredResourceJson;
        private Hashtable _requiredResourceHash;
        private HashSet<string> _packagesOnMachine;
        InstallHelper _installHelper;
        ResourceFileType _resourceFileType;

        #endregion

        #region Method Overrides

        protected override void BeginProcessing()
        {
            // Create a repository story (the PSResourceRepository.xml file) if it does not already exist
            // This is to create a better experience for those who have just installed v3 and want to get up and running quickly
            RepositorySettings.CheckRepositoryStore();

            _pathsToInstallPkg = Utils.GetAllInstallationPaths(this, Scope);
            List<string> pathsToSearch = Utils.GetAllResourcePaths(this, Scope);
            // Only need to find packages installed if -Reinstall is not passed in
            _packagesOnMachine = Reinstall ? new HashSet<string>(StringComparer.CurrentCultureIgnoreCase) : Utils.GetInstalledPackages(pathsToSearch, this);

            var networkCred = Credential != null ? new NetworkCredential(Credential.UserName, Credential.Password) : null;

            _installHelper = new InstallHelper(cmdletPassedIn: this, networkCredential: networkCred);
        }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case NameParameterSet:
                    ProcessInstallHelper(
                        pkgNames: Name,
                        pkgVersion: Version,
                        pkgPrerelease: Prerelease,
                        pkgRepository: Repository,
                        pkgCredential: Credential,
                        reqResourceParams: null);
                    break;
                    
                case InputObjectParameterSet:
                    string normalizedVersionString = Utils.GetNormalizedVersionString(InputObject.Version.ToString(), InputObject.Prerelease);
                    ProcessInstallHelper(
                        pkgNames: new string[] { InputObject.Name },
                        pkgVersion: normalizedVersionString,
                        pkgPrerelease: InputObject.IsPrerelease,
                        pkgRepository: new string[]{ InputObject.Repository },
                        pkgCredential: Credential,
                        reqResourceParams: null);
                    break;

                case RequiredResourceFileParameterSet:
                    /* .json file contents should look like:
                       {
                          "Pester": {
                            "allowPrerelease": true,
                            "version": "[4.4.2,4.7.0]",
                            "repository": "PSGallery",
                            "credential": null
                          }
                        }
                    */

                    /* .psd1 file contents should look like:
                       @{
                          "Configuration" =  @{ version = "[4.4.2,4.7.0]" }
                          "Pester" = @{
                             version = "[4.4.2,4.7.0]"
                             repository = PSGallery
                             credential = $cred
                             prerelease = $true
                          }
                       }
                    */

                    string requiredResourceFileStream = string.Empty;
                    using (StreamReader sr = new StreamReader(_requiredResourceFile))
                    {
                        requiredResourceFileStream = sr.ReadToEnd();
                    }

                    Hashtable pkgsInFile = null;
                    try
                    {
                        switch (_resourceFileType)
                        {
                            case ResourceFileType.JsonFile:
                                pkgsInFile = Utils.ConvertJsonToHashtable(this, requiredResourceFileStream);
                                break;

                            case ResourceFileType.PSDataFile:
                                if (!Utils.TryReadRequiredResourceFile(
                                    resourceFilePath: _requiredResourceFile,
                                    out pkgsInFile,
                                    out Exception error))
                                {
                                    throw error;
                                }
                                break;

                            case ResourceFileType.UnknownFile:
                                throw new PSInvalidOperationException(
                                    message: "Unkown file type. Required resource file must be either a json or psd1 data file.");
                        }
                    }
                    catch (Exception)
                    {
                        var exMessage = String.Format("Argument for parameter -RequiredResourceFile is not in proper json or hashtable format.  Make sure argument is either a valid .json or .psd1 file.");
                        var ex = new ArgumentException(exMessage);
                        var RequiredResourceFileNotInProperJsonFormat = new ErrorRecord(ex, "RequiredResourceFileNotInProperJsonFormat", ErrorCategory.InvalidData, null);

                        ThrowTerminatingError(RequiredResourceFileNotInProperJsonFormat);
                    }
                    
                    RequiredResourceHelper(pkgsInFile);
                    break;

                case RequiredResourceParameterSet:
                    if (!string.IsNullOrWhiteSpace(_requiredResourceJson))
                    {
                        /* json would look like:
                           {
                              "Pester": {
                                "allowPrerelease": true,
                                "version": "[4.4.2,4.7.0]",
                                "repository": "PSGallery",
                                "credential": null
                              }
                            }
                        */
                                              
                        Hashtable pkgsHash = null;
                        try
                        {
                            pkgsHash = Utils.ConvertJsonToHashtable(this, _requiredResourceJson);
                        }
                        catch (Exception)
                        {
                            var exMessage = String.Format("Argument for parameter -RequiredResource is not in proper json format.  Make sure argument is either a valid json file.");
                            var ex = new ArgumentException(exMessage);
                            var RequiredResourceFileNotInProperJsonFormat = new ErrorRecord(ex, "RequiredResourceFileNotInProperJsonFormat", ErrorCategory.InvalidData, null);

                            ThrowTerminatingError(RequiredResourceFileNotInProperJsonFormat);
                        }

                        RequiredResourceHelper(pkgsHash);
                    }

                    if (_requiredResourceHash != null)
                    {
                        /* hashtable would look like:
                            @{
                                "Configuration" =  @{ version = "[4.4.2,4.7.0]" }
                                "Pester" = @{
                                    version = "[4.4.2,4.7.0]"
                                    repository = PSGallery
                                    credential = $cred
                                    prerelease = $true
                                  }
                            }
                        */

                        RequiredResourceHelper(_requiredResourceHash);
                    }
                    break;

                default:
                    Dbg.Assert(false, "Invalid parameter set");
                    break;
            }
        }

        #endregion

        #region Private Methods

        private void RequiredResourceHelper(Hashtable reqResourceHash)
        {
            var pkgNames = reqResourceHash.Keys;

            foreach (string pkgName in pkgNames)
            {
                var pkgParamInfo = reqResourceHash[pkgName];

                // Format should now be a hashtable, whether the original input format was json or hashtable
                if (!(pkgParamInfo is Hashtable pkgInstallInfo))
                {
                    return;
                }

                InstallPkgParams pkgParams = new InstallPkgParams();
                var pkgParamNames = pkgInstallInfo.Keys;

                PSCredential pkgCredential = Credential;
                foreach (string paramName in pkgParamNames)
                {
                    if (string.Equals(paramName, "credential", StringComparison.InvariantCultureIgnoreCase))
                    {
                        WriteVerbose("Credential specified for required resource");
                        pkgCredential = pkgInstallInfo[paramName] as PSCredential;
                    }

                    pkgParams.SetProperty(paramName, pkgInstallInfo[paramName] as string, out ErrorRecord IncorrectVersionFormat);

                    if (IncorrectVersionFormat != null)
                    {
                        ThrowTerminatingError(IncorrectVersionFormat);
                    }
                }
                    
                if (pkgParams.Scope == ScopeType.AllUsers)
                {
                    _pathsToInstallPkg = Utils.GetAllInstallationPaths(this, pkgParams.Scope);
                }

                string pkgVersion = pkgInstallInfo["version"] == null ? String.Empty : pkgInstallInfo["version"].ToString();

                ProcessInstallHelper(
                    pkgNames: new string[] { pkgName },
                    pkgVersion: pkgVersion,
                    pkgPrerelease: pkgParams.Prerelease,
                    pkgRepository: new string[] { pkgParams.Repository },
                    pkgCredential: pkgCredential,
                    reqResourceParams: pkgParams);
            }
        }

        private void ProcessInstallHelper(string[] pkgNames, string pkgVersion, bool pkgPrerelease, string[] pkgRepository, PSCredential pkgCredential, InstallPkgParams reqResourceParams)
        {
            var inputNameToInstall = Utils.ProcessNameWildcards(pkgNames, removeWildcardEntries:false, out string[] errorMsgs, out bool nameContainsWildcard);
            if (nameContainsWildcard)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException("Name with wildcards is not supported for Install-PSResource cmdlet"),
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
            // but after filtering out unsupported wildcard names there are no elements left in namesToInstall
            if (inputNameToInstall.Length == 0)
            {
                return;
            }

            // parse version
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

            if (!ShouldProcess(string.Format("package to install: '{0}'", String.Join(", ", inputNameToInstall))))
            {
                WriteVerbose(string.Format("Install operation cancelled by user for packages: {0}", String.Join(", ", inputNameToInstall)));
                return;
            }

            var installedPkgs = _installHelper.InstallPackages(
                names: pkgNames,
                versionRange: versionRange,
                nugetVersion: nugetVersion,
                versionType: versionType,
                versionString: Version,
                prerelease: pkgPrerelease,
                repository: pkgRepository,
                acceptLicense: AcceptLicense,
                quiet: Quiet,
                reinstall: Reinstall,
                force: false,
                trustRepository: TrustRepository,
                noClobber: NoClobber,
                asNupkg: false,
                includeXml: true,
                skipDependencyCheck: SkipDependencyCheck,
                authenticodeCheck: AuthenticodeCheck,
                savePkg: false,
                pathsToInstallPkg: _pathsToInstallPkg,
                scope: Scope,
                tmpPath: _tmpPath,
                pkgsInstalled: _packagesOnMachine);

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
