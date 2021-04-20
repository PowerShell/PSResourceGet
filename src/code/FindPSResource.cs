using System.Reflection.Emit;
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
using LinqKit;
using MoreLinq.Extensions;
using NuGet.Versioning;
using System.Data;
using System.Linq;
using System.Net;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System.Net.Http;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static System.Environment;
using Dbg = System.Diagnostics.Debug;

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
            Script,
            DscResource,
            Command,
            RoleCapability
        };
        #endregion

        #region Members
        private const string ResourceNameParameterSet = "ResourceNameParameterSet";
        private const string CommandNameParameterSet = "CommandNameParameterSet";
        private const string DscResourceNameParameterSet = "DscResourceNameParameterSet";

        // This will be a list of all the repository caches
        public static readonly List<string> RepoCacheFileName = new List<string>();
        // Temporarily store cache in this path for testing purposes
        public static readonly string RepositoryCacheDir = Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "PowerShellGet", "RepositoryCache");
        //public static readonly string RepositoryCacheDir = @"%APPDATA%/PowerShellGet/repositorycache";//@"%APPDTA%\NuGet";
        //private readonly object p;

        // Define the cancellation token.
        CancellationTokenSource source;
        CancellationToken cancellationToken;
        List<string> pkgsLeftToFind;
        private const string CursorFileName = "cursor.json";

        #endregion

        #region Parameters

        /// <summary>
        /// todo
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = ResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; }

        // todo: Type param here
        /// <summary>
        /// todo
        /// </summary>
        [Parameter(ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = ResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public ResourceCategory Type { get; set; }


        /// <summary>
        /// todo
        /// </summary>
        [Parameter(ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ParameterSetName = CommandNameParameterSet)]
        [Parameter(ParameterSetName = DscResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }

        /// <summary>
        /// todo
        /// </summary>
        [Parameter(ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ParameterSetName = CommandNameParameterSet)]
        [Parameter(ParameterSetName = DscResourceNameParameterSet)]
        public SwitchParameter Prerelease { get; set; }

        /// <summary>
        /// todo
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = CommandNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = DscResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string ModuleName { get; set; }

        /// <summary>
        /// todo
        /// </summary>
        [Parameter(ValueFromPipeline = true, ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ValueFromPipeline = true, ParameterSetName = CommandNameParameterSet)]
        [Parameter(ValueFromPipeline = true, ParameterSetName = DscResourceNameParameterSet)]
        [ValidateNotNull]
        public string[] Tags { get; set; }

        /// <summary>
        /// todo
        /// </summary>
        [Parameter(ValueFromPipeline = true, ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ValueFromPipeline = true, ParameterSetName = CommandNameParameterSet)]
        [Parameter(ValueFromPipeline = true, ParameterSetName = DscResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        /// <summary>
        /// todo
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = CommandNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = DscResourceNameParameterSet)]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// todo
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = CommandNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = DscResourceNameParameterSet)]
        public SwitchParameter IncludeDependencies { get; set; }

        #endregion

        #region Methods
        protected override void ProcessRecord()
        {
            List<PSResourceInfo> items = new List<PSResourceInfo>();

            switch (ParameterSetName)
            {
                case ResourceNameParameterSet:
                    WriteVerbose("in ResourceNameParameterSet");
                    items = ProcessRecordResourcePSSet();
                    break;

                case CommandNameParameterSet:
                    // todo
                    break;

                case DscResourceNameParameterSet:
                    // todo
                    break;

                default:
                    Dbg.Assert(false, "Invalid parameter set");
                    break;
            }

            foreach (PSResourceInfo item in items)
            {
                WriteObject(item);
            }
        }

        // private void ProcessRecordResourcePSSet()
        // {
        //     CancellationToken cancellationToken = new CancellationToken(); //todo possibly get from source
        //     List<PSRepositoryInfo> repositoriesToSearch = RepositorySettings.Read(Repository, out string[] errorList);
        //     List<string> pkgsLeftToFind = Name.ToList();
        //     // not sure what this does?
        //     List<IEnumerable<IPackageSearchMetadata>> returnedPkgsFound = new List<IEnumerable<IPackageSearchMetadata>>();

        //     foreach (PSRepositoryInfo repo in repositoriesToSearch)
        //     {
        //         WriteDebug(String.Format("Searching in repository {0}", repo.Name));
        //         if (repo.Url.ToString().EndsWith("/v3/index.json"))
        //         {
        //             WriteDebug("Search V3 style");
        //         }

        //         // todo why not just pass Uri?
        //         returnedPkgsFound.AddRange(FindPackagesFromSource(repo.Name, repo.Url.ToString(), cancellationToken));

        //         var flattenedPkgs = returnedPkgsFound.Flatten();
        //         if (flattenedPkgs.Any() && flattenedPkgs.First() != null)
        //         {
        //             foreach (IPackageSearchMetadata pkg in flattenedPkgs)
        //             {
        //                 // create PSResourceInfo object and add to list to return or writeObject() here
        //             }
        //         }

        //         // reset found packages...not needed imo if we just create the var in the foreach
        //         returnedPkgsFound.Clear();
        //         if (!pkgsLeftToFind.Any())
        //         {
        //             break;
        //         }
        //     }
        // }

        // // why not return List of PSResourceInfo object?
        // private List<IEnumerable<IPackageSearchMetadata>> FindPackagesFromSource(string repoName, string repoUrl, CancellationToken cancellationToken)
        // {
        //     return new List<IEnumerable<IPackageSearchMetadata>>();
        // }
        #endregion


        private List<PSResourceInfo> ProcessRecordResourcePSSet()
        {
            source = new CancellationTokenSource();
            cancellationToken = source.Token;


            List<PSRepositoryInfo> repositoriesToSearch = RepositorySettings.Read(Repository, out string[] errorList);

            // var foundPkgs = new List<IEnumerable<IPackageSearchMetadata>>();
            List<PSResourceInfo> foundPkgs = new List<PSResourceInfo>();

            pkgsLeftToFind = Name.ToList();

            for (int i = 0; i < repositoriesToSearch.Count && pkgsLeftToFind.Any(); i++)
            {
                WriteVerbose(String.Format("iterating through repository: {0}", repositoriesToSearch[i].Name));
                WriteDebug(string.Format("Searching in repository {0}", repositoriesToSearch[i].Name));
                foundPkgs.AddRange(FindPackagesFromSourcePSSet(repositoriesToSearch[i].Name, repositoriesToSearch[i].Url, cancellationToken));
            }
            return foundPkgs;
        }


        public List<PSResourceInfo> FindPackagesFromSourcePSSet(string repoName, Uri repositoryUrl, CancellationToken cancellationToken)
        {
            List<PSResourceInfo> returnedPkgs = new List<PSResourceInfo>();

            if (repositoryUrl.Scheme == Uri.UriSchemeFile)
            {
                FindLocalPackagesResourceV2 localResource = new FindLocalPackagesResourceV2(repositoryUrl.ToString());

                LocalPackageSearchResource resourceSearch = new LocalPackageSearchResource(localResource);
                LocalPackageMetadataResource resourceMetadata = new LocalPackageMetadataResource(localResource);

                SearchFilter filter = new SearchFilter(Prerelease);
                SourceCacheContext context = new SourceCacheContext();

                // // todo: how would Name be null since we validate [NotNullOrEmpty], how length == 0? (empty name array) or like name is ""?
                // if ((Name == null) || (Name.Length == 0))
                // {
                //     returnedPkgs.AddRange(FindPackagesFromSourceHelper(repoName, repositoryUrl, null, resourceSearch, resourceMetadata, filter, context));
                // }

                foreach (string indivName in Name)
                {
                    if (pkgsLeftToFind.Any())
                    {
                        var foundPkgs = FindPackagesFromSourceHelperPSSet(repoName, repositoryUrl, indivName, resourceSearch, resourceMetadata, filter, context);
                        if (foundPkgs.Any())
                        {
                            returnedPkgs.AddRange(foundPkgs);
                        }
                        // todo: ask Amber why this is here
                        // if (Repository == null || !Repository[0].Equals("*"))
                        // {
                        //     pkgsLeftToFind.Remove(indivName);
                        // }
                    }
                }
            }
            else
            {
                WriteVerbose("Uri is not file based");
                PackageSource source = new PackageSource(repositoryUrl.ToString());
                if (Credential != null)
                {
                    string password = new NetworkCredential(string.Empty, Credential.Password).Password;
                    source.Credentials = PackageSourceCredential.FromUserInput(repositoryUrl.ToString(), Credential.UserName, password, true, null);
                }

                var provider = FactoryExtensionsV3.GetCoreV3(NuGet.Protocol.Core.Types.Repository.Provider);

                SourceRepository repository = new SourceRepository(source, provider);


                PackageSearchResource resourceSearch = null;
                PackageMetadataResource resourceMetadata = null;
                try
                {
                    resourceSearch = repository.GetResourceAsync<PackageSearchResource>().GetAwaiter().GetResult();
                    resourceMetadata = repository.GetResourceAsync<PackageMetadataResource>().GetAwaiter().GetResult();
                }
                catch (Exception e){
                    WriteDebug("Error retrieving resource from repository: " + e.Message);
                    return returnedPkgs;
                }

                SearchFilter filter = new SearchFilter(Prerelease);
                SourceCacheContext context = new SourceCacheContext();


                // // todo: how would Name be null since we validate [NotNullOrEmpty], how length == 0? (empty name array) or like name is ""?
                // if ((Name == null) || (Name.Length == 0))
                // {
                //     returnedPkgs.AddRange(FindPackagesFromSourceHelperPSSet(repoName, repositoryUrl, null, resourceSearch, resourceMetadata, filter, context));
                // }

                foreach (string indivName in Name)
                {
                    if (pkgsLeftToFind.Any())
                    {
                        WriteVerbose(String.Format("packages left to find and currently looking for name: {0}", indivName));
                        var foundPkgs = FindPackagesFromSourceHelperPSSet(repoName, repositoryUrl, indivName, resourceSearch, resourceMetadata, filter, context);
                        WriteVerbose("came back from helper()");
                        if (foundPkgs.Any())
                        {
                            returnedPkgs.AddRange(foundPkgs);


                            // if the repository is not specified or the repository is specified (but it's not '*'), then we can stop continuing to search for the package
                            if (Repository == null ||  !Repository[0].Equals("*"))
                            {
                                pkgsLeftToFind.Remove(indivName);
                            }
                        }
                    }
                }
            }
            WriteVerbose("returning from findPackagesFromSource");
            return returnedPkgs;
        }

        private List<PSResourceInfo> FindPackagesFromSourceHelperPSSet(string repoName, Uri repositoryUrl, string name, PackageSearchResource pkgSearchResource, PackageMetadataResource pkgMetadataResource, SearchFilter searchFilter, SourceCacheContext srcContext)
        {
            WriteVerbose("in helper() now");
            List<IEnumerable<IPackageSearchMetadata>> foundPackages = new List<IEnumerable<IPackageSearchMetadata>>();
            List<IEnumerable<IPackageSearchMetadata>> filteredFoundPkgs = new List<IEnumerable<IPackageSearchMetadata>>();
            List<IEnumerable<IPackageSearchMetadata>> scriptPkgsNotNeeded = new List<IEnumerable<IPackageSearchMetadata>>();

            List<PSResourceInfo> foundResources = new List<PSResourceInfo>();
            List<IPackageSearchMetadata> foundPkgsWithoutEnumerable = new List<IPackageSearchMetadata>();

            if (!String.IsNullOrEmpty(name))
            {
                WriteVerbose("name not null or empty");
                // If a resource name is specified, search for that specific pkg name
                if (!name.Contains("*"))
                {
                    WriteVerbose("name doesn't contain wildcard");
                    IEnumerable<IPackageSearchMetadata> retrievedPkgs = null;
                    try
                    {
                        retrievedPkgs = pkgMetadataResource.GetMetadataAsync(name, Prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();
                        // create PSRepositoryInfo objects from EACH metadata ? holy heck that will be expensive
                    }
                    catch { }
                    if (retrievedPkgs == null || retrievedPkgs.Count() == 0)
                    {
                        this.WriteVerbose(string.Format("'{0}' could not be found in repository '{1}'", name, repoName));
                        return foundResources;
                    }
                    else
                    {
                        WriteVerbose("retrieved packages was not null");
                        // foundPackages.Add(retrievedPkgs);
                        foundPkgsWithoutEnumerable.AddRange(retrievedPkgs.ToList());
                    }
                }
                // contains wildcard, so searching for range of pkgs
                else
                {
                    // TODO:  follow up on this
                    //foundPackages.Add(pkgSearchResource.SearchAsync(name, searchFilter, 0, 6000, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult());

                    // name = name.Equals("*") ? "" : name;   // can't use * in v3 protocol
                    var wildcardPkgs = pkgSearchResource.SearchAsync(name, searchFilter, 0, 6000, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();
                    // can account for na*me or * or "" (last one idk?)
                    WildcardPattern nameWildcardPattern = new WildcardPattern(name, WildcardOptions.IgnoreCase);
                    // foundPackages.Add(wildcardPkgs.Where(p => nameWildcardPattern.IsMatch(p.Identity.Id)));
                    foundPkgsWithoutEnumerable.AddRange(wildcardPkgs.Where(p => nameWildcardPattern.IsMatch(p.Identity.Id)).ToList());
                    // if (foundPackages.Flatten().Any())
                    // {
                    //     pkgsLeftToFind.Remove(name);
                    // }
                    if (foundPkgsWithoutEnumerable.Any())
                    {
                        pkgsLeftToFind.Remove(name);
                    }

                    // // If not searching for *all* packages
                    // if (!name.Equals("") && !name[0].Equals('*'))
                    // {
                    //     WildcardPattern nameWildcardPattern = new WildcardPattern(name, WildcardOptions.IgnoreCase);
                    //     foundPackages.Add(wildcardPkgs.Where(p => nameWildcardPattern.IsMatch(p.Identity.Id)));
                    //     if (foundPackages.Flatten().Any())
                    //     {
                    //         pkgsLeftToFind.Remove(name);
                    //     }
                    // }
                    // else
                    // {
                    //     foundPackages.Add(wildcardPkgs);
                    //     pkgsLeftToFind.Remove("*");
                    // }


                }
            }


            // if (Version == null)
            // {
            //     // if Version is null -> return latest version
            //     foundPackages = foundPackages.OrderByDescending(p => p.Identity.Version, VersionComparer.VersionRelease);



            //     // if Version == "*" or no  version provided --> return latest version
            //     // specific version --> return that one
            //     // range provided --> ?

            // }
            /***
            // //use either ModuleName or Name (whichever not null) to prevent id error
            // var nameVal = name == null ? _moduleName : name;

            // Check version first to narrow down the number of pkgs before potentially searching through tags
            if (_version != null && nameVal != null)
            {

                if (_version.Equals("*"))
                {
                    // ensure that the latst version is returned first (the ordering of versions differ)

                    if(_moduleName != null)
                    {
                        // perform checks for PSModule before adding to filteredFoundPackages
                        filteredFoundPkgs.Add(pkgMetadataResource.GetMetadataAsync(nameVal, _prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult()
                            .Where(p => p.Tags.Split(delimiter, StringSplitOptions.RemoveEmptyEntries).Contains("PSModule"))
                            .OrderByDescending(p => p.Identity.Version, VersionComparer.VersionRelease));

                        scriptPkgsNotNeeded.Add(pkgMetadataResource.GetMetadataAsync(nameVal, _prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult()
                            .Where(p => p.Tags.Split(delimiter, StringSplitOptions.RemoveEmptyEntries).Contains("PSScript"))
                            .OrderByDescending(p => p.Identity.Version, VersionComparer.VersionRelease));

                        scriptPkgsNotNeeded.RemoveAll(p => true);
                    }
                    else
                    { //name != null
                        filteredFoundPkgs.Add(pkgMetadataResource.GetMetadataAsync(nameVal, _prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult().OrderByDescending(p => p.Identity.Version, VersionComparer.VersionRelease));
                    }
                }
                else
                {
                    // try to parse into a singular NuGet version
                    NuGetVersion specificVersion;
                    NuGetVersion.TryParse(_version, out specificVersion);

                    foundPackages.RemoveAll(p => true);
                    VersionRange versionRange = null;

                    //todo: fix! when the Version is inputted as "[2.0]" it doesnt get parsed by TryParse() returns null
                    if (specificVersion != null)
                    {
                        // exact version
                        versionRange = new VersionRange(specificVersion, true, specificVersion, true, null, null);
                    }
                    else
                    {
                        // maybe try catch this
                        // check if version range
                        versionRange = VersionRange.Parse(_version);

                    }
                    // Search for packages within a version range
                    // ensure that the latst version is returned first (the ordering of versions differ
                    // test wth  Find-PSResource 'Carbon' -Version '[,2.4.0)'
                    foundPackages.Add(pkgMetadataResource.GetMetadataAsync(nameVal, _prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult()
                        .Where(p => versionRange.Satisfies(p.Identity.Version))
                        .OrderByDescending(p => p.Identity.Version, VersionComparer.VersionRelease));

                    // TODO:  TEST AFTER CHANGES
                    // choose the most recent version -- it's not doing this right now
                    //int toRemove = foundPackages.First().Count() - 1;
                    //var singlePkg = (System.Linq.Enumerable.SkipLast(foundPackages.FirstOrDefault(), toRemove));
                    var pkgList = foundPackages.FirstOrDefault();
                    var singlePkg = Enumerable.Repeat(pkgList.FirstOrDefault(), 1);


                    if(singlePkg != null)
                    {
                        if(_moduleName != null)
                        {
                            var tags = singlePkg.First().Tags.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
                            if(tags.Contains("PSModule"))
                            {
                                filteredFoundPkgs.Add(singlePkg);
                            }
                        }
                        else if(_name != null)
                        {
                            filteredFoundPkgs.Add(singlePkg);
                        }
                    }
                }
            }
            else // version is null
            {
                // if version is null, but there we want to return multiple packages (muliple names), skip over this step of removing all but the lastest package/version:
                if ((name != null && !name.Contains("*")) || _moduleName != null)
                {
                    // choose the most recent version -- it's not doing this right now
                    int toRemove = foundPackages.First().Count() - 1;

                    //to prevent NullException
                    if(toRemove >= 0)
                    {
                        var pkgList = foundPackages.FirstOrDefault();
                        var singlePkg = Enumerable.Repeat(pkgList.FirstOrDefault(), 1);

                        //if it was a ModuleName then check if the Tag is PSModule and only add then
                        var tags = singlePkg.First().Tags.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
                        if(_moduleName != null){
                            if(tags.Contains("PSModule")){
                                filteredFoundPkgs.Add(singlePkg);
                            }
                        }
                        else
                        { // _name != null
                            filteredFoundPkgs.Add(singlePkg);
                        }
                    }
                }
                else
                { //if name not null,but name contains * and version is null
                    filteredFoundPkgs = foundPackages;
                }
            }




            // TAGS
            /// should move this to the last thing that gets filtered
            var flattenedPkgs = filteredFoundPkgs.Flatten().ToList();
            if (_tags != null)
            {
                // clear filteredfoundPkgs because we'll just be adding back the pkgs we we
                filteredFoundPkgs.RemoveAll(p => true);
                var pkgsFilteredByTags = new List<IPackageSearchMetadata>();

                foreach (IPackageSearchMetadata p in flattenedPkgs)
                {
                    // Enumerable.ElementAt(0)
                    var tagArray = p.Tags.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string t in _tags)
                    {
                        // if the pkg contains one of the tags we're searching for
                        if (tagArray.Contains(t, StringComparer.OrdinalIgnoreCase))
                        {
                            pkgsFilteredByTags.Add(p);
                        }
                    }
                }
                // make sure just adding one pkg if not * versions
                if (_version != "*")
                {
                    filteredFoundPkgs.Add(pkgsFilteredByTags.DistinctBy(p => p.Identity.Id));
                }
                else
                {
                    // if we want all version, make sure there's only one package id/version in the returned list.
                    filteredFoundPkgs.Add(pkgsFilteredByTags.DistinctBy(p => p.Identity.Version));
                }
            }




            // optimizes searcching by
            if ((_type != null || !filteredFoundPkgs.Flatten().Any()) && pkgsLeftToFind.Any() && !_name.Contains("*"))
            {
                //if ((_type.Contains("Module") || _type.Contains("Script")) && !_type.Contains("DscResource") && !_type.Contains("Command") && !_type.Contains("RoleCapability"))
                if (_type == null || _type.Contains("DscResource") || _type.Contains("Command") || _type.Contains("RoleCapability"))
                {

                    if (!filteredFoundPkgs.Flatten().Any())
                    {
                        filteredFoundPkgs.Add(pkgSearchResource.SearchAsync("", searchFilter, 0, 6000, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult());
                    }

                    var tempList = (FilterPkgsByResourceType(filteredFoundPkgs));
                    filteredFoundPkgs.RemoveAll(p => true);

                    filteredFoundPkgs.Add(tempList);
                }

            }
            // else type == null







            // Search for dependencies
            if (_includeDependencies && filteredFoundPkgs.Any())
            {
                List<IEnumerable<IPackageSearchMetadata>> foundDependencies = new List<IEnumerable<IPackageSearchMetadata>>();

                // need to parse the depenency and version and such
                var filteredFoundPkgsFlattened = filteredFoundPkgs.Flatten();
                foreach (IPackageSearchMetadata pkg in filteredFoundPkgsFlattened)
                {
                    // need to improve this later
                    // this function recursively finds all dependencies
                    // change into an ieunumerable (helpful for the finddependenciesfromsource function)

                    foundDependencies.AddRange(FindDependenciesFromSource(Enumerable.Repeat(pkg, 1), pkgMetadataResource, srcContext));
                }

                filteredFoundPkgs.AddRange(foundDependencies);
            }
            */

            // return foundPackages;
            // this returns all packages matching that name, i.e if Carbon returns ALL versions of carbon
            WriteVerbose("going to iterate through packages found (IPackageSearchMetadata)");
            // todo: what's a better way to iterate through an List<IEnumerable<IPackageMetadata>?
            List<IPackageSearchMetadata> pkgMetadatas = new List<IPackageSearchMetadata>();
            // foreach (var pkg in foundPackages)
            // {
            //     pkgMetadatas.AddRange(pkg.ToList());
            // }
            // foreach (var pkgM in pkgMetadatas)
            // {
            //     WriteVerbose(String.Format("extracting from IPackageSearchMetadata of pkg name: {0}", pkgM.Identity.Id));

            //     PSResourceInfo currentPkg = new PSResourceInfo();
            //     currentPkg.Name = pkgM.Identity.Id;
            //     currentPkg.Version = pkgM.Identity.Version.Version;
            //     currentPkg.Repository = repoName;
            //     currentPkg.Description = pkgM.Description;
            //     WriteVerbose("add PSResourceInfo obj to list");
            //     foundResources.Add(currentPkg);
            // }
            foreach (IPackageSearchMetadata pkg in foundPkgsWithoutEnumerable)
            {
                WriteVerbose(String.Format("extracting from IPackageSearchMetadata of pkg name: {0}", pkg.Identity.Id));

                PSResourceInfo currentPkg = new PSResourceInfo();
                currentPkg.Name = pkg.Identity.Id;
                currentPkg.Version = pkg.Identity.Version.Version;
                currentPkg.Repository = repoName;
                currentPkg.Description = pkg.Description;
                WriteVerbose("add PSResourceInfo obj to list");
                foundResources.Add(currentPkg);
            }
            // return foundPackages;
            WriteVerbose("return list of PSResourceInfo objects");
            return foundResources;


        }

    }
}