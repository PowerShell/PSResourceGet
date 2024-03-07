// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    internal class ResponseUtilFactory
    {
        public static ResponseUtil GetResponseUtil(PSRepositoryInfo repository)
        {
            PSRepositoryInfo.APIVersion repoApiVersion = repository.ApiVersion;
            ResponseUtil currentResponseUtil = null;

            switch (repoApiVersion)
            {
                case PSRepositoryInfo.APIVersion.V2:
                    currentResponseUtil = new V2ResponseUtil(repository);
                    break;

                case PSRepositoryInfo.APIVersion.V3:
                    currentResponseUtil = new V3ResponseUtil(repository);
                    break;

                case PSRepositoryInfo.APIVersion.Local:
                    currentResponseUtil = new LocalResponseUtil(repository);
                    break;

                case PSRepositoryInfo.APIVersion.NugetServer:
                    currentResponseUtil = new NuGetServerResponseUtil(repository);
                    break;

                case PSRepositoryInfo.APIVersion.ContainerRegistry:
                    currentResponseUtil = new ACRResponseUtil(repository);
                    break;

                case PSRepositoryInfo.APIVersion.Unknown:
                    break;
            }

            return currentResponseUtil;
        }
    }
}
