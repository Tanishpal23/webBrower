using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Web.WebView2.Core;

namespace AerisBrowser.Core
{
    public class DownloadItem
    {
        public string FileName { get; set; }
        public string ResultFilePath { get; set; }
        public long TotalBytes { get; set; }
        public long ReceivedBytes { get; set; }
        public string State { get; set; }
        public int PercentComplete { get; set; }
        public DateTime StartTime { get; set; }

        public string FormattedSize
        {
            get
            {
                if (TotalBytes <= 0) return string.Format("{0:0.0} MB", ReceivedBytes / (1024.0 * 1024.0));
                return string.Format("{0:0.0} / {1:0.0} MB", ReceivedBytes / (1024.0 * 1024.0), TotalBytes / (1024.0 * 1024.0));
            }
        }
    }

    public class DownloadManager
    {
        private readonly List<DownloadItem> _downloads = new List<DownloadItem>();
        private readonly object _lock = new object();

        public event Action<DownloadItem> DownloadStarted;
        public event Action<DownloadItem> DownloadUpdated;
        public event Action<DownloadItem> DownloadCompleted;

        public List<DownloadItem> GetDownloads()
        {
            lock (_lock)
            {
                return new List<DownloadItem>(_downloads);
            }
        }

        public void HandleDownloadStarting(object sender, CoreWebView2DownloadStartingEventArgs e)
        {
            string fileName = Path.GetFileName(e.ResultFilePath);
            var item = new DownloadItem
            {
                FileName = fileName,
                ResultFilePath = e.ResultFilePath,
                TotalBytes = e.DownloadOperation.TotalBytesToReceive.HasValue ? (long)e.DownloadOperation.TotalBytesToReceive.Value : 0,
                ReceivedBytes = 0,
                State = "InProgress",
                PercentComplete = 0,
                StartTime = DateTime.Now
            };

            lock (_lock)
            {
                _downloads.Insert(0, item);
            }

            if (DownloadStarted != null)
                DownloadStarted(item);

            e.DownloadOperation.BytesReceivedChanged += (s, args) =>
            {
                item.ReceivedBytes = (long)e.DownloadOperation.BytesReceived;
                if (item.TotalBytes > 0)
                {
                    item.PercentComplete = (int)((item.ReceivedBytes * 100) / item.TotalBytes);
                }
                if (DownloadUpdated != null)
                    DownloadUpdated(item);
            };

            e.DownloadOperation.StateChanged += (s, args) =>
            {
                item.State = e.DownloadOperation.State.ToString();
                if (e.DownloadOperation.State == CoreWebView2DownloadState.Completed)
                {
                    item.PercentComplete = 100;
                    if (DownloadCompleted != null)
                        DownloadCompleted(item);
                }
            };
        }

        public void OpenFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                try { Process.Start(filePath); } catch {}
            }
        }

        public void ShowInFolder(string filePath)
        {
            if (File.Exists(filePath))
            {
                try { Process.Start("explorer.exe", string.Format("/select,\"{0}\"", filePath)); } catch {}
            }
        }
    }
}
