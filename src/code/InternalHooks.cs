// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.PowerShell.PSResourceGet.UtilClasses
{
    public class InternalHooks
    {
        internal static bool InvokedFromCompat;

        internal static bool EnableGPRegistryHook;

        internal static bool GPEnabledStatus;

        internal static string AllowedUri;

        internal static string MARPrefix;

        // PSContentPath testing hooks
        internal static string LastUserContentPathSource;
        internal static string LastUserContentPath;

        public static void SetTestHook(string property, object value)
        {
            var fieldInfo = typeof(InternalHooks).GetField(property, BindingFlags.Static | BindingFlags.NonPublic);
            fieldInfo?.SetValue(null, value);
        }

        public static object GetTestHook(string property)
        {
            var fieldInfo = typeof(InternalHooks).GetField(property, BindingFlags.Static | BindingFlags.NonPublic);
            return fieldInfo?.GetValue(null);
        }

        public static void ClearPSContentPathHooks()
        {
            LastUserContentPathSource = null;
            LastUserContentPath = null;
        }

        public static string GetUserString()
        {
            return Microsoft.PowerShell.PSResourceGet.Cmdlets.UserAgentInfo.UserAgentString();
        }

        public static string GetCurrentRuntimeIdentifier()
        {
            return RuntimeIdentifierHelper.GetCurrentRuntimeIdentifier();
        }

        public static IReadOnlyList<string> GetCompatibleRuntimeIdentifiers()
        {
            return RuntimeIdentifierHelper.GetCompatibleRuntimeIdentifiers();
        }

        public static IReadOnlyList<string> GetCompatibleRuntimeIdentifiersFor(string rid)
        {
            return RuntimeIdentifierHelper.GetCompatibleRuntimeIdentifiers(rid);
        }

        public static bool IsCompatibleRid(string rid)
        {
            return RuntimeIdentifierHelper.IsCompatibleRid(rid);
        }

        public static bool IsCompatibleRidWith(string candidateRid, string targetRid)
        {
            return RuntimeIdentifierHelper.IsCompatibleRid(candidateRid, targetRid);
        }

        public static bool IsRidFolder(string folderName)
        {
            return RuntimePackageHelper.IsRidFolder(folderName);
        }

        public static bool IsRuntimeAssetEntry(string entryFullName)
        {
            return RuntimePackageHelper.IsRuntimeAssetEntry(entryFullName);
        }

        public static string GetRidFromRuntimeAssetEntry(string entryFullName)
        {
            return RuntimePackageHelper.GetRidFromRuntimeAssetEntry(entryFullName);
        }

        public static bool ShouldIncludeRuntimeAssetEntry(string entryFullName)
        {
            return RuntimePackageHelper.ShouldIncludeEntry(entryFullName);
        }

        public static bool ShouldIncludeRuntimeAssetEntryForRid(string entryFullName, string targetRid)
        {
            return RuntimePackageHelper.ShouldIncludeEntry(entryFullName, targetRid);
        }

        public static IReadOnlyList<string> GetAvailableRidsFromZipFile(string zipPath)
        {
            return RuntimePackageHelper.GetAvailableRidsFromZipFile(zipPath);
        }
    }
}
