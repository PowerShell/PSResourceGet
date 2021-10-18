// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Dbg = System.Diagnostics.Debug;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;

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
        DefaultParameterSetName = ResourceNameParameterSet,
        SupportsShouldProcess = true)]
    [OutputType(typeof(PSResourceInfo), typeof(PSCommandResourceInfo))]
    public sealed class FindPSResource : PSCmdlet
    {
        #region Members
        
        private const string ResourceNameParameterSet = "ResourceNameParameterSet";
        private const string CommandNameParameterSet = "CommandNameParameterSet";
        private const string DscResourceNameParameterSet = "DscResourceNameParameterSet";
        private CancellationTokenSource _source;
        private CancellationToken _cancellationToken;

        #endregion

        #region Parameters

        /// <summary>
        /// Specifies name of a resource or resources to find. Accepts wild card characters.
        /// </summary>
        [Parameter(Position = 0, 
                   ValueFromPipeline = true,
                   ParameterSetName = ResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; }

        /// <summary>
        /// Specifies one or more resource types to find.
        /// Resource types supported are: Module, Script, Command, DscResource
        /// </summary>
        [Parameter(ParameterSetName = ResourceNameParameterSet)]
        public ResourceType Type { get; set; }

        /// <summary>
        /// Specifies the version of the resource to be found and returned.
        /// </summary>
        [Parameter(ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ParameterSetName = CommandNameParameterSet)]
        [Parameter(ParameterSetName = DscResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }

        /// <summary>
        /// When specified, includes prerelease versions in search.
        /// </summary>
        [Parameter(ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ParameterSetName = CommandNameParameterSet)]
        [Parameter(ParameterSetName = DscResourceNameParameterSet)]
        public SwitchParameter Prerelease { get; set; }

        /// <summary>
        /// Specifies a module resource package name type to search for. Wildcards are supported.
        /// </summary>
        [Parameter(ParameterSetName = CommandNameParameterSet)]
        [Parameter(ParameterSetName = DscResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] ModuleName { get; set; }

        /// <summary>
        /// Specifies a list of command names that searched module packages will provide. Wildcards are supported.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = CommandNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] CommandName { get; set; }

        /// <summary>
        /// Specifies a list of dsc resource names that searched module packages will provide. Wildcards are supported.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = DscResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] DscResourceName { get; set; }

        /// <summary>
        /// Filters search results for resources that include one or more of the specified tags.
        /// </summary>
        [Parameter(ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ParameterSetName = CommandNameParameterSet)]
        [Parameter(ParameterSetName = DscResourceNameParameterSet)]
        [ValidateNotNull]
        public string[] Tag { get; set; }

        /// <summary>
        /// Specifies one or more repository names to search. If not specified, search will include all currently registered repositories.
        /// </summary>
        [Parameter(ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ParameterSetName = CommandNameParameterSet)]
        [Parameter(ParameterSetName = DscResourceNameParameterSet)]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        /// <summary>
        /// Specifies optional credentials to be used when accessing a repository.
        /// </summary>
        [Parameter(ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ParameterSetName = CommandNameParameterSet)]
        [Parameter(ParameterSetName = DscResourceNameParameterSet)]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// When specified, search will return all matched resources along with any resources the matched resources depends on.
        /// </summary>
        [Parameter(ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ParameterSetName = CommandNameParameterSet)]
        [Parameter(ParameterSetName = DscResourceNameParameterSet)]
        public SwitchParameter IncludeDependencies { get; set; }

        #endregion

        #region Method overrides

        protected override void BeginProcessing()
        {
            _source = new CancellationTokenSource();
            _cancellationToken = _source.Token;

            // Create a respository story (the PSResourceRepository.xml file) if it does not already exist
            // This is to create a better experience for those who have just installed v3 and want to get up and running quickly
            RepositorySettings.CheckRepositoryStore();
        }

        protected override void StopProcessing()
        {
            _source.Cancel();
        }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ResourceNameParameterSet:
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

        #region Private methods

        private void ProcessResourceNameParameterSet()
        {
            if (!MyInvocation.BoundParameters.ContainsKey(nameof(Name)))
            {
                // only cases where Name is allowed to not be specified is if Type or Tag parameters are
                if (!MyInvocation.BoundParameters.ContainsKey(nameof(Type)) && !MyInvocation.BoundParameters.ContainsKey(nameof(Tag)))
                {
                    ThrowTerminatingError(
                        new ErrorRecord(
                            new PSInvalidOperationException("Name parameter must be provided."),
                            "NameParameterNotProvided",
                            ErrorCategory.InvalidOperation,
                            this));
                }

                Name = new string[] {"*"};
            }

            Name = Utils.ProcessNameWildcards(Name, out string[] errorMsgs, out bool nameContainsWildcard);
            
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

            FindHelper findHelper = new FindHelper(_cancellationToken, this);
            List<PSResourceInfo> foundPackages = new List<PSResourceInfo>();

            foreach (PSResourceInfo package in findHelper.FindByResourceName(
                Name,
                Type,
                Version,
                Prerelease,
                Tag,
                Repository,
                Credential,
                IncludeDependencies))
            {
                foundPackages.Add(package);
            }

            foreach (var uniquePackageVersion in foundPackages.GroupBy(
                m => new {m.Name, m.Version, m.Repository}).Select(
                    group => group.First()).ToList())
            {
                WriteObject(uniquePackageVersion);
            }
        }

        private void ProcessCommandOrDscParameterSet(bool isSearchingForCommands)
        {
            var commandOrDSCNamesToSearch = Utils.ProcessNameWildcards(
                pkgNames: isSearchingForCommands ? CommandName : DscResourceName,
                errorMsgs: out string[] errorMsgs,
                isContainWildcard: out bool nameContainsWildcard);

            if (nameContainsWildcard)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException("Wilcards are not supported for -CommandName or -DSCResourceName for Find-PSResource. So all CommandName or DSCResourceName entries will be discarded."),
                    "CommandDSCResourceNameWithWildcardsNotSupported",
                    ErrorCategory.InvalidArgument,
                    this));
                return;
            }

            foreach (string error in errorMsgs)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(error),
                    "ErrorFilteringCommandDscResourceNamesForUnsupportedWildcards",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            // this catches the case where Name wasn't passed in as null or empty,
            // but after filtering out unsupported wildcard names there are no elements left in commandOrDSCNamesToSearch
            if (commandOrDSCNamesToSearch.Length == 0)
            {
                 return;
            }
            
            var moduleNamesToSearch = Utils.ProcessNameWildcards(
                pkgNames: ModuleName,
                errorMsgs: out string[] moduleErrorMsgs,
                isContainWildcard: out bool _);

            foreach (string error in moduleErrorMsgs)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(error),
                    "ErrorFilteringModuleNamesForUnsupportedWildcards",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            if (moduleNamesToSearch.Length == 0)
            {
                moduleNamesToSearch = new string[] {"*"};
            }

            FindHelper findHelper = new FindHelper(_cancellationToken, this);
            List<PSResourceInfo> foundPackages = new List<PSResourceInfo>();

            foreach (PSResourceInfo package in findHelper.FindByResourceName(
                name: moduleNamesToSearch,
                // provide type so Scripts endpoint for PSGallery won't be searched
                type: isSearchingForCommands? ResourceType.Command : ResourceType.DscResource,
                version: Version,
                prerelease: Prerelease,
                tag: Tag,
                repository: Repository,
                credential: Credential,
                includeDependencies: IncludeDependencies))
            {
                foundPackages.Add(package);
            }

            // if a single package contains multiple commands we are interested in, return a unique entry for each:
            // Command1 , PackageA
            // Command2 , PackageA        
            foreach (string nameToSearch in commandOrDSCNamesToSearch)
            {
                foreach (var package in foundPackages)
                {
                    // this check ensures DSC names provided as a Command name won't get returned mistakenly
                    // -CommandName "command1", "dsc1" <- (will not return or add DSC name)
                    if ((isSearchingForCommands && package.Includes.Command.Contains(nameToSearch)) ||
                        (!isSearchingForCommands && package.Includes.DscResource.Contains(nameToSearch)))
                    {
                        WriteObject(new PSCommandResourceInfo(nameToSearch, package));
                    }
                }
            }
        }

        #endregion
    }
}
