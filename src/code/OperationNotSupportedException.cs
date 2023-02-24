// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    public class OperationNotSupportedException : Exception
    {
        public OperationNotSupportedException(string message)
            : base(message)
        {
        }

    }
}