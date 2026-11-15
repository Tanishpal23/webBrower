using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using AerisBrowser.Core;
using AerisBrowser.Search;
using Microsoft.Web.WebView2.Core;

namespace AerisBrowser.UI
{
    public class MainForm : Form
    {
        private readonly TabManager _tabManager;
        private readonly ContentBlocker _contentBlocker;
        private readonly PrivacyManager _privacyManager;
        private readonly HistoryManager _historyManager;
        private readonly BookmarkManager _bookmarkManager;
        private readonly DownloadManager _downloadManager;
        private readonly SearchEngineService _searchService;
        private readonly string _dataDir;
        private readonly bool _isIncognito;

        private Panel _pnlTabStrip;
        private FlowLayoutPanel _flpTabs;
        private Button _btnNewTab;
        private Panel _pnlToolbar;
        private Button _btnBack;
        private Button _btnForward;
        private Button _btnReload;
        private Button _btnHome;
        private Panel _pnlOmnibox;
        private Label _lblSecurity;
        private TextBox _txtOmnibox;
        private Button _btnBookmarkStar;
        private Button _btnShield;
        private Button _btnPerformance;
        private Button _btnMenu;
        private Panel _pnlContent;
        private ContextMenuStrip _mainMenu;

        // Find In Page Bar
        private Panel _pnlFind;
        private TextBox _txtFind;
        private Button _btnFindPrev;
        private Button _btnFindNext;
        private Button _btnCloseFind;
        private Label _lblFindStatus;

        // Suggestions Dropdown
        private ListBox _lbSuggestions;
        private Timer _suggestionDebounce;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
        private bool _isSelectingSuggestion = false;

        public MainForm(CoreWebView2Environment env, string dataDir, bool isIncognito = false)
        {
            _dataDir = dataDir;
            _isIncognito = isIncognito;

            _contentBlocker = new ContentBlocker();
            _privacyManager = new PrivacyManager();
            _historyManager = new HistoryManager(dataDir) { IsIncognito = isIncognito };
            _bookmarkManager = new BookmarkManager(dataDir);
            _downloadManager = new DownloadManager();

            string searchResultsTemplate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "Assets", "SearchResults.html");
            _searchService = new SearchEngineService(new DuckDuckGoProvider(), searchResultsTemplate);

            _tabManager = new TabManager(env, _contentBlocker, _privacyManager, _downloadManager, dataDir, isIncognito);

            _downloadManager.DownloadCompleted += item =>
            {
                this.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show("Download complete: " + item.FileName, "Aeris Download Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }));
            };

            // Set Window Icon if present
            LoadAppIcon();

            InitializeComponent();
            SetupShortcuts();
            HookTabManagerEvents();

            // Load saved session or open home tab
            var savedTabs = !_isIncognito ? _tabManager.LoadSavedSession() : null;
            if (savedTabs != null && savedTabs.Count > 0)
            {
                foreach (var url in savedTabs)
                {
                    _tabManager.CreateTab(url);
                }
            }
            else
            {
                _tabManager.CreateTab("about:home");
            }
        }

