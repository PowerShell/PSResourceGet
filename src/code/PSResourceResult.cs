using System;

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
            returnedObject = this.returnedObject;
            errorMsg = this.errorMsg;
            isTerminatingError = this.isTerminatingError;
        }


        public PSResourceResult(PSCommandResourceInfo returnedCmdObject, string errorMsg, bool isTerminatingError)
        {
            returnedCmdObject = this.returnedCmdObject;
            errorMsg = this.errorMsg;
            isTerminatingError = this.isTerminatingError;
        }
    }
}
