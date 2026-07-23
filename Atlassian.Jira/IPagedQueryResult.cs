using System;
using System.Collections.Generic;

namespace Atlassian.Jira
{
    /// <summary>
    /// Represents a query result for a resource that supports pagination.
    /// </summary>
    public interface IPagedQueryResult<T> : IEnumerable<T>
    {
        /// <summary>
        /// The maximum number of items included on each page.
        /// </summary>
        int ItemsPerPage { get; }

        /// <summary>
        /// The index of the first item in the paged result.
        /// </summary>
        int StartAt { get; }

        /// <summary>
        /// The total number of items.
        /// </summary>
        int TotalItems { get; }

        /// <summary>
        /// Token to request the next page from Jira Cloud's enhanced search.
        /// Null when this is the last page or the endpoint does not use token pagination.
        /// </summary>
        string NextPageToken { get; }
    }
}
