// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.Xml;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    internal class NuGetServerResponseUtil : ResponseUtil
    {
        #region Members

        internal override PSRepositoryInfo Repository { get; set; }

        #endregion

        #region Constructor

        public NuGetServerResponseUtil(PSRepositoryInfo repository) : base(repository)
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
                    Exception notFoundException = new V2ResourceNotFoundException("Package does not exist on the server");

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

        #region NuGet.Server Specific Methods

        public XmlNode[] ConvertResponseToXML(string httpResponse) {

            //Create the XmlDocument.
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(httpResponse);

            XmlNodeList elemList = doc.GetElementsByTagName("m:properties");
            
            XmlNode[] nodes = new XmlNode[elemList.Count]; 
            for (int i = 0; i < elemList.Count; i++) 
            {
                nodes[i] = elemList[i]; 
            }

            return nodes;
        }

        #endregion
    }
}
