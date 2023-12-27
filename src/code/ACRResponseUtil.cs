// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    internal class ACRResponseUtil : ResponseUtil
    {
        #region Members

        internal override PSRepositoryInfo Repository { get; set; }

        #endregion

        #region Constructor

        public ACRResponseUtil(PSRepositoryInfo repository) : base(repository)
        {
            Repository = repository;
        }

        #endregion

        #region Overriden Methods

        public override IEnumerable<PSResourceResult> ConvertToPSResourceResult(FindResults responseResults)
        {
            // in FindHelper:
            // serverApi.FindName() -> return responses, and out errRecord
            // check outErrorRecord
            // 
            // acrConverter.ConvertToPSResourceInfo(responses) -> return PSResourceResult
            // check resourceResult for error, write if needed
            Hashtable[] responses = responseResults.HashtableResponse;
            foreach (Hashtable response in responses)
            {
                string responseConversionError = String.Empty;
                PSResourceInfo pkg = null;

                string packageName = string.Empty;
                string packageMetadata = null;

                foreach (DictionaryEntry entry in response)
                {
                    packageName = (string)entry.Key;
                    packageMetadata = (string)entry.Value;
                }

                try
                {
                    using (JsonDocument pkgVersionEntry = JsonDocument.Parse(packageMetadata))
                    {
                        PSResourceInfo.TryConvertFromACRJson(packageName, pkgVersionEntry, out pkg, Repository, out responseConversionError);
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
