// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using NuGet.Versioning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation;
using System.Text.Json;
using System.Xml;
using Microsoft.PowerShell.Commands;

using Dbg = System.Diagnostics.Debug;

namespace Microsoft.PowerShell.PSResourceGet.UtilClasses
{
    #region Enums

    public enum ResourceType
    {
        None,
        Module,
        Script,
        Nupkg
    }

    public enum VersionType
    {
        NoVersion,
        SpecificVersion,
        VersionRange
    }

    public enum ScopeType
    {
        CurrentUser,
        AllUsers
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
        /// An object describes a package dependency
        /// </summary>
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

        public string[] Names { get; }

        public PSResourceInfo ParentResource { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="names">Name of the command or DSC resource</param>
        /// <param name="parentResource">the parent module resource the command or dsc resource belongs to</param>
        public PSCommandResourceInfo(string[] names, PSResourceInfo parentResource)
        {
           Names = names;
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
        public string Author { get; set; }
        public string CompanyName { get; set; }
        public string Copyright { get; set; }
        public Dependency[] Dependencies { get; set; }
        public string Description { get; set; }
        public Uri IconUri { get; set; }
        public ResourceIncludes Includes { get; }
        public DateTime? InstalledDate { get; set; }
        public string InstalledLocation { get; set; }
        public bool IsPrerelease { get; set; }
        public Uri LicenseUri { get; set; }
        public string Name { get; set; }
        private string PowerShellGetFormatVersion { get; }
        public string Prerelease { get; }
        public Uri ProjectUri { get; set; }
        public DateTime? PublishedDate { get; set; }
        public string ReleaseNotes { get; set; }
        public string Repository { get; set; }
        public string RepositorySourceLocation { get; set; }
        public string[] Tags { get; set; }
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
            bool isPrerelease,
            Uri licenseUri,
            string name,
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
            IsPrerelease = isPrerelease;
            LicenseUri = licenseUri;
            Name = name ?? string.Empty;
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
        /// PSResourceGet file format.
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
                    @"TryWritePSGetInfo: Cannot convert and write the PSResourceGet information to file, with error: {0}",
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
                    isPrerelease: GetProperty<bool>(nameof(PSResourceInfo.IsPrerelease), psObjectInfo),
                    licenseUri: GetProperty<Uri>(nameof(PSResourceInfo.LicenseUri), psObjectInfo),
                    name: GetStringProperty(nameof(PSResourceInfo.Name), psObjectInfo),
                    powershellGetFormatVersion: GetStringProperty(nameof(PSResourceInfo.PowerShellGetFormatVersion), psObjectInfo),
                    prerelease: prerelease,
                    projectUri: GetProperty<Uri>(nameof(PSResourceInfo.ProjectUri), psObjectInfo),
                    publishedDate: GetProperty<DateTime>(nameof(PSResourceInfo.PublishedDate), psObjectInfo),
                    releaseNotes: GetStringProperty(nameof(PSResourceInfo.ReleaseNotes), psObjectInfo),
                    repository: GetStringProperty(nameof(PSResourceInfo.Repository), psObjectInfo),
                    repositorySourceLocation: GetStringProperty(nameof(PSResourceInfo.RepositorySourceLocation), psObjectInfo),
                    tags: Utils.GetStringArray(GetProperty<ArrayList>(nameof(PSResourceInfo.Tags), psObjectInfo)),
                    type: Enum.TryParse(
                            GetProperty<object>(nameof(PSResourceInfo.Type), psObjectInfo).ToString() ?? nameof(ResourceType.Module),
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
                    @"TryReadPSGetInfo: Cannot read the PSResourceGet information file with error: {0}",
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

        /// <summary>
        /// Converts XML entry to PSResourceInfo instance
        /// used for V2 Server API call find response conversion to PSResourceInfo object
        /// </summary>
        public static bool TryConvertFromXml(
            XmlNode entry,
            out PSResourceInfo psGetInfo,
            PSRepositoryInfo repository,
            out string errorMsg)
        {
            psGetInfo = null;
            errorMsg = String.Empty;

            if (entry == null)
            {
                errorMsg = "TryConvertXmlToPSResourceInfo: Invalid XmlNodeList object. Object cannot be null.";
                return false;
            }
            
            try
            {
                Hashtable metadata = new Hashtable(StringComparer.InvariantCultureIgnoreCase);

                var entryChildNodes = entry.ChildNodes;
                foreach (XmlElement entryChild in entryChildNodes)
                {
                    var entryKey = entryChild.LocalName;

                    // For repositories such as JFrog's Artifactory, there is no 'Id' property, just 'title' (which contains the name of the pkg).
                    // However, other repos, like PSGallery include the name of the pkg in the 'Id' property and leave 'title' empty.
                    // In JFrog's Artifactory, 'title' exists both as a child of the 'entry' node and as a child of the 'properties' node,
                    // though sometimes 'title' under the 'properties' node can be empty (so default to using the former).
                    if (entryKey.Equals("title"))
                    {
                        metadata["Id"] = entryChild.InnerText;
                    }
                    else if (entryKey.Equals("properties"))
                    {
                        var propertyChildNodes = entryChild.ChildNodes;
                        foreach (XmlElement propertyChild in propertyChildNodes)
                        {
                            var propertyKey = propertyChild.LocalName;
                            var propertyValue = propertyChild.InnerText;

                            if (propertyKey.Equals("Title"))
                            {
                                if (!metadata.ContainsKey("Id"))
                                {
                                    metadata["Id"] = propertyValue;
                                }
                            }
                            if (propertyKey.Equals("Version"))
                            {
                                metadata[propertyKey] = ParseHttpVersion(propertyValue, out string prereleaseLabel);
                                metadata["Prerelease"] = prereleaseLabel;
                            }
                            else if (propertyKey.EndsWith("Url"))
                            {
                                metadata[propertyKey] = ParseHttpUrl(propertyValue) as Uri;
                            }
                            else if (propertyKey.Equals("Tags"))
                            {
                                metadata[propertyKey] = propertyValue.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            }
                            else if (propertyKey.Equals("Published"))
                            {
                                metadata[propertyKey] = ParseHttpDateTime(propertyValue);
                            }
                            else if (propertyKey.Equals("Dependencies"))
                            {
                                metadata[propertyKey] = ParseHttpDependencies(propertyValue);
                            }
                            else if (propertyKey.Equals("IsPrerelease"))
                            {
                                bool.TryParse(propertyValue, out bool isPrerelease);

                                metadata[propertyKey] = isPrerelease;
                            }
                            else if (propertyKey.Equals("NormalizedVersion"))
                            {
                                if (!NuGetVersion.TryParse(propertyValue, out NuGetVersion parsedNormalizedVersion))
                                {
                                    errorMsg = string.Format(
                                        CultureInfo.InvariantCulture,
                                        @"TryReadPSGetInfo: Cannot parse NormalizedVersion");

                                    parsedNormalizedVersion = new NuGetVersion("1.0.0.0");
                                }

                                metadata[propertyKey] = parsedNormalizedVersion;
                            }
                            else
                            {
                                metadata[propertyKey] = propertyValue;
                            }
                        }
                    }
                }

                var typeInfo = ParseHttpMetadataType(metadata["Tags"] as string[], out ArrayList commandNames, out ArrayList cmdletNames, out ArrayList dscResourceNames);
                var resourceHashtable = new Hashtable {
                    { nameof(PSResourceInfo.Includes.Command), new PSObject(commandNames) },
                    { nameof(PSResourceInfo.Includes.Cmdlet), new PSObject(cmdletNames) },
                    { nameof(PSResourceInfo.Includes.DscResource), new PSObject(dscResourceNames) }
                };

                var additionalMetadataHashtable = new Dictionary<string, string>();

                // Only add NormalizedVersion to additionalMetadata if server response included it
                if (metadata.ContainsKey("NormalizedVersion")) {
                    additionalMetadataHashtable.Add("NormalizedVersion", metadata["NormalizedVersion"].ToString());
                }

                var includes = new ResourceIncludes(resourceHashtable);

                psGetInfo = new PSResourceInfo(
                    additionalMetadata: additionalMetadataHashtable,
                    author: metadata["Authors"] as String,
                    companyName: metadata["CompanyName"] as String,
                    copyright: metadata["Copyright"] as String,
                    dependencies: metadata["Dependencies"] as Dependency[],
                    description: metadata["Description"] as String,
                    iconUri: metadata["IconUrl"] as Uri,
                    includes: includes,
                    installedDate: null,
                    installedLocation: null,
                    isPrerelease: (bool) metadata["IsPrerelease"],
                    licenseUri: metadata["LicenseUrl"] as Uri,
                    name: metadata["Id"] as String,
                    powershellGetFormatVersion: null,   
                    prerelease: metadata["Prerelease"] as String,
                    projectUri: metadata["ProjectUrl"] as Uri,
                    publishedDate: metadata["Published"] as DateTime?,
                    releaseNotes: metadata["ReleaseNotes"] as String,
                    repository: repository.Name,
                    repositorySourceLocation: repository.Uri.ToString(),
                    tags: metadata["Tags"] as string[],
                    type: typeInfo,
                    updatedDate: null,
                    version: metadata["Version"] as Version);
                
                return true;
            }
            catch (Exception ex)
            {
                errorMsg = string.Format(
                    CultureInfo.InvariantCulture,
                    @"TryConvertFromXml: Cannot parse PSResourceInfo from XmlNode with error: {0}",
                    ex.Message);

                return false;
            }
        }

        /// <summary>
        /// Converts JsonDocument entry to PSResourceInfo instance
        /// used for V3 Server API call find response conversion to PSResourceInfo object
        /// </summary>
        public static bool TryConvertFromJson(
          JsonDocument pkgJson,
          out PSResourceInfo psGetInfo,
          PSRepositoryInfo repository,
          out string errorMsg)
        {
            psGetInfo = null;
            errorMsg = String.Empty;

            if (pkgJson == null)
            {
                errorMsg = "TryConvertJsonToPSResourceInfo: Invalid json object. Object cannot be null.";
                return false;
            }

            try
            {
                Hashtable metadata = new Hashtable(StringComparer.InvariantCultureIgnoreCase);
                JsonElement rootDom = pkgJson.RootElement;

                // Version
                if (rootDom.TryGetProperty("version", out JsonElement versionElement))
                {
                    string versionValue = versionElement.ToString();
                    metadata["Version"] = ParseHttpVersion(versionValue, out string prereleaseLabel);
                    metadata["Prerelease"] = prereleaseLabel;
                    // ADO server response does not contain "isPrerelease" element, so we set it here.
                    metadata["IsPrerelease"] = !String.IsNullOrEmpty(prereleaseLabel);

                    if (!NuGetVersion.TryParse(versionValue, out NuGetVersion parsedNormalizedVersion))
                    {
                        errorMsg = string.Format(
                            CultureInfo.InvariantCulture,
                            @"TryReadPSGetInfo: Cannot parse NormalizedVersion");

                        parsedNormalizedVersion = new NuGetVersion("1.0.0.0");
                    }

                    metadata["NormalizedVersion"] = parsedNormalizedVersion;
                }

                // License Url
                if (rootDom.TryGetProperty("licenseUrl", out JsonElement licenseUrlElement))
                {
                    metadata["LicenseUrl"] = ParseHttpUrl(licenseUrlElement.ToString()) as Uri;
                }

                // Project Url
                if (rootDom.TryGetProperty("projectUrl", out JsonElement projectUrlElement))
                {
                    metadata["ProjectUrl"] = ParseHttpUrl(projectUrlElement.ToString()) as Uri;
                }

                // Icon Url
                if (rootDom.TryGetProperty("iconUrl", out JsonElement iconUrlElement))
                {
                    metadata["IconUrl"] = ParseHttpUrl(iconUrlElement.ToString()) as Uri;
                }

                // Tags
                if (rootDom.TryGetProperty("tags", out JsonElement tagsElement))
                {
                    string[] pkgTags = Utils.EmptyStrArray;
                    if (tagsElement.ValueKind == JsonValueKind.Array)
                    {
                        var arrayLength = tagsElement.GetArrayLength();
                        List<string> tags = new List<string>(arrayLength);
                        foreach (var tag in tagsElement.EnumerateArray())
                        {
                            tags.Add(tag.ToString());
                        }

                        pkgTags = tags.ToArray();
                    }
                    else if (tagsElement.ValueKind == JsonValueKind.String)
                    {
                        string tagStr = tagsElement.ToString();
                        pkgTags = tagStr.Split(Utils.WhitespaceSeparator, StringSplitOptions.RemoveEmptyEntries);
                    }

                    metadata["Tags"] = pkgTags;
                }

                // PublishedDate
                if (rootDom.TryGetProperty("published", out JsonElement publishedElement))
                {
                    metadata["PublishedDate"] = ParseHttpDateTime(publishedElement.ToString());
                }

                // Dependencies
                // TODO, tracked via: https://github.com/PowerShell/PSResourceGet/issues/1169

                // IsPrerelease
                // NuGet.org repository's response does contain 'isPrerelease' element so it can be accquired and set here.
                if (rootDom.TryGetProperty("isPrerelease", out JsonElement isPrereleaseElement))
                {
                    metadata["IsPrerelease"] = isPrereleaseElement.GetBoolean();
                }

                // Author
                if (rootDom.TryGetProperty("authors", out JsonElement authorsElement))
                {
                    metadata["Authors"] = authorsElement.ToString();

                    // CompanyName
                    // CompanyName is not provided in v3 pkg metadata response, so we've just set it to the author,
                    // which is often the company
                    metadata["CompanyName"] = authorsElement.ToString();
                }

                // Copyright
                if (rootDom.TryGetProperty("copyright", out JsonElement copyrightElement))
                {
                    metadata["Copyright"] = copyrightElement.ToString();
                }

                // Description
                if (rootDom.TryGetProperty("description", out JsonElement descriptiontElement))
                {
                    metadata["Description"] = descriptiontElement.ToString();
                }

                // Id
                if (rootDom.TryGetProperty("id", out JsonElement idElement))
                {
                    metadata["Id"] = idElement.ToString();
                }
                
                // ReleaseNotes
                if (rootDom.TryGetProperty("releaseNotes", out JsonElement releaseNotesElement)) {
                    metadata["ReleaseNotes"] = releaseNotesElement.ToString();
                }

                var additionalMetadataHashtable = new Dictionary<string, string>
                {
                    { "NormalizedVersion", metadata["NormalizedVersion"].ToString() }
                };

                psGetInfo = new PSResourceInfo(
                    additionalMetadata: additionalMetadataHashtable,
                    author: metadata["Authors"] as String,
                    companyName: metadata["CompanyName"] as String,
                    copyright: metadata["Copyright"] as String,
                    dependencies: metadata["Dependencies"] as Dependency[],
                    description: metadata["Description"] as String,
                    iconUri: null,
                    includes: null,
                    installedDate: null,
                    installedLocation: null,
                    isPrerelease: (bool)metadata["IsPrerelease"],
                    licenseUri: metadata["LicenseUrl"] as Uri,
                    name: metadata["Id"] as String,
                    powershellGetFormatVersion: null,
                    prerelease: metadata["Prerelease"] as String,
                    projectUri: metadata["ProjectUrl"] as Uri,
                    publishedDate: metadata["PublishedDate"] as DateTime?,
                    releaseNotes: metadata["ReleaseNotes"] as String,
                    repository: repository.Name,
                    repositorySourceLocation: repository.Uri.ToString(),
                    tags: metadata["Tags"] as string[],
                    type: ResourceType.None,
                    updatedDate: null,
                    version: metadata["Version"] as Version);
                    
                return true;
                
            }
            catch (Exception ex)
            {
                errorMsg = string.Format(
                    CultureInfo.InvariantCulture,
                    @"TryConvertFromJson: Cannot parse PSResourceInfo from json object with error: {0}",
                    ex.Message);
                    
                return false;
            }
        }

        /// <summary>
        /// Converts ContainerRegistry JsonDocument entry to PSResourceInfo instance
        /// used for ContainerRegistry Server API call find response conversion to PSResourceInfo object
        /// </summary>
        public static bool TryConvertFromContainerRegistryJson(
          string packageName,
          JsonDocument packageMetadata,
          ResourceType? resourceType,
          out PSResourceInfo psGetInfo,
          PSRepositoryInfo repository, 
          out string errorMsg)
        {
            psGetInfo = null;
            errorMsg = String.Empty;

            if (packageMetadata == null)
            {
                errorMsg = "TryConvertFromContainerRegistryJson: Invalid json object. Object cannot be null.";
                return false;
            }

            try
            {
                Hashtable metadata = new Hashtable(StringComparer.InvariantCultureIgnoreCase);
                JsonElement rootDom = packageMetadata.RootElement;
                metadata["IsPrerelease"] = false;
                metadata["Prerelease"] = String.Empty;
                string versionValue = String.Empty;
                Version pkgVersion = null;

                // Version
                // For scripts (i.e with "Version" property) the version can contain prerelease label
                if (rootDom.TryGetProperty("Version", out JsonElement scriptVersionElement))
                {
                    versionValue = scriptVersionElement.ToString();
                    pkgVersion = ParseHttpVersion(versionValue, out string prereleaseLabel);
                    metadata["Version"] = pkgVersion;
                    metadata["Prerelease"] = prereleaseLabel;
                    metadata["IsPrerelease"] = !String.IsNullOrEmpty(prereleaseLabel);
                }
                else if(rootDom.TryGetProperty("ModuleVersion", out JsonElement moduleVersionElement))
                {
                    // For modules (i.e with "ModuleVersion" property) it will just contain the numerical part not prerelease label, so we must find that from PrivateData.PSData.Prerelease entry
                    versionValue = moduleVersionElement.ToString();
                    pkgVersion = ParseHttpVersion(versionValue, out string prereleaseLabel);
                    metadata["Version"] = pkgVersion;

                    if (rootDom.TryGetProperty("PrivateData", out JsonElement privateDataElement) && privateDataElement.TryGetProperty("PSData", out JsonElement psDataElement))
                    {
                        if (psDataElement.TryGetProperty("Prerelease", out JsonElement pkgPrereleaseLabelElement) && !String.IsNullOrEmpty(pkgPrereleaseLabelElement.ToString().Trim()))
                        {
                            prereleaseLabel = pkgPrereleaseLabelElement.ToString().Trim();
                            versionValue += $"-{prereleaseLabel}";
                            metadata["Prerelease"] = prereleaseLabel;
                            metadata["IsPrerelease"] = true;
                        }
                    }
                }
                else
                {
                    errorMsg = string.Format(
                        CultureInfo.InvariantCulture,
                        @"TryConvertFromContainerRegistryJson: Neither 'ModuleVersion' nor 'Version' could be found in package metadata");

                    return false;
                }

                if (!NuGetVersion.TryParse(versionValue, out NuGetVersion parsedNormalizedVersion) && pkgVersion == null)
                {
                    errorMsg = string.Format(
                        CultureInfo.InvariantCulture,
                        @"TryConvertFromContainerRegistryJson: Cannot parse NormalizedVersion or System.Version from version in metadata.");

                    return false;
                }

                metadata["NormalizedVersion"] = parsedNormalizedVersion.ToNormalizedString();

                // License Url
                if (rootDom.TryGetProperty("LicenseUrl", out JsonElement licenseUrlElement))
                {
                    metadata["LicenseUrl"] = ParseHttpUrl(licenseUrlElement.ToString()) as Uri;
                }

                // Project Url
                if (rootDom.TryGetProperty("ProjectUrl", out JsonElement projectUrlElement))
                {
                    metadata["ProjectUrl"] = ParseHttpUrl(projectUrlElement.ToString()) as Uri;
                }

                // Icon Url
                if (rootDom.TryGetProperty("IconUrl", out JsonElement iconUrlElement))
                {
                    metadata["IconUrl"] = ParseHttpUrl(iconUrlElement.ToString()) as Uri;
                }

                // Tags
                if (rootDom.TryGetProperty("Tags", out JsonElement tagsElement))
                {
                    string[] pkgTags = Utils.EmptyStrArray;
                    if (tagsElement.ValueKind == JsonValueKind.Array)
                    {
                        var arrayLength = tagsElement.GetArrayLength();
                        List<string> tags = new List<string>(arrayLength);
                        foreach (var tag in tagsElement.EnumerateArray())
                        {
                            tags.Add(tag.ToString());
                        }

                        pkgTags = tags.ToArray();
                    }
                    else if (tagsElement.ValueKind == JsonValueKind.String)
                    {
                        string tagStr = tagsElement.ToString();
                        pkgTags = tagStr.Split(Utils.WhitespaceSeparator, StringSplitOptions.RemoveEmptyEntries);
                    }

                    metadata["Tags"] = pkgTags;
                }

                // PublishedDate
                if (rootDom.TryGetProperty("Published", out JsonElement publishedElement))
                {
                    metadata["PublishedDate"] = ParseHttpDateTime(publishedElement.ToString());
                }

                // IsPrerelease
                if (rootDom.TryGetProperty("IsPrerelease", out JsonElement isPrereleaseElement))
                {
                    metadata["IsPrerelease"] = isPrereleaseElement.GetBoolean();
                }

                // Author
                if (rootDom.TryGetProperty("Authors", out JsonElement authorsElement))
                {
                    metadata["Authors"] = authorsElement.ToString();

                    // CompanyName
                    // CompanyName is not provided in v3 pkg metadata response, so we've just set it to the author,
                    // which is often the company
                    metadata["CompanyName"] = authorsElement.ToString();
                }

                // Copyright
                if (rootDom.TryGetProperty("Copyright", out JsonElement copyrightElement))
                {
                    metadata["Copyright"] = copyrightElement.ToString();
                }

                // Description
                if (rootDom.TryGetProperty("Description", out JsonElement descriptiontElement))
                {
                    metadata["Description"] = descriptiontElement.ToString();
                }

                // ReleaseNotes
                if (rootDom.TryGetProperty("ReleaseNotes", out JsonElement releaseNotesElement))
                {
                    metadata["ReleaseNotes"] = releaseNotesElement.ToString();
                }

                // Dependencies
                if (rootDom.TryGetProperty("RequiredModules", out JsonElement requiredModulesElement))
                {
                    metadata["Dependencies"] = ParseContainerRegistryDependencies(requiredModulesElement, out errorMsg).ToArray();
                }
                if (string.Equals(packageName, "Az", StringComparison.OrdinalIgnoreCase) || packageName.StartsWith("Az.", StringComparison.OrdinalIgnoreCase))
                {
                    if (rootDom.TryGetProperty("PrivateData", out JsonElement privateDataElement) && privateDataElement.TryGetProperty("PSData", out JsonElement psDataElement))
                    {
                        if (psDataElement.TryGetProperty("ModuleList", out JsonElement moduleListDepsElement))
                        {
                            metadata["Dependencies"] = ParseContainerRegistryDependencies(moduleListDepsElement, out errorMsg).ToArray();
                        }
                    }
                }

                var additionalMetadataHashtable = new Dictionary<string, string>
                {
                    { "NormalizedVersion", metadata["NormalizedVersion"].ToString() }
                };
               
                psGetInfo = new PSResourceInfo(
                    additionalMetadata: additionalMetadataHashtable,
                    author: metadata["Authors"] as String,
                    companyName: metadata["CompanyName"] as String,
                    copyright: metadata["Copyright"] as String,
                    dependencies: metadata["Dependencies"] as Dependency[],
                    description: metadata["Description"] as String,
                    iconUri: null,
                    includes: null,
                    installedDate: null,
                    installedLocation: null,
                    isPrerelease: (bool)metadata["IsPrerelease"],
                    licenseUri: metadata["LicenseUrl"] as Uri,
                    name: packageName,
                    powershellGetFormatVersion: null,
                    prerelease: metadata["Prerelease"] as String,
                    projectUri: metadata["ProjectUrl"] as Uri,
                    publishedDate: metadata["PublishedDate"] as DateTime?,
                    releaseNotes: metadata["ReleaseNotes"] as String,
                    repository: repository.Name,
                    repositorySourceLocation: repository.Uri.ToString(),
                    tags: metadata["Tags"] as string[],
                    type: resourceType ?? ResourceType.None,
                    updatedDate: null,
                    version: metadata["Version"] as Version);

                return true;
                
            }
            catch (Exception ex)
            {
                errorMsg = string.Format(
                    CultureInfo.InvariantCulture,
                    @"TryConvertFromContainerRegistryJson: Cannot parse PSResourceInfo from json object with error: {0}",
                    ex.Message);
                    
                return false;
            }
        }

        public static bool TryConvertFromHashtableForPsd1(
            Hashtable pkgMetadata,
            out PSResourceInfo psGetInfo,
            out string errorMsg,
            PSRepositoryInfo repository)
        {
            errorMsg = String.Empty;
            psGetInfo = null;

            try
            {
                List<Hashtable> requiredModulesHashList = new List<Hashtable>();
                if (pkgMetadata.ContainsKey("RequiredModules"))
                {
                    var requiredModules = pkgMetadata["RequiredModules"] as Object[];
                    foreach (Object obj in requiredModules)
                    {
                        if (obj != null)
                        {
                            if (obj is Hashtable hash)
                            {
                                requiredModulesHashList.Add(hash);
                            }
                            else if (obj is string modName)
                            {
                                Hashtable moduleNameHash = new Hashtable(StringComparer.OrdinalIgnoreCase);
                                moduleNameHash.Add("ModuleName", modName);
                                requiredModulesHashList.Add(moduleNameHash);
                            }
                        } 
                    }
                }

                Hashtable[] requiredModulesHashArray = requiredModulesHashList.ToArray();
                Dependency[] deps = GetDependenciesForPsd1(requiredModulesHashArray);

                var typeInfo = ParseHttpMetadataTypeForLocalRepo(pkgMetadata, out ArrayList commandNames, out ArrayList cmdletNames, out ArrayList dscResourceNames);
                var resourceHashtable = new Hashtable {
                    { nameof(PSResourceInfo.Includes.Command), new PSObject(commandNames) },
                    { nameof(PSResourceInfo.Includes.Cmdlet), new PSObject(cmdletNames) },
                    { nameof(PSResourceInfo.Includes.DscResource), new PSObject(dscResourceNames) }
                };

                string prereleaseLabel = (string) pkgMetadata["Prerelease"];
                bool isPrerelease = !String.IsNullOrEmpty(prereleaseLabel);

                Uri iconUri = pkgMetadata["IconUri"] as Uri;
                Uri licenseUri = pkgMetadata["LicenseUri"] as Uri;
                Uri projectUri = pkgMetadata["ProjectUri"] as Uri;
                string releaseNotes = pkgMetadata["ReleaseNotes"] as string;
                string[] tags = pkgMetadata["Tags"] as string[];

                string version = pkgMetadata["ModuleVersion"] as string;
                string normalizedVersion = ConcatenateVersionWithPrerelease(version, prereleaseLabel);
                var additionalMetadataHashtable = new Dictionary<string, string>();
                additionalMetadataHashtable.Add("NormalizedVersion", normalizedVersion);

                var includes = new ResourceIncludes(resourceHashtable);

                psGetInfo = new PSResourceInfo(
                    additionalMetadata: additionalMetadataHashtable,
                    author: pkgMetadata["Author"] as String,
                    companyName: pkgMetadata["CompanyName"] as String,
                    copyright: pkgMetadata["Copyright"] as String,
                    dependencies: deps,
                    description: pkgMetadata["Description"] as String,
                    iconUri: iconUri,
                    includes: includes,
                    installedDate: null,
                    installedLocation: null,
                    isPrerelease: isPrerelease,
                    licenseUri: licenseUri,
                    name: pkgMetadata["Id"] as String,
                    powershellGetFormatVersion: null,   
                    prerelease: prereleaseLabel,
                    projectUri: projectUri,
                    publishedDate: null,
                    releaseNotes: releaseNotes,
                    repository: repository.Name,
                    repositorySourceLocation: repository.Uri.ToString(),
                    tags: tags,
                    type: ResourceType.Module,
                    updatedDate: null,
                    version: Version.Parse(version));

                return true;
            }
            catch(Exception ex)
            {
                errorMsg = string.Format(
                    CultureInfo.InvariantCulture,
                    @"TryConvertFromHashtableForPsd1: Could not find expected information from module manifest file hashtable with error: {0}",
                    ex.Message);

                return false;
            }
        }

        public static bool TryConvertFromHashtableForPs1(
            Hashtable pkgMetadata,
            out PSResourceInfo psGetInfo,
            out string errorMsg,
            PSRepositoryInfo repository)
        {
            errorMsg = String.Empty;
            psGetInfo = null;

            try
            {
                string[] tagsEntry = pkgMetadata["Tags"] as string[];
                var typeInfo = ParseHttpMetadataTypeForLocalRepo(pkgMetadata, out ArrayList commandNames, out ArrayList cmdletNames, out ArrayList dscResourceNames);
                var resourceHashtable = new Hashtable {
                    { nameof(PSResourceInfo.Includes.Command), new PSObject(commandNames) },
                    {  nameof(PSResourceInfo.Includes.Cmdlet), new PSObject(cmdletNames) },
                    {  nameof(PSResourceInfo.Includes.DscResource), new PSObject(dscResourceNames) }
                };

                var includes = new ResourceIncludes(resourceHashtable);

                NuGetVersion nugetVersion = pkgMetadata["Version"] as NuGetVersion;
                bool isPrerelease = nugetVersion.IsPrerelease;
                Version version = nugetVersion.Version;
                string prereleaseLabel = isPrerelease ? nugetVersion.ToNormalizedString().Split(new char[]{'-'})[1] : String.Empty;

                var additionalMetadataHashtable = new Dictionary<string, string> {
                    { "NormalizedVersion", nugetVersion.ToNormalizedString() }
                };

                ModuleSpecification[] requiredModules = pkgMetadata.ContainsKey("RequiredModules") ? pkgMetadata["RequiredModules"] as ModuleSpecification[] : new ModuleSpecification[]{};

                psGetInfo = new PSResourceInfo(
                    additionalMetadata: additionalMetadataHashtable,
                    author: pkgMetadata["Author"] as String,
                    companyName: pkgMetadata["CompanyName"] as String,
                    copyright: pkgMetadata["Copyright"] as String,
                    dependencies: GetDependenciesForPs1(requiredModules),
                    description: pkgMetadata["Description"] as String,
                    iconUri: pkgMetadata["IconUri"] as Uri,
                    includes: includes,
                    installedDate: null,
                    installedLocation: null,
                    isPrerelease: isPrerelease,
                    licenseUri: pkgMetadata["LicenseUri"] as Uri,
                    name: pkgMetadata["Id"] as String,
                    powershellGetFormatVersion: null,   
                    prerelease: prereleaseLabel,
                    projectUri: pkgMetadata["ProjectUri"] as Uri,
                    publishedDate: null,
                    releaseNotes: pkgMetadata["ReleaseNotes"] as String,
                    repository: repository.Name,
                    repositorySourceLocation: repository.Uri.ToString(),
                    tags: tagsEntry,
                    type: ResourceType.Script,
                    updatedDate: null,
                    version: version);

                return true;
            }
            catch(Exception ex)
            {
                errorMsg = string.Format(
                    CultureInfo.InvariantCulture,
                    @"TryConvertFromHashtableForPs1: Could not find expected information from module manifest file hashtable with error: {0}",
                    ex.Message);

                return false;
            }
        }

        public static bool TryConvertFromHashtableForNuspec(
            Hashtable pkgMetadata,
            out PSResourceInfo psGetInfo,
            out string errorMsg,
            PSRepositoryInfo repository)
        {
            errorMsg = String.Empty;
            psGetInfo = null;

            try
            {
                string tagsEntry = pkgMetadata["tags"] as string;
                string[] tags = tagsEntry.Split(new char[] { ' ' });
                var typeInfo = ParseHttpMetadataType(tags, out ArrayList commandNames, out ArrayList cmdletNames, out ArrayList dscResourceNames);
                var resourceHashtable = new Hashtable {
                    { nameof(PSResourceInfo.Includes.Command), new PSObject(commandNames) },
                    { nameof(PSResourceInfo.Includes.Cmdlet), new PSObject(cmdletNames) },
                    { nameof(PSResourceInfo.Includes.DscResource), new PSObject(dscResourceNames) }
                };

                var additionalMetadataHashtable = new Dictionary<string, string>();
                string versionEntry = pkgMetadata["version"] as string;
                NuGetVersion.TryParse(versionEntry, out NuGetVersion nugetVersion);
                bool isPrerelease = nugetVersion.IsPrerelease;
                Version version = nugetVersion.Version;
                string prereleaseLabel = isPrerelease ? nugetVersion.ToNormalizedString().Split(new char[] { '-' })[1] : String.Empty;

                additionalMetadataHashtable.Add("NormalizedVersion", nugetVersion.ToNormalizedString());

                Uri.TryCreate((string)pkgMetadata["licenseUrl"], UriKind.Absolute, out Uri licenseUri);
                Uri.TryCreate((string)pkgMetadata["projectUrl"], UriKind.Absolute, out Uri projectUri);
                Uri.TryCreate((string)pkgMetadata["iconUrl"], UriKind.Absolute, out Uri iconUri);

                var includes = new ResourceIncludes(resourceHashtable);

                psGetInfo = new PSResourceInfo(
                    additionalMetadata: additionalMetadataHashtable,
                    author: pkgMetadata["authors"] as String,
                    companyName: String.Empty,
                    copyright: pkgMetadata["copyright"] as String,
                    dependencies: new Dependency[] { },
                    description: pkgMetadata["description"] as String,
                    iconUri: iconUri,
                    includes: includes,
                    installedDate: null,
                    installedLocation: null,
                    isPrerelease: isPrerelease,
                    licenseUri: licenseUri,
                    name: pkgMetadata["id"] as String,
                    powershellGetFormatVersion: null,   
                    prerelease: prereleaseLabel,
                    projectUri: projectUri,
                    publishedDate: null,
                    releaseNotes: String.Empty,
                    repository: repository.Name,
                    repositorySourceLocation: repository.Uri.ToString(),
                    tags: tags,
                    type: ResourceType.Module,
                    updatedDate: null,
                    version: version);

                return true;
            }
            catch(Exception ex)
            {
                errorMsg = string.Format(
                    CultureInfo.InvariantCulture,
                    @"TryConvertFromHashtableForPsd1: Could not find expected information from module manifest file hashtable with error: {0}",
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

        #endregion

        #region Parse Metadata private static methods

        private static Version ParseHttpVersion(string versionString, out string prereleaseLabel)
        {
            prereleaseLabel = String.Empty;

            if (!String.IsNullOrEmpty(versionString))
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
                        prereleaseLabel = versionStringParsed[1];
                    }
                }

                // at this point, version is normalized (i.e either "1.2.0" (if part of prerelease) or "1.2.0.0" otherwise)
                // parse the pkgVersion parsed out above into a System.Version object
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

            // version could not be parsed as string, it was written to XML file as a System.Version object
            // V3 code briefly did so, I believe so we provide support for it
            return new System.Version();
        }

        internal static Uri ParseHttpUrl(string uriString)
        {
            Uri parsedUri;
            Uri.TryCreate(uriString, UriKind.Absolute, out parsedUri);
            
            return parsedUri;
        }

        internal static DateTime? ParseHttpDateTime(string publishedString)
        {
            DateTime.TryParse(publishedString, out DateTime parsedDateTime);
            return parsedDateTime;
        }

        internal static Dependency[] ParseHttpDependencies(string dependencyString)
        {
            /*
            Az.Profile:[0.1.0, ):|Az.Aks:[0.1.0, ):|Az.AnalysisServices:[0.1.0, ):
            Post 1st Split: 
            ["Az.Profile:[0.1.0, ):", "Az.Aks:[0.1.0, ):", "Az.AnalysisServices:[0.1.0, ):"]
            */
            string[] dependencies = dependencyString.Split(new char[]{'|'}, StringSplitOptions.RemoveEmptyEntries);

            List<Dependency> dependencyList = new List<Dependency>();
            foreach (string dependency in dependencies)
            {
                /*
                The Element: "Az.Profile:[0.1.0, ):"
                Post 2nd Split: ["Az.Profile", "[0.1.0, )"]
                */
                string[] dependencyParts = dependency.Split(new char[]{':'}, StringSplitOptions.RemoveEmptyEntries);

                VersionRange dependencyVersion;
                if (dependencyParts.Length == 1)
                {
                    dependencyVersion = VersionRange.All;
                }
                else 
                {
                    if (!Utils.TryParseVersionOrVersionRange(dependencyParts[1], out dependencyVersion))
                    {
                        dependencyVersion = VersionRange.All;
                    }
                }

                dependencyList.Add(new Dependency(dependencyParts[0], dependencyVersion));
            }
            
            return dependencyList.ToArray();
        }

        internal static List<Dependency> ParseContainerRegistryDependencies(JsonElement requiredModulesElement, out string errorMsg)
        {
            errorMsg = string.Empty;
            List<Dependency> pkgDeps = new List<Dependency>();
            if (requiredModulesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var dependency in requiredModulesElement.EnumerateArray())
                {
                    if (dependency.ValueKind == JsonValueKind.String)
                    {
                        // Dependency name with no specified version
                        pkgDeps.Add(new Dependency(dependency.GetString(), VersionRange.All));
                    }
                    else if (dependency.ValueKind == JsonValueKind.Object)
                    {
                        // Dependency hashtable
                        string depName = string.Empty;
                        VersionRange depVersionRange = VersionRange.All;
                        if (dependency.TryGetProperty("ModuleName", out JsonElement depNameElement))
                        {
                            depName = depNameElement.ToString();
                        }

                        if (dependency.TryGetProperty("ModuleVersion", out JsonElement depModuleVersionElement))
                        {
                            // New-ScriptFileInfo will add "RequiredVersion" value as "null" if nothing is explicitly passed in
                            if (!NuGetVersion.TryParse(depModuleVersionElement.ToString(), out NuGetVersion depNuGetVersion))
                            {
                                errorMsg = string.Format("Error parsing 'ModuleVersion' property from 'RequiredModules' in metadata.");
                                return pkgDeps;
                            }

                            depVersionRange = new VersionRange(
                                            minVersion: depNuGetVersion,
                                            includeMinVersion: true);
                        }
                        else if (dependency.TryGetProperty("RequiredVersion", out JsonElement depRequiredVersionElement))
                        {
                            // New-ScriptFileInfo will add "RequiredVersion" value as "null" if nothing is explicitly passed in,
                            // Which gets translated to an empty string.
                            // In this case, we just want the VersionRange to be VersionRange.All
                            if (!string.Equals(depModuleVersionElement.ToString(), string.Empty))
                            {
                                if (!NuGetVersion.TryParse(depRequiredVersionElement.ToString(), out NuGetVersion depNuGetVersion))
                                {
                                    errorMsg = string.Format("Error parsing 'RequiredVersion' property from 'RequiredModules' in metadata.");
                                    return pkgDeps;
                                }

                                depVersionRange = new VersionRange(
                                                minVersion: depNuGetVersion,
                                                includeMinVersion: true,
                                                maxVersion: depNuGetVersion,
                                                includeMaxVersion: true);
                            }
                        }

                        pkgDeps.Add(new Dependency(depName, depVersionRange));
                    }
                }
            }

            return pkgDeps;
        }

        private static ResourceType ParseHttpMetadataType(
            string[] tags,
            out ArrayList commandNames,
            out ArrayList cmdletNames,
            out ArrayList dscResourceNames)
        {
            // possible type combinations:
            // M, C
            // M, D
            // M
            // S

            commandNames = new ArrayList();
            cmdletNames = new ArrayList();
            dscResourceNames = new ArrayList();

            ResourceType pkgType = ResourceType.Module;
            foreach (string tag in tags)
            {
                if(String.Equals(tag, "PSScript", StringComparison.InvariantCultureIgnoreCase))
                {
                    // clear default Module tag, because a Script resource cannot be a Module resource also
                    pkgType = ResourceType.Script;
                    pkgType &= ~ResourceType.Module;
                }

                if (tag.StartsWith("PSCommand_", StringComparison.InvariantCultureIgnoreCase))
                {
                    commandNames.Add(tag.Split('_')[1]);
                }

                if (tag.StartsWith("PSCmdlet_", StringComparison.InvariantCultureIgnoreCase))
                {
                    cmdletNames.Add(tag.Split('_')[1]);
                }

                if (tag.StartsWith("PSDscResource_", StringComparison.InvariantCultureIgnoreCase))
                {
                    dscResourceNames.Add(tag.Split('_')[1]);
                }
            }

            return pkgType;
        }

        private static ResourceType ParseHttpMetadataTypeForLocalRepo(
            Hashtable pkgMetadata,
            out ArrayList commandNames,
            out ArrayList cmdletNames,
            out ArrayList dscResourceNames)
        {
            cmdletNames = new ArrayList();
            var cmdletNamesObj = pkgMetadata["CmdletsToExport"] as object[];
            if (cmdletNamesObj != null)
            {
                foreach (var cmdlet in cmdletNamesObj)
                {
                    cmdletNames.Add(cmdlet as string);
                }
            }
            // Because there is no "CommandsToExport" propertly in a module manifest, we use CmdletsToExport to find command names
            commandNames = cmdletNames;

            dscResourceNames = new ArrayList();
            var dscResourceNamesObj = pkgMetadata["DscResourceToExport"] as object[];
            if (dscResourceNamesObj != null)
            {
                foreach (var dsc in dscResourceNamesObj)
                {
                    dscResourceNames.Add(dsc as string);
                }
            }

            ResourceType pkgType = ResourceType.Module;
            var tags = pkgMetadata["Tags"] as string[];
            foreach (string tag in tags)
            {
                // Local repository packages can either be PSResources (module or script) or non-PSResources (i.e downloaded from NuGetGallery)
                // but we want non-script resources to be considered module for better/fuller discovery.
                if (String.Equals(tag, "PSScript", StringComparison.InvariantCultureIgnoreCase))
                {
                    // clear default None tag
                    pkgType = ResourceType.Script;
                    pkgType &= ~ResourceType.Module;
                }

                if (tag.StartsWith("PSCommand_", StringComparison.InvariantCultureIgnoreCase))
                {
                    commandNames.Add(tag.Split('_')[1]);
                }

                if (tag.StartsWith("PSCmdlet_", StringComparison.InvariantCultureIgnoreCase))
                {
                    cmdletNames.Add(tag.Split('_')[1]);
                }

                if (tag.StartsWith("PSDscResource_", StringComparison.InvariantCultureIgnoreCase))
                {
                    dscResourceNames.Add(tag.Split('_')[1]);
                }
            }

            return pkgType;
        }

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
            psObject.Properties.Add(new PSNoteProperty(nameof(AdditionalMetadata), additionalMetadata));
            psObject.Properties.Add(new PSNoteProperty(nameof(InstalledLocation), InstalledLocation));

            return psObject;
        }

        private static Dependency[] GetDependenciesForPs1(ModuleSpecification[] requiredModules)
        {
            List<Dependency> deps = new List<Dependency>();
            if (requiredModules == null)
            {
                return deps.ToArray();
            }

            foreach(ModuleSpecification depModule in requiredModules)
            {
                // ModuleSpecification has Version, RequiredVersion, MaximumVersion
                string depName = depModule.Name;
                VersionRange depVersionRange = VersionRange.All;

                if (depModule.RequiredVersion != null)
                {
                    Utils.TryParseVersionOrVersionRange(depModule.RequiredVersion.ToString(), out depVersionRange);
                }
                else if (depModule.MaximumVersion != null && depModule.Version != null)
                {
                    NuGetVersion.TryParse(depModule.Version.ToString(), out NuGetVersion minVersion);
                    NuGetVersion.TryParse(depModule.MaximumVersion.ToString(), out NuGetVersion maxVersion);
                    depVersionRange = new VersionRange(
                        minVersion: minVersion,
                        includeMinVersion: true,
                        maxVersion: maxVersion,
                        includeMaxVersion: true);
                }
                else if (depModule.Version != null)
                {
                    NuGetVersion.TryParse(depModule.Version.ToString(), out NuGetVersion minVersion);
                    depVersionRange = new VersionRange(
                        minVersion: minVersion,
                        includeMinVersion: true,
                        maxVersion: null,
                        includeMaxVersion: true);
                }
                else if (depModule.MaximumVersion != null)
                {
                    NuGetVersion.TryParse(depModule.MaximumVersion.ToString(), out NuGetVersion maxVersion);
                    depVersionRange = new VersionRange(
                        minVersion: null,
                        includeMinVersion: true,
                        maxVersion: maxVersion,
                        includeMaxVersion: true);
                }

                deps.Add(new Dependency(depName, depVersionRange));
            }

            return deps.ToArray();
        }

        private static Dependency[] GetDependenciesForPsd1(Hashtable[] requiredModules)
        {
            List<Dependency> deps = new List<Dependency>();
            foreach(Hashtable depModule in requiredModules)
            {

                VersionRange depVersionRange = VersionRange.All;
                if (!depModule.ContainsKey("ModuleName"))
                {
                    continue;
                }

                String depName = (string) depModule["ModuleName"];
                if (depModule.ContainsKey("RequiredVersion"))
                {
                    // = 2.5.0
                    Utils.TryParseVersionOrVersionRange((string) depModule["RequiredVersion"], out depVersionRange);
                }
                else if (depModule.ContainsKey("ModuleVersion") || depModule.ContainsKey("MaximumVersion"))
                {
                    if (depModule.ContainsKey("ModuleVersion") && depModule.ContainsKey("MaximumVersion"))
                    {
                        NuGetVersion.TryParse((string) depModule["ModuleVersion"], out NuGetVersion minVersion);
                        NuGetVersion.TryParse((string) depModule["MaximumVersion"], out NuGetVersion maxVersion);
                        depVersionRange = new VersionRange(
                            minVersion: minVersion,
                            includeMinVersion: true,
                            maxVersion: maxVersion,
                            includeMaxVersion: true);
                    }
                    else if (depModule.ContainsKey("ModuleVersion"))
                    {
                        NuGetVersion.TryParse((string) depModule["ModuleVersion"], out NuGetVersion minVersion);
                        depVersionRange = new VersionRange(
                            minVersion: minVersion,
                            includeMinVersion: true,
                            maxVersion: null,
                            includeMaxVersion: true);
                    }
                    else
                    {
                        // depModule has "MaximumVersion" key
                        NuGetVersion.TryParse((string) depModule["MaximumVersion"], out NuGetVersion maxVersion);
                        depVersionRange = new VersionRange(
                            minVersion: null,
                            includeMinVersion: true,
                            maxVersion: maxVersion,
                            includeMaxVersion: true);
                    }

                }

                deps.Add(new Dependency(depName, depVersionRange));
            }

            return deps.ToArray();
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
