using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace AerisBrowser.Search
{
    public class SearchEngineService
    {
        private readonly ISearchProvider _provider;
        private readonly string _templateHtml;

        public SearchEngineService(ISearchProvider provider, string templatePath)
        {
            _provider = provider ?? new DuckDuckGoProvider();
            if (File.Exists(templatePath))
            {
                _templateHtml = File.ReadAllText(templatePath, Encoding.UTF8);
            }
            else
            {
                _templateHtml = "<html><body><h2>Search Results: {{QUERY}}</h2>{{RESULTS_HTML}}</body></html>";
            }
        }

        public ISearchProvider Provider { get { return _provider; } }

        public List<string> GetSuggestions(string query)
        {
            return _provider.GetSuggestions(query);
        }

        public string GenerateResultsHtml(string query, string category, int page)
        {
            var response = _provider.Search(query, category, page);
            return RenderHtml(response);
        }

        private string RenderHtml(SearchResponse response)
        {
            string html = _templateHtml;

            html = html.Replace("{{QUERY}}", WebUtility.HtmlEncode(response.Query ?? ""));
            html = html.Replace("{{CURRENT_CATEGORY}}", response.Category ?? "all");

            // Category tab active states
            html = html.Replace("{{ACTIVE_ALL}}", response.Category == "all" ? "active" : "");
            html = html.Replace("{{ACTIVE_IMAGES}}", response.Category == "images" ? "active" : "");
            html = html.Replace("{{ACTIVE_VIDEOS}}", response.Category == "videos" ? "active" : "");
            html = html.Replace("{{ACTIVE_NEWS}}", response.Category == "news" ? "active" : "");

            // Stats
            string stats = string.Format("About {0} results ({1:0.00} seconds)", response.TotalResults, response.QueryTimeMs / 1000.0);
            html = html.Replace("{{RESULT_STATS}}", stats);

            // Instant Answer Card
            if (!string.IsNullOrEmpty(response.InstantAnswerTitle) && !string.IsNullOrEmpty(response.InstantAnswerText))
            {
                var sbInstant = new StringBuilder();
                sbInstant.Append("<div class=\"instant-card\">");
                sbInstant.AppendFormat("<div class=\"instant-title\">{0}</div>", WebUtility.HtmlEncode(response.InstantAnswerTitle));
                sbInstant.AppendFormat("<div class=\"instant-desc\">{0}</div>", WebUtility.HtmlEncode(response.InstantAnswerText));
                if (!string.IsNullOrEmpty(response.InstantAnswerUrl))
                {
                    sbInstant.AppendFormat("<div class=\"instant-source\"><a href=\"#\" onclick=\"navigateUrl('{0}')\">More on Wikipedia / Reference ›</a></div>", WebUtility.HtmlEncode(response.InstantAnswerUrl));
                }
                sbInstant.Append("</div>");
                html = html.Replace("{{INSTANT_CARD_HTML}}", sbInstant.ToString());
            }
            else
            {
                html = html.Replace("{{INSTANT_CARD_HTML}}", "");
            }

            // Results list
            var sbResults = new StringBuilder();

            if (response.Results.Count == 0)
            {
                sbResults.Append("<div style=\"padding:40px 0; text-align:center; color:var(--subtext-color);\">");
                sbResults.Append("<h3>No matching results found</h3>");
                sbResults.Append("<p style=\"margin-top:8px;\">Check your spelling, try more general terms, or navigate to a direct URL.</p>");
                sbResults.Append("</div>");
            }
            else if (response.Category == "images")
            {
                sbResults.Append("<div class=\"images-grid\">");
                foreach (var item in response.Results)
                {
                    string thumb = !string.IsNullOrEmpty(item.ImageUrl) ? item.ImageUrl : "https://via.placeholder.com/200x140?text=Image";
                    sbResults.AppendFormat("<div class=\"image-card\" onclick=\"navigateUrl('{0}')\">", WebUtility.HtmlEncode(item.Url));
                    sbResults.AppendFormat("<img class=\"image-thumbnail\" src=\"{0}\" alt=\"{1}\" loading=\"lazy\" />", WebUtility.HtmlEncode(thumb), WebUtility.HtmlEncode(item.Title));
                    sbResults.Append("<div class=\"image-meta\">");
                    sbResults.AppendFormat("<div class=\"image-title\">{0}</div>", WebUtility.HtmlEncode(item.Title));
                    sbResults.AppendFormat("<div class=\"image-source\">{0}</div>", WebUtility.HtmlEncode(item.DisplayUrl));
                    sbResults.Append("</div></div>");
                }
                sbResults.Append("</div>");
            }
            else if (response.Category == "videos")
            {
                foreach (var item in response.Results)
                {
                    sbResults.AppendFormat("<div class=\"video-card\" onclick=\"navigateUrl('{0}')\">", WebUtility.HtmlEncode(item.Url));
                    sbResults.Append("<div class=\"video-thumb-wrapper\">");
                    sbResults.AppendFormat("<img class=\"video-thumb\" src=\"{0}\" alt=\"Video thumbnail\" />", !string.IsNullOrEmpty(item.ImageUrl) ? WebUtility.HtmlEncode(item.ImageUrl) : "https://via.placeholder.com/160x90?text=Video");
                    sbResults.Append("<div class=\"video-duration\">🎬 Video</div>");
                    sbResults.Append("</div>");
                    sbResults.Append("<div class=\"video-info\">");
                    sbResults.AppendFormat("<div class=\"result-title\"><a onclick=\"navigateUrl('{0}')\">{1}</a></div>", WebUtility.HtmlEncode(item.Url), WebUtility.HtmlEncode(item.Title));
                    sbResults.AppendFormat("<div class=\"result-domain\">{0}</div>", WebUtility.HtmlEncode(item.DisplayUrl));
                    sbResults.AppendFormat("<div class=\"result-snippet\">{0}</div>", WebUtility.HtmlEncode(item.Snippet));
                    sbResults.Append("</div></div>");
                }
            }
            else if (response.Category == "news")
            {
                foreach (var item in response.Results)
                {
                    sbResults.Append("<div class=\"news-card\">");
                    sbResults.AppendFormat("<div class=\"news-publisher\">{0}</div>", WebUtility.HtmlEncode(item.DisplayUrl));
                    sbResults.AppendFormat("<div class=\"result-title\"><a onclick=\"navigateUrl('{0}')\">{1}</a></div>", WebUtility.HtmlEncode(item.Url), WebUtility.HtmlEncode(item.Title));
                    sbResults.AppendFormat("<div class=\"result-snippet\">{0}</div>", WebUtility.HtmlEncode(item.Snippet));
                    sbResults.Append("</div>");
                }
            }
            else
            {
                // Standard Web results
                foreach (var item in response.Results)
                {
                    sbResults.Append("<div class=\"result-card\">");
                    sbResults.Append("<div class=\"result-url-container\">");
                    sbResults.AppendFormat("<span class=\"result-domain\">{0}</span>", WebUtility.HtmlEncode(item.DisplayUrl));
                    sbResults.Append("</div>");
                    sbResults.AppendFormat("<div class=\"result-title\"><a onclick=\"navigateUrl('{0}')\">{1}</a></div>", WebUtility.HtmlEncode(item.Url), WebUtility.HtmlEncode(item.Title));
                    sbResults.AppendFormat("<div class=\"result-snippet\">{0}</div>", WebUtility.HtmlEncode(item.Snippet));
                    sbResults.Append("</div>");
                }
            }

            html = html.Replace("{{RESULTS_HTML}}", sbResults.ToString());

            // Pagination
            var sbPages = new StringBuilder();
            if (response.CurrentPage > 1)
            {
                sbPages.AppendFormat("<button class=\"page-btn\" onclick=\"goToPage({0})\">‹ Previous</button>", response.CurrentPage - 1);
            }
            for (int i = 1; i <= Math.Min(response.TotalPages, 5); i++)
            {
                string activeStyle = i == response.CurrentPage ? "style=\"background:var(--tab-active); color:#fff; font-weight:600;\"" : "";
                sbPages.AppendFormat("<button class=\"page-btn\" {0} onclick=\"goToPage({1})\">{1}</button>", activeStyle, i);
            }
            if (response.CurrentPage < response.TotalPages)
            {
                sbPages.AppendFormat("<button class=\"page-btn\" onclick=\"goToPage({0})\">Next ›</button>", response.CurrentPage + 1);
            }
            html = html.Replace("{{PAGINATION_HTML}}", sbPages.ToString());

            return html;
        }
    }
}

