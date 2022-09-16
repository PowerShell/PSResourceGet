using System.Security.AccessControl;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using NuGet.Versioning;

public class ServerAPICalls : IServerAPICalls
{
    // Any interface method that is not implemented here should be processed in the parent method and then call one of the implemented 
    // methods below.

    #region Methods
    // High level design: Find-PSResource >>> IFindPSResource (loops, version checks, etc.) >>> IServerAPICalls (call to repository endpoint/url)    

    /// <summary>
    /// Find method which allows for searching for all packages from a repository and returns latest version for each.
    /// Examples: Search -Repository PSGallery
    /// API call: 
    /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion
    /// </summary>
    public string FindAllWithNoPrerelease(PSRepositoryInfo repository, out string[] errRecord) {
        errRecord = null;
        // var requestUrlV2 = $"{repository.Uri}/Search()?$filter=IsLatestVersion"

        // request object will send requestUrl 
        // if response != 200  capture and return error 

        // response will be json metadata object that will get returned

        return String.Empty;
    }

    /// <summary>
    /// Find method which allows for searching for all packages from a repository and returns latest version for each.
    /// Examples: Search -Repository PSGallery
    /// API call: 
    /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&includePrerelease=true
    /// </summary>
    public string FindAllWithPrerelease(PSRepositoryInfo repository, out string[] errRecord) {
        errRecord = null;
        // var requestUrlV2 = $"{repository.Uri}/Search()?$filter=IsAbsoluteLatestVersion&includePrerelease=true"

        // request object will send requestUrl 
        // if response != 200  capture and return error 

        // response will be json metadata object that will get returned

        return String.Empty;
    }

    /// <summary>
    /// Find method which allows for searching for packages with tag(s) from a repository and returns latest version for each.
    /// Examples: Search -Tag "JSON" -Repository PSGallery
    /// API call: 
    /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='tag:JSON'
    /// </summary>
    public string FindTagsWithNoPrerelease(string[] tags, PSRepositoryInfo repository, out string[] errRecord) {
        errRecord = null;
        // loop through put all tags into a properly formatted string, tokenized by whitespace
        // var tagsString 
        // var requestUrlV2 = $"{repository.Uri}/Search()?$filter=IsLatestVersion&searchTerm='tag:{tagsString}'"

        // request object will send requestUrl 
        // if response != 200  capture and return error 

        // response will be json metadata object that will get returned

        return string.Empty;
    }

    /// <summary>
    /// Find method which allows for searching for packages with tag(s) from a repository and returns latest version for each.
    /// Examples: Search -Tag "JSON" -Repository PSGallery
    /// API call: 
    /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&searchTerm='tag:JSON'&includePrerelease=true
    /// </summary>
    public string FindTagsWithPrerelease(string[] tags, PSRepositoryInfo repository, out string[] errRecord)
    {
        errRecord = null;
        // loop through put all tags into a properly formatted string
        // var tagsString 
        // var requestUrlV2 = $"{repository.Uri}/Search()?$filter=IsAbsoluteLatestVersion&searchTerm='tag:{tagsString}'&includePrerelease=true"

        // request object will send requestUrl 
        // if response != 200  capture and return error 

        // response will be json metadata object that will get returned

        return string.Empty;
    }

    /// <summary>
    /// Find method which allows for searching for packages with resource type specified from a repository and returns latest version for each.
    /// Name: supports wildcards
    /// Type: Module, Script, Command, DSCResource (can take multiple)
    /// Examples: Search -Type Module -Repository PSGallery
    ///           Search -Type Module -Name "Az*" -Repository PSGallery
    /// TODO: discuss consolidating Modules and Scripts endpoints (move scripts to modules endpoint)
    /// TODO Note: searchTerm is tokenized by whitespace.
    /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='Az* tag:PSModule'
    /// </summary>
    public string FindTypesWithNoPrerelease(ResourceType packageResourceType, string packageName, PSRepositoryInfo repository, out string[] errRecord) {
        errRecord = null;
        // var requestUrlV2 = $"{repository.Uri}/Search()?$filter=IsLatestVersion&searchTerm='{packageName} tag:{packageResourceType}'"

        // request object will send requestUrl 
        // if response != 200  capture and return error 

        // response will be json metadata object that will get returned

        return string.Empty;
    }

    /// <summary>
    /// Find method which allows for searching for packages with resource type specified from a repository and returns latest version for each.
    /// Name: supports wildcards
    /// Type: Module, Script, Command, DSCResource (can take multiple)
    /// Examples: Search -Type Module -Repository PSGallery
    ///           Search -Type Module -Name "Az*" -Repository PSGallery
    /// TODO: discuss consolidating Modules and Scripts endpoints (move scripts to modules endpoint)
    /// TODO Note: searchTerm is tokenized by whitespace.
    /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='az* tag:PSScript'&includePrerelease=true
    /// </summary>
    public string FindTypesWithPrerelease(ResourceType packageResourceType, string packageName, PSRepositoryInfo repository, out string[] errRecord)
    {
        errRecord = null;
        // var requestUrlV2 = $"{repository.Uri}/Search()?$filter=IsLatestVersion&searchTerm='{packageName} tag:{packageResourceType}'&includePrerelease=true"

        // request object will send requestUrl 
        // if response != 200  capture and return error 

        // response will be json metadata object that will get returned

        return string.Empty;
    }


