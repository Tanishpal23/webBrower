using System;
using System.Drawing;
using System.Windows.Forms;
using AerisBrowser.Core;

namespace AerisBrowser.UI
{
    public class HistoryViewer : Form
    {
        private readonly HistoryManager _historyManager;
        private readonly Action<string> _navigateCallback;

        private ListView _lvHistory;
        private TextBox _txtSearch;

        public HistoryViewer(HistoryManager historyManager, Action<string> navigateCallback)
        {
            _historyManager = historyManager;
            _navigateCallback = navigateCallback;

            InitializeComponent();
            LoadEntries("");
        }

        private void InitializeComponent()
        {
            this.Text = "Browsing History";
            this.Size = new Size(680, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = Color.White;

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                Padding = new Padding(16, 10, 16, 10)
            };

            _txtSearch = new TextBox
            {
                Location = new Point(16, 12),
                Size = new Size(340, 26),
                Font = new Font("Segoe UI", 10.0f)
            };
            _txtSearch.TextChanged += (s, e) => LoadEntries(_txtSearch.Text);

            var lblSearch = new Label
            {
                Text = "🔍 Search History:",
                Location = new Point(16, 14),
                AutoSize = true,
                Visible = false
            };

            var btnClear = new Button
            {
                Text = "🗑️ Clear All History",
                Location = new Point(500, 10),
                Size = new Size(150, 30),
                BackColor = Color.FromArgb(254, 238, 238),
                ForeColor = Color.FromArgb(197, 34, 31),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClear.FlatAppearance.BorderColor = Color.FromArgb(248, 180, 180);
            btnClear.Click += (s, e) =>
            {
                if (MessageBox.Show("Are you sure you want to clear your local browsing history?", "Clear History", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _historyManager.Clear();
                    LoadEntries("");
                }
            };

            topPanel.Controls.Add(_txtSearch);
            topPanel.Controls.Add(btnClear);
            this.Controls.Add(topPanel);

            _lvHistory = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("Segoe UI", 9.5f)
            };
            _lvHistory.Columns.Add("Title", 320);
            _lvHistory.Columns.Add("URL", 220);
            _lvHistory.Columns.Add("Time", 120);

            _lvHistory.DoubleClick += (s, e) =>
            {
                if (_lvHistory.SelectedItems.Count > 0)
                {
                    string url = _lvHistory.SelectedItems[0].SubItems[1].Text;
                    if (_navigateCallback != null)
                    {
                        _navigateCallback(url);
                        this.Close();
                    }
                }
            };

            this.Controls.Add(_lvHistory);
        }

        private void LoadEntries(string query)
        {
            _lvHistory.Items.Clear();
            var list = string.IsNullOrEmpty(query) ? _historyManager.GetHistory(200) : _historyManager.Search(query);

            foreach (var item in list)
            {
                var lvi = new ListViewItem(item.Title ?? item.Url);
                lvi.SubItems.Add(item.Url ?? "");
                lvi.SubItems.Add(item.FormattedTime);
                _lvHistory.Items.Add(lvi);
            }
        }
    }
}

