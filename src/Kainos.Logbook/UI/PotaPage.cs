/*  PotaPage.cs

The POTA page, under the entry row: the activators on the air now (refreshed every minute while the page is
shown), with filters (mode, band, hide those already logged today); double-click one to tune VFO A to it and
fill in the call and park. On the right, activating: your park(s) and grid, Start / End, how many QSOs (10 make
it count), Spot me (a window: you check it and press Send), Export for pota.app, and how many parks you've hunted.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Kainos.Modules;

namespace Kainos.Logbook.UI
{
    internal class PotaPage : Panel
    {
        private readonly Book _book;
        private readonly Pota _pota;
        private readonly IKainosTheme _t;
        private readonly KButton[] _modeBtns;
        private readonly ComboBox _band;
        private readonly CheckBox _hide;
        private readonly Label _info, _act, _hunt;
        private readonly KButton _refresh, _start, _spotMe, _export;
        private readonly DataGridView _grid;
        private readonly TextBox _parks, _grid6;
        private readonly ActivationBar _bar;
        private readonly Timer _timer;
        private List<PotaSpot> _spots = new List<PotaSpot>();
        private List<PotaSpot> _shown = new List<PotaSpot>();
        private string _mode = "All";
        private DateTime _fetched;
        private bool _fetching;

        public event Action<PotaSpot> SpotChosen;

        public PotaPage(Book book, Pota pota)
        {
            _book = book;
            _pota = pota;
            _t = book.Host.Theme;
            BackColor = _t.Bg;
            SuspendLayout();
            ToolTip tip = new ToolTip();

            string[] modes = { "All", "CW", "SSB", "Digital" };
            _modeBtns = modes.Select(m =>
            {
                KButton b = new KButton(_t, m) { On = m == "All" };
                b.Click += (s, e) => { _mode = m; foreach (KButton x in _modeBtns) x.On = x.Text == m; fill(); };
                Controls.Add(b);
                return b;
            }).ToArray();
            _band = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = _t.Surface, ForeColor = _t.Text };
            _band.Items.Add("All bands");
            _band.Items.AddRange(Bands.Grid);
            _band.SelectedIndex = 0;
            _band.SelectedIndexChanged += (s, e) => fill();
            Controls.Add(_band);
            _hide = new CheckBox { Text = "Hide logged today", ForeColor = _t.Dim, BackColor = _t.Bg, AutoSize = true, Checked = book.Host.GetSetting("pota.hide", "0") == "1" };
            _hide.CheckedChanged += (s, e) => { book.Host.SetSetting("pota.hide", _hide.Checked ? "1" : "0"); fill(); };
            Controls.Add(_hide);
            _refresh = new KButton(_t, "Refresh");
            _refresh.Click += (s, e) => fetch();
            Controls.Add(_refresh);
            _info = new Label { ForeColor = _t.Faint, BackColor = _t.Bg, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            Controls.Add(_info);

            _grid = makeGrid();
            Controls.Add(_grid);

            // activating
            _parks = new TextBox { BorderStyle = BorderStyle.FixedSingle, BackColor = _t.Surface, ForeColor = _t.Gold, CharacterCasing = CharacterCasing.Upper, Font = _t.Font(_t.S(14), true), Text = string.Join(", ", pota.Parks) };
            _grid6 = new TextBox { BorderStyle = BorderStyle.FixedSingle, BackColor = _t.Surface, ForeColor = _t.Text, CharacterCasing = CharacterCasing.Upper, Font = _t.Font(_t.S(13), false), Text = pota.ActivationGrid.Length > 0 ? pota.ActivationGrid : book.MyGrid };
            tip.SetToolTip(_parks, "Your park, like US-1234; two or more for a two-fer (US-1234, US-5678)");
            tip.SetToolTip(_grid6, "Your grid at the park (it goes in each QSO as MY_GRIDSQUARE)");
            Controls.Add(_parks);
            Controls.Add(_grid6);
            _start = new KButton(_t, "Start activation");
            _start.Click += (s, e) => startEnd();
            _spotMe = new KButton(_t, "Spot me…");
            tip.SetToolTip(_spotMe, "Spot yourself on pota.app: a window shows the spot; nothing is sent until you press Send");
            _spotMe.Click += (s, e) => spotMe();
            _export = new KButton(_t, "Export for pota.app…");
            tip.SetToolTip(_export, "The activation's QSOs as pota.app wants them: a file per park and UTC day (CALL@PARK-YYYYMMDD.adi)");
            _export.Click += (s, e) => export();
            Controls.Add(_start); Controls.Add(_spotMe); Controls.Add(_export);
            _act = new Label { ForeColor = _t.Dim, BackColor = _t.Bg, AutoEllipsis = true };
            _bar = new ActivationBar(_t);
            _hunt = new Label { ForeColor = _t.Dim, BackColor = _t.Bg };
            Controls.Add(_act); Controls.Add(_bar); Controls.Add(_hunt);

            _timer = new Timer { Interval = 60000 };
            _timer.Tick += (s, e) => fetch();
            _book.Store.Changed += () => _book.Host.BeginInvoke(() => { if (!IsDisposed) { fill(); status(); } });
            ResumeLayout(false);
            status();
        }

        public bool Showing
        {
            set
            {
                _timer.Enabled = value;
                if (value && (DateTime.UtcNow - _fetched).TotalSeconds > 50) fetch();
                if (value) status();
            }
        }

        private DataGridView makeGrid()
        {
            DataGridView g = new DataGridView
            {
                ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                EnableHeadersVisualStyles = false, BackgroundColor = _t.Panel, GridColor = _t.Line, AutoGenerateColumns = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing, ColumnHeadersHeight = _t.S(24),
            };
            g.RowTemplate.Height = _t.S(22);
            g.DefaultCellStyle = new DataGridViewCellStyle { BackColor = _t.Panel, ForeColor = _t.Text, SelectionBackColor = Draw.Blend(_t.Panel, _t.Ice, 0.18f), SelectionForeColor = _t.Text, Font = new Font("Consolas", _t.S(12), FontStyle.Regular, GraphicsUnit.Pixel), Padding = new Padding(4, 0, 4, 0) };
            g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = _t.Surface, ForeColor = _t.Faint, Font = _t.Font(_t.S(10), true), SelectionBackColor = _t.Surface, SelectionForeColor = _t.Faint, Padding = new Padding(4, 0, 4, 0) };
            string[] heads = { "UTC", "ACTIVATOR", "KHZ", "MODE", "PARK", "NAME · WHERE", "" };
            int[] w = { 52, 96, 78, 52, 84, 200, 96 };
            for (int i = 0; i < heads.Length; i++)
            {
                DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn { HeaderText = heads[i], Width = _t.S(w[i]), SortMode = DataGridViewColumnSortMode.NotSortable };
                if (i == 5) { c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; c.DefaultCellStyle = new DataGridViewCellStyle { ForeColor = _t.Dim, Font = _t.Font(_t.S(12), false) }; }
                if (i == 1) c.DefaultCellStyle = new DataGridViewCellStyle { ForeColor = _t.Gold, Font = new Font("Consolas", _t.S(12), FontStyle.Bold, GraphicsUnit.Pixel) };
                if (i == 4) c.DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Draw.Ok };
                if (i == 6) c.DefaultCellStyle = new DataGridViewCellStyle { Font = _t.Font(_t.S(10), true) };
                g.Columns.Add(c);
            }
            g.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0 && e.RowIndex < _shown.Count) SpotChosen?.Invoke(_shown[e.RowIndex]); };
            g.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && g.CurrentRow != null && g.CurrentRow.Index < _shown.Count) { e.SuppressKeyPress = true; SpotChosen?.Invoke(_shown[g.CurrentRow.Index]); } };
            return g;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (_hunt == null) return;
            int pad = 0, gap = _t.S(6), bh = _t.S(24), w = ClientSize.Width;
            int right = Math.Max(_t.S(250), Math.Min(_t.S(340), w * 30 / 100));
            int lw = w - right - gap;
            int x = pad, y = 0;
            foreach (KButton b in _modeBtns) { b.SetBounds(x, y, _t.S(62), bh); x += _t.S(66); }
            _band.SetBounds(x, y + 1, _t.S(96), bh); x += _t.S(102);
            _hide.Location = new Point(x, y + _t.S(4)); x += _hide.Width + gap;
            _refresh.SetBounds(lw - _t.S(70), y, _t.S(70), bh);
            _info.SetBounds(x, y, Math.Max(10, lw - _t.S(76) - x), bh);
            y += bh + gap;
            _grid.SetBounds(0, y, lw, Math.Max(_t.S(40), ClientSize.Height - y));

            int rx = lw + gap, ry = 0, rw = right;
            Label h = heading("ACTIVATING", rx, ry, rw); ry += _t.S(18);
            _parks.SetBounds(rx, ry, rw * 62 / 100, _t.S(26));
            _grid6.SetBounds(rx + rw * 62 / 100 + gap, ry, rw - rw * 62 / 100 - gap, _t.S(26));
            ry += _t.S(32);
            _start.SetBounds(rx, ry, rw, bh); ry += bh + gap;
            _act.SetBounds(rx, ry, rw, _t.S(34)); ry += _t.S(34);
            _bar.SetBounds(rx, ry, rw, _t.S(10)); ry += _t.S(16);
            _spotMe.SetBounds(rx, ry, rw / 2 - gap / 2, bh);
            _export.SetBounds(rx + rw / 2 + gap / 2, ry, rw - rw / 2 - gap / 2, bh); ry += bh + _t.S(10);
            _hunt.SetBounds(rx, ry, rw, Math.Max(10, ClientSize.Height - ry));
        }

        private Label _head;
        private Label heading(string text, int x, int y, int w)
        {
            if (_head == null) { _head = new Label { Text = text, ForeColor = _t.Dim, BackColor = _t.Bg, Font = _t.Font(_t.S(10), true) }; Controls.Add(_head); }
            _head.SetBounds(x, y, w, _t.S(16));
            return _head;
        }

        // ---- spots ----
        private async void fetch()
        {
            if (_fetching) return;
            _fetching = true;
            _info.Text = "Getting spots…";
            try
            {
                _spots = await _pota.SpotsAsync();
                _fetched = DateTime.UtcNow;
                fill();
            }
            catch (Exception ex) { if (!IsDisposed) _info.Text = "Couldn't get spots from pota.app: " + (ex.InnerException ?? ex).Message; }
            finally { _fetching = false; }
        }

        // logged today: this activator at this park (UTC day, as POTA counts)
        private HashSet<string> loggedToday()
        {
            DateTime today = DateTime.UtcNow.Date;
            return new HashSet<string>(_book.Store.All().Where(q => q.TimeOn.Date == today && q.PotaRef.Length > 0).Select(q => Cty.BaseCall(q.Call, out string _) + "|" + q.PotaRef + "|" + q.Band));
        }

        private void fill()
        {
            if (_spots == null) return;
            HashSet<string> today = loggedToday();
            HashSet<string> hunted = new HashSet<string>(_book.Store.All().Select(q => q.PotaRef).Where(p => p.Length > 0));
            string band = _band.SelectedIndex > 0 ? (string)_band.SelectedItem : "";
            _shown = _spots.Where(sp =>
                (_mode == "All" || (_mode == "CW" && sp.Mode == "CW") || (_mode == "SSB" && (sp.Mode == "SSB" || sp.Mode == "USB" || sp.Mode == "LSB" || sp.Mode == "AM" || sp.Mode == "FM")) || (_mode == "Digital" && sp.Class == ModeClass.Digital && sp.Mode != "CW")) &&
                (band.Length == 0 || sp.Band == band) &&
                !(_hide.Checked && today.Contains(Cty.BaseCall(sp.Activator, out string _) + "|" + sp.Reference + "|" + sp.Band))).ToList();
            _grid.Rows.Clear();
            foreach (PotaSpot sp in _shown)
            {
                bool done = today.Contains(Cty.BaseCall(sp.Activator, out string _) + "|" + sp.Reference + "|" + sp.Band);
                string tag = done ? "LOGGED" : !hunted.Contains(sp.Reference) ? "NEW PARK" : "";
                int i = _grid.Rows.Add(sp.Time == DateTime.MinValue ? "" : sp.Time.ToString("HHmm", CultureInfo.InvariantCulture), sp.Activator, sp.Khz.ToString("0.0", CultureInfo.InvariantCulture), sp.Mode, sp.Reference,
                    sp.ParkName + (sp.Location.Length > 0 ? " · " + sp.Location : ""), tag);
                _grid.Rows[i].Cells[6].Style.ForeColor = done ? Draw.Ok : _t.Gold;
                if (done) _grid.Rows[i].DefaultCellStyle.ForeColor = _t.Faint;
            }
            _info.Text = _fetched == DateTime.MinValue ? "" : _shown.Count + " of " + _spots.Count + " spots · pota.app, " + _fetched.ToLocalTime().ToString("HH:mm") + " · double-click to tune";
        }

        // ---- activating ----
        private void startEnd()
        {
            if (_pota.Active) { _pota.End(); status(); return; }
            string problem = _pota.Start(_parks.Text, _grid6.Text.Trim());
            if (problem != null) { _act.ForeColor = _t.Tx; _act.Text = problem; return; }
            status();
        }

        private void status()
        {
            List<Qso> act = _pota.ActivationQsos(_book.Store.All());
            int n = act.Count;
            _start.Text = _pota.Active ? "End activation" : "Start activation";
            _start.On = _pota.Active;
            _parks.ReadOnly = _grid6.ReadOnly = _pota.Active;
            _act.ForeColor = _t.Dim;
            string parks = string.Join(" + ", _pota.Parks);
            _act.Text = _pota.Active ? "Activating " + parks + " · " + n + " QSO" + (n == 1 ? "" : "s") + (n >= 10 ? " · counts" : " of 10") + (act.Count(q => q.PotaRef.Length > 0) > 0 ? " · " + act.Count(q => q.PotaRef.Length > 0) + " park-to-park" : "")
                      : n > 0 ? "Last activation: " + parks + ", " + n + " QSOs" : "Not activating. Enter your park and press Start.";
            _bar.Value = Math.Min(10, n);
            _spotMe.Enabled = _pota.Active;
            _export.Enabled = n > 0;
            List<Qso> all = _book.Store.All();
            int parksHunted = all.Select(q => q.PotaRef).Where(p => p.Length > 0).Distinct().Count();
            int huntQsos = all.Count(q => q.PotaRef.Length > 0);
            _hunt.Text = "HUNTER\r\n" + parksHunted + " park" + (parksHunted == 1 ? "" : "s") + " hunted, " + huntQsos + " QSO" + (huntQsos == 1 ? "" : "s") + ".\r\n\r\nHunters needn't upload: pota.app credits you from the activator's log.";
        }

        private void spotMe()
        {
            using (SpotForm f = new SpotForm(_book, _pota)) f.ShowDialog(FindForm());
        }

        private void export()
        {
            List<Qso> act = _pota.ActivationQsos(_book.Store.All());
            Dictionary<string, string> files = Pota.ExportFiles(act, _book.MyCall, _book.Version);
            using (FolderBrowserDialog d = new FolderBrowserDialog { Description = "Where to save the activation's files for pota.app (" + files.Count + " file" + (files.Count == 1 ? "" : "s") + ")" })
            {
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                foreach (KeyValuePair<string, string> kv in files) File.WriteAllText(Path.Combine(d.SelectedPath, kv.Key), kv.Value, new UTF8Encoding(false));
                _act.ForeColor = Draw.Ok;
                _act.Text = "Saved " + string.Join(", ", files.Keys) + ". Upload at pota.app > My Logs.";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }

    // the activation's progress to 10 QSOs
    internal class ActivationBar : Control
    {
        private readonly IKainosTheme _t;
        private int _v;
        public int Value { get { return _v; } set { _v = value; Invalidate(); } }

        public ActivationBar(IKainosTheme t)
        {
            _t = t;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = t.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (Brush b = new SolidBrush(_t.Surface)) e.Graphics.FillRectangle(b, r);
            using (Brush b = new SolidBrush(_v >= 10 ? Draw.Ok : _t.Gold)) e.Graphics.FillRectangle(b, 0, 0, (Width - 1) * _v / 10, Height - 1);
            using (Pen p = new Pen(_t.Line)) e.Graphics.DrawRectangle(p, r);
        }
    }

    // spotting yourself: what goes to pota.app, sent only on Send
    internal class SpotForm : KDialog
    {
        public SpotForm(Book book, Pota pota) : base(book.Host.Theme, "Spot me on pota.app")
        {
            int x = T.S(16), y = T.S(14), lw = T.S(110), bw = T.S(220);
            IKainosRadio r = book.Host.Radio;
            double khz = Math.Round(r.VfoHz(r.TxReceiver) / 100.0) / 10.0;
            Func<string, string, TextBox> row = (label, value) =>
            {
                Controls.Add(new Label { Text = label, Location = new Point(x, y + 3), Size = new Size(lw, T.S(20)), ForeColor = T.Dim });
                TextBox b = Box(x + lw, y, bw);
                b.Text = value;
                y += T.S(28);
                return b;
            };
            TextBox act = row("Activator", book.MyCall);
            TextBox freq = row("Frequency (kHz)", khz.ToString("0.0", CultureInfo.InvariantCulture));
            TextBox mode = row("Mode", Pota.ModeFor(r.Mode(r.TxReceiver)));
            Controls.Add(new Label { Text = "Park", Location = new Point(x, y + 3), Size = new Size(lw, T.S(20)), ForeColor = T.Dim });
            ComboBox park = Combo(x + lw, y, bw, pota.Parks.ToArray());
            if (park.Items.Count > 0) park.SelectedIndex = 0;
            y += T.S(28);
            TextBox cmt = row("Comment", "");
            Label result = new Label { Location = new Point(x, y + 4), Size = new Size(lw + bw, T.S(36)), ForeColor = T.Faint, Text = "Nothing is sent until you press Send." };
            Controls.Add(result);
            y += T.S(42);
            KButton send = null;
            send = Button("Send", x + lw + bw - T.S(200), y, T.S(96), async () =>
            {
                double k;
                if (!double.TryParse(freq.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out k) || k < 1000) { result.ForeColor = T.Tx; result.Text = "The frequency should be in kHz, like 14286.0"; return; }
                if (park.SelectedItem == null) { result.ForeColor = T.Tx; result.Text = "Start an activation first"; return; }
                send.Enabled = false;
                result.ForeColor = T.Faint;
                result.Text = "Sending…";
                string json = Pota.SpotJson(act.Text.Trim().ToUpperInvariant(), book.MyCall, k, mode.Text.Trim().ToUpperInvariant(), (string)park.SelectedItem, cmt.Text.Trim());
                string problem;
                try { problem = await pota.PostSpotAsync(json); }
                catch (Exception ex) { problem = "couldn't reach pota.app: " + (ex.InnerException ?? ex).Message; }
                if (IsDisposed) return;
                result.ForeColor = problem == null ? Draw.Ok : T.Tx;
                result.Text = problem == null ? "Spotted. It shows on pota.app within a minute." : problem;
                send.Enabled = problem != null;
            });
            Button("Close", x + lw + bw - T.S(96), y, T.S(96), Close);
            ClientSize = new Size(x * 2 + lw + bw, y + T.S(44));
        }
    }
}
