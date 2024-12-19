using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Management.Automation;
using System.Text.Json;
using System.Net.Http;
using System.Net;

namespace Microsoft.PowerShell.PSResourceGet.UtilClasses
{
    internal static class CredentialProvider
    {
        private static readonly string _credProviderExe = "CredentialProvider.Microsoft.exe";
        private static readonly string _credProviderDll = "CredentialProvider.Microsoft.dll";

        private static string FindCredProviderFromPluginsPath()
        {
            // Get environment variable "NUGET_PLUGIN_PATHS"
            // The environment variable NUGET_PLUGIN_PATHS should have the value of the .exe or .dll of the credential provider found in plugins\netfx\CredentialProvider.Microsoft\
            // For example, $env:NUGET_PLUGIN_PATHS="my-alternative-location\CredentialProvider.Microsoft.exe".
            // OR $env:NUGET_PLUGIN_PATHS="my-alternative-location\CredentialProvider.Microsoft.dll"

            return Environment.GetEnvironmentVariable("NUGET_PLUGIN_PATHS", EnvironmentVariableTarget.User) ?? Environment.GetEnvironmentVariable("NUGET_PLUGIN_PATHS", EnvironmentVariableTarget.Machine);
        }

        private static string FindCredProviderFromDefaultLocation()
        {
            // Default locations are either:
            // $env:UserProfile\.nuget\plugins\netfx\CredentialProvider\CredentialProvider.Microsoft.exe
            // OR $env:UserProfile\.nuget\plugins\netcore\CredentialProvider\CredentialProvider.Microsoft.exe (or) CredentialProvider.Microsoft.dll
            var credProviderDefaultLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "plugins");

