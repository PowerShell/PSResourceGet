// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    internal class ContainerRegistryResponseUtil : ResponseUtil
    {
        #region Members

        internal override PSRepositoryInfo Repository { get; set; }

        #endregion

        #region Constructor

        public ContainerRegistryResponseUtil(PSRepositoryInfo repository) : base(repository)
        {
            Repository = repository;
        }

        #endregion

        #region Overridden Methods

        public override IEnumerable<PSResourceResult> ConvertToPSResourceResult(FindResults responseResults, bool isResourceRequestedWithWildcard = false)
        {
            Hashtable[] responses = responseResults.HashtableResponse;
            foreach (Hashtable response in responses)
            {
                string responseConversionError = String.Empty;
                PSResourceInfo pkg = null;

                // Hashtable should have keys for Name, Metadata, ResourceType
                if (!response.ContainsKey("Name") && string.IsNullOrWhiteSpace(response["Name"].ToString()))
                {
                    yield return new PSResourceResult(returnedObject: pkg, exception: new ConvertToPSResourceException("Error retrieving package name from response."), isTerminatingError: true);
                }

                if (!response.ContainsKey("Metadata"))
                {
                    yield return new PSResourceResult(returnedObject: pkg, exception: new ConvertToPSResourceException("Error retrieving package metadata from response."), isTerminatingError: true);
                }

                ResourceType? resourceType = response.ContainsKey("ResourceType") ? response["ResourceType"] as ResourceType? : ResourceType.None;

                try
                {
                    using (JsonDocument pkgVersionEntry = JsonDocument.Parse(response["Metadata"].ToString()))
                    {
                        PSResourceInfo.TryConvertFromContainerRegistryJson(response["Name"].ToString(), pkgVersionEntry, resourceType, out pkg, Repository, out responseConversionError);
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
