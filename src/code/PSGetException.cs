// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.PowerShell.PSResourceGet.UtilClasses
{
    public class V3ResourceNotFoundException : Exception
    {
        public V3ResourceNotFoundException(string message, Exception innerException = null)
            : base (message, innerException)
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

    public class LocalResourceEmpty : Exception
    {
        public LocalResourceEmpty(string message)
            : base (message)
        {
        }
    }

    public class LocalResourceNotFoundException : Exception
    {
        public LocalResourceNotFoundException(string message)
            : base (message)
        {
        }
    }
}
