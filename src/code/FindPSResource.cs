using System.Text.RegularExpressions;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Management.Automation;
using System.Collections.Generic;
using NuGet.Configuration;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System.Threading;
using MoreLinq.Extensions;
using NuGet.Versioning;
using System.Data;
using System.Linq;
using System.Net;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using static System.Environment;
using Dbg = System.Diagnostics.Debug;
using NuGet.CatalogReader;
using System.IO;

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
        DefaultParameterSetName = "ResourceNameParameterSet",
        SupportsShouldProcess = true,
        HelpUri = "<add>")]
    public sealed
    class FindPSResource : PSCmdlet
    {
        #region Members
        private const string ResourceNameParameterSet = "ResourceNameParameterSet";
        private const string CommandNameParameterSet = "CommandNameParameterSet";
        private const string DscResourceNameParameterSet = "DscResourceNameParameterSet";
        // TODO: look into whether cancellationtoken needed for
        private CancellationToken _cancellationToken;
        // NuGet's SearchAsync() API takes a top parameter of 6000, but testing shows for PSGallery
        // usually a max of around 5990 is returned while more are left to retrieve in a second SearchAsync() call
        private const int SearchAsyncMaxReturned = 5990;

        #endregion

        #region Parameters

        /// <summary>
        /// Specifies name of a resource or resources to find. Accepts wild card characters.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = ResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; }

        /// <summary>
        /// Specifies one or more resource types to find. Resource types supported are: Module, Script, Command, DscResource, RoleCapability
        /// </summary>
        [Parameter(ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = ResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public ResourceType? Type { get; set; }

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
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = CommandNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = DscResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string ModuleName { get; set; }

        /// <summary>
        /// Specifies a list of command names that searched module packages will provide. Wildcards are supported.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = CommandNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] CommandName { get; set; }

        /// <summary>
        /// Specifies a list of dsc resource names that searched module packages will provide. Wildcards are supported.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = DscResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] DscResourceName { get; set; }

        /// <summary>
        /// Filters search results for resources that include one or more of the specified tags.
        /// </summary>
        [Parameter(ValueFromPipeline = true, ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ValueFromPipeline = true, ParameterSetName = CommandNameParameterSet)]
        [Parameter(ValueFromPipeline = true, ParameterSetName = DscResourceNameParameterSet)]
        [ValidateNotNull]
        public string[] Tag { get; set; }

        /// <summary>
        /// Specifies one or more repository names to search. If not specified, search will include all currently registered repositories.
        /// </summary>
        [Parameter(ValueFromPipeline = true, ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ValueFromPipeline = true, ParameterSetName = CommandNameParameterSet)]
        [Parameter(ValueFromPipeline = true, ParameterSetName = DscResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        /// <summary>
        /// Specifies optional credentials to be used when accessing a repository.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = CommandNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = DscResourceNameParameterSet)]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// When specified, search will return all matched resources along with any resources the matched resources depends on.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = CommandNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = DscResourceNameParameterSet)]
        public SwitchParameter IncludeDependencies { get; set; }

        #endregion

        #region Methods

        protected override void BeginProcessing()
        {
            _cancellationToken = new CancellationTokenSource().Token;

            // TODO: Write error and don't handle other cases: -Name "a", "*", "b*"
            int wildcardIndex = Array.FindIndex(Name, p => String.Equals(p, "*", StringComparison.CurrentCultureIgnoreCase));
            if (wildcardIndex != -1)
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException("-Name '*' is not supported for Find-PSResource. Please use the TODO-cmdlet"),
                    "NameStarNotSupported",
                    ErrorCategory.InvalidArgument,
                    this));

                Name = Name.Where(p => !String.Equals(p, "*", StringComparison.CurrentCultureIgnoreCase)).ToArray();
            }

            // ThrowTerminatingError()
        }

        protected override void StopProcessing()
        {
            // TODO:
            // CancellationTokenSource.Cancel
            // useful for cmdlet handling pipeline input
            // check that when this cancels, it doesn't throw or handle if it does
        }


        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ResourceNameParameterSet:
                    FindHelper findHelper = new FindHelper(_cancellationToken, this);
                    foreach (PSResourceInfo pkgObj in findHelper.FindByResourceName(Name, Type, Version, Prerelease, Tag, Repository, Credential))
                    {
                        WriteObject(pkgObj);
                    }
                    break;

                case CommandNameParameterSet:
                    ThrowTerminatingError(new ErrorRecord(
                        new PSNotImplementedException("CommandNameParameterSet is not yet implemented. Please rerun cmdlet with other parameter set."),
                        "ParameterSetNotImplementedYet",
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

                default:
                    Dbg.Assert(false, "Invalid parameter set");
                    break;
            }
        }




        #endregion

        #region HelperMethods
        private IEnumerable<PSResourceInfo> ResourceNameParameterSetHelper()
        {
            if (Name.Length == 0)
            {
                yield break;
            }

            List<string> pkgsLeftToFind = Name.ToList();
            List<PSRepositoryInfo> repositoriesToSearch = new List<PSRepositoryInfo>();

            try
            {
                repositoriesToSearch = RepositorySettings.Read(Repository, out string[] errorList);

                foreach (string error in errorList)
                {
                    WriteError(new ErrorRecord(
                        new PSInvalidOperationException(error),
                        "ErrorGettingSpecifiedRepo",
                        ErrorCategory.InvalidOperation,
                        this));
                }
            }
            catch (Exception e)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new PSInvalidOperationException(e.Message),
                    "ErrorLoadingRepositoryStoreFile",
                    ErrorCategory.InvalidArgument,
                    this));
                yield break;
            }

            for (int i = 0; i < repositoriesToSearch.Count && pkgsLeftToFind.Any(); i++)
            {
                if (String.Equals(repositoriesToSearch[i].Name, "PSGallery", StringComparison.OrdinalIgnoreCase))
                {
                    // for PowerShellGallery, Modules and Scripts have different endpoints so separate repositories have to be registered
                    // with those endpoints in order for the NuGet APIs to search across both (in the case where name includes *)

                    // detect if Scripts repository needs to be added and/or Modules repository needs to be skipped
                    Uri psGalleryScriptsUrl = new Uri("http://www.powershellgallery.com/api/v2/items/psscript/");
                    PSRepositoryInfo psGalleryScripts = new PSRepositoryInfo("PSGalleryScripts", psGalleryScriptsUrl, 50, false);

                    if (Type == null)
                    {
                        WriteDebug("Null Type provided, so add PSGalleryScripts repository");
                        repositoriesToSearch.Add(psGalleryScripts);
                    }
                    else if (Type != null && Type == ResourceType.Script)
                    {
                        WriteDebug("Type Script provided, so add PSGalleryScripts and remove PSGallery (Modules only)");
                        repositoriesToSearch.Add(psGalleryScripts);
                        // skip current repository PSGallery (Modules) since Module Type not specified
                        continue;
                    }
                }
                WriteDebug(string.Format("Searching in repository {0}", repositoriesToSearch[i].Name));
                foreach (var pkg in SearchFromRepository(repositoriesToSearch[i].Name, repositoriesToSearch[i].Url, pkgsLeftToFind, _cancellationToken))
                {
                    yield return pkg;
                }
            }
        }

        public IEnumerable<PSResourceInfo> SearchFromRepository(string repoName, Uri repositoryUrl, List<string> pkgsLeftToFind, CancellationToken cancellationToken)
        {
            PackageSearchResource resourceSearch = null;
            PackageMetadataResource resourceMetadata = null;
            SearchFilter filter = null;
            SourceCacheContext context = null;

            // file based Uri scheme
            if (repositoryUrl.Scheme == Uri.UriSchemeFile)
            {
                FindLocalPackagesResourceV2 localResource = new FindLocalPackagesResourceV2(repositoryUrl.ToString());
                resourceSearch = new LocalPackageSearchResource(localResource);
                resourceMetadata = new LocalPackageMetadataResource(localResource);
                filter = new SearchFilter(Prerelease);
                context = new SourceCacheContext();

                foreach(PSResourceInfo pkg in SearchAcrossNamesInRepository(repoName, resourceSearch, resourceMetadata, filter, context, pkgsLeftToFind, cancellationToken))
                {
                    yield return pkg;
                }
            }
            // HTTP, HTTPS, FTP Uri schemes (only other Uri schemes allowed by RepositorySettings.Read() API)
            else
            {
                PackageSource source = new PackageSource(repositoryUrl.ToString());
                if (Credential != null)
                {
                    string password = new NetworkCredential(string.Empty, Credential.Password).Password;
                    source.Credentials = PackageSourceCredential.FromUserInput(repositoryUrl.ToString(), Credential.UserName, password, true, null);
                }

                // GetCoreV3() API is able to handle V2 and V3 repository endpoints
                var provider = FactoryExtensionsV3.GetCoreV3(NuGet.Protocol.Core.Types.Repository.Provider);
                SourceRepository repository = new SourceRepository(source, provider);

                try
                {
                    resourceSearch = repository.GetResourceAsync<PackageSearchResource>().GetAwaiter().GetResult();
                    resourceMetadata = repository.GetResourceAsync<PackageMetadataResource>().GetAwaiter().GetResult();
                }
                catch (Exception e){
                    WriteDebug("Error retrieving resource from repository: " + e.Message);
                }

                if (resourceSearch == null || resourceMetadata == null)
                {
                    yield break;
                }

                filter = new SearchFilter(Prerelease);
                context = new SourceCacheContext();

                foreach(PSResourceInfo pkg in SearchAcrossNamesInRepository(repoName, resourceSearch, resourceMetadata, filter, context, pkgsLeftToFind, cancellationToken))
                {
                    yield return pkg;
                }
            }
        }

        public IEnumerable<PSResourceInfo> SearchAcrossNamesInRepository(string repoName, PackageSearchResource pkgSearchResource, PackageMetadataResource pkgMetadataResource, SearchFilter searchFilter, SourceCacheContext srcContext, List<string> pkgsLeftToFind, CancellationToken cancellationToken)
        {
            foreach (string pkgName in Name)
            {
                if (!pkgsLeftToFind.Any())
                {
                    yield break;
                }

                if (String.IsNullOrWhiteSpace(pkgName))
                {
                    WriteError(new ErrorRecord(
                        new PSInvalidOperationException("Name array cannot contain names which are null or whitespace"),
                        "NameCannotBeNullOrWhitespace",
                        ErrorCategory.InvalidArgument,
                        this));
                    continue;
                }

                foreach (PSResourceInfo pkg in FindFromPackageSourceSearchAPI(repoName, pkgName, pkgSearchResource, pkgMetadataResource, searchFilter, srcContext, pkgsLeftToFind, cancellationToken))
                {
                    yield return pkg;
                }
            }
        }
        private IEnumerable<PSResourceInfo> FindFromPackageSourceSearchAPI(string repoName, string name, PackageSearchResource pkgSearchResource, PackageMetadataResource pkgMetadataResource, SearchFilter searchFilter, SourceCacheContext srcContext, List<string> pkgsLeftToFind, CancellationToken cancellationToken)
        {
            List<IPackageSearchMetadata> foundPackagesMetadata = new List<IPackageSearchMetadata>();

            // filter by param: Name
            if (!name.Contains("*"))
            {
                // case: searching for specific package name i.e "Carbon"
                IEnumerable<IPackageSearchMetadata> retrievedPkgs = null;
                try
                {
                    // GetMetadataAsync() API returns all versions for a specific non-wildcard package name
                    // For PSGallery GetMetadataAsync() API returns both Script and Module resources by checking only the Modules endpoint
                    retrievedPkgs = pkgMetadataResource.GetMetadataAsync(name, Prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    WriteDebug(String.Format("Exception retrieving package {0} due to {1}.", name, e.Message));
                }

                if (retrievedPkgs == null || retrievedPkgs.Count() == 0)
                {
                    WriteVerbose(string.Format("'{0}' could not be found in repository '{1}'", name, repoName));
                    yield break;
                }

                foundPackagesMetadata.AddRange(retrievedPkgs.ToList());
                pkgsLeftToFind.Remove(name);
            }
            else
            {
                // case: searching for name containing wildcard i.e "Carbon.*"
                // SearchAsync() returns the latest version only for all packages that match the wild-card name
                IEnumerable<IPackageSearchMetadata> wildcardPkgs = pkgSearchResource.SearchAsync(name, searchFilter, 0, 6000, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();
                if (wildcardPkgs.Count() > SearchAsyncMaxReturned)
                {
                    // get the rest of the packages
                    wildcardPkgs = wildcardPkgs.Concat(pkgSearchResource.SearchAsync(name, searchFilter, 6000, 9000, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult());
                }

                // filter additionally because NuGet wildcard search API returns more than we need
                WildcardPattern nameWildcardPattern = new WildcardPattern(name, WildcardOptions.IgnoreCase);
                foundPackagesMetadata.AddRange(wildcardPkgs.Where(p => nameWildcardPattern.IsMatch(p.Identity.Id)).ToList());

                // if the Script Uri endpoint still needs to be searched, don't remove the wildcard name from pkgsLeftToFind
                // PSGallery + Type == null -> M, S
                // PSGallery + Type == M    -> M
                // PSGallery + Type == S    -> S (but PSGallery would be skipped early on, only PSGalleryScripts would be checked)
                // PSGallery + Type == C    -> M
                // PSGallery + Type == D    -> M
                bool needToCheckPSGalleryScriptsRepo = String.Equals(repoName, "PSGallery", StringComparison.InvariantCultureIgnoreCase) && Type == null;
                if (foundPackagesMetadata.Any() && !needToCheckPSGalleryScriptsRepo)
                {
                    pkgsLeftToFind.Remove(name);
                }
            }

            if (foundPackagesMetadata.Count == 0)
            {
                // no need to attempt to filter
                yield break;
            }

            // filter by param: Version
            if (Version == null)
            {
                // return latest version for each package
                foundPackagesMetadata = foundPackagesMetadata.GroupBy(
                    p => p.Identity.Id, StringComparer.InvariantCultureIgnoreCase).Select(
                        x => x.OrderByDescending(
                            p => p.Identity.Version, VersionComparer.VersionRelease).FirstOrDefault()).ToList();
            }
            else
            {
                // TODO: PR comments
                if (!Utils.TryParseVersionOrVersionRange(Version, out VersionRange versionRange))
                {
                    WriteError(new ErrorRecord(
                        new ArgumentException("Argument for -Version parameter is not in the proper format"),
                        "IncorrectVersionFormat",
                        ErrorCategory.InvalidArgument,
                        this));
                    yield break;
                }

                // at this point, version should be parsed successfully, into allVersions (null or "*") or versionRange (specific or range)
                if (name.Contains("*"))
                {
                    // -Name containing wc with Version "*", or specific range
                    // at this point foundPackagesMetadata contains latest version for each package, get list of distinct
                    // package names and get all versions for each name, this is due to the SearchAsync and GetMetadataAsync() API restrictions !
                    List<IPackageSearchMetadata> allPkgsAllVersions = new List<IPackageSearchMetadata>();
                    foreach (string n in foundPackagesMetadata.Select(p => p.Identity.Id).Distinct(StringComparer.InvariantCultureIgnoreCase))
                    {
                        // get all versions for this package
                        allPkgsAllVersions.AddRange(pkgMetadataResource.GetMetadataAsync(n, Prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult().ToList());
                    }

                    foundPackagesMetadata = allPkgsAllVersions;
                    if (versionRange == VersionRange.All) // Version = "*"
                    {
                        foundPackagesMetadata = foundPackagesMetadata.GroupBy(
                            p => p.Identity.Id, StringComparer.InvariantCultureIgnoreCase).SelectMany(
                                x => x.OrderByDescending(
                                    p => p.Identity.Version, VersionComparer.VersionRelease)).ToList();
                    }
                    else // Version range
                    {
                        foundPackagesMetadata = foundPackagesMetadata.Where(
                            p => versionRange.Satisfies(p.Identity.Version)).GroupBy(
                                p => p.Identity.Id, StringComparer.InvariantCultureIgnoreCase).SelectMany(
                                    x => x.OrderByDescending(
                                    p => p.Identity.Version, VersionComparer.VersionRelease)).ToList();
                    }
                }
                else // name doesn't contain wildcards
                {
                    // for non wildcard names, NuGet GetMetadataAsync() API is which returns all versions for that package ordered descendingly
                    if (versionRange != VersionRange.All) // Version range
                    {
                        foundPackagesMetadata = foundPackagesMetadata.Where(
                            p => versionRange.Satisfies(
                                p.Identity.Version, VersionComparer.VersionRelease)).OrderByDescending(
                                    p => p.Identity.Version).ToList();
                    }
                }
            }

            foreach (IPackageSearchMetadata pkg in foundPackagesMetadata)
            {
                PSResourceInfo currentPkg = new PSResourceInfo();
                if (!PSResourceInfo.TryParse(pkg, out currentPkg, name, repoName, Type, out string errorMsg)){
                    WriteError(new ErrorRecord(
                        new PSInvalidOperationException("Error parsing IPackageSearchMetadata to PSResourceInfo with message: " + errorMsg),
                        "IPackageSearchMetadataToPSResourceInfoParsingError",
                        ErrorCategory.InvalidResult,
                        this));
                    yield break;
                }

                if (Type != null)
                {
                    if (Type == ResourceType.Command && !currentPkg.Type.HasFlag(ResourceType.Command))
                    {
                        continue;
                    }
                    if (Type == ResourceType.DscResource && !currentPkg.Type.HasFlag(ResourceType.DscResource))
                    {
                        continue;
                    }
                }

                if (Tag == null || (Tag != null && IsTagMatch(currentPkg)))
                {
                    yield return currentPkg;
                }
            }
        }

        private bool IsTagMatch(PSResourceInfo pkg)
        {
            return Tag.Intersect(pkg.Tags, StringComparer.InvariantCultureIgnoreCase).ToList().Count > 0;
        }
        #endregion
    }
}