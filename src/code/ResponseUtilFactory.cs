// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    internal class ResponseUtilFactory
    {
        public static ResponseUtil GetResponseUtil(PSRepositoryInfo repository)
        {
            PSRepositoryInfo.APIVersion repoApiVersion = repository.ApiVersion;
            ResponseUtil currentResponseUtil = null;

            switch (repoApiVersion)
            {
                case PSRepositoryInfo.APIVersion.v2:
                    currentResponseUtil = new V2ResponseUtil(repository);
                    break;

                case PSRepositoryInfo.APIVersion.v3:
                    currentResponseUtil = new V3ResponseUtil(repository);
                    break;
            }

            return currentResponseUtil;
        }
    }
}