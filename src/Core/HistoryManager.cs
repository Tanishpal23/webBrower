using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace AerisBrowser.Core
{
    public class HistoryEntry
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public DateTime Timestamp { get; set; }
        public string FormattedTime { get { return Timestamp.ToString("MMM dd, yyyy HH:mm"); } }
    }

    public class HistoryManager
    {
        private readonly string _filePath;
        private readonly List<HistoryEntry> _entries = new List<HistoryEntry>();
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
        private readonly object _lock = new object();

        public bool IsIncognito { get; set; }

        public HistoryManager(string dataDirectory)
        {
            if (!Directory.Exists(dataDirectory))
                Directory.CreateDirectory(dataDirectory);

            _filePath = Path.Combine(dataDirectory, "history.json");
            Load();
        }

        public void AddEntry(string url, string title)
        {
            if (IsIncognito || string.IsNullOrEmpty(url) || url.StartsWith("about:") || url.StartsWith("data:"))
                return;

            lock (_lock)
            {
                // Don't duplicate if identical to last entry
                if (_entries.Count > 0 && _entries[0].Url == url)
                {
                    _entries[0].Timestamp = DateTime.Now;
                    if (!string.IsNullOrEmpty(title) && title != url)
                        _entries[0].Title = title;
                }
                else
                {
                    _entries.Insert(0, new HistoryEntry
                    {
                        Url = url,
                        Title = string.IsNullOrEmpty(title) ? url : title,
                        Timestamp = DateTime.Now
                    });

                    // Keep at most 2000 items to conserve memory
                    if (_entries.Count > 2000)
                        _entries.RemoveAt(_entries.Count - 1);
                }

                Save();
            }
        }

        public List<HistoryEntry> GetHistory(int limit = 100)
        {
            lock (_lock)
            {
                int count = Math.Min(limit, _entries.Count);
                return _entries.GetRange(0, count);
            }
        }

        public List<HistoryEntry> Search(string query)
        {
            var results = new List<HistoryEntry>();
            if (string.IsNullOrWhiteSpace(query))
                return results;

            string q = query.ToLowerInvariant();
            lock (_lock)
            {
                foreach (var item in _entries)
                {
                    if ((item.Title != null && item.Title.ToLowerInvariant().Contains(q)) ||
                        (item.Url != null && item.Url.ToLowerInvariant().Contains(q)))
                    {
                        results.Add(item);
                        if (results.Count >= 50) break;
                    }
                }
            }
            return results;
        }

        public void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
                Save();
            }
        }

        private void Load()
        {
            if (!File.Exists(_filePath)) return;
            try
            {
                string json = File.ReadAllText(_filePath);
                var loaded = _serializer.Deserialize<List<HistoryEntry>>(json);
                if (loaded != null)
                {
                    lock (_lock)
                    {
                        _entries.Clear();
                        _entries.AddRange(loaded);
                    }
                }
            }
            catch {}
        }

        private void Save()
        {
            if (IsIncognito) return;
            try
            {
                string json = _serializer.Serialize(_entries);
                File.WriteAllText(_filePath, json);
            }
            catch {}
        }
    }
}

