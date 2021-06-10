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
using NuGet.Versioning;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    #region Enums

    [Flags]
    public enum ResourceType
    {
        // 00001 -> M
        // 00100 -> C
        // 00101 -> M, C
        // TODO: add default value of None = 0x0, and then see if we can pass in multiple values via cmdline
        None = 0x0,
        Module = 0x1,
        Script = 0x2,
        Command = 0x4,
        DscResource = 0x8
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


    #region Dependency

    public sealed class Dependency
    {
        #region Properties

        public string Name { get; }

        public VersionRange VersionRange { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor
        ///
        /// </summary>
        /// <param name="includes">Hashtable of PSGet includes</param>
        public Dependency(string dependencyName, VersionRange dependencyVersionRange)
        {
            Name = dependencyName;
            VersionRange = dependencyVersionRange;
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
        // public string[] Dependencies { get; set; }
        public Dependency[] Dependencies { get; set; }
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
        public ResourceType Type { get; set; }
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
                    // Dependencies = Utils.GetStringArray(GetProperty<ArrayList>(nameof(PSResourceInfo.Dependencies), psObjectInfo)),

                    Dependencies = GetDependencies(GetProperty<ArrayList>(nameof(PSResourceInfo.Dependencies), psObjectInfo)),

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
                    // try to get the value of PSResourceInfo.Type property, if the value is null use ResourceType.Module as value
                    // this value will be used in Enum.TryParse. If Enum.TryParse returns false, use ResourceType.Module to set Type instead.
                    Type = Enum.TryParse(
                        GetProperty<string>(nameof(PSResourceInfo.Type), psObjectInfo) ?? nameof(ResourceType.Module),
                        out ResourceType currentReadType)
                            ? currentReadType : ResourceType.Module,
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
            string pkgName,
            string repositoryName,
            ResourceType? type,
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
                    // not all of the properties of PSResourceInfo are filled as they are not there in metadata returned for Find-PSResource.
                    Author = ParseMetadataAuthor(metadataToParse),
                    Dependencies = ParseMetadataDependencies(metadataToParse),
                    Description = ParseMetadataDescription(metadataToParse),
                    IconUri = ParseMetadataIconUri(metadataToParse),
                    LicenseUri = ParseMetadataLicenseUri(metadataToParse),
                    Name = ParseMetadataName(metadataToParse),
                    ProjectUri = ParseMetadataProjectUri(metadataToParse),
                    PublishedDate = ParseMetadataPublishedDate(metadataToParse),
                    Repository = repositoryName,
                    Tags = ParseMetadataTags(metadataToParse),
                    Type = ParseMetadataType(metadataToParse, pkgName, repositoryName, type),
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

        // TODO: remove this code!
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


        private static Dependency[] GetDependencies(ArrayList dependencyInfos)
        {
            List<Dependency> dependenciesFound = new List<Dependency>();
            if (dependencyInfos == null) { return dependenciesFound.ToArray(); }

            foreach(Hashtable dependencyInfo in dependencyInfos)
            {
                if (!dependencyInfo.ContainsKey("Name"))
                {
                    // TODO: add dbg assert
                    continue;
                }
                string dependencyName = (string) dependencyInfo["Name"];
                // TODO: dbg assert name not null or empty

                if (dependencyInfo.ContainsKey("RequiredVersion"))
                {
                    if (!Utils.TryParseVersionOrVersionRange((string) dependencyInfo["RequiredVersion"], out VersionRange dependencyVersion))
                    {
                        // dbg assert
                        continue; // TODO: ask Amber, use all version version instead?
                    }

                    dependenciesFound.Add(new Dependency(dependencyName, dependencyVersion));
                    continue;
                }

                if (dependencyInfo.ContainsKey("MinimumVersion") || dependencyInfo.ContainsKey("MaximumVersion"))
                {
                    NuGetVersion minimumVersion = null;
                    NuGetVersion maximumVersion = null;
                    bool includeMin = false;
                    bool includeMax = false;

                    if (dependencyInfo.ContainsKey("MinimumVersion") &&
                        !NuGetVersion.TryParse((string) dependencyInfo["MinimumVersion"], out minimumVersion))
                    {
                        // "(" + versionString + ","
                        // dbg assert
                        continue;
                    }

                    if (dependencyInfo.ContainsKey("MaximumVersion") &&
                        !NuGetVersion.TryParse((string) dependencyInfo["MaximumVersion"], out maximumVersion))
                    {
                        // dbg assert- say this is invalid range entry
                        continue;
                    }

                    if (minimumVersion != null)
                    {
                        includeMin = true;
                    }

                    if (maximumVersion != null)
                    {
                        includeMax = true;
                    }

                    VersionRange dependencyVersionRange = new VersionRange(
                        minVersion: minimumVersion,
                        includeMinVersion: includeMin,
                        maxVersion: maximumVersion,
                        includeMaxVersion: includeMax);

                    dependenciesFound.Add(new Dependency(dependencyName, dependencyVersionRange));
                    continue;
                }

                // neither Required, Minimum or Maximum Version provided
                VersionRange dependencyVersionRangeAll = VersionRange.All;
                dependenciesFound.Add(new Dependency(dependencyName, dependencyVersionRangeAll));
            }

            return dependenciesFound.ToArray();
        }


        #region Parse Metadata private static methods

        private static string ParseMetadataAuthor(IPackageSearchMetadata pkg)
        {
            return pkg.Authors;
        }

        // private static string[] ParseMetadataDependencies(IPackageSearchMetadata pkg)
        // {
        //     List<string> deps = new List<string>();
        //     foreach(var r in pkg.DependencySets)
        //     {
        //         foreach (var pkgDependencyItem in r.Packages)
        //         {
        //             string depInfo = pkgDependencyItem.Id + "-" + pkgDependencyItem.VersionRange.ToString();
        //             deps.Add(depInfo);
        //             // deps.Add(pkgDependencyItem.Id);
        //         }
        //     }
        //     return deps.ToArray();
        // }

        private static Dependency[] ParseMetadataDependencies(IPackageSearchMetadata pkg)
        {
            List<Dependency> dependencies = new List<Dependency>();
            foreach(var pkgDependencyGroup in pkg.DependencySets)
            {
                foreach(var pkgDependencyItem in pkgDependencyGroup.Packages)
                {
                    // do I have to check version range is not null? can we have package with dependency but no version?
                    Dependency currentDependency = new Dependency(pkgDependencyItem.Id, pkgDependencyItem.VersionRange);
                    dependencies.Add(currentDependency);
                }
            }
            return dependencies.ToArray();
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

        private static ResourceType ParseMetadataType(IPackageSearchMetadata pkg, string pkgName, string repoName, ResourceType? pkgType)
        {
            // M, C
            // M, D
            // M
            // S

            string[] tags = ParseMetadataTags(pkg);
            ResourceType currentPkgType = ResourceType.Module;

            // Check if package came from PSGalleryScripts repo- this indicates that it should have a PSScript tag
            // (however some packages that had a wildcard in their name are missing PSScript or PSModule tags)
            // but we were able to get the packages by using SearchAsync() with the appropriate Script or Module repository endpoint
            // and can check repository endpoint to determine Type.
            // Module packages missing tags are accounted for as the default case, and we account for scripts with the following check:
            if ((pkgType == null && String.Equals("PSGalleryScripts", repoName, StringComparison.InvariantCultureIgnoreCase)) ||
                (pkgType != null && pkgType == ResourceType.Script))
            {
                // it's a Script resource, so clear default Module tag because a Script resource cannot also be a Module resource
                currentPkgType &= ~ResourceType.Module;
                currentPkgType |= ResourceType.Script;
            }

            // if Name contains wildcard, currently Script and Module tags should be set properly, but need to account for Command and DscResource types too
            // if Name does not contain wildcard, GetMetadataAsync() was used, PSGallery only is searched (and pkg will successfully be found
            // and returned from there) before PSGalleryScripts can be searched
            foreach(string tag in tags)
            {
                if(String.Equals(tag, "PSScript", StringComparison.InvariantCultureIgnoreCase))
                {
                    // clear default Module tag, because a Script resource cannot be a Module resource also
                    currentPkgType &= ~ResourceType.Module;
                    currentPkgType |= ResourceType.Script;
                }
                if (tag.StartsWith("PSCommand_"))
                {
                    currentPkgType |= ResourceType.Command;
                }
                if (String.Equals(tag, "PSIncludes_DscResource", StringComparison.InvariantCultureIgnoreCase))
                {
                    currentPkgType |= ResourceType.DscResource;
                }
            }
            return currentPkgType;
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
