// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Threading;

using Dbg = System.Diagnostics.Debug;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Find-PSResource cmdlet combines the Find-Module, Find-Script, Find-DscResource, Find-Command cmdlets from V2.
    /// It performs a search on a repository (local or remote) based on the -Name parameter argument.
    /// It returns PSResourceInfo objects which describe each resource item found.
    /// Other parameters allow the returned results to be filtered by item Type and Tag.
    /// </summary>
    [Cmdlet(VerbsCommon.Find,
        "PSResource",
        DefaultParameterSetName = NameParameterSet)]
    [OutputType(typeof(PSResourceInfo), typeof(PSCommandResourceInfo))]
    public sealed class FindPSResource : PSCmdlet
    {
        #region Members
        
        private const string NameParameterSet = "NameParameterSet";
        private const string CommandNameParameterSet = "CommandNameParameterSet";
        private const string DscResourceNameParameterSet = "DscResourceNameParameterSet";
        private CancellationTokenSource _cancellationTokenSource;
        private FindHelper _findHelper;

        #endregion

        #region Parameters

        /// <summary>
        /// Specifies name of a resource or resources to find. Accepts wild card characters.
        /// </summary>
        [SupportsWildcards]
        [Parameter(Position = 0, 
                   ValueFromPipeline = true,
                   ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; }

        /// <summary>
        /// Specifies one or more resource types to find.
        /// Resource types supported are: Module, Script
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public ResourceType Type { get; set; }

        /// <summary>
        /// Specifies the version of the resource to be found and returned. Wildcards are supported.
        /// </summary>
        [SupportsWildcards]
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }

        /// <summary>
        /// When specified, includes prerelease versions in search.
        /// </summary>
        [Parameter()]
        public SwitchParameter Prerelease { get; set; }

        /// <summary>
        /// Specifies a list of command names that searched module packages will provide. Wildcards are supported.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = CommandNameParameterSet, HelpMessage = "Command name(s) to search for in packages.")]
        [ValidateNotNullOrEmpty]
        public string[] CommandName { get; set; }

        /// <summary>
        /// Specifies a list of dsc resource names that searched module packages will provide.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = DscResourceNameParameterSet, HelpMessage = "DSC Resource name(s) to search for in packages.")]
        [ValidateNotNullOrEmpty]
        public string[] DscResourceName { get; set; }

        /// <summary>
        /// Filters search results for resources that include one or more of the specified tags.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNull]
        public string[] Tag { get; set; }

        /// <summary>
        /// Specifies one or more repository names to search. If not specified, search will include all currently registered repositories.
        /// </summary>
        [SupportsWildcards]
        [Parameter()]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        /// <summary>
        /// Specifies optional credentials to be used when accessing a repository.
        /// </summary>
        [Parameter()]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// When specified, search will return all matched resources along with any resources the matched resources depends on.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter IncludeDependencies { get; set; }

        #endregion

        #region Method Overrides

        protected override void BeginProcessing()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            var networkCred = Credential != null ? new NetworkCredential(Credential.UserName, Credential.Password) : null;

            _findHelper = new FindHelper(
                cancellationToken: _cancellationTokenSource.Token,
                cmdletPassedIn: this,
                networkCredential: networkCred);

            // Create a repository story (the PSResourceRepository.xml file) if it does not already exist
            // This is to create a better experience for those who have just installed v3 and want to get up and running quickly
            RepositorySettings.CheckRepositoryStore();
        }

        protected override void StopProcessing()
        {
            _cancellationTokenSource?.Cancel();
        }

        protected override void EndProcessing()
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case NameParameterSet:
                    ProcessResourceNameParameterSet();
                    break;

                case CommandNameParameterSet:
                    ProcessCommandOrDscParameterSet(isSearchingForCommands: true);
                    break;

                case DscResourceNameParameterSet:
                    ProcessCommandOrDscParameterSet(isSearchingForCommands: false);
                    break;

                default:
                    Dbg.Assert(false, "Invalid parameter set");
                    break;
            }
        }

        #endregion

        #region Private Methods

        private void ProcessResourceNameParameterSet()
        {
            // only cases where Name is allowed to not be specified is if Type or Tag parameters are
            if (!MyInvocation.BoundParameters.ContainsKey(nameof(Name)))
            {
                if (MyInvocation.BoundParameters.ContainsKey(nameof(Tag)))
                {
                    ProcessTags();
                    return;
                }
                else if (MyInvocation.BoundParameters.ContainsKey(nameof(Type)))
                {
                    Name = new string[] {"*"};
                }
                else
                {
                    ThrowTerminatingError(
                        new ErrorRecord(
                            new PSInvalidOperationException("Name parameter must be provided, unless Tag or Type parameters are used."),
                            "NameParameterNotProvided",
                            ErrorCategory.InvalidOperation,
                            this));
                }
            }

            Name = Utils.ProcessNameWildcards(Name, removeWildcardEntries:false, out string[] errorMsgs, out bool nameContainsWildcard);
            
            foreach (string error in errorMsgs)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(error),
                    "ErrorFilteringNamesForUnsupportedWildcards",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            // this catches the case where Name wasn't passed in as null or empty,
            // but after filtering out unsupported wildcard names there are no elements left in namesToSearch
            if (Name.Length == 0)
            {
                return;
            }         

            // determine/parse out Version param
            VersionType versionType = VersionType.VersionRange;
            NuGetVersion nugetVersion = null;
            VersionRange versionRange = null;

            if (Version != null)
            {
                if (!NuGetVersion.TryParse(Version, out nugetVersion))
                {
                    if (Version.Trim().Equals("*"))
                    {
                        versionRange = VersionRange.All;
                        versionType = VersionType.VersionRange;
                    }
                    else if (!VersionRange.TryParse(Version, out versionRange))
                    {
                        WriteError(new ErrorRecord(
                            new ArgumentException("Argument for -Version parameter is not in the proper format"),
                            "IncorrectVersionFormat",
                            ErrorCategory.InvalidArgument,
                            this));
                        return;
                    }
                }
                else
                {
                    versionType = VersionType.SpecificVersion;
                }
            }
            else
            {
                versionType = VersionType.NoVersion;
            }

            foreach (PSResourceInfo pkg in _findHelper.FindByResourceName(
                name: Name,
                type: Type,
                versionRange: versionRange,
                nugetVersion: nugetVersion,
                versionType: versionType,
                version: Version,
                prerelease: Prerelease,
                tag: Tag,
                repository: Repository,
                includeDependencies: IncludeDependencies))
            {
                WriteObject(pkg);
            }
        }

        private void ProcessCommandOrDscParameterSet(bool isSearchingForCommands)
        {
            var commandOrDSCNamesToSearch = Utils.ProcessNameWildcards(
                pkgNames: isSearchingForCommands ? CommandName : DscResourceName,
                removeWildcardEntries: true,
                errorMsgs: out string[] errorMsgs,
                isContainWildcard: out bool _);

            var paramName = isSearchingForCommands ? "CommandName" : "DscResourceName";
            foreach (string error in errorMsgs)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException($"Wildcards are not supported for -{paramName}: {error}"),
                    "WildcardsUnsupportedForCommandNameorDSCResourceName",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            // this catches the case where Name wasn't passed in as null or empty,
            // but after filtering out unsupported wildcard names there are no elements left in commandOrDSCNamesToSearch
            if (commandOrDSCNamesToSearch.Length == 0)
            {
                 return;
            }
            
            foreach (PSCommandResourceInfo cmdPkg in _findHelper.FindByCommandOrDscResource(
                isSearchingForCommands: isSearchingForCommands,
                prerelease: Prerelease,
                tag: commandOrDSCNamesToSearch,
                repository: Repository))
            {
                WriteObject(cmdPkg);
            }
        }

        private void ProcessTags()
        {
            var tagsToSearch = Utils.ProcessNameWildcards(
                pkgNames: Tag,
                removeWildcardEntries: true,
                errorMsgs: out string[] errorMsgs,
                isContainWildcard: out bool _);

            foreach (string error in errorMsgs)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException($"Wildcards are not supported for -Tag: {error}"),
                    "WildcardsUnsupportedForTag",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            // this catches the case where Name wasn't passed in as null or empty,
            // but after filtering out unsupported wildcard names there are no elements left in tagsToSearch
            if (tagsToSearch.Length == 0)
            {
                 return;
            }
            
            foreach (PSResourceInfo tagPkg in _findHelper.FindByTag(
                type: Type,
                prerelease: Prerelease,
                tag: tagsToSearch,
                repository: Repository))
            {
                WriteObject(tagPkg);
            }
        }

        #endregion
    }
}
