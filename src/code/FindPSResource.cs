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

            pkgsLeftToFind = Name.ToList();

            for (int i = 0; i < repositoriesToSearch.Count && pkgsLeftToFind.Any(); i++)
            {
                WriteDebug(string.Format("Searching in repository {0}", repositoriesToSearch[i].Name));
                foreach (var pkg in FindPackagesFromSourcePSSet(repositoriesToSearch[i].Name, repositoriesToSearch[i].Url, cancellationToken))
                {
                    yield return pkg;
                }
            }
        }

        public IEnumerable<PSResourceInfo> FindPackagesFromSourcePSSet(string repoName, Uri repositoryUrl, CancellationToken cancellationToken)
        {
            if (repositoryUrl.Scheme == Uri.UriSchemeFile)
            {
                FindLocalPackagesResourceV2 localResource = new FindLocalPackagesResourceV2(repositoryUrl.ToString());

                LocalPackageSearchResource resourceSearch = new LocalPackageSearchResource(localResource);
                LocalPackageMetadataResource resourceMetadata = new LocalPackageMetadataResource(localResource);

                SearchFilter filter = new SearchFilter(Prerelease);
                SourceCacheContext context = new SourceCacheContext();

                foreach(PSResourceInfo pkg in FindEachPackage(repoName, repositoryUrl, cancellationToken, resourceSearch, resourceMetadata, filter, context))
                {
                    yield return pkg;
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
                }
                if (resourceSearch == null || resourceMetadata == null)
                {
                    yield break;
                }

                SearchFilter filter = new SearchFilter(Prerelease);
                SourceCacheContext context = new SourceCacheContext();

                foreach(PSResourceInfo pkg in FindEachPackage(repoName, repositoryUrl, cancellationToken, resourceSearch, resourceMetadata, filter, context))
                {
                    yield return pkg;
                }
            }
        }


        public IEnumerable<PSResourceInfo> FindEachPackage(string repoName, Uri repositoryUrl, CancellationToken cancellationToken, PackageSearchResource pkgSearchResource, PackageMetadataResource pkgMetadataResource, SearchFilter searchFilter, SourceCacheContext srcContext)
        {
            foreach (string pkgName in Name)
            {
                if (pkgsLeftToFind.Any())
                {
                    if (!String.IsNullOrWhiteSpace(pkgName))
                    {
                        foreach (PSResourceInfo pkg in FindPackagesFromSourceHelperPSSet(repoName, repositoryUrl, pkgName, pkgSearchResource, pkgMetadataResource, searchFilter, srcContext))
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
        private IEnumerable<PSResourceInfo> FindPackagesFromSourceHelperPSSet(string repoName, Uri repositoryUrl, string name, PackageSearchResource pkgSearchResource, PackageMetadataResource pkgMetadataResource, SearchFilter searchFilter, SourceCacheContext srcContext)
        {
            List<IPackageSearchMetadata> foundPackagesMetadata = new List<IPackageSearchMetadata>();
            Collection<PSResourceInfo> foundPackages = new Collection<PSResourceInfo>();

            if (!String.IsNullOrEmpty(name))
            { // todo NOW: validate Name at higher level!! Remove RepositoryURL param!
                if (!name.Contains("*"))
                {
                    // case: searching for specific package name i.e "Carbon"
                    IEnumerable<IPackageSearchMetadata> retrievedPkgs = null;
                    try
                    {
                        retrievedPkgs = pkgMetadataResource.GetMetadataAsync(name, Prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();
                    }
                    catch { }
                    if (retrievedPkgs == null || retrievedPkgs.Count() == 0)
                    {
                        this.WriteVerbose(string.Format("'{0}' could not be found in repository '{1}'", name, repoName));
                        yield break;
                    }
                    else
                    {
                        foundPackagesMetadata.AddRange(retrievedPkgs.ToList());
                    }
                }
                else
                {
                    // case: searching for name containing wildcard i.e "Ca*bon"
                    // NuGet API doesn't handle wildcards so get all packages, then filter for wilcard match
                    IEnumerable<IPackageSearchMetadata> wildcardPkgs = pkgSearchResource.SearchAsync(name, searchFilter, 0, 6000, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();

                    WildcardPattern nameWildcardPattern = new WildcardPattern(name, WildcardOptions.IgnoreCase);
                    foundPackagesMetadata.AddRange(wildcardPkgs.Where(p => nameWildcardPattern.IsMatch(p.Identity.Id)).ToList());

                    if (foundPackagesMetadata.Any())
                    {
                        pkgsLeftToFind.Remove(name);
                    }
                }
            }

            // filter by Version parameter
            // Todo: implement other cases for Version
            if (Version == null)
            {
                // if no Version parameter provided, return latest version
                foundPackagesMetadata.OrderByDescending(p => p.Identity.Version, VersionComparer.VersionRelease);
                foundPackagesMetadata.RemoveRange(1, foundPackagesMetadata.Count -1);
            }

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