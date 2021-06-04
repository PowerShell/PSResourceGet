// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using MoreLinq.Extensions;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using static Microsoft.PowerShell.PowerShellGet.UtilClasses.PSResourceInfo;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.Diagnostics;
using NuGet.Configuration;
using NuGet.Common;
using System.Data;
using System.Net;
using static System.Environment;
using Dbg = System.Diagnostics.Debug;
using NuGet.CatalogReader;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// Find helper class provides the core functionality for FindPSResource.
    /// </summary>
    internal class FindHelper
    {
        private CancellationToken _cancellationToken;
        private readonly PSCmdlet _cmdletPassedIn;
        private List<string> _pkgsLeftToFind;
        private string[] _name;
        private ResourceType? _type;
        private string _version;
        private SwitchParameter _prerelease = false;
        private PSCredential _credential;
        private string[] _tag;
        private string[] _repository;
        private string _psGalleryRepoName = "PSGallery";
        private const int SearchAsyncMaxReturned = 5990;
        private const int GalleryMax = 12000;


        public FindHelper(CancellationToken cancellationToken, PSCmdlet cmdletPassedIn)
        {
            _cancellationToken = cancellationToken;
            _cmdletPassedIn = cmdletPassedIn;
        }

        public IEnumerable<PSResourceInfo> FindByResourceName(
            string[] name,
            ResourceType? type,
            string version,
            SwitchParameter prerelease,
            string[] tag,
            string[] repository,
            PSCredential credential)
        {
            // TODO: Ctrl F and check _name is used instead of name, and such for rest of fields
            _name = name;
            _type = type;
            _version = version;
            _prerelease = prerelease;
            _tag = tag;
            _repository = repository;
            _credential = credential;

            Dbg.Assert(_name.Length != 0, "Name length cannot be 0");

            _pkgsLeftToFind = _name.ToList();

            List<PSRepositoryInfo> repositoriesToSearch;

            try
            {
                repositoriesToSearch = RepositorySettings.Read(_repository, out string[] errorList);

                foreach (string error in errorList)
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new PSInvalidOperationException(error),
                        "ErrorGettingSpecifiedRepo",
                        ErrorCategory.InvalidOperation,
                        this));
                }
            }
            catch (Exception e)
            {
                _cmdletPassedIn.ThrowTerminatingError(new ErrorRecord(
                    new PSInvalidOperationException(e.Message),
                    "ErrorLoadingRepositoryStoreFile",
                    ErrorCategory.InvalidArgument,
                    this));
                yield break;
            }

            // loop through repositoriesToSearch and if PSGallery add it to list with same priority as PSGallery repo
            for (int i = 0; i < repositoriesToSearch.Count; i++)
            {
                // TODO: reach out to Dongbo if is this safe; if not then use temp list or track indices of where to insert/remove at.
                if (String.Equals(repositoriesToSearch[i].Name, _psGalleryRepoName, StringComparison.InvariantCultureIgnoreCase))
                {
                    // for PowerShellGallery, Modules and Scripts have different endpoints so separate repositories have to be registered
                    // with those endpoints in order for the NuGet APIs to search across both (in the case where name includes *)

                    // detect if Scripts repository needs to be added and/or Modules repository needs to be skipped
                    Uri psGalleryScriptsUrl = new Uri("http://www.powershellgallery.com/api/v2/items/psscript/");
                    PSRepositoryInfo psGalleryScripts = new PSRepositoryInfo("PSGalleryScripts", psGalleryScriptsUrl, repositoriesToSearch[i].Priority, false);
                    if (_type == null)
                    {
                        _cmdletPassedIn.WriteDebug("Null Type provided, so add PSGalleryScripts repository");
                        repositoriesToSearch.Insert(i + 1, psGalleryScripts);
                    }
                    else if (_type != null && _type == ResourceType.Script)
                    {
                        _cmdletPassedIn.WriteDebug("Type Script provided, so add PSGalleryScripts and remove PSGallery (Modules only)");
                        repositoriesToSearch.Insert(i + 1, psGalleryScripts);
                        repositoriesToSearch.RemoveAt(i); // remove PSGallery
                    }
                }
            }

            for (int i = 0; i < repositoriesToSearch.Count && _pkgsLeftToFind.Any(); i++)
            {
                _cmdletPassedIn.WriteDebug(string.Format("Searching in repository {0}", repositoriesToSearch[i].Name));
                foreach (var pkg in SearchFromRepository(
                    repositoryName: repositoriesToSearch[i].Name,
                    repositoryUrl: repositoriesToSearch[i].Url))
                {
                    yield return pkg;
                }
            }
        }


        public IEnumerable<PSResourceInfo> SearchFromRepository(
            string repositoryName,
            Uri repositoryUrl)
        {
            PackageSearchResource resourceSearch;
            PackageMetadataResource resourceMetadata;
            SearchFilter filter;
            SourceCacheContext context;

            // file based Uri scheme
            if (repositoryUrl.Scheme == Uri.UriSchemeFile)
            {
                FindLocalPackagesResourceV2 localResource = new FindLocalPackagesResourceV2(repositoryUrl.ToString());
                resourceSearch = new LocalPackageSearchResource(localResource);
                resourceMetadata = new LocalPackageMetadataResource(localResource);
                filter = new SearchFilter(_prerelease);
                context = new SourceCacheContext();

                foreach(PSResourceInfo pkg in SearchAcrossNamesInRepository(
                    repositoryName: repositoryName,
                    pkgSearchResource: resourceSearch,
                    pkgMetadataResource: resourceMetadata,
                    searchFilter: filter,
                    sourceContext: context))
                {
                    yield return pkg;
                }
                yield break;
            }
            // HTTP, HTTPS, FTP Uri schemes (only other Uri schemes allowed by RepositorySettings.Read() API)

            PackageSource source = new PackageSource(repositoryUrl.ToString());
            if (_credential != null)
            {
                string password = new NetworkCredential(string.Empty, _credential.Password).Password;
                source.Credentials = PackageSourceCredential.FromUserInput(repositoryUrl.ToString(), _credential.UserName, password, true, null);
            }

            // GetCoreV3() API is able to handle V2 and V3 repository endpoints
            var provider = FactoryExtensionsV3.GetCoreV3(NuGet.Protocol.Core.Types.Repository.Provider);
            SourceRepository repository = new SourceRepository(source, provider);
            resourceSearch = null;
            resourceMetadata = null;

            try
            {
                resourceSearch = repository.GetResourceAsync<PackageSearchResource>().GetAwaiter().GetResult();
                resourceMetadata = repository.GetResourceAsync<PackageMetadataResource>().GetAwaiter().GetResult();
            }
            catch (Exception e){
                _cmdletPassedIn.WriteDebug("Error retrieving resource from repository: " + e.Message);
            }

            if (resourceSearch == null || resourceMetadata == null)
            {
                yield break;
            }

            filter = new SearchFilter(_prerelease);
            context = new SourceCacheContext();

            foreach(PSResourceInfo pkg in SearchAcrossNamesInRepository(
                repositoryName: repositoryName,
                pkgSearchResource: resourceSearch,
                pkgMetadataResource: resourceMetadata,
                searchFilter: filter,
                sourceContext: context))
            {
                yield return pkg;
            }
        }

        public IEnumerable<PSResourceInfo> SearchAcrossNamesInRepository(
            string repositoryName,
            PackageSearchResource pkgSearchResource,
            PackageMetadataResource pkgMetadataResource,
            SearchFilter searchFilter,
            SourceCacheContext sourceContext)
        {
            foreach (string pkgName in _pkgsLeftToFind.ToArray())
            {
                if (String.IsNullOrWhiteSpace(pkgName))
                {
                    _cmdletPassedIn.WriteDebug(String.Format("Package name: {0} provided was null or whitespace, so name was skipped in search.", pkgName == null ? "null string" : pkgName));
                    continue;
                }

                foreach (PSResourceInfo pkg in FindFromPackageSourceSearchAPI(
                    repositoryName: repositoryName,
                    pkgName: pkgName,
                    pkgSearchResource: pkgSearchResource,
                    pkgMetadataResource: pkgMetadataResource,
                    searchFilter: searchFilter,
                    sourceContext: sourceContext))
                {
                    yield return pkg;
                }
            }
        }

        private IEnumerable<PSResourceInfo> FindFromPackageSourceSearchAPI(
            string repositoryName,
            string pkgName,
            PackageSearchResource pkgSearchResource,
            PackageMetadataResource pkgMetadataResource,
            SearchFilter searchFilter,
            SourceCacheContext sourceContext)
        {
            List<IPackageSearchMetadata> foundPackagesMetadata = new List<IPackageSearchMetadata>();

            // filter by param: Name
            if (!pkgName.Contains("*"))
            {
                // case: searching for specific package name i.e "Carbon"
                IEnumerable<IPackageSearchMetadata> retrievedPkgs = null;
                try
                {
                    // GetMetadataAsync() API returns all versions for a specific non-wildcard package name
                    // For PSGallery GetMetadataAsync() API returns both Script and Module resources by checking only the Modules endpoint
                    retrievedPkgs = pkgMetadataResource.GetMetadataAsync(
                        packageId: pkgName,
                        includePrerelease: _prerelease,
                        includeUnlisted: false,
                        sourceCacheContext: sourceContext,
                        log: NullLogger.Instance,
                        token: _cancellationToken).GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    _cmdletPassedIn.WriteDebug(String.Format("Exception retrieving package {0} due to {1}.", pkgName, e.Message));
                }

                if (retrievedPkgs == null || retrievedPkgs.Count() == 0)
                {
                    _cmdletPassedIn.WriteVerbose(string.Format("'{0}' could not be found in repository '{1}'", pkgName, repositoryName));
                    yield break;
                }

                foundPackagesMetadata.AddRange(retrievedPkgs.ToList());
                _pkgsLeftToFind.Remove(pkgName);
            }
            else
            {
                // case: searching for name containing wildcard i.e "Carbon.*"
                // SearchAsync() returns the latest version only for all packages that match the wild-card name
                IEnumerable<IPackageSearchMetadata> wildcardPkgs = pkgSearchResource.SearchAsync(
                    searchTerm: pkgName,
                    filters: searchFilter,
                    skip: 0,
                    take: 6000,
                    log: NullLogger.Instance,
                    cancellationToken: _cancellationToken).GetAwaiter().GetResult();
                if (wildcardPkgs.Count() > SearchAsyncMaxReturned)
                {
                    // get the rest of the packages
                    wildcardPkgs = wildcardPkgs.Concat(pkgSearchResource.SearchAsync(
                        searchTerm: pkgName,
                        filters: searchFilter,
                        skip: 6000,
                        take: GalleryMax,
                        log: NullLogger.Instance,
                        cancellationToken: _cancellationToken).GetAwaiter().GetResult());
                }

                // filter additionally because NuGet wildcard search API returns more than we need
                // TODO: filter out names including OTHER wildcards allowed by the API but which we don't support
                WildcardPattern nameWildcardPattern = new WildcardPattern(pkgName, WildcardOptions.IgnoreCase);
                foundPackagesMetadata.AddRange(wildcardPkgs.Where(
                    p => nameWildcardPattern.IsMatch(p.Identity.Id)).ToList());

                // if the Script Uri endpoint still needs to be searched, don't remove the wildcard name from _pkgsLeftToFind
                // PSGallery + Type == null -> M, S
                // PSGallery + Type == M    -> M
                // PSGallery + Type == S    -> S (but PSGallery would be skipped early on, only PSGalleryScripts would be checked)
                // PSGallery + Type == C    -> M
                // PSGallery + Type == D    -> M

                bool needToCheckPSGalleryScriptsRepo = String.Equals(repositoryName, _psGalleryRepoName, StringComparison.InvariantCultureIgnoreCase) && _type == null;
                if (foundPackagesMetadata.Any() && !needToCheckPSGalleryScriptsRepo)
                {
                    _pkgsLeftToFind.Remove(pkgName);
                }
            }

            if (foundPackagesMetadata.Count == 0)
            {
                // no need to attempt to filter
                yield break;
            }

            // filter by param: Version
            if (_version == null)
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
                if (!Utils.TryParseVersionOrVersionRange(_version, out VersionRange versionRange))
                {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new ArgumentException("Argument for -Version parameter is not in the proper format"),
                        "IncorrectVersionFormat",
                        ErrorCategory.InvalidArgument,
                        this));
                    yield break;
                }

                // at this point, version should be parsed successfully, into allVersions (null or "*") or versionRange (specific or range)
                if (pkgName.Contains("*"))
                {
                    // -Name containing wc with Version "*", or specific range
                    // at this point foundPackagesMetadata contains latest version for each package, get list of distinct
                    // package names and get all versions for each name, this is due to the SearchAsync and GetMetadataAsync() API restrictions !
                    List<IPackageSearchMetadata> allPkgsAllVersions = new List<IPackageSearchMetadata>();
                    foreach (string n in foundPackagesMetadata.Select(p => p.Identity.Id).Distinct(StringComparer.InvariantCultureIgnoreCase))
                    {
                        // get all versions for this package
                        allPkgsAllVersions.AddRange(pkgMetadataResource.GetMetadataAsync(n, _prerelease, false, sourceContext, NullLogger.Instance, _cancellationToken).GetAwaiter().GetResult().ToList());
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
                if (!PSResourceInfo.TryParse(
                    metadataToParse: pkg,
                    psGetInfo: out PSResourceInfo currentPkg,
                    pkgName: pkgName,
                    repositoryName: repositoryName,
                    type: _type,
                    errorMsg: out string errorMsg))
                    {
                    _cmdletPassedIn.WriteError(new ErrorRecord(
                        new PSInvalidOperationException("Error parsing IPackageSearchMetadata to PSResourceInfo with message: " + errorMsg),
                        "IPackageSearchMetadataToPSResourceInfoParsingError",
                        ErrorCategory.InvalidResult,
                        this));
                    yield break;
                }

                if (_type != null)
                {
                    if (_type == ResourceType.Command && !currentPkg.Type.HasFlag(ResourceType.Command))
                    {
                        continue;
                    }
                    if (_type == ResourceType.DscResource && !currentPkg.Type.HasFlag(ResourceType.DscResource))
                    {
                        continue;
                    }
                }

                if (_tag == null || (_tag != null && IsTagMatch(currentPkg)))
                {
                    yield return currentPkg;
                }
            }
        }

        private bool IsTagMatch(PSResourceInfo pkg)
        {
            return _tag.Intersect(pkg.Tags, StringComparer.InvariantCultureIgnoreCase).ToList().Count > 0;
        }
    }
}