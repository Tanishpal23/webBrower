using System;
using System.Drawing;
using System.Windows.Forms;
using AerisBrowser.Core;

namespace AerisBrowser.UI
{
    public class SettingsDialog : Form
    {
        private readonly ContentBlocker _contentBlocker;
        private readonly PrivacyManager _privacyManager;
        private readonly TabManager _tabManager;
        private readonly HistoryManager _historyManager;

        private ComboBox _cbBlockerMode;
        private ComboBox _cbSuspension;
        private CheckBox _chkHttpsFirst;
        private CheckBox _chkDoNotTrack;
        private ListBox _lbWhitelist;
        private TextBox _txtNewWhitelist;

        public SettingsDialog(ContentBlocker blocker, PrivacyManager privacy, TabManager tabManager, HistoryManager history)
        {
            _contentBlocker = blocker;
            _privacyManager = privacy;
            _tabManager = tabManager;
            _historyManager = history;

            InitializeComponent();
            LoadCurrentSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "Aeris Settings & Privacy Controls";
            this.Size = new Size(560, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = Color.FromArgb(248, 249, 250);

            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(14, 8)
            };

            // TAB 1: Privacy & Content Blocking
            var tabPrivacy = new TabPage("🛡️ Privacy & Blocking");
            tabPrivacy.BackColor = Color.White;
            tabPrivacy.Padding = new Padding(20);

            var lblBlocker = new Label { Text = "Content Blocker Mode:", Location = new Point(20, 20), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            _cbBlockerMode = new ComboBox { Location = new Point(20, 44), Size = new Size(260, 26), DropDownStyle = ComboBoxStyle.DropDownList };
            _cbBlockerMode.Items.AddRange(new object[] { "Standard (Blocks known ads & trackers)", "Aggressive (Blocks ad heuristics & scripts)", "Disabled" });

            _chkHttpsFirst = new CheckBox { Text = "HTTPS-First Navigation (Auto-upgrade insecure HTTP)", Location = new Point(20, 90), Size = new Size(450, 24) };
            _chkDoNotTrack = new CheckBox { Text = "Send 'Do Not Track' (DNT) header with all requests", Location = new Point(20, 120), Size = new Size(450, 24) };

            var btnClearData = new Button
            {
                Text = "🗑️ Clear Browsing Data (Cache & History)",
                Location = new Point(20, 160),
                Size = new Size(300, 34),
                BackColor = Color.FromArgb(254, 238, 238),
                ForeColor = Color.FromArgb(197, 34, 31),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClearData.FlatAppearance.BorderColor = Color.FromArgb(248, 180, 180);
            btnClearData.Click += (s, e) =>
            {
                _historyManager.Clear();
                MessageBox.Show("Local browsing history and in-memory caches have been cleared.", "Privacy Cleanse Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            var lblWhitelist = new Label { Text = "Whitelisted Websites (No ad-blocking):", Location = new Point(20, 215), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            _lbWhitelist = new ListBox { Location = new Point(20, 240), Size = new Size(340, 100) };

            _txtNewWhitelist = new TextBox { Location = new Point(20, 350), Size = new Size(240, 26) };
            var btnAddWhitelist = new Button { Text = "Add", Location = new Point(266, 349), Size = new Size(60, 28), Cursor = Cursors.Hand };
            btnAddWhitelist.Click += (s, e) =>
            {
                string host = _txtNewWhitelist.Text.Trim();
                if (!string.IsNullOrEmpty(host))
                {
                    _contentBlocker.AddToWhitelist(host);
                    _txtNewWhitelist.Text = "";
                    RefreshWhitelist();
                }
            };

            var btnRemoveWhitelist = new Button { Text = "Remove", Location = new Point(330, 349), Size = new Size(70, 28), Cursor = Cursors.Hand };
            btnRemoveWhitelist.Click += (s, e) =>
            {
                if (_lbWhitelist.SelectedItem != null)
                {
                    _contentBlocker.RemoveFromWhitelist(_lbWhitelist.SelectedItem.ToString());
                    RefreshWhitelist();
                }
            };

            tabPrivacy.Controls.Add(lblBlocker);
            tabPrivacy.Controls.Add(_cbBlockerMode);
            tabPrivacy.Controls.Add(_chkHttpsFirst);
            tabPrivacy.Controls.Add(_chkDoNotTrack);
            tabPrivacy.Controls.Add(btnClearData);
            tabPrivacy.Controls.Add(lblWhitelist);
            tabPrivacy.Controls.Add(_lbWhitelist);
            tabPrivacy.Controls.Add(_txtNewWhitelist);
            tabPrivacy.Controls.Add(btnAddWhitelist);
            tabPrivacy.Controls.Add(btnRemoveWhitelist);

            tabControl.TabPages.Add(tabPrivacy);

            // TAB 2: Performance & Tabs
            var tabPerf = new TabPage("⚡ Performance & Tabs");
            tabPerf.BackColor = Color.White;
            tabPerf.Padding = new Padding(20);

            var lblSuspension = new Label { Text = "Inactive Tab Memory Suspension:", Location = new Point(20, 20), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            _cbSuspension = new ComboBox { Location = new Point(20, 44), Size = new Size(260, 26), DropDownStyle = ComboBoxStyle.DropDownList };
            _cbSuspension.Items.AddRange(new object[] { "Hibernate after 1 minute idle", "Hibernate after 3 minutes idle (Recommended)", "Hibernate after 5 minutes idle", "Hibernate after 10 minutes idle", "Never suspend tabs" });

            var lblSuspensionDesc = new Label
            {
                Text = "When tabs are suspended, their heavy DOM, scripts, and GPU buffers are unloaded from RAM. Tab titles and scroll locations are preserved and restore instantly when clicked.",
                Location = new Point(20, 80),
                Size = new Size(480, 50),
                ForeColor = Color.FromArgb(95, 99, 104)
            };

            tabPerf.Controls.Add(lblSuspension);
            tabPerf.Controls.Add(_cbSuspension);
            tabPerf.Controls.Add(lblSuspensionDesc);

            tabControl.TabPages.Add(tabPerf);

            this.Controls.Add(tabControl);

            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = Color.FromArgb(241, 243, 244)
            };

            var btnSave = new Button
            {
                Text = "Save Changes",
                Location = new Point(410, 10),
                Size = new Size(120, 32),
                BackColor = Color.FromArgb(26, 115, 232),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSave.Click += (s, e) =>
            {
                SaveSettings();
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            bottomPanel.Controls.Add(btnSave);
            this.Controls.Add(bottomPanel);
        }

        private void LoadCurrentSettings()
        {
            if (_contentBlocker.Mode == ContentBlockerMode.Standard) _cbBlockerMode.SelectedIndex = 0;
            else if (_contentBlocker.Mode == ContentBlockerMode.Aggressive) _cbBlockerMode.SelectedIndex = 1;
            else _cbBlockerMode.SelectedIndex = 2;

            _chkHttpsFirst.Checked = _privacyManager.HttpsFirstEnabled;
            _chkDoNotTrack.Checked = _privacyManager.DoNotTrackHeaderEnabled;

            if (!_tabManager.AutoSuspendEnabled) _cbSuspension.SelectedIndex = 4;
            else if (_tabManager.SuspensionTimeoutMinutes == 1) _cbSuspension.SelectedIndex = 0;
            else if (_tabManager.SuspensionTimeoutMinutes == 5) _cbSuspension.SelectedIndex = 2;
            else if (_tabManager.SuspensionTimeoutMinutes == 10) _cbSuspension.SelectedIndex = 3;
            else _cbSuspension.SelectedIndex = 1;

            RefreshWhitelist();
        }

        private void RefreshWhitelist()
        {
            _lbWhitelist.Items.Clear();
            foreach (var item in _contentBlocker.GetWhitelist())
            {
                _lbWhitelist.Items.Add(item);
            }
        }

        private void SaveSettings()
        {
            if (_cbBlockerMode.SelectedIndex == 0) _contentBlocker.Mode = ContentBlockerMode.Standard;
            else if (_cbBlockerMode.SelectedIndex == 1) _contentBlocker.Mode = ContentBlockerMode.Aggressive;
            else _contentBlocker.Mode = ContentBlockerMode.Disabled;

            _privacyManager.HttpsFirstEnabled = _chkHttpsFirst.Checked;
            _privacyManager.DoNotTrackHeaderEnabled = _chkDoNotTrack.Checked;

            if (_cbSuspension.SelectedIndex == 4)
            {
                _tabManager.AutoSuspendEnabled = false;
            }
            else
            {
                _tabManager.AutoSuspendEnabled = true;
                if (_cbSuspension.SelectedIndex == 0) _tabManager.SuspensionTimeoutMinutes = 1;
                else if (_cbSuspension.SelectedIndex == 1) _tabManager.SuspensionTimeoutMinutes = 3;
                else if (_cbSuspension.SelectedIndex == 2) _tabManager.SuspensionTimeoutMinutes = 5;
                else if (_cbSuspension.SelectedIndex == 3) _tabManager.SuspensionTimeoutMinutes = 10;
            }
        }
    }
}

