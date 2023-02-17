// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System.Collections.Generic;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    internal abstract class ResponseUtil
    {
        #region Members

        public abstract PSRepositoryInfo repository { get; set; }

        #endregion

        #region Constructor

        #endregion
        
        #region Methods
    
        public abstract IEnumerable<PSResourceResult> ConvertToPSResourceResult(string[] responses);

        #endregion
    
    }
}