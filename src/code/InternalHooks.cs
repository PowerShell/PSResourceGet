// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
    }
}
