
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Data;
using static System.Environment;
//using Microsoft.Extensions.DependencyModel;

namespace Microsoft.PowerShell.PowerShellGet
{
    /// <summary>
    /// Cache settings
    /// </summary>

    class CacheSettings
    {
        /// <summary>
        /// Default file name for a settings file is 'psresourcerepository.config'
        /// Also, the user level setting file at '%APPDATA%\NuGet' always uses this name
        /// </summary>
//        public static readonly string DefaultCacheFileName = "PSResourceRepository.xml";
        public static readonly string DefaultCachePath = Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "PowerShellGet", "RepositoryCache");
        //public static readonly string DefaultCachePath = @"%APPDATA%/PowerShellGet/repositorycache";    //@"%APPDTA%\NuGet";   // c:\code\temp\repositorycache
                                                                                         //        public static readonly string DefaultFullCachePath = Path.Combine(DefaultCachePath, DefaultCacheFileName);

        public static string DefaultFullCachePath = "";

        public CacheSettings() { }


        /// <summary>
        /// Find a repository cache
        /// Returns: bool 
        /// </summary>
        /// <param name="sectionName"></param>
        public bool CacheExists(string repositoryName)
        {
            // Search in the designated location for the cache
            DefaultFullCachePath = Path.Combine(DefaultCachePath, repositoryName + ".json");

            if (File.Exists(DefaultFullCachePath))
            {
                return true;
            }

            return false;
        }


        /// <summary>
        /// For any repository that is not the PSGallery, 
        /// create a cache by essentially calling find *
        /// </summary>
        /// <param name="sectionName"></param>
        public void CreateCache(string repositoryName)
        {
            // TODO
        }
    


        
        public void Read(string repositoryName)
        {
            // Call CacheExists() 
            if (!CacheExists(repositoryName))
            {
                // user should not receive this error--- just for me :)
                throw new ArgumentException("Was not able to successfully find cache");
                //return;
            }
        }




