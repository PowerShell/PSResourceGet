// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using NuGet.Versioning;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;

using Dbg = System.Diagnostics.Debug;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    #region Enums

    [Flags]
    public enum ResourceType
    {
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
        internal ResourceIncludes(Hashtable includes)
        {
            if (includes == null) { return; }

            Cmdlet = GetHashTableItem(includes, nameof(Cmdlet));
            Command = GetHashTableItem(includes, nameof(Command));
            DscResource = GetHashTableItem(includes, nameof(DscResource));
            Function = GetHashTableItem(includes, nameof(Function));
            RoleCapability = GetHashTableItem(includes, nameof(RoleCapability));
            Workflow = GetHashTableItem(includes, nameof(Workflow));
        }

        internal ResourceIncludes()
        {
            Cmdlet = Utils.EmptyStrArray;
            Command = Utils.EmptyStrArray;
            DscResource = Utils.EmptyStrArray;
            Function = Utils.EmptyStrArray;
            RoleCapability = Utils.EmptyStrArray;
            Workflow = Utils.EmptyStrArray;
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

    #region PSCommandResourceInfo
    public sealed class PSCommandResourceInfo
    {
        // this object will represent a Command or DSCResource
        // included by the PSResourceInfo property
        #region Properties

        public string Name { get; }

        public PSResourceInfo ParentResource { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">Name of the command or DSC resource</param>
        /// <param name="parentResource">the parent module resource the command or dsc resource belongs to</param>
        public PSCommandResourceInfo(string name, PSResourceInfo parentResource)
        {
           Name = name;
           ParentResource = parentResource; 
        }

        #endregion        
    }

    #endregion

    #region PSResourceInfo

    public sealed class PSResourceInfo
    {
        #region Properties

        public Dictionary<string, string> AdditionalMetadata { get; }
        public string Author { get; }
        public string CompanyName { get; internal set; }
        public string Copyright { get; }
        public Dependency[] Dependencies { get; }
        public string Description { get; }
        public Uri IconUri { get; }
        public ResourceIncludes Includes { get; }
        public DateTime? InstalledDate { get; internal set; }
        public string InstalledLocation { get; internal set; }
        public bool IsPrerelease { get; }
        public Uri LicenseUri { get; }
        public string Name { get; }
        public string PackageManagementProvider { get; }
        public string PowerShellGetFormatVersion { get; }
        public string Prerelease { get; }
        public Uri ProjectUri { get; }
        public DateTime? PublishedDate { get; }
        public string ReleaseNotes { get; }
        public string Repository { get; }
        public string RepositorySourceLocation { get; }
        public string[] Tags { get; }
        public ResourceType Type { get; }
        public DateTime? UpdatedDate { get; }
        public Version Version { get; }

        #endregion

        #region Constructors

        private PSResourceInfo() { }

        private PSResourceInfo(
            Dictionary<string, string> additionalMetadata,
            string author,
            string companyName,
            string copyright,
            Dependency[] dependencies,
            string description,
            Uri iconUri,
            ResourceIncludes includes,
            DateTime? installedDate,
            string installedLocation,
            bool isPrelease,
            Uri licenseUri,
            string name,
            string packageManagementProvider,
            string powershellGetFormatVersion,
            string prerelease,
            Uri projectUri,
            DateTime? publishedDate,
            string releaseNotes,
            string repository,
            string repositorySourceLocation,
            string[] tags,
            ResourceType type,
            DateTime? updatedDate,
            Version version)
        {
            AdditionalMetadata = additionalMetadata ?? new Dictionary<string, string>();
            Author = author ?? string.Empty;
            CompanyName = companyName ?? string.Empty;
            Copyright = copyright ?? string.Empty;
            Dependencies = dependencies ?? new Dependency[0];
            Description = description ?? string.Empty;
            IconUri = iconUri;
            Includes = includes ?? new ResourceIncludes();
            InstalledDate = installedDate;
            InstalledLocation = installedLocation ?? string.Empty;
            IsPrerelease = isPrelease;
            LicenseUri = licenseUri;
            Name = name ?? string.Empty;
            PackageManagementProvider = packageManagementProvider ?? string.Empty;
            PowerShellGetFormatVersion = powershellGetFormatVersion ?? string.Empty;
            Prerelease = prerelease ?? string.Empty;
            ProjectUri = projectUri;
            PublishedDate = publishedDate;
            ReleaseNotes = releaseNotes ?? string.Empty;
            Repository = repository ?? string.Empty;
            RepositorySourceLocation = repositorySourceLocation ?? string.Empty;
            Tags = tags ?? Utils.EmptyStrArray;
            Type = type;
            UpdatedDate = updatedDate;
            Version = version ?? new Version();
        }

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
                Version version = GetVersionInfo(psObjectInfo, additionalMetadata, out string prerelease);

                psGetInfo = new PSResourceInfo(
                    additionalMetadata: additionalMetadata,
                    author: GetStringProperty(nameof(PSResourceInfo.Author), psObjectInfo),
                    companyName: GetStringProperty(nameof(PSResourceInfo.CompanyName), psObjectInfo),
                    copyright: GetStringProperty(nameof(PSResourceInfo.Copyright), psObjectInfo),
                    dependencies: GetDependencies(GetProperty<ArrayList>(nameof(PSResourceInfo.Dependencies), psObjectInfo)),
                    description: GetStringProperty(nameof(PSResourceInfo.Description), psObjectInfo),
                    iconUri: GetProperty<Uri>(nameof(PSResourceInfo.IconUri), psObjectInfo),
                    includes: new ResourceIncludes(GetProperty<Hashtable>(nameof(PSResourceInfo.Includes), psObjectInfo)),
                    installedDate: GetProperty<DateTime>(nameof(PSResourceInfo.InstalledDate), psObjectInfo),
                    installedLocation: GetStringProperty(nameof(PSResourceInfo.InstalledLocation), psObjectInfo),
                    isPrelease: GetProperty<bool>(nameof(PSResourceInfo.IsPrerelease), psObjectInfo),
                    licenseUri: GetProperty<Uri>(nameof(PSResourceInfo.LicenseUri), psObjectInfo),
                    name: GetStringProperty(nameof(PSResourceInfo.Name), psObjectInfo),
                    packageManagementProvider: GetStringProperty(nameof(PSResourceInfo.PackageManagementProvider), psObjectInfo),
                    powershellGetFormatVersion: GetStringProperty(nameof(PSResourceInfo.PowerShellGetFormatVersion), psObjectInfo),
                    prerelease: prerelease,
                    projectUri: GetProperty<Uri>(nameof(PSResourceInfo.ProjectUri), psObjectInfo),
                    publishedDate: GetProperty<DateTime>(nameof(PSResourceInfo.PublishedDate), psObjectInfo),
                    releaseNotes: GetStringProperty(nameof(PSResourceInfo.ReleaseNotes), psObjectInfo),
                    repository: GetStringProperty(nameof(PSResourceInfo.Repository), psObjectInfo),
                    repositorySourceLocation: GetStringProperty(nameof(PSResourceInfo.RepositorySourceLocation), psObjectInfo),
                    tags: Utils.GetStringArray(GetProperty<ArrayList>(nameof(PSResourceInfo.Tags), psObjectInfo)),
                    type: Enum.TryParse(
                            GetProperty<string>(nameof(PSResourceInfo.Type), psObjectInfo) ?? nameof(ResourceType.Module),
                                out ResourceType currentReadType)
                                    ? currentReadType : ResourceType.Module,
                    updatedDate: GetProperty<DateTime>(nameof(PSResourceInfo.UpdatedDate), psObjectInfo),
                    version: version);

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
            out string prerelease)
        {
            string versionString = GetProperty<string>(nameof(PSResourceInfo.Version), psObjectInfo);
            prerelease = String.Empty;

            if (!String.IsNullOrEmpty(versionString) ||
                additionalMetadata.TryGetValue("NormalizedVersion", out versionString))
            {
                string pkgVersion = versionString;
                if (versionString.Contains("-"))
                {
                    // versionString: "1.2.0-alpha1"
                    string[] versionStringParsed = versionString.Split('-');
                    if (versionStringParsed.Length == 1)
                    {
                        // versionString: "1.2.0-" (unlikely, at least should not be from our PSResourceInfo.TryWrite())
                        pkgVersion = versionStringParsed[0];
                    }
                    else
                    {
                        // versionStringParsed.Length > 1 (because string contained '-' so couldn't be 0)
                        // versionString: "1.2.0-alpha1"
                        pkgVersion = versionStringParsed[0];
                        prerelease = versionStringParsed[1];
                    }
                }

                // at this point, version is normalized (i.e either "1.2.0" (if part of prerelease) or "1.2.0.0" otherwise)
                // parse the pkgVersion parsed out above into a System.Version object
                if (!Version.TryParse(pkgVersion, out Version parsedVersion))
                {
                    prerelease = String.Empty;
                    return null;
                }
                else
                {
                    return parsedVersion;
                }
            }

            // version could not be parsed as string, it was written to XML file as a System.Version object
            // V3 code briefly did so, I believe so we provide support for it
            prerelease = String.Empty;
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
                var typeInfo = ParseMetadataType(metadataToParse, repositoryName, type, out ArrayList commandNames, out ArrayList dscResourceNames);
                var resourceHashtable = new Hashtable();
                resourceHashtable.Add(nameof(PSResourceInfo.Includes.Command), new PSObject(commandNames));
                resourceHashtable.Add(nameof(PSResourceInfo.Includes.DscResource), new PSObject(dscResourceNames));
                var includes = new ResourceIncludes(resourceHashtable);


                psGetInfo = new PSResourceInfo(
                    additionalMetadata: null,
                    author: ParseMetadataAuthor(metadataToParse),
                    companyName: null,
                    copyright: null,
                    dependencies: ParseMetadataDependencies(metadataToParse),
                    description: ParseMetadataDescription(metadataToParse),
                    iconUri: ParseMetadataIconUri(metadataToParse),
                    includes: includes,
                    installedDate: null,
                    installedLocation: null,
                    isPrelease: ParseMetadataIsPrerelease(metadataToParse),
                    licenseUri: ParseMetadataLicenseUri(metadataToParse),
                    name: ParseMetadataName(metadataToParse),
                    packageManagementProvider: null,
                    powershellGetFormatVersion: null,
                    prerelease: ParsePrerelease(metadataToParse),
                    projectUri: ParseMetadataProjectUri(metadataToParse),
                    publishedDate: ParseMetadataPublishedDate(metadataToParse),
                    releaseNotes: null,
                    repository: repositoryName,
                    repositorySourceLocation: null,
                    tags: ParseMetadataTags(metadataToParse),
                    // type: ParseMetadataType(metadataToParse, repositoryName, type),
                    type: typeInfo,
                    updatedDate: null,
                    version: ParseMetadataVersion(metadataToParse));

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

        private static Dependency[] GetDependencies(ArrayList dependencyInfos)
        {
            List<Dependency> dependenciesFound = new List<Dependency>();
            if (dependencyInfos == null) { return dependenciesFound.ToArray(); }

            foreach(PSObject dependencyObj in dependencyInfos)
            {
                // The dependency object can be a string or a hashtable
                // eg:
                // RequiredModules = @('PSGetTestDependency1')
                // RequiredModules = @(@{ModuleName='PackageManagement';ModuleVersion='1.0.0.1'})
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
                    VersionRange versionRange = VersionRange.All;
                    if (dependencyObj.Properties["VersionRange"] != null)
                    {
                        VersionRange.TryParse(
                            dependencyObj.Properties["VersionRange"].Value.ToString(),
                            out versionRange);
                    }

                    dependenciesFound.Add(new Dependency(name, versionRange));
                }
            }

            return dependenciesFound.ToArray();
        }

        private static string ConcatenateVersionWithPrerelease(string version, string prerelease)
        {
            return Utils.GetNormalizedVersionString(version, prerelease);
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

                    dependencies.Add(
                        new Dependency(pkgDependencyItem.Id, depVersionRange));
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
            return pkg.Identity.Version.ReleaseLabels.Count() > 0 ?
                pkg.Identity.Version.ReleaseLabels.FirstOrDefault() :
                String.Empty;
        }

        private static Uri ParseMetadataProjectUri(IPackageSearchMetadata pkg)
        {
            return pkg.ProjectUrl;
        }

        private static DateTime? ParseMetadataPublishedDate(IPackageSearchMetadata pkg)
        {
            if (pkg.Published.HasValue)
            {
                return pkg.Published.Value.DateTime;
            }

            return null;
        }

        private static string[] ParseMetadataTags(IPackageSearchMetadata pkg)
        {
            return pkg.Tags.Split(Delimeter, StringSplitOptions.RemoveEmptyEntries);
        }

        private static ResourceType ParseMetadataType(
            IPackageSearchMetadata pkg,
            string repoName,
            ResourceType? pkgType,
            out ArrayList commandNames,
            out ArrayList dscResourceNames)
        {
            // possible type combinations:
            // M, C
            // M, D
            // M
            // S

            commandNames = new ArrayList();
            dscResourceNames = new ArrayList();
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
            foreach (string tag in tags)
            {
                if(String.Equals(tag, "PSScript", StringComparison.InvariantCultureIgnoreCase))
                {
                    // clear default Module tag, because a Script resource cannot be a Module resource also
                    currentPkgType &= ~ResourceType.Module;
                    currentPkgType |= ResourceType.Script;
                }

                if (tag.StartsWith("PSCommand_", StringComparison.InvariantCultureIgnoreCase))
                {
                    currentPkgType |= ResourceType.Command;
                    commandNames.Add(tag.Split('_')[1]);
                }

                if (tag.StartsWith("PSDscResource_", StringComparison.InvariantCultureIgnoreCase))
                {
                    currentPkgType |= ResourceType.DscResource;
                    dscResourceNames.Add(tag.Split('_')[1]);
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
            // 1.0.0-alpha1
            // 1.0.0.0
            string NormalizedVersion = IsPrerelease ? ConcatenateVersionWithPrerelease(Version.ToString(), Prerelease) : Version.ToString();

            var additionalMetadata = new PSObject();

            if (!AdditionalMetadata.ContainsKey(nameof(IsPrerelease)))
            {
                AdditionalMetadata.Add(nameof(IsPrerelease), IsPrerelease.ToString());
            }
            else
            {
                AdditionalMetadata[nameof(IsPrerelease)] = IsPrerelease.ToString();
            }

            // This is added for V2, V3 does not need it.
            if (!AdditionalMetadata.ContainsKey(nameof(NormalizedVersion)))
            {
                AdditionalMetadata.Add(nameof(NormalizedVersion), NormalizedVersion);
            }
            else
            {
                AdditionalMetadata[nameof(NormalizedVersion)] = NormalizedVersion;
            }

            foreach (var item in AdditionalMetadata)
            {
                additionalMetadata.Properties.Add(new PSNoteProperty(item.Key, item.Value));
            }

            var psObject = new PSObject();
            psObject.Properties.Add(new PSNoteProperty(nameof(Name), Name));
            psObject.Properties.Add(new PSNoteProperty(nameof(Version), NormalizedVersion));
            psObject.Properties.Add(new PSNoteProperty(nameof(Type), Type));
            psObject.Properties.Add(new PSNoteProperty(nameof(Description), Description));
            psObject.Properties.Add(new PSNoteProperty(nameof(Author), Author));
            psObject.Properties.Add(new PSNoteProperty(nameof(CompanyName), CompanyName));
            psObject.Properties.Add(new PSNoteProperty(nameof(Copyright), Copyright));
            psObject.Properties.Add(new PSNoteProperty(nameof(PublishedDate), PublishedDate));
            psObject.Properties.Add(new PSNoteProperty(nameof(InstalledDate), InstalledDate));
            psObject.Properties.Add(new PSNoteProperty(nameof(IsPrerelease), IsPrerelease));
            psObject.Properties.Add(new PSNoteProperty(nameof(UpdatedDate), UpdatedDate));
            psObject.Properties.Add(new PSNoteProperty(nameof(LicenseUri), LicenseUri));
            psObject.Properties.Add(new PSNoteProperty(nameof(ProjectUri), ProjectUri));
            psObject.Properties.Add(new PSNoteProperty(nameof(IconUri), IconUri));
            psObject.Properties.Add(new PSNoteProperty(nameof(Tags), Tags));
            psObject.Properties.Add(new PSNoteProperty(nameof(Includes), Includes.ConvertToHashtable()));
            psObject.Properties.Add(new PSNoteProperty(nameof(PowerShellGetFormatVersion), PowerShellGetFormatVersion));
            psObject.Properties.Add(new PSNoteProperty(nameof(ReleaseNotes), ReleaseNotes));
            psObject.Properties.Add(new PSNoteProperty(nameof(Dependencies), Dependencies));
            psObject.Properties.Add(new PSNoteProperty(nameof(RepositorySourceLocation), RepositorySourceLocation));
            psObject.Properties.Add(new PSNoteProperty(nameof(Repository), Repository));
            psObject.Properties.Add(new PSNoteProperty(nameof(PackageManagementProvider), PackageManagementProvider));
            psObject.Properties.Add(new PSNoteProperty(nameof(AdditionalMetadata), additionalMetadata));
            psObject.Properties.Add(new PSNoteProperty(nameof(InstalledLocation), InstalledLocation));

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
                if (!psGetInfo.TryWrite(filePath, out string errorMsg))
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
