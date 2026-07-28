using System;
using System.Collections.Generic;

namespace OCC.Shared.Framework
{
    /// <summary>
    /// Generic container for paginated query results.
    /// </summary>
    /// <typeparam name="T">Type of items contained in the page.</typeparam>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        public PagedResult()
        {
        }

        public PagedResult(List<T> items, int count, int pageIndex, int pageSize)
        {
            Items = items ?? new List<T>();
            TotalCount = count;
            PageIndex = pageIndex < 1 ? 1 : pageIndex;
            PageSize = pageSize < 1 ? 10 : pageSize;
        }

        public static PagedResult<T> Create(List<T> items, int totalCount, int pageIndex, int pageSize)
        {
            return new PagedResult<T>(items, totalCount, pageIndex, pageSize);
        }
    }
}
