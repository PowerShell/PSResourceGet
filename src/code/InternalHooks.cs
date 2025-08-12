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

        public static void SetTestHook(string property, object value)
        {
            FieldInfo fieldInfo = typeof(InternalHooks).GetField(property, BindingFlags.Static | BindingFlags.NonPublic);
            fieldInfo?.SetValue(null, value);
        }
    }
}
