using System.Security.AccessControl;
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
using System.Collections.ObjectModel;

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
                    // items = ProcessRecordResourcePSSet();
                    foreach (PSResourceInfo pkgObj in ProcessRecordResourcePSSet())
                    {
                        WriteObject(pkgObj);
                    }
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
        }

        #endregion


        private IEnumerable<PSResourceInfo> ProcessRecordResourcePSSet()
        {
            source = new CancellationTokenSource();
            cancellationToken = source.Token;


            List<PSRepositoryInfo> repositoriesToSearch = RepositorySettings.Read(Repository, out string[] errorList);

            // List<PSResourceInfo> foundPkgs = new List<PSResourceInfo>();

            pkgsLeftToFind = Name.ToList();

            for (int i = 0; i < repositoriesToSearch.Count && pkgsLeftToFind.Any(); i++)
            {
                WriteVerbose(String.Format("iterating through repository: {0}", repositoriesToSearch[i].Name));
                WriteDebug(string.Format("Searching in repository {0}", repositoriesToSearch[i].Name));
                // foundPkgs.AddRange(FindPackagesFromSourcePSSet(repositoriesToSearch[i].Name, repositoriesToSearch[i].Url, cancellationToken));
                foreach (var pkg in FindPackagesFromSourcePSSet(repositoriesToSearch[i].Name, repositoriesToSearch[i].Url, cancellationToken))
                {
                    WriteVerbose("in main one here");
                    // if (pkg != null)
                    // {
                    //     WriteVerbose("yielding in highest level method: " + pkg.Name);
                    //     yield return pkg;
                    // }
                    WriteVerbose("yielding in highest level method: " + pkg.Name);
                    yield return pkg;
                }
            }
        }


        public IEnumerable<PSResourceInfo> FindPackagesFromSourcePSSet(string repoName, Uri repositoryUrl, CancellationToken cancellationToken)
        {
            List<PSResourceInfo> returnedPkgs = new List<PSResourceInfo>();
            // Collection<PSResourceInfo> returnedPkgs = new Collection<PSResourceInfo>();

            if (repositoryUrl.Scheme == Uri.UriSchemeFile)
            {
                FindLocalPackagesResourceV2 localResource = new FindLocalPackagesResourceV2(repositoryUrl.ToString());

                LocalPackageSearchResource resourceSearch = new LocalPackageSearchResource(localResource);
                LocalPackageMetadataResource resourceMetadata = new LocalPackageMetadataResource(localResource);

                SearchFilter filter = new SearchFilter(Prerelease);
                SourceCacheContext context = new SourceCacheContext();

                foreach (string indivName in Name)
                {
                    if (pkgsLeftToFind.Any())
                    {
                        foreach (PSResourceInfo pkg in FindPackagesFromSourceHelperPSSet(repoName, repositoryUrl, indivName, resourceSearch, resourceMetadata, filter, context))
                        {
                            // if (pkg != null)
                            // {
                            //     // returnedPkgs.Add(pkg);
                            //     yield return pkg;
                            // }
                            yield return pkg;
                        }
                        // var foundPkgs = FindPackagesFromSourceHelperPSSet(repoName, repositoryUrl, indivName, resourceSearch, resourceMetadata, filter, context);
                        // if (foundPkgs.Any())
                        // {
                        //     returnedPkgs.AddRange(foundPkgs);
                        // }
                    }
                }
            }
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


                PackageSearchResource resourceSearch = null;
                PackageMetadataResource resourceMetadata = null;
                try
                {
                    resourceSearch = repository.GetResourceAsync<PackageSearchResource>().GetAwaiter().GetResult();
                    resourceMetadata = repository.GetResourceAsync<PackageMetadataResource>().GetAwaiter().GetResult();
                }
                catch (Exception e){
                    WriteDebug("Error retrieving resource from repository: " + e.Message);
                    // yield return null;
                    // return returnedPkgs.Cast<PSResourceInfo>();
                }
                if (resourceSearch == null || resourceMetadata == null)
                {
                    WriteVerbose("yielding null 2");
                    // yield return null;
                    yield break;
                }

                SearchFilter filter = new SearchFilter(Prerelease);
                SourceCacheContext context = new SourceCacheContext();

                foreach (string indivName in Name)
                {
                    if (pkgsLeftToFind.Any())
                    {
                        foreach (PSResourceInfo pkg in FindPackagesFromSourceHelperPSSet(repoName, repositoryUrl, indivName, resourceSearch, resourceMetadata, filter, context))
                        {
                            // if (pkg != null)
                            // {
                            //     // returnedPkgs.Add(pkg);
                            //     WriteVerbose("yielding: " + pkg.Name);
                            //     yield return pkg;
                            // }
                            // returnedPkgs.Add(pkg);
                            WriteVerbose("yielding: " + pkg.Name);
                            yield return pkg;
                        }
                    }
                }
            }

        }

        private IEnumerable<PSResourceInfo> FindPackagesFromSourceHelperPSSet(string repoName, Uri repositoryUrl, string name, PackageSearchResource pkgSearchResource, PackageMetadataResource pkgMetadataResource, SearchFilter searchFilter, SourceCacheContext srcContext)
        {
            List<PSResourceInfo> foundResources = new List<PSResourceInfo>();
            List<IPackageSearchMetadata> foundPackagesMetadata = new List<IPackageSearchMetadata>();


            Collection<PSResourceInfo> foundPackages = new Collection<PSResourceInfo>();

            if (!String.IsNullOrEmpty(name))
            {
                // If a resource name is specified, search for that specific pkg name
                if (!name.Contains("*"))
                {
                    IEnumerable<IPackageSearchMetadata> retrievedPkgs = null;
                    try
                    {
                        retrievedPkgs = pkgMetadataResource.GetMetadataAsync(name, Prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();
                    }
                    catch { }
                    if (retrievedPkgs == null || retrievedPkgs.Count() == 0)
                    {
                        this.WriteVerbose(string.Format("'{0}' could not be found in repository '{1}'", name, repoName));
                        // return foundPackages;
                        // yield return null;
                        yield break;
                    }
                    else
                    {
                        foundPackagesMetadata.AddRange(retrievedPkgs.ToList());
                    }
                }
                // contains wildcard, so searching for range of pkgs
                else
                {
                    var wildcardPkgs = pkgSearchResource.SearchAsync(name, searchFilter, 0, 6000, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();
                    WildcardPattern nameWildcardPattern = new WildcardPattern(name, WildcardOptions.IgnoreCase);
                    foundPackagesMetadata.AddRange(wildcardPkgs.Where(p => nameWildcardPattern.IsMatch(p.Identity.Id)).ToList());

                    if (foundPackagesMetadata.Any())
                    {
                        pkgsLeftToFind.Remove(name);
                    }
                }
            }


            if (Version == null)
            {
                // if Version is null -> return latest version
                foundPackagesMetadata.OrderByDescending(p => p.Identity.Version, VersionComparer.VersionRelease);
                foundPackagesMetadata.RemoveRange(1, foundPackagesMetadata.Count -1);
            }


            // return foundPackages;
            // this returns all packages matching that name, i.e if Carbon returns ALL versions of carbon
            foreach (IPackageSearchMetadata pkg in foundPackagesMetadata)
            {
                WriteVerbose(String.Format("extracting from IPackageSearchMetadata of pkg name: {0}", pkg.Identity.Id));

                PSResourceInfo currentPkg = new PSResourceInfo();
                currentPkg.Name = pkg.Identity.Id;
                currentPkg.Version = pkg.Identity.Version.Version;
                currentPkg.Repository = repoName;
                currentPkg.Description = pkg.Description;
                yield return currentPkg;
            }
        }

    }
}