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

        /// <summary>
        /// Gets statistics about the runtime assets in a package, showing what would be included vs excluded.
        /// </summary>
        /// <param name="archive">The zip archive to analyze.</param>
        /// <returns>A RuntimeFilterStats object with filtering statistics.</returns>
        public static RuntimeFilterStats GetFilteringStatistics(ZipArchive archive)
        {
            if (archive == null)
            {
                throw new ArgumentNullException(nameof(archive));
            }

            var stats = new RuntimeFilterStats
            {
                CurrentRid = RuntimeIdentifierHelper.GetCurrentRuntimeIdentifier(),
                CompatibleRids = RuntimeIdentifierHelper.GetCompatibleRuntimeIdentifiers().ToList()
            };

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.Length == 0)
                {
                    continue; // Skip directories
                }

                if (IsRuntimesEntry(entry.FullName))
                {
                    string rid = GetRidFromRuntimesEntry(entry.FullName);
                    
                    if (ShouldIncludeEntry(entry.FullName))
                    {
                        stats.IncludedRuntimeEntriesCount++;
                        stats.IncludedRuntimeEntriesSize += entry.Length;
                        
                        if (!string.IsNullOrEmpty(rid))
                        {
                            if (!stats.IncludedRids.Contains(rid))
                            {
                                stats.IncludedRids.Add(rid);
                            }
                        }
                    }
                    else
                    {
                        stats.ExcludedRuntimeEntriesCount++;
                        stats.ExcludedRuntimeEntriesSize += entry.Length;
                        
                        if (!string.IsNullOrEmpty(rid))
                        {
                            if (!stats.ExcludedRids.Contains(rid))
                            {
                                stats.ExcludedRids.Add(rid);
                            }
                        }
                    }
                }
                else
                {
                    stats.NonRuntimeEntriesCount++;
                    stats.NonRuntimeEntriesSize += entry.Length;
                }
            }

            return stats;
        }

        /// <summary>
        /// Filters zip entries based on the current platform's RID.
        /// Returns only entries that should be extracted for this platform.
        /// </summary>
        /// <param name="entries">The collection of zip entries to filter.</param>
        /// <returns>Filtered entries that should be extracted.</returns>
        public static IEnumerable<ZipArchiveEntry> FilterEntriesForCurrentPlatform(IEnumerable<ZipArchiveEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            foreach (ZipArchiveEntry entry in entries)
            {
                if (ShouldIncludeEntry(entry.FullName))
                {
                    yield return entry;
                }
            }
        }

        /// <summary>
        /// Checks if a package has any platform-specific runtime assets.
        /// </summary>
        /// <param name="archive">The zip archive to check.</param>
        /// <returns>True if the package contains runtime assets; otherwise, false.</returns>
        public static bool HasRuntimeAssets(ZipArchive archive)
        {
            if (archive == null)
            {
                return false;
            }

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (IsRuntimesEntry(entry.FullName))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }

    /// <summary>
    /// Statistics about runtime asset filtering for a package.
    /// </summary>
    internal class RuntimeFilterStats
    {
        /// <summary>
        /// The current platform's runtime identifier.
        /// </summary>
        public string CurrentRid { get; set; }

        /// <summary>
        /// List of RIDs compatible with the current platform.
        /// </summary>
        public List<string> CompatibleRids { get; set; } = new List<string>();

        /// <summary>
        /// RIDs that will be included in the installation.
        /// </summary>
        public List<string> IncludedRids { get; set; } = new List<string>();

        /// <summary>
        /// RIDs that will be excluded from the installation.
        /// </summary>
        public List<string> ExcludedRids { get; set; } = new List<string>();

        /// <summary>
        /// Number of non-runtime entries in the package.
        /// </summary>
        public int NonRuntimeEntriesCount { get; set; }

        /// <summary>
        /// Total size of non-runtime entries in bytes.
        /// </summary>
        public long NonRuntimeEntriesSize { get; set; }

        /// <summary>
        /// Number of runtime entries that will be included.
        /// </summary>
        public int IncludedRuntimeEntriesCount { get; set; }

        /// <summary>
        /// Total size of included runtime entries in bytes.
        /// </summary>
        public long IncludedRuntimeEntriesSize { get; set; }

        /// <summary>
        /// Number of runtime entries that will be excluded.
        /// </summary>
        public int ExcludedRuntimeEntriesCount { get; set; }

        /// <summary>
        /// Total size of excluded runtime entries in bytes.
        /// </summary>
        public long ExcludedRuntimeEntriesSize { get; set; }

        /// <summary>
        /// Total size of all entries in the package in bytes.
        /// </summary>
        public long TotalPackageSize => NonRuntimeEntriesSize + IncludedRuntimeEntriesSize + ExcludedRuntimeEntriesSize;

        /// <summary>
        /// Total size that will be installed in bytes.
        /// </summary>
        public long InstalledSize => NonRuntimeEntriesSize + IncludedRuntimeEntriesSize;

        /// <summary>
        /// Total size that will be saved by filtering in bytes.
        /// </summary>
        public long SavedSize => ExcludedRuntimeEntriesSize;

        /// <summary>
        /// Percentage of space saved by filtering.
        /// </summary>
        public double SavedPercentage => TotalPackageSize > 0 
            ? (double)SavedSize / TotalPackageSize * 100 
            : 0;

        /// <summary>
        /// Returns a human-readable summary of the filtering statistics.
        /// </summary>
        public override string ToString()
        {
            return $"Current RID: {CurrentRid}\n" +
                   $"Compatible RIDs: {string.Join(", ", CompatibleRids)}\n" +
                   $"Included RIDs: {string.Join(", ", IncludedRids)}\n" +
                   $"Excluded RIDs: {string.Join(", ", ExcludedRids)}\n" +
                   $"Non-runtime files: {NonRuntimeEntriesCount} ({FormatBytes(NonRuntimeEntriesSize)})\n" +
                   $"Included runtime files: {IncludedRuntimeEntriesCount} ({FormatBytes(IncludedRuntimeEntriesSize)})\n" +
                   $"Excluded runtime files: {ExcludedRuntimeEntriesCount} ({FormatBytes(ExcludedRuntimeEntriesSize)})\n" +
                   $"Total package size: {FormatBytes(TotalPackageSize)}\n" +
                   $"Installed size: {FormatBytes(InstalledSize)}\n" +
                   $"Space saved: {FormatBytes(SavedSize)} ({SavedPercentage:F1}%)";
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int suffixIndex = 0;
            double size = bytes;

            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            return $"{size:F2} {suffixes[suffixIndex]}";
        }
    }
}