        private void LoadAppIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
            }
            catch {}
        }

        private void InitializeComponent()
        {
            this.Text = _isIncognito ? "Aeris Browser (Private / Incognito)" : "Aeris Browser";
            this.Size = new Size(1180, 780);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(640, 480);
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = _isIncognito ? Color.FromArgb(32, 33, 36) : Color.FromArgb(241, 243, 244);
            this.KeyPreview = true;

            // 1. Tab Strip (Top)
            _pnlTabStrip = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = _isIncognito ? Color.FromArgb(32, 33, 36) : Color.FromArgb(222, 225, 230),
                Padding = new Padding(8, 4, 8, 0)
            };
            _pnlTabStrip.MouseDown += (s, e) => HideSuggestions();

            _flpTabs = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                AutoScroll = false,
                FlowDirection = FlowDirection.LeftToRight
            };
            _flpTabs.MouseDown += (s, e) => HideSuggestions();

            _btnNewTab = new Button
            {
                Text = "+",
                Size = new Size(28, 28),
                Font = new Font("Segoe UI", 12.0f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = _isIncognito ? Color.White : Color.FromArgb(60, 64, 67),
                Cursor = Cursors.Hand,
                Margin = new Padding(4, 2, 0, 0)
            };
            _btnNewTab.FlatAppearance.BorderSize = 0;
            _btnNewTab.Click += (s, e) =>
            {
                HideSuggestions();
                _tabManager.CreateTab("about:home");
            };

            _pnlTabStrip.Controls.Add(_flpTabs);
            // (Docking to be registered after all panels are configured)

            // 2. Navigation Toolbar
            _pnlToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = _isIncognito ? Color.FromArgb(40, 42, 45) : Color.White,
                Padding = new Padding(8, 6, 8, 6)
            };
            _pnlToolbar.MouseDown += (s, e) => HideSuggestions();

            _btnBack = CreateToolButton("←", "Back (Alt+Left)", 32, (s, e) =>
            {
                HideSuggestions();
                if (_tabManager.ActiveTab != null) _tabManager.ActiveTab.GoBack();
            });

            _btnForward = CreateToolButton("→", "Forward (Alt+Right)", 32, (s, e) =>
            {
                HideSuggestions();
                if (_tabManager.ActiveTab != null) _tabManager.ActiveTab.GoForward();
            });

            _btnReload = CreateToolButton("↻", "Reload (Ctrl+R)", 32, (s, e) =>
            {
                HideSuggestions();
                if (_tabManager.ActiveTab != null)
                {
                    if (_tabManager.ActiveTab.IsLoading) _tabManager.ActiveTab.Stop();
                    else _tabManager.ActiveTab.Reload();
                }
            });

            _btnHome = CreateToolButton("⌂", "Search Home", 32, (s, e) =>
            {
                HideSuggestions();
                if (_tabManager.ActiveTab != null) _tabManager.ActiveTab.LoadHomePage();
            });

            // Omnibox
            _pnlOmnibox = new Panel
            {
                Height = 32,
                BackColor = _isIncognito ? Color.FromArgb(30, 31, 34) : Color.FromArgb(241, 243, 244),
                Padding = new Padding(10, 4, 10, 4)
            };
            _pnlOmnibox.Paint += (s, e) =>
            {
                using (var pen = new Pen(_isIncognito ? Color.FromArgb(60, 64, 67) : Color.FromArgb(218, 220, 224), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, _pnlOmnibox.Width - 1, _pnlOmnibox.Height - 1);
                }
            };

            _lblSecurity = new Label
            {
                Text = "🔒",
                AutoSize = true,
                Location = new Point(8, 6),
                Font = new Font("Segoe UI", 9.0f),
                ForeColor = Color.FromArgb(24, 134, 75),
                Cursor = Cursors.Hand
            };
            _lblSecurity.Click += (s, e) => HideSuggestions();

            _txtOmnibox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = _isIncognito ? Color.FromArgb(30, 31, 34) : Color.FromArgb(241, 243, 244),
                ForeColor = _isIncognito ? Color.White : Color.FromArgb(32, 33, 36),
                Font = new Font("Segoe UI", 10.0f),
                Location = new Point(32, 6),
                Width = 400
            };
            _txtOmnibox.KeyDown += TxtOmnibox_KeyDown;
            _txtOmnibox.TextChanged += TxtOmnibox_TextChanged;

            // Auto-close suggestions when omnibox loses focus
            _txtOmnibox.LostFocus += (s, e) =>
            {
                if (!_isSelectingSuggestion && !_lbSuggestions.ContainsFocus)
                {
                    HideSuggestions();
                }
            };
            _txtOmnibox.Click += (s, e) =>
            {
                if (_txtOmnibox.Text.Trim().Length >= 2)
                {
                    FetchOmniboxSuggestions(_txtOmnibox.Text.Trim());
                }
            };

            _btnBookmarkStar = new Button
            {
                Text = "☆",
                Size = new Size(24, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(120, 120, 120),
                Cursor = Cursors.Hand
            };
            _btnBookmarkStar.FlatAppearance.BorderSize = 0;
            _btnBookmarkStar.Click += (s, e) =>
            {
                HideSuggestions();
                BtnBookmarkStar_Click(s, e);
            };

            _btnShield = new Button
            {
                Text = "🛡️ 0",
                Size = new Size(48, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(26, 115, 232),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnShield.FlatAppearance.BorderSize = 0;
            _btnShield.Click += (s, e) =>
            {
                HideSuggestions();
                OpenShieldMenu();
            };

            _pnlOmnibox.Controls.Add(_lblSecurity);
            _pnlOmnibox.Controls.Add(_txtOmnibox);
            _pnlOmnibox.Controls.Add(_btnBookmarkStar);
            _pnlOmnibox.Controls.Add(_btnShield);

            _btnPerformance = CreateToolButton("⚡", "Performance Dashboard (Ctrl+Shift+P)", 34, (s, e) =>
            {
                HideSuggestions();
                OpenPerformanceDashboard();
            });

            _btnMenu = CreateToolButton("⋮", "Settings & Menu", 32, (s, e) =>
            {
                HideSuggestions();
                ShowMainMenu();
            });

            _pnlToolbar.Controls.Add(_btnBack);
            _pnlToolbar.Controls.Add(_btnForward);
            _pnlToolbar.Controls.Add(_btnReload);
            _pnlToolbar.Controls.Add(_btnHome);
            _pnlToolbar.Controls.Add(_pnlOmnibox);
            _pnlToolbar.Controls.Add(_btnPerformance);
            _pnlToolbar.Controls.Add(_btnMenu);

            // 3. Content Viewport Container
            _pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            _pnlContent.MouseDown += (s, e) => HideSuggestions();

            // Crucial WinForms docking order:
            // Controls added to this.Controls are docked in reverse order.
            // 1. Fill container added FIRST
            // 2. Toolbar added SECOND
            // 3. TabStrip added LAST (so it sits at Y=0, Toolbar at Y=38, Content at Y=82)
            this.Controls.Add(_pnlContent);
            this.Controls.Add(_pnlToolbar);
            this.Controls.Add(_pnlTabStrip);

            // Find in Page Bar (Slide-down)
            InitializeFindBar();

            // Suggestions Dropdown
            _lbSuggestions = new ListBox
            {
                Visible = false,
                Font = new Font("Segoe UI", 9.5f),
                ItemHeight = 24,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };
            _lbSuggestions.MouseDown += (s, e) => { _isSelectingSuggestion = true; };
            _lbSuggestions.Click += LbSuggestions_Click;
            _lbSuggestions.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && _lbSuggestions.SelectedItem != null)
                {
                    string text = _lbSuggestions.SelectedItem.ToString();
                    HideSuggestions();
                    _txtOmnibox.Text = CleanSuggestionText(text);
                    ExecuteOmniboxSearch(_txtOmnibox.Text);
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    HideSuggestions();
                    _txtOmnibox.Focus();
                }
                else if (e.KeyCode == Keys.Up && _lbSuggestions.SelectedIndex == 0)
                {
                    _txtOmnibox.Focus();
                    _txtOmnibox.SelectionStart = _txtOmnibox.Text.Length;
                }
            };
            _lbSuggestions.LostFocus += (s, e) =>
            {
                if (!_txtOmnibox.ContainsFocus)
                {
                    HideSuggestions();
                }
            };

            this.Controls.Add(_lbSuggestions);
            _lbSuggestions.BringToFront();

            _suggestionDebounce = new Timer { Interval = 160 };
            _suggestionDebounce.Tick += (s, e) =>
            {
                _suggestionDebounce.Stop();
                FetchOmniboxSuggestions(_txtOmnibox.Text.Trim());
            };

            // Global window events to auto-close suggestions menu
            this.Deactivate += (s, e) => HideSuggestions();
            this.Resize += (s, e) =>
            {
                LayoutToolbar();
                HideSuggestions();
            };
            this.Move += (s, e) => HideSuggestions();

            LayoutToolbar();
        }

        private void InitializeFindBar()
        {
            _pnlFind = new Panel
            {
                Size = new Size(320, 36),
                BackColor = Color.FromArgb(248, 249, 250),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };

            _txtFind = new TextBox
            {
                Location = new Point(8, 7),
                Size = new Size(160, 24),
                Font = new Font("Segoe UI", 9.0f)
            };
            _txtFind.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    if (_tabManager.ActiveTab != null)
                        _tabManager.ActiveTab.FindInPage(_txtFind.Text.Trim(), !e.Shift);
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    HideFindBar();
                }
            };

            _btnFindPrev = new Button { Text = "▲", Location = new Point(174, 5), Size = new Size(26, 24), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            _btnFindPrev.FlatAppearance.BorderSize = 0;
            _btnFindPrev.Click += (s, e) => { if (_tabManager.ActiveTab != null) _tabManager.ActiveTab.FindInPage(_txtFind.Text.Trim(), false); };

            _btnFindNext = new Button { Text = "▼", Location = new Point(202, 5), Size = new Size(26, 24), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            _btnFindNext.FlatAppearance.BorderSize = 0;
            _btnFindNext.Click += (s, e) => { if (_tabManager.ActiveTab != null) _tabManager.ActiveTab.FindInPage(_txtFind.Text.Trim(), true); };

            _lblFindStatus = new Label { Text = "", Location = new Point(232, 9), AutoSize = true, Font = new Font("Segoe UI", 8.0f), ForeColor = Color.Gray };

            _btnCloseFind = new Button { Text = "✕", Location = new Point(288, 5), Size = new Size(24, 24), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            _btnCloseFind.FlatAppearance.BorderSize = 0;
            _btnCloseFind.Click += (s, e) => HideFindBar();

            _pnlFind.Controls.Add(_txtFind);
            _pnlFind.Controls.Add(_btnFindPrev);
            _pnlFind.Controls.Add(_btnFindNext);
            _pnlFind.Controls.Add(_lblFindStatus);
            _pnlFind.Controls.Add(_btnCloseFind);

            _pnlContent.Controls.Add(_pnlFind);
            _pnlFind.BringToFront();
        }

        private void ShowFindBar()
        {
            _pnlFind.Location = new Point(_pnlContent.Width - _pnlFind.Width - 20, 10);
            _pnlFind.Visible = true;
            _pnlFind.BringToFront();
            _txtFind.Focus();
            _txtFind.SelectAll();
        }

        private void HideFindBar()
        {
            _pnlFind.Visible = false;
            if (_tabManager.ActiveTab != null)
                _tabManager.ActiveTab.SetVisible(true);
        }

        public void HideSuggestions()
        {
            _isSelectingSuggestion = false;
            if (_lbSuggestions != null && _lbSuggestions.Visible)
            {
                _lbSuggestions.Visible = false;
            }
        }

        private Button CreateToolButton(string text, string tooltip, int width, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(width, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = _isIncognito ? Color.FromArgb(220, 220, 220) : Color.FromArgb(60, 64, 67),
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            if (onClick != null) btn.Click += onClick;

            var tt = new ToolTip();
            tt.SetToolTip(btn, tooltip);
            return btn;
        }

        private void LayoutToolbar()
        {
            int left = 8;
            _btnBack.Location = new Point(left, 6); left += _btnBack.Width + 4;
            _btnForward.Location = new Point(left, 6); left += _btnForward.Width + 4;
            _btnReload.Location = new Point(left, 6); left += _btnReload.Width + 4;
            _btnHome.Location = new Point(left, 6); left += _btnHome.Width + 8;

            int rightSpace = _btnMenu.Width + _btnPerformance.Width + 24;
            int omniWidth = Math.Max(200, _pnlToolbar.Width - left - rightSpace);

            _pnlOmnibox.Location = new Point(left, 6);
            _pnlOmnibox.Width = omniWidth;

            _txtOmnibox.Location = new Point(32, 6);
            _txtOmnibox.Width = Math.Max(50, omniWidth - 110);

            _btnBookmarkStar.Location = new Point(omniWidth - 76, 4);
            _btnShield.Location = new Point(omniWidth - 52, 4);

            _btnPerformance.Location = new Point(left + omniWidth + 8, 6);
            _btnMenu.Location = new Point(_btnPerformance.Right + 4, 6);

            _lbSuggestions.Location = new Point(_pnlOmnibox.Left, _pnlToolbar.Bottom);
            _lbSuggestions.Width = _pnlOmnibox.Width;

            if (_pnlFind != null && _pnlFind.Visible)
            {
                _pnlFind.Location = new Point(_pnlContent.Width - _pnlFind.Width - 20, 10);
            }
        }

        private void HookTabManagerEvents()
        {
            _tabManager.TabCreated += tab =>
            {
                tab.TitleChanged += t => UpdateTabStripUI();
                tab.UrlChanged += t => OnTabUrlChanged(t);
                tab.LoadingStateChanged += t => OnTabLoadingStateChanged(t);
                tab.StatusChanged += t => UpdateTabStripUI();
                tab.TabGotFocus += t => HideSuggestions();
                tab.WebMessageReceived += HandleTabWebMessage;
                UpdateTabStripUI();
            };

            _tabManager.TabClosed += tab => UpdateTabStripUI();

            _tabManager.ActiveTabChanged += tab =>
            {
                HideSuggestions();
                foreach (var t in _tabManager.Tabs)
                {
                    bool isActive = (t == tab);
                    t.SetVisible(isActive);
                }
                if (tab != null)
                {
                    if (!_pnlContent.Controls.Contains(tab.ViewContainer))
                    {
                        _pnlContent.Controls.Add(tab.ViewContainer);
                    }
                    tab.ViewContainer.BringToFront();
                    if (tab.Controller != null)
                    {
                        tab.Controller.Bounds = tab.ViewContainer.ClientRectangle;
                    }
                }
                UpdateOmniboxUI();
                UpdateTabStripUI();
                UpdateNavButtons();
            };

            _tabManager.TabsStateUpdated += () => UpdateTabStripUI();
        }

        private void UpdateTabStripUI()
        {
            _flpTabs.SuspendLayout();
            _flpTabs.Controls.Clear();

            int tabCount = Math.Max(1, _tabManager.Tabs.Count);
            int availableWidth = Math.Max(180, _pnlTabStrip.Width - 60);
            int tabWidth = Math.Max(85, Math.Min(180, availableWidth / tabCount));

            foreach (var tab in _tabManager.Tabs)
            {
                var tabBtn = CreateTabButton(tab, tabWidth);
                _flpTabs.Controls.Add(tabBtn);
            }

            _flpTabs.Controls.Add(_btnNewTab);
            _flpTabs.ResumeLayout();

            UpdateNavButtons();
        }

        private Panel CreateTabButton(BrowserTab tab, int tabWidth)
        {
            bool isActive = (tab == _tabManager.ActiveTab);
            var pnl = new Panel
            {
                Size = new Size(tabWidth, 32),
                BackColor = isActive
                    ? (_isIncognito ? Color.FromArgb(40, 42, 45) : Color.White)
                    : (_isIncognito ? Color.FromArgb(28, 29, 31) : Color.FromArgb(210, 213, 218)),
                Margin = new Padding(2, 2, 2, 0),
                Cursor = Cursors.Hand
            };

            string statusIcon = tab.IsSuspended ? "💤 " : (tab.IsLoading ? "🔄 " : "");
            string displayTitle = statusIcon + (string.IsNullOrEmpty(tab.Title) ? "New Tab" : tab.Title);

            var lblTitle = new Label
            {
                Text = displayTitle,
                Location = new Point(8, 7),
                Size = new Size(Math.Max(30, tabWidth - 36), 18),
                Font = new Font("Segoe UI", 9.0f, isActive ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = _isIncognito ? Color.White : Color.FromArgb(32, 33, 36),
                AutoEllipsis = true
            };
            lblTitle.Click += (s, e) =>
            {
                HideSuggestions();
                _tabManager.SetActiveTab(tab);
            };
            pnl.Click += (s, e) =>
            {
                HideSuggestions();
                _tabManager.SetActiveTab(tab);
            };

            var btnClose = new Button
            {
                Text = "×",
                Size = new Size(18, 18),
                Location = new Point(tabWidth - 24, 7),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = _isIncognito ? Color.FromArgb(180, 180, 180) : Color.FromArgb(95, 99, 104),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) =>
            {
                HideSuggestions();
                _tabManager.CloseTab(tab);
            };

            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(btnClose);
            return pnl;
        }

        private void OnTabUrlChanged(BrowserTab tab)
        {
            if (tab == _tabManager.ActiveTab)
            {
                UpdateOmniboxUI();
                UpdateNavButtons();
                if (!string.IsNullOrEmpty(tab.Url) && !tab.Url.StartsWith("about:") && !tab.Url.StartsWith("search:"))
                {
                    _historyManager.AddEntry(tab.Url, tab.Title);
                }
            }
        }

        private void OnTabLoadingStateChanged(BrowserTab tab)
        {
            if (tab == _tabManager.ActiveTab)
            {
                _btnReload.Text = tab.IsLoading ? "✕" : "↻";
                UpdateNavButtons();
            }
            UpdateTabStripUI();
        }

        private void UpdateNavButtons()
        {
            if (_tabManager.ActiveTab != null)
            {
                bool canBack = _tabManager.ActiveTab.CanGoBack;
                bool canFwd = _tabManager.ActiveTab.CanGoForward;

                _btnBack.Enabled = canBack;
                _btnForward.Enabled = canFwd;

                Color activeColor = _isIncognito ? Color.White : Color.FromArgb(60, 64, 67);
                Color disabledColor = _isIncognito ? Color.FromArgb(90, 90, 90) : Color.FromArgb(180, 180, 180);

                _btnBack.ForeColor = canBack ? activeColor : disabledColor;
                _btnForward.ForeColor = canFwd ? activeColor : disabledColor;
            }
        }

        private void UpdateOmniboxUI()
        {
            if (_tabManager.ActiveTab == null) return;
            string url = _tabManager.ActiveTab.Url;

            if (string.IsNullOrEmpty(url) || url == "about:home")
            {
                _txtOmnibox.Text = "";
                _lblSecurity.Text = "🔍";
            }
            else if (url.StartsWith("search:"))
            {
                _txtOmnibox.Text = url.Substring(7);
                _lblSecurity.Text = "🔍";
            }
            else
            {
                _txtOmnibox.Text = url;
                _lblSecurity.Text = url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "🔒" : "⚠️";
            }

            _btnBookmarkStar.Text = _bookmarkManager.IsBookmarked(url) ? "★" : "☆";
            _btnBookmarkStar.ForeColor = _bookmarkManager.IsBookmarked(url) ? Color.FromArgb(244, 180, 0) : Color.FromArgb(120, 120, 120);

            _btnShield.Text = string.Format("🛡️ {0}", _tabManager.ActiveTab.BlockedRequestsCount);
        }

        private void TxtOmnibox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                HideSuggestions();
                ExecuteOmniboxSearch(_txtOmnibox.Text.Trim());
            }
            else if (e.KeyCode == Keys.Down && _lbSuggestions.Visible && _lbSuggestions.Items.Count > 0)
            {
                _lbSuggestions.Focus();
                _lbSuggestions.SelectedIndex = 0;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                HideSuggestions();
                e.SuppressKeyPress = true;
            }
        }

        private void TxtOmnibox_TextChanged(object sender, EventArgs e)
        {
            string query = _txtOmnibox.Text.Trim();
            if (query.Length < 2)
            {
                HideSuggestions();
                return;
            }

            _suggestionDebounce.Stop();
            _suggestionDebounce.Start();
        }

        private void FetchOmniboxSuggestions(string query)
        {
            if (string.IsNullOrEmpty(query)) return;

            var list = _searchService.GetSuggestions(query);
            _lbSuggestions.Items.Clear();

            // Include history suggestions
            var historyMatches = _historyManager.Search(query);
            foreach (var h in historyMatches)
            {
                if (_lbSuggestions.Items.Count >= 5) break;
                _lbSuggestions.Items.Add("🕒 " + h.Title);
            }

            foreach (var s in list)
            {
                if (_lbSuggestions.Items.Count >= 8) break;
                _lbSuggestions.Items.Add("🔍 " + s);
            }

            if (_lbSuggestions.Items.Count > 0 && _txtOmnibox.Focused)
            {
                _lbSuggestions.Height = Math.Min(220, _lbSuggestions.Items.Count * 25 + 4);
                _lbSuggestions.Visible = true;
                _lbSuggestions.BringToFront();
            }
            else
            {
                HideSuggestions();
            }
        }

        private void LbSuggestions_Click(object sender, EventArgs e)
        {
            if (_lbSuggestions.SelectedItem != null)
            {
                string text = _lbSuggestions.SelectedItem.ToString();
                text = CleanSuggestionText(text);

                HideSuggestions();
                _txtOmnibox.Text = text;
                ExecuteOmniboxSearch(text);
            }
        }

        private string CleanSuggestionText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            if (raw.StartsWith("🕒 ") || raw.StartsWith("🔍 "))
                return raw.Substring(2).Trim();
            return raw.Trim();
        }

        private void ExecuteOmniboxSearch(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            HideSuggestions();

            var urlPattern = new System.Text.RegularExpressions.Regex(@"^(https?:\/\/)?([a-zA-Z0-9-]+\.)+[a-zA-Z]{2,}(:\d+)?(\/.*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var isLocalhost = new System.Text.RegularExpressions.Regex(@"^(https?:\/\/)?localhost(:\d+)?(\/.*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (urlPattern.IsMatch(text) || isLocalhost.IsMatch(text))
            {
                string target = text;
                if (!target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    target = "https://" + target;
                }
                if (_tabManager.ActiveTab != null) _tabManager.ActiveTab.Navigate(target);
            }
            else
            {
                PerformSearch(text, "all", 1);
            }
        }

        private void PerformSearch(string query, string category, int page)
        {
            if (_tabManager.ActiveTab == null) return;
            HideSuggestions();

            _tabManager.ActiveTab.CurrentSearchQuery = query;
            string html = _searchService.GenerateResultsHtml(query, category, page);

            // Write results to a persistent clean local file
            try
            {
                string resultsFile = Path.Combine(_dataDir, "search_results.html");
                File.WriteAllText(resultsFile, html, System.Text.Encoding.UTF8);

                string fileUri = "file:///" + resultsFile.Replace('\\', '/');
                _tabManager.ActiveTab.Navigate(fileUri);
                _tabManager.ActiveTab.Title = query + " - Aeris Search";
                _tabManager.ActiveTab.Url = "search:" + query;
                UpdateOmniboxUI();
            }
            catch
            {
                if (_tabManager.ActiveTab.CoreWebView2 != null)
                {
                    _tabManager.ActiveTab.CoreWebView2.NavigateToString(html);
                    _tabManager.ActiveTab.Title = query + " - Aeris Search";
                    _tabManager.ActiveTab.Url = "search:" + query;
                    UpdateOmniboxUI();
                }
            }
        }

        private void HandleTabWebMessage(BrowserTab tab, string json)
        {
            try
            {
                var dict = _serializer.Deserialize<Dictionary<string, object>>(json);
                if (dict == null || !dict.ContainsKey("type")) return;

                string type = dict["type"].ToString();
                if (type == "navigate" && dict.ContainsKey("url"))
                {
                    tab.Navigate(dict["url"].ToString());
                }
                else if (type == "performSearch" && dict.ContainsKey("query"))
                {
                    string q = dict["query"].ToString();
                    string cat = dict.ContainsKey("category") ? dict["category"].ToString() : "all";
                    int page = dict.ContainsKey("page") ? Convert.ToInt32(dict["page"]) : 1;
                    PerformSearch(q, cat, page);
                }
                else if (type == "goHome")
                {
                    tab.LoadHomePage();
                }
                else if (type == "openPerformance")
                {
                    OpenPerformanceDashboard();
                }
                else if (type == "openSettings")
                {
                    OpenSettings();
                }
                else if (type == "getSuggestions" && dict.ContainsKey("query"))
                {
                    string q = dict["query"].ToString();
                    var list = _searchService.GetSuggestions(q);
                    var resp = new Dictionary<string, object>
                    {
                        { "type", "suggestionsResult" },
                        { "suggestions", list }
                    };
                    if (tab.CoreWebView2 != null)
                        tab.CoreWebView2.PostWebMessageAsJson(_serializer.Serialize(resp));
                }
            }
            catch {}
        }

        private void BtnBookmarkStar_Click(object sender, EventArgs e)
        {
            if (_tabManager.ActiveTab == null) return;
            string url = _tabManager.ActiveTab.Url;
            string title = _tabManager.ActiveTab.Title;

            if (string.IsNullOrEmpty(url) || url.StartsWith("about:")) return;

            if (_bookmarkManager.IsBookmarked(url))
            {
                _bookmarkManager.Remove(url);
            }
            else
            {
                _bookmarkManager.Add(title, url);
            }
            UpdateOmniboxUI();
        }

        private void OpenShieldMenu()
        {
            if (_tabManager.ActiveTab == null) return;
            string host = "";
            try { host = new Uri(_tabManager.ActiveTab.Url).Host; } catch {}

            var menu = new ContextMenuStrip();
            menu.Items.Add(string.Format("🛡️ Content Blocker: {0}", _contentBlocker.Mode)).Enabled = false;
            menu.Items.Add(string.Format("Blocked on this tab: {0}", _tabManager.ActiveTab.BlockedRequestsCount)).Enabled = false;
            menu.Items.Add(string.Format("Total requests blocked: {0}", _contentBlocker.TotalBlockedCount)).Enabled = false;
            menu.Items.Add(new ToolStripSeparator());

            if (!string.IsNullOrEmpty(host))
            {
                bool isWhitelisted = _contentBlocker.IsWhitelisted(host);
                var itemWhitelist = menu.Items.Add(isWhitelisted ? "Disable Ad-Block for " + host : "Whitelist " + host + " (Allow Ads)");
                itemWhitelist.Click += (s, e) =>
                {
                    if (isWhitelisted) _contentBlocker.RemoveFromWhitelist(host);
                    else _contentBlocker.AddToWhitelist(host);
                    _tabManager.ActiveTab.Reload();
                };
            }

            var itemSettings = menu.Items.Add("Blocker Settings...");
            itemSettings.Click += (s, e) => OpenSettings();

            menu.Show(_btnShield, new Point(0, _btnShield.Height));
        }

        private void OpenPerformanceDashboard()
        {
            using (var dlg = new PerformanceDashboard(_tabManager, _contentBlocker))
            {
                if (this.Icon != null) dlg.Icon = this.Icon;
                dlg.ShowDialog(this);
            }
        }

        private void OpenSettings()
        {
            using (var dlg = new SettingsDialog(_contentBlocker, _privacyManager, _tabManager, _historyManager))
            {
                if (this.Icon != null) dlg.Icon = this.Icon;
                dlg.ShowDialog(this);
            }
        }

        private void ShowMainMenu()
        {
            if (_mainMenu == null)
            {
                _mainMenu = new ContextMenuStrip();
                _mainMenu.Items.Add("📄 New Tab (Ctrl+T)", null, (s, e) => _tabManager.CreateTab("about:home"));
                _mainMenu.Items.Add("🕶️ New Incognito Window (Ctrl+Shift+N)", null, (s, e) => OpenIncognitoWindow());
                _mainMenu.Items.Add(new ToolStripSeparator());
                _mainMenu.Items.Add("🔍 Find in Page (Ctrl+F)", null, (s, e) => ShowFindBar());
                _mainMenu.Items.Add("🕒 Browsing History (Ctrl+H)", null, (s, e) => OpenHistory());
                _mainMenu.Items.Add("★ Bookmarks Manager (Ctrl+B)", null, (s, e) => OpenBookmarks());
                _mainMenu.Items.Add("⚡ Performance & RAM Monitor (Ctrl+Shift+P)", null, (s, e) => OpenPerformanceDashboard());
                _mainMenu.Items.Add(new ToolStripSeparator());
                _mainMenu.Items.Add("⚙️ Privacy & Browser Settings", null, (s, e) => OpenSettings());
                _mainMenu.Items.Add("ℹ️ About Aeris Browser", null, (s, e) => ShowAbout());
                _mainMenu.Items.Add(new ToolStripSeparator());
                _mainMenu.Items.Add("❌ Exit", null, (s, e) => this.Close());
            }
            _mainMenu.Show(_btnMenu, new Point(0, _btnMenu.Height));
        }

        private void OpenIncognitoWindow()
        {
            string incognitoDir = Path.Combine(Path.GetTempPath(), "AerisIncognito_" + Guid.NewGuid().ToString("N"));
            var envOptions = new CoreWebView2EnvironmentOptions();
            var env = CoreWebView2Environment.CreateAsync(null, incognitoDir, envOptions).GetAwaiter().GetResult();

            var incognitoForm = new MainForm(env, incognitoDir, true);
            if (this.Icon != null) incognitoForm.Icon = this.Icon;
            incognitoForm.Show();
        }

        private void OpenHistory()
        {
            using (var dlg = new HistoryViewer(_historyManager, url =>
            {
                if (_tabManager.ActiveTab != null) _tabManager.ActiveTab.Navigate(url);
            }))
            {
                if (this.Icon != null) dlg.Icon = this.Icon;
                dlg.ShowDialog(this);
            }
        }

        private void OpenBookmarks()
        {
            using (var dlg = new BookmarksViewer(_bookmarkManager, url =>
            {
                if (_tabManager.ActiveTab != null) _tabManager.ActiveTab.Navigate(url);
            }))
            {
                if (this.Icon != null) dlg.Icon = this.Icon;
                dlg.ShowDialog(this);
            }
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                "Aeris Browser v1.0\n\n" +
                "Lightweight, Privacy-Focused Web Browser & Ad-Free Search Engine\n\n" +
                "• Engine: Native Evergreen Chromium Blink/V8 (WebView2)\n" +
                "• Memory: Inactive tab suspension & working set optimization\n" +
                "• Search: Ad-free, tracker-free integrated search engine\n" +
                "• Privacy: HTTPS-first, zero telemetry, local history only",
                "About Aeris Browser", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SetupShortcuts()
        {
            this.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.T)
                {
                    HideSuggestions();
                    _tabManager.CreateTab("about:home");
                    e.SuppressKeyPress = true;
                }
                else if (e.Control && e.KeyCode == Keys.W)
                {
                    HideSuggestions();
                    if (_tabManager.ActiveTab != null) _tabManager.CloseTab(_tabManager.ActiveTab);
                    e.SuppressKeyPress = true;
                }
                else if (e.Control && e.KeyCode == Keys.R)
                {
                    HideSuggestions();
                    if (_tabManager.ActiveTab != null) _tabManager.ActiveTab.Reload();
                    e.SuppressKeyPress = true;
                }
                else if (e.Control && e.KeyCode == Keys.L)
                {
                    _txtOmnibox.Focus();
                    _txtOmnibox.SelectAll();
                    e.SuppressKeyPress = true;
                }
                else if (e.Control && e.KeyCode == Keys.F)
                {
                    ShowFindBar();
                    e.SuppressKeyPress = true;
                }
                else if (e.Control && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add))
                {
                    if (_tabManager.ActiveTab != null) _tabManager.ActiveTab.ZoomIn();
                    e.SuppressKeyPress = true;
                }
                else if (e.Control && (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract))
                {
                    if (_tabManager.ActiveTab != null) _tabManager.ActiveTab.ZoomOut();
                    e.SuppressKeyPress = true;
                }
                else if (e.Control && (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0))
                {
                    if (_tabManager.ActiveTab != null) _tabManager.ActiveTab.ZoomReset();
                    e.SuppressKeyPress = true;
                }
                else if (e.Control && e.KeyCode == Keys.H)
                {
                    HideSuggestions();
                    OpenHistory();
                    e.SuppressKeyPress = true;
                }
                else if (e.Control && e.KeyCode == Keys.B)
                {
                    HideSuggestions();
                    OpenBookmarks();
                    e.SuppressKeyPress = true;
                }
                else if (e.Control && e.Shift && e.KeyCode == Keys.P)
                {
                    HideSuggestions();
                    OpenPerformanceDashboard();
                    e.SuppressKeyPress = true;
                }
                else if (e.Control && e.Shift && e.KeyCode == Keys.N)
                {
                    HideSuggestions();
                    OpenIncognitoWindow();
                    e.SuppressKeyPress = true;
                }
                else if (e.Alt && e.KeyCode == Keys.Left)
                {
                    HideSuggestions();
                    if (_tabManager.ActiveTab != null) _tabManager.ActiveTab.GoBack();
                    e.SuppressKeyPress = true;
                }
                else if (e.Alt && e.KeyCode == Keys.Right)
                {
                    HideSuggestions();
                    if (_tabManager.ActiveTab != null) _tabManager.ActiveTab.GoForward();
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    HideSuggestions();
                    if (_pnlFind.Visible) HideFindBar();
                }
            };
        }
    }
}