        public DataSet CreateDataTable(string repositoryName)
        {
            // Call CacheExists() 
            if (!CacheExists(repositoryName))
            {
                // user should not receive this error--- just for me :)
                throw new ArgumentException("Was not able to successfully find cache");
                //return;
            }

            // Open file 
            List<PackageMetadata> repoCache = null;
            using (StreamReader r = new StreamReader(DefaultFullCachePath))
            {
                string json = r.ReadToEnd();

                try
                {
                    repoCache = JsonConvert.DeserializeObject<List<PackageMetadata>>(json, new JsonSerializerSettings() { MaxDepth = 6 });
                }
                catch (Exception e)
                {
                    // throw error
                    Console.WriteLine("EXCEPTION: " + e);
                }
            }

            DataSet dataSet = new DataSet("CacheDataSet");
            // Create new DataTable.
            DataTable table = dataSet.Tables.Add("CacheDataTable");

                       
            string[] stringProperties = new string[] { "Key", "Name", "Version", "Type", "Description", "Author", "Copyright", "PublishedDate", "InstalledDate", "UpdatedDate", "LicenseUri", "ProjectUri", "IconUri", "PowerShellGetFormatVersion", "ReleaseNotes", "RepositorySourceLocation", "Repository", "PackageManagementProvider", "IsPrerelease" };
                        
            // Declare DataColumn and DataRow variables.
            DataRow row, tagsRow, dependenciesRow, commandsRow, dscResoureRow, roleCapabilityRow;
            DataColumn column, tagsColumn, dependenciesColumn, commandsColumn, dscResourceColumn, roleCapabilityColumn;


            /* Create Main Metadata Table */
            // Create the column for all properties
            foreach (var property in stringProperties)
            {
                // Add column to datatable
                column = new DataColumn(property, typeof(System.String));
                table.Columns.Add(column);
            }


            /* Create Tags Table */
            DataTable tagsTable = dataSet.Tables.Add("TagsTable");
            tagsColumn = new DataColumn("Key", typeof(System.String));
            tagsTable.Columns.Add(tagsColumn);
            tagsColumn = new DataColumn("Tags", typeof(System.String));
            tagsTable.Columns.Add(tagsColumn);


            /* Create Dependencies Table */
            DataTable dependenciesTable = dataSet.Tables.Add("DependenciesTable");
            dependenciesColumn = new DataColumn("Key", typeof(System.String));
            dependenciesTable.Columns.Add(dependenciesColumn);
            dependenciesColumn = new DataColumn("Dependencies", typeof(Dependency));
            dependenciesTable.Columns.Add(dependenciesColumn);


            /* Create Commands Table */
            DataTable commandsTable = dataSet.Tables.Add("CommandsTable");
            commandsColumn = new DataColumn("Key", typeof(System.String));
            commandsTable.Columns.Add(commandsColumn);
            commandsColumn = new DataColumn("Commands", typeof(System.String));
            commandsTable.Columns.Add(commandsColumn);


            /* Create DscResource Table */
            DataTable dscResourceTable = dataSet.Tables.Add("DscResourceTable");
            dscResourceColumn = new DataColumn("Key", typeof(System.String));
            dscResourceTable.Columns.Add(dscResourceColumn);
            dscResourceColumn = new DataColumn("DscResources", typeof(System.String));
            dscResourceTable.Columns.Add(dscResourceColumn);

            /* Create RoleCapability Table */
            DataTable roleCapabilityTable = dataSet.Tables.Add("RoleCapabilityTable");
            roleCapabilityColumn = new DataColumn("Key", typeof(System.String));
            roleCapabilityTable.Columns.Add(roleCapabilityColumn);
            roleCapabilityColumn = new DataColumn("RoleCapability", typeof(System.String));
            roleCapabilityTable.Columns.Add(roleCapabilityColumn);


            foreach (PackageMetadata pkg in repoCache)
            {
                
               // Console.WriteLine(pkg.Name);                
                var pkgKey = pkg.Name + pkg.Version + pkg.Repository;

                // Create new DataRow objects and add to DataTable.    
                row = table.NewRow();
                row["Key"] = pkgKey;
                row["Name"] = pkg.Name;
                row["Version"] = pkg.Version;
                row["Type"] = pkg.Type;
                row["Description"] = pkg.Description;
               // row["Author"] = pkg.Author;
                row["Copyright"] = pkg.Copyright;
                row["PublishedDate"] = pkg.PublishedDate;
                row["InstalledDate"] = pkg.InstalledDate;
                row["UpdatedDate"] = pkg.UpdatedDate;
                row["LicenseUri"] = pkg.LicenseUri;
                row["ProjectUri"] = pkg.ProjectUri;
                row["IconUri"] = pkg.IconUri;
                row["PowerShellGetFormatVersion"] = pkg.PowerShellGetFormatVersion;
                row["ReleaseNotes"] = pkg.ReleaseNotes;
                row["RepositorySourceLocation"] = pkg.RepositorySourceLocation;
                row["Repository"] = pkg.Repository;
                row["PackageManagementProvider"] = pkg.PackageManagementProvider;
                row["IsPrerelease"] = pkg.AdditionalMetadata.IsPrerelease;

                table.Rows.Add(row);

                if (pkg.Tags.Length == 0)
                {
                    tagsRow = tagsTable.NewRow();
                    tagsRow["Key"] = pkgKey;
                    tagsRow["Tags"] = "";
                    tagsTable.Rows.Add(tagsRow);
                }
                foreach (var tag in pkg.Tags)
                {
                    tagsRow = tagsTable.NewRow();
                    tagsRow["Key"] = pkgKey;
                    tagsRow["Tags"] = tag;
                    tagsTable.Rows.Add(tagsRow);
                }

                if (pkg.Dependencies.Length == 0)
                {
                    dependenciesRow = dependenciesTable.NewRow();
                    dependenciesRow["Key"] = pkgKey;

                    var dep = new Dependency();

                    dependenciesRow["Dependencies"] = dep;
                    dependenciesTable.Rows.Add(dependenciesRow);
                }
                foreach (var dependency in pkg.Dependencies)
                {
                    dependenciesRow = dependenciesTable.NewRow();
                    dependenciesRow["Key"] = pkgKey;

                    var dep = new Dependency();
                    dep.Name = dependency.Name;
                    dep.MinimumVersion = dependency.MinimumVersion;
                    dep.MaximumVersion = dependency.MaximumVersion;
                    dependenciesRow["Dependencies"] = dep;

                    dependenciesTable.Rows.Add(dependenciesRow);
                }

                if (pkg.Includes.Command.Length == 0)
                {
                    commandsRow = commandsTable.NewRow();
                    commandsRow["Key"] = pkgKey;
                    commandsRow["Commands"] = "";
                    commandsTable.Rows.Add(commandsRow);
                }
                foreach (var command in pkg.Includes.Command)
                {
                    commandsRow = commandsTable.NewRow();
                    commandsRow["Key"] = pkgKey;
                    commandsRow["Commands"] = command;
                    commandsTable.Rows.Add(commandsRow);
                }

                if (pkg.Includes.DscResource.Length == 0)
                {
                    dscResoureRow = dscResourceTable.NewRow();
                    dscResoureRow["Key"] = pkgKey;
                    dscResoureRow["DscResources"] = "";
                    dscResourceTable.Rows.Add(dscResoureRow);
                }
                foreach (var dscResource in pkg.Includes.DscResource)
                {
                    dscResoureRow = dscResourceTable.NewRow();
                    dscResoureRow["Key"] = pkgKey;
                    dscResoureRow["DscResources"] = dscResource;
                    dscResourceTable.Rows.Add(dscResoureRow);
                }

                if (pkg.Includes.RoleCapability.Length == 0)
                {
                    roleCapabilityRow = roleCapabilityTable.NewRow();
                    roleCapabilityRow["Key"] = pkgKey;
                    roleCapabilityRow["RoleCapability"] = "";
                    roleCapabilityTable.Rows.Add(roleCapabilityRow);
                }
                foreach (var roleCapbility in pkg.Includes.RoleCapability)
                {
                    roleCapabilityRow = roleCapabilityTable.NewRow();
                    roleCapabilityRow["Key"] = pkgKey;
                    roleCapabilityRow["RoleCapability"] = roleCapbility;
                    roleCapabilityTable.Rows.Add(roleCapabilityRow);
                }
            }

            return dataSet;
        }

    }
}
