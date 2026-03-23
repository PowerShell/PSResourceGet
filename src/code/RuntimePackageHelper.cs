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
    /// Helper class for parsing package runtime assets and filtering during extraction.
    /// Provides functionality to filter runtime-specific assets based on the current platform's RID.
    /// Detects root-level RID folders (e.g., win-x64/native.dll) used by PowerShell modules
    /// with platform-specific native dependencies.
    /// </summary>
    internal static class RuntimePackageHelper
    {
        #region Constants

        /// <summary>
        /// Path separator used in zip archives.
        /// </summary>
        private const char ZipPathSeparator = '/';

        /// <summary>
        /// Known OS prefixes used in .NET Runtime Identifiers.
        /// </summary>
        private static readonly string[] s_knownOsPrefixes = new[]
        {
            "win", "linux", "osx", "unix", "maccatalyst", "browser"
        };

        /// <summary>
        /// Known architectures used in .NET Runtime Identifiers.
        /// </summary>
        private static readonly string[] s_knownArchitectures = new[]
        {
            "loongarch64", "ppc64le", "mips64", "s390x", "arm64", "armel", "wasm", "arm", "x64", "x86"
        };

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks if a folder name looks like a .NET Runtime Identifier.
        /// Matches patterns like: win-x64, linux-arm64, osx-arm64, linux-musl-x64, etc.
        /// </summary>
        /// <param name="folderName">The folder name to check.</param>
        /// <returns>True if the folder name matches a RID pattern; otherwise, false.</returns>
        public static bool IsRidFolder(string folderName)
        {
            if (string.IsNullOrEmpty(folderName) || !folderName.Contains("-"))
            {
                return false;
            }

            // Must start with a known OS prefix
            bool startsWithKnownOs = false;
            foreach (string prefix in s_knownOsPrefixes)
            {
                if (folderName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    startsWithKnownOs = true;
                    break;
                }
            }

            if (!startsWithKnownOs)
            {
                return false;
            }

            // Must end with a known architecture
            string[] parts = folderName.Split('-');
            string lastPart = parts[parts.Length - 1];
            foreach (string arch in s_knownArchitectures)
            {
                if (string.Equals(lastPart, arch, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a zip entry path is under a root-level RID folder.
        /// Detects entries like win-x64/native.dll, linux-arm64/libfoo.so, etc.
        /// </summary>
        /// <param name="entryFullName">The full path of the zip entry.</param>
        /// <returns>True if the entry is under a RID folder; otherwise, false.</returns>
        public static bool IsRuntimesEntry(string entryFullName)
        {
            if (string.IsNullOrEmpty(entryFullName))
            {
                return false;
            }

            string normalizedPath = entryFullName.Replace('\\', ZipPathSeparator);
            string[] segments = normalizedPath.Split(ZipPathSeparator);

            // Pattern: {rid}/... (root-level RID folders like win-x64/native.dll)
            return segments.Length >= 2 && IsRidFolder(segments[0]);
        }

        /// <summary>
        /// Extracts the RID from a root-level RID folder entry path.
        /// </summary>
        /// <param name="entryFullName">The full path of the zip entry (e.g., "win-x64/native.dll").</param>
        /// <returns>The RID (e.g., "win-x64"), or null if not under a RID folder.</returns>
        public static string GetRidFromRuntimesEntry(string entryFullName)
        {
            if (string.IsNullOrEmpty(entryFullName))
            {
                return null;
            }

            string normalizedPath = entryFullName.Replace('\\', ZipPathSeparator);
            string[] parts = normalizedPath.Split(ZipPathSeparator);

            if (parts.Length >= 2 && IsRidFolder(parts[0]))
            {
                return parts[0];
            }

            return null;
        }

        /// <summary>
        /// Determines if a zip entry should be included based on the current platform's RID.
        /// </summary>
        /// <param name="entryFullName">The full path of the zip entry.</param>
        /// <returns>True if the entry should be included; otherwise, false.</returns>
        public static bool ShouldIncludeEntry(string entryFullName)
        {
            if (!IsRuntimesEntry(entryFullName))
            {
                return true;
            }

            string entryRid = GetRidFromRuntimesEntry(entryFullName);

            if (string.IsNullOrEmpty(entryRid))
            {
                return true;
            }

            return RuntimeIdentifierHelper.IsCompatibleRid(entryRid);
        }

        /// <summary>
        /// Determines if a zip entry should be included based on an explicit target RID.
        /// Used when the user specifies -RuntimeIdentifier for cross-platform deployment.
        /// </summary>
        /// <param name="entryFullName">The full path of the zip entry.</param>
        /// <param name="targetRid">The target RID to filter for.</param>
        /// <returns>True if the entry should be included; otherwise, false.</returns>
        public static bool ShouldIncludeEntry(string entryFullName, string targetRid)
        {
            if (!IsRuntimesEntry(entryFullName))
            {
                return true;
            }

            string entryRid = GetRidFromRuntimesEntry(entryFullName);

            if (string.IsNullOrEmpty(entryRid))
            {
                return true;
            }

            return RuntimeIdentifierHelper.IsCompatibleRid(entryRid, targetRid);
        }

        /// <summary>
        /// Gets a list of all unique RIDs present in a zip archive.
        /// Detects both runtimes/{rid}/ and root-level {rid}/ patterns.
        /// </summary>
        /// <param name="archive">The zip archive to scan.</param>
        /// <returns>A list of unique RIDs found in the archive.</returns>
        public static IReadOnlyList<string> GetAvailableRidsFromArchive(ZipArchive archive)
        {
            if (archive == null)
            {
                throw new ArgumentNullException(nameof(archive));
            }

            var rids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string rid = GetRidFromRuntimesEntry(entry.FullName);
                if (!string.IsNullOrEmpty(rid))
                {
                    rids.Add(rid);
                }
            }

            return rids.ToList();
        }

        /// <summary>
        /// Gets a list of all unique RIDs present in a zip file.
        /// </summary>
        /// <param name="zipPath">The path to the zip file.</param>
        /// <returns>A list of unique RIDs found in the archive.</returns>
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

        #endregion
    }
}
