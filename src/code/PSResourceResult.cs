using System;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    public sealed class PSResourceResult
    {

        internal PSResourceInfo returnedObject { get; }
        internal PSCommandResourceInfo returnedCmdObject { get; }
        internal string errorMsg { get; }
        internal bool isTerminatingError { get; }


        public PSResourceResult(PSResourceInfo returnedObj, string errorMsg, bool isTerminatingError)
        {
            returnedObj = this.returnedObject;
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
