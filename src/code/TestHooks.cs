// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.Cmdlets;
using NuGet.Versioning;
using System.Management.Automation;

namespace Microsoft.PowerShell.PSResourceGet.UtilClasses
{
    /// <summary>
    /// Entry points that expose internal functionality to the Pester tests.
    /// </summary>
    public static class TestHooks
    {
        public static PSObject ReadPSGetResourceInfo(string filePath)
        {
            if (PSResourceInfo.TryRead(filePath, out PSResourceInfo psGetInfo, out string errorMsg))
            {
                return PSObject.AsPSObject(psGetInfo);
            }

            throw new PSInvalidOperationException(errorMsg);
        }

        public static void WritePSGetResourceInfo(
            string filePath,
            PSObject psObjectGetInfo)
        {
            if (psObjectGetInfo.BaseObject is PSResourceInfo psGetInfo)
            {
                if (!psGetInfo.TryWrite(filePath, out string errorMsg))
                {
                    throw new PSInvalidOperationException(errorMsg);
                }

                return;
            }

            throw new PSArgumentException("psObjectGetInfo argument is not a PSGetResourceInfo type.");
        }

        /// <summary>
        /// Selects the "packageContent" URL from V3 registration entries for the given version, or for the latest version if no version is given.
        /// </summary>
        public static string SelectV3PackageContentUrl(
            string[] registrationEntries,
            string version)
        {
            NuGetVersion requiredVersion = null;
            if (!string.IsNullOrEmpty(version) && !NuGetVersion.TryParse(version, out requiredVersion))
            {
                throw new PSArgumentException($"Version '{version}' is not a valid NuGet version.");
            }

            return V3ServerAPICalls.GetPackageContentUrl(registrationEntries, requiredVersion);
        }
    }
}
