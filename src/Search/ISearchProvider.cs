using System;
using System.Collections.Generic;

namespace AerisBrowser.Search
{
    public interface ISearchProvider
    {
        string Name { get; }
        string Description { get; }
        SearchResponse Search(string query, string category, int page);
        List<string> GetSuggestions(string query);
    }
}

