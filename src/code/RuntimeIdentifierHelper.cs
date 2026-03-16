// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.PSResourceGet.UtilClasses
{
    /// <summary>
    /// Helper class for Runtime Identifier (RID) detection and compatibility.
    /// Used for platform-aware package installation to filter runtime-specific assets.
    /// </summary>
    internal static class RuntimeIdentifierHelper
    {
        #region Private Fields

        /// <summary>
        /// Cached current runtime identifier to avoid repeated detection.
        /// </summary>
        private static string s_currentRid = null;

        /// <summary>
        /// Cached compatible RIDs for the current platform.
        /// </summary>
        private static List<string> s_compatibleRids = null;

        /// <summary>
        /// Lock object for thread-safe initialization.
        /// </summary>
        private static readonly object s_lock = new object();

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the .NET Runtime Identifier (RID) for the current platform.
        /// </summary>
        /// <returns>
        /// A RID string like "win-x64", "linux-x64", "osx-arm64", etc.
        /// Follows the .NET RID catalog: https://learn.microsoft.com/en-us/dotnet/core/rid-catalog
        /// </returns>
        /// <example>
        /// Windows 64-bit: "win-x64"
        /// Windows 32-bit: "win-x86"
        /// Linux 64-bit: "linux-x64"
        /// Alpine Linux: "linux-musl-x64"
        /// macOS Intel: "osx-x64"
        /// macOS Apple Silicon: "osx-arm64"
        /// </example>
        public static string GetCurrentRuntimeIdentifier()
        {
            if (s_currentRid != null)
            {
                return s_currentRid;
            }

            lock (s_lock)
            {
                if (s_currentRid != null)
                {
                    return s_currentRid;
                }

                s_currentRid = DetectRuntimeIdentifier();
                return s_currentRid;
            }
        }

        /// <summary>
        /// Gets a list of compatible Runtime Identifiers for the current platform.
        /// RIDs follow an inheritance chain (e.g., win10-x64 -> win-x64 -> any).
        /// </summary>
        /// <returns>
        /// A list of RIDs that are compatible with the current platform, ordered from most specific to least specific.
        /// </returns>
        public static IReadOnlyList<string> GetCompatibleRuntimeIdentifiers()
        {
            if (s_compatibleRids != null)
            {
                return s_compatibleRids;
            }

            lock (s_lock)
            {
                if (s_compatibleRids != null)
                {
                    return s_compatibleRids;
                }

                s_compatibleRids = BuildCompatibleRidList(GetCurrentRuntimeIdentifier());
                return s_compatibleRids;
            }
        }

        /// <summary>
        /// Gets a list of compatible Runtime Identifiers for a given RID.
        /// </summary>
        /// <param name="primaryRid">The primary RID to get compatibility for.</param>
        /// <returns>
        /// A list of RIDs that are compatible with the specified RID, ordered from most specific to least specific.
        /// </returns>
        public static IReadOnlyList<string> GetCompatibleRuntimeIdentifiers(string primaryRid)
        {
            if (string.IsNullOrWhiteSpace(primaryRid))
            {
                throw new ArgumentException("Primary RID cannot be null or empty.", nameof(primaryRid));
            }

            return BuildCompatibleRidList(primaryRid);
        }

        /// <summary>
        /// Checks if a given RID is compatible with the current platform.
        /// A package RID is compatible if:
        /// 1. It's in our platform's compatibility chain (e.g., 'win' folder works on 'win-x64' machine), OR
        /// 2. Our platform is in the package RID's compatibility chain (e.g., 'win10-x64' folder works on 'win-x64' machine)
        /// </summary>
        /// <param name="rid">The RID to check.</param>
        /// <returns>True if the RID is compatible with the current platform; otherwise, false.</returns>
        public static bool IsCompatibleRid(string rid)
        {
            if (string.IsNullOrWhiteSpace(rid))
            {
                return false;
            }

            string currentRid = GetCurrentRuntimeIdentifier();
            
            // Check if the package RID is in our platform's compatibility chain
            // e.g., our platform is win-x64, and package has 'win' folder -> compatible
            var ourCompatibleRids = GetCompatibleRuntimeIdentifiers();
            foreach (var compatibleRid in ourCompatibleRids)
            {
                if (string.Equals(rid, compatibleRid, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Check if our platform is in the package RID's compatibility chain
            // e.g., our platform is win-x64, and package has 'win10-x64' folder -> compatible
            // because win10-x64's chain includes win-x64
            var packageRidCompatibles = BuildCompatibleRidList(rid);
            foreach (var compatibleRid in packageRidCompatibles)
            {
                if (string.Equals(currentRid, compatibleRid, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a given RID is compatible with a specified target RID (rather than the current platform).
        /// Used when the user explicitly specifies a -RuntimeIdentifier for cross-platform deployment scenarios.
        /// </summary>
        /// <param name="candidateRid">The RID from the package entry to check.</param>
        /// <param name="targetRid">The target RID to check compatibility against.</param>
        /// <returns>True if the candidate RID is compatible with the target; otherwise, false.</returns>
        public static bool IsCompatibleRid(string candidateRid, string targetRid)
        {
            if (string.IsNullOrWhiteSpace(candidateRid) || string.IsNullOrWhiteSpace(targetRid))
            {
                return false;
            }

            // Check if the candidate RID is in the target's compatibility chain
            var targetCompatibleRids = BuildCompatibleRidList(targetRid);
            foreach (var compatibleRid in targetCompatibleRids)
            {
                if (string.Equals(candidateRid, compatibleRid, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Check if the target is in the candidate RID's compatibility chain
            var candidateCompatibleRids = BuildCompatibleRidList(candidateRid);
            foreach (var compatibleRid in candidateCompatibleRids)
            {
                if (string.Equals(targetRid, compatibleRid, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a folder name in the runtimes directory should be included for the current platform.
        /// </summary>
        /// <param name="runtimeFolderName">The name of the folder in the runtimes directory.</param>
        /// <returns>True if the folder should be included; otherwise, false.</returns>
        public static bool ShouldIncludeRuntimeFolder(string runtimeFolderName)
        {
            return IsCompatibleRid(runtimeFolderName);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Detects the runtime identifier for the current platform.
        /// </summary>
        private static string DetectRuntimeIdentifier()
        {
            // Get architecture
            string arch = GetArchitectureString();

            // Detect OS and construct RID
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return $"win-{arch}";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Check for musl-based distros (Alpine, etc.)
                if (IsMuslBasedLinux())
                {
                    return $"linux-musl-{arch}";
                }
                return $"linux-{arch}";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return $"osx-{arch}";
            }
            else
            {
                // Fallback for unknown platforms
                return $"unix-{arch}";
            }
        }

        /// <summary>
        /// Gets the architecture string for the current process.
        /// </summary>
        private static string GetArchitectureString()
        {
            Architecture processArch = RuntimeInformation.ProcessArchitecture;

            return processArch switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm => "arm",
                Architecture.Arm64 => "arm64",
#if NET6_0_OR_GREATER
                Architecture.S390x => "s390x",
                Architecture.Ppc64le => "ppc64le",
#endif
#if NET7_0_OR_GREATER
                Architecture.LoongArch64 => "loongarch64",
#endif
#if NET8_0_OR_GREATER
                Architecture.Armv6 => "arm",
#endif
                _ => processArch.ToString().ToLowerInvariant()
            };
        }

        /// <summary>
        /// Checks if the current Linux system is musl-based (e.g., Alpine Linux).
        /// </summary>
        private static bool IsMuslBasedLinux()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return false;
            }

            try
            {
                // Check /etc/os-release for Alpine or musl indicators
                const string osReleasePath = "/etc/os-release";
                if (File.Exists(osReleasePath))
                {
                    string content = File.ReadAllText(osReleasePath);
                    // Alpine Linux specifically uses musl
                    if (content.IndexOf("alpine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        content.IndexOf("ID=alpine", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }

                // Alternative: Check if libc is musl by examining /lib/libc.musl-*.so
                // This is a more direct check but requires directory enumeration
                if (Directory.Exists("/lib"))
                {
                    string[] muslLibs = Directory.GetFiles("/lib", "libc.musl-*.so*");
                    if (muslLibs.Length > 0)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // If we can't determine, assume glibc (most common)
            }

            return false;
        }

        /// <summary>
        /// Builds a list of compatible RIDs for the given primary RID.
        /// RIDs follow an inheritance chain from most specific to least specific.
        /// </summary>
        /// <param name="primaryRid">The primary RID to build the compatibility list for.</param>
        /// <returns>A list of compatible RIDs.</returns>
        private static List<string> BuildCompatibleRidList(string primaryRid)
        {
            var compatibleRids = new List<string> { primaryRid };

            // Parse the RID to extract OS and architecture
            // RID format: {os}[-{version}][-{qualifier}]-{arch}
            // Examples: win-x64, win10-x64, linux-x64, linux-musl-x64, osx.12-arm64

            if (primaryRid.StartsWith("win", StringComparison.OrdinalIgnoreCase))
            {
                // Windows compatibility chain
                // win10-x64 -> win-x64 -> win -> any
                string arch = ExtractArchitecture(primaryRid);
                if (arch != null)
                {
                    string genericWinRid = $"win-{arch}";
                    if (!string.Equals(primaryRid, genericWinRid, StringComparison.OrdinalIgnoreCase))
                    {
                        compatibleRids.Add(genericWinRid);
                    }
                    compatibleRids.Add("win");
                    compatibleRids.Add("any");
                }
                else
                {
                    // Just "win" folder without architecture
                    compatibleRids.Add("any");
                }
            }
            else if (primaryRid.StartsWith("linux-musl", StringComparison.OrdinalIgnoreCase))
            {
                // Alpine/musl Linux compatibility chain
                // linux-musl-x64 -> linux-x64 -> unix -> any
                string arch = ExtractArchitecture(primaryRid);
                if (arch != null)
                {
                    compatibleRids.Add($"linux-{arch}");
                    compatibleRids.Add("linux");
                    compatibleRids.Add("unix");
                    compatibleRids.Add("any");
                }
            }
            else if (primaryRid.StartsWith("linux", StringComparison.OrdinalIgnoreCase))
            {
                // Linux compatibility chain
                // linux-x64 -> linux -> unix -> any
                // linux-armel -> linux-arm -> linux -> unix -> any
                string arch = ExtractArchitecture(primaryRid);
                if (arch != null)
                {
                    // armel (ARM EABI soft-float) is compatible with arm
                    if (string.Equals(arch, "armel", StringComparison.OrdinalIgnoreCase))
                    {
                        compatibleRids.Add("linux-arm");
                    }
                    compatibleRids.Add("linux");
                    compatibleRids.Add("unix");
                    compatibleRids.Add("any");
                }
            }
            else if (primaryRid.StartsWith("maccatalyst", StringComparison.OrdinalIgnoreCase))
            {
                // Mac Catalyst compatibility chain (iOS apps on Mac)
                // maccatalyst-arm64 -> osx-arm64 -> osx -> unix -> any
                string arch = ExtractArchitecture(primaryRid);
                if (arch != null)
                {
                    compatibleRids.Add($"osx-{arch}");
                    compatibleRids.Add("osx");
                    compatibleRids.Add("unix");
                    compatibleRids.Add("any");
                }
            }
            else if (primaryRid.StartsWith("osx", StringComparison.OrdinalIgnoreCase))
            {
                // macOS compatibility chain
                // osx.12-arm64 -> osx-arm64 -> osx -> unix -> any
                string arch = ExtractArchitecture(primaryRid);
                if (arch != null)
                {
                    string genericOsxRid = $"osx-{arch}";
                    if (!string.Equals(primaryRid, genericOsxRid, StringComparison.OrdinalIgnoreCase))
                    {
                        compatibleRids.Add(genericOsxRid);
                    }
                    compatibleRids.Add("osx");
                    compatibleRids.Add("unix");
                    compatibleRids.Add("any");
                }
                else
                {
                    // Just "osx" folder without architecture
                    compatibleRids.Add("unix");
                    compatibleRids.Add("any");
                }
            }
            else if (primaryRid.StartsWith("browser-wasm", StringComparison.OrdinalIgnoreCase))
            {
                // Browser WebAssembly - not compatible with native platforms
                compatibleRids.Add("any");
            }
            else if (primaryRid.StartsWith("unix", StringComparison.OrdinalIgnoreCase))
            {
                // Generic Unix compatibility chain
                compatibleRids.Add("any");
            }
            else
            {
                // Unknown RID, just add "any" as fallback
                compatibleRids.Add("any");
            }

            return compatibleRids;
        }

        /// <summary>
        /// Extracts the architecture from a RID string.
        /// </summary>
        /// <param name="rid">The RID string.</param>
        /// <returns>The architecture portion of the RID, or null if not found.</returns>
        private static string ExtractArchitecture(string rid)
        {
            // Known architectures - order matters, longer names first to avoid partial matches
            string[] knownArchitectures = new[]
            {
                "loongarch64", "ppc64le", "mips64", "s390x", "arm64", "armel", "wasm", "arm", "x64", "x86"
            };

            // Split RID by '-' and check for architecture at the end
            string[] parts = rid.Split('-');
            if (parts.Length >= 2)
            {
                string lastPart = parts[parts.Length - 1];
                foreach (string arch in knownArchitectures)
                {
                    if (string.Equals(lastPart, arch, StringComparison.OrdinalIgnoreCase))
                    {
                        return arch;
                    }
                }
            }

            return null;
        }

        #endregion
    }
}
