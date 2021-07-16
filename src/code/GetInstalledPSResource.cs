// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Threading;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// It retrieves a resource that was installed with Install-PSResource
    /// Returns a single resource or multiple resource.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "InstalledPSResource")]
    public sealed class GetInstalledPSResource : PSCmdlet
    {
        #region Members

        private VersionRange _versionRange;
        private List<string> _pathsToSearch;

        #endregion

        #region Parameters

        /// <summary>
        /// Specifies the desired name for the resource to look for.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public string[] Name { get; set; }

        /// <summary>
        /// Specifies the version of the resource to include to look for.
        /// </summary>
        [Parameter()]
        [ValidateNotNullOrEmpty()]
        public string Version { get; set; }

        /// <summary>
        /// Specifies the path to look in.
        /// </summary>
        [Parameter()]
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
                WriteDebug(string.Format("Provided path is: '{0}'", Path));

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
                WriteDebug(string.Format("Provided resolved path is '{0}'", resolvedPath));

                try
                {
                    _pathsToSearch.AddRange(Directory.GetDirectories(resolvedPath));
                }
                catch (Exception e)
                {
                    var exMessage = String.Format("Error retrieving directories from provided path '{0}': '{1}'.", Path, e.Message);
                    var ex = new ArgumentException(exMessage);
                    var ErrorRetrievingDirectories = new ErrorRecord(ex, "ErrorRetrievingDirectories", ErrorCategory.ResourceUnavailable, null);
                    ThrowTerminatingError(ErrorRetrievingDirectories);
                }
            }
            else
            {
                // retrieve all possible paths
                _pathsToSearch = Utils.GetAllResourcePaths(this);
            }

            if (Name == null)
            {
                Name = new string[] { "*" };
            }
            // if '*' is passed in as an argument for -Name with other -Name arguments,
            // ignore all arguments except for '*' since it is the most inclusive
            // eg:  -Name ["TestModule, Test*, *"]  will become -Name ["*"]
            if (Name != null && Name.Length > 1)
            {
                foreach (var pkgName in Name)
                {
                    if (pkgName.Trim().Equals("*"))
                    {
                        Name = new string[] { "*" };
                        break;
                    }
                }
            }
        }

        protected override void ProcessRecord()
        {
            WriteDebug("Entering GetInstalledPSResource");

            GetHelper getHelper = new GetHelper(this);
            foreach (PSResourceInfo pkg in getHelper.FilterPkgPaths(Name, _versionRange, _pathsToSearch))
            {
                WriteVerbose("version range for Get: " + _versionRange.ToString());
                WriteObject(pkg);
            }
        }

        #endregion
    }
}
