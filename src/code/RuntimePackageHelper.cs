// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Microsoft.PowerShell.PSResourceGet.UtilClasses
{
    /// <summary>
    /// Filters runtime assets stored in root-level RID folders used by PowerShell packages.
    /// </summary>
    internal static class RuntimePackageHelper
    {
        private const char ZipPathSeparator = '/';

        private static readonly string[] s_knownOsPrefixes =
        {
            "win",
            "linux",
            "osx",
            "unix",
            "maccatalyst",
            "browser",
        };

        private static readonly string[] s_knownArchitectures =
        {
            "loongarch64",
            "ppc64le",
            "mips64",
            "s390x",
            "arm64",
            "armel",
            "wasm",
            "arm",
            "x64",
            "x86",
        };

        private static readonly string[] s_genericRids =
        {
            "any",
            "win",
            "linux",
            "osx",
            "unix",
        };

        /// <summary>
        /// Determines whether a folder name resembles a supported Runtime Identifier.
        /// </summary>
        public static bool IsRidFolder(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
            {
                return false;
            }

            foreach (string genericRid in s_genericRids)
            {
                if (string.Equals(folderName, genericRid, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (!folderName.Contains("-"))
            {
                return false;
            }

            bool startsWithKnownOs = false;
            foreach (string prefix in s_knownOsPrefixes)
            {
                if (folderName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    folderName.Length > prefix.Length)
                {
                    char nextCharacter = folderName[prefix.Length];
                    if (nextCharacter == '-' || nextCharacter == '.' || char.IsDigit(nextCharacter))
                    {
                        startsWithKnownOs = true;
                        break;
                    }
                }
            }

            if (!startsWithKnownOs)
            {
                return false;
            }

            string[] parts = folderName.Split('-');
            string lastPart = parts[parts.Length - 1];
            foreach (string architecture in s_knownArchitectures)
            {
                if (string.Equals(lastPart, architecture, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether an archive entry is under a root-level RID folder.
        /// Standard NuGet runtimes/{rid}/ entries are intentionally not included.
        /// </summary>
        public static bool IsRuntimeAssetEntry(string entryFullName)
        {
            return !string.IsNullOrEmpty(GetRidFromRuntimeAssetEntry(entryFullName));
        }

        /// <summary>
        /// Gets the RID from a root-level RID folder entry.
        /// </summary>
        public static string GetRidFromRuntimeAssetEntry(string entryFullName)
        {
            if (string.IsNullOrEmpty(entryFullName))
            {
                return null;
            }

            string normalizedPath = entryFullName.Replace('\\', ZipPathSeparator);
            string[] parts = normalizedPath.Split(ZipPathSeparator);

            return parts.Length >= 2 && IsRidFolder(parts[0]) ? parts[0] : null;
        }

        /// <summary>
        /// Determines whether an archive entry should be extracted for the current process RID.
        /// </summary>
        public static bool ShouldIncludeEntry(string entryFullName)
        {
            string entryRid = GetRidFromRuntimeAssetEntry(entryFullName);
            return string.IsNullOrEmpty(entryRid) || RuntimeIdentifierHelper.IsCompatibleRid(entryRid);
        }

        /// <summary>
        /// Determines whether an archive entry should be extracted for an explicit target RID.
        /// </summary>
        public static bool ShouldIncludeEntry(string entryFullName, string targetRid)
        {
            string entryRid = GetRidFromRuntimeAssetEntry(entryFullName);
            return string.IsNullOrEmpty(entryRid) ||
                RuntimeIdentifierHelper.IsCompatibleRid(entryRid, targetRid);
        }

        /// <summary>
        /// Gets the unique root-level RIDs present in an archive.
        /// </summary>
        public static IReadOnlyList<string> GetAvailableRidsFromArchive(ZipArchive archive)
        {
            if (archive == null)
            {
                throw new ArgumentNullException(nameof(archive));
            }

            var rids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string rid = GetRidFromRuntimeAssetEntry(entry.FullName);
                if (!string.IsNullOrEmpty(rid))
                {
                    rids.Add(rid);
                }
            }

            return rids.OrderBy(rid => rid, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Gets the unique root-level RIDs present in a zip file.
        /// </summary>
        public static IReadOnlyList<string> GetAvailableRidsFromZipFile(string zipPath)
        {
            if (string.IsNullOrEmpty(zipPath))
            {
                throw new ArgumentException("Zip path cannot be null or empty.", nameof(zipPath));
            }

            if (!File.Exists(zipPath))
            {
                throw new FileNotFoundException("Zip file not found.", zipPath);
            }

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                return GetAvailableRidsFromArchive(archive);
            }
        }
    }
}
