using System.Reflection.Emit;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NuGet.Protocol.Core.Types;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    public enum ResourceType
    {
        Module,
        Script
    }

    public class PSResourceInfo
    {
        #region Properties
        public string Name { get; set; }
        public System.Version Version { get; set; }
        public ResourceType Type { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string CompanyName { get; set; }
        public string Copyright { get; set; }
        public DateTime? PublishedDate { get; set; }
        public DateTime? InstalledDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string LicenseUri { get; set; }
        public string ProjectUri { get; set; }
        public string IconUri { get; set; }
        public string PowerShellGetFormatVersion { get; set; }
        public string ReleaseNotes { get; set; }
        public string Repository { get; set; }
        public string IsPrerelease { get; set; }
        public string[] Tags { get; set; }
        public Dictionary<string, VersionInfo> Dependencies { get; set; }
        internal string AdditionalMetadata { get; set; }
        public ArrayList Commands { get; set; }
        public ArrayList Cmdlets { get; set; }
        public ArrayList DscResources { get; set; }
        public ArrayList Functions { get; set; }
        public string InstalledLocation { get; set; }

        #endregion

        public struct VersionInfo
        {
            public VersionInfo(VersionType versionType, System.Version versionNum)
            {
                this.versionType = versionType;
                this.versionNum = versionNum;
            }

            public VersionType versionType { get; }
            public System.Version versionNum { get; }

            public override string ToString() => $"{versionType}: {versionNum}";

            public enum VersionType
            {
                Unknown,
                MinimumVersion,
                RequiredVersion,
                MaximumVersion
            }
        }

        #region Public static methods
        public bool TryParse(
            IPackageSearchMetadata pkgToParse,
            out string errorMsg)
        {
            errorMsg = String.Empty;
            try
            {
                Name = ParseName(pkgToParse); //notvneeded
                Version = ParseVersion(pkgToParse);
                Type = ParseType(pkgToParse);
                Description = ParseDescription(pkgToParse); // not needed
                Author = ParseAuthor(pkgToParse); //not needed
                LicenseUri = ParseLicenseUri(pkgToParse);
                ProjectUri = ParseProjectUri(pkgToParse);
                IconUri = ParseIconUri(pkgToParse);
                CompanyName = ParseCompany(pkgToParse);
                Copyright = ParseCopyright(pkgToParse);
                PublishedDate = ParsePublishedDate(pkgToParse);
                InstalledDate = ParseInstalledDate(pkgToParse);
                UpdatedDate = ParseUpdateDate(pkgToParse);
                PowerShellGetFormatVersion = ParsePowerShellGetFormatVersion(pkgToParse);
                ReleaseNotes = ParseReleaseNotes(pkgToParse);
                Repository = ParseRepository(pkgToParse);
                Tags = ParseTags(pkgToParse);
                IsPrerelease = ParsePrelease(pkgToParse);
                Dependencies = ParseDependencies(pkgToParse);
                Commands = ParseCommands(pkgToParse);
                Cmdlets = ParseCmdlets(pkgToParse);
                DscResources = ParseDscResources(pkgToParse);
                Functions = ParseFunctions(pkgToParse);
                InstalledLocation = ParseInstalledLocation(pkgToParse);
                return true;
            }
            catch (Exception ex)
            {
                errorMsg = string.Format(
                    CultureInfo.InvariantCulture,
                    @"TryReadPSGetInfo: Cannot read the PowerShellGet information file with error: {0}",
                    ex.Message);

                return false;
            }
        }

        #endregion

        #region Private static methods

        #endregion

        #region Private methods
        public string ParseName(IPackageSearchMetadata pkg)
        {
            return pkg.Identity.Id;
        }

        public Version ParseVersion(IPackageSearchMetadata pkg)
        {
            return pkg.Identity.Version.Version;
        }

        public ResourceType ParseType(IPackageSearchMetadata pkg)
        {
            string[] tags = ParseTags(pkg);
            foreach (string tag in tags)
            {
                if(tag.Equals("PS" + ResourceType.Script) || tag.Equals(ResourceType.Script))
                {
                    return ResourceType.Script;
                }
                else if(tag.Equals("PS" + ResourceType.Module) || tag.Equals(ResourceType.Module))
                {
                    return ResourceType.Module;
                }
            }
            // todo: fix this assumption! see how PSGallery UI is detecting type
            return ResourceType.Module;
        }

        public string ParseDescription(IPackageSearchMetadata pkg)
        {
            return pkg.Description;
        }

        public string ParseAuthor(IPackageSearchMetadata pkg)
        {
            return pkg.Authors;
        }

        // todo: return proper metadata
        public string ParseCopyright(IPackageSearchMetadata pkg)
        {
            return String.Empty;
        }

        // todo: return proper metadata
        public string ParseCompany(IPackageSearchMetadata pkg)
        {
            return String.Empty;
        }

        // todo: return proper metadata
        public DateTime? ParsePublishedDate(IPackageSearchMetadata pkg)
        {
            return null;
        }

        // todo: return proper metadata
        public DateTime? ParseInstalledDate(IPackageSearchMetadata pkg)
        {
            return null;
        }

        // todo: return proper metadata
        public DateTime? ParseUpdateDate(IPackageSearchMetadata pkg)
        {
            return null;
        }

        // todo: return proper metadata
        public string ParseLicenseUri(IPackageSearchMetadata pkg)
        {
            if(pkg.LicenseUrl != null)
            {
                return pkg.LicenseUrl.ToString();
            }
            return String.Empty;
        }

        // todo: return proper metadata
        public string ParseProjectUri(IPackageSearchMetadata pkg)
        {
            if (pkg.ProjectUrl != null)
            {
                return pkg.ProjectUrl.ToString();
            }
            return String.Empty;
        }

        // todo: return proper metadata
        public string ParseIconUri(IPackageSearchMetadata pkg)
        {
            if (pkg.IconUrl != null)
            {
                return pkg.IconUrl.ToString();
            }
            return String.Empty;
        }

        // todo: return proper metadata
        public string ParsePowerShellGetFormatVersion(IPackageSearchMetadata pkg)
        {
            return String.Empty;
        }

        // todo: return proper metadata
        public string ParseReleaseNotes(IPackageSearchMetadata pkg)
        {
            return String.Empty;
        }

        // todo: return proper metadata
        public string ParseRepository(IPackageSearchMetadata pkg)
        {
            return String.Empty;
        }

        // todo: return proper metadata
        public string ParsePrelease(IPackageSearchMetadata pkg)
        {
            return String.Empty;
        }

        // todo: return proper metadata
        public string[] ParseTags(IPackageSearchMetadata pkg)
        {
            char[] delimiter = new char[] { ' ', ',' };
            string[] tags = pkg.Tags.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
            return tags;
        }

        // todo: return proper metadata
        public Dictionary<string, VersionInfo> ParseDependencies(IPackageSearchMetadata pkg)
        {
            return new Dictionary<string, VersionInfo>();
        }

        // todo: return proper metadata
        public ArrayList ParseCommands(IPackageSearchMetadata pkg)
        {
            return new ArrayList();
        }

        // todo: return proper metadata
        public ArrayList ParseCmdlets(IPackageSearchMetadata pkg)
        {
            return new ArrayList();
        }

        // todo: return proper metadata
        public ArrayList ParseDscResources(IPackageSearchMetadata pkg)
        {
            return new ArrayList();
        }

        // todo: return proper metadata
        public ArrayList ParseFunctions(IPackageSearchMetadata pkg)
        {
            return new ArrayList();
        }

        // todo: return proper metadata
        public string ParseInstalledLocation(IPackageSearchMetadata pkg)
        {
            return String.Empty;
        }

        #endregion
    }
}