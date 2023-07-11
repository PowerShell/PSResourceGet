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

    public class V2ResourceNotFoundException : Exception
    {
        public V2ResourceNotFoundException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }

    public class PackageNotFoundException : Exception
    {
        public PackageNotFoundException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }

    public class JsonParsingException : Exception
    {
        public JsonParsingException(string message, Exception innerException = null)
            : base (message)
        {
        }
    }

    public class XmlParsingException : Exception
    {
        public XmlParsingException(string message, Exception innerException = null)
            : base(message)
        {
        }
    }

    public class ConvertToPSResourceException : Exception
    {
        public ConvertToPSResourceException(string message, Exception innerException = null)
            : base(message)
        {
        }
    }

    public class SpecifiedTagsNotFoundException : Exception
    {
        public SpecifiedTagsNotFoundException(string message, Exception innerException = null)
            : base (message)
        {
        }
    }

    public class InvalidOrEmptyResponse : Exception
    {
        public InvalidOrEmptyResponse(string message, Exception innerException = null)
            : base (message)
        {   
        }
    }

    public class LocalResourceEmpty : Exception
    {
        public LocalResourceEmpty(string message, Exception innerException = null)
            : base (message)
        {
        }
    }

    public class LocalResourceNotFoundException : Exception
    {
        public LocalResourceNotFoundException(string message, Exception innerException = null)
            : base (message)
        {
        }
    }
}
