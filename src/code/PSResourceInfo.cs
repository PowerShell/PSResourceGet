using System.Text.RegularExpressions;
using System.Linq;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using Dbg = System.Diagnostics.Debug;
using System.Globalization;
using System.Management.Automation;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.Reflection;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    #region Enums

    [Flags]
    public enum ResourceType
    {
        // 00001 -> M
        // 00100 -> C
        // 00101 -> M, C
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

    public enum ScopeType
    {
        None,
        CurrentUser,
        AllUsers
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
        public Dependency[] Dependencies { get; set; }
        public string Description { get; set; }
        public Uri IconUri { get; set; }
        public ResourceIncludes Includes { get; set; }
        public DateTime? InstalledDate { get; set; }
        public string InstalledLocation { get; set; }
        public bool IsPrerelease { get; set; }
        public Uri LicenseUri { get; set; }
        public string Name { get; set; }
        public string PackageManagementProvider { get; set; }
        public string PowerShellGetFormatVersion { get; set; }
        public string PrereleaseLabel { get; set; }
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

        #region Private fields
        private static readonly char[] Delimeter = {' ', ','};

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
        /// a PSResourceInfo object containing the file contents.
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

                var additionalMetadata = GetProperty<Dictionary<string,string>>(nameof(PSResourceInfo.AdditionalMetadata), psObjectInfo);
                Version version = GetVersionInfo(psObjectInfo, additionalMetadata, out string prereleaseLabel);

                psGetInfo = new PSResourceInfo
                {
                    AdditionalMetadata = additionalMetadata,
                    Author = GetStringProperty(nameof(PSResourceInfo.Author), psObjectInfo),
                    CompanyName = GetStringProperty(nameof(PSResourceInfo.CompanyName), psObjectInfo),
                    Copyright = GetStringProperty(nameof(PSResourceInfo.Copyright), psObjectInfo),
                    Dependencies = GetDependencies(GetProperty<ArrayList>(nameof(PSResourceInfo.Dependencies), psObjectInfo)),
                    Description = GetStringProperty(nameof(PSResourceInfo.Description), psObjectInfo),
                    IconUri = GetProperty<Uri>(nameof(PSResourceInfo.IconUri), psObjectInfo),
                    Includes = new ResourceIncludes(GetProperty<Hashtable>(nameof(PSResourceInfo.Includes), psObjectInfo)),
                    InstalledDate = GetProperty<DateTime>(nameof(PSResourceInfo.InstalledDate), psObjectInfo),
                    InstalledLocation = GetStringProperty(nameof(PSResourceInfo.InstalledLocation), psObjectInfo),
                    IsPrerelease = GetProperty<bool>(nameof(PSResourceInfo.IsPrerelease), psObjectInfo),
                    LicenseUri = GetProperty<Uri>(nameof(PSResourceInfo.LicenseUri), psObjectInfo),
                    Name = GetStringProperty(nameof(PSResourceInfo.Name), psObjectInfo),
                    PackageManagementProvider = GetStringProperty(nameof(PSResourceInfo.PackageManagementProvider), psObjectInfo),
                    PowerShellGetFormatVersion = GetStringProperty(nameof(PSResourceInfo.PowerShellGetFormatVersion), psObjectInfo),
                    PrereleaseLabel = prereleaseLabel,
                    ProjectUri = GetProperty<Uri>(nameof(PSResourceInfo.ProjectUri), psObjectInfo),
                    PublishedDate = GetProperty<DateTime>(nameof(PSResourceInfo.PublishedDate), psObjectInfo),
                    ReleaseNotes = GetStringProperty(nameof(PSResourceInfo.ReleaseNotes), psObjectInfo),
                    Repository = GetStringProperty(nameof(PSResourceInfo.Repository), psObjectInfo),
                    RepositorySourceLocation = GetStringProperty(nameof(PSResourceInfo.RepositorySourceLocation), psObjectInfo),
                    Tags = Utils.GetStringArray(GetProperty<ArrayList>(nameof(PSResourceInfo.Tags), psObjectInfo)),
                    // try to get the value of PSResourceInfo.Type property, if the value is null use ResourceType.Module as value
                    // this value will be used in Enum.TryParse. If Enum.TryParse returns false, use ResourceType.Module to set Type instead.
                    Type = Enum.TryParse(
                        GetProperty<string>(nameof(PSResourceInfo.Type), psObjectInfo) ?? nameof(ResourceType.Module),
                            out ResourceType currentReadType)
                                ? currentReadType : ResourceType.Module,
                    UpdatedDate = GetProperty<DateTime>(nameof(PSResourceInfo.UpdatedDate), psObjectInfo),
                    Version = version
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

        private static string GetStringProperty(
            string name,
            PSObject psObjectInfo)
        {
            return GetProperty<string>(name, psObjectInfo) ?? string.Empty;
        }

        private static Version GetVersionInfo(
            PSObject psObjectInfo,
            Dictionary<string, string> additionalMetadata,
            out string prereleaseLabel)
        {
            string versionString = GetProperty<string>(nameof(PSResourceInfo.Version), psObjectInfo);
            prereleaseLabel = String.Empty;

            if (!String.IsNullOrEmpty(versionString) ||
                additionalMetadata.TryGetValue("NormalizedVersion", out versionString))
            {
                string pkgVersion = versionString;
                if (versionString.Contains("-"))
                {
                    string[] versionStringParsed = versionString.Split('-');
                    if (versionStringParsed.Length == 1)
                    {
                        pkgVersion = versionStringParsed[0];
                    }
                    else
                    {
                        // versionStringParsed.Length > 1 (because string contained '-' so couldn't be 0)
                        pkgVersion = versionStringParsed[0];
                        prereleaseLabel = versionStringParsed[1];
                    }
                }

                if (!Version.TryParse(pkgVersion, out Version parsedVersion))
                {
                    prereleaseLabel = String.Empty;
                    return null;
                }
                else
                {
                    return parsedVersion;
                }
            }

            prereleaseLabel = String.Empty;
            return GetProperty<Version>(nameof(PSResourceInfo.Version), psObjectInfo);
        }


        public static bool TryConvert(
            IPackageSearchMetadata metadataToParse,
            out PSResourceInfo psGetInfo,
            string repositoryName,
            ResourceType? type,
            out string errorMsg)
        {
            psGetInfo = null;
            errorMsg = String.Empty;

            if (metadataToParse == null)
            {
                errorMsg = "TryConvertPSResourceInfo: Invalid IPackageSearchMetadata object. Object cannot be null.";
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
                    IsPrerelease = ParseMetadataIsPrerelease(metadataToParse),
                    LicenseUri = ParseMetadataLicenseUri(metadataToParse),
                    Name = ParseMetadataName(metadataToParse),
                    PrereleaseLabel = ParsePrerelease(metadataToParse),
                    ProjectUri = ParseMetadataProjectUri(metadataToParse),
                    PublishedDate = ParseMetadataPublishedDate(metadataToParse),
                    Repository = repositoryName,
                    Tags = ParseMetadataTags(metadataToParse),
                    Type = ParseMetadataType(metadataToParse, repositoryName, type),
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

        private static string GetPrereleaseLabel(Version version)
        {
            string versionAsString = version.ToString();

            if (!versionAsString.Contains("-"))
            {
                // no prerelease label present
                return String.Empty;
            }

            string[] prereleaseParsed = versionAsString.Split('-');
            if (prereleaseParsed.Length <= 1)
            {
                return String.Empty;
            }

            string prereleaseString = prereleaseParsed[1];
            Regex prereleasePattern = new Regex("^[a-zA-Z0-9]+$");
            if (!prereleasePattern.IsMatch(prereleaseString))
            {
                return String.Empty;
            }

            return prereleaseString;
        }

        private static Dependency[] GetDependencies(ArrayList dependencyInfos)
        {
            List<Dependency> dependenciesFound = new List<Dependency>();
            if (dependencyInfos == null) { return dependenciesFound.ToArray(); }

            
            foreach(PSObject dependencyObj in dependencyInfos)
            {
                // can be an array or hashtable
                if (dependencyObj.BaseObject is Hashtable dependencyInfo)
                {
                    if (!dependencyInfo.ContainsKey("Name"))
                    {
                        Dbg.Assert(false, "Derived dependencies Hashtable must contain a Name key");
                        continue;
                    }

                    string dependencyName = (string)dependencyInfo["Name"];
                    if (String.IsNullOrEmpty(dependencyName))
                    {
                        Dbg.Assert(false, "Dependency Name must not be null or empty");
                        continue;
                    }

                    if (dependencyInfo.ContainsKey("RequiredVersion"))
                    {
                        if (!Utils.TryParseVersionOrVersionRange((string)dependencyInfo["RequiredVersion"], out VersionRange dependencyVersion))
                        {
                            dependencyVersion = VersionRange.All;
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
                            !NuGetVersion.TryParse((string)dependencyInfo["MinimumVersion"], out minimumVersion))
                        {
                            VersionRange dependencyAll = VersionRange.All;
                            dependenciesFound.Add(new Dependency(dependencyName, dependencyAll));
                            continue;
                        }

                        if (dependencyInfo.ContainsKey("MaximumVersion") &&
                            !NuGetVersion.TryParse((string)dependencyInfo["MaximumVersion"], out maximumVersion))
                        {
                            VersionRange dependencyAll = VersionRange.All;
                            dependenciesFound.Add(new Dependency(dependencyName, dependencyAll));
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
                else if (dependencyObj.Properties["Name"] != null)
                {
                    string name = dependencyObj.Properties["Name"].Value.ToString();

                    string version = string.Empty;
                    VersionRange versionRange = VersionRange.All;

                    if (dependencyObj.Properties["VersionRange"] != null)
                    {
                        version = dependencyObj.Properties["VersionRange"].Value.ToString();
                        VersionRange.TryParse(version, out versionRange);
                    }

                    dependenciesFound.Add(new Dependency(name, versionRange));
                }
            }

            return dependenciesFound.ToArray();
        }

        private static string ConcatenateVersionWithPrerelease(string version, string prerelease)
        {
            // if no prerelease, just version suffices
            if (String.IsNullOrEmpty(prerelease))
            {
                return version;
            }

            int numVersionDigits = version.Split('.').Count();
            if (numVersionDigits == 3)
            {
                // 0.5.3 -> version string , preview4 -> prerelease string , return: 5.3.0-preview4
                return version + "-" + prerelease;
            }


            // number of digits not equivalent to 3 was not supported in V2
            return version;
        }


        #region Parse Metadata private static methods

        private static string ParseMetadataAuthor(IPackageSearchMetadata pkg)
        {
            return pkg.Authors;
        }

        private static Dependency[] ParseMetadataDependencies(IPackageSearchMetadata pkg)
        {
            List<Dependency> dependencies = new List<Dependency>();
            foreach(var pkgDependencyGroup in pkg.DependencySets)
            {
                foreach(var pkgDependencyItem in pkgDependencyGroup.Packages)
                {
                    // check if version range is not null. In case we have package with dependency but no version specified
                    VersionRange depVersionRange;
                    if (pkgDependencyItem.VersionRange == null)
                    {
                        depVersionRange = VersionRange.All;
                    }
                    else
                    {
                        depVersionRange = pkgDependencyItem.VersionRange;
                    }

                    Dependency currentDependency = new Dependency(pkgDependencyItem.Id, depVersionRange);
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

        private static bool ParseMetadataIsPrerelease(IPackageSearchMetadata pkg)
        {
            return pkg.Identity?.Version?.IsPrerelease ?? false;
        }

        private static Uri ParseMetadataLicenseUri(IPackageSearchMetadata pkg)
        {
            return pkg.LicenseUrl;
        }

        private static string ParseMetadataName(IPackageSearchMetadata pkg)
        {
            return pkg.Identity?.Id ?? string.Empty;
        }

        private static string ParsePrerelease(IPackageSearchMetadata pkg)
        {
            return pkg.Identity.Version.ReleaseLabels.Count() == 0 ?
                String.Empty :
                pkg.Identity.Version.ReleaseLabels.FirstOrDefault();
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
            return pkg.Tags.Split(Delimeter, StringSplitOptions.RemoveEmptyEntries);
        }

        private static ResourceType ParseMetadataType(IPackageSearchMetadata pkg, string repoName, ResourceType? pkgType)
        {
            // possible type combinations:
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

        #region Private methods

        private PSObject ConvertToCustomObject()
        {
            var additionalMetadata = new PSObject();

            // Need to add a null check here due to null ref exception getting thrown
            if (AdditionalMetadata != null)
            {
                foreach (var item in AdditionalMetadata)
                {
                    additionalMetadata.Properties.Add(new PSNoteProperty(item.Key, item.Value));
                }

            }
            var psObject = new PSObject();
            psObject.Properties.Add(new PSNoteProperty(nameof(Name), Name ?? string.Empty));
            psObject.Properties.Add(new PSNoteProperty(nameof(Version), ConcatenateVersionWithPrerelease(Version.ToString(), PrereleaseLabel)));
            psObject.Properties.Add(new PSNoteProperty(nameof(Type), Type));
            psObject.Properties.Add(new PSNoteProperty(nameof(Description), Description ?? string.Empty));
            psObject.Properties.Add(new PSNoteProperty(nameof(Author), Author ?? string.Empty));
            psObject.Properties.Add(new PSNoteProperty(nameof(CompanyName), CompanyName ?? string.Empty));
            psObject.Properties.Add(new PSNoteProperty(nameof(Copyright), Copyright ?? string.Empty));
            psObject.Properties.Add(new PSNoteProperty(nameof(PublishedDate), PublishedDate));
            psObject.Properties.Add(new PSNoteProperty(nameof(InstalledDate), InstalledDate));
            psObject.Properties.Add(new PSNoteProperty(nameof(UpdatedDate), UpdatedDate));
            psObject.Properties.Add(new PSNoteProperty(nameof(LicenseUri), LicenseUri));
            psObject.Properties.Add(new PSNoteProperty(nameof(ProjectUri), ProjectUri));
            psObject.Properties.Add(new PSNoteProperty(nameof(IconUri), IconUri));
            psObject.Properties.Add(new PSNoteProperty(nameof(Tags), Tags));
            psObject.Properties.Add(new PSNoteProperty(nameof(Includes), Includes != null ? Includes.ConvertToHashtable() : null));
            psObject.Properties.Add(new PSNoteProperty(nameof(PowerShellGetFormatVersion), PowerShellGetFormatVersion ?? string.Empty));
            psObject.Properties.Add(new PSNoteProperty(nameof(ReleaseNotes), ReleaseNotes ?? string.Empty));
            psObject.Properties.Add(new PSNoteProperty(nameof(Dependencies), Dependencies));
            psObject.Properties.Add(new PSNoteProperty(nameof(RepositorySourceLocation), RepositorySourceLocation ?? string.Empty));
            psObject.Properties.Add(new PSNoteProperty(nameof(Repository), Repository ?? string.Empty));
            psObject.Properties.Add(new PSNoteProperty(nameof(PackageManagementProvider), PackageManagementProvider ?? string.Empty));
            psObject.Properties.Add(new PSNoteProperty(nameof(AdditionalMetadata), additionalMetadata));
            psObject.Properties.Add(new PSNoteProperty(nameof(InstalledLocation), InstalledLocation ?? string.Empty));

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
