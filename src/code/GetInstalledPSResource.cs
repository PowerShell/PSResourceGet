// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    /// <summary>
    /// It retrieves a resource that was installed with Install-PSResource
    /// Returns a single resource or multiple resource.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "InstalledPSResource")]
    [Alias("Get-PSResource")]
    [OutputType(typeof(PSResourceInfo))]
    public sealed class GetInstalledPSResourceCommand : PSCmdlet
    {
        #region Members

        private VersionRange _versionRange;
        private List<string> _pathsToSearch;

        #endregion

        #region Parameters

        /// <summary>
        /// Specifies the desired name for the resource to look for.
        /// </summary>
        [SupportsWildcards]
        [Parameter(Position = 0, ValueFromPipeline = true)]
        public string[] Name { get; set; }

        /// <summary>
        /// Specifies the version of the resource to include to look for.
        /// </summary>
        [SupportsWildcards]
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string Version { get; set; }

        /// <summary>
        /// Specifies the path to look in.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string Path { get; set; }

        /// <summary>
        /// Specifies the scope of installation.
        /// </summary>
        [Parameter]
        public ScopeType Scope { get; set; }

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

                var resolvedPaths = GetResolvedProviderPathFromPSPath(Path, out ProviderInfo provider);
                if (resolvedPaths.Count != 1)
                {
                    ThrowTerminatingError(
                        new ErrorRecord(
                            new PSArgumentException("Error: Could not resolve provided Path argument into a single path."),
                            "ErrorInvalidPathArgument",
                            ErrorCategory.InvalidArgument,
                            this));
                }

                var resolvedPath = resolvedPaths[0];
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
            }
            else
            {
                // retrieve all possible paths
                _pathsToSearch = Utils.GetAllResourcePaths(this, Scope);
            }
        }

        protected override void ProcessRecord()
        {
            WriteVerbose("Entering GetInstalledPSResource");

            var namesToSearch = Utils.ProcessNameWildcards(Name, removeWildcardEntries:false, out string[] errorMsgs, out bool _);
            foreach (string error in errorMsgs)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(error),
                    "ErrorFilteringNamesForUnsupportedWildcards",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            // This catches the case where Name wasn't passed in as null or empty,
            // but after filtering out unsupported wildcard names in BeginProcessing() there are no elements left in Name.
            if (namesToSearch.Length == 0)
            {
                 return;
            }

            // SelectPrereleaseOnly is false because we want both stable and prerelease versions all the time..
            GetHelper getHelper = new GetHelper(this);
            List<string> pkgsFound = new List<string>();
            foreach (PSResourceInfo pkg in getHelper.GetPackagesFromPath(
                name: namesToSearch,
                versionRange: _versionRange,
                pathsToSearch: _pathsToSearch,
                selectPrereleaseOnly: false))
            {
                pkgsFound.Add(pkg.Name);
                WriteObject(pkg);
            }

            List<string> pkgsNotFound = new List<string>();
            foreach (string name in namesToSearch)
            {
                if (!pkgsFound.Contains(name, StringComparer.OrdinalIgnoreCase)) 
                {
                    if (name.Contains('*'))
                    {
                        WildcardPattern nameWildCardPattern = new WildcardPattern(name, WildcardOptions.IgnoreCase);

                        bool foundWildcardMatch = false;
                        foreach (string pkgFound in pkgsFound)
                        {
                            if (nameWildCardPattern.IsMatch(pkgFound))
                            {
                                foundWildcardMatch = true;
                                break;
                            }
                        }

                        if (!foundWildcardMatch)
                        {
                            pkgsNotFound.Add(name);
                        }
                    }
                    else
                    {
                        pkgsNotFound.Add(name);
                    }
                }
            }

            if (pkgsNotFound.Count > 0)
            {
                WriteError(new ErrorRecord(
                    new PackageNotFoundException($"No match was found for package '{string.Join(", ", pkgsNotFound)}'."),
                    "InstalledPackageNotFound",
                    ErrorCategory.ObjectNotFound,
                    this));
            }
        }

        #endregion
    }
}
