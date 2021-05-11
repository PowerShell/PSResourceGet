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

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// todo:
    /// </summary>

    [Cmdlet(VerbsCommon.Find,
        "PSResource",
        DefaultParameterSetName = "ResourceNameParameterSet",
        SupportsShouldProcess = true,
        HelpUri = "<add>")]
    public sealed
    class FindPSResource : PSCmdlet
    {
        # region Enums
        public enum ResourceCategory {
            Module,
            Script
        };
        #endregion

        #region Members
        private const string ResourceNameParameterSet = "ResourceNameParameterSet";
        private const string CommandNameParameterSet = "CommandNameParameterSet";
        private const string DscResourceNameParameterSet = "DscResourceNameParameterSet";

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
        public ResourceCategory? Type { get; set; }


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
        public string[] Tags { get; set; }

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
        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ResourceNameParameterSet:
                    foreach (PSResourceInfo pkgObj in ResourceNameParameterSetHelper())
                    {
                        WriteObject(pkgObj);
                    }
                    break;

                case CommandNameParameterSet:
                    break;

                case DscResourceNameParameterSet:
                    break;

                default:
                    Dbg.Assert(false, "Invalid parameter set");
                    break;
            }
        }

        #endregion

        private IEnumerable<PSResourceInfo> ResourceNameParameterSetHelper()
        {
            CancellationToken cancellationToken = new CancellationTokenSource().Token;

            List<PSRepositoryInfo> repositoriesToSearch = RepositorySettings.Read(Repository, out string[] errorList);

            List<string> pkgsLeftToFind = Name.ToList();

            for (int i = 0; i < repositoriesToSearch.Count && pkgsLeftToFind.Any(); i++)
            {
                if (String.Equals(repositoriesToSearch[i].Name, "PSGallery", StringComparison.OrdinalIgnoreCase))
                {
                    Uri psGalleryScriptsUrl = new Uri("http://www.powershellgallery.com/api/v2/items/psscript/");
                    PSRepositoryInfo psGalleryScripts = new PSRepositoryInfo("PSGalleryScripts", psGalleryScriptsUrl, 50, false);
                    if (Type == null)
                    {
                        WriteDebug("Null Type provided, so add PSGalleryScripts repository");
                        repositoriesToSearch.Add(psGalleryScripts);
                    }
                    else if (Type != null && Type == ResourceCategory.Script)
                    {
                        WriteDebug("Type Script provided, so add PSGalleryScripts and remove PSGallery (Modules only)");
                        repositoriesToSearch.Add(psGalleryScripts);
                        continue;
                    }
                }
                WriteDebug(string.Format("Searching in repository {0}", repositoriesToSearch[i].Name));
                foreach (var pkg in SearchFromRepository(repositoriesToSearch[i].Name, repositoriesToSearch[i].Url, pkgsLeftToFind, cancellationToken))
                {
                    yield return pkg;
                }
            }
        }

        public IEnumerable<PSResourceInfo> SearchFromRepository(string repoName, Uri repositoryUrl, List<string> pkgsLeftToFind, CancellationToken cancellationToken)
        {
            // if repositoryUrl ends with V3 endpoint and either of the names in Name is CatalogReader
            // each V3 repository must have a service index resource: https://docs.microsoft.com/en-us/nuget/api/service-index
            if (repositoryUrl.AbsoluteUri.EndsWith("index.json")) {
                bool isNameAsterisk = false;
                foreach (string name in Name)
                {
                    if (String.Equals(name, "*"))
                    {
                        isNameAsterisk = true;
                    }
                }
                if (isNameAsterisk){
                    // Name.Length should not be 0 (validated by parameter binding)
                    if (Name.Length > 1)
                    {
                        WriteError(new ErrorRecord(
                            new PSInvalidOperationException("Name array cannot contain additional elements if one is '*'."),
                            "NameCannotBeNullOrWhitespace",
                            ErrorCategory.InvalidArgument,
                            this));
                    }
                    else
                    {
                        foreach(PSResourceInfo pkg in SearchFromCatalogReader(repoName, repositoryUrl, cancellationToken))
                        {
                            yield return pkg;
                        }
                    }
                }

            }
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
                // or optionally create a whole other packageSource object conditionally and have a foreach like above in that condition
            }
        }

        public IEnumerable<PSResourceInfo> SearchAcrossNamesInRepository(string repoName, PackageSearchResource pkgSearchResource, PackageMetadataResource pkgMetadataResource, SearchFilter searchFilter, SourceCacheContext srcContext, List<string> pkgsLeftToFind, CancellationToken cancellationToken)
        {
            foreach (string pkgName in Name)
            {
                if (pkgsLeftToFind.Any())
                {
                    if (!String.IsNullOrWhiteSpace(pkgName))
                    {
                        foreach (PSResourceInfo pkg in FindFromPackageSourceSearchAPI(repoName, pkgName, pkgSearchResource, pkgMetadataResource, searchFilter, srcContext, pkgsLeftToFind, cancellationToken))
                        {
                            yield return pkg;
                        }
                    }
                    else
                    {
                        WriteError(new ErrorRecord(
                            new PSInvalidOperationException("Name array cannot contain names which are null or whitespace"),
                            "NameCannotBeNullOrWhitespace",
                            ErrorCategory.InvalidArgument,
                            this));
                    }
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
                    retrievedPkgs = pkgMetadataResource.GetMetadataAsync(name, Prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    WriteDebug(String.Format("Exception retrieving package {0} due to {1}.", name, e.Message));
                }
                if (retrievedPkgs == null || retrievedPkgs.Count() == 0)
                {
                    this.WriteVerbose(string.Format("'{0}' could not be found in repository '{1}'", name, repoName));
                    yield break;
                }
                else
                {
                    foundPackagesMetadata.AddRange(retrievedPkgs.ToList());
                    if (foundPackagesMetadata.Any())
                    {
                        pkgsLeftToFind.Remove(name);
                    }
                }
            }
            else
            {
                WriteVerbose("searching for name with wildcards");
                // case: searching for name containing wildcard i.e "Carbon.*"
                // NuGet API doesn't handle wildcards so get all packages, then filter for wilcard match
                WriteVerbose("name: " + name);
                IEnumerable<IPackageSearchMetadata> wildcardPkgs = pkgSearchResource.SearchAsync(name, searchFilter, 0, 6000, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();

                WildcardPattern nameWildcardPattern = new WildcardPattern(name, WildcardOptions.IgnoreCase);
                foundPackagesMetadata.AddRange(wildcardPkgs.Where(p => nameWildcardPattern.IsMatch(p.Identity.Id)).ToList());

                bool needToCheckPSGalleryScriptsRepo = String.Equals(repoName, "PSGallery", StringComparison.OrdinalIgnoreCase) && Type == null;
                if (foundPackagesMetadata.Any() && !needToCheckPSGalleryScriptsRepo)
                {
                    pkgsLeftToFind.Remove(name);
                }
            }

            // filter by param: Version
            if (Version == null)
            {
                // if no Version parameter provided, return latest version
                // should be ok for both name with wildcard (groupby name needed) and without wildcard (groupby doesn't break it I think)
                foundPackagesMetadata = foundPackagesMetadata.GroupBy(p => p.Identity.Id).Select(x => x.OrderByDescending(p => p.Identity.Version, VersionComparer.VersionRelease).FirstOrDefault()).ToList();
            }
            else
            {
                if (!Utils.TryParseVersionOrVersionRange(Version.ToString(), out VersionRange versionRange, out bool allVersions, this))
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
                    List<IPackageSearchMetadata> temp = new List<IPackageSearchMetadata>();
                    foreach (string n in foundPackagesMetadata.Select(p => p.Identity.Id).Distinct())
                    {
                        // get all versions for this package
                        temp.AddRange(pkgMetadataResource.GetMetadataAsync(n, Prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult().ToList());
                    }
                    foundPackagesMetadata = temp;
                    if (allVersions)
                    {
                        foundPackagesMetadata = foundPackagesMetadata.GroupBy(p => p.Identity.Id).SelectMany(x => x.OrderByDescending(p => p.Identity.Version, VersionComparer.VersionRelease)).ToList();
                    }
                    else // version range
                    {
                        foundPackagesMetadata = foundPackagesMetadata.Where(p => versionRange.Satisfies(p.Identity.Version)).GroupBy(p => p.Identity.Id).SelectMany(x => x.OrderByDescending(p => p.Identity.Version, VersionComparer.VersionRelease)).ToList();
                    }
                }
                else // name doesn't contain wildcards
                {
                    // allVersions for non-wildcard name is already ordered descending.
                    if (!allVersions)
                    {
                        foundPackagesMetadata = foundPackagesMetadata.Where(p => versionRange.Satisfies(p.Identity.Version, VersionComparer.VersionRelease)).OrderByDescending(p => p.Identity.Version).ToList();
                    }
                }
            }

            // filter by param: Type
            if (Type != null)
            {
                foundPackagesMetadata = FilterPkgsByResourceType(foundPackagesMetadata);
            }
            // filter by param: Tags

            foreach (IPackageSearchMetadata pkg in foundPackagesMetadata)
            {
                PSResourceInfo currentPkg = new PSResourceInfo();
                if(!PSResourceInfo.TryParse(pkg, out currentPkg, out string errorMsg)){
                    // todo: have better WriteError method here
                    WriteVerbose(errorMsg);
                    yield break;
                }
                yield return currentPkg;
            }
        }

        private List<IPackageSearchMetadata> FilterPkgsByResourceType(List<IPackageSearchMetadata> foundPkgs)
        {
            List<IPackageSearchMetadata> pkgsFilteredByTags = new List<IPackageSearchMetadata>();
            char[] delimiter = new char[] { ' ', ',' };
            foreach (IPackageSearchMetadata pkg in foundPkgs)
            {
                string[] tags = pkg.Tags.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
                foreach (string tag in tags)
                {
                    if(tag == "PS" + Type)
                    {
                        pkgsFilteredByTags.Add(pkg);
                    }
                    else if (Enum.TryParse(tag, out ResourceCategory parsedType) && parsedType == ResourceCategory.Module)
                    {
                        WriteVerbose("added here");
                        pkgsFilteredByTags.Add(pkg);
                    }
                }
            }
            return pkgsFilteredByTags;
        }

        private IEnumerable<PSResourceInfo> SearchFromCatalogReader(string repoName, Uri repositoryUrl, CancellationToken cancellationToken)
        {

            using (var catalog = new CatalogReader(repositoryUrl))
            {
                IReadOnlyList<CatalogEntry> allPkgs = catalog.GetFlattenedEntriesAsync(cancellationToken).GetAwaiter().GetResult();
                foreach(CatalogEntry pkg in allPkgs)
                {
                    PSResourceInfo currentPkg = new PSResourceInfo();
                    if(!PSResourceInfo.TryParseCatalogEntry(pkg, out currentPkg, out string errorMsg)){
                        // todo: have better WriteError method here
                        WriteVerbose(errorMsg);
                        yield break;
                    }
                    yield return currentPkg;
                }
            }
        }
    }
}