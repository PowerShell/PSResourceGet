using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using System.Xml;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    internal class HttpFindPSResource : IFindPSResource
    {
        #region Members
        
        readonly V2ServerAPICalls v2ServerAPICall = new V2ServerAPICalls();
        readonly V3ServerAPICalls v3ServerAPICall = new V3ServerAPICalls();

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
        public IEnumerable<PSResourceResult> FindAll(PSRepositoryInfo repository, bool includePrerelease, ResourceType type)
        {
            string[] responses = v2ServerAPICall.FindAll(repository, includePrerelease, type, out string errRecord);
            if (!String.IsNullOrEmpty(errRecord))
            {
                yield return new PSResourceResult(returnedObj: null, errorMsg: errRecord, isTerminatingError: false);
            }

            foreach (string response in responses)
            {
                var elemList = ConvertResponseToXML(response);

                foreach (var element in elemList)
                {
                    if (!PSResourceInfo.TryConvertFromXml(element, out PSResourceInfo psGetInfo, repository.Name, out string errorMsg)) {
                        yield return new PSResourceResult(returnedObj: null, errorMsg: errorMsg, isTerminatingError: false);
                    }

                    yield return new PSResourceResult(returnedObj: psGetInfo, errorMsg: String.Empty, isTerminatingError: false);
                }
            }
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag(s) from a repository and returns latest version for each.
        /// Examples: Search -Tag "JSON" -Repository PSGallery
        /// API call: 
        /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='tag:JSON'
        /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&searchTerm='tag:JSON'&includePrerelease=true
        /// </summary>
        public IEnumerable<PSResourceResult> FindTags(string[] tags, PSRepositoryInfo repository, bool includePrerelease, ResourceType type)
        {
            HashSet<string> tagPkgs = new HashSet<string>();

            if (repository.ApiVersion == PSRepositoryInfo.APIVersion.v2)
            {
                yield return FindTagsV2(tags, repository, includePrerelease, type, tagPkgs).First();
            }
            else if (repository.ApiVersion == PSRepositoryInfo.APIVersion.v3)
            {
                yield return FindTagsV3(tags, repository, includePrerelease, type, tagPkgs).First();
            }
        }

        /// <summary>
        /// Find method which allows for searching for packages with tag(s) from a repository and returns latest version for each.
        /// Examples: Search -Tag "JSON" -Repository PSGallery
        /// API call: 
        /// - No prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsLatestVersion&searchTerm='tag:JSON'
        /// - Include prerelease: http://www.powershellgallery.com/api/v2/Search()?$filter=IsAbsoluteLatestVersion&searchTerm='tag:JSON'&includePrerelease=true
        /// </summary>
        public IEnumerable<PSResourceResult> FindCommandOrDscResource(string[] tags, PSRepositoryInfo repository, bool includePrerelease, bool isSearchingForCommands)
        {
            if (repository.Name.Equals("PSGallery", StringComparison.OrdinalIgnoreCase)) {
                // error out
            }

            yield return FindCommandOrDSCResourceV2(tags, repository, includePrerelease, isSearchingForCommands).First();
        }

        // Complete
        /// <summary>
        /// Find method which allows for searching for single name and returns latest version.
        /// Name: no wildcard support
        /// Examples: Search "PowerShellGet"
        /// API call: 
        /// - No prerelease: http://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'
        /// - Include prerelease: http://www.powershellgallery.com/api/v2/FindPackagesById()?id='PowerShellGet'
        /// Implementation Note: Need to filter further for latest version (prerelease or non-prerelease dependening on user preference)
        /// </summary>
        public IEnumerable<PSResourceResult> FindName(string packageName, PSRepositoryInfo repository, bool includePrerelease, ResourceType type)
        {
            if (repository.ApiVersion == PSRepositoryInfo.APIVersion.v2)
            {
                yield return FindNameV2(packageName, repository, includePrerelease, type).First();
            }
            else if (repository.ApiVersion == PSRepositoryInfo.APIVersion.v3)
            {
                yield return FindNameV3(packageName, repository, includePrerelease, type).First();
            }

            yield break;
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
        public IEnumerable<PSResourceResult> FindNameGlobbing(string packageName, PSRepositoryInfo repository, bool includePrerelease, ResourceType type)
        {
            if (repository.ApiVersion == PSRepositoryInfo.APIVersion.v2)
            {
                yield return FindNameGlobbingV2(packageName, repository, includePrerelease, type).First();
            }
            else if (repository.ApiVersion == PSRepositoryInfo.APIVersion.v3)
            {
                yield return FindNameGlobbingV3(packageName, repository, includePrerelease, type).First();
            }

            yield break;
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
        public IEnumerable<PSResourceResult> FindVersionGlobbing(string packageName, VersionRange versionRange, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, bool getOnlyLatest)
        {
            if (repository.ApiVersion == PSRepositoryInfo.APIVersion.v2)
            {
                foreach (string response in v2ServerAPICall.FindVersionGlobbing(packageName, versionRange, repository, includePrerelease, type, getOnlyLatest, out string errRecord))
                {
                    if (!String.IsNullOrEmpty(errRecord))
                    {
                        yield return new PSResourceResult(returnedObj: null, errorMsg: errRecord, isTerminatingError: false);
                    }

                    var elemList = ConvertResponseToXML(response);
                    foreach (var element in elemList)
                    {
                        if (!PSResourceInfo.TryConvertFromXml(element, out PSResourceInfo psGetInfo, repository.Name, out string errorMsg)) 
                        {
                            yield return new PSResourceResult(returnedObj: null, errorMsg: errorMsg, isTerminatingError: false);
                        }

                        yield return new PSResourceResult(returnedObj: psGetInfo, errorMsg: String.Empty, isTerminatingError: false);
                    }
                }
            }
            else if (repository.ApiVersion == PSRepositoryInfo.APIVersion.v3)
            {
                string[] responses = v3ServerAPICall.FindVersionGlobbing(packageName, versionRange, repository, includePrerelease, type, getOnlyLatest, out string errMsg);
                
                if (!String.IsNullOrEmpty(errMsg))
                {
                    yield return new PSResourceResult(returnedObj: null, errorMsg: errMsg, isTerminatingError: false);
                }

                // convert response to json document
                foreach (string response in responses)
                {
                    string parseError = String.Empty;
                    JsonDocument pkgVersionEntry = null;
                    try
                    {
                        pkgVersionEntry = JsonDocument.Parse(response);
                    }
                    catch (Exception e) {
                        parseError = e.Message;
                    }

                    if (!String.IsNullOrEmpty(parseError))
                    {
                        yield return new PSResourceResult(returnedObj: null, errorMsg: parseError, isTerminatingError: false);
                    }

                    if (!PSResourceInfo.TryConvertFromJson(pkgVersionEntry, out PSResourceInfo psGetInfo, repository.Name, out string errorMsg))
                    {
                        yield return new PSResourceResult(returnedObj: null, errorMsg: errorMsg, isTerminatingError: false);
                    }

                    yield return new PSResourceResult(returnedObj: psGetInfo, errorMsg: String.Empty, isTerminatingError: false);
                }
            }

            yield break;
        }

        /// <summary>
        /// Find method which allows for searching for single name with specific version.
        /// Name: no wildcard support
        /// Version: no wildcard support
        /// Examples: Search "PowerShellGet" "2.2.5"
        /// API call: http://www.powershellgallery.com/api/v2/Packages(Id='PowerShellGet', Version='2.2.5')
        /// </summary>
        public IEnumerable<PSResourceResult> FindVersion(string packageName, string version, PSRepositoryInfo repository, ResourceType type)
        {
            if (repository.ApiVersion == PSRepositoryInfo.APIVersion.v2)
            {
                // Same API calls for both prerelease and non-prerelease
                var response = v2ServerAPICall.FindVersion(packageName, version, repository, type, out string errMsg);

                if (!String.IsNullOrEmpty(errMsg))
                {
                    yield return new PSResourceResult(returnedObj: null, errorMsg: errMsg, isTerminatingError: false);
                }

                var elemList = ConvertResponseToXML(response);

                if (elemList.Length == 0)
                {
                    throw new Exception("Response could not be parsed into XML.");
                }

                if (!PSResourceInfo.TryConvertFromXml(elemList[0], out PSResourceInfo psGetInfo, repository.Name, out string errorMsg))
                {
                    yield return new PSResourceResult(returnedObj: null, errorMsg: errorMsg, isTerminatingError: false);
                }

                yield return new PSResourceResult(returnedObj: psGetInfo, errorMsg: String.Empty, isTerminatingError: false);

            }
            else if (repository.ApiVersion == PSRepositoryInfo.APIVersion.v3)
            {
                string response = v3ServerAPICall.FindVersion(packageName, version, repository, type, out string errMsg);

                if (!String.IsNullOrEmpty(errMsg))
                {
                    yield return new PSResourceResult(returnedObj: null, errorMsg: errMsg, isTerminatingError: false);
                }

                string parseError = String.Empty;
                JsonDocument pkgVersionEntry = null;
                try
                {
                    pkgVersionEntry = JsonDocument.Parse(response);
                }
                catch (Exception e) {
                    parseError = e.Message;
                }

                if (!String.IsNullOrEmpty(parseError))
                {
                    yield return new PSResourceResult(returnedObj: null, errorMsg: parseError, isTerminatingError: false);
                }

                if (!PSResourceInfo.TryConvertFromJson(pkgVersionEntry, out PSResourceInfo psGetInfo, repository.Name, out string errorMsg))
                {
                    yield return new PSResourceResult(returnedObj: null, errorMsg: errorMsg, isTerminatingError: false);
                }

                yield return new PSResourceResult(returnedObj: psGetInfo, errorMsg: String.Empty, isTerminatingError: false);
            }

            yield break;
        }

        #endregion

        #region HelperMethods

        private IEnumerable<PSResourceResult> FindTagsV2(string[] tags, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, HashSet<string> tagPkgs) {
            // TAG example:
            // chocolatey, crescendo 
            //  >  chocolatey  ===  ModuleA
            //  >  crescendo   ===  ModuleA
            // --->   for tags get rid of duplicate modules             
            foreach (string tag in tags)
            {
                string[] responses = v2ServerAPICall.FindTag(tag, repository, includePrerelease, type, out string errRecord);
                if (!String.IsNullOrEmpty(errRecord))
                {
                    yield return new PSResourceResult(returnedObj: null, errorMsg: errRecord, isTerminatingError: false);
                }

                foreach (string response in responses)
                {
                    var elemList = ConvertResponseToXML(response);

                    foreach (var element in elemList)
                    {
                        if (!PSResourceInfo.TryConvertFromXml(element, out PSResourceInfo psGetInfo, repository.Name, out string errorMsg))
                        {
                            yield return new PSResourceResult(returnedObj: null, errorMsg: errorMsg, isTerminatingError: false);
                        }

                        if (psGetInfo != null && !tagPkgs.Contains(psGetInfo.Name))
                        {
                            tagPkgs.Add(psGetInfo.Name);

                            yield return new PSResourceResult(returnedObj: psGetInfo, errorMsg: String.Empty, isTerminatingError: false);
                        }
                    }
                }
            }
        }

        private IEnumerable<PSResourceResult> FindTagsV3(string[] tags, PSRepositoryInfo repository, bool includePrerelease, ResourceType type, HashSet<string> tagPkgs)
        {
            // TAG example:
            // chocolatey, crescendo 
            //  >  chocolatey  ===  ModuleA
            //  >  crescendo   ===  ModuleA
            // --->   for tags get rid of duplicate modules             
            foreach (string tag in tags)
            {
                string[] responses = v3ServerAPICall.FindTag(tag, repository, includePrerelease, type, out string errMsg);
                if (!String.IsNullOrEmpty(errMsg))
                {
                    yield return new PSResourceResult(returnedObj: null, errorMsg: errMsg, isTerminatingError: false);
                }

                foreach (string response in responses)
                {
                    string parseError = String.Empty;
                    JsonDocument pkgEntry = null;
                    try
                    {
                        pkgEntry = JsonDocument.Parse(response);
                    }
                    catch (Exception e)
                    {
                        parseError = e.Message;
                    }

                    if (!String.IsNullOrEmpty(parseError))
                    {
                        yield return new PSResourceResult(returnedObj: null, errorMsg: parseError, isTerminatingError: false);
                    }

                    if (!PSResourceInfo.TryConvertFromJson(pkgEntry, out PSResourceInfo psGetInfo, repository.Name, out string errorMsg))
                    {
                        yield return new PSResourceResult(returnedObj: null, errorMsg: errorMsg, isTerminatingError: false);
                    }

                    if (psGetInfo != null && !tagPkgs.Contains(psGetInfo.Name))
                    {
                        tagPkgs.Add(psGetInfo.Name);

                        yield return new PSResourceResult(returnedObj: psGetInfo, errorMsg: String.Empty, isTerminatingError: false);
                    }
                }
            }
        }

        private IEnumerable<PSResourceResult> FindCommandOrDSCResourceV2(string[] tags, PSRepositoryInfo repository, bool includePrerelease, bool isSearchingForCommands) {
            Hashtable pkgHash = new Hashtable();

            // COMMAND example:
            // command1, command2 
            //  >  command1  ===  ModuleA
            //  >  command2   ===  ModuleA            
            //
            //  >  command1, command2   ===  ModuleA

            foreach (string tag in tags)
            {
                string[] responses = v2ServerAPICall.FindCommandOrDscResource(tag, repository, includePrerelease, isSearchingForCommands, out string errRecord);

                if (!String.IsNullOrEmpty(errRecord))
                {
                    throw new Exception(errRecord);
                }

                foreach (string response in responses)
                {
                    var elemList = ConvertResponseToXML(response);

                    foreach (var element in elemList)
                    {
                        if (!PSResourceInfo.TryConvertFromXml(element, out PSResourceInfo psGetInfo, repository.Name, out string errorMsg))
                        {
                            throw new Exception(errorMsg);
                        }

                        if (psGetInfo != null)
                        {
                            // Map the tag with the package which the tag came from 
                            if (!pkgHash.Contains(psGetInfo.Name))
                            {
                                pkgHash.Add(psGetInfo.Name, Tuple.Create<List<string>, PSResourceInfo>(new List<string> { tag }, psGetInfo));
                            }
                            else
                            {
                                // if the package is already in the hashtable, add this tag to the list of tags associated with that package
                                Tuple<List<string>, PSResourceInfo> hashValue = (Tuple<List<string>, PSResourceInfo>)pkgHash[psGetInfo.Name];
                                hashValue.Item1.Add(tag);
                            }
                        }
                    }
                }
            }

            // convert hashtable to PSCommandInfo
            foreach (DictionaryEntry pkg in pkgHash)
            {
                Tuple<List<string>, PSResourceInfo> hashValue = (Tuple<List<string>, PSResourceInfo>)pkg.Value;

                var psGetCmdInfo = new PSCommandResourceInfo(hashValue.Item1.ToArray(), hashValue.Item2);
                yield return new PSResourceResult(returnedCmdObject: psGetCmdInfo, errorMsg: String.Empty, isTerminatingError: false);

            }
        }

        private IEnumerable<PSResourceResult> FindNameV2(string packageName, PSRepositoryInfo repository, bool includePrerelease, ResourceType type) {
            // Same API calls for both prerelease and non-prerelease
            var response = v2ServerAPICall.FindName(packageName, repository, includePrerelease, type, out string errMsg);
            if (!String.IsNullOrEmpty(errMsg))
            {
                yield return new PSResourceResult(returnedObj: null, errorMsg: errMsg, isTerminatingError: false);
            }

            var elemList = ConvertResponseToXML(response);

            if (elemList.Length == 0)
            {
                string xmlError = "Response could not be parsed into XML.";
                yield return new PSResourceResult(returnedObj: null, errorMsg: xmlError, isTerminatingError: false);
            }


            if (!PSResourceInfo.TryConvertFromXml(elemList[0], out PSResourceInfo psGetInfo, repository.Name, out string errorMsg))
            {
                yield return new PSResourceResult(returnedObj: null, errorMsg: errorMsg, isTerminatingError: false);
            }

            yield return new PSResourceResult(returnedObj: psGetInfo, errorMsg: String.Empty, isTerminatingError: false);
        }

        private IEnumerable<PSResourceResult> FindNameV3(string packageName, PSRepositoryInfo repository, bool includePrerelease, ResourceType type) {
            // Same API calls for both prerelease and non-prerelease
            var response = v3ServerAPICall.FindName(packageName, repository, includePrerelease, type, out string errMsg);
            if (!String.IsNullOrEmpty(errMsg))
            {
                yield return new PSResourceResult(returnedObj: null, errorMsg: errMsg, isTerminatingError: false);
            }

            string parseError = String.Empty;
            JsonDocument pkgVersionEntry = null;
            try
            {
                pkgVersionEntry = JsonDocument.Parse(response);
            }
            catch (Exception e)
            {
                parseError = e.Message;
            }

            if (!String.IsNullOrEmpty(parseError))
            {
                yield return new PSResourceResult(returnedObj: null, errorMsg: parseError, isTerminatingError: false);
            }

            if (!PSResourceInfo.TryConvertFromJson(pkgVersionEntry, out PSResourceInfo psGetInfo, repository.Name, out string errorMsg))
            {
                yield return new PSResourceResult(returnedObj: null, errorMsg: errorMsg, isTerminatingError: false);
            }

            yield return new PSResourceResult(returnedObj: psGetInfo, errorMsg: String.Empty, isTerminatingError: false);

        }

        private IEnumerable<PSResourceResult> FindNameGlobbingV2(string packageName, PSRepositoryInfo repository, bool includePrerelease, ResourceType type) {
            string[] responses = v2ServerAPICall.FindNameGlobbing(packageName, repository, includePrerelease, type, out string errRecord);
            if (!String.IsNullOrEmpty(errRecord))
            {
                yield return new PSResourceResult(returnedObj: null, errorMsg: errRecord, isTerminatingError: false);
            }

            foreach (string response in responses)
            {
                var elemList = ConvertResponseToXML(response);
                foreach (var element in elemList)
                {
                    if (!PSResourceInfo.TryConvertFromXml(element, out PSResourceInfo psGetInfo, repository.Name, out string errorMsg))
                    {
                        yield return new PSResourceResult(returnedObj: null, errorMsg: errorMsg, isTerminatingError: false);
                    }

                    yield return new PSResourceResult(returnedObj: psGetInfo, errorMsg: String.Empty, isTerminatingError: false);
                }
            }
        }

        private IEnumerable<PSResourceResult> FindNameGlobbingV3(string packageName, PSRepositoryInfo repository, bool includePrerelease, ResourceType type) {
            string[] responses = v3ServerAPICall.FindNameGlobbing(packageName, repository, includePrerelease, type, out string errMsg);

            if (!String.IsNullOrEmpty(errMsg))
            {
                yield return new PSResourceResult(returnedObj: null, errorMsg: errMsg, isTerminatingError: false);
            }

            // convert response to json document
            foreach (string response in responses)
            {
                string parseError = String.Empty;
                JsonDocument pkgVersionEntry = null;
                try
                {
                    pkgVersionEntry = JsonDocument.Parse(response);
                }
                catch (Exception e)
                {
                    parseError = e.Message;
                }

                if (!String.IsNullOrEmpty(parseError))
                {
                    yield return new PSResourceResult(returnedObj: null, errorMsg: parseError, isTerminatingError: false);
                }

                if (!PSResourceInfo.TryConvertFromJson(pkgVersionEntry, out PSResourceInfo psGetInfo, repository.Name, out string errorMsg))
                {
                    yield return new PSResourceResult(returnedObj: null, errorMsg: errorMsg, isTerminatingError: false);
                }

                yield return new PSResourceResult(returnedObj: psGetInfo, errorMsg: String.Empty, isTerminatingError: false);
            }
        }
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
