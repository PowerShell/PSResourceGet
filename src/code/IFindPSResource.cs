using System.Collections.Generic;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;

public interface IFindPSResource
{
    #region Methods

    /// <summary>
    /// Find method which allows for searching for all packages from a repository and returns latest version for each.
    /// Examples: Search -Repository PSGallery
    /// API call: 
    /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion
    /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&includePrerelease=true
    /// </summary>
    PSResourceInfo FindAll(PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out string errRecord);

    /// <summary>
    /// Find method which allows for searching for packages with tag(s) from a repository and returns latest version for each.
    /// Examples: Search -Tag "JSON" -Repository PSGallery
    /// API call: 
    /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='tag:JSON'
    /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&searchTerm='tag:JSON'&includePrerelease=true
    /// </summary>
    PSResourceInfo[] FindTags(string[] tags, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out HashSet<string> tagsFound, out string errRecord);

    /// <summary>
    /// Find method which allows for searching for single name and returns latest version.
    /// Name: no wildcard support
    /// Examples: Search "PowerShellGet"
    /// API call: 
    /// - No prerelease: http://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'
    /// - Include prerelease: http://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'
    /// Implementation Note: Need to filter further for latest version (prerelease or non-prerelease dependening on user preference)
    /// </summary>
    PSResourceInfo FindName(string packageName, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out string errRecord);

    /// <summary>
    /// Find method which allows for searching for single name with wildcards and returns latest version.
    /// Name: supports wildcards
    /// Examples: Search "PowerShell*"
    /// API call: 
    /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='az*'
    /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&searchTerm='az*'&includePrerelease=true
    /// Implementation Note: filter additionally and verify ONLY package name was a match.
    /// </summary>
    PSResourceInfo[] FindNameGlobbing(string packageName, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out string errRecord);

    /// <summary>
    /// Find method which allows for searching for single name with version range.
    /// Name: no wildcard support
    /// Version: supports wildcards
    /// Examples: Search "PowerShellGet" "[3.0.0.0, 5.0.0.0]"
    ///           Search "PowerShellGet" "3.*"
    /// API Call: http://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'
    /// Implementation note: Returns all versions, including prerelease ones. Later (in the API client side) we'll do filtering on the versions to satisfy what user provided.
    /// </summary>
    PSResourceInfo[] FindVersionGlobbing(string packageName, VersionRange versionRange, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out string errRecord);

    /// <summary>
    /// Find method which allows for searching for single name with specific version.
    /// Name: no wildcard support
    /// Version: no wildcard support
    /// Examples: Search "PowerShellGet" "2.2.5"
    /// API call: http://www.powershellgallery.com/api/v2/Packages(Id='PowerShellGet', Version='2.2.5')
    /// </summary>
    PSResourceInfo FindVersion(string packageName, string version, PSRepositoryInfo repository, ResourceType type, out string errRecord);
    
    #endregion
}
