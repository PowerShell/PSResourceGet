// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    internal class V3ResponseUtil : ResponseUtil
    {
        #region Members

        internal override PSRepositoryInfo Repository { get; set; }

        #endregion

        #region Constructor

        public V3ResponseUtil(PSRepositoryInfo repository) : base(repository)
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
            // v3Converter.ConvertToPSResourceInfo(responses) -> return PSResourceResult
            // check resourceResult for error, write if needed
            string[] responses = responseResults.StringResponse;
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
                    yield return new PSResourceResult(returnedObject: null, errorMsg: parseError, isTerminatingError: false);
                }

                if (!PSResourceInfo.TryConvertFromJson(pkgVersionEntry, out PSResourceInfo psGetInfo, Repository, out string errorMsg))
                {
                    yield return new PSResourceResult(returnedObject: null, errorMsg: errorMsg, isTerminatingError: false);
                }

                yield return new PSResourceResult(returnedObject: psGetInfo, errorMsg: String.Empty, isTerminatingError: false);
            }
            // foreach (string response in responses)
            // {
            //     // string parseError = String.Empty;
            //     // JsonDocument pkgVersionEntry = null;
            //     // JsonElement metadataElement;
            //     // try
            //     // {
            //     //     pkgVersionEntry = JsonDocument.Parse(response);
            //     // }
            //     // catch (Exception e)
            //     // {
            //     //     parseError = e.Message;
            //     // }

            //     // if (!String.IsNullOrEmpty(parseError))
            //     // {
            //     //     yield return new PSResourceResult(returnedObject: null, errorMsg: parseError, isTerminatingError: false);
            //     // }

            //     // JsonElement rootDom = pkgVersionEntry.RootElement;
            //     // rootDom.TryGetProperty("items", out JsonElement itemsElement);
            //     // JsonElement firstItem = itemsElement[0]; // we will only need 1st element for this

            //     // JsonElement innerItemsElements = firstItem.GetProperty("items"); // this is the item for each version of the package
            //     // JsonElement countElement = firstItem.GetProperty("count");
            //     // bool parsedCount = countElement.TryGetInt32(out int count);
            //     // if (!parsedCount)
            //     // {
            //     //     // TODO: some error?
            //     //     count = 0;
            //     // }

            //     // for (int i= 0; i < count; i++)
            //     // {
            //     //     JsonElement versionedItem = innerItemsElements[i];
            //     //     metadataElement = versionedItem.GetProperty("catalogEntry");
            //     //     if (!PSResourceInfo.TryConvertFromJson(metadataElement, out PSResourceInfo psGetInfo, Repository, out string errorMsg))
            //     //     {
            //     //         yield return new PSResourceResult(returnedObject: null, errorMsg: errorMsg, isTerminatingError: false);
            //     //     }

            //     //     yield return new PSResourceResult(returnedObject: psGetInfo, errorMsg: String.Empty, isTerminatingError: false);
            //     // }

            //     // // metadataElement = innerItemsElement.GetProperty("catalogEntry");
            //}
        }

        #endregion
    }
}
