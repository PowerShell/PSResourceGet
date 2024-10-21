// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using Microsoft.Win32;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    /// <summary>
    /// This class is used to enforce group policy for repositories.
    /// </summary>
    public class GroupPolicyRepositoryEnforcement
    {
        const string userRoot = "HKEY_CURRENT_USER";
        const string psresourcegetGPPath = @"SOFTWARE\Policies\Microsoft\PSResourceGetRepository";
        const string gpRootPath = @"Software\Microsoft\Windows\CurrentVersion\Group Policy Objects";

        private GroupPolicyRepositoryEnforcement()
        {
        }

        /// <summary>
        /// This method is used to see if the group policy is enabled.
        /// </summary>
        ///
        /// <returns>True if the group policy is enabled, false otherwise.</returns>
        public static bool IsGroupPolicyEnabled()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                // Always return false for non-Windows platforms and Group Policy is not available.
                return false;
            }

            if (InternalHooks.EnableGPRegistryHook)
            {
                return InternalHooks.GPEnabledStatus;
            }

            var values = ReadGPFromRegistry();

            if (values is not null && values.Count > 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get allowed list of URIs for allowed repositories.
        /// </summary>
        /// <returns>Array of allowed URIs.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the group policy is not enabled.</exception>
        public static Uri[]? GetAllowedRepositoryURIs()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                throw new PlatformNotSupportedException("Group policy is only supported on Windows.");
            }

            if (InternalHooks.EnableGPRegistryHook)
            {
                var uri = new Uri(InternalHooks.AllowedUri);
                return new Uri[] { uri };
            }

            if (!IsGroupPolicyEnabled())
            {
                return null;
            }
            else
            {
                List<Uri> allowedUris = new List<Uri>();

                var allowedRepositories = ReadGPFromRegistry();

                if (allowedRepositories is not null && allowedRepositories.Count > 0)
                {
                    foreach (var allowedRepository in allowedRepositories)
                    {
                        allowedUris.Add(allowedRepository.Value);
                    }
                }

                return allowedUris.ToArray();
            }
        }

        internal static bool IsRepositoryAllowed(Uri repositoryUri)
        {
            bool isAllowed = false;

            if(GroupPolicyRepositoryEnforcement.IsGroupPolicyEnabled())
            {
                var allowedList = GroupPolicyRepositoryEnforcement.GetAllowedRepositoryURIs();

                if (allowedList != null && allowedList.Length > 0)
                {
                    isAllowed = allowedList.Any(uri => uri.Equals(repositoryUri));
                }
            }
            else
            {
                isAllowed = true;
            }

            return isAllowed;
        }

        private static List<KeyValuePair<string, Uri>>? ReadGPFromRegistry()
        {
            List<KeyValuePair<string, Uri>> allowedRepositories = new List<KeyValuePair<string, Uri>>();

            using (var key = Registry.CurrentUser.OpenSubKey(gpRootPath))
            {
                if (key is null)
                {
                    throw new InvalidOperationException("Group policy is not enabled.");
                }

                var subKeys = key.GetSubKeyNames();

                if (subKeys is null)
                {
                    throw new InvalidOperationException("Group policy is not enabled.");
                }

                foreach (var subKey in subKeys)
                {
                    if (subKey.EndsWith("Machine"))
                    {
                        continue;
                    }

                    using (var psrgKey = key.OpenSubKey(subKey + "\\" + psresourcegetGPPath))
                    {
                        if (psrgKey is null)
                        {
                            // this GPO does not have PSResourceGetRepository key
                            continue;
                        }

                        var valueNames =  psrgKey.GetValueNames();

                        // This means it is disabled
                        if (valueNames is null || valueNames.Length == 0 || valueNames.Length == 1 && valueNames[0].Equals("**delvals.", StringComparison.OrdinalIgnoreCase))
                        {
                            return null;
                        }
                        else
                        {
                            foreach (var valueName in valueNames)
                            {
                                if (valueName.Equals("**delvals.", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                var value = psrgKey.GetValue(valueName);

                                if (value is null)
                                {
                                    throw new InvalidOperationException("Invalid registry value.");
                                }

                                string valueString = value.ToString();
                                var kvRegValue = ConvertRegValue(valueString);
                                allowedRepositories.Add(kvRegValue);
                            }
                        }
                    }
                }
            }

            return allowedRepositories;
        }

        private static KeyValuePair<string, Uri> ConvertRegValue(string regValue)
        {
            if (string.IsNullOrEmpty(regValue))
            {
                throw new ArgumentException("Registry value is empty.");
            }

            var KvPairs = regValue.Split(new char[] { ';' });

            string? nameValue = null;
            string? uriValue = null;

            foreach (var kvPair in KvPairs)
            {
                var kv = kvPair.Split(new char[] { '=' }, 2);

                if (kv.Length != 2)
                {
                    throw new InvalidOperationException("Invalid registry value.");
                }

                if (kv[0].Equals("Name", StringComparison.OrdinalIgnoreCase))
                {
                    nameValue = kv[1];
                }

                if (kv[0].Equals("Uri", StringComparison.OrdinalIgnoreCase))
                {
                    uriValue = kv[1];
                }
            }

            if (nameValue is not null && uriValue is not null)
            {
                return new KeyValuePair<string, Uri>(nameValue, new Uri(uriValue));
            }
            else
            {
                throw new InvalidOperationException("Invalid registry value.");
            }
        }
    }
}