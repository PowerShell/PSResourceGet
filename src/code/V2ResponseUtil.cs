// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
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

        #region Overriden Methods
        public override IEnumerable<PSResourceResult> ConvertToPSResourceResult(FindResults responseResults)
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

                    // Unlisted versions will have a published year as 1900 or earlier.
                    if (!psGetInfo.PublishedDate.HasValue || psGetInfo.PublishedDate.Value.Year > 1900)
                    {
                        yield return new PSResourceResult(returnedObject: psGetInfo, exception: null, isTerminatingError: false);
                    }
                }
            }
        }

        #endregion

        #region V2 Specific Methods

        public XmlNode[] ConvertResponseToXML(string httpResponse) {
            NuGetVersion emptyVersion = new NuGetVersion("0.0.0.0");
            NuGetVersion firstVersion = emptyVersion;
            NuGetVersion lastVersion = emptyVersion;

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
                                    parsedNormalizedVersion = emptyVersion;
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

            // only order the array in desc order if array has more than 1 element and is currently in ascending order
            // check for emptyVersion is in case a version that couldn't be parsed was found, just keep ordering as is.
            if (nodes.Length > 1 && firstVersion != emptyVersion && lastVersion != emptyVersion && firstVersion < lastVersion)
            {
                nodes = nodes.Reverse().ToArray();
            }

            return nodes;
        }

        #endregion
    }
}
