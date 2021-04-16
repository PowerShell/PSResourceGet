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
                    // todo
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

        protected override void ProcessRecordResourcePSSet()
        {
            source = new CancellationTokenSource();
            cancellationToken = source.Token;


            List<PSRepositoryInfo> repositoriesToSearch = RepositorySettings.Read(Repository, out string[] errorList);

            var returnedPkgsFound = new List<IEnumerable<IPackageSearchMetadata>>();
            pkgsLeftToFind = Name.ToList();
            foreach (PSRepositoryInfo repo in repositoriesToSearch)
            {
                WriteDebug(string.Format("Searching in repository '{0}'", repoName.Properties["Name"].Value.ToString()));
                // We'll need to use the catalog reader when enumerating through all packages under v3 protocol.
                // if using v3 endpoint and there's no exact name specified (ie no name specified or a name with a wildcard is specified)
                if (repoName.Properties["Url"].Value.ToString().EndsWith("/v3/index.json") &&
                    (_name.Length == 0 || _name.Any(n => n.Contains("*"))))//_name.Contains("*")))  /// TEST THIS!!!!!!!
                {
                    this.WriteWarning("This functionality is not yet implemented");
                    // right now you can use wildcards with an array of names, ie -name "Az*", "PS*", "*Get", will take a hit performance wise, though.
                    // TODO:  add wildcard condition here
                    //ProcessCatalogReader(repoName.Properties["Name"].Value.ToString(), repoName.Properties["Url"].Value.ToString());
                }



                // if it can't find the pkg in one repository, it'll look in the next one in the list
                // returns any pkgs found, and any pkgs that weren't found
                returnedPkgsFound.AddRange(FindPackagesFromSource(repoName.Properties["Name"].Value.ToString(), repoName.Properties["Url"].Value.ToString(), cancellationToken));


                // Flatten returned pkgs before displaying output returnedPkgsFound.Flatten().ToList()[0]
                var flattenedPkgs = returnedPkgsFound.Flatten();
                // flattenedPkgs.ToList();

                if (flattenedPkgs.Any() && flattenedPkgs.First() != null)
                {
                    foreach (IPackageSearchMetadata pkg in flattenedPkgs)
                    {
                        //WriteObject(pkg);


                        PSObject pkgAsPSObject = new PSObject();
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Name", pkg.Identity.Id));
                        // Version.Version ensures type is System.Version instead of type NuGetVersion
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Version", pkg.Identity.Version.Version));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Repository", repoName.Properties["Name"].Value.ToString()));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Description", pkg.Description));

                        WriteObject(pkgAsPSObject);

                    }
                }

                // reset found packages
                returnedPkgsFound.Clear();

                // if we need to search all repositories, we'll continue, otherwise we'll just return
                if (!pkgsLeftToFind.Any())
                {
                    break;
                }



            } // end of foreach
        }

        public List<IEnumerable<IPackageSearchMetadata>> FindPackagesFromSourcePSSet(string repoName, string repositoryUrl, CancellationToken cancellationToken)
        {
            List<IEnumerable<IPackageSearchMetadata>> returnedPkgs = new List<IEnumerable<IPackageSearchMetadata>>();

            if (repositoryUrl.StartsWith("file://"))
            {

                FindLocalPackagesResourceV2 localResource = new FindLocalPackagesResourceV2(repositoryUrl);

                LocalPackageSearchResource resourceSearch = new LocalPackageSearchResource(localResource);
                LocalPackageMetadataResource resourceMetadata = new LocalPackageMetadataResource(localResource);

                SearchFilter filter = new SearchFilter(_prerelease);
                SourceCacheContext context = new SourceCacheContext();


                if ((_name == null) || (_name.Length == 0))
                {
                    returnedPkgs.AddRange(FindPackagesFromSourceHelper(repoName, repositoryUrl, null, resourceSearch, resourceMetadata, filter, context));
                }

                foreach(string n in _name){
                    if (pkgsLeftToFind.Any())
                    {
                        var foundPkgs = FindPackagesFromSourceHelper(repoName,repositoryUrl, n, resourceSearch, resourceMetadata, filter, context);
                        if (foundPkgs.Any() && foundPkgs.First().Count() != 0)
                        {
                            returnedPkgs.AddRange(foundPkgs);


                            // if the repository is not specified or the repository is specified (but it's not '*'), then we can stop continuing to search for the package
                            if (_repository == null ||  !_repository[0].Equals("*"))
                            {
                                pkgsLeftToFind.Remove(n);
                            }
                        }
                    }
                }
            }
            else
            {



                PackageSource source = new PackageSource(repositoryUrl);
                if (_credential != null)
                {
                    string password = new NetworkCredential(string.Empty, _credential.Password).Password;
                    source.Credentials = PackageSourceCredential.FromUserInput(repositoryUrl, _credential.UserName, password, true, null);
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

                SearchFilter filter = new SearchFilter(_prerelease);
                SourceCacheContext context = new SourceCacheContext();

                if ((_name == null) || (_name.Length == 0))
                {
                    returnedPkgs.AddRange(FindPackagesFromSourceHelper(repoName, repositoryUrl, null, resourceSearch, resourceMetadata, filter, context));
                }

                foreach (string n in _name)
                {
                    if (pkgsLeftToFind.Any())
                    {
                        var foundPkgs = FindPackagesFromSourceHelper(repoName, repositoryUrl, n, resourceSearch, resourceMetadata, filter, context);
                        if (foundPkgs.Any() && foundPkgs.First().Count() != 0)
                        {
                            returnedPkgs.AddRange(foundPkgs);


                            // if the repository is not specified or the repository is specified (but it's not '*'), then we can stop continuing to search for the package
                            if (_repository == null ||  !_repository[0].Equals("*"))
                            {
                                pkgsLeftToFind.Remove(n);
                            }
                        }
                    }
                }
            }

            return returnedPkgs;
        }


    }
}