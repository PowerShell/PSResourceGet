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

        // TODO: implememt StopProcessing()

        #endregion

        private IEnumerable<PSResourceInfo> ResourceNameParameterSetHelper()
        {
            WriteVerbose("made it here 1");
            // TODO: class variable
            CancellationToken cancellationToken = new CancellationTokenSource().Token;

            List<PSRepositoryInfo> repositoriesToSearch = RepositorySettings.Read(Repository, out string[] errorList);

            // TODO: if errorList write errors

            List<string> pkgsLeftToFind = Name.ToList();

            for (int i = 0; i < repositoriesToSearch.Count && pkgsLeftToFind.Any(); i++)
            {
                if (String.Equals(repositoriesToSearch[i].Name, "PSGallery", StringComparison.OrdinalIgnoreCase))
                {
                    // detect if Scripts repository needs to be added and/or Modules repository needs to be skipped
                    // note about how Scripts and Modules have different endpoints, to use NuGet APIs need to search across both for PSGallery
                    Uri psGalleryScriptsUrl = new Uri("http://www.powershellgallery.com/api/v2/items/psscript/");
                    PSRepositoryInfo psGalleryScripts = new PSRepositoryInfo("PSGalleryScripts", psGalleryScriptsUrl, 50, false);
                    if (Type == null) //Type != MOdule && Type != Script
                    {
                        WriteDebug("Null Type provided, so add PSGalleryScripts repository");
                        repositoriesToSearch.Add(psGalleryScripts);
                    }
                    else if (Type != null && Type == ResourceType.Script)
                    {
                        WriteDebug("Type Script provided, so add PSGalleryScripts and remove PSGallery (Modules only)");
                        repositoriesToSearch.Add(psGalleryScripts);
                        // skip current Modules repository (PSGallery) since Module Type not specified
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
            WriteVerbose("made it here 2");
            // if repositoryUrl ends with V3 endpoint and either of the names in Name is CatalogReader
            // each V3 repository must have a service index resource: https://docs.microsoft.com/en-us/nuget/api/service-index

            if (repositoryUrl.AbsoluteUri.EndsWith("index.json")) {
                int wildcardIndex = Array.FindIndex(Name, p => String.Equals(p, "*", StringComparison.OrdinalIgnoreCase));
                // -1 -> continue on (like outside this if condition above and then below get flagged for URL online cond)
                // 0  -> CreateCatalogReader and yield at end
                // 1 or more -> write error and yield back. or strip other elements and handle as case 2?
                // "*" -> ok
                // "*", "Carbon" -> not ok, doesn't generate error
                // "Carbon", "*" -> not ok, generates error tho

                // Carbon*, PowerShellGet


                if (wildcardIndex == 0 && Name.Length == 1)
                {
                    foreach(PSResourceInfo pkg in SearchFromCatalogReader(repoName, repositoryUrl, cancellationToken))
                    {
                        // pkg.Type = repositoryUrl or null if PSGallery
                        yield return pkg;
                    }
                }
                else if (wildcardIndex > 0 || (wildcardIndex == 0 && Name.Length > 1))
                {
                    // TODO: ask- here do we want to just write the error and not do any search. OR discard other names, write error and search "*" still?
                    WriteError(new ErrorRecord(
                        new PSInvalidOperationException("Name array cannot contain additional elements if one is '*'."),
                        "NameCannotBeNullOrWhitespace",
                        ErrorCategory.InvalidArgument,
                        this));
                    yield break;
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
                    WriteVerbose("in caller method");
                    yield return pkg;
                }
            }
        }

        public IEnumerable<PSResourceInfo> SearchAcrossNamesInRepository(string repoName, PackageSearchResource pkgSearchResource, PackageMetadataResource pkgMetadataResource, SearchFilter searchFilter, SourceCacheContext srcContext, List<string> pkgsLeftToFind, CancellationToken cancellationToken)
        {
            WriteVerbose("made it here 3");
            foreach (string pkgName in Name)
            {
                // TODO:
                // if (nullwhitespace)
                // write error + yield break/continue ? (even better: check each name element in preprocessing of Name)

                // if (not pakcages left to find)
                // yield break?

                if (pkgsLeftToFind.Any())
                {
                    if (!String.IsNullOrWhiteSpace(pkgName))
                    {
                        foreach (PSResourceInfo pkg in FindFromPackageSourceSearchAPI(repoName, pkgName, pkgSearchResource, pkgMetadataResource, searchFilter, srcContext, pkgsLeftToFind, cancellationToken))
                        {
                            if (Tag == null || (Tag != null && IsTagMatch(pkg)))
                            {
                                yield return pkg;
                            }
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
            WriteVerbose("made it here 4");
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
                    // TODO: check if filter by Type here, for PSGallery resource OR maybe do it after this method returns
                    retrievedPkgs = pkgMetadataResource.GetMetadataAsync(name, Prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();
                    // if (repoName == PSGallery)
                    // iterate through retrievedPkgs and check each one's Tag's if equals PSScript && Type != Script --> Type assign as Module
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

                // TODO: if wildcardPackages.Count == 6000 (verify!) ->  to call SearchAsync(6000, 8000) again to get rest of packages

                WildcardPattern nameWildcardPattern = new WildcardPattern(name, WildcardOptions.IgnoreCase);
                // filter additionally because NuGet wildcard returns more than we need
                foundPackagesMetadata.AddRange(wildcardPkgs.Where(p => nameWildcardPattern.IsMatch(p.Identity.Id)).ToList());

                // if the Script Uri endpoint still needs to be searched, don't remove the wildcard name from pkgsLeftToFind
                // TODO: change condition to Type == Module too
                bool needToCheckPSGalleryScriptsRepo = String.Equals(repoName, "PSGallery", StringComparison.OrdinalIgnoreCase) && Type == null;
                if (foundPackagesMetadata.Any() && !needToCheckPSGalleryScriptsRepo)
                {
                    pkgsLeftToFind.Remove(name);
                }
            }

            // check if foundPackagesMetadata not empty

            // filter by param: Version
            if (Version == null)
            {
                // if no Version parameter provided, return latest version for each package
                // TODO: case insensitive grouping, can GroupBy take case insensitive string comparison
                foundPackagesMetadata = foundPackagesMetadata.GroupBy(p => p.Identity.Id).Select(x => x.OrderByDescending(p => p.Identity.Version, VersionComparer.VersionRelease).FirstOrDefault()).ToList();
            }
            else
            {
                // TODO: PR comments
                if (!Utils.TryParseVersionOrVersionRange(Version, out VersionRange versionRange, out bool allVersions, this))
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
                    if (String.Equals(name, "*"))
                    {
                        // TODO: high impact warning and ShouldProcess here! if not ShouldProcess then break, otherwise continue
                        // version could either be * (allVersions), specific, range (are last two also high impact)?
                        // TODO: have message to tell user if you'd like to Find XYZ, use different parameter set or different cmdlet: ask Sydney
                        yield break;
                    }

                    // -Name containing wc with Version "*", or specific range
                    // at this point foundPackagesMetadata contains latest version for each package, get list of distinct
                    // package names and get all versions for each name, this is due to the SearchAsync and GetMetadataAsync() API restrictions !
                    List<IPackageSearchMetadata> temp = new List<IPackageSearchMetadata>();
                    foreach (string n in foundPackagesMetadata.Select(p => p.Identity.Id).Distinct())
                    {
                        // get all versions for this package
                        temp.AddRange(pkgMetadataResource.GetMetadataAsync(n, Prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult().ToList());
                    }
                    foundPackagesMetadata = temp;
                    if (allVersions) // Version = "*"
                    {
                        foundPackagesMetadata = foundPackagesMetadata.GroupBy(
                            p => p.Identity.Id).SelectMany(
                                x => x.OrderByDescending(
                                    p => p.Identity.Version, VersionComparer.VersionRelease)).ToList();
                    }
                    else // Version range
                    {
                        foundPackagesMetadata = foundPackagesMetadata.Where(p => versionRange.Satisfies(p.Identity.Version)).GroupBy(p => p.Identity.Id).SelectMany(x => x.OrderByDescending(p => p.Identity.Version, VersionComparer.VersionRelease)).ToList();
                    }
                }
                else // name doesn't contain wildcards
                {
                    // allVersions for non-wildcard name is all versions (bc SearchAsync() was used) and is already ordered descending.
                    // TODO: tie it back to GetMetadataAsync() API about how we already have all versions bc of this API


                    if (!allVersions) // Version range
                    {
                        foundPackagesMetadata = foundPackagesMetadata.Where(p => versionRange.Satisfies(p.Identity.Version, VersionComparer.VersionRelease)).OrderByDescending(p => p.Identity.Version).ToList();
                    }
                }
            }

            foreach (IPackageSearchMetadata pkg in foundPackagesMetadata)
            {
                PSResourceInfo currentPkg = new PSResourceInfo();
                if(!PSResourceInfo.TryParse(pkg, out currentPkg, out string errorMsg)){
                    WriteError(new ErrorRecord(
                        new PSInvalidOperationException("Error parsing IPackageSearchMetadata to PSResourceInfo with message: " + errorMsg),
                        "IPackageSearchMetadataToPSResourceInfoParsingError",
                        ErrorCategory.InvalidResult,
                        this));
                    yield break;
                }
                yield return currentPkg;
            }
        }


        private bool IsTagMatch(PSResourceInfo pkg)
        {
            WriteVerbose("made it here 5");
            // TODO: check if Tag null here, so cleaner in caller method
            // TODO: resolve case sensitivty + look at Intersect takes StringComparator
            return Tag.Intersect(pkg.Tags).ToList().Count > 0;
        }

        // private void FilterByTypeAtm(PSResourceInfo pkg, HashSet<string> otherTags, List<string> otherNames)
        // {
        //     // HashSet<string> otherTags = new HashSet<string>();

        //     bool hasPSModule = false;
        //     bool hasPSScript = false;
        //     bool hasModule = false;
        //     foreach (string t in pkg.Tags)
        //     {
        //         if (String.Equals("PSModule", t, StringComparison.OrdinalIgnoreCase))
        //         {
        //             hasPSModule = true;
        //         }
        //         if (String.Equals("PSScript", t, StringComparison.OrdinalIgnoreCase))
        //         {
        //             hasPSScript = true;
        //         }
        //         if (String.Equals("Module", t, StringComparison.OrdinalIgnoreCase))
        //         {
        //             hasModule = true;
        //         }
        //     }
        //     if (!(hasPSModule || hasPSScript || hasModule))
        //     {
        //         foreach (string t in pkg.Tags)
        //         {
        //             otherTags.Add(t);
        //         }
        //         // WriteVerbose("NAME: " + pkg.Name + " tags: " + otherTags.ToString());
        //         otherNames.Add(pkg.Name);
        //     }
        //     if (hasModule && !(hasPSModule || hasPSScript))
        //     {
        //         otherNames.Add("JUSTMODULE-" + pkg.Name);
        //     }
        // }

        // private List<PSResourceInfo> FilterByResourceType(List<PSResourceInfo> pkgs)
        // {
        //     List<PSResourceInfo> filteredPkgs = new List<PSResourceInfo>();
        //     foreach (PSResourceInfo p in pkgs)
        //     {
        //         // Type tags:
        //         // PSScript -> def script
        //         // Script -> this resource would be tagged as Module on PSGallery but just have Script tag
        //         // PSModule -> def Module
        //         // Module -> could there be resources with PSScript + Module tags?
        //         // others -> did these PSModule/PSScript && Module/Script?
        //         if (p.Type == Type.ToString())
        //         {
        //             filteredPkgs.Add(p);
        //         }
        //     }
        //     return filteredPkgs;
        // }

        // private List<IPackageSearchMetadata> FilterPkgsByResourceType(List<IPackageSearchMetadata> foundPkgs)
        // {
        //     List<IPackageSearchMetadata> pkgsFilteredByTags = new List<IPackageSearchMetadata>();
        //     char[] delimiter = new char[] { ' ', ',' };
        //     foreach (IPackageSearchMetadata pkg in foundPkgs)
        //     {
        //         string[] tags = pkg.Tags.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
        //         foreach (string tag in tags)
        //         {
        //             if(tag == "PS" + Type)
        //             {
        //                 pkgsFilteredByTags.Add(pkg);
        //             }
        //             else if (Enum.TryParse(tag, out ResourceCategory parsedType) && parsedType == ResourceCategory.Module)
        //             {
        //                 pkgsFilteredByTags.Add(pkg);
        //             }
        //         }
        //     }
        //     return pkgsFilteredByTags;
        // }

        #region CatalogReader Helper Methods
        private IEnumerable<PSResourceInfo> SearchFromCatalogReader(string repoName, Uri repositoryUrl, CancellationToken cancellationToken)
        {
            WriteVerbose("made it here 6");
            using (var feedReader = new FeedReader(repositoryUrl))
            {
                bool hasCatalog = false;
                try
                {
                    hasCatalog = feedReader.HasCatalog().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    WriteError(new ErrorRecord(
                        new PSInvalidOperationException("Error creating or accessing feed: " + ex.Message),
                        "CantCreateFeedForV3Repository",
                        ErrorCategory.InvalidOperation,
                        this));
                    yield break;
                }
                if (!hasCatalog)
                {
                    // use SearchAsync() to return upto 6000 packages, ADO feeds
                    // if # of packages returned == 6000, writeDebug("there may be more packages. Search by name specifically");
                    WriteError(new ErrorRecord(
                        new PSInvalidOperationException("This V3 package feed source does not contain a catalog resource. Searching for all packages is not supported at the moment."),
                        "PackageSourceFeedDoesNotImplementCatalog",
                        ErrorCategory.InvalidOperation,
                        this));
                    yield break;
                }

            }

            // at this point, we should be able to access feed and it's known catalog resource
            Dictionary<string, CatalogEntry> uniquePkgs = new Dictionary<string, CatalogEntry>();
            foreach (CatalogEntry pkg in YieldCatalogEntriesAsFound(repositoryUrl, cancellationToken))
            {
                if((Prerelease || !pkg.Version.IsPrerelease) && ((!uniquePkgs.TryGetValue(pkg.Id.ToLower(), out CatalogEntry entry)) || (pkg.Version > entry.Version)))
                {
                    uniquePkgs[pkg.Id.ToLower()] = pkg;
                }
            }
            foreach (CatalogEntry entry in uniquePkgs.Values)
            {
                if (!PSResourceInfo.TryParseCatalogEntry(entry, out PSResourceInfo currentPkg, out string errorMsg))
                {
                    WriteError(new ErrorRecord(
                        new PSInvalidOperationException("Error parsing CatalogEntry to PSResourceInfo with message: " + errorMsg),
                        "CatalogEntryToPSResourceInfoParsingError",
                        ErrorCategory.InvalidResult,
                        this));
                    yield break;
                }
                yield return currentPkg;
            }
        }

        private IEnumerable<CatalogEntry> YieldCatalogEntriesAsFound(Uri repositoryUrl, CancellationToken cancellationToken)
        {
            WriteVerbose("made it here 7");
            using (var catalog = new CatalogReader(repositoryUrl))
            {
                if (catalog == null)
                {
                    WriteError(new ErrorRecord(
                        new PSInvalidOperationException("Catalog expected to be not null"),
                        "NullCatalogReader",
                        ErrorCategory.InvalidResult,
                        this));
                    yield break;
                }
                foreach(CatalogEntry p in catalog.GetFlattenedEntriesAsync(cancellationToken).GetAwaiter().GetResult())
                {
                    // TODO: ask Jim about denoting infinite progress (can't tell out of how many packages yet)
                    yield return p;
                }
            }
        }
        #endregion


    }
}