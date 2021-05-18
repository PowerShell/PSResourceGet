using System.Data.Common;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation;
using NuGet.CatalogReader;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    #region Enums

    // flag attribute?
    public enum ResourceType
    {
        Module,
        Script,
        DscResource,
        Command
    }

    public enum VersionType
    {
        Unknown,
        MinimumVersion,
        RequiredVersion,
        MaximumVersion
    }

    #endregion

    #region VersionInfo

    public sealed class VersionInfo
    {
        public VersionInfo(
            VersionType versionType,
            Version versionNum)
        {
            VersionType = versionType;
            VersionNum = versionNum;
        }

        public VersionType VersionType { get; }
        public Version VersionNum { get; }

        public override string ToString() => $"{VersionType}: {VersionNum}";
    }

    #endregion

    #region ResourceIncludes

    public sealed class ResourceIncludes
    {
        #region Properties

        public string[] Cmdlet { get; }

        public string[] Command { get; }

        public string[] DscResource { get; }

        public string[] Function { get; }

        public string[] RoleCapability { get; }

        public string[] Workflow { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor
        ///
        /// Provided hashtable has form:
        ///     Key: Cmdlet
        ///     Value: ArrayList of Cmdlet name strings
        ///     Key: Command
        ///     Value: ArrayList of Command name strings
        ///     Key: DscResource
        ///     Value: ArrayList of DscResource name strings
        ///     Key: Function
        ///     Value: ArrayList of Function name strings
        ///     Key: RoleCapability (deprecated for PSGetV3)
        ///     Value: ArrayList of RoleCapability name strings
        ///     Key: Workflow (deprecated for PSGetV3)
        ///     Value: ArrayList of Workflow name strings
        /// </summary>
        /// <param name="includes">Hashtable of PSGet includes</param>
        public ResourceIncludes(Hashtable includes)
        {
            if (includes == null) { return; }

            Cmdlet = GetHashTableItem(includes, nameof(Cmdlet));
            Command = GetHashTableItem(includes, nameof(Command));
            DscResource = GetHashTableItem(includes, nameof(DscResource));
            Function = GetHashTableItem(includes, nameof(Function));
            RoleCapability = GetHashTableItem(includes, nameof(RoleCapability));
            Workflow = GetHashTableItem(includes, nameof(Workflow));
        }

        #endregion

        #region Public methods

        public Hashtable ConvertToHashtable()
        {
            var hashtable = new Hashtable
            {
                { nameof(Cmdlet), Cmdlet },
                { nameof(Command), Command },
                { nameof(DscResource), DscResource },
                { nameof(Function), Function },
                { nameof(RoleCapability), RoleCapability },
                { nameof(Workflow), Workflow }
            };

            return hashtable;
        }

        #endregion

        #region Private methods

        private string[] GetHashTableItem(
            Hashtable table,
            string name)
        {
            if (table.ContainsKey(name) &&
                table[name] is PSObject psObjectItem)
            {
                return Utils.GetStringArray(psObjectItem.BaseObject as ArrayList);
            }

            return null;
        }

        #endregion
    }

    #endregion

    #region PSResourceInfo

    public sealed class PSResourceInfo
    {
        #region Properties

        public Dictionary<string, string> AdditionalMetadata { get; set; }
        public string Author { get; set; }
        public string CompanyName { get; set; }
        public string Copyright { get; set; }
        public string[] Dependencies { get; set; }
        public string Description { get; set; }
        public Uri IconUri { get; set; }
        public ResourceIncludes Includes { get; set; }
        public DateTime? InstalledDate { get; set; }
        public string InstalledLocation { get; set; }
        public Uri LicenseUri { get; set; }
        public string Name { get; set; }
        public string PackageManagementProvider { get; set; }
        public string PowerShellGetFormatVersion { get; set; }
        public Uri ProjectUri { get; set; }
        public DateTime? PublishedDate { get; set; }
        public string ReleaseNotes { get; set; }
        public string Repository { get; set; }
        public string RepositorySourceLocation { get; set; }
        public string[] Tags { get; set; }
        public string Type { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public Version Version { get; set; }

        #endregion

        #region Public static methods

        /// <summary>
        /// Writes the PSGetResourceInfo properties to the specified file path as a
        /// PowerShell serialized xml file, maintaining compatibility with
        /// PowerShellGet v2 file format.
        /// </summary>
        public bool TryWrite(
            string filePath,
            out string errorMsg)
        {
            errorMsg = string.Empty;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                errorMsg = "TryWritePSGetInfo: Invalid file path. Filepath cannot be empty or whitespace.";
                return false;
            }

            try
            {
                var infoXml = PSSerializer.Serialize(
                    source: ConvertToCustomObject(),
                    depth: 5);

                System.IO.File.WriteAllText(
                    path: filePath,
                    contents: infoXml);

                return true;
            }
            catch(Exception ex)
            {
                errorMsg = string.Format(
                    CultureInfo.InvariantCulture,
                    @"TryWritePSGetInfo: Cannot convert and write the PowerShellGet information to file, with error: {0}",
                    ex.Message);

                return false;
            }
        }

        /// <summary>
        /// Reads a PSGet resource xml (PowerShell serialized) file and returns
        /// a PSGetResourceInfo object containing the file contents.
        /// </summary>
        public static bool TryRead(
            string filePath,
            out PSResourceInfo psGetInfo,
            out string errorMsg)
        {
            psGetInfo = null;
            errorMsg = string.Empty;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                errorMsg = "TryReadPSGetInfo: Invalid file path. Filepath cannot be empty or whitespace.";
                return false;
            }

            try
            {
                // Read and deserialize information xml file.
                var psObjectInfo = (PSObject) PSSerializer.Deserialize(
                    System.IO.File.ReadAllText(
                        filePath));

                psGetInfo = new PSResourceInfo
                {
                    AdditionalMetadata = GetProperty<Dictionary<string,string>>(nameof(PSResourceInfo.AdditionalMetadata), psObjectInfo),
                    Author = GetProperty<string>(nameof(PSResourceInfo.Author), psObjectInfo),
                    CompanyName = GetProperty<string>(nameof(PSResourceInfo.CompanyName), psObjectInfo),
                    Copyright = GetProperty<string>(nameof(PSResourceInfo.Copyright), psObjectInfo),
                    Dependencies = Utils.GetStringArray(GetProperty<ArrayList>(nameof(PSResourceInfo.Dependencies), psObjectInfo)),
                    Description = GetProperty<string>(nameof(PSResourceInfo.Description), psObjectInfo),
                    IconUri = GetProperty<Uri>(nameof(PSResourceInfo.IconUri), psObjectInfo),
                    Includes = new ResourceIncludes(GetProperty<Hashtable>(nameof(PSResourceInfo.Includes), psObjectInfo)),
                    InstalledDate = GetProperty<DateTime>(nameof(PSResourceInfo.InstalledDate), psObjectInfo),
                    InstalledLocation = GetProperty<string>(nameof(PSResourceInfo.InstalledLocation), psObjectInfo),
                    LicenseUri = GetProperty<Uri>(nameof(PSResourceInfo.LicenseUri), psObjectInfo),
                    Name = GetProperty<string>(nameof(PSResourceInfo.Name), psObjectInfo),
                    PackageManagementProvider = GetProperty<string>(nameof(PSResourceInfo.PackageManagementProvider), psObjectInfo),
                    PowerShellGetFormatVersion = GetProperty<string>(nameof(PSResourceInfo.PowerShellGetFormatVersion), psObjectInfo),
                    ProjectUri = GetProperty<Uri>(nameof(PSResourceInfo.ProjectUri), psObjectInfo),
                    PublishedDate = GetProperty<DateTime>(nameof(PSResourceInfo.PublishedDate), psObjectInfo),
                    ReleaseNotes = GetProperty<string>(nameof(PSResourceInfo.ReleaseNotes), psObjectInfo),
                    Repository = GetProperty<string>(nameof(PSResourceInfo.Repository), psObjectInfo),
                    RepositorySourceLocation = GetProperty<string>(nameof(PSResourceInfo.RepositorySourceLocation), psObjectInfo),
                    Tags = Utils.GetStringArray(GetProperty<ArrayList>(nameof(PSResourceInfo.Tags), psObjectInfo)),
                    Type = GetProperty<string>(nameof(PSResourceInfo.Type), psObjectInfo),
                    UpdatedDate = GetProperty<DateTime>(nameof(PSResourceInfo.UpdatedDate), psObjectInfo),
                    Version = GetProperty<Version>(nameof(PSResourceInfo.Version), psObjectInfo)
                };

                return true;
            }
            catch(Exception ex)
            {
                errorMsg = string.Format(
                    CultureInfo.InvariantCulture,
                    @"TryReadPSGetInfo: Cannot read the PowerShellGet information file with error: {0}",
                    ex.Message);

                return false;
            }
        }

        public static bool TryParse(
            IPackageSearchMetadata metadataToParse,
            out PSResourceInfo psGetInfo,
            out string errorMsg)
        {
            psGetInfo = null;
            errorMsg = String.Empty;

            if (metadataToParse == null)
            {
                errorMsg = "TryParsePSResourceInfo: Invalid IPackageSearchMetadata object. Object cannot be null.";
                return false;
            }

            try
            {
                psGetInfo = new PSResourceInfo
                {
                    // AdditionalMetadata = GetProperty<Dictionary<string,string>>(nameof(PSResourceInfo.AdditionalMetadata), psObjectInfo),
                    Author = ParseMetadataAuthor(metadataToParse),
                    // CompanyName = GetProperty<string>(nameof(PSResourceInfo.CompanyName), psObjectInfo),
                    // Copyright = GetProperty<string>(nameof(PSResourceInfo.Copyright), psObjectInfo),
                    Dependencies = ParseMetadataDependencies(metadataToParse),
                    Description = ParseMetadataDescription(metadataToParse),
                    IconUri = ParseMetadataIconUri(metadataToParse),
                    // Includes = new ResourceIncludes(GetProperty<Hashtable>(nameof(PSResourceInfo.Includes), psObjectInfo)),
                    // InstalledDate = GetProperty<DateTime>(nameof(PSResourceInfo.InstalledDate), psObjectInfo),
                    // InstalledLocation = GetProperty<string>(nameof(PSResourceInfo.InstalledLocation), psObjectInfo),
                    LicenseUri = ParseMetadataLicenseUri(metadataToParse),
                    Name = ParseMetadataName(metadataToParse),
                    // PackageManagementProvider = GetProperty<string>(nameof(PSResourceInfo.PackageManagementProvider), psObjectInfo),
                    // PowerShellGetFormatVersion = GetProperty<string>(nameof(PSResourceInfo.PowerShellGetFormatVersion), psObjectInfo),
                    ProjectUri = ParseMetadataProjectUri(metadataToParse),
                    PublishedDate = ParseMetadataPublishedDate(metadataToParse),
                    // ReleaseNotes = GetProperty<string>(nameof(PSResourceInfo.ReleaseNotes), psObjectInfo),
                    // Repository = GetProperty<string>(nameof(PSResourceInfo.Repository), psObjectInfo),
                    // RepositorySourceLocation = GetProperty<string>(nameof(PSResourceInfo.RepositorySourceLocation), psObjectInfo),
                    Tags = ParseMetadataTags(metadataToParse),
                    Type = ParseMetadataType(metadataToParse),
                    // UpdatedDate = GetProperty<DateTime>(nameof(PSResourceInfo.UpdatedDate), psObjectInfo),
                    Version = ParseMetadataVersion(metadataToParse)
                };

                return true;
            }
            catch (Exception ex)
            {
                errorMsg = string.Format(
                    CultureInfo.InvariantCulture,
                    @"TryReadPSGetInfo: Cannot parse PSResourceInfo from IPackageSearchMetadata with error: {0}",
                    ex.Message);
                return false;
            }
        }

        public static bool TryParseCatalogEntry(CatalogEntry objToParse,
            out PSResourceInfo psGetInfo,
            out string errorMsg)
        {
            psGetInfo = null;
            errorMsg = String.Empty;

            if (objToParse == null)
            {
                errorMsg = "TryParseCatalogEntry: Invalid CatalogEntry object. Object cannot be null.";
                return false;
            }
            try
            {
                NuspecReader pkgNuspec = objToParse.GetNuspecAsync().GetAwaiter().GetResult();
                psGetInfo = new PSResourceInfo
                {
                    // // AdditionalMetadata = GetProperty<Dictionary<string,string>>(nameof(PSResourceInfo.AdditionalMetadata), psObjectInfo),
                    // Author = pkgNuspec.GetAuthors(),
                    // // CompanyName = GetProperty<string>(nameof(PSResourceInfo.CompanyName), psObjectInfo),
                    // Copyright = pkgNuspec.GetCopyright(),
                    // Dependencies = ParseCatalogEntryDependencies(pkgNuspec),
                    // Description = pkgNuspec.GetDescription(),
                    // IconUri = ParseCatalogEntryIconUri(pkgNuspec),
                    // // Includes = new ResourceIncludes(GetProperty<Hashtable>(nameof(PSResourceInfo.Includes), psObjectInfo)),
                    // // InstalledDate = GetProperty<DateTime>(nameof(PSResourceInfo.InstalledDate), psObjectInfo),
                    // // InstalledLocation = GetProperty<string>(nameof(PSResourceInfo.InstalledLocation), psObjectInfo),
                    // LicenseUri = ParseCatalogEntryLicenseUri(pkgNuspec),
                    Name = objToParse.Id,
                    // // PackageManagementProvider = GetProperty<string>(nameof(PSResourceInfo.PackageManagementProvider), psObjectInfo),
                    // // PowerShellGetFormatVersion = pkgNuspec.GetRepositoryMetadata.PowerShellGetFormatVersion,
                    // ProjectUri = ParseCatalogEntryProjectUri(pkgNuspec),
                    // PublishedDate = ParseCatalogEntryPublishedDate(objToParse),
                    // ReleaseNotes = pkgNuspec.GetReleaseNotes(),
                    // // Repository = GetProperty<string>(nameof(PSResourceInfo.Repository), psObjectInfo),
                    // RepositorySourceLocation = pkgNuspec.GetRepositoryMetadata().Url,
                    Tags = pkgNuspec.GetTags().Split(new char[]{' ', ','}, StringSplitOptions.RemoveEmptyEntries),
                    // Type = pkgNuspec.GetType().ToString(), //possibly need to change!
                    // // UpdatedDate = GetProperty<DateTime>(nameof(PSResourceInfo.UpdatedDate), psObjectInfo),
                    // Version = objToParse.Version.Version,
                };
                return true;
            }
            catch (Exception ex)
            {
                errorMsg = string.Format(
                    CultureInfo.InvariantCulture,
                    @"TryParseCatalogEntry: Cannot parse PSResourceInfo from CatalogEntry with error: {0}",
                    ex.Message);
                return false;
            }
        }

        #endregion

        #region Private static methods

        private static T ConvertToType<T>(PSObject psObject)
        {
            // We only convert Dictionary<string, string> types.
            if (typeof(T) != typeof(Dictionary<string, string>))
            {
                return default(T);
            }

            var dict = new Dictionary<string, string>();
            foreach (var prop in psObject.Properties)
            {
                dict.Add(prop.Name, prop.Value.ToString());
            }

            return (T)Convert.ChangeType(dict, typeof(T));
        }

        private static T GetProperty<T>(
            string Name,
            PSObject psObjectInfo)
        {
            var val = psObjectInfo.Properties[Name]?.Value;
            if (val == null)
            {
                return default(T);
            }

            switch (val)
            {
                case T valType:
                    return valType;

                case PSObject valPSObject:
                    switch (valPSObject.BaseObject)
                    {
                        case T valBase:
                            return valBase;

                        case PSCustomObject _:
                            // A base object of PSCustomObject means this is additional metadata
                            // and type T should be Dictionary<string,string>.
                            return ConvertToType<T>(valPSObject);

                        default:
                            return default(T);
                    }

                default:
                    return default(T);
            }
        }

        #region Parse Metadata private static methods

        private static string ParseMetadataAuthor(IPackageSearchMetadata pkg)
        {
            return pkg.Authors;
        }

        private static string[] ParseMetadataDependencies(IPackageSearchMetadata pkg)
        {
            List<string> deps = new List<string>();
            foreach(var r in pkg.DependencySets)
            {
                foreach (var pkgDependencyItem in r.Packages)
                {
                    deps.Add(pkgDependencyItem.Id);
                }
            }
            return deps.ToArray();
        }

        private static string ParseMetadataDescription(IPackageSearchMetadata pkg)
        {
            return pkg.Description;
        }

        private static Uri ParseMetadataIconUri(IPackageSearchMetadata pkg)
        {
            return pkg.IconUrl;
        }

        private static Uri ParseMetadataLicenseUri(IPackageSearchMetadata pkg)
        {
            return pkg.LicenseUrl;
        }

        private static string ParseMetadataName(IPackageSearchMetadata pkg)
        {
            if (pkg.Identity != null)
            {
                return pkg.Identity.Id;
            }
            return String.Empty;
        }

        private static Uri ParseMetadataProjectUri(IPackageSearchMetadata pkg)
        {
            return pkg.ProjectUrl;
        }

        private static DateTime? ParseMetadataPublishedDate(IPackageSearchMetadata pkg)
        {
            DateTime? publishDate = null;
            DateTimeOffset? pkgPublishedDate = pkg.Published;
            if (pkgPublishedDate.HasValue)
            {
                publishDate = pkgPublishedDate.Value.DateTime;
            }
            return publishDate;
        }

        private static string[] ParseMetadataTags(IPackageSearchMetadata pkg)
        {
            char[] delimeter = new char[]{' ', ','};
            return pkg.Tags.Split(delimeter, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string ParseMetadataType(IPackageSearchMetadata pkg)
        {
            string[] tags = ParseMetadataTags(pkg);
            foreach(string tag in tags)
            {
                if(String.Equals(tag, "PSScript", StringComparison.OrdinalIgnoreCase))
                {
                    return "Script";
                }
                // else if(String.Equals(tag, "PSModule", StringComparison.OrdinalIgnoreCase))
                // {
                //     return "Module";
                // }
            }
            // todo: or do we want to have additional type "undefined" which is returned here, esp for Galleries that don't have Type (i.e NuGet)
            return "Module";
        }

        private static Version ParseMetadataVersion(IPackageSearchMetadata pkg)
        {
            if (pkg.Identity != null)
            {
                return pkg.Identity.Version.Version;
            }
            return null;
        }

        #endregion

        #endregion

        #region Parse CatalogEntry private static methods
        private static string[] ParseCatalogEntryDependencies(NuspecReader pkgNuspec)
        {
            List<string> deps = new List<string>();
            foreach(var r in pkgNuspec.GetDependencyGroups())
            {
                foreach (var pkgDependencyItem in r.Packages)
                {
                    deps.Add(pkgDependencyItem.Id);
                }
            }
            return deps.ToArray();
        }

        private static DateTime? ParseCatalogEntryPublishedDate(CatalogEntry pkg)
        {
            DateTime? publishDate = null;
            DateTimeOffset? pkgPublishedDate = pkg.CommitTimeStamp;
            if (pkgPublishedDate.HasValue)
            {
                publishDate = pkgPublishedDate.Value.DateTime;
            }
            return publishDate;
        }

        private static Uri ParseCatalogEntryIconUri(NuspecReader pkgNuspec)
        {
            if(Uri.TryCreate(pkgNuspec.GetIconUrl(), 0, out Uri url))
            {
                return url;
            }
            return null;
        }

        private static Uri ParseCatalogEntryLicenseUri(NuspecReader pkgNuspec)
        {
            if(Uri.TryCreate(pkgNuspec.GetLicenseUrl(), 0, out Uri url))
            {
                return url;
            }
            return null;
        }

        private static Uri ParseCatalogEntryProjectUri(NuspecReader pkgNuspec)
        {
            if(Uri.TryCreate(pkgNuspec.GetProjectUrl(), 0, out Uri url))
            {
                return url;
            }
            return null;
        }
        #endregion

        #region Private methods

        private PSObject ConvertToCustomObject()
        {
            var additionalMetadata = new PSObject();
            foreach (var item in AdditionalMetadata)
            {
                additionalMetadata.Properties.Add(new PSNoteProperty(item.Key, item.Value));
            }

            var psObject = new PSObject();
            psObject.Properties.Add(new PSNoteProperty(nameof(AdditionalMetadata), additionalMetadata));
            psObject.Properties.Add(new PSNoteProperty(nameof(Author), Author));
            psObject.Properties.Add(new PSNoteProperty(nameof(CompanyName), CompanyName));
            psObject.Properties.Add(new PSNoteProperty(nameof(Copyright), Copyright));
            psObject.Properties.Add(new PSNoteProperty(nameof(Dependencies), Dependencies));
            psObject.Properties.Add(new PSNoteProperty(nameof(Description), Description));
            psObject.Properties.Add(new PSNoteProperty(nameof(IconUri), IconUri));
            psObject.Properties.Add(new PSNoteProperty(nameof(Includes), Includes.ConvertToHashtable()));
            psObject.Properties.Add(new PSNoteProperty(nameof(InstalledDate), InstalledDate));
            psObject.Properties.Add(new PSNoteProperty(nameof(InstalledLocation), InstalledLocation));
            psObject.Properties.Add(new PSNoteProperty(nameof(LicenseUri), LicenseUri));
            psObject.Properties.Add(new PSNoteProperty(nameof(Name), Name));
            psObject.Properties.Add(new PSNoteProperty(nameof(PackageManagementProvider), PackageManagementProvider));
            psObject.Properties.Add(new PSNoteProperty(nameof(PowerShellGetFormatVersion), PowerShellGetFormatVersion));
            psObject.Properties.Add(new PSNoteProperty(nameof(ProjectUri), ProjectUri));
            psObject.Properties.Add(new PSNoteProperty(nameof(PublishedDate), PublishedDate));
            psObject.Properties.Add(new PSNoteProperty(nameof(ReleaseNotes), ReleaseNotes));
            psObject.Properties.Add(new PSNoteProperty(nameof(Repository), Repository));
            psObject.Properties.Add(new PSNoteProperty(nameof(RepositorySourceLocation), RepositorySourceLocation));
            psObject.Properties.Add(new PSNoteProperty(nameof(Tags), Tags));
            psObject.Properties.Add(new PSNoteProperty(nameof(Type), Type));
            psObject.Properties.Add(new PSNoteProperty(nameof(UpdatedDate), UpdatedDate));
            psObject.Properties.Add(new PSNoteProperty(nameof(Version), Version));

            return psObject;
        }

        #endregion
    }

    #endregion

    #region Test Hooks

    public static class TestHooks
    {
        public static PSObject ReadPSGetResourceInfo(string filePath)
        {
            if (PSResourceInfo.TryRead(filePath, out PSResourceInfo psGetInfo, out string errorMsg))
            {
                return PSObject.AsPSObject(psGetInfo);
            }

            throw new PSInvalidOperationException(errorMsg);
        }

        public static void WritePSGetResourceInfo(
            string filePath,
            PSObject psObjectGetInfo)
        {
            if (psObjectGetInfo.BaseObject is PSResourceInfo psGetInfo)
            {
                if (! psGetInfo.TryWrite(filePath, out string errorMsg))
                {
                    throw new PSInvalidOperationException(errorMsg);
                }

                return;
            }

            throw new PSArgumentException("psObjectGetInfo argument is not a PSGetResourceInfo type.");
        }
    }

    #endregion
}
