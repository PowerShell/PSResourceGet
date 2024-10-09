// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Threading;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    /// <summary>
    /// Publishes a module, script, or nupkg to a designated repository.
    /// </summary>
    [Cmdlet(VerbsData.Publish,
        "PSResource",
        SupportsShouldProcess = true)]
    [Alias("pbres")]
    public sealed class PublishPSResource : PSCmdlet, IDynamicParameters
    {
        #region Parameters

        private const string PathParameterSet = "PathParameterSet";
        private const string NupkgPathParameterSet = "NupkgPathParameterSet";
        private ContainerRegistryDynamicParameters _pkgPrefix;

        /// <summary>
        /// Specifies the API key that you want to use to publish a module to the online gallery.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string ApiKey { get; set; }

        /// <summary>
        /// Specifies the repository to publish to.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        public string Repository { get; set; }

        /// <summary>
        /// Specifies the path to the resource that you want to publish. This parameter accepts the path to the folder that contains the resource.
        /// Specifies a path to one or more locations. Wildcards are permitted. The default location is the current directory (.).
        /// </summary>
        [Parameter (Mandatory = true, Position = 0, ParameterSetName = PathParameterSet, HelpMessage = "Path to the resource to be published.")]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; }

        /// <summary>
        /// Specifies the path to where the resource (as a nupkg) should be saved to. This parameter can be used in conjunction with the
        /// -Repository parameter to publish to a repository and also save the exact same package to the local file system.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string DestinationPath { get; set; }

        /// <summary>
        /// Specifies a user account that has rights to a specific repository (used for finding dependencies).
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// Bypasses the default check that all dependencies are present.
        /// </summary>
        [Parameter]
        public SwitchParameter SkipDependenciesCheck { get; set; }

        /// <summary>
        /// Bypasses validating a resource module manifest before publishing.
        /// </summary>
        [Parameter]
        public SwitchParameter SkipModuleManifestValidate { get; set; }

        /// <summary>
        /// Specifies a proxy server for the request, rather than a direct connection to the internet resource.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public Uri Proxy {
            set
            {
                if (value != null)
                {
                    WriteError(new ErrorRecord(
                        new ArgumentException("Not yet implemented."),
                        "ProxyNotImplemented",
                        ErrorCategory.InvalidData,
                        this));
                }
            }
        }

        /// <summary>
        /// Specifies a user account that has permission to use the proxy server that is specified by the Proxy parameter.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public PSCredential ProxyCredential {
            set
            {
                if (value != null)
                {
                    WriteError(new ErrorRecord(
                        new ArgumentException("Not yet implemented."),
                        "ProxyCredentialNotImplemented",
                        ErrorCategory.InvalidData,
                        this));
                }
            }
        }

        [Parameter(Mandatory = true, ParameterSetName = NupkgPathParameterSet, HelpMessage = "Path to the resource to be published.")]
        [ValidateNotNullOrEmpty]
        public string NupkgPath { get; set; }

        #endregion

        #region DynamicParameters
        public object GetDynamicParameters()
        {
            PSRepositoryInfo repository = RepositorySettings.Read(new[] { Repository }, out string[] _).FirstOrDefault();
            if (repository is not null && repository.ApiVersion == PSRepositoryInfo.APIVersion.ContainerRegistry)
            {
                _pkgPrefix = new ContainerRegistryDynamicParameters();
                return _pkgPrefix;
            }

            return null;
        }

        #endregion

        #region Members

        private CancellationToken _cancellationToken;
        private NetworkCredential _networkCredential;
        private bool _isNupkgPathSpecified = false;
        private PublishHelper _publishHelper;

        #endregion

        #region Method overrides

        protected override void BeginProcessing()
        {
            _cancellationToken = new CancellationToken();

            _networkCredential = Credential != null ? new NetworkCredential(Credential.UserName, Credential.Password) : null;

            if (!string.IsNullOrEmpty(NupkgPath))
            {
                _isNupkgPathSpecified = true;
                Path = NupkgPath;
            }

            // Create a respository story (the PSResourceRepository.xml file) if it does not already exist
            // This is to create a better experience for those who have just installed v3 and want to get up and running quickly
            RepositorySettings.CheckRepositoryStore();

            _publishHelper = new PublishHelper(
                this,
                Credential,
                ApiKey,
                Path,
                DestinationPath,
                SkipModuleManifestValidate,
                _cancellationToken,
                _isNupkgPathSpecified);

            _publishHelper.CheckAllParameterPaths();
        }

        protected override void EndProcessing()
        {
            if (!_isNupkgPathSpecified)
            {
                _publishHelper.PackResource();
            }

            if (_publishHelper.ScriptError || !_publishHelper.ShouldProcess)
            {
                return;
            }

            string modulePrefix = _pkgPrefix?.ModulePrefix;

            _publishHelper.PushResource(Repository, modulePrefix, SkipDependenciesCheck, _networkCredential);
        }

        #endregion

    }

    public class ContainerRegistryDynamicParameters
    {
        [Parameter]
        public string ModulePrefix { get; set; }
    }
}
