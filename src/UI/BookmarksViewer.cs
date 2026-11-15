using System;
using System.Drawing;
using System.Windows.Forms;
using AerisBrowser.Core;

namespace AerisBrowser.UI
{
    public class BookmarksViewer : Form
    {
        private readonly BookmarkManager _bookmarkManager;
        private readonly Action<string> _navigateCallback;

        private ListView _lvBookmarks;

        public BookmarksViewer(BookmarkManager bookmarkManager, Action<string> navigateCallback)
        {
            _bookmarkManager = bookmarkManager;
            _navigateCallback = navigateCallback;

            InitializeComponent();
            LoadEntries();
        }

        private void InitializeComponent()
        {
            this.Text = "Bookmarks Manager";
            this.Size = new Size(620, 440);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = Color.White;

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                Padding = new Padding(16, 8, 16, 8)
            };

            var btnDelete = new Button
            {
                Text = "🗑️ Delete Selected",
                Location = new Point(16, 8),
                Size = new Size(140, 30),
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnDelete.Click += (s, e) =>
            {
                if (_lvBookmarks.SelectedItems.Count > 0)
                {
                    string url = _lvBookmarks.SelectedItems[0].SubItems[1].Text;
                    _bookmarkManager.Remove(url);
                    LoadEntries();
                }
            };

            var btnOpen = new Button
            {
                Text = "🔗 Open Selected",
                Location = new Point(166, 8),
                Size = new Size(130, 30),
                BackColor = Color.FromArgb(232, 240, 254),
                ForeColor = Color.FromArgb(26, 115, 232),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnOpen.Click += (s, e) =>
            {
                if (_lvBookmarks.SelectedItems.Count > 0)
                {
                    string url = _lvBookmarks.SelectedItems[0].SubItems[1].Text;
                    if (_navigateCallback != null)
                    {
                        _navigateCallback(url);
                        this.Close();
                    }
                }
            };

            topPanel.Controls.Add(btnDelete);
            topPanel.Controls.Add(btnOpen);
            this.Controls.Add(topPanel);

            _lvBookmarks = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("Segoe UI", 9.5f)
            };
            _lvBookmarks.Columns.Add("Title", 280);
            _lvBookmarks.Columns.Add("URL", 200);
            _lvBookmarks.Columns.Add("Date Added", 110);

            _lvBookmarks.DoubleClick += (s, e) =>
            {
                if (_lvBookmarks.SelectedItems.Count > 0)
                {
                    string url = _lvBookmarks.SelectedItems[0].SubItems[1].Text;
                    if (_navigateCallback != null)
                    {
                        _navigateCallback(url);
                        this.Close();
                    }
                }
            };

            this.Controls.Add(_lvBookmarks);
        }

        private void LoadEntries()
        {
            _lvBookmarks.Items.Clear();
            var list = _bookmarkManager.GetAll();
            foreach (var b in list)
            {
                var lvi = new ListViewItem(b.Title);
                lvi.SubItems.Add(b.Url);
                lvi.SubItems.Add(b.DateAdded.ToString("MMM dd, yyyy"));
                _lvBookmarks.Items.Add(lvi);
            }
        }
    }
}

