// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.PSResourceGet.UtilClasses
{
    public class InternalHooks
    {
        internal static bool InvokedFromCompat;

        internal static bool EnableGPRegistryHook;

        internal static bool GPEnabledStatus;

        internal static string AllowedUri;

        internal static string MARPrefix;

        public static void SetTestHook(string property, object value)
        {
            var fieldInfo = typeof(InternalHooks).GetField(property, BindingFlags.Static | BindingFlags.NonPublic);
            fieldInfo?.SetValue(null, value);
        }

        public static string GetUserString()
        {
            return Microsoft.PowerShell.PSResourceGet.Cmdlets.UserAgentInfo.UserAgentString();
        }

        #region RuntimeIdentifierHelper Test Hooks

        /// <summary>
        /// Returns the detected RID for the current platform.
        /// </summary>
        public static string GetCurrentRuntimeIdentifier()
        {
            return RuntimeIdentifierHelper.GetCurrentRuntimeIdentifier();
        }

        /// <summary>
        /// Returns the compatible RID list for the current platform.
        /// </summary>
        public static IReadOnlyList<string> GetCompatibleRuntimeIdentifiers()
        {
            return RuntimeIdentifierHelper.GetCompatibleRuntimeIdentifiers();
        }

        /// <summary>
        /// Returns the compatible RID list for a specified RID.
        /// </summary>
        public static IReadOnlyList<string> GetCompatibleRuntimeIdentifiersFor(string rid)
        {
            return RuntimeIdentifierHelper.GetCompatibleRuntimeIdentifiers(rid);
        }

        /// <summary>
        /// Checks if a given RID is compatible with the current platform.
        /// </summary>
        public static bool IsCompatibleRid(string rid)
        {
            return RuntimeIdentifierHelper.IsCompatibleRid(rid);
        }

        #endregion

        #region RuntimePackageHelper Test Hooks

        /// <summary>
        /// Checks if a folder name looks like a .NET Runtime Identifier.
        /// </summary>
        public static bool IsRidFolder(string folderName)
        {
            return RuntimePackageHelper.IsRidFolder(folderName);
        }

        /// <summary>
        /// Checks if a zip entry path contains runtime-specific assets.
        /// </summary>
        public static bool IsRuntimesEntry(string entryFullName)
        {
            return RuntimePackageHelper.IsRuntimesEntry(entryFullName);
        }

        /// <summary>
        /// Extracts the RID from a runtimes entry path.
        /// </summary>
        public static string GetRidFromRuntimesEntry(string entryFullName)
        {
            return RuntimePackageHelper.GetRidFromRuntimesEntry(entryFullName);
        }

        /// <summary>
        /// Determines if a zip entry should be included for the current platform.
        /// </summary>
        public static bool ShouldIncludeEntry(string entryFullName)
        {
            return RuntimePackageHelper.ShouldIncludeEntry(entryFullName);
        }

        /// <summary>
        /// Determines if a zip entry should be included for an explicit target RID.
        /// </summary>
        public static bool ShouldIncludeEntryForRid(string entryFullName, string targetRid)
        {
            return RuntimePackageHelper.ShouldIncludeEntry(entryFullName, targetRid);
        }

        /// <summary>
        /// Checks if a given RID is compatible with a specified target RID.
        /// </summary>
        public static bool IsCompatibleRidWith(string candidateRid, string targetRid)
        {
            return RuntimeIdentifierHelper.IsCompatibleRid(candidateRid, targetRid);
        }

        /// <summary>
        /// Returns a list of RIDs from a zip file's runtimes folder.
        /// </summary>
        public static IReadOnlyList<string> GetAvailableRidsFromZipFile(string zipPath)
        {
            return RuntimePackageHelper.GetAvailableRidsFromZipFile(zipPath);
        }

        #endregion
    }
}
