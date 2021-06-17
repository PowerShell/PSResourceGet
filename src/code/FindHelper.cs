using System.Net.Http;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using MoreLinq.Extensions;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using static Microsoft.PowerShell.PowerShellGet.UtilClasses.PSResourceInfo;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.Configuration;
using NuGet.Common;
using System.Data;
using System.Net;
using Dbg = System.Diagnostics.Debug;

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
        private ResourceType _type;
        private string _version;
        private SwitchParameter _prerelease = false;
        private PSCredential _credential;
        private string[] _tag;
        private string[] _repository;
        private SwitchParameter _includeDependencies = false;
        private readonly string _psGalleryRepoName = "PSGallery";
        private readonly string _psGalleryScriptsRepoName = "PSGalleryScripts";

        // NuGet's SearchAsync() API takes a top parameter of 6000, but testing shows for PSGallery
        // usually a max of around 5990 is returned while more are left to retrieve in a second SearchAsync() call
        private const int SearchAsyncMaxTake = 6000;
        private const int SearchAsyncMaxReturned = 5990;
        private const int GalleryMax = 12000;

        public FindHelper(CancellationToken cancellationToken, PSCmdlet cmdletPassedIn)
        {
            _cancellationToken = cancellationToken;
            _cmdletPassedIn = cmdletPassedIn;
        }

        public IEnumerable<PSResourceInfo> FindByResourceName(
            string[] name,
            ResourceType type,
            string version,
            SwitchParameter prerelease,
            string[] tag,
            string[] repository,
            PSCredential credential,
            SwitchParameter includeDependencies)
        {
            _name = name;
            _type = type;
            _version = version;
            _prerelease = prerelease;
            _tag = tag;
            _repository = repository;
            _credential = credential;
            _includeDependencies = includeDependencies;

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
                if (String.Equals(repositoriesToSearch[i].Name, _psGalleryRepoName, StringComparison.InvariantCultureIgnoreCase))
                {
                    // for PowerShellGallery, Module and Script resources have different endpoints so separate repositories have to be registered
                    // with those endpoints in order for the NuGet APIs to search across both in the case where name includes '*'

                    // detect if Script repository needs to be added and/or Module repository needs to be skipped
                    Uri psGalleryScriptsUrl = new Uri("http://www.powershellgallery.com/api/v2/items/psscript/");
                    PSRepositoryInfo psGalleryScripts = new PSRepositoryInfo(_psGalleryScriptsRepoName, psGalleryScriptsUrl, repositoriesToSearch[i].Priority, false);
                    if (_type == ResourceType.None)
                    {
                        _cmdletPassedIn.WriteDebug("Null Type provided, so add PSGalleryScripts repository");
                        repositoriesToSearch.Insert(i + 1, psGalleryScripts);
                    }
                    else if (_type != ResourceType.None && _type == ResourceType.Script)
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
                _cmdletPassedIn.WriteVerbose("credential successfully set for repository:" + repositoryName);
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
                    _cmdletPassedIn.WriteDebug(String.Format("Package name: {0} provided was null or whitespace, so name was skipped in search.",
                        pkgName == null ? "null string" : pkgName));
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
                catch (HttpRequestException ex)
                {
                    if ((String.Equals(repositoryName, _psGalleryRepoName, StringComparison.InvariantCultureIgnoreCase) ||
                        String.Equals(repositoryName, _psGalleryScriptsRepoName, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        _cmdletPassedIn.WriteDebug(String.Format("Error receiving package from PSGallery. To check if this is due to a PSGallery outage check: https://aka.ms/psgallerystatus . Specific error: {0}", ex.Message));
                        yield break;
                    }
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
                IEnumerable<IPackageSearchMetadata> wildcardPkgs = null;
                try
                {
                    // SearchAsync() API returns the latest version only for all packages that match the wild-card name
                    wildcardPkgs = pkgSearchResource.SearchAsync(
                        searchTerm: pkgName,
                        filters: searchFilter,
                        skip: 0,
                        take: SearchAsyncMaxTake,
                        log: NullLogger.Instance,
                        cancellationToken: _cancellationToken).GetAwaiter().GetResult();
                        _cmdletPassedIn.WriteVerbose("first SearchAsync() call made");
                    if (wildcardPkgs.Count() > SearchAsyncMaxReturned)
                    {
                        // get the rest of the packages
                        wildcardPkgs = wildcardPkgs.Concat(pkgSearchResource.SearchAsync(
                            searchTerm: pkgName,
                            filters: searchFilter,
                            skip: SearchAsyncMaxTake,
                            take: GalleryMax,
                            log: NullLogger.Instance,
                            cancellationToken: _cancellationToken).GetAwaiter().GetResult());
                    }
                }
                catch (HttpRequestException ex)
                {
                    if ((String.Equals(repositoryName, _psGalleryRepoName, StringComparison.InvariantCultureIgnoreCase) ||
                        String.Equals(repositoryName, _psGalleryScriptsRepoName, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        _cmdletPassedIn.WriteDebug(String.Format("Error receiving package from PSGallery. To check if this is due to a PSGallery outage check: https://aka.ms/psgallerystatus . Specific error: {0}", ex.Message));
                        yield break;
                    }
                }
                catch (Exception e)
                {
                    _cmdletPassedIn.WriteDebug(String.Format("Exception retrieving package {0} due to {1}.", pkgName, e.Message));
                }

                // filter additionally because NuGet wildcard search API returns more than we need
                // perhaps validate in Find-PSResource, and use debugassert here?
                WildcardPattern nameWildcardPattern = new WildcardPattern(pkgName, WildcardOptions.IgnoreCase);
                foundPackagesMetadata.AddRange(wildcardPkgs.Where(
                    p => nameWildcardPattern.IsMatch(p.Identity.Id)).ToList());

                // if the Script Uri endpoint still needs to be searched, don't remove the wildcard name from _pkgsLeftToFind
                // PSGallery + Type == null -> M, S
                // PSGallery + Type == M    -> M
                // PSGallery + Type == S    -> S (but PSGallery would be skipped early on, only PSGalleryScripts would be checked)
                // PSGallery + Type == C    -> M
                // PSGallery + Type == D    -> M

                bool needToCheckPSGalleryScriptsRepo = String.Equals(repositoryName, _psGalleryRepoName, StringComparison.InvariantCultureIgnoreCase) && _type == ResourceType.None;
                if (foundPackagesMetadata.Any() && !needToCheckPSGalleryScriptsRepo)
                {
                    _pkgsLeftToFind.Remove(pkgName);
                }
            }

            if (foundPackagesMetadata.Count == 0)
            {
                // no need to attempt to filter further
                _cmdletPassedIn.WriteVerbose("no packages found");
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
                if (!PSResourceInfo.TryConvert(
                    metadataToParse: pkg,
                    psGetInfo: out PSResourceInfo currentPkg,
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

                if (_type != ResourceType.None)
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

                // Only going to go in here for the main package, resolve Type and Tag requirements if any, and then find dependencies
                if (_tag == null || (_tag != null && IsTagMatch(currentPkg)))
                {
                    yield return currentPkg;

                    if (_includeDependencies)
                    {
                        foreach (PSResourceInfo pkgDep in FindDependencyPackages(currentPkg, pkgMetadataResource, sourceContext))
                        {
                            yield return pkgDep;
                        }
                    }
                }
            }
        }

        private bool IsTagMatch(PSResourceInfo pkg)
        {
            return _tag.Intersect(pkg.Tags, StringComparer.InvariantCultureIgnoreCase).ToList().Count > 0;
        }

        private List<PSResourceInfo> FindDependencyPackages(
            PSResourceInfo currentPkg,
            PackageMetadataResource packageMetadataResource,
            SourceCacheContext sourceCacheContext
        )
        {
            List<PSResourceInfo> thoseToAdd = new List<PSResourceInfo>();
            FindDependencyPackagesHelper(currentPkg, thoseToAdd, packageMetadataResource, sourceCacheContext);
            return thoseToAdd;
        }

        private void FindDependencyPackagesHelper(
            PSResourceInfo currentPkg,
            List<PSResourceInfo> thoseToAdd,
            PackageMetadataResource packageMetadataResource,
            SourceCacheContext sourceCacheContext
        )
        {
            foreach(var dep in currentPkg.Dependencies)
            {
                IEnumerable<IPackageSearchMetadata> depPkgs = packageMetadataResource.GetMetadataAsync(
                    packageId: dep.Name,
                    includePrerelease: _prerelease,
                    includeUnlisted: false,
                    sourceCacheContext: sourceCacheContext,
                    log: NullLogger.Instance,
                    token: _cancellationToken).GetAwaiter().GetResult();

                if (depPkgs.Count() > 0)
                {
                    if (dep.VersionRange == VersionRange.All)
                    {
                        // return latest version
                        IPackageSearchMetadata depPkgLatestVersion = depPkgs.First();

                        if (!PSResourceInfo.TryConvert(
                            metadataToParse: depPkgLatestVersion,
                            psGetInfo: out PSResourceInfo depPSResourceInfoPkg,
                            repositoryName: currentPkg.Repository,
                            type: currentPkg.Type,
                            errorMsg: out string errorMsg))
                        {
                            _cmdletPassedIn.WriteError(new ErrorRecord(
                                new PSInvalidOperationException("Error parsing dependency IPackageSearchMetadata to PSResourceInfo with message: " + errorMsg),
                                "DependencyIPackageSearchMetadataToPSResourceInfoParsingError",
                                ErrorCategory.InvalidResult,
                                this));
                        }

                        thoseToAdd.Add(depPSResourceInfoPkg);
                        FindDependencyPackagesHelper(depPSResourceInfoPkg, thoseToAdd, packageMetadataResource, sourceCacheContext);
                    }
                    else
                    {
                        List<IPackageSearchMetadata> pkgVersionsInRange = depPkgs.Where(
                            p => dep.VersionRange.Satisfies(
                                p.Identity.Version, VersionComparer.VersionRelease)).OrderByDescending(
                                    p => p.Identity.Version).ToList();

                        if (pkgVersionsInRange.Count() > 0)
                        {
                            IPackageSearchMetadata depPkgLatestInRange = pkgVersionsInRange.First();
                            if (depPkgLatestInRange != null)
                            {
                                if (!PSResourceInfo.TryConvert(
                                    metadataToParse: depPkgLatestInRange,
                                    psGetInfo: out PSResourceInfo depPSResourceInfoPkg,
                                    repositoryName: currentPkg.Repository,
                                    type: currentPkg.Type,
                                    errorMsg: out string errorMsg))
                                {
                                    _cmdletPassedIn.WriteError(new ErrorRecord(
                                        new PSInvalidOperationException("Error parsing dependency range IPackageSearchMetadata to PSResourceInfo with message: " + errorMsg),
                                        "DependencyRangeIPackageSearchMetadataToPSResourceInfoParsingError",
                                        ErrorCategory.InvalidResult,
                                        this));
                                }

                                thoseToAdd.Add(depPSResourceInfoPkg);
                                FindDependencyPackagesHelper(depPSResourceInfoPkg, thoseToAdd, packageMetadataResource, sourceCacheContext);
                            }
                        }
                    }
                }
            }
        }
    }
}