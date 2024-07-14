// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
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
                        "PathMissingExpectedSubdirectories",
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
                        "PathMissingExpectedSubdirectories",
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
                        "PathMissingExpectedSubdirectories",
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
            WriteVerbose(string.Format("Current value of PSModulePath in {0} context: '{1}'", envScope.ToString(), PSModulePath));
        }

        #endregion
    }
}
