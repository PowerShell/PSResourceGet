// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
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
    [Alias("cmpres")]
    public sealed class CompressPSResource : PSCmdlet
    {
        #region Parameters

        private PublishPSResource _publishPSResource;

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
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string DestinationPath { get; set; }

        #endregion

        #region Method Overrides

        protected override void BeginProcessing()
        {
            // Create a respository store (the PSResourceRepository.xml file) if it does not already exist
            // This is to create a better experience for those who have just installed v3 and want to get up and running quickly
            RepositorySettings.CheckRepositoryStore();

            string resolvedPath = null;
            string resolvedDestinationPath = null;

            try
            {
                resolvedPath = GetResolvedProviderPathFromPSPath(Path, out ProviderInfo provider).First();
            }
            catch (MethodInvocationException)
            {
                // path does not exist
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException("The path to the resource to publish does not exist, point to an existing path or file of the module or script to publish."),
                    "SourcePathDoesNotExist",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            if (!String.IsNullOrEmpty(DestinationPath))
            {
                resolvedDestinationPath = GetResolvedProviderPathFromPSPath(DestinationPath, out ProviderInfo provider).First();

                if (Directory.Exists(resolvedDestinationPath))
                {
                    DestinationPath = resolvedDestinationPath;
                }
                else
                {
                    try
                    {
                        Directory.CreateDirectory(resolvedDestinationPath);
                    }
                    catch (Exception e)
                    {
                        ThrowTerminatingError(new ErrorRecord(
                            new ArgumentException($"Destination path does not exist and cannot be created: {e.Message}"),
                            "InvalidDestinationPath",
                            ErrorCategory.InvalidArgument,
                            this));
                    }
                }
            }

            _publishPSResource = new PublishPSResource(true, Path, resolvedDestinationPath, resolvedPath);

            try
            {
                _publishPSResource.CheckAllParameterPaths();
            }
            catch (Exception e)
            {
                WriteError(new ErrorRecord(
                    new ArgumentException(e.Message),
                    "InvalidPath",
                    ErrorCategory.InvalidArgument,
                    this));
            }
        }

        protected override void EndProcessing()
        {
            try
            {
                _publishPSResource.PackAndPush();
            }
            catch (Exception e)
            {
                WriteError(new ErrorRecord(
                    new ArgumentException(e.Message),
                    "InvalidPath",
                    ErrorCategory.InvalidArgument,
                    this));
            }
        }

        #endregion

    }
}