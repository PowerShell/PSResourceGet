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
    [Cmdlet(VerbsData.Save, "PSResource", DefaultParameterSetName = "NameParameterSet", SupportsShouldProcess = true)]
    public sealed class SavePSResource : PSCmdlet
    {
        #region Members

        private const string NameParameterSet = "NameParameterSet";
        private const string InputObjectParameterSet = "InputObjectParameterSet";
        VersionRange _versionRange;
        InstallHelper _installHelper;
        string nugetConfig;
        string nugetConfigOriginal;
        private bool savednugetConfigFileExistsOnMachine;

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
        [Parameter]
        public PSCredential Credential { get; set; }
        
        /// <summary>
        /// Saves the resource as a .nupkg
        /// </summary>
        [Parameter]
        public SwitchParameter AsNupkg { get; set; }

        /// <summary>
        /// Saves the metadata XML file with the resource
        /// </summary>
        [Parameter]
        public SwitchParameter IncludeXML { get; set; }

        /// <summary>
        /// The destination where the resource is to be installed. Works for all resource types.
        /// </summary>
        [Parameter]
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
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = InputObjectParameterSet)]
        [ValidateNotNullOrEmpty]
        public PSResourceInfo InputObject { get; set; }

        /// <summary>
        /// Skips the check for resource dependencies, so that only found resources are saved,
        /// and not any resources the found resource depends on.
        /// </summary>
        [Parameter]
        public SwitchParameter SkipDependencyCheck { get; set; }

        /// <summary>
        /// Suppresses progress information.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public SwitchParameter Quiet { get; set; }

        /// <summary>
        /// Specifies a proxy server for the request, rather than a direct connection to the internet resource.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public String Proxy { get; set; }

        /// <summary>
        /// Specifies a user account that has permission to use the proxy server that is specified by the Proxy parameter.
        /// </summary>
        [Parameter]
        public PSCredential ProxyCredential { get; set; }

        #endregion

        #region Method overrides

        protected override void BeginProcessing()
        {
            // ENV variable can be used:            
            // http://<IP-address>
            // http://<IP-address>:<port>
            // http://<username>:<password>@<proxy-URL>
            // http://<username>:<password>@<proxy-URL>:<port>
            // all of the above can use http or https
            // or parameters can be passed in:

            // open nuget.config file, save contents
            // then write the current proxy values to file 
            // in EndProcessing() revert the file to its original contents
            // OR
            // if no nuget.config file exits, create it and then delete it in EndProcessing()

            // Set proxy and proxy credentials if passed in
            if (!string.IsNullOrWhiteSpace(Proxy) || (ProxyCredential != null))
            {
                /*
                 @"<config>
                        <add key="http_proxy" value="http://my.proxy.address:port" />
                        <add key="http_proxy.user" value="mydomain\myUserName" />
                        <add key="http_proxy.password" value="base64encodedEncryptedPassword" />
                  <config>"
                  */


                var content = string.Empty;
                if (!string.IsNullOrWhiteSpace(Proxy))
                {
                    var httpProxy = $"<add key = \"http_proxy\" value=\"{Proxy}\" />";
                    content = httpProxy + "\n";
                }
                if (ProxyCredential != null)
                {
                    var username = $"<add key=\"http_proxy.user\" value=\"{ProxyCredential.UserName}\" />";
                    var password = $"<add key=\"http_proxy.password\" value=\"{ProxyCredential.Password}\" />";
                    content = content + username + "\n" + password + "\n";
                }
                var configContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?> \n" +
                                 "<configuration> \n" +
                                     "<config> \n" +
                                         $"{content}" +
                                     "</config> \n" +
                                 "</configuration>";

                // %appdata%\NuGet
                // TODO: check that this is the correct path for unix systems
                nugetConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NuGet", "nuget.config");
                nugetConfigOriginal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NuGet", "nuget.config-original");

                if (File.Exists(nugetConfig))
                {
                    // rename file to nuget.config-original
                    // in EndProcessing make sure nuget.config-original exists, swap names back
                    savednugetConfigFileExistsOnMachine = true;
                    File.Move(nugetConfig, nugetConfigOriginal);
                }
                // in EndpRocessing just delete the file 

                // create a new nuget.config file and write contents to it
                using (StreamWriter outputFile = new StreamWriter(nugetConfig))
                {
                    outputFile.WriteLine(configContent);
                }
            }

            // Create a repository story (the PSResourceRepository.xml file) if it does not already exist
            // This is to create a better experience for those who have just installed v3 and want to get up and running quickly
            RepositorySettings.CheckRepositoryStore();

            // If the user does not specify a path to save to, use the user's current working directory
            if (string.IsNullOrWhiteSpace(_path))
            {
                _path = SessionState.Path.CurrentLocation.Path;
            }

            _installHelper = new InstallHelper(cmdletPassedIn: this);
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
                    string normalizedVersionString = Utils.GetNormalizedVersionString(InputObject.Version.ToString(), InputObject.Prerelease);
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

        protected override void EndProcessing()
        {
            if (!string.IsNullOrWhiteSpace(Proxy) || (ProxyCredential != null))
            {
                File.Delete(nugetConfig);
                if (savednugetConfigFileExistsOnMachine)
                {
                    // rename file to back to nuget.config
                    File.Move(nugetConfigOriginal, nugetConfig);
                }
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

            var installedPkgs = _installHelper.InstallPackages(
                names: namesToSave, 
                versionRange: _versionRange, 
                prerelease: pkgPrerelease, 
                repository: pkgRepository, 
                acceptLicense: true, 
                quiet: Quiet, 
                reinstall: true, 
                force: false, 
                trustRepository: TrustRepository,
                credential: Credential, 
                noClobber: false, 
                asNupkg: AsNupkg, 
                includeXML: IncludeXML, 
                skipDependencyCheck: SkipDependencyCheck,
                savePkg: true,
                pathsToInstallPkg: new List<string> { _path });

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
