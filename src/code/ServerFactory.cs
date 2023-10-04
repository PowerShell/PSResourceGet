// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    internal static class UserAgentInfo
    {
        static UserAgentInfo()
        {
            using (System.Management.Automation.PowerShell ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace))
            {
                _psVersion = ps.AddScript("$PSVersionTable.PSVersion.ToString()").Invoke<string>()[0];
            }

            _psResourceGetVersion = typeof(UserAgentInfo).Assembly.GetName().Version.ToString();
            _distributionChannel = System.Environment.GetEnvironmentVariable("POWERSHELL_DISTRIBUTION_CHANNEL") ?? "unknown";
        }

        private static string _psVersion;
        private static string _psResourceGetVersion;
        private static string _distributionChannel;

        internal static string UserAgentString()
        {
            string psGetCompat = InternalHooks.InvokedFromCompat ? "true" : "false";
            return $"PSResourceGet/{_psResourceGetVersion} PowerShell/{_psVersion} DistributionChannel/{_distributionChannel} PowerShellGetCompat/{psGetCompat}";
        }
    }

    internal class ServerFactory
    {
        public static ServerApiCall GetServer(PSRepositoryInfo repository, PSCmdlet cmdletPassedIn, NetworkCredential networkCredential)
        {
            PSRepositoryInfo.APIVersion repoApiVersion = repository.ApiVersion;
            ServerApiCall currentServer = null;
            string userAgentString = UserAgentInfo.UserAgentString();

            switch (repoApiVersion)
            {
                case PSRepositoryInfo.APIVersion.v2:
                    currentServer = new V2ServerAPICalls(repository, cmdletPassedIn, networkCredential, userAgentString);
                    break;

                case PSRepositoryInfo.APIVersion.v3:
                    currentServer = new V3ServerAPICalls(repository, cmdletPassedIn, networkCredential, userAgentString);
                    break;

                case PSRepositoryInfo.APIVersion.local:
                    currentServer = new LocalServerAPICalls(repository, cmdletPassedIn, networkCredential);
                    break;

                case PSRepositoryInfo.APIVersion.nugetServer:
                    currentServer = new NuGetServerAPICalls(repository, cmdletPassedIn, networkCredential, userAgentString);
                    break;

                case PSRepositoryInfo.APIVersion.unknown:
                    break;
            }

            return currentServer;
        }
    }
}
