using System;

namespace AerisBrowser.Search
{
    public class SearchResultItem
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string DisplayUrl { get; set; }
        public string Snippet { get; set; }
        public string ImageUrl { get; set; }
        public string Source { get; set; }
        public string Duration { get; set; }
        public string PublishedDate { get; set; }
        public string Category { get; set; }

        public SearchResultItem()
        {
            Category = "all";
        }
    }

    public class SearchResponse
    {
        public string Query { get; set; }
        public string Category { get; set; }
        public int TotalResults { get; set; }
        public long QueryTimeMs { get; set; }
        public System.Collections.Generic.List<SearchResultItem> Results { get; set; }
        public string InstantAnswerTitle { get; set; }
        public string InstantAnswerText { get; set; }
        public string InstantAnswerUrl { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }

        public SearchResponse()
        {
            Results = new System.Collections.Generic.List<SearchResultItem>();
            CurrentPage = 1;
            TotalPages = 1;
        }
    }
}

