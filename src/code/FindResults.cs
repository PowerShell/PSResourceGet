using System;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections;

using Dbg = System.Diagnostics.Debug;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    /// <summary>
    /// Wrapper class for the results from Find server level APIs.
    /// </summary>

    public enum FindResponseType
    {
        None,
        ResponseString,
        ResponseHashtable
    }

    public sealed class FindResults
    {
        public string[] StringResponse { get; private set; } = Utils.EmptyStrArray;
        public Hashtable[] HashtableResponse { get; private set; } = new Hashtable[]{};
        public FindResponseType ResponseType { get; set; }

        public FindResults()
        {
            this.StringResponse = Utils.EmptyStrArray;
            this.HashtableResponse = new Hashtable[]{};
            this.ResponseType = FindResponseType.None;
        }

        public FindResults(string[] stringResponse, Hashtable[] hashtableResponse, FindResponseType responseType)
        {
            this.StringResponse = stringResponse;
            this.HashtableResponse = hashtableResponse;
            this.ResponseType = responseType;
        }   

        public bool IsFindResultsEmpty()
        {
            return StringResponse.Length == 0 && HashtableResponse.Length == 0;
        }
    }
}