    /// <summary>
    /// Find method which allows for searching for command names and returns latest version of matching packages.
    /// Name: supports wildcards.
    /// Examples: Search -Name "Command1", "Command2" -Repository PSGallery
    /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='Az* tag:PSCommand_Command1 tag:PSCommand_Command2'
    /// </summary>
    public string FindCommandNameWithNoPrerelease(string[] commandNames, PSRepositoryInfo repository, out string[] errRecord) {
        errRecord = null;
        // loop through and create a string of commandNames
        // var commandNamesString
        // var requestUrlV2 = $"{repository.Uri}/Search()?$filter=IsLatestVersion&searchTerm='{packageName} tag:{commandNamesString}'"

        // request object will send requestUrl 
        // if response != 200  capture and return error 

        // response will be json metadata object that will get returned

        return string.Empty;
    }

    /// <summary>
    /// Find method which allows for searching for command names and returns latest version of matching packages.
    /// Name: supports wildcards.
    /// Examples: Search -Name "Command1", "Command2" -Repository PSGallery
    /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='Az* tag:PSCommand_Command1 tag:PSCommand_Command2'&includePrerelease=true
    /// </summary>
    public string FindCommandNameWithPrerelease(string[] commandNames, PSRepositoryInfo repository, out string[] errRecord)
    {
        errRecord = null;
        // loop through and create a string of commandNames
        // var commandNamesString
        // var requestUrlV2 = $"{repository.Uri}/Search()?$filter=IsLatestVersion&searchTerm='{packageName} tag:{commandNamesString}'&includePrerelease=true"

        // request object will send requestUrl 
        // if response != 200  capture and return error 

        // response will be json metadata object that will get returned

        return string.Empty;
    }

    /// <summary>
    /// Find method which allows for searching for DSC Resource names and returns latest version of matching packages.
    /// Name: Support wildcards.
    /// Examples: Search -Name "DSCResource1", "DSCResource2" -Repository PSGallery
    /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='Az* tag:PSDSCResource_DSCResource1 tag:PSDSCResource_DSCResource2'
    public string FindDSCResourceNameWithNoPrerelease(string[] dscResourceName, PSRepositoryInfo repository, out string[] errRecord) {
        errRecord = null;
        // loop through and create a string of dscResourceNames
        // var dscResourceNamesString
        // var requestUrlV2 = $"{repository.Uri}/Search()?$filter=IsLatestVersion&searchTerm='{packageName} tag:{dscResourceNamesString}'"

        // request object will send requestUrl 
        // if response != 200  capture and return error 

        // response will be json metadata object that will get returned

        return string.Empty;
    }

    /// <summary>
    /// Find method which allows for searching for DSC Resource names and returns latest version of matching packages.
    /// Name: Support wildcards.
    /// Examples: Search -Name "DSCResource1", "DSCResource2" -Repository PSGallery
    /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='Az* tag:PSDSCResource_DSCResource1 tag:PSDSCResource_DSCResource2'&includePrerelease=true
    public string FindDSCResourceNameWithPrerelease(string[] dscResourceName, PSRepositoryInfo repository, out string[] errRecord)
    {
        errRecord = null;
        // loop through and create a string of dscResourceNames
        // var dscResourceNamesString
        // var requestUrlV2 = $"{repository.Uri}/Search()?$filter=IsLatestVersion&searchTerm='{packageName} tag:{dscResourceNamesString}'&includePrerelease=true"

        // request object will send requestUrl 
        // if response != 200  capture and return error 

        // response will be json metadata object that will get returned

        return string.Empty;
    }

    /// <summary>
    /// Find method which allows for searching for single name and returns latest version.
    /// Name: no wildcard support
    /// Examples: Search "PowerShellGet"
    /// API call: 
    /// - No prerelease: http://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'
    /// - Include prerelease: http://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'
    /// Implementation Note: Need to filter further for latest version (prerelease or non-prerelease dependening on user preference)
    /// </summary>
    public string FindName(string packageName, PSRepositoryInfo repository, out string[] errRecord) {
        errRecord = null;
        // var requestUrlV2 = $"{repository.Uri}/FindPackagesById()?id='{packageName}'"

        // request object will send requestUrl 
        // if response != 200  capture and return error 

        // response will be json metadata object that will get returned
        // Note: Need to filter further for latest version (prerelease or non-prerelease dependening on user preference)


        return string.Empty;
    }

