using System;
using System.Collections;
using System.Xml;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;
using System.Collections.Generic;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    internal class HttpFindPSResource : IFindPSResource
    {
        #region Members
        
        readonly V2ServerAPICalls v2ServerAPICall = new V2ServerAPICalls();

        #endregion

        #region Constructor

        public HttpFindPSResource() {}

        #endregion

        #region Methods

        /// <summary>
        /// Find method which allows for searching for all packages from a repository and returns latest version for each.
        /// Examples: Search -Repository PSGallery
        /// API call: 
        /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion
        /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&includePrerelease=true
        /// </summary>
        public PSResourceInfo FindAll(PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out string errRecord)
        {
            var response = v2ServerAPICall.FindAll(repository, includePrerelease, type, out errRecord);

            PSResourceInfo currentPkg = null;
            if (!string.IsNullOrEmpty(errRecord))
            {
                return currentPkg;
            }

            return currentPkg;
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag(s) from a repository and returns latest version for each.
        /// Examples: Search -Tag "JSON" -Repository PSGallery
        /// API call: 
        /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='tag:JSON'
        /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&searchTerm='tag:JSON'&includePrerelease=true
        /// </summary>
        public PSResourceInfo[] FindTags(string[] tags, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out HashSet<string> tagsFound, out string errRecord)
        {
            errRecord = String.Empty;
            List<PSResourceInfo> pkgsFound = new List<PSResourceInfo>(); 
            HashSet<string> tagPkgs = new HashSet<string>();   
            tagsFound = new HashSet<string>();   
            int skip = 0;

            // TAG example:
            // chocolatey, crescendo 
            //  >  chocolatey  ===  ModuleA
            //  >  crescendo   ===  ModuleA
            // --->   for tags get rid of duplicate modules             
            foreach (string tag in tags)
            {
                string[] responses = v2ServerAPICall.FindTag(tag, repository, includePrerelease, type, skip, out errRecord);

                foreach (string response in responses)
                {
                    var elemList = ConvertResponseToXML(response);
                    
                    foreach (var element in elemList)
                    {
                        PSResourceInfo.TryConvertFromXml(
                            element,
                            includePrerelease,
                            out PSResourceInfo psGetInfo,
                            repository.Name,
                            out string errorMsg);

                        if (psGetInfo != null && !tagPkgs.Contains(psGetInfo.Name))
                        {
                            tagPkgs.Add(psGetInfo.Name);
                            pkgsFound.Add(psGetInfo);
                            tagsFound.Add(tag);
                        }
                        else 
                        {
                            // TODO: Write error for corresponding null scenario
                            // TODO: array out of bounds exception when name does not exist
                            // http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='tag:PSCommand_Get-TargetResource'
                            errRecord = errorMsg;
                        }
                    }
                }
            }

            return pkgsFound.ToArray();
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag(s) from a repository and returns latest version for each.
        /// Examples: Search -Tag "JSON" -Repository PSGallery
        /// API call: 
        /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='tag:JSON'
        /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&searchTerm='tag:JSON'&includePrerelease=true
        /// </summary>
        public PSCommandResourceInfo[] FindCommandOrDscResource(string[] tags, PSRepositoryInfo repository, bool includePrerelease, bool isSearchingForCommands, out string errRecord)
        {
            errRecord = String.Empty;
            Hashtable pkgHash = new Hashtable();   
            List<PSCommandResourceInfo> cmdInfoObjs = new List<PSCommandResourceInfo>();

            // COMMAND example:
            // command1, command2 
            //  >  command1  ===  ModuleA
            //  >  command2   ===  ModuleA            
            //
            //  >  command1, command2   ===  ModuleA

            foreach (string tag in tags)
            {
                string response = v2ServerAPICall.FindCommandOrDscResource(tag, repository, includePrerelease, isSearchingForCommands, out errRecord);

                var elemList = ConvertResponseToXML(response);
                
                foreach (var element in elemList)
                {
                    PSResourceInfo.TryConvertFromXml(
                        element,
                        includePrerelease,
                        out PSResourceInfo psGetInfo,
                        repository.Name,
                        out string errorMsg);

                    if (psGetInfo != null )
                    {
                        // Map the tag with the package which the tag came from 
                        if (!pkgHash.Contains(psGetInfo.Name))
                        {
                            pkgHash.Add(psGetInfo.Name, Tuple.Create<List<string>, PSResourceInfo>(new List<string> { tag }, psGetInfo)); 
                        }
                        else {
                            // if the package is already in the hashtable, add this tag to the list of tags associated with that package
                            Tuple<List<string>, PSResourceInfo> hashValue = (Tuple<List<string>, PSResourceInfo>)pkgHash[psGetInfo.Name];
                            hashValue.Item1.Add(tag);
                        }
                    }
                    else 
                    {
                        // TODO: Write error for corresponding null scenario
                        // TODO: array out of bounds exception when name does not exist
                        // http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='tag:PSCommand_Get-TargetResource'
                        errRecord = errorMsg;
                    }
                }               
            }

            // convert hashtable to PSCommandInfo
            foreach(DictionaryEntry pkg in pkgHash)
            {
                Tuple<List<string>, PSResourceInfo> hashValue = (Tuple<List<string>, PSResourceInfo>) pkg.Value;

                cmdInfoObjs.Add(new PSCommandResourceInfo(hashValue.Item1.ToArray(), hashValue.Item2));
            }

            return cmdInfoObjs.ToArray();
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
        public PSResourceInfo FindName(string packageName, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out string errRecord)
        {
            errRecord = string.Empty;

            if (repository.ApiVersion == PSRepositoryInfo.APIVersion.v2)
            {
                // Same API calls for both prerelease and non-prerelease
                var response = v2ServerAPICall.FindName(packageName, repository, includePrerelease, type, out errRecord);

                var elemList = ConvertResponseToXML(response);

                if (elemList.Length == 0)
                {
                    Console.WriteLine("empty response. Error handle");
                }

        
                PSResourceInfo.TryConvertFromXml(
                    elemList[0],
                    includePrerelease,
                    out PSResourceInfo psGetInfo,
                    repository.Name,
                    out string errorMsg);

                if (psGetInfo != null)
                {
                    return psGetInfo;
                }
                else 
                {
                    // TODO: Write error for corresponding null scenario
                    errRecord = errorMsg;
                }
            

            }
            else if (repository.ApiVersion == PSRepositoryInfo.APIVersion.v3)
            {
                // TODO: handle V3 endpoints, test for NuGetGallery
            }

            return null;
        }

        /// <summary>
        /// Find method which allows for searching for single name with wildcards and returns latest version.
        /// Name: supports wildcards
        /// Examples: Search "PowerShell*"
        /// API call: 
        /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='az*'
        /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&searchTerm='az*'&includePrerelease=true
        /// Implementation Note: filter additionally and verify ONLY package name was a match.
        /// </summary>
        public PSResourceInfo[] FindNameGlobbing(string packageName, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out string errRecord)
        {
            List<string> responses = new List<string>();
            int skip = 0;

            var initialResponse = v2ServerAPICall.FindNameGlobbing(packageName, repository, includePrerelease, type, skip, out errRecord);
            responses.Add(initialResponse);

            // check count (regex)  425 ==> count/100  ~~>  4 calls 
            int initalCount = GetCountFromResponse(initialResponse);  // count = 4
            int count = initalCount / 100;
            // if more than 100 count, loop and add response to list
            while (count > 0)
            {
                // skip 100
                skip += 100;
                var tmpResponse = v2ServerAPICall.FindNameGlobbing(packageName, repository, includePrerelease, type, skip, out errRecord);
                responses.Add(tmpResponse);
                count--;
            }

            List<PSResourceInfo> pkgsFound = new List<PSResourceInfo>(); // TODO: discuss if we want to yield return here for better performance

            foreach (string response in responses)
            {
                var elemList = ConvertResponseToXML(response);
                foreach (var element in elemList)
                {
                    PSResourceInfo.TryConvertFromXml(
                        element,
                        includePrerelease,
                        out PSResourceInfo psGetInfo,
                        repository.Name,
                        out string errorMsg);

                    if (psGetInfo != null)
                    {
                        pkgsFound.Add(psGetInfo);
                    }
                    else 
                    {
                        // TODO: Write error for corresponding null scenario
                        errRecord = errorMsg;
                    }
                }
            }

            return pkgsFound.ToArray();
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
        public PSResourceInfo[] FindVersionGlobbing(string packageName, VersionRange versionRange, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, out string errRecord)
        {
            List<string> responses = new List<string>();
            int skip = 0;

            var initialResponse = v2ServerAPICall.FindVersionGlobbing(packageName, versionRange, repository, includePrerelease, type, skip, out errRecord);
            responses.Add(initialResponse);

            int initalCount = GetCountFromResponse(initialResponse);
            int count = initalCount / 100;

            while (count > 0)
            {
                // skip 100
                skip += 100;
                var tmpResponse = v2ServerAPICall.FindVersionGlobbing(packageName, versionRange, repository, includePrerelease, type, skip, out errRecord);
                responses.Add(tmpResponse);
                count--;
            }

            List<PSResourceInfo> pkgsFound = new List<PSResourceInfo>(); 
            
            foreach (string response in responses)
            {
                var elemList = ConvertResponseToXML(response);
                foreach (var element in elemList)
                {
                    PSResourceInfo.TryConvertFromXml(
                        element,
                        includePrerelease,
                        out PSResourceInfo psGetInfo,
                        repository.Name,
                        out string errorMsg);

                    if (psGetInfo != null)
                    {
                        pkgsFound.Add(psGetInfo);
                    }
                    else 
                    {
                        // TODO: Write error for corresponding null scenario
                        errRecord = errorMsg;
                    }
                }
            }

            return pkgsFound.ToArray();
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5"
        /// API call: http://www.powershellgallery.com/api/v2/Packages(Id='PowerShellGet', Version='2.2.5')
        /// </summary>
        public PSResourceInfo FindVersion(string packageName, string version, PSRepositoryInfo repository, ResourceType type, out string errRecord)
        {
            errRecord = string.Empty;

            if (repository.ApiVersion == PSRepositoryInfo.APIVersion.v2)
            {
                // Same API calls for both prerelease and non-prerelease
                var response = v2ServerAPICall.FindVersion(packageName, version, repository, type, out errRecord);

                var elemList = ConvertResponseToXML(response);

                if (elemList.Length == 0)
                {
                    Console.WriteLine("empty response. Error handle");
                }


                PSResourceInfo.TryConvertFromXml(
                    elemList[0],
                    false, // TODO: confirm, but this seems to only apply for FindName() cases
                    out PSResourceInfo psGetInfo,
                    repository.Name,
                    out string errorMsg);

                if (psGetInfo != null)
                {
                    return psGetInfo;
                }
                else
                {
                    // TODO: Write error for corresponding null scenario
                    errRecord = errorMsg;
                }
            }
            else if (repository.ApiVersion == PSRepositoryInfo.APIVersion.v3)
            {
                // TODO: handle V3 endpoints, test for NuGetGallery
            }

            return null;
        }
        
        #endregion

        #region HelperMethods

        public XmlNode[] ConvertResponseToXML(string httpResponse) {

            //Create the XmlDocument.
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(httpResponse);

            XmlNodeList elemList = doc.GetElementsByTagName("m:properties");
            
            XmlNode[] nodes = new XmlNode[elemList.Count]; 
            for (int i=0; i<elemList.Count; i++) 
            {
                nodes[i] = elemList[i]; 
            }

            return nodes;
        }

        public int GetCountFromResponse(string httpResponse) {
            int count = 0;

            //Create the XmlDocument.
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(httpResponse);

            XmlNodeList elemList = doc.GetElementsByTagName("m:count");            
            if (elemList.Count > 0) {
                XmlNode node = elemList[0];
                count = int.Parse(node.InnerText);
            }
            
            return count;
        }

        #endregion
    }
}