            var netCorePath = Path.Combine(credProviderDefaultLocation, "netcore", "CredentialProvider.Microsoft");
            var netFxPath = Path.Combine(credProviderDefaultLocation, "netfx", "CredentialProvider.Microsoft");
            var credProviderPath = string.Empty;
            if (Directory.Exists(netCorePath))
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    credProviderPath = Path.Combine(netCorePath, _credProviderExe);
                }
                else
                {
                    credProviderPath = Path.Combine(netCorePath, _credProviderDll);
                }
            }
            else if (Directory.Exists(netFxPath) && Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                credProviderPath = Path.Combine(netFxPath, _credProviderExe);
            }

            return credProviderPath;
        }

        private static string FindCredProviderFromVSLocation(out ErrorRecord error)
        {
            error = null;

            // C:\Program Files\Microsoft Visual Studio\
            var visualStudioPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio");
            // "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\CommonExtensions\Microsoft\NuGet\Plugins\CredentialProvider.Microsoft\CredentialProvider.Microsoft.exe"
            // "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\CommonExtensions\Microsoft\NuGet\Plugins\CredentialProvider.Microsoft\CredentialProvider.Microsoft.dll"

            var credProviderPath = string.Empty;
            if (Directory.Exists(visualStudioPath))
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    credProviderPath = VSCredentialProviderFile(visualStudioPath, _credProviderExe, out error);
                }
                else if (string.IsNullOrEmpty(credProviderPath))
                {
                    credProviderPath = VSCredentialProviderFile(visualStudioPath, _credProviderDll, out error);
                }
            }

            return credProviderPath;
        }

        private static string VSCredentialProviderFile(string visualStudioPath, string credProviderFile, out ErrorRecord error)
        {
            error = null;
            try
            {
                // Search for the file in the directory and subdirectories
                string[] exeFile = Directory.GetFiles(visualStudioPath, credProviderFile, SearchOption.AllDirectories);

                if (exeFile.Length > 0)
                {
                    return exeFile[0];
                }
            }
            catch (UnauthorizedAccessException e)
            {
                error = new ErrorRecord(
                            e,
                            "AccessToCredentialProviderFileDenied",
                            ErrorCategory.PermissionDenied,
                            null);
            }
            catch (Exception ex)
            {
                error = new ErrorRecord(
                            ex,
                            "ErrorRetrievingCredentialProvider",
                            ErrorCategory.NotSpecified,
                            null);
            }

            return string.Empty;
        }

        internal static PSCredential GetCredentialsFromProvider(Uri uri, PSCmdlet cmdletPassedIn)
        {
            cmdletPassedIn.WriteDebug("Enterting CredentialProvider::GetCredentialsFromProvider");
            string credProviderPath = string.Empty;
            
            //  Find credential provider
            //  Option 1. Use env var 'NUGET_PLUGIN_PATHS' to find credential provider.
            //   See: https://docs.microsoft.com/en-us/nuget/reference/extensibility/nuget-cross-platform-plugins#plugin-installation-and-discovery
            //  Nuget prioritizes credential providers stored in the NUGET_PLUGIN_PATHS env var
            credProviderPath = FindCredProviderFromPluginsPath();

            //  Option 2. Check default locations ($env:UserProfile\.nuget\plugins)
            //  .NET Core based plugins should be installed in:
            //      %UserProfile%/.nuget/plugins/netcore
            //  .NET Framework based plugins should be installed in:
            //      %UserProfile%/.nuget/plugins/netfx
            if (String.IsNullOrEmpty(credProviderPath))
            {
                credProviderPath = FindCredProviderFromDefaultLocation();
            }

            //  Option 3. Check Visual Studio installation paths
            if (String.IsNullOrEmpty(credProviderPath))
            {
                credProviderPath = FindCredProviderFromVSLocation(out ErrorRecord error);
                if (error != null)
                {
                    cmdletPassedIn.WriteError(error);
                    return null;
                }
            }

            cmdletPassedIn.WriteDebug($"Credential provider path is '{credProviderPath}'");
            if (string.IsNullOrEmpty(credProviderPath))
            {
                cmdletPassedIn.WriteError(new ErrorRecord(
                        new ArgumentNullException("Path to the Azure Artifacts Credential Provider is null or empty. See https://github.com/NuGet/Home/wiki/NuGet-cross-plat-authentication-plugin#plugin-installation-and-discovery to set up the Credential Provider."),
                        "CredentialProviderPathIsNullOrEmpty",
                        ErrorCategory.InvalidArgument,
                        credProviderPath));
                return null;
            }

            if (!File.Exists(credProviderPath))
            {
                // If the Credential Provider is not found on a Unix machine, try looking for a case insensitive file.
                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    FileInfo fileInfo = new FileInfo(credProviderPath);
                    string resolvedFilePath = Utils.GetCaseInsensitiveFilePath(fileInfo.Directory.FullName, _credProviderDll);
                    if (resolvedFilePath != null)
                    {
                        credProviderPath = resolvedFilePath;
                    }
                    else
                    {
                        cmdletPassedIn.WriteError(new ErrorRecord(
                            new FileNotFoundException($"Path found '{credProviderPath}' is not a valid Azure Artifact Credential Provider executable. See https://github.com/NuGet/Home/wiki/NuGet-cross-plat-authentication-plugin#plugin-installation-and-discovery to set up the Credential Provider."),
                            "CredentialProviderFileNotFound",
                            ErrorCategory.ObjectNotFound,
                            credProviderPath));
                    }
                }
                else
                {
                    cmdletPassedIn.WriteError(new ErrorRecord(
                            new FileNotFoundException($"Path found '{credProviderPath}' is not a valid Azure Artifact Credential Provider executable. See https://github.com/NuGet/Home/wiki/NuGet-cross-plat-authentication-plugin#plugin-installation-and-discovery to set up the Credential Provider."),
                            "CredentialProviderFileNotFound",
                            ErrorCategory.ObjectNotFound,
                            credProviderPath));

                    return null;
                }
            }

            cmdletPassedIn.WriteVerbose($"Credential Provider path found at: '{credProviderPath}'");

            string fileName = credProviderPath;
            // If running on unix machines, the Credential Provider needs to be called with dotnet cli.
            if (credProviderPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                fileName = "dotnet";
            }

            string arguments = string.Equals(fileName, "dotnet", StringComparison.OrdinalIgnoreCase) ?
                                                       $"{credProviderPath} -Uri {uri} -NonInteractive -IsRetry -F Json" :
                                                       $"-Uri {uri} -NonInteractive -IsRetry -F Json";
            string fullCallingCmd = string.Equals(fileName, "dotnet", StringComparison.OrdinalIgnoreCase) ?
                                                       $"dotnet {credProviderPath} -Uri {uri} -NonInteractive -IsRetry -F Json" :
                                                       $"{credProviderPath} -Uri {uri} -NonInteractive -IsRetry -F Json";
            cmdletPassedIn.WriteVerbose($"Calling Credential Provider with the following: '{fullCallingCmd}'");
            using (Process process = new Process())
            {
                // Windows call should look like:   "CredentialProvider.Microsoft.exe -Uri <uri> -NonInteractive -IsRetry -F Json"
                // Unix call should look like:      "dotnet CredentialProvider.Microsoft.dll -Uri <uri> -NonInteractive -IsRetry -F Json"
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var stdError = process.StandardError.ReadToEnd();

                // Timeout in milliseconds (e.g., 5000 ms = 5 seconds)
                process.WaitForExit(5000);

                if (process.ExitCode != 0)
                {
                    if (!string.IsNullOrEmpty(stdError))
                    {
                        cmdletPassedIn.WriteError(new ErrorRecord(
                                new ArgumentException($"Standard error: {stdError}"),
                                "ProcessStandardError",
                                ErrorCategory.InvalidResult,
                                credProviderPath));
                    }

                    cmdletPassedIn.WriteError(new ErrorRecord(
                            new Exception($"Process exited with code {process.ExitCode}"),
                            "ProcessExitCodeError",
                            ErrorCategory.InvalidResult,
                            credProviderPath));
                }
                else if (string.IsNullOrEmpty(output))
                {
                    cmdletPassedIn.WriteError(new ErrorRecord(
                            new ArgumentException($"Standard output is empty."),
                            "ProcessStandardOutputError",
                            ErrorCategory.InvalidResult,
                            credProviderPath));
                }

                string username = string.Empty;
                SecureString passwordSecure = new SecureString();
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(output))
                    {
                        JsonElement root = doc.RootElement;
                        if (root.TryGetProperty("Username", out JsonElement usernameToken))
                        {
                            username = usernameToken.GetString();
                            cmdletPassedIn.WriteVerbose("Username retrieved from Credential Provider.");
                        }
                        if (String.IsNullOrEmpty(username))
                        {
                            cmdletPassedIn.WriteError(new ErrorRecord(
                                    new ArgumentNullException("Credential Provider username is null or empty. See https://github.com/NuGet/Home/wiki/NuGet-cross-plat-authentication-plugin#plugin-installation-and-discovery for more info."),
                                    "CredentialProviderUserNameIsNullOrEmpty",
                                    ErrorCategory.InvalidArgument,
                                    credProviderPath));
                            return null;
                        }

                        if (root.TryGetProperty("Password", out JsonElement passwordToken))
                        {
                            string password = passwordToken.GetString();
                            if (String.IsNullOrEmpty(password))
                            {
                                cmdletPassedIn.WriteError(new ErrorRecord(
                                        new ArgumentNullException("Credential Provider password is null or empty. See https://github.com/NuGet/Home/wiki/NuGet-cross-plat-authentication-plugin#plugin-installation-and-discovery for more info."),
                                        "CredentialProviderUserNameIsNullOrEmpty",
                                        ErrorCategory.InvalidArgument,
                                        credProviderPath));
                                return null;
                            }

                            passwordSecure = Utils.ConvertToSecureString(password);
                            cmdletPassedIn.WriteVerbose("Password retrieved from Credential Provider.");
                        }
                    }
                }
                catch (Exception e)
                {
                    cmdletPassedIn.WriteError(new ErrorRecord(
                            new Exception("Error retrieving credentials from Credential Provider. See https://github.com/NuGet/Home/wiki/NuGet-cross-plat-authentication-plugin#plugin-installation-and-discovery for more info.", e),
                            "InvalidCredentialProviderResponse",
                            ErrorCategory.InvalidResult,
                            credProviderPath));
                    return null;
                }

                return new PSCredential(username, passwordSecure);
            }
        }
    }
}
