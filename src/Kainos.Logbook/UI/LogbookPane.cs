/*  LogbookPane.cs

The logbook's LOG page: a toolbar (Import, Export, Settings, Pop out), the entry row (call, reports, name, their
park, a comment, LOG), what will be logged with it (time, VFO A's frequency, band and mode, power, grid) and the
alerts (new one, new band, new mode, worked today), then the log on the left and the lookup on the right.
Enter logs, Esc clears.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kainos.Logbook.Lookup;
using Kainos.Logbook.Contest;
using Kainos.Logbook.Services;
using Kainos.Modules;

namespace Kainos.Logbook.UI
{
    internal class LogbookPane : Panel, IKainosPaneHeight
    {
        public int PreferredHeight { get { return 360; } }
        public int MinimumHeight { get { return 220; } }

        private readonly Book _book;
        private readonly Feeds _feeds;
        private readonly Uploader _up;
        private readonly Pota _pota;
        private readonly PotaPage _potaPage;
        private readonly ContestPage _contestPage;
        private readonly AwardsPage _awardsPage;
        private string _page = "log";
        private int _parkSeq;
        private readonly IKainosTheme _t;
        private readonly KButton _tabLog, _tabPota, _tabContest, _tabAwards, _import, _export, _uploads, _settings, _pop, _logBtn;
        private readonly Label _status, _auto;
        private readonly Field _call, _sent, _rcvd, _name, _park, _comment;
        private readonly FlagStrip _flags;
        private readonly LogGrid _grid;
        private readonly LookupCard _card;
        private readonly Timer _tick, _lookupWait;
        private DateTime? _started;
        private string _lastBand = "", _lastMode = "";
        private ModeClass _class = ModeClass.Phone;
        private int _feedTicks;
        private CallInfo _info;
        private int _lookupSeq;
        private bool _running;

        public event Action PopToggled;

        public LogbookPane(Book book, Feeds feeds, Uploader up, Pota pota)
        {
            _book = book;
            _feeds = feeds;
            _up = up;
            _pota = pota;
            _t = book.Host.Theme;
            BackColor = _t.Bg;
            DoubleBuffered = true;
            SuspendLayout();
            ToolTip tip = new ToolTip();

            Func<string, string, KButton> btn = (text, tt) =>
            {
                KButton b = new KButton(_t, text);
                if (tt != null) tip.SetToolTip(b, tt);
                Controls.Add(b);
                return b;
            };
            _tabLog = btn("LOG", "The log and the lookup");
            _tabLog.On = true;
            _tabAwards = btn("AWARDS", "DXCC, WAS, WAZ and grids: worked and confirmed");
            _tabContest = btn("CONTEST", "Contests: the exchange, dupes, mults, the score, Cabrillo");
            _tabPota = btn("POTA", "Parks on the Air: activators on the air now (double-click to tune), and your own activations");
            _import = btn("Import…", "Bring QSOs in from an ADIF file (another logger, LoTW, QRZ). QSOs already in the log are skipped.");
            _export = btn("Export…", "Save the log as ADIF, ADX or CSV");
            _uploads = btn("Uploads…", "LoTW, eQSL, QRZ Logbook and Club Log: switch them on, see what's waiting, preview, send, get confirmations");
            _settings = btn("Settings…", "Station, QRZ / HamQTH lookups, what counts as confirmed, QSOs from the other modules");
            _pop = btn("Pop out", "Docked under the panadapter, or in its own window");
            _logBtn = btn("LOG  ⏎", "Log the QSO (Enter)");
            _logBtn.FontPx = 12;
            _status = new Label { ForeColor = _t.Faint, BackColor = _t.Bg, AutoSize = false, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Font = _t.Font(_t.S(11.5f), false) };
            Controls.Add(_status);

            _call = field("Call", 20, true, _t.Gold);
            _sent = field("Sent", 13, true, _t.Text);
            _rcvd = field("Rcvd", 13, true, _t.Text);
            _name = field("Name", 13, false, _t.Text);
            _park = field("Their park", 13, true, _t.Text);
            _comment = field("Comment", 13, false, _t.Text);
            tip.SetToolTip(_park.Box, "A POTA park (US-1234) when you work an activator; it's logged as SIG POTA");
            _auto = new Label { ForeColor = _t.Faint, BackColor = _t.Bg, AutoSize = false, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Consolas", _t.S(11.5f), FontStyle.Regular, GraphicsUnit.Pixel) };
            Controls.Add(_auto);
            _flags = new FlagStrip(_t);
            Controls.Add(_flags);

            _grid = new LogGrid(_t);
            Controls.Add(_grid);
            _card = new LookupCard(_t, book);
            Controls.Add(_card);
            _potaPage = new PotaPage(book, pota) { Visible = false };
            _contestPage = new ContestPage(new Contesting(book)) { Visible = false };
            Controls.Add(_contestPage);
            _contestPage.Status += m => status(m);
            _awardsPage = new AwardsPage(book) { Visible = false };
            Controls.Add(_awardsPage);
            _awardsPage.ConfirmChanged += () => analyse();
            _tabAwards.Click += (s, e) => page("awards");
            _tabContest.Click += (s, e) => page("contest");
            Controls.Add(_potaPage);
            _potaPage.SpotChosen += tuneTo;
            _tabLog.Click += (s, e) => page("log");
            _tabPota.Click += (s, e) => page("pota");
            _park.Box.TextChanged += (s, e) => parkChanged();

            _import.Click += (s, e) => import();
            _export.Click += (s, e) => { using (ExportForm f = new ExportForm(_book)) f.ShowDialog(FindForm()); };
            _settings.Click += (s, e) => { using (SettingsForm f = new SettingsForm(_book, _feeds)) if (f.ShowDialog(FindForm()) == DialogResult.OK) { analyse(); _card.Invalidate(); } };
            _uploads.Click += (s, e) => { using (ServicesForm f = new ServicesForm(_book, _up)) f.ShowDialog(FindForm()); uploadsText(); };
            _up.Status += m => _book.Host.BeginInvoke(() => status(m));
            _up.Changed += () => _book.Host.BeginInvoke(uploadsText);
            _pop.Click += (s, e) => PopToggled?.Invoke();
            _logBtn.Click += (s, e) => logIt();

            _call.Box.TextChanged += (s, e) => callChanged();
            foreach (Field f in new[] { _call, _sent, _rcvd, _name, _park, _comment })
                f.Box.KeyDown += entryKey;

            _grid.EditWanted += edit;
            _grid.DeleteWanted += delete;
            _grid.LookupWanted += q => { _call.Text = q.Call; _call.Box.Focus(); _call.Box.SelectAll(); };
            _grid.CardToggled += q => { Qso c = q.Clone(); c.CardReceived = !q.CardReceived; _book.Store.Update(c); status(c.CardReceived ? "Card from " + c.Call + " ticked" : "Card from " + c.Call + " unticked"); };

            _book.Store.Changed += () => _book.Host.BeginInvoke(refresh);
            _book.CtyChanged += () => _book.Host.BeginInvoke(() => { analyse(); _card.Invalidate(); });
            _feeds.Added += (n, from) => _book.Host.BeginInvoke(() => status(n + " QSO" + (n == 1 ? "" : "s") + " from " + from + " added to the log"));

            _tick = new Timer { Interval = 500 };
            _tick.Tick += (s, e) => tick();
            _lookupWait = new Timer { Interval = 650 };
            _lookupWait.Tick += (s, e) => { _lookupWait.Stop(); lookupNow(); };

            resetReports();
            refresh();
            ResumeLayout(true);
        }

        private Field field(string label, float px, bool upper, Color fore)
        {
            Field f = new Field(_t, label, px, upper, fore);
            Controls.Add(f);
            return f;
        }

        public bool Running
        {
            get { return _running; }
            set
            {
                _running = value;
                _tick.Enabled = value;
                _potaPage.Showing = value && _page == "pota";
                if (value) { tick(); Task.Run(() => _feeds.Poll()); }
            }
        }

        public void FocusCall() { _call.Box.Focus(); }

        // ---- layout ----
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (_card == null) return;      // still being built
            int pad = _t.S(6), y = pad, w = ClientSize.Width - 2 * pad, bh = _t.S(24);
            int x = pad;
            _tabLog.SetBounds(x, y, _t.S(54), bh); x += _t.S(60);
            _tabPota.SetBounds(x, y, _t.S(60), bh); x += _t.S(66);
            _tabContest.SetBounds(x, y, _t.S(84), bh); x += _t.S(90);
            _tabAwards.SetBounds(x, y, _t.S(80), bh); x += _t.S(86);
            int right = pad + w;
            _pop.SetBounds(right - _t.S(70), y, _t.S(70), bh); right -= _t.S(76);
            _settings.SetBounds(right - _t.S(78), y, _t.S(78), bh); right -= _t.S(84);
            _uploads.SetBounds(right - _t.S(96), y, _t.S(96), bh); right -= _t.S(102);
            _export.SetBounds(right - _t.S(70), y, _t.S(70), bh); right -= _t.S(76);
            _import.SetBounds(right - _t.S(70), y, _t.S(70), bh); right -= _t.S(76);
            _status.SetBounds(x + _t.S(4), y, Math.Max(10, right - x - _t.S(8)), bh);
            y += bh + _t.S(6);
            _contestPage.SetBounds(pad, y, w, Math.Max(_t.S(60), ClientSize.Height - y - pad));

            // the entry row: call wide, the reports narrow, the rest share what's left
            int fh = _t.S(44), gap = _t.S(6);
            int callW = _t.S(150), rptW = _t.S(56), btnW = _t.S(78), parkW = _t.S(96);
            int rest = Math.Max(_t.S(140), w - callW - 2 * rptW - parkW - btnW - 6 * gap);
            int nameW = rest * 2 / 5, cmtW = rest - nameW;
            x = pad;
            _call.SetBounds(x, y, callW, fh); x += callW + gap;
            _sent.SetBounds(x, y + _t.S(6), rptW, fh - _t.S(6)); x += rptW + gap;
            _rcvd.SetBounds(x, y + _t.S(6), rptW, fh - _t.S(6)); x += rptW + gap;
            _name.SetBounds(x, y + _t.S(6), nameW, fh - _t.S(6)); x += nameW + gap;
            _park.SetBounds(x, y + _t.S(6), parkW, fh - _t.S(6)); x += parkW + gap;
            _comment.SetBounds(x, y + _t.S(6), cmtW, fh - _t.S(6)); x += cmtW + gap;
            _logBtn.SetBounds(pad + w - btnW, y + _t.S(20), btnW, fh - _t.S(20));
            y += fh + _t.S(4);
            _auto.SetBounds(pad, y, w / 2, _t.S(20));
            _flags.SetBounds(pad + w / 2, y, w - w / 2, _t.S(22));
            y += _t.S(26);

            int cardW = Math.Max(_t.S(290), Math.Min(_t.S(420), w * 36 / 100));
            int h = Math.Max(_t.S(60), ClientSize.Height - y - pad);
            _grid.SetBounds(pad, y, w - cardW - gap, h);
            _card.SetBounds(pad + w - cardW, y, cardW, h);
            _potaPage.SetBounds(pad, y, w, h);
            _awardsPage.SetBounds(pad, y, w, h);
        }

        // ---- pages: LOG (the log and the lookup) or POTA (spots and activating), under the same entry row ----
        private void page(string p)
        {
            _page = p;
            _tabLog.On = p == "log";
            _tabPota.On = p == "pota";
            _tabContest.On = p == "contest";
            _tabAwards.On = p == "awards";
            bool entry = p != "contest";
            foreach (Control c in new Control[] { _call, _sent, _rcvd, _name, _park, _comment, _logBtn, _auto, _flags }) c.Visible = entry;
            _contestPage.Visible = !entry;
            _up.Paused = !entry;
            if (!entry) { _contestPage.RadioChanged(); _contestPage.FocusCall(); }
            else _up.Kick(false);
            _grid.Visible = _card.Visible = p == "log";
            _potaPage.Visible = p == "pota";
            _awardsPage.Visible = p == "awards";
            _potaPage.Showing = _running && p == "pota";
            _book.Host.SetSetting("page", p);
        }

        public void RestorePage() { page(_book.Host.GetSetting("page", "log")); }

        // a spot double-clicked: VFO A to it, in its mode, with the call and park filled in
        private void tuneTo(PotaSpot sp)
        {
            IKainosRadio r = _book.Host.Radio;
            int rx = r.TxReceiver;
            r.SetMode(rx, Pota.KainosMode(sp));
            r.TuneVfo(rx, sp.Khz * 1000.0);
            tick();                 // the new band and mode first, so the reports suit them
            resetReports();
            _call.Text = sp.Activator;
            _park.Text = sp.Reference;
            status("Tuned to " + sp.Activator + " at " + sp.Reference + " (" + sp.ParkName + "), " + sp.Khz.ToString("0.0", CultureInfo.InvariantCulture) + " kHz " + sp.Mode);
            _call.Box.Focus();
        }

        private async void parkChanged()
        {
            string p = _park.Text.Trim().ToUpperInvariant();
            int seq = ++_parkSeq;
            PotaPark known = _pota.Known(p);
            if (!Pota.RefRx.IsMatch(p)) { _card.Park = ""; _card.Invalidate(); return; }
            _card.Park = p + (known != null && known.Name.Length > 0 ? " · " + known.Name : "");
            _card.Invalidate();
            if (known != null && known.Name.Length > 0) return;
            PotaPark got = null;
            try { got = await _pota.ParkAsync(p); } catch { }
            if (seq != _parkSeq || IsDisposed) return;
            if (got != null) { _card.Park = p + " · " + got.Name + (got.Location.Length > 0 ? ", " + got.Location : ""); _card.Invalidate(); }
        }

        // ---- the radio, the lookup ----
        private void tick()
        {
            IKainosRadio r = _book.Host.Radio;
            int rx = r.TxReceiver;
            double mhz = r.VfoHz(rx) / 1e6;
            string band = Bands.Of(mhz), mode, sub;
            Bands.ToAdif(r.Mode(rx), out mode, out sub);
            string shown = sub ?? mode;
            DateTime now = DateTime.UtcNow;
            _auto.Text = (_started.HasValue ? "started " + _started.Value.ToString("HH:mm", CultureInfo.InvariantCulture) + "  " : "UTC " + now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  ")
                + mhz.ToString("0.000000", CultureInfo.InvariantCulture) + " MHz  " + (band.Length > 0 ? band : "out of band") + "  " + shown
                + "  " + _book.MyPower + " W  " + (_pota.Active && _pota.ActivationGrid.Length > 0 ? _pota.ActivationGrid : _book.MyGrid)
                + (_pota.Active ? "  POTA " + string.Join("+", _pota.Parks) : "");
            ModeClass cls = Bands.ClassOf(shown);
            if (band != _lastBand || shown != _lastMode)
            {
                bool classChanged = cls != _class;
                _lastBand = band; _lastMode = shown; _class = cls;
                if (classChanged && _call.Text.Trim().Length == 0) resetReports();
                analyse();
                if (_page == "contest") _contestPage.RadioChanged();
            }
            if (++_feedTicks >= 10) { _feedTicks = 0; Task.Run(() => _feeds.Poll()); }
        }

        private void resetReports()
        {
            string rst = Bands.DefaultRst(_class);
            _sent.Text = rst;
            _rcvd.Text = rst;
        }

        private void callChanged()
        {
            string c = _call.Text.Trim();
            if (c.Length > 0 && !_started.HasValue) _started = DateTime.UtcNow;
            if (c.Length == 0) _started = null;
            if (_info != null && !Cty.BaseCall(c, out string _).Equals(Cty.BaseCall(_info.Call ?? "", out string _), StringComparison.OrdinalIgnoreCase))
            {
                if (_name.Text == (_info.FirstName ?? "") || _name.Text == (_info.Name ?? "")) _name.Text = "";
                _info = null;
            }
            analyse();
            _lookupWait.Stop();
            if (c.Length >= 3 && _book.Lookup.Any) _lookupWait.Start();
        }

        private void analyse()
        {
            string c = _call.Text.Trim().ToUpperInvariant();
            _card.Call = c;
            _card.Band = _lastBand;
            _card.Class = _class;
            _card.Info = _info;
            _card.Worked = c.Length >= 2 ? _book.Analyse(c, _lastBand, _class) : null;
            _flags.Set(_card.Worked, _class, _lastBand, _book);
            _card.Invalidate();
        }

        private async void lookupNow()
        {
            string c = _call.Text.Trim().ToUpperInvariant();
            if (c.Length < 3) return;
            CtyInfo ci = _book.Cty.Resolve(c);
            int seq = ++_lookupSeq;
            _card.Looking = true;
            _card.Problem = "";
            _card.Invalidate();
            CallInfo r = await _book.Lookup.FindAsync(c, ci != null && ci.Name == "United States");
            if (seq != _lookupSeq || IsDisposed) return;
            _card.Looking = false;
            _card.Problem = _book.Lookup.LastProblem;
            if (r != null && _call.Text.Trim().ToUpperInvariant() == c)
            {
                _info = r;
                if (_name.Text.Trim().Length == 0) _name.Text = !string.IsNullOrEmpty(r.FirstName) ? r.FirstName : r.Name;
            }
            analyse();
        }

        // ---- logging ----
        private void entryKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; logIt(); }
            else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; clear(); }
        }

        private void logIt()
        {
            string call = _call.Text.Trim().ToUpperInvariant();
            if (call.Length < 3 || !call.Any(char.IsDigit) || _book.Cty.Resolve(call) == null)
            {
                status(call.Length == 0 ? "Type the call first" : call + " doesn't look like a call");
                _call.Box.Focus();
                return;
            }
            Qso q = _book.NewFromRadio();
            q.LoggedHere = true;
            _pota.Apply(q);
            if (_started.HasValue) { q.TimeOn = _started.Value; q.Set("TIME_OFF", DateTime.UtcNow.ToString("HHmmss", CultureInfo.InvariantCulture)); q.Set("QSO_DATE_OFF", DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); }
            q.Call = call;
            q.RstSent = _sent.Text.Trim();
            q.RstRcvd = _rcvd.Text.Trim();
            q.Name = _name.Text.Trim();
            q.Comment = _comment.Text.Trim();
            string park = _park.Text.Trim().ToUpperInvariant();
            if (park.Length > 0) { q.Set("SIG", "POTA"); q.Set("SIG_INFO", park); q.Set("POTA_REF", park); }
            _book.Fill(q, _info != null && Cty.BaseCall(call, out string _) == Cty.BaseCall(_info.Call ?? "", out string _) ? _info : null);
            try { _book.Store.Add(q); }
            catch (Exception ex) { status("Couldn't save the log: " + ex.Message); return; }
            _up.Kick(true);
            status("Logged " + q.Call + " · " + q.Band + " " + q.ModeShown + (q.Name.Length > 0 ? " · " + q.Name : ""));
            clear();
        }

        private void clear()
        {
            _call.Text = _name.Text = _park.Text = _comment.Text = "";
            _info = null;
            _started = null;
            resetReports();
            analyse();
            _call.Box.Focus();
        }

        private void refresh()
        {
            _grid.SetRows(_book.Store.All());
            uploadsText();
            analyse();
        }

        private void edit(Qso q)
        {
            using (EditForm f = new EditForm(_t, q))
                if (f.ShowDialog(FindForm()) == DialogResult.OK) { Uploader.Edited(q, f.Result); _book.Store.Update(f.Result); status("Saved " + f.Result.Call); _up.Kick(false); }
        }

        private void delete(Qso q)
        {
            if (MessageBox.Show(FindForm(), "Delete the QSO with " + q.Call + " on " + q.TimeOn.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC?\n\nA copy of the log before this change is kept as logbook.adi.bak.",
                    "Delete QSO", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            _book.Store.Delete(q.Id);
            status("Deleted " + q.Call);
        }

        private void import()
        {
            using (OpenFileDialog d = new OpenFileDialog { Filter = "ADIF log (*.adi;*.adif)|*.adi;*.adif|All files|*.*", Title = "Import an ADIF log" })
            {
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    List<Qso> qsos = Adif.Parse(File.ReadAllText(d.FileName, Encoding.UTF8));
                    foreach (Qso q in qsos) { q.Id = ""; _book.Fill(q, null); }
                    int n = _book.Store.AddMany(qsos);
                    status("Imported " + n + " of " + qsos.Count + " QSOs from " + Path.GetFileName(d.FileName) + (qsos.Count > n ? " (the rest were already in the log)" : ""));
                }
                catch (Exception ex) { status("Couldn't import: " + ex.Message); }
            }
        }

        // the button shows how many QSOs are waiting to go
        private void uploadsText()
        {
            int n = _up.WaitingTotal;
            _uploads.Text = n > 0 ? "Uploads (" + n + ")" : "Uploads…";
        }

        private void status(string s)
        {
            _status.ForeColor = _t.Dim;
            _status.Text = DateTime.UtcNow.ToString("HH:mm", CultureInfo.InvariantCulture) + "  " + s;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _tick.Dispose(); _lookupWait.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // the alerts under the entry row
    internal class FlagStrip : Control
    {
        private readonly IKainosTheme _t;
        private readonly List<Tuple<string, Color>> _items = new List<Tuple<string, Color>>();

        public FlagStrip(IKainosTheme t)
        {
            _t = t;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = t.Bg;
        }

        public void Set(Worked w, ModeClass cls, string band, Book book)
        {
            _items.Clear();
            if (w != null && w.Cty != null)
            {
                string ent = w.Cty.Name;
                if (w.NewOne) _items.Add(Tuple.Create("NEW ONE · " + ent, _t.Gold));
                else
                {
                    if (w.NewBand) _items.Add(Tuple.Create("NEW BAND · " + band, _t.Gold));
                    if (w.NewMode) _items.Add(Tuple.Create("NEW MODE · " + Bands.ClassName(cls), _t.Gold));
                    _items.Add(w.EntityConfirmed ? Tuple.Create(ent + " CONFIRMED", Draw.Ok) : Tuple.Create(ent + " WORKED, NOT CONFIRMED", _t.Ice));
                }
                if (w.DupeToday) _items.Add(Tuple.Create("WORKED TODAY ON " + band + " " + Bands.ClassName(cls), _t.Tx));
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            int x = 0;
            using (Font f = _t.Font(_t.S(10), true))
                foreach (Tuple<string, Color> it in _items)
                {
                    if (x > Width - _t.S(40)) break;
                    x += Draw.Chip(e.Graphics, _t, f, it.Item1.ToUpperInvariant(), it.Item2, x, 1) + _t.S(6);
                }
        }
    }
}
