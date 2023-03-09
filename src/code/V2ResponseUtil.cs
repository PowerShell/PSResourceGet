// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Xml;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    internal class V2ResponseUtil : ResponseUtil
    {
        #region Members

        public override PSRepositoryInfo repository { get; set; }

        #endregion

        #region Constructor

        public V2ResponseUtil(PSRepositoryInfo repository) : base(repository)
        {
            this.repository = repository;
        }

        #endregion

        #region Overriden Methods
        public override IEnumerable<PSResourceResult> ConvertToPSResourceResult(string[] responses)
        {
            // in FindHelper:
            // serverApi.FindName() -> return responses, and out errRecord
            // check outErrorRecord
            // 
            // v2Converter.ConvertToPSResourceInfo(responses) -> return PSResourceResult
            // check resourceResult for error, write if needed

            foreach (string response in responses)
            {
                var elemList = ConvertResponseToXML(response);
                if (elemList.Length == 0)
                {
                    // this indicates we got a non-empty, XML response (as noticed for V2 server) but it's not a response that's meaningful (contains 'properties')
                    string errorMsg = $"Response didn't contain properties element";
                    yield return new PSResourceResult(returnedObject: null, errorMsg: errorMsg, isTerminatingError: false);
                }

                foreach (var element in elemList)
                {
                    if (!PSResourceInfo.TryConvertFromXml(element, out PSResourceInfo psGetInfo, repository.Name, out string errorMsg))
                    {
                        yield return new PSResourceResult(returnedObject: null, errorMsg: errorMsg, isTerminatingError: false);
                    }

                    yield return new PSResourceResult(returnedObject: psGetInfo, errorMsg: String.Empty, isTerminatingError: false);
                }
            }
        }

        #endregion

        #region V2 Specific Methods

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

        #endregion
    }
}