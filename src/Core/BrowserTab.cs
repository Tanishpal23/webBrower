using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace AerisBrowser.Core
{
    public class BrowserTab : IDisposable
    {
        public string Id { get; private set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string CurrentSearchQuery { get; set; }
        public bool IsSuspended { get; private set; }
        public bool IsLoading { get; set; }
        public bool IsIncognito { get; set; }
        public long BlockedRequestsCount { get; private set; }
        public DateTime LastActiveTime { get; set; }

        public Panel ViewContainer { get; private set; }
        public CoreWebView2Controller Controller { get; private set; }
        public CoreWebView2 CoreWebView2 { get { return Controller != null ? Controller.CoreWebView2 : null; } }
        public bool IsInitialized { get; private set; }

        public bool CanGoBack { get { return CoreWebView2 != null && CoreWebView2.CanGoBack; } }
        public bool CanGoForward { get { return CoreWebView2 != null && CoreWebView2.CanGoForward; } }

        public double ZoomFactor
        {
            get { return Controller != null ? Controller.ZoomFactor : 1.0; }
            set { if (Controller != null) Controller.ZoomFactor = Math.Max(0.25, Math.Min(4.0, value)); }
        }

        private string _pendingUrl;
        private ContentBlocker _contentBlocker;
        private PrivacyManager _privacyManager;
        private DownloadManager _downloadManager;
        private CoreWebView2Environment _environment;

        public event Action<BrowserTab> TitleChanged;
        public event Action<BrowserTab> UrlChanged;
        public event Action<BrowserTab> LoadingStateChanged;
        public event Action<BrowserTab> StatusChanged;
        public event Action<BrowserTab> TabGotFocus;
        public event Action<BrowserTab, string> WebMessageReceived;

        public BrowserTab(bool isIncognito = false)
        {
            Id = Guid.NewGuid().ToString("N");
            Title = "New Tab";
            Url = "about:home";
            IsIncognito = isIncognito;
            LastActiveTime = DateTime.Now;

            ViewContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            ViewContainer.Resize += ViewContainer_Resize;
        }

        private void ViewContainer_Resize(object sender, EventArgs e)
        {
            if (Controller != null && ViewContainer.Width > 0 && ViewContainer.Height > 0)
            {
                Controller.Bounds = ViewContainer.ClientRectangle;
            }
        }

        public void SetVisible(bool visible)
        {
            ViewContainer.Visible = visible;
            if (visible)
            {
                ViewContainer.BringToFront();
            }
            if (Controller != null)
            {
                Controller.IsVisible = visible;
                if (visible && ViewContainer.Width > 0 && ViewContainer.Height > 0)
                {
                    Controller.Bounds = ViewContainer.ClientRectangle;
                }
            }
        }

        public void Initialize(CoreWebView2Environment env, ContentBlocker blocker, PrivacyManager privacy, DownloadManager downloads)
        {
            _environment = env;
            _contentBlocker = blocker;
            _privacyManager = privacy;
            _downloadManager = downloads;

            IntPtr hwnd = ViewContainer.Handle;

            var task = env.CreateCoreWebView2ControllerAsync(hwnd);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Title = "Error initializing engine";
                    if (TitleChanged != null) TitleChanged(this);
                    return;
                }

                ViewContainer.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        Controller = t.Result;
                        Controller.Bounds = ViewContainer.ClientRectangle;
                        Controller.IsVisible = ViewContainer.Visible;
                        IsInitialized = true;

                        // Hook GotFocus to hide any open dropdowns/menus in the shell
                        Controller.GotFocus += (s, e) =>
                        {
                            if (TabGotFocus != null)
                                TabGotFocus(this);
                        };

                        var core = Controller.CoreWebView2;
                        if (core != null)
                        {
                            _privacyManager.ConfigureSecuritySettings(core.Settings);

                            // Content blocking request filter
                            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                            core.WebResourceRequested += CoreWebView2_WebResourceRequested;

                            // Inject cosmetic ad-hiding CSS
                            if (_contentBlocker != null && _contentBlocker.CosmeticFilteringEnabled)
                            {
                                core.AddScriptToExecuteOnDocumentCreatedAsync(ContentBlocker.CosmeticFilterScript);
                            }

                            core.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;
                            core.SourceChanged += CoreWebView2_SourceChanged;
                            core.NavigationStarting += CoreWebView2_NavigationStarting;
                            core.NavigationCompleted += CoreWebView2_NavigationCompleted;
                            core.PermissionRequested += _privacyManager.HandlePermissionRequested;

                            if (_downloadManager != null)
                            {
                                core.DownloadStarting += _downloadManager.HandleDownloadStarting;
                            }

                            core.WebMessageReceived += CoreWebView2_WebMessageReceived;

                            if (!string.IsNullOrEmpty(_pendingUrl))
                            {
                                string nav = _pendingUrl;
                                _pendingUrl = null;
                                Navigate(nav);
                            }
                            else if (Url == "about:home")
                            {
                                LoadHomePage();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Title = "Init error: " + ex.Message;
                        if (TitleChanged != null) TitleChanged(this);
                    }
                }));
            });
        }

        public void Navigate(string targetUrl)
        {
            if (string.IsNullOrEmpty(targetUrl)) return;
            Url = targetUrl;
            LastActiveTime = DateTime.Now;

            if (IsSuspended)
            {
                Resume();
                return;
            }

            if (!IsInitialized || CoreWebView2 == null)
            {
                _pendingUrl = targetUrl;
                return;
            }

            string finalUrl = _privacyManager != null ? _privacyManager.EnsureSecureUrl(targetUrl) : targetUrl;

            try
            {
                if (finalUrl.StartsWith("about:home", StringComparison.OrdinalIgnoreCase))
                {
                    LoadHomePage();
                }
                else
                {
                    CoreWebView2.Navigate(finalUrl);
                }
            }
            catch {}
        }

        public void LoadHomePage()
        {
            if (!IsInitialized || CoreWebView2 == null) return;
            try
            {
                string homePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "Assets", "SearchHome.html");
                if (File.Exists(homePath))
                {
                    CoreWebView2.Navigate("file:///" + homePath.Replace('\\', '/'));
                }
                else
                {
                    CoreWebView2.NavigateToString("<html><body style='font-family:sans-serif;text-align:center;padding:50px;'><h1>Aeris Browser</h1><p>Minimalist, Lightweight, Privacy-First</p></body></html>");
                }
                Title = "New Tab";
                Url = "about:home";
                CurrentSearchQuery = null;
                if (TitleChanged != null) TitleChanged(this);
            }
            catch {}
        }

        public void Suspend()
        {
            if (IsSuspended) return;

            IsSuspended = true;
            try
            {
                if (CoreWebView2 != null)
                {
                    CoreWebView2.Navigate("about:blank");
                }
                if (Controller != null)
                {
                    Controller.IsVisible = false;
                }
            }
            catch {}

            if (StatusChanged != null) StatusChanged(this);
        }

        public void Resume()
        {
            if (!IsSuspended) return;

            IsSuspended = false;
            LastActiveTime = DateTime.Now;
            if (Controller != null)
            {
                Controller.IsVisible = ViewContainer.Visible;
                if (ViewContainer.Width > 0 && ViewContainer.Height > 0)
                    Controller.Bounds = ViewContainer.ClientRectangle;
            }

            if (!string.IsNullOrEmpty(Url) && Url != "about:blank")
            {
                if (Url == "about:home")
                    LoadHomePage();
                else if (CoreWebView2 != null)
                    CoreWebView2.Navigate(Url);
            }

            if (StatusChanged != null) StatusChanged(this);
        }

        public void ZoomIn() { ZoomFactor += 0.1; }
        public void ZoomOut() { ZoomFactor -= 0.1; }
        public void ZoomReset() { ZoomFactor = 1.0; }

        public void FindInPage(string text, bool forward = true)
        {
            if (CoreWebView2 == null || string.IsNullOrEmpty(text)) return;
            try
            {
                string escaped = text.Replace("\\", "\\\\").Replace("'", "\\'");
                string js = string.Format("window.find('{0}', false, {1}, true, false, false, false);", escaped, forward ? "false" : "true");
                CoreWebView2.ExecuteScriptAsync(js);
            }
            catch {}
        }

        private void CoreWebView2_WebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (_contentBlocker == null) return;

            try
            {
                var uri = new Uri(e.Request.Uri);
                string reason;
                if (_contentBlocker.ShouldBlock(uri, out reason))
                {
                    BlockedRequestsCount++;
                    e.Response = _environment.CreateWebResourceResponse(
                        null, 403, "Blocked by Aeris Content Blocker", "Content-Type: text/plain");
                }
            }
            catch {}
        }

        private void CoreWebView2_DocumentTitleChanged(object sender, object e)
        {
            if (IsSuspended || CoreWebView2 == null) return;
            string newTitle = CoreWebView2.DocumentTitle;
            if (!string.IsNullOrEmpty(newTitle) && newTitle != "about:blank")
            {
                Title = newTitle;
                if (TitleChanged != null) TitleChanged(this);
            }
        }

        private void CoreWebView2_SourceChanged(object sender, CoreWebView2SourceChangedEventArgs e)
        {
            if (IsSuspended || CoreWebView2 == null) return;
            string src = CoreWebView2.Source;
            if (string.IsNullOrEmpty(src) || src == "about:blank" || src.StartsWith("data:"))
                return;

            if (src.Contains("SearchHome.html"))
            {
                Url = "about:home";
                Title = "New Tab";
            }
            else if (src.Contains("search_results.html") && !string.IsNullOrEmpty(CurrentSearchQuery))
            {
                Url = "search:" + CurrentSearchQuery;
                Title = CurrentSearchQuery + " - Aeris Search";
            }
            else
            {
                Url = src;
                CurrentSearchQuery = null;
            }

            if (UrlChanged != null) UrlChanged(this);
        }

        private void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (IsSuspended) return;
            IsLoading = true;
            if (LoadingStateChanged != null) LoadingStateChanged(this);
        }

        private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (IsSuspended) return;
            IsLoading = false;
            if (LoadingStateChanged != null) LoadingStateChanged(this);
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.WebMessageAsJson;
                if (WebMessageReceived != null)
                    WebMessageReceived(this, json);
            }
            catch {}
        }

        public void Reload()
        {
            if (IsSuspended) Resume();
            else if (CoreWebView2 != null) CoreWebView2.Reload();
        }

        public void Stop()
        {
            if (CoreWebView2 != null) CoreWebView2.Stop();
        }

        public void GoBack()
        {
            if (CoreWebView2 != null && CoreWebView2.CanGoBack)
                CoreWebView2.GoBack();
        }

        public void GoForward()
        {
            if (CoreWebView2 != null && CoreWebView2.CanGoForward)
                CoreWebView2.GoForward();
        }

        public void Dispose()
        {
            if (Controller != null)
            {
                Controller.Close();
                Controller = null;
            }
            if (ViewContainer != null)
            {
                ViewContainer.Dispose();
                ViewContainer = null;
            }
        }
    }
}
