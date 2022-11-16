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
    PSResourceInfo FindAll(PSRepositoryInfo repository, bool includePrerelease, out string errRecord);

    /// <summary>
    /// Find method which allows for searching for packages with tag(s) from a repository and returns latest version for each.
    /// Examples: Search -Tag "JSON" -Repository PSGallery
    /// API call: 
    /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='tag:JSON'
    /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&searchTerm='tag:JSON'&includePrerelease=true
    /// </summary>
    PSResourceInfo[] FindTags(string[] tags, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out HashSet<string> tagsFound, out string errRecord);

    /// <summary>
    /// Find method which allows for searching for packages with resource type specified from a repository and returns latest version for each.
    /// Name: supports wildcards
    /// Type: Module, Script, Command, DSCResource (can take multiple)
    /// Examples: Search -Type Module -Repository PSGallery
    ///           Search -Type Module -Name "Az*" -Repository PSGallery
    /// TODO: discuss consolidating Modules and Scripts endpoints (move scripts to modules endpoint)
    /// TODO Note: searchTerm is tokenized by whitespace.
    /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='Az* tag:PSModule'
    /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='az* tag:PSScript'&includePrerelease=true
    /// </summary>
    PSResourceInfo FindTypes(ResourceType packageResourceType, string packageName, PSRepositoryInfo repository, bool includePrerelease, out string errRecord);

    /// <summary>
    /// Find method which allows for searching for command names and returns latest version of matching packages.
    /// Name: supports wildcards.
    /// Examples: Search -Name "Command1", "Command2" -Repository PSGallery
    /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='Az* tag:PSCommand_Command1 tag:PSCommand_Command2'
    /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='Az* tag:PSCommand_Command1 tag:PSCommand_Command2'&includePrerelease=true
    /// </summary>
    PSResourceInfo FindCommandName(string[] commandNames, PSRepositoryInfo repository, bool includePrerelease, out string errRecord);

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
    
    /// <summary>
    /// *** we will not support this scenario ***
    /// Find method which allows for searching for single name with wildcards with version range.
    /// Name: supports wildcards
    /// Version: support wildcards
    /// Examples: Search "PowerShell*" "[3.0.0.0, 5.0.0.0]"
    ///           Search "PowerShell*" "3.*"
    /// </summary>
    //PSResourceInfo FindNameGlobbingAndVersionGlobbing(string packageName, VersionRange versionRange, PSRepositoryInfo repository, bool includePrerelease, out string errRecord);

    /// <summary>
    /// *** we will not support this scenario ***
    /// Find method which allows for searching for single name with wildcards with specific version.
    /// Name: supports wildcards
    /// Version: no wildcard support
    /// Examples: Search "PowerShell*" "3.0.0.0"
    /// </summary>
    //PSResourceInfo FindNameGlobbingAndVersion(string packageName, NuGetVersion version, PSRepositoryInfo repository, out string errRecord);

    /// <summary>
    /// *** Note: we would just iterate through the names client side and call FindName() or FindNameGlobbing() *** 
    /// Find method which allows for searching for multiple names and returns latest version for each.
    /// Name: supports wildcards
    /// Examples: Search "PowerShellGet", "Package*", "PSReadLine"
    /// </summary>
    // PSResourceInfo FindNamesGlobbing(string[] packageNames, PSRepositoryInfo repository, bool includePrerelease, out string[] errRecords);

    /// <summary>
    /// *** we will not support this scenario ***
    /// Find method which allows for searching for multiple names with specific version.
    /// Name: supports wildcards
    /// Version: no wildcard support
    /// Examples: Search "PowerShellGet", "Package*", "PSReadLine" "3.0.0.0"
    /// </summary>
    //PSResourceInfo FindNamesGlobbingAndVersion(string[] packageNames, NuGetVersion version, PSRepositoryInfo repository, out string errRecord);

    /// <summary>
    /// *** Note: would just iterate through names client side, and call FindVersionGlobbing() for each and discard (error) for name with globbing) ***
    /// Find method which allows for searching for multiple names with version range.
    /// Name: supports wildcards
    /// Version: support wildcards
    /// Examples: Search "PowerShellGet", "Package*", "PSReadLine" "[3.0.0.0, 5.0.0.0]" --> do it for first, write error for second, do it for third
    ///           Search "PowerShellGet", "Package*", "PSReadLine" "3.*" --> do it for first, write error for second, do it for third
    ///           Search "Package*", "PSReadLin*" "3.*" --> not supported
    /// </summary>
    // PSResourceInfo FindNamesAndVersionGlobbing(string[] packageNames, VersionRange versionRange, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out string[] errRecord); 

    #endregion
}
