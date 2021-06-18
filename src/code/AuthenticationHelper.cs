// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// Authentication helper class includes functions to get repository credentials from Microsoft.PowerShell.SecretManagement if provided
    /// </summary>
    internal class AuthenticationHelper
    {
        internal static readonly string VaultNameAttribute = "VaultName";
        internal static readonly string SecretAttribute = "Secret";

        private readonly PSCmdlet _cmdletPassedIn;

        private static readonly string SecretManagementModuleName = "Microsoft.PowerShell.SecretManagement";

        public AuthenticationHelper(PSCmdlet cmdletPassedIn)
        {
            _cmdletPassedIn = cmdletPassedIn;
        }

        public string GetRepositoryAuthenticationPassword(string repositoryName, string vaultName, string secretName)
        {
            var results = PowerShellInvoker.InvokeScriptWithHost<string>(
                cmdlet: _cmdletPassedIn,
                script: $@"
                    param (
                        [string] $VaultName,
                        [string] $SecretName
                    )
                    $module = Microsoft.PowerShell.Core\Import-Module -Name {SecretManagementModuleName} -PassThru
                    if ($null -eq $module) {{
                        return
                    }}
                    & $module ""Get-Secret"" -Name $SecretName -Vault $VaultName -AsPlainText
                ",
                args: new object[] { vaultName, secretName },
                out Exception terminatingError);

            string secretValueInPlainText = (results.Count == 1) ? results[0] : null;
            // SecretStore allows empty secret values so only check for null
            if (secretValueInPlainText == null)
            {
                _cmdletPassedIn.ThrowTerminatingError(
                    new ErrorRecord(
                        new PSInvalidOperationException(
                            message: string.Format(CultureInfo.InvariantCulture, "Unable to read secret {0} from vault {1} for authenticating to PSResourceRepository {2}", secretName, vaultName, repositoryName),
                            innerException: terminatingError),
                        "RepositoryAuthenticationCannotGetSecretFromVault",
                        ErrorCategory.InvalidOperation,
                        this));
            }

            return secretValueInPlainText;
        }
    }

    #region PowerShellInvoker

    internal static class PowerShellInvoker
    {
        #region Members

        private static Runspace _runspace;

        #endregion Members

        #region Methods

        public static Collection<T> InvokeScriptWithHost<T>(
            PSCmdlet cmdlet,
            string script,
            object[] args,
            out Exception terminatingError)
        {
            Collection<T> returnCollection = new Collection<T>();
            terminatingError = null;

            if (_runspace == null || _runspace.RunspaceStateInfo.State != RunspaceState.Opened)
            {
                if (_runspace != null)
                {
                    _runspace.Dispose();
                }

                var iss = InitialSessionState.CreateDefault2();
                // We are running trusted script.
                iss.LanguageMode = PSLanguageMode.FullLanguage;
                // Import the current PowerShellGet module.
                var modPathObjects = cmdlet.InvokeCommand.InvokeScript(
                    script: "(Get-Module -Name PowerShellGet).Path");
                string modPath = (modPathObjects.Count > 0 &&
                                  modPathObjects[0].BaseObject is string modPathStr)
                                  ? modPathStr : string.Empty;
                if (!string.IsNullOrEmpty(modPath))
                {
                    iss.ImportPSModule(new string[] { modPath });
                }

                try
                {
                    _runspace = RunspaceFactory.CreateRunspace(cmdlet.Host, iss);
                    _runspace.Open();
                }
                catch (Exception ex)
                {
                    terminatingError = ex;
                    return returnCollection;
                }
            }

            using (var ps = System.Management.Automation.PowerShell.Create())
            {
                ps.Runspace = _runspace;

                var cmd = new Command(
                    command: script, 
                    isScript: true, 
                    useLocalScope: true);
                cmd.MergeMyResults(
                    myResult: PipelineResultTypes.Error | PipelineResultTypes.Warning | PipelineResultTypes.Verbose | PipelineResultTypes.Debug | PipelineResultTypes.Information,
                    toResult: PipelineResultTypes.Output);
                ps.Commands.AddCommand(cmd);
                foreach (var arg in args)
                {
                    ps.Commands.AddArgument(arg);
                }
                
                try
                {
                    // Invoke the script.
                    var results = ps.Invoke();

                    // Extract expected output types from results pipeline.
                    foreach (var psItem in results)
                    {
                        if (psItem == null || psItem.BaseObject == null) { continue; }

                        switch (psItem.BaseObject)
                        {
                            case ErrorRecord error:
                                cmdlet.WriteError(error);
                                break;

                            case WarningRecord warning:
                                cmdlet.WriteWarning(warning.Message);
                                break;

                            case VerboseRecord verbose:
                                cmdlet.WriteVerbose(verbose.Message);
                                break;

                            case DebugRecord debug:
                                cmdlet.WriteDebug(debug.Message);
                                break;

                            case InformationRecord info:
                                cmdlet.WriteInformation(info);
                                break;
                                
                            case T result:
                                returnCollection.Add(result);
                                break;

                            case T[] resultArray:
                                foreach (var item in resultArray)
                                {
                                    returnCollection.Add(item);
                                }
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    terminatingError = ex;
                }
            }

            return returnCollection;
        }

        #endregion Methods
    }

    #endregion PowerShellInvoker
}