    /// <summary>
    /// Find method which allows for searching for single name with wildcards and returns latest version.
    /// Name: supports wildcards
    /// Examples: Search "PowerShell*"
    /// API call: 
    /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='az*'
    /// Implementation Note: filter additionally and verify ONLY package name was a match.
    /// </summary>
    public string FindNameGlobbingWithNoPrerelease(string packageName, PSRepositoryInfo repository, out string[] errRecord) {
        errRecord = null;
        // var requestUrlV2 = $"{repository.Uri}/Search()?$filter=IsLatestVersion&searchTerm='{packageName}'"

        // request object will send requestUrl 
        // if response != 200  capture and return error 

        // response will be json metadata object that will get returned

        return string.Empty;
    }

    /// <summary>
    /// Find method which allows for searching for single name with wildcards and returns latest version.
    /// Name: supports wildcards
    /// Examples: Search "PowerShell*"
    /// API call: 
    /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&searchTerm='az*'&includePrerelease=true
    /// Implementation Note: filter additionally and verify ONLY package name was a match.
    /// </summary>
    public string FindNameGlobbingWithPrerelease(string packageName, PSRepositoryInfo repository, out string[] errRecord)
    {
        errRecord = null;
        // var requestUrlV2 = $"{repository.Uri}/Search()?$filter=IsAbsoluteLatestVersion&searchTerm='{packageName}'&includePrerelease=true"

        // request object will send requestUrl 
        // if response != 200  capture and return error 

        // response will be json metadata object that will get returned

        return string.Empty;
    }

    /// <summary>
    /// Find method which allows for searching for single name with version range.
    /// Name: no wildcard support
    /// Version: supports wildcards
    /// Examples: Search "PowerShellGet" "[3.0.0.0, 5.0.0.0]"
    ///           Search "PowerShellGet" "3.*"
    /// API Call: http://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'
    /// Implementation note: Returns all versions, including prerelease ones. Later (in the API client side) we'll do filtering on the versions to satisfy what user provided.
    /// </summary>
    public string FindVersionGlobbing(string packageName, VersionRange versionRange, PSRepositoryInfo repository, out string[] errRecord) {
        errRecord = null;
        // var requestUrlV2 = $"{repository.Uri}/FindPackagesById()?id='{packageName}'" 

        // request object will send requestUrl 
        // if response != 200  capture and return error 

        // response will be json metadata object that will get returned
        // Note: Need to filter further for versions (prerelease or non-prerelease dependening on user preference) in the calling method/interface

        return string.Empty;
    }

    /// <summary>
    /// Find method which allows for searching for single name with specific version.
    /// Name: no wildcard support
    /// Version: no wildcard support
    /// Examples: Search "PowerShellGet" "2.2.5"
    /// API call: http://www.powershellgallery.com/api/v2/Packages(Id='PowerShellGet', Version='2.2.5')
    /// </summary>
    public string FindVersion(string packageName, NuGetVersion version, PSRepositoryInfo repository, out string[] errRecord) {
        errRecord = null;
        // var requestUrlV2 = $"{repository.Uri}/Packages(Id='{packageName}', Version='{version}') "

        // request object will send requestUrl 
        // if response != 200  capture and return error 

        // response will be json metadata object that will get returned
        // Note: this is looking for a specific version 

        return string.Empty;
    }

    /**  INSTALL APIS **/

    /// <summary>
    /// Installs specific package.
    /// Name: no wildcard support.
    /// Examples: Install "PowerShellGet"
    /// Implementation Note:   if not prerelease: https://www.powershellgallery.com/api/v2/package/powershellget (Returns latest stable)
    ///                        if prerelease, the calling method should first call IFindPSResource.FindName(), 
    ///                             then find the exact version to install, then call into install version
    /// </summary>
    public string InstallName(string packageName, PSRepositoryInfo repository, out string[] errRecord) {
        errRecord = null;
        // var requestUrlV2 = $"repository.Uri/package/{packageName}"

        // request object will send requestUrl 
        // if response != 200  capture and return error 

        // response will be json metadata object that will get returned

        return string.Empty;
    }


    /// <summary>
    /// Installs package with specific name and version.
    /// Name: no wildcard support.
    /// Version: no wildcard support.
    /// Examples: Install "PowerShellGet" -Version "3.0.0.0"
    ///           Install "PowerShellGet" -Version "3.0.0-beta16"
    /// API Call: https://www.powershellgallery.com/api/v2/package/Id/version (version can be prerelease)
    /// </summary>    
    public string InstallVersion(string packageName, NuGetVersion version, PSRepositoryInfo repository, out string[] errRecord) {
        errRecord = null;
        // var requestUrlV2 = $"{repository.Uri}/package/{packageName}/{version}"

        // request object will send requestUrl 
        // if response != 200  capture and return error 

        // response will be json metadata object that will get returned

        return string.Empty;
    }

    #endregion
}
