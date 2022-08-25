using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;

public interface IFindPSResource
{
    #region Methods

    /// <summary>
    /// Find method which allows for searching for all packages from a repository and returns latest version for each.
    /// Examples: Search -Repository PSGallery
    /// </summary>
    PSResourceInfo FindAll(PSRepositoryInfo repository, bool includePrerelease);

    /// <summary>
    /// Find method which allows for searching for packages with tag(s) from a repository and returns latest version for each.
    /// Examples: Search -Tag "JSON" -Repository PSGallery
    /// </summary>
    PSResourceInfo FindTags(string[] tags, PSRepositoryInfo repository, bool includePrerelease);

    /// <summary>
    /// Find method which allows for searching for packages with resource type specified from a repository and returns latest version for each.
    /// Name: supports wildcards
    /// Type: Module, Script, Command, DSCResource (can take multiple)
    /// Examples: Search -Type Module -Repository PSGallery
    ///           Search "Az*" -Type Module -Repository PSGallery
    /// </summary>
    PSResourceInfo FindTypes(ResourceType packageResourceType, string packageName, PSRepositoryInfo repository, bool includePrerelease);

    /// <summary>
    /// Find method which allows for searching for command names and returns latest version of matching packages.
    /// Name: supports wildcards
    /// Examples: Search -Name "Command1", "Command2" -Repository PSGallery
    /// </summary>
    PSResourceInfo FindCommandName(string[] commandNames, PSRepositoryInfo repository, bool includePrerelease);

    /// <summary>
    /// Find method which allows for searching for DSC Resource names and returns latest version of matching packages.
    /// Name: supports wildcards
    /// Examples: Search -Name "DSCResource1", "DSCResource2" -Repository PSGallery
    PSResourceInfo FindDSCResourceName(string[] dscResourceName, PSRepositoryInfo repository, bool includePrerPSResourceInfoPSResourceInfoelease);

    /// <summary>
    /// Find method which allows for searching for single name and returns latest version.
    /// Name: no wildcard support
    /// Examples: Search "PowerShellGet"
    /// </summary>
    PSResourceInfo FindName(string packageName, PSRepositoryInfo repository, bool includePrerelease);

    /// <summary>
    /// Find method which allows for searching for single name with wildcards and returns latest version.
    /// Name: supports wildcards
    /// Examples: Search "PowerShell*"
    /// </summary>
    PSResourceInfo FindNameGlobbing(string packageName, PSRepositoryInfo repository, bool includePrerelease);

    /// <summary>
    /// Find method which allows for searching for single name with version range.
    /// Name: no wildcard support
    /// Version: supports wildcards
    /// Examples: Search "PowerShellGet" "[3.0.0.0, 5.0.0.0]"
    ///           Search "PowerShellGet" "3.*"
    /// </summary>
    PSResourceInfo FindVersionGlobbing(string packageName, VersionRange versionRange, PSRepositoryInfo repository, bool includePrerelease);

    /// <summary>
    /// Find method which allows for searching for single name with specific version.
    /// Name: no wildcard support
    /// Version: no wildcard support
    /// Examples: Search "PowerShellGet" "3.0.0.0"
    /// </summary>
    PSResourceInfo FindVersion(string packageName, NuGetVersion version, PSRepositoryInfo repository);

    /// <summary>
    /// Find method which allows for searching for single name with wildcards with version range.
    /// Name: supports wildcards
    /// Version: support wildcards
    /// Examples: Search "PowerShell*" "[3.0.0.0, 5.0.0.0]"
    ///           Search "PowerShell*" "3.*"
    /// </summary>
    PSResourceInfo FindNameGlobbingAndVersionGlobbing(string packageName, VersionRange versionRange, PSRepositoryInfo repository, bool includePrerelease);

    /// <summary>
    /// Find method which allows for searching for single name with wildcards with specific version.
    /// Name: supports wildcards
    /// Version: no wildcard support
    /// Examples: Search "PowerShell*" "3.0.0.0"
    /// </summary>
    PSResourceInfo FindNameGlobbingAndVersion(string packageName, NuGetVersion version, PSRepositoryInfo repository);

    /// <summary>
    /// Find method which allows for searching for multiple names and returns latest version for each.
    /// Name: supports wildcards
    /// Examples: Search "PowerShellGet", "Package*", "PSReadLine"
    /// </summary>
    PSResourceInfo FindNamesGlobbing(string[] packageNames, PSRepositoryInfo repository, bool includePrerelease);

    /// <summary>
    /// Find method which allows for searching for multiple names with specific version.
    /// Name: supports wildcards
    /// Version: no wildcard support
    /// Examples: Search "PowerShellGet", "Package*", "PSReadLine" "3.0.0.0"
    /// </summary>
    PSResourceInfo FindNamesGlobbingAndVersion(string[] packageNames, NuGetVersion version, PSRepositoryInfo repository);

    /// <summary>
    /// Find method which allows for searching for multiple names with version range.
    /// Name: supports wildcards
    /// Version: support wildcards
    /// Examples: Search "PowerShellGet", "Package*", "PSReadLine" "[3.0.0.0, 5.0.0.0]"
    ///           Search "PowerShellGet", "Package*", "PSReadLine" "3.*"
    /// </summary>
    PSResourceInfo FindNamesGlobbingAndVersionGlobbing(string[] packageNames, VersionRange versionRange, PSRepositoryInfo repository, bool includePrerelease); 

    #endregion
}
