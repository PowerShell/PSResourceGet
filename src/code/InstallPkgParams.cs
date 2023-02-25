// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Management.Automation;

public class InstallPkgParams
{
    public string Name { get; set; }
    public VersionRange Version { get; set; }
    public string Repository { get; set; }
    public bool AcceptLicense { get; set; } 
    public bool Prerelease { get; set; }
    public ScopeType Scope { get; set; }
    public bool Quiet { get; set; }
    public bool Reinstall { get; set; }
    public bool Force { get; set; }
    public bool TrustRepository { get; set; }
    public bool NoClobber { get; set; }
    public bool SkipDependencyCheck { get; set; }



    #region Private methods

    public void SetProperty(string propertyName, string propertyValue, out ErrorRecord IncorrectVersionFormat)
    {
        IncorrectVersionFormat = null;
        switch (propertyName.ToLower())
        {
            case "name":
                Name = propertyValue.Trim();
                break;

            case "version":
                // If no Version specified, install latest version for the package.
                // Otherwise validate Version can be parsed out successfully.
                VersionRange versionTmp = VersionRange.None;
                if (string.IsNullOrWhiteSpace(propertyValue))
                {
                    Version = VersionRange.All;
                }
                else if (!Utils.TryParseVersionOrVersionRange(propertyValue, out versionTmp))
                {
                    var exMessage = "Argument for Version parameter is not in the proper format.";
                    var ex = new ArgumentException(exMessage);
                    IncorrectVersionFormat = new ErrorRecord(ex, "IncorrectVersionFormat", ErrorCategory.InvalidArgument, null);
                }
                Version = versionTmp;
                break;

            case "repository":
                Repository = propertyValue.Trim();
                break;

            case "acceptlicense":
                bool.TryParse(propertyValue, out bool acceptLicenseTmp);
                AcceptLicense = acceptLicenseTmp;
                break;

            case "prerelease":
                bool.TryParse(propertyValue, out bool prereleaseTmp);
                Prerelease = prereleaseTmp;
                break;

            case "scope":
                ScopeType scope = (ScopeType)Enum.Parse(typeof(ScopeType), propertyValue, ignoreCase: true);
                Scope = scope;
                break;

            case "quiet":
                bool.TryParse(propertyValue, out bool quietTmp);
                Quiet = quietTmp;
                break;

            case "reinstall":
                bool.TryParse(propertyValue, out bool reinstallTmp);
                Reinstall = reinstallTmp;
                break;

            case "trustrepository":
                bool.TryParse(propertyValue, out bool trustRepositoryTmp);
                TrustRepository = trustRepositoryTmp;
                break;

            case "noclobber":
                bool.TryParse(propertyValue, out bool noClobberTmp);
                NoClobber = noClobberTmp;
                break;

            case "skipdependencycheck":
                bool.TryParse(propertyValue, out bool skipDependencyCheckTmp);
                SkipDependencyCheck = skipDependencyCheckTmp;
                break;

            default:
                // write error
                break;
        }
    }

    #endregion
}