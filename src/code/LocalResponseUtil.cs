// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Xml;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    internal class LocalResponseUtil : ResponseUtil
    {
        #region Members

        public override PSRepositoryInfo repository { get; set; }

        #endregion

        #region Constructor

        public LocalResponseUtil(PSRepositoryInfo repository) : base(repository)
        {
            this.repository = repository;
        }

        #endregion

        #region Overriden Methods
        public override IEnumerable<PSResourceResult> ConvertToPSResourceResult(string[] responses)
        {
            yield break;
        }

        #endregion

        #region Local Repository Specific Methods

        #endregion
    }
}
