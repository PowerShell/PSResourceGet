// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    internal class ServerFactory
    {
        public static ServerApiCall GetServer(PSRepositoryInfo repository)
        {
            PSRepositoryInfo.APIVersion repoApiVersion = repository.ApiVersion;
            ServerApiCall currentServer = null;

            switch (repoApiVersion)
            {
                case PSRepositoryInfo.APIVersion.v2:
                    currentServer = new V2ServerAPICalls(repository);
                    break;

                case PSRepositoryInfo.APIVersion.v3:
                    currentServer = new V3ServerAPICalls(repository);
                    break;
            }

            return currentServer;
        }
    }
}