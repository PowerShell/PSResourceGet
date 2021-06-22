// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Dbg = System.Diagnostics.Debug;
using System.Management.Automation;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Save-PSResource cmdlet combines the Save-Module, Save-Script cmdlets from V2.
    /// It saves from a package found from a repository (local or remote) based on the -Name parameter argument.
    /// It does not return an object. Other parameters allow the returned results to be further filtered.
    /// </summary>

    [Cmdlet(VerbsData.Update,
        "PSResource",
        DefaultParameterSetName = NameParameterSet,
        SupportsShouldProcess = true,
        HelpUri = "<add>")]
    [OutputType(typeof(PSResourceInfo))]
    public sealed
    class UpdatePSResource : PSCmdlet
    {
        #region Members

        private const string NameParameterSet = "NameParameterSet";
        private const string InputObjectParameterSet = "InputObjectParameterSet";

        #endregion

        #region Parameters

        /// <summary>
        /// Specifies name of a resource or resources to update.
        /// Accepts wildcard characters.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; }

        /// <summary>
        /// Specifies the version the resource is to be updated to.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }

        /// <summary>
        /// When specified, allows updating to a prerelease version.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter Prerelease { get; set; }

        /// <summary>
        /// Specifies one or more repository names to update packages from.
        /// If not specified, search will include all currently registered repositories in order of highest priority.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        /// <summary>
        /// Specifies the scope of the resource to update.
        /// </summary>
        // TODO: make enum with allowedValues: 'CurrentUser', 'AllUsers'
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Scope { get; set; }

        /// <summary>
        /// When specified, supresses being prompted for untrusted sources.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public SwitchParameter TrustRepository { get; set; }

        /// <summary>
        /// Specifies optional credentials to be used when accessing a private repository.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// Supresses progress information.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public SwitchParameter Quiet { get; set; }

        /// <summary>
        /// For resources that require a license, AcceptLicense automatically accepts the license agreement during the update.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public SwitchParameter AcceptLicense { get; set; }

        /// <summary>
        /// Prevents updating modules that have the same cmdlets as a differently named module already.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = InputObjectParameterSet)]
        public SwitchParameter NoClobber { get; set; }

        /// <summary>
        /// Used to pass in an object via pipeline to update.
        /// </summary>
        [Parameter(ValueFromPipeline = true, ParameterSetName = NameParameterSet)]
        public object[] InputObject { get; set; }

        #endregion

        #region Methods
        protected override void ProcessRecord()
        {
            // TODO: remove all wildcards?
            // Name = Utils.FilterOutWildcardNames(Name, out string[] errorMsgs);

            // foreach (string error in errorMsgs)
            // {
            //     WriteError(new ErrorRecord(
            //         new PSInvalidOperationException(error),
            //         "ErrorFilteringNamesForUnsupportedWildcards",
            //         ErrorCategory.InvalidArgument,
            //         this));
            // }

            if (Name.Length == 0)
            {
                 return;
            }

            switch (ParameterSetName)
            {
                case NameParameterSet:
                    // TODO
                    // FindHelper findHelper = new FindHelper(_cancellationToken, this);
                    // List<PSResourceInfo> foundPackages = new List<PSResourceInfo>();

                    // foreach (PSResourceInfo package in findHelper.FindByResourceName(Name, Type, Version, Prerelease, Tag, Repository, Credential, IncludeDependencies))
                    // {
                    //     foundPackages.Add(package);
                    // }

                    // foreach (var uniquePackageVersion in foundPackages.GroupBy(
                    //     m => new {m.Name, m.Version}).Select(
                    //         group => group.First()).ToList())
                    // {
                    //     WriteObject(uniquePackageVersion);
                    // }

                    break;

                case InputObjectParameterSet:
                    // TODO
                    break;

                default:
                    Dbg.Assert(false, "Invalid parameter set");
                    break;
            }
        }

        #endregion
    }
}