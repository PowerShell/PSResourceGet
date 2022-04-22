// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;


namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    #region Enums

    public enum CatalogValidationStatus
    {
        Valid,
        ValidationFailed
    }

    #endregion

    #region CatalogInformation

    public sealed class CatalogInformation
    {
        #region Properties

        public CatalogValidationStatus Status { get; }
        public Signature Signature { get; }
        public string HashAlgorithm { get; }
        public Dictionary<string, string> CatalogItems { get; }
        public Dictionary<string, string> PathItems { get; }

        #endregion

        #region Constructors

        private CatalogInformation() { }

        private CatalogInformation(
            CatalogValidationStatus status,
            Signature signature,
            string hashAlgorithm,
            Dictionary<string, string> catalogItems,
            Dictionary<string, string> pathItems)
        {
            Status = status;
            Signature = signature;
            HashAlgorithm = hashAlgorithm ?? string.Empty;
            CatalogItems = catalogItems ?? new Dictionary<string, string>();
            pathItems = pathItems ?? new Dictionary<string, string>();
        }

        #endregion
    }

    #endregion
}
