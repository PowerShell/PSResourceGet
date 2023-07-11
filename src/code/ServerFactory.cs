// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System.Net;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    internal class ServerFactory
    {
        public static ServerApiCall GetServer(PSRepositoryInfo repository, NetworkCredential networkCredential)
        {
            PSRepositoryInfo.APIVersion repoApiVersion = repository.ApiVersion;
            ServerApiCall currentServer = null;

            switch (repoApiVersion)
            {
                case PSRepositoryInfo.APIVersion.v2:
                    currentServer = new V2ServerAPICalls(repository, networkCredential);
                    break;

                case PSRepositoryInfo.APIVersion.v3:
                    currentServer = new V3ServerAPICalls(repository, networkCredential);
                    break;

                case PSRepositoryInfo.APIVersion.local:
                    currentServer = new LocalServerAPICalls(repository, networkCredential);
                    break;

                case PSRepositoryInfo.APIVersion.nugetServer:
                    currentServer = new NuGetServerAPICalls(repository, networkCredential);
                    break;

                case PSRepositoryInfo.APIVersion.unknown:
                    break;
            }

            return currentServer;
        }
    }
}
