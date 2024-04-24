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

        /// <summary>
        ///     The filter to use when querying the NuGet API (query parameter <c>$filter</c>), if needed.
        /// </summary>
        /// <remarks>
        ///     If no criteria are added with <seealso cref="NuGetV2FilterBuilder.AddCriterion(string)"/>, the built query string will not contain a <c>$filter</c> parameter unless <seealso cref="ShouldEmitEmptyFilter"/> is true.
        /// </remarks>
        internal NuGetV2FilterBuilder FilterBuilder { get; private set; }
        
        /// <summary>
        ///     Indicates whether an empty <c>$filter</c> parameter should be emitted if <seealso cref="FilterBuilder"/> contains no criteria.
        /// </summary>
        internal bool ShouldEmitEmptyFilter = false;

        /// <summary>
        ///     The search term to pass to NuGet (<c>searchTerm</c> parameter), if needed.
        /// </summary>
        /// <remarks>
        ///     No additional quote-encapsulation is performed on the string. A <seealso cref="null"/> string will cause the parameter to be omitted.
        /// </remarks>
        internal string SearchTerm;

        /// <summary>
        ///     Construct a new <seealso cref="NuGetV2QueryBuilder"/> with no additional query parameters.
        /// </summary>
        internal NuGetV2QueryBuilder()
        {

            FilterBuilder = new NuGetV2FilterBuilder();
            AdditionalParameters = new Dictionary<string, string> { };
        }

        /// <summary>
        ///     Construct a new <seealso cref="NuGetV2QueryBuilder"/> with a user-specified collection of query parameters.
        /// </summary>
        /// <param name="parameters">
        ///     The set of additional parameters to provide.
        /// </param>
        internal NuGetV2QueryBuilder(Dictionary<string, string> parameters) : this()
        {
            AdditionalParameters = new Dictionary<string, string>(parameters);
        }

        /// <summary>
        ///     Serialize the instance to an HTTP-compatible query string.
        /// </summary>
        /// <remarks>
        ///     Query key-value pairs from <seealso cref="AdditionalParameters"/> will take precedence.
        /// </remarks>
        /// <returns>
        ///     A <seealso cref="string"/> containing URL-encoded query parameters separated by <c><![CDATA[&]]></c>. No <c>?</c> is prefixed at the beginning of the string.
        /// </returns>
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

    /// <summary>
    ///     Helper class for building NuGet v2 (OData) filter strings based on a set of criteria
    /// </summary>
    internal class NuGetV2FilterBuilder
    {

        /// <summary>
        ///     Construct a new <seealso cref="NuGetV2FilterBuilder"/> with an empty set of criteria.
        /// </summary>
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

        /// <summary>
        ///     Add a given OData-compatible criterion to the object's internal criteria set.
        /// </summary>
        /// <param name="criterion">
        ///     The criterion to add, e.g. <c>IsLatestVersion</c> or <c>Id eq 'Foo'</c>.
        /// </param>
        /// <returns>
        ///     A boolean indicating whether the criterion was added to the set. <cref>false</cref> indicates the criteria set already contains the given string.
        /// </returns>
        /// <remarks>
        ///     This method encapsulates over <seealso cref="HashSet{string}.Add(string)"/>. Similar comparison and equality semantics apply.
        /// </remarks>
        /// <exception cref="ArgumentException">
        ///     The provided criterion string was null or empty.
        /// </exception>
        public bool AddCriterion(string criterion)
        {
            if (string.IsNullOrEmpty(criterion))
            {
                throw new ArgumentException("Criteria cannot be null or empty.", nameof(criterion));
            }
            else
            {
                return FilterCriteria.Add(criterion);
            }
        }

        /// <summary>
        ///     Remove a criterion from the instance's internal criteria set.
        /// </summary>
        /// <param name="criterion">
        ///     The criteria to remove.
        /// </param>
        /// <returns>
        ///     <cref>true</cref> if the criterion was removed, <cref>false</cref> if it was not found.
        /// </returns>
        /// <remarks>
        ///     This method encapsulates over <seealso cref="HashSet{string}.Remove(string)"/>. Similar comparison and equality semantics apply.
        /// </remarks>
        public bool RemoveCriterion(string criterion) => FilterCriteria.Remove(criterion);

        public int CriteriaCount => FilterCriteria.Count;

    }
}