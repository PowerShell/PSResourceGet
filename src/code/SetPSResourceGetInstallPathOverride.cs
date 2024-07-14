// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
using System.Collections.Specialized;
using System.Management.Automation;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    /// <summary>
    /// The Set-PSResourceGetInstallPathOverride cmdlet is used to override install path for PS resources.
    /// </summary>
    [Cmdlet(VerbsCommon.Set,"PSResourceGetInstallPathOverride",SupportsShouldProcess = true)]
    [Alias("Update-PSResourceGetInstallPathOverride")]
    [OutputType(typeof(void))]

    public sealed class SetPSResourceGetInstallPathOverride : PSCmdlet
    {
        #region Parameters

        /// <summary>
        /// Specifies the desired path for the override.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, Mandatory = true)]
        public string Path { get; set; }

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

            // Validate path is not null or empty
            if (string.IsNullOrEmpty(Path)) {
                ThrowTerminatingError(
                    new ErrorRecord(
                        new PSInvalidOperationException($"Error input path is null or empty: '{Path}'"),
                        "InputPathIsEmpty",
                        ErrorCategory.InvalidArgument,
                        this
                    )
                );
            }

            // Validate path is absolute
            if (!System.IO.Path.IsPathRooted(Path)) {
                ThrowTerminatingError(
                    new ErrorRecord(
                        new PSInvalidOperationException($"Error input path is not rooted / absolute: '{Path}'"),
                        "InputPathIsNotRooted",
                        ErrorCategory.InvalidArgument,
                        this
                    )
                );
            }

            // Validate path can be expanded
            try {
                Environment.ExpandEnvironmentVariables(Path);
            }
            catch (Exception)
            {
                ThrowTerminatingError(
                    new ErrorRecord(
                        new PSInvalidOperationException($"Error input path could not be expanded: '{Path}'"),
                        "InputPathCannotBeExpanded",
                        ErrorCategory.InvalidArgument,
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
            EnvironmentVariableTarget envScope = (Scope is ScopeType.AllUsers) ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User;

            // Set env variable for install path override
            Environment.SetEnvironmentVariable(
                "PSResourceGetInstallPathOverride",
                Path,
                envScope
            );

            // Add install path override to PSModule path
            string PSModulePath = Environment.GetEnvironmentVariable(
                "PSModulePath",
                envScope
            );
            if (String.IsNullOrEmpty(PSModulePath)) {
                WriteVerbose(String.Format("PSModulePath in {0} context is empty.", envScope.ToString()));
                System.Environment.SetEnvironmentVariable(
                    "PSModulePath",
                    Path,
                    envScope
                );
            }
            WriteVerbose(string.Format("Current value of PSModulePath in {0} context: '{1}'", envScope.ToString(), PSModulePath));
            StringCollection PSModulePaths = new();
            foreach (string Item in PSModulePath.Trim(';').Split(';')) {
                try {
                    PSModulePaths.Add(System.Environment.ExpandEnvironmentVariables(Item));
                }
                catch {
                    WriteVerbose(string.Format("Will not validate '{0}' as it could not be expanded.", Item));
                }
            }
            if (PSModulePaths.Contains(System.Environment.ExpandEnvironmentVariables(Path))) {
                WriteVerbose(String.Format("Override install path is already in PSModulePath for scope '{0}'", envScope.ToString()));
            }
            else {
                WriteVerbose(
                    String.Format(
                        "Override install path is not already in PSModulePath for scope '{0}'",
                        envScope.ToString()
                    )
                );
                System.Environment.SetEnvironmentVariable(
                    "PSModulePath",
                    String.Format("{0};{1}", Path, PSModulePath),
                    envScope
                );
                WriteVerbose(
                    String.Format(
                        "Override install path was successfully added to PSModulePath for scope '{0}'.",
                        envScope.ToString()
                    )
                );
            }
        }

        #endregion
    }
}
