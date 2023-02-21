// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    internal class V3ResponseUtil : ResponseUtil
    {
        #region Members

        public override PSRepositoryInfo repository { get; set; }

        #endregion

        #region Constructor

        public V3ResponseUtil(PSRepositoryInfo repository) : base(repository)
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
            // v3Converter.ConvertToPSResourceInfo(responses) -> return PSResourceResult
            // check resourceResult for error, write if needed

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

                if (!PSResourceInfo.TryConvertFromJson(pkgVersionEntry, out PSResourceInfo psGetInfo, repository.Name, out string errorMsg))
                {
                    yield return new PSResourceResult(returnedObject: null, errorMsg: errorMsg, isTerminatingError: false);
                }

                yield return new PSResourceResult(returnedObject: psGetInfo, errorMsg: String.Empty, isTerminatingError: false);
            }
        }

        #endregion
    
    }
}