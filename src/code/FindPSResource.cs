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
    [OutputType(typeof(PSResourceInfo))]
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
                   ValueFromPipelineByPropertyName = true,
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
        public string ModuleName { get; set; }

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
                    ProcessCommandOrDscParameterSet();
                    break;

                case DscResourceNameParameterSet:
                    ProcessCommandOrDscParameterSet();
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
                // TODO: Add support for Tag and Type parameters without Name parameter being specified.
                if (MyInvocation.BoundParameters.ContainsKey(nameof(Type)) || MyInvocation.BoundParameters.ContainsKey(nameof(Tag)))
                {
                    ThrowTerminatingError(
                        new ErrorRecord(
                            new PSNotImplementedException("Search by Tag or Type parameter is not yet implemented."),
                            "TagTypeSearchNotYetImplemented",
                            ErrorCategory.NotImplemented,
                            this));
                }

                ThrowTerminatingError(
                    new ErrorRecord(
                        new PSInvalidOperationException("Name parameter must be provided."),
                        "NameParameterNotProvided",
                        ErrorCategory.InvalidOperation,
                        this));
            }
            
            var namesToSearch = Utils.ProcessNameWildcards(Name, out string[] errorMsgs, out bool nameContainsWildcard);
            
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
            if (namesToSearch.Length == 0)
            {
                 return;
            }

            if (String.Equals(namesToSearch[0], "*", StringComparison.InvariantCultureIgnoreCase))
            {
                // WriteVerbose("Package names were detected to be (or contain an element equal to): '*', so all packages will be updated");
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException("-Name '*' is not supported for Find-PSResource so all Name entries will be discarded."),
                    "NameEqualsWildcardIsNotSupported",
                    ErrorCategory.InvalidArgument,
                    this));
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
                m => new {m.Name, m.Version}).Select(
                    group => group.First()).ToList())
            {
                WriteObject(uniquePackageVersion);
            }
        }

        private void ProcessCommandOrDscParameterSet()
        {
            // can have commandName
            // or have commandName + moduleName
            // cannot have Command + DSCResource BOTH. bc one pset called at a time
            // cannot have neither Command nor DSCResource, bc pset wouldn't have been called.
            // add Dbg.Assert?
            bool isSearchingForCommands = (DscResourceName == null || DscResourceName.Length == 0);
            var commandOrDSCNamesToSearch = Utils.ProcessNameWildcards(isSearchingForCommands ? CommandName : DscResourceName,
                out string[] errorMsgs,
                out bool nameContainsWildcard);
            
            foreach (string error in errorMsgs)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(error),
                    "ErrorFilteringNamesForUnsupportedWildcards",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            // this catches the case where Name wasn't passed in as null or empty,
            // but after filtering out unsupported wildcard names there are no elements left in commandOrDSCNamesToSearch
            if (commandOrDSCNamesToSearch.Length == 0)
            {
                 return;
            }

            if (String.Equals(commandOrDSCNamesToSearch[0], "*", StringComparison.InvariantCultureIgnoreCase))
            {
                // WriteVerbose("Resource names were detected to be (or contain an element equal to): '*', so all packages will be updated");
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException("-CommandName '*' or -DSCResourceName '*' is not supported for Find-PSResource so all CommandName or DSCResourceName entries will be discarded."),
                    "CommandDSCResourceNameEqualsWildcardIsNotSupported",
                    ErrorCategory.InvalidArgument,
                    this));
                return;
            }

            // if ModuleName not specified search all packages (Name '*') w/ Type Command or DSC
            // if ModuleName is  specified, provide that new string[] {ModuleName} as Name w/ that type

            FindHelper findHelper = new FindHelper(_cancellationToken, this);
            List<PSResourceInfo> foundPackages = new List<PSResourceInfo>();

            if (String.IsNullOrEmpty(ModuleName))
            {
                foreach (PSResourceInfo package in findHelper.FindByResourceName(
                    name: new string[]{"*"},
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
            }
            else
            {
                foreach (PSResourceInfo package in findHelper.FindByResourceName(
                    name: new string[]{ModuleName},
                    type: isSearchingForCommands ? ResourceType.Command : ResourceType.DscResource,
                    version: Version,
                    prerelease: Prerelease,
                    tag: Tag,
                    repository: Repository,
                    credential: Credential,
                    includeDependencies: IncludeDependencies))
                {
                    foundPackages.Add(package);
                }
            }

            WriteVerbose("packages before type filtering by name: " + foundPackages.Count());
            WriteVerbose("namesToSearch count: " + commandOrDSCNamesToSearch.Count());

            // -CommandName "command1", "dsc1" <- should not return or add DSC name
            List<PSIncludedResourceInfo> resourcesWithCorrectCommandOrDSC = new List<PSIncludedResourceInfo>();
            foreach (string resourceName in commandOrDSCNamesToSearch)
            {
                WriteVerbose("resource name: " + resourceName);
                // TODO: question here, if package contained multiple commands we are interested in,
                // we'd return:
                // Command1 , PackageA
                // Command2 , PackageB (right?, not make the packages unique! so I think below is ok)
                foreach (var uniquePkgsWithType in foundPackages)
                {
                    // WriteVerbose("uniquepkg name: " + uniquePkgsWithType.Name);
                    if (isSearchingForCommands && uniquePkgsWithType.Includes.Command.Contains(resourceName))
                    {
                        resourcesWithCorrectCommandOrDSC.Add(new PSIncludedResourceInfo(resourceName, uniquePkgsWithType));
                        WriteVerbose("Command Added " + resourceName + " from " + uniquePkgsWithType.Name);
                    }
                    else if (!isSearchingForCommands && uniquePkgsWithType.Includes.DscResource.Contains(resourceName))
                    {
                        resourcesWithCorrectCommandOrDSC.Add(new PSIncludedResourceInfo(resourceName, uniquePkgsWithType));
                        WriteVerbose("DSC Added " + resourceName + " from " + uniquePkgsWithType.Name);
                    }
                }
            }

            foreach (PSIncludedResourceInfo resource in resourcesWithCorrectCommandOrDSC)
            {
                WriteObject(resource);
            }


            // foreach (var uniquePackageVersion in foundPackages.GroupBy(
            //     m => new {m.Name, m.Version}).Select(
            //         group => group.First()).ToList())
            // {
            //     WriteObject(uniquePackageVersion);
            // }
        }
        #endregion
    }
}
