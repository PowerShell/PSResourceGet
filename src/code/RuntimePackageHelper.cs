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
    /// </summary>
    internal static class RuntimePackageHelper
    {
        #region Constants

        /// <summary>
        /// The name of the runtimes folder in NuGet packages.
        /// </summary>
        private const string RuntimesFolderName = "runtimes";

        /// <summary>
        /// Path separator used in zip archives.
        /// </summary>
        private const char ZipPathSeparator = '/';

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks if a zip entry path is within the runtimes folder.
        /// </summary>
        /// <param name="entryFullName">The full path of the zip entry.</param>
        /// <returns>True if the entry is in the runtimes folder; otherwise, false.</returns>
        public static bool IsRuntimesEntry(string entryFullName)
        {
            if (string.IsNullOrEmpty(entryFullName))
            {
                return false;
            }

            // Normalize path separators for comparison
            string normalizedPath = entryFullName.Replace('\\', ZipPathSeparator);
            
            return normalizedPath.StartsWith(RuntimesFolderName + ZipPathSeparator, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Extracts the RID from a runtimes folder entry path.
        /// </summary>
        /// <param name="entryFullName">The full path of the zip entry (e.g., "runtimes/win-x64/native/file.dll").</param>
        /// <returns>The RID (e.g., "win-x64"), or null if not a runtimes entry.</returns>
        public static string GetRidFromRuntimesEntry(string entryFullName)
        {
            if (!IsRuntimesEntry(entryFullName))
            {
                return null;
            }

            // Normalize path separators
            string normalizedPath = entryFullName.Replace('\\', ZipPathSeparator);
            
            // Path format: runtimes/{rid}/...
            string[] parts = normalizedPath.Split(ZipPathSeparator);
            
            if (parts.Length >= 2)
            {
                return parts[1]; // The RID is the second segment
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
            // Non-runtimes entries are always included
            if (!IsRuntimesEntry(entryFullName))
            {
                return true;
            }

            string entryRid = GetRidFromRuntimesEntry(entryFullName);
            
            if (string.IsNullOrEmpty(entryRid))
            {
                // If we can't determine the RID, include the entry to be safe
                return true;
            }

            // Check if this RID is compatible with the current platform
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
            // Non-runtimes entries are always included
            if (!IsRuntimesEntry(entryFullName))
            {
                return true;
            }

            string entryRid = GetRidFromRuntimesEntry(entryFullName);
            
            if (string.IsNullOrEmpty(entryRid))
            {
                return true;
            }

            // Check if this RID is compatible with the specified target
            return RuntimeIdentifierHelper.IsCompatibleRid(entryRid, targetRid);
        }

        /// <summary>
        /// Gets a list of all unique RIDs present in a zip archive's runtimes folder.
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
        /// Gets a list of all unique RIDs present in a zip file's runtimes folder.
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
