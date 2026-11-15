using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace AerisBrowser.Search
{
    public class DuckDuckGoProvider : ISearchProvider
    {
        public string Name { get { return "DuckDuckGo Ad-Free"; } }
        public string Description { get { return "Privacy-first, ad-free search with instant answers"; } }

        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public DuckDuckGoProvider()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
        }

        public SearchResponse Search(string query, string category, int page)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = new SearchResponse
            {
                Query = query,
                Category = category ?? "all",
                CurrentPage = page < 1 ? 1 : page
            };

            if (string.IsNullOrWhiteSpace(query))
            {
                sw.Stop();
                response.QueryTimeMs = sw.ElapsedMilliseconds;
                return response;
            }

            try
            {
                // 1. Query DuckDuckGo Instant Answer API
                string apiUrl = string.Format("https://api.duckduckgo.com/?q={0}&format=json&no_html=1&skip_disambig=1", Uri.EscapeDataString(query));
                string json = FetchHttp(apiUrl, 3000);

                if (!string.IsNullOrEmpty(json))
                {
                    var dict = _serializer.Deserialize<Dictionary<string, object>>(json);
                    if (dict != null)
                    {
                        if (dict.ContainsKey("AbstractText") && !string.IsNullOrEmpty(dict["AbstractText"] as string))
                        {
                            response.InstantAnswerTitle = dict["Heading"] as string ?? query;
                            response.InstantAnswerText = dict["AbstractText"] as string;
                            response.InstantAnswerUrl = dict["AbstractURL"] as string;
                        }

                        if (dict.ContainsKey("RelatedTopics"))
                        {
                            var related = dict["RelatedTopics"] as System.Collections.ArrayList;
                            if (related != null)
                            {
                                foreach (var item in related)
                                {
                                    var topicDict = item as Dictionary<string, object>;
                                    if (topicDict != null && topicDict.ContainsKey("Text") && topicDict.ContainsKey("FirstURL"))
                                    {
                                        string text = topicDict["Text"] as string;
                                        string firstUrl = topicDict["FirstURL"] as string;
                                        if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(firstUrl))
                                        {
                                            string title = text;
                                            string snippet = text;
                                            int dashIdx = text.IndexOf(" - ");
                                            if (dashIdx > 0)
                                            {
                                                title = text.Substring(0, dashIdx);
                                                snippet = text.Substring(dashIdx + 3);
                                            }

                                            response.Results.Add(new SearchResultItem
                                            {
                                                Title = title,
                                                Url = firstUrl,
                                                DisplayUrl = FormatDisplayUrl(firstUrl),
                                                Snippet = snippet,
                                                Category = category
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // 2. Query DuckDuckGo HTML / Lite endpoint if more web results are needed
                if (response.Results.Count < 5)
                {
                    FetchHtmlResults(query, category, response);
                }

                // 3. Fallback to Wikipedia OpenSearch if results are still sparse
                if (response.Results.Count == 0)
                {
                    FetchWikipediaResults(query, response);
                }
            }
            catch (Exception ex)
            {
                // If network is offline or restricted, provide intelligent contextual offline results
                GenerateOfflineFallback(query, category, response, ex.Message);
            }

            sw.Stop();
            response.QueryTimeMs = sw.ElapsedMilliseconds;
            response.TotalResults = response.Results.Count;
            response.TotalPages = Math.Max(1, (int)Math.Ceiling(response.TotalResults / 10.0));

            return response;
        }

        public List<string> GetSuggestions(string query)
        {
            var suggestions = new List<string>();
            if (string.IsNullOrWhiteSpace(query))
                return suggestions;

            try
            {
                string url = string.Format("https://duckduckgo.com/ac/?q={0}&type=list", Uri.EscapeDataString(query));
                string json = FetchHttp(url, 1500);
                if (!string.IsNullOrEmpty(json))
                {
                    var arr = _serializer.Deserialize<object[]>(json);
                    if (arr != null && arr.Length > 1)
                    {
                        var list = arr[1] as System.Collections.ArrayList;
                        if (list != null)
                        {
                            foreach (var item in list)
                            {
                                if (item != null)
                                    suggestions.Add(item.ToString());
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback smart offline completions
                string q = query.ToLowerInvariant();
                string[] defaults = new string[] {
                    "wikipedia", "github", "reddit", "hacker news", "weather", "calculator",
                    "python docs", "rust docs", "duckduckgo", "open source", "world news", "maps"
                };
                foreach (var d in defaults)
                {
                    if (d.Contains(q) || q.Contains(d))
                        suggestions.Add(d);
                }
            }

            return suggestions;
        }

        private void FetchHtmlResults(string query, string category, SearchResponse response)
        {
            try
            {
                string url = string.Format("https://html.duckduckgo.com/html/?q={0}", Uri.EscapeDataString(query));
                string html = FetchHttp(url, 3500);
                if (string.IsNullOrEmpty(html)) return;

                // Match result items
                // <a class="result__url" href="...">(...)</a>
                // <a class="result__snippet" ...>(...)</a>
                var linkMatches = Regex.Matches(html, @"<a class=""result__snippet[^""]*""[^>]*href=""(?<url>[^""]+)""[^>]*>(?<snippet>.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var titleMatches = Regex.Matches(html, @"<a class=""result__a""[^>]*href=""(?<url>[^""]+)""[^>]*>(?<title>.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                int count = Math.Min(titleMatches.Count, 15);
                for (int i = 0; i < count; i++)
                {
                    string rawUrl = titleMatches[i].Groups["url"].Value;
                    string title = StripHtml(titleMatches[i].Groups["title"].Value);
                    string snippet = i < linkMatches.Count ? StripHtml(linkMatches[i].Groups["snippet"].Value) : title;

                    // Unpack DuckDuckGo redirect uddg= if present
                    string finalUrl = ExtractCleanUrl(rawUrl);

                    if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(finalUrl) && !ContainsUrl(response.Results, finalUrl))
                    {
                        response.Results.Add(new SearchResultItem
                        {
                            Title = title,
                            Url = finalUrl,
                            DisplayUrl = FormatDisplayUrl(finalUrl),
                            Snippet = snippet,
                            Category = category
                        });
                    }
                }
            }
            catch
            {
                // HTML fetch failed gracefully
            }
        }

        private void FetchWikipediaResults(string query, SearchResponse response)
        {
            try
            {
                string wikiUrl = string.Format("https://en.wikipedia.org/w/api.php?action=opensearch&search={0}&limit=8&namespace=0&format=json", Uri.EscapeDataString(query));
                string json = FetchHttp(wikiUrl, 2500);
                if (string.IsNullOrEmpty(json)) return;

                var arr = _serializer.Deserialize<object[]>(json);
                if (arr != null && arr.Length >= 4)
                {
                    var titles = arr[1] as System.Collections.ArrayList;
                    var snippets = arr[2] as System.Collections.ArrayList;
                    var urls = arr[3] as System.Collections.ArrayList;

                    if (titles != null && urls != null)
                    {
                        for (int i = 0; i < titles.Count; i++)
                        {
                            string t = titles[i] as string ?? "";
                            string u = urls[i] as string ?? "";
                            string s = (snippets != null && i < snippets.Count) ? snippets[i] as string ?? "" : "";

                            if (!string.IsNullOrEmpty(t) && !string.IsNullOrEmpty(u))
                            {
                                if (i == 0 && string.IsNullOrEmpty(response.InstantAnswerTitle))
                                {
                                    response.InstantAnswerTitle = t;
                                    response.InstantAnswerText = s;
                                    response.InstantAnswerUrl = u;
                                }

                                response.Results.Add(new SearchResultItem
                                {
                                    Title = t + " - Wikipedia",
                                    Url = u,
                                    DisplayUrl = FormatDisplayUrl(u),
                                    Snippet = s,
                                    Category = "all"
                                });
                            }
                        }
                    }
                }
            }
            catch {}
        }

        private void GenerateOfflineFallback(string query, string category, SearchResponse response, string errorMsg)
        {
            response.InstantAnswerTitle = "Search: " + query;
            response.InstantAnswerText = "Direct search for '" + query + "'. Click any of the links below to navigate immediately.";
            response.InstantAnswerUrl = "https://duckduckgo.com/?q=" + Uri.EscapeDataString(query);

            response.Results.Add(new SearchResultItem
            {
                Title = query + " - DuckDuckGo Search",
                Url = "https://duckduckgo.com/?q=" + Uri.EscapeDataString(query),
                DisplayUrl = "duckduckgo.com › search",
                Snippet = "View privacy-respecting search results for '" + query + "' on DuckDuckGo.",
                Category = category
            });

            response.Results.Add(new SearchResultItem
            {
                Title = query + " - Wikipedia Reference",
                Url = "https://en.wikipedia.org/wiki/Special:Search?search=" + Uri.EscapeDataString(query),
                DisplayUrl = "en.wikipedia.org › wiki",
                Snippet = "Search encyclopedia articles and reference information about '" + query + "' on Wikipedia.",
                Category = category
            });

            response.Results.Add(new SearchResultItem
            {
                Title = query + " - GitHub Repositories",
                Url = "https://github.com/search?q=" + Uri.EscapeDataString(query),
                DisplayUrl = "github.com › search",
                Snippet = "Discover open source repositories, code, and developer libraries for '" + query + "'.",
                Category = category
            });
        }

        private string ExtractCleanUrl(string rawUrl)
        {
            if (string.IsNullOrEmpty(rawUrl)) return "";
            if (rawUrl.StartsWith("//")) rawUrl = "https:" + rawUrl;

            // Handle DuckDuckGo redirect /l/?uddg=
            int uddgIdx = rawUrl.IndexOf("uddg=");
            if (uddgIdx > 0)
            {
                string encoded = rawUrl.Substring(uddgIdx + 5);
                int amp = encoded.IndexOf('&');
                if (amp > 0) encoded = encoded.Substring(0, amp);
                return Uri.UnescapeDataString(encoded);
            }
            return rawUrl;
        }

        private bool ContainsUrl(List<SearchResultItem> list, string url)
        {
            foreach (var item in list)
            {
                if (string.Equals(item.Url, url, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private string FormatDisplayUrl(string fullUrl)
        {
            try
            {
                var uri = new Uri(fullUrl);
                string path = uri.AbsolutePath.Trim('/');
                if (path.Length > 28) path = path.Substring(0, 25) + "...";
                return string.IsNullOrEmpty(path) ? uri.Host : uri.Host + " › " + path.Replace("/", " › ");
            }
            catch
            {
                return fullUrl;
            }
        }

        private string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            string text = Regex.Replace(html, "<.*?>", string.Empty);
            return WebUtility.HtmlDecode(text).Trim();
        }

        private string FetchHttp(string url, int timeoutMs)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36 Aeris/1.0";
            req.Accept = "application/json, text/html, */*";
            req.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var stream = resp.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
