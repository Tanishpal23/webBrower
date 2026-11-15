using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using Microsoft.Web.WebView2.Core;

namespace AerisBrowser.Core
{
    public class TabManager
    {
        private readonly List<BrowserTab> _tabs = new List<BrowserTab>();
        private readonly string _sessionFile;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
        private System.Windows.Forms.Timer _suspensionTimer;

        public CoreWebView2Environment Environment { get; private set; }
        public ContentBlocker ContentBlocker { get; private set; }
        public PrivacyManager PrivacyManager { get; private set; }
        public DownloadManager DownloadManager { get; private set; }
        public bool IsIncognito { get; private set; }

        public BrowserTab ActiveTab { get; private set; }
        public int SuspensionTimeoutMinutes { get; set; }
        public bool AutoSuspendEnabled { get; set; }

        public event Action<BrowserTab> TabCreated;
        public event Action<BrowserTab> TabClosed;
        public event Action<BrowserTab> ActiveTabChanged;
        public event Action TabsStateUpdated;

        public List<BrowserTab> Tabs { get { return new List<BrowserTab>(_tabs); } }

        public TabManager(CoreWebView2Environment env, ContentBlocker blocker, PrivacyManager privacy, DownloadManager downloads, string dataDir, bool isIncognito = false)
        {
            Environment = env;
            ContentBlocker = blocker;
            PrivacyManager = privacy;
            DownloadManager = downloads;
            IsIncognito = isIncognito;

            SuspensionTimeoutMinutes = 3; // 3 minutes default inactivity suspension
            AutoSuspendEnabled = true;

            if (!string.IsNullOrEmpty(dataDir) && !Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir);

            _sessionFile = !string.IsNullOrEmpty(dataDir) ? Path.Combine(dataDir, "session.json") : null;

            // Setup background tab suspension timer (low overhead check every 30s)
            _suspensionTimer = new System.Windows.Forms.Timer();
            _suspensionTimer.Interval = 30000;
            _suspensionTimer.Tick += SuspensionTimer_Tick;
            _suspensionTimer.Start();
        }

        public BrowserTab CreateTab(string initialUrl = "about:home")
        {
            var tab = new BrowserTab(IsIncognito);
            _tabs.Add(tab);

            tab.Initialize(Environment, ContentBlocker, PrivacyManager, DownloadManager);
            if (!string.IsNullOrEmpty(initialUrl))
            {
                tab.Navigate(initialUrl);
            }

            if (TabCreated != null)
                TabCreated(tab);

            SetActiveTab(tab);
            SaveSession();

            return tab;
        }

        public void SetActiveTab(BrowserTab tab)
        {
            if (tab == null || !_tabs.Contains(tab)) return;

            ActiveTab = tab;
            tab.LastActiveTime = DateTime.Now;

            // If the tab was suspended, resume it immediately
            if (tab.IsSuspended)
            {
                tab.Resume();
            }

            if (ActiveTabChanged != null)
                ActiveTabChanged(tab);

            if (TabsStateUpdated != null)
                TabsStateUpdated();
        }

        public void CloseTab(BrowserTab tab)
        {
            if (tab == null || !_tabs.Contains(tab)) return;

            int index = _tabs.IndexOf(tab);
            _tabs.Remove(tab);

            if (TabClosed != null)
                TabClosed(tab);

            // Dispose webview and resources
            tab.Dispose();

            // Select new active tab if the closed tab was active
            if (ActiveTab == tab)
            {
                if (_tabs.Count > 0)
                {
                    int nextIdx = Math.Min(index, _tabs.Count - 1);
                    SetActiveTab(_tabs[nextIdx]);
                }
                else
                {
                    // Create a fresh tab if the user closed the last one
                    CreateTab("about:home");
                }
            }

            SaveSession();

            if (TabsStateUpdated != null)
                TabsStateUpdated();
        }

        public void CloseOtherTabs(BrowserTab keepTab)
        {
            var toRemove = new List<BrowserTab>();
            foreach (var t in _tabs)
            {
                if (t != keepTab) toRemove.Add(t);
            }
            foreach (var t in toRemove)
            {
                CloseTab(t);
            }
        }

        public void HibernateAllInactiveTabs()
        {
            foreach (var tab in _tabs)
            {
                if (tab != ActiveTab && !tab.IsSuspended)
                {
                    tab.Suspend();
                }
            }
            if (TabsStateUpdated != null)
                TabsStateUpdated();
        }

        private void SuspensionTimer_Tick(object sender, EventArgs e)
        {
            if (!AutoSuspendEnabled) return;

            var now = DateTime.Now;
            bool stateChanged = false;

            foreach (var tab in _tabs)
            {
                if (tab != ActiveTab && !tab.IsSuspended)
                {
                    var idleTime = now - tab.LastActiveTime;
                    if (idleTime.TotalMinutes >= SuspensionTimeoutMinutes)
                    {
                        tab.Suspend();
                        stateChanged = true;
                    }
                }
            }

            if (stateChanged && TabsStateUpdated != null)
            {
                TabsStateUpdated();
            }
        }

        public void SaveSession()
        {
            if (IsIncognito || string.IsNullOrEmpty(_sessionFile)) return;

            try
            {
                var list = new List<string>();
                foreach (var t in _tabs)
                {
                    if (!string.IsNullOrEmpty(t.Url) && t.Url != "about:blank")
                        list.Add(t.Url);
                }
                File.WriteAllText(_sessionFile, _serializer.Serialize(list));
            }
            catch {}
        }

        public List<string> LoadSavedSession()
        {
            if (IsIncognito || string.IsNullOrEmpty(_sessionFile) || !File.Exists(_sessionFile))
                return new List<string>();

            try
            {
                string json = File.ReadAllText(_sessionFile);
                var list = _serializer.Deserialize<List<string>>(json);
                return list ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}

