// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration.Assemblies;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    internal class NuGetV2QueryBuilder
    {

        internal Dictionary<string, string> AdditionalParameters { get; private set; }

        internal NuGetV2FilterBuilder FilterBuilder { get; private set; }

        internal bool ShouldEmitEmptyFilter = false;

        internal string SearchTerm;

        internal NuGetV2QueryBuilder()
        {

            FilterBuilder = new NuGetV2FilterBuilder();
            AdditionalParameters = new Dictionary<string, string> { };
        }

        internal NuGetV2QueryBuilder(Dictionary<string, string> parameters) : this()
        {
            AdditionalParameters = new Dictionary<string, string>(parameters);
        }
        internal string BuildQueryString()
        {

            var QueryParameters = HttpUtility.ParseQueryString("");


            if (FilterBuilder.CriteriaCount > 0 || ShouldEmitEmptyFilter)
            {
                QueryParameters["$filter"] = FilterBuilder.BuildFilterString();
            }

            if (SearchTerm != null) {
                QueryParameters["searchTerm"] = SearchTerm;
            }

            foreach (var parameter in AdditionalParameters)
            {
                QueryParameters[parameter.Key] = parameter.Value;
            }

            return QueryParameters.ToString();

        }

    }

    internal class NuGetV2FilterBuilder
    {

        internal NuGetV2FilterBuilder()
        {

        }

        private HashSet<String> FilterCriteria = new HashSet<String>();

        /// <summary>
        ///     Convert the builder's provided set of filter criteria into an OData-compatible <c>filter</c> string.
        /// </summary>
        /// <remarks>
        ///     Criteria order is not guaranteed. Filter criteria are combined with the <c>and</c> operator.
        /// </remarks>
        /// <returns>
        ///     Filter criteria combined into a single string.
        /// </returns>
        /// <example>
        ///     The following example will emit one of the two values:
        ///     <list type="bullet">
        ///         <item>
        ///             <description><c>IsPrerelease eq false and Id eq 'Microsoft.PowerShell.PSResourceGet'</c></description>
        ///         </item>
        ///         <item>
        ///             <description><c>Id eq 'Microsoft.PowerShell.PSResourceGet' and IsPrerelease eq false</c></description>
        ///         </item>
        ///     </list>
        ///     <code>
        ///     var filter = new NuGetV2FilterBuilder();
        ///     filter.AddCriteria("IsPrerelease eq false");
        ///     filter.AddCriteria("Id eq 'Microsoft.PowerShell.PSResourceGet'");
        ///     return filter.BuildFilterString();
        ///     </code>
        /// </example>
        public string BuildFilterString()
        {

            if (FilterCriteria.Count == 0)
            {
                return "";
            }

            // Parenthesizing binary criteria (like "Id eq 'Foo'") would ideally provide better isolation/debuggability of mis-built filters.
            // However, a $filter like "(IsLatestVersion)" appears to be rejected by PSGallery (possibly because grouping operators cannot be used with single unary operators).
            // Parenthesizing only binary criteria requires more introspection into the underlying criteria, which we don't currently have with string-form criteria. 

            // Figure out the expected size of our filter string, based on:
            int ExpectedSize = FilterCriteria.Select(x => x.Length).Sum() // The length of the filter criteria themselves.
                + 5 * (FilterCriteria.Count - 1); // The length of the combining string, " and ", interpolated between the filters.

            // Allocate a StringBuilder with our calculated capacity. 
            // This helps right-size memory allocation and reduces performance impact from resizing the builder's internal capacity.
            StringBuilder sb = new StringBuilder(ExpectedSize);

            // StringBuilder.AppendJoin() is not available in .NET 4.8.1/.NET Standard 2,
            // so we have to make do with repeated calls to Append().


            int CriteriaAdded = 0;

            foreach (string filter in FilterCriteria)
            {
                sb.Append(filter);
                CriteriaAdded++;
                if (CriteriaAdded < FilterCriteria.Count)
                {
                    sb.Append(" and ");
                }
            }

            return sb.ToString();

        }

        public bool AddCriteria(string criteria)
        {
            if (string.IsNullOrEmpty(criteria))
            {
                throw new ArgumentException("Criteria cannot be null or empty.", nameof(criteria));
            }
            else
            {
                return FilterCriteria.Add(criteria);
            }
        }

        public bool RemoveCriteria(string criteria) => FilterCriteria.Remove(criteria);

        public int CriteriaCount => FilterCriteria.Count;

    }
}