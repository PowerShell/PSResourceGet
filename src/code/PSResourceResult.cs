// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    public sealed class PSResourceResult
    {
        internal PSResourceInfo returnedObject { get; set; }
        internal PSCommandResourceInfo returnedCmdObject { get; set; }
        internal string errorMsg { get; set; }
        internal bool isTerminatingError { get; set; }


        public PSResourceResult(PSResourceInfo returnedObject, string errorMsg, bool isTerminatingError)
        {
            this.returnedObject = returnedObject;
            this.errorMsg = errorMsg;
            this.isTerminatingError = isTerminatingError;
        }


        public PSResourceResult(PSCommandResourceInfo returnedCmdObject, string errorMsg, bool isTerminatingError)
        {
            this.returnedCmdObject = returnedCmdObject;
            this.errorMsg = errorMsg;
            this.isTerminatingError = isTerminatingError;
        }
    }
}
