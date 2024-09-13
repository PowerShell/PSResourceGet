// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System.IO;
using System.Linq;
using System.Management.Automation;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    /// <summary>
    /// Compresses a module, script, or nupkg to a designated repository.
    /// </summary>
    [Cmdlet(VerbsData.Compress,
        "PSResource",
        SupportsShouldProcess = true)]
    [Alias("cmres")]
    public sealed class CompressPSResource : PSCmdlet
    {
        #region Parameters

        /// <summary>
        /// Specifies the path to the resource that you want to compress. This parameter accepts the path to the folder that contains the resource.
        /// Specifies a path to one or more locations. Wildcards are permitted. The default location is the current directory (.).
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "Path to the resource to be compressed.")]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; }

        /// <summary>
        /// Specifies the path where the compressed resource (as a .nupkg file) should be saved.
        /// This parameter allows you to save the package to a specified location on the local file system.
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, HelpMessage = "Path to save the compressed resource.")]
        [ValidateNotNullOrEmpty]
        public string DestinationPath { get; set; }

        /// <summary>
        /// When specified, passes the full path of the nupkg through the pipeline.
        /// </summary>
        [Parameter(Mandatory = false, HelpMessage = "Pass the full path of the nupkg through the pipeline")]
        public SwitchParameter PassThru { get; set; }

        /// <summary>
        /// Bypasses validating a resource module manifest before compressing.
        /// </summary>
        [Parameter]
        public SwitchParameter SkipModuleManifestValidate { get; set; }

        #endregion

        #region Members

        private PublishHelper _publishHelper;

        #endregion

        #region Method Overrides

        protected override void BeginProcessing()
        {
            // Create a respository store (the PSResourceRepository.xml file) if it does not already exist
            // This is to create a better experience for those who have just installed v3 and want to get up and running quickly
            RepositorySettings.CheckRepositoryStore();

            _publishHelper = new PublishHelper(
                this,
                Path,
                DestinationPath,
                PassThru,
                SkipModuleManifestValidate);

            _publishHelper.CheckAllParameterPaths();
        }

        protected override void EndProcessing()
        {
            _publishHelper.PackResource();
        }

        #endregion

    }
}
