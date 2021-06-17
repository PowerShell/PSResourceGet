// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
        SupportsShouldProcess = true,
        HelpUri = "<add>")]
    [OutputType(typeof(PSResourceInfo))]
    public sealed
    class FindPSResource : PSCmdlet
    {
        #region Members
        private const string ResourceNameParameterSet = "ResourceNameParameterSet";
        private const string CommandNameParameterSet = "CommandNameParameterSet";
        private const string DscResourceNameParameterSet = "DscResourceNameParameterSet";
        private const string TagParameterSet = "TagParameterSet";
        private const string TypeParameterSet = "TypeParameterSet";
        private CancellationTokenSource _source;
        private CancellationToken _cancellationToken;

        #endregion

        #region Parameters

        /// <summary>
        /// Specifies name of a resource or resources to find. Accepts wild card characters.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = ResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; }

        /// <summary>
        /// Specifies one or more resource types to find.
        /// Resource types supported are: Module, Script, Command, DscResource
        /// </summary>
        [Parameter(ParameterSetName = ResourceNameParameterSet)]
        [Parameter(Mandatory = true, ParameterSetName = TypeParameterSet)]
        // TODO: look at DebugMode, how multiple types are being passed in via cmd line
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
        [Parameter(ParameterSetName = CommandNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] CommandName { get; set; }

        /// <summary>
        /// Specifies a list of dsc resource names that searched module packages will provide. Wildcards are supported.
        /// </summary>
        [Parameter(ParameterSetName = DscResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] DscResourceName { get; set; }

        /// <summary>
        /// Filters search results for resources that include one or more of the specified tags.
        /// </summary>
        [Parameter(ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ParameterSetName = CommandNameParameterSet)]
        [Parameter(ParameterSetName = DscResourceNameParameterSet)]
        [Parameter(Mandatory = true, ParameterSetName = TagParameterSet)]
        [ValidateNotNull]
        public string[] Tag { get; set; }

        /// <summary>
        /// Specifies one or more repository names to search. If not specified, search will include all currently registered repositories.
        /// </summary>
        [Parameter(ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ParameterSetName = CommandNameParameterSet)]
        [Parameter(ParameterSetName = DscResourceNameParameterSet)]
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

        #region Methods

        protected override void BeginProcessing()
        {
            _source = new CancellationTokenSource();
            _cancellationToken = _source.Token;
        }

        protected override void StopProcessing()
        {
            _source.Cancel();
        }

        protected override void ProcessRecord()
        {
            Name = Utils.FilterOutWildcardNames(Name, out string[] errorMsgs);

            foreach (string error in errorMsgs)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException(error),
                    "ErrorFilteringNamesForUnsupportedWildcards",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            if (Name.Length == 0)
            {
                 return;
            }

            switch (ParameterSetName)
            {
                case ResourceNameParameterSet:
                    FindHelper findHelper = new FindHelper(_cancellationToken, this);
                    List<PSResourceInfo> foundPackages = new List<PSResourceInfo>();

                    foreach (PSResourceInfo package in findHelper.FindByResourceName(Name, Type, Version, Prerelease, Tag, Repository, Credential, IncludeDependencies))
                    {
                        foundPackages.Add(package);
                    }

                    foreach (var uniquePackageVersion in foundPackages.GroupBy(
                        m => new {m.Name, m.Version}).Select(
                            group => group.First()).ToList())
                    {
                        WriteObject(uniquePackageVersion);
                    }

                    break;

                case CommandNameParameterSet:
                    ThrowTerminatingError(new ErrorRecord(
                        new PSNotImplementedException("CommandNameParameterSet is not yet implemented. Please rerun cmdlet with other parameter set."),
                        "CommandParameterSetNotImplementedYet",
                        ErrorCategory.NotImplemented,
                        this));
                    break;

                case DscResourceNameParameterSet:
                    ThrowTerminatingError(new ErrorRecord(
                        new PSNotImplementedException("DscResourceNameParameterSet is not yet implemented. Please rerun cmdlet with other parameter set."),
                        "DscResourceParameterSetNotImplementedYet",
                        ErrorCategory.NotImplemented,
                        this));
                    break;

                case TagParameterSet:
                    ThrowTerminatingError(new ErrorRecord(
                        new PSNotImplementedException("DscResourceNameParameterSet is not yet implemented. Please rerun cmdlet with other parameter set."),
                        "TagParameterSetNotImplementedYet",
                        ErrorCategory.NotImplemented,
                        this));
                    break;

                case TypeParameterSet:
                    ThrowTerminatingError(new ErrorRecord(
                        new PSNotImplementedException("TypeParameterSet is not yet implemented. Please rerun cmdlet with other parameter set."),
                        "TypeParameterSetNotImplementedYet",
                        ErrorCategory.NotImplemented,
                        this));
                    break;

                default:
                    Dbg.Assert(false, "Invalid parameter set");
                    break;
            }
        }

        #endregion
    }
}
