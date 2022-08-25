using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;

public interface IInstallPSResource
{
    #region Methods

    /// <summary>
    /// Installs specific package.
    /// Name: no wildcard support.
    /// Examples: Install "PowerShellGet"
    /// </summary>
    PSResourceInfo InstallName(string pkgName, PSRepositoryInfo repository, bool includePrerelease);

    /// <summary>
    /// Installs package with specific name and version.
    /// Name: no wildcard support.
    /// Version: no wildcard support.
    /// Examples: Install "PowerShellGet" -Version "3.0.0.0"
    ///           Install "PowerShellGet" -Version "3.0.0-beta16"
    /// </summary>    
    PSResourceInfo InstallVersion(string pkgName, NuGetVersion version, PSRepositoryInfo repository);

    /// <summary>
    /// Installs latest package within version range provided.
    /// Name: no wildcard support.
    /// Version: supports wilcard.
    /// Examples: Install "PowerShellGet" -Version [2.2.5, 3.0.0.0]
    ///           Install "PowerShellGet" -Version "3.*"
    /// </summary>
    PSResourceInfo InstallVersionGlobbing(string pkgName, VersionRange versionRange, PSRepositoryInfo repository, bool includePrerelease);

    #endregion
}