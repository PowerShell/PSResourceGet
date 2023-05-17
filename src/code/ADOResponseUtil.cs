// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    internal class ADOResponseUtil : ResponseUtil
    {
        #region Members

        internal override PSRepositoryInfo Repository { get; set; }

        #endregion

        #region Constructor

        public ADOResponseUtil(PSRepositoryInfo repository) : base(repository)
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
                JsonElement metadataElement;
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

                JsonElement rootDom = pkgVersionEntry.RootElement;
                rootDom.TryGetProperty("items", out JsonElement itemsElement);
                JsonElement innerItemsElement = itemsElement[0].GetProperty("items")[0];
                metadataElement = innerItemsElement.GetProperty("catalogEntry");

                if (!PSResourceInfo.TryConvertFromJson(metadataElement, out PSResourceInfo psGetInfo, Repository, out string errorMsg))
                {
                    yield return new PSResourceResult(returnedObject: null, errorMsg: errorMsg, isTerminatingError: false);
                }

                yield return new PSResourceResult(returnedObject: psGetInfo, errorMsg: String.Empty, isTerminatingError: false);
            }
        }

        #endregion
    }
}
