// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;

namespace Microsoft.PowerShell.PSResourceGet.UtilClasses
{
    public class InternalHooks
    {
        internal static bool InvokedFromCompat;

        public static void SetTestHook(string property, object value)
        {
            var fieldInfo = typeof(InternalHooks).GetField(property, BindingFlags.Static | BindingFlags.NonPublic);
            fieldInfo?.SetValue(null, value);
        }
    }
}