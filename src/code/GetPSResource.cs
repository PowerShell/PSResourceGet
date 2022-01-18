// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// It retrieves a resource that was installed with Install-PSResource
    /// Returns a single resource or multiple resource.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "PSResource")]
    public sealed class GetPSResource : PSCmdlet
    {
        #region Members

        private VersionRange _versionRange;
        private List<string> _pathsToSearch;

        #endregion

        #region Parameters

        /// <summary>
        /// Specifies the desired name for the resource to look for.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true)]
        public string[] Name { get; set; }

        /// <summary>
        /// Specifies the version of the resource to include to look for. 
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string Version { get; set; }

        /// <summary>
        /// Specifies the path to look in. 
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string Path { get; set; }

        #endregion

        #region Methods

        protected override void BeginProcessing()
        {
            // Validate that if a -Version param is passed in that it can be parsed into a NuGet version range. 
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

            // Determine paths to search.
            _pathsToSearch = new List<string>();
            if (Path != null)
            {
                WriteVerbose(string.Format("Provided path is: '{0}'", Path));

                var resolvedPaths = SessionState.Path.GetResolvedPSPathFromPSPath(Path);
                if (resolvedPaths.Count != 1)
                {
                    ThrowTerminatingError(
                        new ErrorRecord(
                            new PSArgumentException("Error: Could not resolve provided Path argument into a single path."),
                            "ErrorInvalidPathArgument",
                            ErrorCategory.InvalidArgument,
                            this));
                }

                var resolvedPath = resolvedPaths[0].Path;
                WriteVerbose(string.Format("Provided resolved path is '{0}'", resolvedPath));

                var versionPaths = Utils.GetSubDirectories(resolvedPath);
                if (versionPaths.Length == 0)
                {
                    ThrowTerminatingError(
                        new ErrorRecord(
                            exception: new PSInvalidOperationException(
                                $"Error cannot find expected subdirectories in provided path: {Path}"),
                            "PathMissingExpectedSubdirectories",
                            ErrorCategory.InvalidOperation,
                            targetObject: null));
                }

                _pathsToSearch.AddRange(versionPaths);
                foreach (string anamPath in _pathsToSearch)
                {
                    WriteVerbose("currently searching path in if: " + anamPath);
                }
            }
            else
            {
                // retrieve all possible paths
                _pathsToSearch = Utils.GetAllResourcePaths(this);
                foreach (string anamPath in _pathsToSearch)
                {
                    WriteVerbose("currently searching path in else: " + anamPath);
                }
            }
        }

        protected override void ProcessRecord()
        {
            WriteVerbose("Entering GetPSResource");

            var namesToSearch = Utils.ProcessNameWildcards(Name, out string[] errorMsgs, out bool _);
            foreach (string error in errorMsgs)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(error),
                    "ErrorFilteringNamesForUnsupportedWildcards",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            // this catches the case where Name wasn't passed in as null or empty,
            // but after filtering out unsupported wildcard names in BeginProcessing() there are no elements left in Name
            if (namesToSearch.Length == 0)
            {
                 return;
            }

            GetHelper getHelper = new GetHelper(this);
            WriteVerbose("Anam, version range: " + _versionRange);
            foreach (PSResourceInfo pkg in getHelper.GetPackagesFromPath(namesToSearch, _versionRange, _pathsToSearch, prereleaseSwitch: false))
            {
                WriteObject(pkg);
            }
        }

        #endregion
    }
}
