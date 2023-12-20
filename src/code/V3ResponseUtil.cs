// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.PowerShell.PSResourceGet
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
                string responseConversionError = String.Empty;
                PSResourceInfo pkg = null;

                try
                {
                    using (JsonDocument pkgVersionEntry = JsonDocument.Parse(response))
                    {
                        PSResourceInfo.TryConvertFromJson(pkgVersionEntry, out pkg, Repository, out responseConversionError);
                    }
                }
                catch (Exception e)
                {
                    responseConversionError = e.Message;
                }

                if (!String.IsNullOrEmpty(responseConversionError))
                {
                    yield return new PSResourceResult(returnedObject: null, new ConvertToPSResourceException(responseConversionError), isTerminatingError: false);
                }

                yield return new PSResourceResult(returnedObject: pkg, exception: null, isTerminatingError: false);
            }
        }

        #endregion
    }
}
