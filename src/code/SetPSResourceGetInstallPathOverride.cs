// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
using System.Collections.Specialized;
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
            {
                return _path;
            }

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
            string PathForModules = System.IO.Path.Combine(_path,"Modules");
            string PathForScripts = System.IO.Path.Combine(_path,"Scripts");

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
                if (this.ShouldProcess($"Set environment variable PSResourceGetPathOverride in scope '{EnvScope} to '{_path}"))
                {
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
            }

            // Add install path override for modules to PSModulePath
            string CurrentPSModulePath = Environment.GetEnvironmentVariable(
                "PSModulePath",
                EnvScope
            );
            if (String.IsNullOrEmpty(CurrentPSModulePath)) {
                WriteVerbose(String.Format("PSModulePath in scope '{0}' is empty.", EnvScope.ToString()));
                if (this.ShouldProcess($"Set environment pariable 'PSModulePath' in scope '{EnvScope} to '{PathForModules}"))
                {
                    System.Environment.SetEnvironmentVariable(
                        "PSModulePath",
                        PathForModules,
                        EnvScope
                    );
                }
            }
            WriteVerbose(string.Format("Current value of PSModulePath in {0} context: '{1}'", EnvScope.ToString(), CurrentPSModulePath));
            StringCollection CurrentPSModulePaths = new();
            foreach (string Item in CurrentPSModulePath.Trim(';').Split(';')) {
                CurrentPSModulePaths.Add(System.Environment.ExpandEnvironmentVariables(Item));
            }
            if (CurrentPSModulePaths.Contains(PathForModules)) {
                WriteVerbose(String.Format("PSModulePath in scope '{0}' already contains '{1}', no change needed.", EnvScope.ToString(),PathForModules));
            }
            else {
                WriteVerbose(
                    String.Format(
                        "PSModulePath in scope '{0}' does not already contain '{1}'",
                        EnvScope.ToString(),
                        PathForModules
                    )
                );
                if (this.ShouldProcess($"Add '{PathForModules}' to environment variable 'PSModulePath' in scope '{EnvScope}"))
                {
                    System.Environment.SetEnvironmentVariable(
                        "PSModulePath",
                        String.Format("{0};{1}", PathForModules, CurrentPSModulePath),
                        EnvScope
                    );
                    WriteVerbose(
                        String.Format(
                            "Successfully added '{0}' to PSModulePath in scope '{1}'",
                            PathForModules,
                            EnvScope.ToString()
                        )
                    );
                }
            }

            // Add install path override for scripts to Path
            string CurrentPath = Environment.GetEnvironmentVariable(
                "Path",
                EnvScope
            );
            if (String.IsNullOrEmpty(CurrentPath)) {
                WriteVerbose(String.Format("Path in scope '{0}' is empty.", EnvScope.ToString()));
                if (this.ShouldProcess($"Set environment pariable 'Path' in scope '{EnvScope} to '{PathForScripts}"))
                {
                    System.Environment.SetEnvironmentVariable(
                        "Path",
                        PathForScripts,
                        EnvScope
                    );
                }
            }
            WriteVerbose(string.Format("Current value of Path in {0} context: '{1}'", EnvScope.ToString(), CurrentPath));
            StringCollection CurrentPaths = new();
            foreach (string Item in CurrentPath.Trim(';').Split(';')) {
                CurrentPaths.Add(System.Environment.ExpandEnvironmentVariables(Item));
            }
            if (CurrentPaths.Contains(PathForScripts)) {
                WriteVerbose(String.Format("Path in scope '{0}' already contains '{1}', no change needed.", EnvScope.ToString(),PathForScripts));
            }
            else {
                WriteVerbose(
                    String.Format(
                        "Override install path is not already in Path for scope '{0}'",
                        EnvScope.ToString()
                    )
                );
                if (this.ShouldProcess($"Add '{PathForScripts}' to environment variable 'Path' in scope '{EnvScope}"))
                {
                    System.Environment.SetEnvironmentVariable(
                        "Path",
                        String.Format("{0};{1}", PathForScripts, CurrentPath),
                        EnvScope
                    );
                    WriteVerbose(
                        String.Format(
                            "Successfully added '{0}' to Path in scope '{1}'",
                            PathForScripts,
                            EnvScope.ToString()
                        )
                    );
                }
            }
        }

        #endregion
    }
}
