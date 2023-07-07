// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.PowerShell.PSResourceGet.UtilClasses
{
    public sealed class PSResourceResult
    {
        internal PSResourceInfo returnedObject { get; set; }
        internal PSCommandResourceInfo returnedCmdObject { get; set; }
        internal Exception exception { get; set; }
        internal bool isTerminatingError { get; set; }


        public PSResourceResult(PSResourceInfo returnedObject, Exception exception, bool isTerminatingError)
        {
            this.returnedObject = returnedObject;
            this.exception = exception;
            this.isTerminatingError = isTerminatingError;
        }


        public PSResourceResult(PSCommandResourceInfo returnedCmdObject, Exception exception, bool isTerminatingError)
        {
            this.returnedCmdObject = returnedCmdObject;
            this.exception = exception;
            this.isTerminatingError = isTerminatingError;
        }
    }
}
