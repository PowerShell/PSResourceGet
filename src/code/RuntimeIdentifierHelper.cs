// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;

namespace Microsoft.PowerShell.PSResourceGet.UtilClasses
{
    /// <summary>
    /// Detects the current Runtime Identifier (RID) and evaluates RID compatibility.
    /// </summary>
    internal static class RuntimeIdentifierHelper
    {
        private static readonly object s_lock = new();
        private static string s_currentRid;
        private static List<string> s_compatibleRids;

        /// <summary>
        /// Gets the portable RID for the current process.
        /// </summary>
        public static string GetCurrentRuntimeIdentifier()
        {
            if (s_currentRid != null)
            {
                return s_currentRid;
            }

            lock (s_lock)
            {
                if (s_currentRid == null)
                {
                    s_currentRid = DetectRuntimeIdentifier();
                }

                return s_currentRid;
            }
        }

        /// <summary>
        /// Gets compatible RIDs for the current process, ordered from most to least specific.
        /// </summary>
        public static IReadOnlyList<string> GetCompatibleRuntimeIdentifiers()
        {
            if (s_compatibleRids != null)
            {
                return s_compatibleRids;
            }

            lock (s_lock)
            {
                if (s_compatibleRids == null)
                {
                    s_compatibleRids = BuildCompatibleRidList(GetCurrentRuntimeIdentifier());
                }

                return s_compatibleRids;
            }
        }

        /// <summary>
        /// Gets compatible RIDs for the specified RID, ordered from most to least specific.
        /// </summary>
        public static IReadOnlyList<string> GetCompatibleRuntimeIdentifiers(string primaryRid)
        {
            if (string.IsNullOrWhiteSpace(primaryRid))
            {
                throw new ArgumentException("Primary RID cannot be null or empty.", nameof(primaryRid));
            }

            return BuildCompatibleRidList(primaryRid);
        }

        /// <summary>
        /// Determines whether a package RID is compatible with the current process RID.
        /// </summary>
        public static bool IsCompatibleRid(string candidateRid)
        {
            return IsCompatibleRid(candidateRid, GetCurrentRuntimeIdentifier());
        }

        /// <summary>
        /// Determines whether a package RID is compatible with the specified target RID.
        /// </summary>
        public static bool IsCompatibleRid(string candidateRid, string targetRid)
        {
            if (string.IsNullOrWhiteSpace(candidateRid) || string.IsNullOrWhiteSpace(targetRid))
            {
                return false;
            }

            if (ContainsRid(BuildCompatibleRidList(targetRid), candidateRid))
            {
                return true;
            }

            // PowerShell modules sometimes use version-specific folders such as win10-x64
            // while the host reports the portable win-x64 RID.
            return ContainsRid(BuildCompatibleRidList(candidateRid), targetRid);
        }

        private static string DetectRuntimeIdentifier()
        {
            string architecture = GetArchitectureString();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return $"win-{architecture}";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return IsMuslBasedLinux() ? $"linux-musl-{architecture}" : $"linux-{architecture}";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return $"osx-{architecture}";
            }

            return $"unix-{architecture}";
        }

        private static string GetArchitectureString()
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm => "arm",
                Architecture.Arm64 => "arm64",
                _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            };
        }

        private static bool IsMuslBasedLinux()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return false;
            }

            try
            {
                const string osReleasePath = "/etc/os-release";
                if (File.Exists(osReleasePath))
                {
                    string content = File.ReadAllText(osReleasePath);
                    if (content.IndexOf("alpine", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }

                return Directory.Exists("/lib") &&
                    Directory.GetFiles("/lib", "libc.musl-*.so*").Length > 0;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (SecurityException)
            {
                return false;
            }
        }

        private static List<string> BuildCompatibleRidList(string primaryRid)
        {
            var compatibleRids = new List<string>();
            AddRid(compatibleRids, primaryRid);

            if (primaryRid.StartsWith("win", StringComparison.OrdinalIgnoreCase))
            {
                string architecture = ExtractArchitecture(primaryRid);
                if (architecture != null)
                {
                    AddRid(compatibleRids, $"win-{architecture}");
                    AddRid(compatibleRids, "win");
                }

                AddRid(compatibleRids, "any");
            }
            else if (primaryRid.StartsWith("linux-musl", StringComparison.OrdinalIgnoreCase))
            {
                string architecture = ExtractArchitecture(primaryRid);
                if (architecture != null)
                {
                    AddRid(compatibleRids, $"linux-{architecture}");
                }

                AddRid(compatibleRids, "linux");
                AddRid(compatibleRids, "unix");
                AddRid(compatibleRids, "any");
            }
            else if (primaryRid.StartsWith("linux", StringComparison.OrdinalIgnoreCase))
            {
                string architecture = ExtractArchitecture(primaryRid);
                if (string.Equals(architecture, "armel", StringComparison.OrdinalIgnoreCase))
                {
                    AddRid(compatibleRids, "linux-arm");
                }

                AddRid(compatibleRids, "linux");
                AddRid(compatibleRids, "unix");
                AddRid(compatibleRids, "any");
            }
            else if (primaryRid.StartsWith("maccatalyst", StringComparison.OrdinalIgnoreCase))
            {
                string architecture = ExtractArchitecture(primaryRid);
                if (architecture != null)
                {
                    AddRid(compatibleRids, $"osx-{architecture}");
                }

                AddRid(compatibleRids, "osx");
                AddRid(compatibleRids, "unix");
                AddRid(compatibleRids, "any");
            }
            else if (primaryRid.StartsWith("osx", StringComparison.OrdinalIgnoreCase))
            {
                string architecture = ExtractArchitecture(primaryRid);
                if (architecture != null)
                {
                    AddRid(compatibleRids, $"osx-{architecture}");
                }

                AddRid(compatibleRids, "osx");
                AddRid(compatibleRids, "unix");
                AddRid(compatibleRids, "any");
            }
            else if (primaryRid.StartsWith("browser-wasm", StringComparison.OrdinalIgnoreCase))
            {
                AddRid(compatibleRids, "any");
            }
            else if (primaryRid.StartsWith("unix", StringComparison.OrdinalIgnoreCase))
            {
                AddRid(compatibleRids, "any");
            }
            else
            {
                AddRid(compatibleRids, "any");
            }

            return compatibleRids;
        }

        private static string ExtractArchitecture(string rid)
        {
            string[] knownArchitectures =
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

            string[] parts = rid.Split('-');
            if (parts.Length < 2)
            {
                return null;
            }

            string lastPart = parts[parts.Length - 1];
            foreach (string architecture in knownArchitectures)
            {
                if (string.Equals(lastPart, architecture, StringComparison.OrdinalIgnoreCase))
                {
                    return architecture;
                }
            }

            return null;
        }

        private static bool ContainsRid(IEnumerable<string> rids, string rid)
        {
            foreach (string candidate in rids)
            {
                if (string.Equals(candidate, rid, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddRid(List<string> rids, string rid)
        {
            if (!ContainsRid(rids, rid))
            {
                rids.Add(rid);
            }
        }
    }
}
