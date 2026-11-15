using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace AerisBrowser.Core
{
    public class BookmarkItem
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public DateTime DateAdded { get; set; }
    }

    public class BookmarkManager
    {
        private readonly string _filePath;
        private readonly List<BookmarkItem> _bookmarks = new List<BookmarkItem>();
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
        private readonly object _lock = new object();

        public BookmarkManager(string dataDirectory)
        {
            if (!Directory.Exists(dataDirectory))
                Directory.CreateDirectory(dataDirectory);

            _filePath = Path.Combine(dataDirectory, "bookmarks.json");
            Load();

            // Populate initial helpful bookmarks if empty
            if (_bookmarks.Count == 0)
            {
                Add("Wikipedia", "https://en.wikipedia.org");
                Add("GitHub", "https://github.com");
                Add("Hacker News", "https://news.ycombinator.com");
                Add("DuckDuckGo", "https://duckduckgo.com");
            }
        }

        public bool IsBookmarked(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            lock (_lock)
            {
                foreach (var b in _bookmarks)
                {
                    if (string.Equals(b.Url, url, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        public void Add(string title, string url)
        {
            if (string.IsNullOrEmpty(url) || url.StartsWith("about:")) return;
            lock (_lock)
            {
                if (!IsBookmarked(url))
                {
                    _bookmarks.Add(new BookmarkItem
                    {
                        Title = string.IsNullOrEmpty(title) ? url : title,
                        Url = url,
                        DateAdded = DateTime.Now
                    });
                    Save();
                }
            }
        }

        public void Remove(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            lock (_lock)
            {
                _bookmarks.RemoveAll(b => string.Equals(b.Url, url, StringComparison.OrdinalIgnoreCase));
                Save();
            }
        }

        public List<BookmarkItem> GetAll()
        {
            lock (_lock)
            {
                return new List<BookmarkItem>(_bookmarks);
            }
        }

        private void Load()
        {
            if (!File.Exists(_filePath)) return;
            try
            {
                string json = File.ReadAllText(_filePath);
                var loaded = _serializer.Deserialize<List<BookmarkItem>>(json);
                if (loaded != null)
                {
                    lock (_lock)
                    {
                        _bookmarks.Clear();
                        _bookmarks.AddRange(loaded);
                    }
                }
            }
            catch {}
        }

        private void Save()
        {
            try
            {
                string json = _serializer.Serialize(_bookmarks);
                File.WriteAllText(_filePath, json);
            }
            catch {}
        }
    }
}

