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

    [Cmdlet(VerbsData.Save,
        "PSResource",
        DefaultParameterSetName = NameParameterSet,
        SupportsShouldProcess = true,
        HelpUri = "<add>")]
    [OutputType(typeof(PSResourceInfo))]
    public sealed
    class SavePSResource : PSCmdlet
    {
        #region Members
        private const string NameParameterSet = "NameParameterSet";
        private const string InputObjectParameterSet = "InputObjectParameterSet";
        #endregion

        #region Parameters

        /// <summary>
        /// Specifies name of a resource or resources to save.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; }

        /// <summary>
        /// Specifies the version or version range of the resource to be saved.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }

        /// <summary>
        /// When specified, allow saving prerelease versions.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter Prerelease { get; set; }

        /// <summary>
        /// Specifies one or more repository names to search and save packages from.
        /// If not specified, search will include all currently registered repositories in order of highest priority.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        /// <summary>
        /// Specifies optional credentials to be used when accessing a private repository.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// When specified, saves the resource as a .nupkg.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter AsNupkg { get; set; }

        /// <summary>
        /// Saves the metadata XML file with the resource.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public string[] IncludeXML { get; set; }

        /// <summary>
        /// Specifies the destination where the resource is to be saved.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public string Path { get; set; }

        /// <summary>
        /// When specified, supresses being prompted for untrusted sources.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter TrustRepository { get; set; }

        /// <summary>
        /// Used to pass in an object via pipeline to save.
        /// </summary>
        [Parameter(ValueFromPipeline = true, ParameterSetName = NameParameterSet)]
        public PSCustomObject[] InputObject { get; set; }

        /// <summary>
        /// When specified, displays the succcessfully saved resource and its information.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter PassThru { get; set; }

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