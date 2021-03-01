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
using Microsoft.PowerShell.PowerShellGet.RepositorySettings;
using System.Net.Http;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static System.Environment;


namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{

    /// <summary>
    /// The Register-PSResourceRepository cmdlet registers the default repository for PowerShell modules.
    /// After a repository is registered, you can reference it from the Find-PSResource, Install-PSResource, and Publish-PSResource cmdlets.
    /// The registered repository becomes the default repository in Find-Module and Install-Module.
    /// It returns nothing.
    /// </summary>

    [Cmdlet(VerbsCommon.Find, "PSResource", DefaultParameterSetName = "NameParameterSet", SupportsShouldProcess = true,
        HelpUri = "<add>", RemotingCapability = RemotingCapability.None)]
    public sealed
    class FindPSResource : PSCmdlet
    {
        //  private string PSGalleryRepoName = "PSGallery";

        /// <summary>
        /// Specifies the desired name for the resource to be searched.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string[] Name
        {
            get
            { return _name; }

            set
            { _name = value; }
        }
        private string[] _name = new string[0];

        /// <summary>
        /// Specifies the type of the resource to be searched for.
        /// </summary>
        [Parameter(ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateSet(new string[] { "Module", "Script", "DscResource", "RoleCapability", "Command" })]
        public string[] Type
        {
            get
            { return _type; }

            set
            { _type = value; }
        }
        private string[] _type;

        /// <summary>
        /// Specifies the version or version range of the package to be searched for
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string Version
        {
            get
            { return _version; }

            set
            { _version = value; }
        }

        private string _version;

        /// <summary>
        /// Specifies to search for prerelease resources.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        public SwitchParameter Prerelease
        {
            get
            { return _prerelease; }

            set
            { _prerelease = value; }
        }

        private SwitchParameter _prerelease;

        /// <summary>
        /// Specifies a user account that has rights to find a resource from a specific repository.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string ModuleName
        {
            get
            { return _moduleName; }

            set
            { _moduleName = value; }
        }
        private string _moduleName;

        /// <summary>
        /// Specifies the type of the resource to be searched for.
        /// </summary>
        [Parameter(ValueFromPipeline = true, ParameterSetName = "NameParameterSet")]
        [ValidateNotNull]
        public string[] Tags
        {
            get
            { return _tags; }

            set
            { _tags = value; }
        }
        private string[] _tags;

        /// <summary>
        /// Specify which repositories to search in.
        /// </summary>
        [ValidateNotNullOrEmpty]
        [Parameter(ValueFromPipeline = true, ParameterSetName = "NameParameterSet")]
        public string[] Repository
        {
            get { return _repository; }

            set { _repository = value; }
        }
        private string[] _repository;

        /// <summary>
        /// Specifies a user account that has rights to find a resource from a specific repository.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        public PSCredential Credential
        {
            get
            { return _credential; }

            set
            { _credential = value; }
        }
        private PSCredential _credential;

        /// <summary>
        /// Specifies to return any dependency packages.
        /// Currently only used when name param is specified.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        public SwitchParameter IncludeDependencies
        {
            get { return _includeDependencies; }

            set { _includeDependencies = value; }
        }
        private SwitchParameter _includeDependencies;




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

        /// <summary>
        /// </summary>
        protected override void ProcessRecord()
        {
            source = new CancellationTokenSource();
            cancellationToken = source.Token;



            var r = new RespositorySettings();
            var listOfRepositories = r.Read(_repository);

            var returnedPkgsFound = new List<IEnumerable<IPackageSearchMetadata>>();
            pkgsLeftToFind = _name.ToList();
            foreach (var repoName in listOfRepositories)
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

/***
        public void ProcessCatalogReader(string repoName, string sourceUrl)
        {

            // test initalizing a cursor see: https://docs.microsoft.com/en-us/nuget/guides/api/query-for-all-published-packages
            // we want all packages published from the beginning of time
            //       DateTime cursor = DateTime.MinValue;



            // Define a lower time bound for leaves to fetch. This exclusive minimum time bound is called the cursor.
            //var cursor = DateTime.MinValue;   /// come back to this GetCursor();
            var cursor = GetCursor();     // come back to this

            // Discover the catalog index URL from the service index.
            var catalogIndexUrl = GetCatalogIndexUrlAsync(sourceUrl).GetAwaiter().GetResult();


            var httpClient = new HttpClient();

            // Download the catalog index and deserialize it.
            var indexString = httpClient.GetStringAsync(catalogIndexUrl).GetAwaiter().GetResult();
            Console.WriteLine($"Fetched catalog index {catalogIndexUrl}.");
            var index = JsonConvert.DeserializeObject<CatalogIndex>(indexString);

            // Find all pages in the catalog index that meet the time bound.

            var pageItems = index
                .Items
                .Where(x => x.CommitTimestamp > cursor);

            var allLeafItems = new List<CatalogLeafItem>();


            foreach (var pageItem in pageItems)  /// need to process one page at a time
            {
                // Download the catalog page and deserialize it.
                var pageString = httpClient.GetStringAsync(pageItem.Url).GetAwaiter().GetResult();
                Console.WriteLine($"Fetched catalog page {pageItem.Url}.");
                var page = JsonConvert.DeserializeObject<CatalogPage>(pageString);

                // Find all leaves in the catalog page that meet the time bound.
                var pageLeafItems = page
                    .Items
                    .Where(x => x.CommitTimestamp > cursor);

                allLeafItems.AddRange(pageLeafItems);


                // local
                FindLocalPackagesResourceV2 localResource = new FindLocalPackagesResourceV2(sourceUrl);

                LocalPackageSearchResource localResourceSearch = new LocalPackageSearchResource(localResource);
                LocalPackageMetadataResource localResourceMetadata = new LocalPackageMetadataResource(localResource);

                SearchFilter filter = new SearchFilter(_prerelease);
                SourceCacheContext context = new SourceCacheContext();



                // url
                PackageSource source = new PackageSource(sourceUrl);
                if (_credential != null)
                {
                    string password = new NetworkCredential(string.Empty, _credential.Password).Password;
                    source.Credentials = PackageSourceCredential.FromUserInput(sourceUrl, _credential.UserName, password, true, null);
                }
                var provider = FactoryExtensionsV3.GetCoreV3(NuGet.Protocol.Core.Types.Repository.Provider);

                SourceRepository repository = new SourceRepository(source, provider);
                PackageSearchResource resourceSearch = repository.GetResourceAsync<PackageSearchResource>().GetAwaiter().GetResult();
                PackageMetadataResource resourceMetadata = repository.GetResourceAsync<PackageMetadataResource>().GetAwaiter().GetResult();




                // Process all of the catalog leaf items.
                Console.WriteLine($"Processing {allLeafItems.Count} catalog leaves.");
                foreach (var leafItem in allLeafItems)
                {
                    _version = leafItem.PackageVersion;

                    IEnumerable<IPackageSearchMetadata> foundPkgs;
                    if (sourceUrl.StartsWith("file://"))
                    {
                        foundPkgs = FindPackagesFromSourceHelper(sourceUrl, leafItem.PackageId, localResourceSearch, localResourceMetadata, filter, context).FirstOrDefault();
                    }
                    else
                    {
                        foundPkgs = FindPackagesFromSourceHelper(sourceUrl, leafItem.PackageId, resourceSearch, resourceMetadata, filter, context).FirstOrDefault();
                    }

                    // var foundPkgsFlattened = foundPkgs.Flatten();
                    foreach(var pkg in foundPkgs)
                    {
                        // First or default to convert the ienumeraable object into just an IPackageSearchmetadata object
                       // var pkgToOutput = foundPkgs.FirstOrDefault();//foundPkgs.Cast<IPackageSearchMetadata>();
                        PSObject pkgAsPSObject = new PSObject();
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Name", pkg.Identity.Id));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Version", pkg.Identity.Version));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Repository", repoName));
                        pkgAsPSObject.Members.Add(new PSNoteProperty("Description", pkg.Description));

                        WriteObject(pkgAsPSObject);
                    }

                }

            }

        }
*/
        //
        public List<IEnumerable<IPackageSearchMetadata>> FindPackagesFromSource(string repoName, string repositoryUrl, CancellationToken cancellationToken)
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


        private IEnumerable<DataRow> FindPackageFromCache(string repositoryName)
        {
            DataSet cachetables = new CacheSettings().CreateDataTable(repositoryName);

            return FindPackageFromCacheHelper(cachetables, repositoryName);
        }


        private IEnumerable<DataRow> FindPackageFromCacheHelper(DataSet cachetables, string repositoryName)
        {
            DataTable metadataTable = cachetables.Tables[0];
            DataTable tagsTable = cachetables.Tables[1];
            DataTable dependenciesTable = cachetables.Tables[2];
            DataTable commandsTable = cachetables.Tables[3];
            DataTable dscResourceTable = cachetables.Tables[4];
            DataTable roleCapabilityTable = cachetables.Tables[5];

            DataTable queryTable = new DataTable();
            var predicate = PredicateBuilder.New<DataRow>(true);


            if ((_tags != null) && (_tags.Length != 0))
            {

                var tagResults = from t0 in metadataTable.AsEnumerable()
                                 join t1 in tagsTable.AsEnumerable()
                                 on t0.Field<string>("Key") equals t1.Field<string>("Key")
                                 select new PackageMetadataAllTables
                                 {
                                     Key = t0["Key"],
                                     Name = t0["Name"],
                                     Version = t0["Version"],
                                     Type = (string)t0["Type"],
                                     Description = t0["Description"],
                                     Author = t0["Author"],
                                     Copyright = t0["Copyright"],
                                     PublishedDate = t0["PublishedDate"],
                                     InstalledDate = t0["InstalledDate"],
                                     UpdatedDate = t0["UpdatedDate"],
                                     LicenseUri = t0["LicenseUri"],
                                     ProjectUri = t0["ProjectUri"],
                                     IconUri = t0["IconUri"],
                                     PowerShellGetFormatVersion = t0["PowerShellGetFormatVersion"],
                                     ReleaseNotes = t0["ReleaseNotes"],
                                     RepositorySourceLocation = t0["RepositorySourceLocation"],
                                     Repository = t0["Repository"],
                                     IsPrerelease = t0["IsPrerelease"],
                                     Tags = t1["Tags"],
                                 };

                DataTable joinedTagsTable = tagResults.ToDataTable();

                var tagsPredicate = PredicateBuilder.New<DataRow>(true);

                // Build predicate by combining tag searches with 'or'
                foreach (string t in _tags)
                {
                    tagsPredicate = tagsPredicate.Or(pkg => pkg.Field<string>("Tags").Equals(t));
                }


                // final results -- All the appropriate pkgs with these tags
                var tagsRowCollection = joinedTagsTable.AsEnumerable().Where(tagsPredicate).Select(p => p);

                // Add row collection to final table to be queried
                queryTable.Merge(tagsRowCollection.ToDataTable());
            }

            if (_type != null)
            {
                if (_type.Contains("Command", StringComparer.OrdinalIgnoreCase))
                {

                    var commandResults = from t0 in metadataTable.AsEnumerable()
                                         join t1 in commandsTable.AsEnumerable()
                                         on t0.Field<string>("Key") equals t1.Field<string>("Key")
                                         select new PackageMetadataAllTables
                                         {
                                             Key = t0["Key"],
                                             Name = t0["Name"],
                                             Version = t0["Version"],
                                             Type = (string)t0["Type"],
                                             Description = t0["Description"],
                                             Author = t0["Author"],
                                             Copyright = t0["Copyright"],
                                             PublishedDate = t0["PublishedDate"],
                                             InstalledDate = t0["InstalledDate"],
                                             UpdatedDate = t0["UpdatedDate"],
                                             LicenseUri = t0["LicenseUri"],
                                             ProjectUri = t0["ProjectUri"],
                                             IconUri = t0["IconUri"],
                                             PowerShellGetFormatVersion = t0["PowerShellGetFormatVersion"],
                                             ReleaseNotes = t0["ReleaseNotes"],
                                             RepositorySourceLocation = t0["RepositorySourceLocation"],
                                             Repository = t0["Repository"],
                                             IsPrerelease = t0["IsPrerelease"],
                                             Commands = t1["Commands"],
                                         };

                    DataTable joinedCommandTable = commandResults.ToDataTable();

                    var commandPredicate = PredicateBuilder.New<DataRow>(true);

                    // Build predicate by combining names of commands searches with 'or'
                    // if no name is specified, we'll return all (?)
                    foreach (string n in _name)
                    {
                        commandPredicate = commandPredicate.Or(pkg => pkg.Field<string>("Commands").Equals(n));
                    }

                    // final results -- All the appropriate pkgs with these tags
                    var commandsRowCollection = joinedCommandTable.AsEnumerable().Where(commandPredicate).Select(p => p);

                    // Add row collection to final table to be queried
                    queryTable.Merge(commandsRowCollection.ToDataTable());
                }


                if (_type.Contains("DscResource", StringComparer.OrdinalIgnoreCase))
                {

                    var dscResourceResults = from t0 in metadataTable.AsEnumerable()
                                             join t1 in dscResourceTable.AsEnumerable()
                                             on t0.Field<string>("Key") equals t1.Field<string>("Key")
                                             select new PackageMetadataAllTables
                                             {
                                                 Key = t0["Key"],
                                                 Name = t0["Name"],
                                                 Version = t0["Version"],
                                                 Type = (string)t0["Type"],
                                                 Description = t0["Description"],
                                                 Author = t0["Author"],
                                                 Copyright = t0["Copyright"],
                                                 PublishedDate = t0["PublishedDate"],
                                                 InstalledDate = t0["InstalledDate"],
                                                 UpdatedDate = t0["UpdatedDate"],
                                                 LicenseUri = t0["LicenseUri"],
                                                 ProjectUri = t0["ProjectUri"],
                                                 IconUri = t0["IconUri"],
                                                 PowerShellGetFormatVersion = t0["PowerShellGetFormatVersion"],
                                                 ReleaseNotes = t0["ReleaseNotes"],
                                                 RepositorySourceLocation = t0["RepositorySourceLocation"],
                                                 Repository = t0["Repository"],
                                                 IsPrerelease = t0["IsPrerelease"],
                                                 DscResources = t1["DscResources"],
                                             };

                    var dscResourcePredicate = PredicateBuilder.New<DataRow>(true);

                    DataTable joinedDscResourceTable = dscResourceResults.ToDataTable();

                    // Build predicate by combining names of commands searches with 'or'
                    // if no name is specified, we'll return all (?)
                    foreach (string n in _name)
                    {
                        dscResourcePredicate = dscResourcePredicate.Or(pkg => pkg.Field<string>("DscResources").Equals(n));
                    }

                    // final results -- All the appropriate pkgs with these tags
                    var dscResourcesRowCollection = joinedDscResourceTable.AsEnumerable().Where(dscResourcePredicate).Select(p => p);

                    // Add row collection to final table to be queried
                    queryTable.Merge(dscResourcesRowCollection.ToDataTable());
                }

                if (_type.Contains("RoleCapability", StringComparer.OrdinalIgnoreCase))
                {

                    var roleCapabilityResults = from t0 in metadataTable.AsEnumerable()
                                                join t1 in roleCapabilityTable.AsEnumerable()
                                                on t0.Field<string>("Key") equals t1.Field<string>("Key")
                                                select new PackageMetadataAllTables
                                                {
                                                    Key = t0["Key"],
                                                    Name = t0["Name"],
                                                    Version = t0["Version"],
                                                    Type = (string)t0["Type"],
                                                    Description = t0["Description"],
                                                    Author = t0["Author"],
                                                    Copyright = t0["Copyright"],
                                                    PublishedDate = t0["PublishedDate"],
                                                    InstalledDate = t0["InstalledDate"],
                                                    UpdatedDate = t0["UpdatedDate"],
                                                    LicenseUri = t0["LicenseUri"],
                                                    ProjectUri = t0["ProjectUri"],
                                                    IconUri = t0["IconUri"],
                                                    PowerShellGetFormatVersion = t0["PowerShellGetFormatVersion"],
                                                    ReleaseNotes = t0["ReleaseNotes"],
                                                    RepositorySourceLocation = t0["RepositorySourceLocation"],
                                                    Repository = t0["Repository"],
                                                    IsPrerelease = t0["IsPrerelease"],
                                                    RoleCapability = t1["RoleCapability"],
                                                };

                    var roleCapabilityPredicate = PredicateBuilder.New<DataRow>(true);

                    DataTable joinedRoleCapabilityTable = roleCapabilityResults.ToDataTable();

                    // Build predicate by combining names of commands searches with 'or'
                    // if no name is specified, we'll return all (?)
                    foreach (string n in _name)
                    {
                        roleCapabilityPredicate = roleCapabilityPredicate.Or(pkg => pkg.Field<string>("RoleCapability").Equals(n));
                    }

                    // final results -- All the appropriate pkgs with these tags
                    var roleCapabilitiesRowCollection = joinedRoleCapabilityTable.AsEnumerable().Where(roleCapabilityPredicate).Select(p => p);

                    // Add row collection to final table to be queried
                    queryTable.Merge(roleCapabilitiesRowCollection.ToDataTable());
                }
            }



            // We'll build the rest of the predicate-- ie the portions of the predicate that do not rely on datatables
            predicate = predicate.Or(BuildPredicate(repositoryName));


            // we want to uniquely add datarows into the table
            // if queryTable is empty, we'll just query upon the metadata table
            if (queryTable == null || queryTable.Rows.Count == 0)
            {
                queryTable = metadataTable;
            }

            // final results -- All the appropriate pkgs with these tags
            var queryTableRowCollection = queryTable.AsEnumerable().Where(predicate).Select(p => p);

            // ensure distinct by key
            var finalQueryResults = queryTableRowCollection.AsEnumerable().DistinctBy(pkg => pkg.Field<string>("Key"));

            // Add row collection to final table to be queried
            // queryTable.Merge(distinctQueryTableRowCollection.ToDataTable());



            /// ignore-- testing.
            //if ((queryTable == null) || (queryTable.Rows.Count == 0) && (((_type == null) || (_type.Contains("Module", StringComparer.OrdinalIgnoreCase) || _type.Contains("Script", StringComparer.OrdinalIgnoreCase))) && ((_tags == null) || (_tags.Length == 0))))
            //{
            //    queryTable = metadataTable;
            //}


            List<IEnumerable<DataRow>> allFoundPkgs = new List<IEnumerable<DataRow>>();
            allFoundPkgs.Add(finalQueryResults);

            // Need to handle includeDependencies
            if (_includeDependencies)
            {
                foreach (var pkg in finalQueryResults)
                {
                    //allFoundPkgs.Add(DependencyFinder(finalQueryResults, dependenciesTable));
                }
            }

            IEnumerable<DataRow> flattenedFoundPkgs = (IEnumerable<DataRow>)allFoundPkgs.Flatten();

            // (IEnumerable<DataRow>)
            return flattenedFoundPkgs;
        }




        /*********************** come back to this
        private IEnumerable<DataRow> DependencyFinder(IEnumerable<DataRow> finalQueryResults, DataTable dependenciesTable )
        {
            List<IEnumerable<PackageMetadataAllTables>> foundDependencies = new List<IEnumerable<IPackageSearchMetadata>>();

            var queryResultsDT = finalQueryResults.ToDataTable();

            //var dependencyResults = from t0 in metadataTable.AsEnumerable()
            var dependencyResults = from t0 in queryResultsDT.AsEnumerable()
                                    join t1 in dependenciesTable.AsEnumerable()
                                    on t0.Field<string>("Key") equals t1.Field<string>("Key")
                                    select new PackageMetadataAllTables
                                    {
                                        Key = t0["Key"],
                                        Name = t0["Name"],
                                        Version = t0["Version"],
                                        Type = (string)t0["Type"],
                                        Description = t0["Description"],
                                        Author = t0["Author"],
                                        Copyright = t0["Copyright"],
                                        PublishedDate = t0["PublishedDate"],
                                        InstalledDate = t0["InstalledDate"],
                                        UpdatedDate = t0["UpdatedDate"],
                                        LicenseUri = t0["LicenseUri"],
                                        ProjectUri = t0["ProjectUri"],
                                        IconUri = t0["IconUri"],
                                        PowerShellGetFormatVersion = t0["PowerShellGetFormatVersion"],
                                        ReleaseNotes = t0["ReleaseNotes"],
                                        RepositorySourceLocation = t0["RepositorySourceLocation"],
                                        Repository = t0["Repository"],
                                        IsPrerelease = t0["IsPrerelease"],
                                        Dependencies = (Dependency)t1["Dependencies"],
                                    };

            var dependencyPredicate = PredicateBuilder.New<DataRow>(true);

            DataTable joinedDependencyTable = dependencyResults.ToDataTable();

            // Build predicate by combining names of dependencies searches with 'or'

            // public string Name { get; set; }
            // public string MinimumVersion { get; set; }
            // public string MaximumVersion { get; set; }



            // for each pkg that was found, check to see if it has a dep
            foreach (var pkg in dependencyResults)
            {
                // because it's an ienumerable, we need to pull out the pkg itself to access its properties, but there is only a 'default' option
                if (!string.IsNullOrEmpty(pkg.Dependencies.Name))
                {
                    // you could just add the data row
                    foundDependencies.Add((IEnumerable<PackageMetadataAllTables>) pkg);
                }

                // and then check to make sure dep name/version exists within the cache (via the metadata table)
                // call helper recursively
                // (IEnumerable<DataRow> finalQueryResults, DataTable dependenciesTable )
                DependencyFinder(pkg, dependencyResults, dependenciesTable)

                var roleCapabilityPredicate = PredicateBuilder.New<DataRow>(true);

                DataTable joinedRoleCapabilityTable = roleCapabilityResults.ToDataTable();

                // Build predicate by combining names of commands searches with 'or'
                // if no name is specified, we'll return all (?)
                foreach (string n in _name)
                {
                    roleCapabilityPredicate = roleCapabilityPredicate.Or(pkg => pkg.Field<string>("RoleCapability").Equals(n));
                }

                // final results -- All the appropriate pkgs with these tags
                var roleCapabilitiesRowCollection = joinedRoleCapabilityTable.AsEnumerable().Where(roleCapabilityPredicate).Select(p => p);

                // Add row collection to final table to be queried
                queryTable.Merge(roleCapabilitiesRowCollection.ToDataTable());
            }

            // final results -- All the
            var dependencies = joinedDependencyTable.AsEnumerable().Where(dependencyPredicate).Select(p => p);

            return
        }
        ***************/


        private ExpressionStarter<DataRow> BuildPredicate(string repository)
        {
            //NuGetVersion nugetVersion0;
            var predicate = PredicateBuilder.New<DataRow>(true);

            if (_type != null)
            {
                var typePredicate = PredicateBuilder.New<DataRow>(true);

                if (_type.Contains("Script", StringComparer.OrdinalIgnoreCase))
                {
                    typePredicate = typePredicate.Or(pkg => pkg.Field<string>("Type").Equals("Script"));
                }
                if (_type.Contains("Module", StringComparer.OrdinalIgnoreCase))
                {
                    typePredicate = typePredicate.Or(pkg => pkg.Field<string>("Type").Equals("Module"));
                }
                predicate.And(typePredicate);

            }

            ExpressionStarter<DataRow> starter2 = PredicateBuilder.New<DataRow>(true);
            if (_moduleName != null)
            {
                predicate = predicate.And(pkg => pkg.Field<string>("Name").Equals(_moduleName));
            }

            if ((_type == null) || ((_type.Length == 0) || !(_type.Contains("Module", StringComparer.OrdinalIgnoreCase) || _type.Contains("Script", StringComparer.OrdinalIgnoreCase))))
            {
                var typeNamePredicate = PredicateBuilder.New<DataRow>(true);
                foreach (string name in _name)
                {

                    //// ?
                    typeNamePredicate = typeNamePredicate.Or(pkg => pkg.Field<string>("Type").Equals("Script"));
                }
            }


            // cache will only contain the latest stable and latest prerelease of each package
            if (_version != null)
            {

                NuGetVersion nugetVersion;

                //VersionRange versionRange = VersionRange.Parse(version);

                if (NuGetVersion.TryParse(_version, out nugetVersion))
                {
                    predicate = predicate.And(pkg => pkg.Field<string>("Version").Equals(nugetVersion));
                }




            }
            if (!_prerelease)
            {
                predicate = predicate.And(pkg => pkg.Field<string>("IsPrerelease").Equals("false"));  // consider checking if it IS prerelease
            }
            return predicate;
        }







        private List<IEnumerable<IPackageSearchMetadata>> FindPackagesFromSourceHelper(string repoName, string repositoryUrl, string name, PackageSearchResource pkgSearchResource, PackageMetadataResource pkgMetadataResource, SearchFilter searchFilter, SourceCacheContext srcContext)
        {

            List<IEnumerable<IPackageSearchMetadata>> foundPackages = new List<IEnumerable<IPackageSearchMetadata>>();
            List<IEnumerable<IPackageSearchMetadata>> filteredFoundPkgs = new List<IEnumerable<IPackageSearchMetadata>>();
            List<IEnumerable<IPackageSearchMetadata>> scriptPkgsNotNeeded = new List<IEnumerable<IPackageSearchMetadata>>();

            char[] delimiter = new char[] { ' ', ',' };

            // If module name is specified, use that as the name for the pkg to search for
            if (_moduleName != null)
            {
                // may need to take 1
                foundPackages.Add(pkgMetadataResource.GetMetadataAsync(_moduleName, _prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult());
                //foundPackages = pkgMetadataResource.GetMetadataAsync(_moduleName, _prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();
            }
            else if (name != null)
            {
                // If a resource name is specified, search for that particular pkg name
                if (!name.Contains("*"))
                {
                    IEnumerable<IPackageSearchMetadata> retrievedPkgs = null;
                    try
                    {
                        retrievedPkgs = pkgMetadataResource.GetMetadataAsync(name, _prerelease, false, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();
                    }
                    catch { }
                    if (retrievedPkgs == null || retrievedPkgs.Count() == 0)
                    {
                        this.WriteVerbose(string.Format("'{0}' could not be found in repository '{1}'", name, repoName));
                        return foundPackages;
                    }
                    else
                    {
                        foundPackages.Add(retrievedPkgs);
                    }
                }
                // search for range of pkg names
                else
                {
                    // TODO:  follow up on this
                    //foundPackages.Add(pkgSearchResource.SearchAsync(name, searchFilter, 0, 6000, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult());

                    name = name.Equals("*") ? "" : name;   // can't use * in v3 protocol
                    var wildcardPkgs = pkgSearchResource.SearchAsync(name, searchFilter, 0, 6000, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();

                    // If not searching for *all* packages
                    if (!name.Equals("") && !name[0].Equals('*'))
                    {
                        char[] wildcardDelimiter = new char[] { '*' };
                        var tokenizedName = name.Split(wildcardDelimiter, StringSplitOptions.RemoveEmptyEntries);

                        //var startsWithWildcard = name[0].Equals('*') ? true : false;
                        //var endsWithWildcard = name[name.Length-1].Equals('*') ? true : false;

                        // 1)  *owershellge*
                        if (name.StartsWith("*") && name.EndsWith("*"))
                        {
                            // filter results
                            foundPackages.Add(wildcardPkgs.Where(p => p.Identity.Id.Contains(tokenizedName[0])));

                            if (foundPackages.Flatten().Any())
                            {
                                pkgsLeftToFind.Remove(name);
                            }
                            // .Where(p => versionRange.Satisfies(p.Identity.Version))
                            // .OrderByDescending(p => p.Identity.Version, VersionComparer.VersionRelease));

                        }
                        // 2)  *erShellGet
                        else if (name.StartsWith("*"))
                        {
                            // filter results
                            foundPackages.Add(wildcardPkgs.Where(p => p.Identity.Id.EndsWith(tokenizedName[0])));

                            if (foundPackages.Flatten().Any())
                            {
                                pkgsLeftToFind.Remove(name);
                            }
                        }
                        // if 1)  PowerShellG*
                        else if (name.EndsWith("*"))
                        {
                            // filter results
                            foundPackages.Add(wildcardPkgs.Where(p => p.Identity.Id.StartsWith(tokenizedName[0])));

                            if (foundPackages.Flatten().Any())
                            {
                                pkgsLeftToFind.Remove(name);
                            }
                        }
                        // 3)  Power*Get
                        else if (tokenizedName.Length == 2)
                        {
                            // filter results
                            foundPackages.Add(wildcardPkgs.Where(p => p.Identity.Id.StartsWith(tokenizedName[0]) && p.Identity.Id.EndsWith(tokenizedName[1])));

                            if (foundPackages.Flatten().Any())
                            {
                                pkgsLeftToFind.Remove("*");
                            }
                        }
                    }
                    else
                    {
                        foundPackages.Add(wildcardPkgs);
                        pkgsLeftToFind.Remove("*");
                    }


                }
            }
            else
            {
                /* can probably get rid of this */
                foundPackages.Add(pkgSearchResource.SearchAsync("", searchFilter, 0, 6000, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult());
                //foundPackages = pkgSearchResource.SearchAsync("", searchFilter, 0, 6000, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();
            }


            //use either ModuleName or Name (whichever not null) to prevent id error
            var nameVal = name == null ? _moduleName : name;
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

            // return foundPackages;
            return filteredFoundPkgs;
        }




        private List<IEnumerable<IPackageSearchMetadata>> FindDependenciesFromSource(IEnumerable<IPackageSearchMetadata> pkg, PackageMetadataResource pkgMetadataResource, SourceCacheContext srcContext)
        {
            /// dependency resolver
            ///
            /// this function will be recursively called
            ///
            /// call the findpackages from source helper (potentially generalize this so it's finding packages from source or cache)
            ///
            List<IEnumerable<IPackageSearchMetadata>> foundDependencies = new List<IEnumerable<IPackageSearchMetadata>>();

            // 1)  check the dependencies of this pkg
            // 2) for each dependency group, search for the appropriate name and version
            // a dependency group are all the dependencies for a particular framework
            // first or default because we need this pkg to be an ienumerable (so we don't need to be doing strange object conversions)
            foreach (var dependencyGroup in pkg.FirstOrDefault().DependencySets)
            {

                //dependencyGroup.TargetFramework
                //dependencyGroup.

                foreach (var pkgDependency in dependencyGroup.Packages)
                {

                    // 2.1) check that the appropriate pkg dependencies exist
                    // returns all versions from a single package id.
                    var dependencies = pkgMetadataResource.GetMetadataAsync(pkgDependency.Id, _prerelease, true, srcContext, NullLogger.Instance, cancellationToken).GetAwaiter().GetResult();

                    // then 2.2) check if the appropriate verion range exists  (if version exists, then add it to the list to return)

                    VersionRange versionRange = null;

                    // to ensure a null OriginalString isn't being parsed which will result in an error being thrown and caught
                    // if OriginalString is null, versionRange will remain null and default version will be installed.
                    if(pkgDependency.VersionRange.OriginalString != null){
                        try
                        {
                            versionRange = VersionRange.Parse(pkgDependency.VersionRange.OriginalString);
                        }
                        catch
                        {
                            Console.WriteLine("Error parsing version range");
                        }
                    }



                    // choose the most recent version
                    int toRemove = dependencies.Count() - 1;

                    // if no version/version range is specified the we just return the latest version
                    var depPkgToReturn = (versionRange == null ?
                        dependencies.FirstOrDefault() :
                        dependencies.Where(v => versionRange.Satisfies(v.Identity.Version)).FirstOrDefault());



                    // using the repeat function to convert the IPackageSearchMetadata object into an enumerable.
                    foundDependencies.Add(Enumerable.Repeat(depPkgToReturn, 1));

                    // 3) search for any dependencies the pkg has
                    foundDependencies.AddRange(FindDependenciesFromSource(Enumerable.Repeat(depPkgToReturn, 1), pkgMetadataResource, srcContext));
                }
            }

            // flatten after returning
            return foundDependencies;
        }










        private List<IPackageSearchMetadata> FilterPkgsByResourceType(List<IEnumerable<IPackageSearchMetadata>> filteredFoundPkgs)
        {


            char[] delimiter = new char[] { ' ', ',' };

            // If there are any packages that were filtered by tags, we'll continue to filter on those packages, otherwise, we'll filter on all the packages returned from the search
            var flattenedPkgs = filteredFoundPkgs.Flatten();

            var pkgsFilteredByResource = new List<IPackageSearchMetadata>();

            // if the type is null, we'll set it to check for everything except modules and scripts, since those types were already checked.
            _type = _type == null ? new string[] { "DscResource", "RoleCapability", "Command" } : _type;
            string[] pkgsToFind = new string[5];
            pkgsLeftToFind.CopyTo(pkgsToFind);

            foreach (IPackageSearchMetadata pkg in flattenedPkgs)
            {
                // Enumerable.ElementAt(0)
                var tagArray = pkg.Tags.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);


                // check modules and scripts here ??

                foreach (var tag in tagArray)
                {


                    // iterate through type array
                    foreach (var resourceType in _type)
                    {

                        switch (resourceType)
                        {
                            case "Module":
                                if (tag.Equals("PSModule"))
                                {
                                    pkgsFilteredByResource.Add(pkg);
                                }
                                break;

                            case "Script":
                                if (tag.Equals("PSScript"))
                                {
                                    pkgsFilteredByResource.Add(pkg);
                                }
                                break;

                            case "Command":
                                if (tag.StartsWith("PSCommand_"))
                                {
                                    foreach (var resourceName in pkgsToFind)
                                    {
                                        if (tag.Equals("PSCommand_" + resourceName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            pkgsFilteredByResource.Add(pkg);
                                            pkgsLeftToFind.Remove(resourceName);

                                        }
                                    }
                                }
                                break;

                            case "DscResource":
                                if (tag.StartsWith("PSDscResource_"))
                                {
                                    foreach (var resourceName in pkgsToFind)
                                    {
                                        if (tag.Equals("PSDscResource_" + resourceName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            pkgsFilteredByResource.Add(pkg);
                                            pkgsLeftToFind.Remove(resourceName);
                                        }
                                    }
                                }
                                break;

                            case "RoleCapability":
                                if (tag.StartsWith("PSRoleCapability_"))
                                {
                                    foreach (var resourceName in pkgsToFind)
                                    {
                                        if (tag.Equals("PSRoleCapability_" + resourceName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            pkgsFilteredByResource.Add(pkg);
                                            pkgsLeftToFind.Remove(resourceName);
                                        }
                                    }
                                }
                                break;
                        }

                    }
                }
            }



            return pkgsFilteredByResource.DistinctBy(p => p.Identity.Id).ToList();

        }













/***
        private static async Task<Uri> GetCatalogIndexUrlAsync(string sourceUrl)
        {
            // This code uses the NuGet client SDK, which are the libraries used internally by the official
            // NuGet client.
            var sourceRepository = NuGet.Protocol.Core.Types.Repository.Factory.GetCoreV3(sourceUrl);
            var serviceIndex = await sourceRepository.GetResourceAsync<ServiceIndexResourceV3>();
            var catalogIndexUrl = serviceIndex.GetServiceEntryUri("Catalog/3.0.0");



            return catalogIndexUrl;
        }
*/


        ///  Modified this
        private static DateTime GetCursor()
        {
            /*
            try
            {
                var cursorString = File.ReadAllText(CursorFileName);
                var cursor = JsonConvert.DeserializeObject<FileCursor>(cursorString);
                var cursorValue = cursor.GetAsync().GetAwaiter().GetResult().GetValueOrDefault();
                DateTime cursorValueDateTime = DateTime.MinValue;
                if (cursorValue != null)
                {
                    cursorValueDateTime = cursorValue.DateTime;
                }
                Console.WriteLine($"Read cursor value: {cursorValueDateTime}.");
                return cursorValueDateTime;
            }
            catch (FileNotFoundException)
            {
            */
                var value = DateTime.MinValue;
                Console.WriteLine($"No cursor found. Defaulting to {value}.");
                return value;
           // }
        }

/***
        private static void ProcessCatalogLeaf(CatalogLeafItem leaf)
        {
            // Here, you can do whatever you want with each catalog item. If you want the full metadata about
            // the catalog leaf, you can use the leafItem.Url property to fetch the full leaf document. In this case,
            // we'll just keep it simple and output the details about the leaf that are included in the catalog page.
            // example: Console.WriteLine($"{leaf.CommitTimeStamp}: {leaf.Id} {leaf.Version} (type is {leaf.Type})");

            Console.WriteLine($"{leaf.CommitTimestamp}: {leaf.PackageId} {leaf.PackageVersion} (type is {leaf.Type})");
        }

        private static void SetCursor(DateTime value)
        {
            Console.WriteLine($"Writing cursor value: {value}.");
            var cursorString = JsonConvert.SerializeObject(new Cursor { Value = value });
            File.WriteAllText(CursorFileName, cursorString);
        }
*/




    }


    public class Cursor
    {
        [JsonProperty("value")]
        public DateTime Value { get; set; }
    }
}
