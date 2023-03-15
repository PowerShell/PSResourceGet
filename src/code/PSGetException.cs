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

    public class V3ResourceNotFoundException : Exception
    {
        public V3ResourceNotFoundException(string message)
            : base (message)
        {
        }
    }

    public class JsonParsingException : Exception
    {
        public JsonParsingException(string message)
            : base (message)
        {
        }
    }

    public class SpecifiedTagsNotFoundException : Exception
    {
        public SpecifiedTagsNotFoundException(string message)
            : base (message)
        {
        }
    }

    public class InvalidOrEmptyResponse : Exception
    {
        public InvalidOrEmptyResponse(string message)
            : base (message)
        {   
        }
    }
}
