// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    /// <summary>
    /// The Set-PSResourceGetInstallPathOverride cmdlet is used to override install path for PS resources.
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "PSResourceGetInstallPathOverride", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
    [Alias("Update-PSResourceGetInstallPathOverride")]
    [OutputType(typeof(void))]

    public sealed class SetPSResourceGetInstallPathOverride : PSCmdlet
    {
        #region Parameters

        /// <summary>
        /// Specifies the desired path for the override.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, Mandatory = true, HelpMessage = "Path for the override.")]
        [ValidateNotNullOrEmpty]
        public string Path
        {
            get
            { return _path; }

            set
            {
                if (WildcardPattern.ContainsWildcardCharacters(value))
                {
                    throw new PSArgumentException("Wildcard characters are not allowed in the path.");
                }

                // This will throw if path cannot be resolved
                _path = GetResolvedProviderPathFromPSPath(
                    Environment.ExpandEnvironmentVariables(value),
                    out ProviderInfo provider
                ).First();
            }
        }
        private string _path;

        /// <summary>
        /// Specifies the scope of installation.
        /// </summary>
        [Parameter(Position = 1)]
        public ScopeType Scope { get; set; }

        #endregion

        #region Method override - Begin

        protected override void BeginProcessing()
        {
            // Only run on Windows for now, due to env variables on Unix being very different
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                ThrowTerminatingError(
                    new ErrorRecord(
                        new PSInvalidOperationException($"Error this only works on Windows for now"),
                        "OsIsNotWindows",
                        ErrorCategory.InvalidOperation,
                        this
                    )
                );
            }
        }

        #endregion

        #region Method override - Process

        protected override void ProcessRecord()
        {
            // Assets
            EnvironmentVariableTarget EnvScope = (Scope is ScopeType.AllUsers) ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User;

            // Set env variable for install path override
            string PathOverrideCurrentValue = Environment.GetEnvironmentVariable(
                "PSResourceGetInstallPathOverride",
                EnvScope
            );
            if (!String.IsNullOrEmpty(PathOverrideCurrentValue)) {
                WriteVerbose(
                    String.Format(
                        "Current value of PSResourceGetInstallPathOverride in scope '{0}': '{1}'",
                        EnvScope.ToString(),
                        PathOverrideCurrentValue
                    )
                );
            }
            if (
                !String.IsNullOrEmpty(PathOverrideCurrentValue) &&
                String.Equals(
                    Environment.ExpandEnvironmentVariables(PathOverrideCurrentValue),
                    _path,
                    StringComparison.Ordinal
                )
            )
            {
                WriteVerbose(
                    String.Format(
                        "PSResourceGetInstallPathOverride in scope '{0}' is already '{1}', no change needed.",
                        EnvScope.ToString(),
                        _path
                    )
                );
            }
            else {
                Environment.SetEnvironmentVariable(
                    "PSResourceGetInstallPathOverride",
                    _path,
                    EnvScope
                );
                WriteVerbose(
                    String.Format(
                        "PSResourceGetInstallPathOverride in scope '{0}' was successfully set to: '{1}'",
                        EnvScope.ToString(),
                        _path
                    )
                );
            }

            // Add install path override to PSModule path
            string PSModulePath = Environment.GetEnvironmentVariable(
                "PSModulePath",
                EnvScope
            );
            if (String.IsNullOrEmpty(PSModulePath)) {
                WriteVerbose(String.Format("PSModulePath in {0} context is empty.", EnvScope.ToString()));
                System.Environment.SetEnvironmentVariable(
                    "PSModulePath",
                    _path,
                    EnvScope
                );
            }
            WriteVerbose(string.Format("Current value of PSModulePath in {0} context: '{1}'", EnvScope.ToString(), PSModulePath));
            StringCollection PSModulePaths = new();
            foreach (string Item in PSModulePath.Trim(';').Split(';')) {
                try {
                    PSModulePaths.Add(System.Environment.ExpandEnvironmentVariables(Item));
                }
                catch {
                    WriteVerbose(string.Format("Will not validate '{0}' as it could not be expanded.", Item));
                }
            }
            if (PSModulePaths.Contains(_path)) {
                WriteVerbose(String.Format("Override install path is already in PSModulePath for scope '{0}'", EnvScope.ToString()));
            }
            else {
                WriteVerbose(
                    String.Format(
                        "Override install path is not already in PSModulePath for scope '{0}'",
                        EnvScope.ToString()
                    )
                );
                System.Environment.SetEnvironmentVariable(
                    "PSModulePath",
                    String.Format("{0};{1}", _path, PSModulePath),
                    EnvScope
                );
                WriteVerbose(
                    String.Format(
                        "Override install path was successfully added to PSModulePath for scope '{0}'.",
                        EnvScope.ToString()
                    )
                );
            }
        }

        #endregion
    }
}
