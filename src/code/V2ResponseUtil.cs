// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Xml;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    internal class V2ResponseUtil : ResponseUtil
    {
        #region Members

        internal override PSRepositoryInfo Repository { get; set; }

        #endregion

        #region Constructor

        public V2ResponseUtil(PSRepositoryInfo repository) : base(repository)
        {
            this.Repository = repository;
        }

        #endregion

        #region Overridden Methods
        public override IEnumerable<PSResourceResult> ConvertToPSResourceResult(FindResults responseResults, bool isResourceRequestedWithWildcard = false)
        {
            // in FindHelper:
            // serverApi.FindName() -> return responses, and out errRecord
            // check outErrorRecord
            //
            // v2Converter.ConvertToPSResourceInfo(responses) -> return PSResourceResult
            // check resourceResult for error, write if needed
            string[] responses = responseResults.StringResponse;

            foreach (string response in responses)
            {
                // if (Repository.Uri.AbsoluteUri.Contains("www.powershellgallery.com"))
                // {
                //     string responseToConvert = response.Trim('\"').Replace("\\n", "").Replace("\\r", "");
                //     if (!PSResourceInfo.TryConvertXmlFromGraphQL(responseToConvert, out PSResourceInfo psGetInfo, Repository, out string errorMsg))
                //     {
                //         Exception parseException = new XmlParsingException(errorMsg);

                //         yield return new PSResourceResult(returnedObject: null, exception: parseException, isTerminatingError: false);
                //     }

                //     yield return new PSResourceResult(returnedObject: psGetInfo, exception: null, isTerminatingError: false);
                // }
                if (Repository.Uri.AbsoluteUri.Contains("www.powershellgallery.com"))
                {
                    string responseToConvert = response.Trim('\"').Replace("\\n", "").Replace("\\r", "");
                    var elemList = ConvertGraphQLResponseToXML(responseToConvert);
                    if (elemList.Length == 0)
                    {
                        // this indicates we got a non-empty, XML response (as noticed for V2 server) but it's not a response that's meaningful (contains 'properties')
                        Exception notFoundException = new ResourceNotFoundException("Package does not exist on the server");

                        yield return new PSResourceResult(returnedObject: null, exception: notFoundException, isTerminatingError: false);
                    }

                    foreach(var element in elemList)
                    {
                        if (!PSResourceInfo.TryConvertXmlFromGraphQL(element, out PSResourceInfo psGetInfo, Repository, out string errorMsg))
                        {
                            Exception parseException = new XmlParsingException(errorMsg);

                            yield return new PSResourceResult(returnedObject: null, exception: parseException, isTerminatingError: false);
                        }

                        yield return new PSResourceResult(returnedObject: psGetInfo, exception: null, isTerminatingError: false);
                    }
                }
                else
                {
                    var elemList = ConvertResponseToXML(response);
                    if (elemList.Length == 0)
                    {
                        // this indicates we got a non-empty, XML response (as noticed for V2 server) but it's not a response that's meaningful (contains 'properties')
                        Exception notFoundException = new ResourceNotFoundException("Package does not exist on the server");

                        yield return new PSResourceResult(returnedObject: null, exception: notFoundException, isTerminatingError: false);
                    }

                    foreach (var element in elemList)
                    {
                        if (!PSResourceInfo.TryConvertFromXml(element, out PSResourceInfo psGetInfo, Repository, out string errorMsg))
                        {
                            Exception parseException = new XmlParsingException(errorMsg);

                            yield return new PSResourceResult(returnedObject: null, exception: parseException, isTerminatingError: false);
                        }

                        // For V2 resources, specifically PSGallery, return unlisted version resources only when not requested with wildcard name
                        // Unlisted versions will have a published year as 1900 or earlier.
                        if (!isResourceRequestedWithWildcard || !psGetInfo.PublishedDate.HasValue || psGetInfo.PublishedDate.Value.Year > 1900)
                        {
                            yield return new PSResourceResult(returnedObject: psGetInfo, exception: null, isTerminatingError: false);
                        }
                    }
                }
            }
        }

        #endregion

        #region V2 Specific Methods

        public XmlNode[] ConvertResponseToXML(string httpResponse)
        {
            NuGetVersion emptyVersion = new NuGetVersion("0.0.0.0");
            NuGetVersion firstVersion = emptyVersion;
            NuGetVersion lastVersion = emptyVersion;
            bool shouldFixOrder = true;

            //Create the XmlDocument.
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(httpResponse);

            XmlNodeList entryNode = doc.GetElementsByTagName("entry");

            XmlNode[] nodes = new XmlNode[entryNode.Count];
            for (int i = 0; i < entryNode.Count; i++)
            {
                XmlNode node = entryNode[i];
                nodes[i] = node;
                var entryChildNodes = node.ChildNodes;
                foreach (XmlElement childNode in entryChildNodes)
                {
                    var entryKey = childNode.LocalName;
                    if (entryKey.Equals("properties"))
                    {
                        var propertyChildNodes = childNode.ChildNodes;
                        foreach (XmlElement propertyChild in propertyChildNodes)
                        {
                            var propertyKey = propertyChild.LocalName;
                            var propertyValue = propertyChild.InnerText;
                            if (propertyKey.Equals("NormalizedVersion"))
                            {
                                if (!NuGetVersion.TryParse(propertyValue, out NuGetVersion parsedNormalizedVersion))
                                {
                                    // if a version couldn't be parsed, keep ordering as is.
                                    shouldFixOrder = false;
                                }

                                if (i == 0)
                                {
                                    firstVersion = parsedNormalizedVersion;
                                }
                                else
                                {
                                    // later version element
                                    lastVersion = parsedNormalizedVersion;
                                }

                                break; // don't care about rest of the childNode's properties
                            }
                        }

                        break; // don't care about rest of the childNode's keys
                    }
                }
            }

            // order the array in descending order if not already.
            if (shouldFixOrder && firstVersion.CompareTo(lastVersion) < 0)
            {
                nodes = nodes.Reverse().ToArray();
            }

            return nodes;
        }


        public XmlNode[] ConvertGraphQLResponseToXML(string graphQLResponse)
        {
            List<XmlNode> nodeList = new List<XmlNode>();
            // GraphQL response is a stringified XML, so trim quotes and remove extra space and newline escaped characters
            string responseToConvert = graphQLResponse.Trim('\"').Replace("\\n", "").Replace("\\r", "");

            // root
            //      operationName1
            //          pkgId
            //          Package
            //      operationName2
            //          pkgId
            //          Package

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(responseToConvert);
            XmlElement rootElement = doc.DocumentElement;
            if (!rootElement.HasChildNodes)
            {
                return nodeList.ToArray(); // TODO some error message?
            }

            // operationName based element, there will be as many nodes as there were responses found (i.e if 2 packages matched, 2 operationName based elements)
            var entryElements = rootElement.ChildNodes;
            int entriesFound = entryElements.Count;

            foreach (XmlNode entry in entryElements)
            {
                if (entry.HasChildNodes)
                {
                    // this node's child nodes will contain the metadata
                    nodeList.Add(entry);
                }
                else
                {
                    continue; // what if FindName "existant", "nonExistant" returns <packageName> <metadata> </packageName> <packageName /> -- more testing needed
                }
            }

            return nodeList.ToArray();
        }
        #endregion
    }
}
