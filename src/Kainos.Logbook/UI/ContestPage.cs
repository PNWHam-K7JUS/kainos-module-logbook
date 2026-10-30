/*  ContestPage.cs

The CONTEST page, in place of the LOG page's entry row and log: the contest (and when it runs, its modes, what you
send), an entry row with that contest's exchange, the alerts (DUPE, NEW MULT, the points), the session's log, and
the score (QSOs, points, mults, claimed score, rate, by band, the mults still to work), with Cabrillo (and EDI for
the VHF contests) to send in.

Enter works the way contest loggers do: with no call it's time to call CQ (F1); with a call but the exchange not
complete it moves to what's missing (send their call and your exchange, F2); with everything in it logs (F3, TU).
Space in the call jumps to the exchange. The RST and their zones are filled in from the call. Esc clears.

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
using Kainos.Logbook.Contest;
using Kainos.Modules;

namespace Kainos.Logbook.UI
{
    internal class ContestPage : Panel
    {
        private readonly Contesting _c;
        private readonly Book _book;
        private readonly IKainosTheme _t;
        private readonly ComboBox _pick;
        private readonly KButton _mine, _messages, _newSession, _cab, _edi, _logBtn;
        private readonly Messages _msg;
        private string _f2For;          // the call F2 was sent to (Enter sends it once per call)
        private readonly Label _info, _esm;
        private readonly Panel _entry;
        private readonly ChipStrip _flags;
        private readonly DataGridView _grid;
        private readonly ScorePanel _score;
        private Field _call;
        private readonly List<Tuple<string, Field>> _fields = new List<Tuple<string, Field>>();
        private readonly HashSet<string> _typed = new HashSet<string>();
        private DateTime? _started;
        private List<Qso> _session = new List<Qso>();

        public event Action<string> Status;

        public ContestPage(Contesting c)
        {
            _c = c;
            _book = c.Book;
            _t = _book.Host.Theme;
            BackColor = _t.Bg;
            SuspendLayout();
            ToolTip tip = new ToolTip();

            _pick = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = _t.Surface, ForeColor = _t.Text, Font = _t.Font(_t.S(12.5f), true), MaxDropDownItems = 24 };
            foreach (ContestDef d in c.Defs) _pick.Items.Add(d.Name);
            _pick.SelectedIndex = Math.Max(0, c.Defs.IndexOf(c.Current));
            _pick.SelectedIndexChanged += (s, e) => { _c.Current = _c.Defs[_pick.SelectedIndex]; build(); refresh(); };
            Controls.Add(_pick);
            Func<string, string, KButton> btn = (text, tt) => { KButton b = new KButton(_t, text); tip.SetToolTip(b, tt); Controls.Add(b); return b; };
            _msg = new Messages(c);
            _messages = btn("Messages…", "Send CQ / the exchange / TU with Enter and F1 - F3: CW through Kainos's keyer, phone through the Voice Keyer module");
            _messages.Click += (s, e) => { using (MessagesForm f = new MessagesForm(_msg)) f.ShowDialog(FindForm()); check(); };
            IKainosKeyer keyer = _book.Host as IKainosKeyer;
            if (keyer != null) keyer.Subscribe("dvk.status", m => Status?.Invoke("Voice keyer: " + m));
            _mine = btn("My exchange…", "What you send: section, state, name, class, county ... (zones and grid come from your call and grid)");
            _newSession = btn("New session", "Start this contest again from zero (the QSOs so far stay in the log)");
            _cab = btn("Cabrillo…", "The log to send in: Cabrillo 3.0, with your categories");
            _edi = btn("EDI…", "European VHF contests' format (REG1TEST)");
            _mine.Click += (s, e) => { using (MyExchangeForm f = new MyExchangeForm(_c)) if (f.ShowDialog(FindForm()) == DialogResult.OK) refresh(); };
            _newSession.Click += (s, e) =>
            {
                if (_session.Count > 0 && MessageBox.Show(FindForm(), "Start " + _c.Current.Name + " again from zero? The " + _session.Count + " QSOs so far stay in the log.", "New session", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
                _c.NewSession(); refresh(); clear();
            };
            _cab.Click += (s, e) => { using (CabrilloForm f = new CabrilloForm(_c, _session)) f.ShowDialog(FindForm()); };
            _edi.Click += (s, e) => saveEdi();

            _info = new Label { ForeColor = _t.Dim, BackColor = _t.Bg, AutoEllipsis = true, Font = _t.Font(_t.S(11.5f), false) };
            Controls.Add(_info);
            _entry = new Panel { BackColor = _t.Bg };
            Controls.Add(_entry);
            _logBtn = btn("LOG  ⏎", "Log the QSO (Enter when the exchange is complete)");
            _logBtn.Click += (s, e) => enter(true);
            _esm = new Label { ForeColor = _t.Dim, BackColor = _t.Bg, AutoEllipsis = true, Font = _t.Font(_t.S(11.5f), false), TextAlign = ContentAlignment.MiddleLeft };
            Controls.Add(_esm);
            _flags = new ChipStrip(_t);
            Controls.Add(_flags);
            _grid = makeGrid();
            Controls.Add(_grid);
            _score = new ScorePanel(_t);
            Controls.Add(_score);

            _book.Store.Changed += () => _book.Host.BeginInvoke(() => { if (!IsDisposed) refresh(); });
            build();
            ResumeLayout(false);
            refresh();
        }

        // ---- the entry row, for the contest's exchange ----
        private void build()
        {
            _entry.SuspendLayout();
            foreach (Control x in _entry.Controls.Cast<Control>().ToList()) { _entry.Controls.Remove(x); x.Dispose(); }
            _fields.Clear();
            _typed.Clear();
            _call = new Field(_t, "Call", 20, true, _t.Gold);
            _call.Box.TextChanged += (s, e) => callChanged();
            _call.Box.KeyDown += key;
            _entry.Controls.Add(_call);
            foreach (string k in _c.Current.Exch)
            {
                Field f = new Field(_t, Contest.Field.Label(k), 14, true, _t.Text);
                string kind = k;
                f.Box.KeyDown += key;
                f.Box.KeyPress += (s, e) => _typed.Add(kind);
                f.Box.TextChanged += (s, e) => check();
                _entry.Controls.Add(f);
                _fields.Add(Tuple.Create(k, f));
            }
            _entry.ResumeLayout();
            _edi.Visible = _c.Current.Scoring == "vhf";
            layoutEntry();
        }

        private void layoutEntry()
        {
            if (_call == null) return;
            int h = _entry.Height, gap = _t.S(6), x = 0;
            float units = _fields.Sum(f => Contest.Field.Width(f.Item1));
            int callW = _t.S(150);
            int unit = (int)Math.Min(_t.S(110), Math.Max(_t.S(50), (_entry.Width - callW - gap * (_fields.Count + 1)) / Math.Max(1f, units)));
            _call.SetBounds(x, 0, callW, h); x += callW + gap;
            foreach (Tuple<string, Field> f in _fields) { int w = (int)(unit * Contest.Field.Width(f.Item1)); f.Item2.SetBounds(x, _t.S(6), w, h - _t.S(6)); x += w + gap; }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (_score == null) return;
            int w = ClientSize.Width, gap = _t.S(6), bh = _t.S(24), y = 0;
            int bx = w;
            _edi.SetBounds(bx - _t.S(60), y, _t.S(60), bh); if (_edi.Visible) bx -= _t.S(66);
            _cab.SetBounds(bx - _t.S(84), y, _t.S(84), bh); bx -= _t.S(90);
            _newSession.SetBounds(bx - _t.S(96), y, _t.S(96), bh); bx -= _t.S(102);
            _mine.SetBounds(bx - _t.S(106), y, _t.S(106), bh); bx -= _t.S(112);
            _messages.SetBounds(bx - _t.S(90), y, _t.S(90), bh); bx -= _t.S(96);
            _pick.SetBounds(0, y + 1, Math.Min(_t.S(340), bx - gap), bh);
            y += bh + _t.S(4);
            _info.SetBounds(0, y, w, _t.S(34)); y += _t.S(36);
            int btnW = _t.S(78);
            _entry.SetBounds(0, y, w - btnW - gap, _t.S(44));
            _logBtn.SetBounds(w - btnW, y + _t.S(20), btnW, _t.S(24));
            layoutEntry();
            y += _t.S(48);
            _esm.SetBounds(0, y, w / 2, _t.S(22));
            _flags.SetBounds(w / 2, y, w - w / 2, _t.S(22));
            y += _t.S(26);
            int sw = Math.Max(_t.S(280), Math.Min(_t.S(380), w * 32 / 100));
            int h = Math.Max(_t.S(40), ClientSize.Height - y);
            _grid.SetBounds(0, y, w - sw - gap, h);
            _score.SetBounds(w - sw, y, sw, h);
        }

        private DataGridView makeGrid()
        {
            DataGridView g = new DataGridView
            {
                ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                EnableHeadersVisualStyles = false, BackgroundColor = _t.Panel, GridColor = _t.Line,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing, ColumnHeadersHeight = _t.S(24),
            };
            g.RowTemplate.Height = _t.S(22);
            g.DefaultCellStyle = new DataGridViewCellStyle { BackColor = _t.Panel, ForeColor = _t.Text, SelectionBackColor = Draw.Blend(_t.Panel, _t.Ice, 0.18f), SelectionForeColor = _t.Text, Font = new Font("Consolas", _t.S(12), FontStyle.Regular, GraphicsUnit.Pixel), Padding = new Padding(4, 0, 4, 0) };
            g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = _t.Surface, ForeColor = _t.Faint, Font = _t.Font(_t.S(10), true), SelectionBackColor = _t.Surface, SelectionForeColor = _t.Faint, Padding = new Padding(4, 0, 4, 0) };
            string[] heads = { "UTC", "CALL", "BAND", "MODE", "SENT", "RCVD", "COUNTRY" };
            int[] w = { 52, 100, 50, 46, 130, 150, 140 };
            for (int i = 0; i < heads.Length; i++)
            {
                DataGridViewTextBoxColumn col = new DataGridViewTextBoxColumn { HeaderText = heads[i], Width = _t.S(w[i]), SortMode = DataGridViewColumnSortMode.NotSortable };
                if (i == 1) col.DefaultCellStyle = new DataGridViewCellStyle { ForeColor = _t.Gold, Font = new Font("Consolas", _t.S(12), FontStyle.Bold, GraphicsUnit.Pixel) };
                if (i == 6) { col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; col.DefaultCellStyle = new DataGridViewCellStyle { ForeColor = _t.Dim, Font = _t.Font(_t.S(12), false) }; }
                g.Columns.Add(col);
            }
            return g;
        }

        // ---- the radio's band and mode ----
        private string band { get { IKainosRadio r = _book.Host.Radio; return Bands.Of(r.VfoHz(r.TxReceiver) / 1e6); } }
        private string mode { get { IKainosRadio r = _book.Host.Radio; return Contesting.ModeOf(r.Mode(r.TxReceiver)); } }

        public void RadioChanged() { info(); check(); _score.Band = band; _score.Invalidate(); }

        private void info()
        {
            ContestDef d = _c.Current;
            string m = mode;
            _info.ForeColor = d.Modes.Contains(m) ? _t.Dim : Color.FromArgb(0xe0, 0xb2, 0x5c);
            _info.Text = d.When + " · " + d.ModesText + " · you send: " + _c.SentText(_session.Count + 1) + (d.Note.Length > 0 ? "\r\n" + d.Note : "")
                + (d.Modes.Contains(m) ? "" : "\r\nThis contest isn't on " + (m == "PH" ? "SSB" : m == "RY" ? "RTTY" : m == "DG" ? "digital" : "CW") + ": change the mode.");
        }

        // ---- entering ----
        private void callChanged()
        {
            string call = _call.Text.Trim().ToUpperInvariant();
            if (call.Length > 0 && !_started.HasValue) _started = DateTime.UtcNow;
            if (call.Length == 0) _started = null;
            Dictionary<string, string> g = _c.Guess(call, mode);
            foreach (Tuple<string, Field> f in _fields)
            {
                if (_typed.Contains(f.Item1)) continue;
                string v;
                f.Item2.Text = call.Length >= 2 && g.TryGetValue(f.Item1, out v) ? v : "";
            }
            check();
        }

        private bool complete { get { return _call.Text.Trim().Length >= 3 && _fields.All(f => Contest.Field.Valid(f.Item1, f.Item2.Text)); } }

        // the alerts and what Enter will do
        private void check()
        {
            if (_call == null) return;
            string call = _call.Text.Trim().ToUpperInvariant();
            List<Tuple<string, Color>> chips = new List<Tuple<string, Color>>();
            foreach (Tuple<string, Field> f in _fields)
                f.Item2.Box.BackColor = f.Item2.Text.Length > 0 && !Contest.Field.Valid(f.Item1, f.Item2.Text) ? Draw.Blend(_t.Surface, _t.Tx, 0.35f) : _t.Surface;
            CtyInfo ci = call.Length >= 3 ? _book.Cty.Resolve(call) : null;
            if (ci != null)
            {
                Qso d = _c.Dupe(_session, call, band, mode);
                if (d != null) chips.Add(Tuple.Create("DUPE · worked " + d.TimeOn.ToString("HHmm", CultureInfo.InvariantCulture) + " on " + d.Band, _t.Tx));
                else if (complete || _c.Current.Exch.All(k => k == "rst" || k == "zone" || k == "itu"))
                {
                    Qso q = _c.Make(call, values(), _session.Count + 1);
                    Score before = _c.Compute(_session), after = _c.Compute(_session.Concat(new[] { q }).ToList());
                    if (after.Mults > before.Mults) chips.Add(Tuple.Create("NEW MULT · " + string.Join(", ", after.NewMults), _t.Gold));
                    int pts = after.Points - before.Points;
                    chips.Add(Tuple.Create(pts + " PT" + (pts == 1 ? "" : "S"), _t.Ice));
                }
                chips.Add(Tuple.Create(ci.Name + " · " + ci.Cont + " · CQ " + ci.Cq, _t.Faint));
            }
            _flags.Set(chips);
            string verb = _msg.On ? "sends" : "is time for";
            _esm.Text = (call.Length == 0 ? "Enter " + verb + " CQ (F1)" : !complete ? "Enter " + verb + " " + call + " and your exchange (F2), then fill in theirs · Space: to the exchange" : "Enter " + (_msg.On ? "sends TU (F3) and logs" : "logs (TU, F3)"))
                + (_msg.On ? " · Esc stops" : " · messages off (Messages…)");
        }

        private Dictionary<string, string> values() { return _fields.ToDictionary(f => f.Item1, f => f.Item2.Text.Trim().ToUpperInvariant()); }

        private void key(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; enter(false); }
            else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; if (_msg.Stop()) Status?.Invoke("Stopped sending"); else clear(); }
            else if (e.KeyCode >= Keys.F1 && e.KeyCode <= Keys.F3 && e.Modifiers == Keys.None)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                int f = e.KeyCode - Keys.F1 + 1;
                Status?.Invoke(_msg.Send(f, _call.Text.Trim(), _session.Count + 1));
                if (f == 2) _f2For = _call.Text.Trim().ToUpperInvariant();
            }
            else if (e.KeyCode == Keys.Space && sender == _call.Box) { e.SuppressKeyPress = true; firstMissing().Focus(); }
        }

        private TextBox firstMissing()
        {
            Tuple<string, Field> f = _fields.FirstOrDefault(x => !Contest.Field.Valid(x.Item1, x.Item2.Text)) ?? _fields.FirstOrDefault();
            return f != null ? f.Item2.Box : _call.Box;
        }

        private void enter(bool button)
        {
            string call = _call.Text.Trim().ToUpperInvariant();
            if (call.Length == 0) { Status?.Invoke(_msg.On ? _msg.Send(1, "", _session.Count + 1) : "CQ (F1)"); _call.Box.Focus(); return; }
            // with messages on, the first Enter for a call sends their call and your exchange and waits for theirs (even
            // if it's filled in already: the guesses may be wrong); the next sends TU and logs, as contest loggers do
            if (_msg.On && _f2For != call && call.Length >= 3)
            {
                _f2For = call;
                Status?.Invoke(_msg.Send(2, call, _session.Count + 1));
                TextBox nb = firstMissing();
                nb.Focus(); nb.SelectAll();
                return;
            }
            if (!complete)
            {
                // their call and your exchange, once per call (a second Enter just moves to what's missing)
                if (_f2For != call)
                {
                    _f2For = call;
                    if (_msg.On) Status?.Invoke(_msg.Send(2, call, _session.Count + 1));
                    else if (!button) Status?.Invoke(call + " " + _c.SentText(_session.Count + 1) + " (F2)");
                }
                TextBox b = call.Length < 3 ? _call.Box : firstMissing();
                b.Focus(); b.SelectAll();
                return;
            }
            Qso q = _c.Make(call, values(), _session.Count + 1);
            string tu = _msg.On ? _msg.Send(3, call, _session.Count + 1) : null;
            if (_started.HasValue) { q.TimeOn = _started.Value; q.Set("TIME_OFF", DateTime.UtcNow.ToString("HHmmss", CultureInfo.InvariantCulture)); }
            bool dupe = _c.Dupe(_session, call, band, mode) != null;
            try { _book.Store.Add(q); }
            catch (Exception ex) { Status?.Invoke("Couldn't save the log: " + ex.Message); return; }
            Status?.Invoke((tu ?? "TU (F3)") + " · logged " + call + " " + q.Get("SRX_STRING") + (dupe ? " (a dupe: 0 points)" : ""));
            clear();
        }

        private void clear()
        {
            _f2For = null;
            _typed.Clear();
            _call.Text = "";
            foreach (Tuple<string, Field> f in _fields) f.Item2.Text = "";
            _started = null;
            check();
            _call.Box.Focus();
        }

        public void FocusCall() { if (_call != null) _call.Box.Focus(); }

        // ---- the log and the score ----
        private void refresh()
        {
            _session = _c.SessionQsos();
            _grid.Rows.Clear();
            foreach (Qso q in Enumerable.Reverse(_session))
                _grid.Rows.Add(q.TimeOn.ToString("HHmm", CultureInfo.InvariantCulture), q.Call, q.Band, Contesting.ModeOf(q), q.Get("STX_STRING"), q.Get("SRX_STRING"), q.Country);
            Score s = _c.Compute(_session);
            int r10, r60;
            Contesting.Rate(_session, out r10, out r60);
            string what;
            List<string> needed = _c.Needed(s, band, out what);
            _score.Set(s, r10, r60, _session, needed, what, _c.Current);
            _score.Band = band;
            info();
            check();
        }

        private void saveEdi()
        {
            using (SaveFileDialog d = new SaveFileDialog { Filter = "EDI|*.edi", FileName = _book.MyCall + "-" + _c.Current.Id + ".edi" })
            {
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                File.WriteAllText(d.FileName, _c.Edi(_session), new UTF8Encoding(false));
                Status?.Invoke("Saved " + Path.GetFileName(d.FileName));
            }
        }
    }

    // chips in a row: DUPE, NEW MULT, points, the country
    internal class ChipStrip : Control
    {
        private readonly IKainosTheme _t;
        private List<Tuple<string, Color>> _items = new List<Tuple<string, Color>>();

        public ChipStrip(IKainosTheme t)
        {
            _t = t;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = t.Bg;
        }

        public void Set(List<Tuple<string, Color>> items) { _items = items; Invalidate(); }

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

    // the score: the four numbers, the rate, by band, and the mults still to work
    internal class ScorePanel : Control
    {
        private readonly IKainosTheme _t;
        private Score _s = new Score();
        private int _r10, _r60;
        private List<Qso> _session = new List<Qso>();
        private List<string> _needed = new List<string>();
        private string _what = "";
        private ContestDef _def;
        public string Band = "";

        public ScorePanel(IKainosTheme t)
        {
            _t = t;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = t.Panel;
        }

        public void Set(Score s, int r10, int r60, List<Qso> session, List<string> needed, string what, ContestDef def)
        {
            _s = s; _r10 = r10; _r60 = r60; _session = session; _needed = needed; _what = what; _def = def;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            using (Pen p = new Pen(_t.Line)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            int pad = _t.S(10), y = pad, w = Width - 2 * pad;
            using (Font head = _t.Font(_t.S(10), true)) TextRenderer.DrawText(g, "SCORE", head, new Point(pad, y), _t.Dim, TextFormatFlags.NoPadding);
            using (Font small = _t.Font(_t.S(10), false)) { string c = "claimed, before log checking"; Size cs = TextRenderer.MeasureText(c, small); TextRenderer.DrawText(g, c, small, new Point(Width - pad - cs.Width, y), _t.Faint, TextFormatFlags.NoPadding); }
            y += _t.S(18);
            string[] lab = { "QSOS", "POINTS", "MULTS", "SCORE" };
            string[] val = { _s.Qsos.ToString("N0", CultureInfo.InvariantCulture), _s.Points.ToString("N0", CultureInfo.InvariantCulture), _s.FieldDay ? "×2" : _s.Mults.ToString("N0", CultureInfo.InvariantCulture), _s.Total.ToString("N0", CultureInfo.InvariantCulture) };
            int cw = (w - 3 * _t.S(6)) / 4;
            using (Font big = new Font("Consolas", _t.S(19), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font lf = _t.Font(_t.S(9), true))
            using (Brush bg = new SolidBrush(_t.Bg))
            using (Pen line = new Pen(_t.Line))
                for (int i = 0; i < 4; i++)
                {
                    Rectangle r = new Rectangle(pad + i * (cw + _t.S(6)), y, cw, _t.S(48));
                    g.FillRectangle(bg, r);
                    g.DrawRectangle(line, r);
                    TextRenderer.DrawText(g, val[i], big, new Rectangle(r.X + _t.S(6), r.Y + _t.S(4), r.Width - _t.S(8), _t.S(24)), i == 3 ? _t.Gold : _t.Text, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                    TextRenderer.DrawText(g, lab[i], lf, new Point(r.X + _t.S(6), r.Y + _t.S(31)), _t.Faint, TextFormatFlags.NoPadding);
                }
            y += _t.S(56);
            using (Font f = _t.Font(_t.S(11.5f), false))
            {
                string rate = "Rate " + (_r10 > 0 ? _r10 + "/hr over the last 10" : "—") + " · " + _r60 + " in the last hour" + (_s.FieldDay ? " · ×2 for 100 W or less, plus bonuses" : "");
                TextRenderer.DrawText(g, rate, f, new Rectangle(pad, y, w, _t.S(18)), _t.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
            y += _t.S(22);
            // by band
            List<string> bands = _session.Select(q => q.Band).Distinct().OrderBy(Bands.Index).ToList();
            using (Font hf = _t.Font(_t.S(9.5f), true))
            using (Font mf = new Font("Consolas", _t.S(11.5f), FontStyle.Regular, GraphicsUnit.Pixel))
            {
                string[] cols = { "BAND", "QSOS", "CW", "SSB", "DIG", "MULTS" };
                int colW = w / cols.Length;
                for (int i = 0; i < cols.Length; i++) TextRenderer.DrawText(g, cols[i], hf, new Point(pad + i * colW, y), _t.Faint, TextFormatFlags.NoPadding);
                y += _t.S(16);
                foreach (string b in bands)
                {
                    if (y > Height - _t.S(60)) break;
                    List<Qso> qs = _session.Where(q => q.Band == b).ToList();
                    string[] v = { b, qs.Count.ToString(CultureInfo.InvariantCulture), qs.Count(q => q.Class == ModeClass.CW).ToString(CultureInfo.InvariantCulture), qs.Count(q => q.Class == ModeClass.Phone).ToString(CultureInfo.InvariantCulture),
                        qs.Count(q => q.Class == ModeClass.Digital).ToString(CultureInfo.InvariantCulture), _s.MultSet.Count(m => m.Contains(" " + b)).ToString(CultureInfo.InvariantCulture) };
                    for (int i = 0; i < v.Length; i++) TextRenderer.DrawText(g, v[i], mf, new Point(pad + i * colW, y), b == Band ? _t.Gold : _t.Text, TextFormatFlags.NoPadding);
                    y += _t.S(16);
                }
                if (bands.Count == 0) { TextRenderer.DrawText(g, "No QSOs yet.", _t.Font(_t.S(11.5f), false), new Point(pad, y), _t.Faint, TextFormatFlags.NoPadding); y += _t.S(16); }
            }
            y += _t.S(8);
            if (_what.Length > 0 && y < Height - _t.S(30))
            {
                using (Font hf = _t.Font(_t.S(9.5f), true)) TextRenderer.DrawText(g, "STILL TO WORK · " + _what.ToUpperInvariant(), hf, new Point(pad, y), _t.Faint, TextFormatFlags.NoPadding);
                y += _t.S(16);
                using (Font mf = new Font("Consolas", _t.S(11), FontStyle.Regular, GraphicsUnit.Pixel))
                    TextRenderer.DrawText(g, _needed.Count == 0 ? "All of them!" : string.Join(" ", _needed), mf, new Rectangle(pad, y, w, Height - y - pad), _t.Gold, TextFormatFlags.WordBreak);
            }
        }
    }

    // the messages: on or off, the CW text for F1 - F3, the voice keyer's slots
    internal class MessagesForm : KDialog
    {
        public MessagesForm(Messages m) : base(m.Theme, "Contest messages")
        {
            int x = T.S(16), y = T.S(14), lw = T.S(90), bw = T.S(380);
            Note("Enter and F1 - F3 on the CONTEST page send these, as contest loggers do: F1 CQ, F2 their call and your exchange, F3 TU. Esc stops. In CW they go through Kainos's keyer (CWX, like the CW terminal); on phone the Voice Keyer module sends its slots; nothing is sent in the digital modes.", x, y, lw + bw);
            y += T.S(64);
            CheckBox on = Check("Send messages with Enter and F1 - F3", x, y, m.On);
            y += T.S(30);
            if (m.Keyer == null) { Note("This Kainos is older than 1.0.15, so the logbook can't send messages yet.", x, y, lw + bw).ForeColor = T.Tx; y += T.S(24); }
            Heading("CW", x, y); y += T.S(22);
            TextBox[] cw = new TextBox[3];
            for (int i = 0; i < 3; i++)
            {
                Controls.Add(new Label { Text = "F" + (i + 1) + (i == 0 ? " CQ" : i == 1 ? " exchange" : " TU"), Location = new Point(x, y + 3), Size = new Size(lw, T.S(20)), ForeColor = T.Dim });
                cw[i] = Box(x + lw, y, bw);
                cw[i].CharacterCasing = CharacterCasing.Upper;
                cw[i].Text = m.Cw(i + 1);
                y += T.S(28);
            }
            Note("{MYCALL} your call, {CALL} theirs, {EXCH} what you send in this contest (with the serial number).", x + lw, y, bw);
            y += T.S(34);
            Heading("Phone: Voice Keyer slots", x, y); y += T.S(22);
            ComboBox[] slots = new ComboBox[3];
            for (int i = 0; i < 3; i++)
            {
                Controls.Add(new Label { Text = "F" + (i + 1), Location = new Point(x + i * T.S(130), y + 3), Size = new Size(T.S(28), T.S(20)), ForeColor = T.Dim });
                slots[i] = Combo(x + i * T.S(130) + T.S(30), y, T.S(80), Enumerable.Range(1, 12).Select(n => "slot F" + n).ToArray());
                slots[i].SelectedIndex = m.Slot(i + 1) - 1;
            }
            y += T.S(34);
            Note("Record them in the Voice Keyer (Tools > Voice Keyer): CQ, your exchange, TU. Their call is yours to say.", x, y, lw + bw);
            y += T.S(36);
            Button("Save", x + lw + bw - T.S(200), y, T.S(96), () =>
            {
                m.On = on.Checked;
                for (int i = 0; i < 3; i++) { m.SetCw(i + 1, cw[i].Text.Trim()); m.SetSlot(i + 1, slots[i].SelectedIndex + 1); }
                DialogResult = DialogResult.OK;
                Close();
            });
            Button("Cancel", x + lw + bw - T.S(96), y, T.S(96), Close);
            ClientSize = new Size(x * 2 + lw + bw, y + T.S(44));
        }
    }

    // what you send
    internal class MyExchangeForm : KDialog
    {
        public MyExchangeForm(Contesting c) : base(c.Book.Host.Theme, "My exchange")
        {
            int x = T.S(16), y = T.S(14), lw = T.S(150), bw = T.S(140);
            Note("What you send in contests. Your CQ and ITU zones and grid come from your call and grid unless you set them. " + c.Current.Name + " sends: " + string.Join(" ", c.Current.Sent.Select(Contest.Field.Label)) + ".", x, y, lw + bw);
            y += T.S(52);
            string[][] rows =
            {
                new[] { "state", "State / province" }, new[] { "sec", "ARRL / RAC section" }, new[] { "name", "Name (as sent)" }, new[] { "cls", "Field Day class" },
                new[] { "chk", "Check (SS: year licensed)" }, new[] { "prec", "Precedence (SS)" }, new[] { "num", "CWops number" }, new[] { "loc", "County (QSO parties)" },
                new[] { "age", "Age (All Asian)" }, new[] { "yr", "Year first licensed (EU HF)" }, new[] { "zone", "CQ zone" }, new[] { "itu", "ITU zone" }, new[] { "grid", "Grid (4)" }, new[] { "pwr", "Power (ARRL DX)" },
            };
            List<Action> saves = new List<Action>();
            foreach (string[] r in rows)
            {
                bool used = c.Current.Sent.Contains(r[0]);
                Controls.Add(new Label { Text = r[1], Location = new Point(x, y + 3), Size = new Size(lw, T.S(20)), ForeColor = used ? T.Gold : T.Dim });
                TextBox b = Box(x + lw, y, bw);
                b.CharacterCasing = CharacterCasing.Upper;
                b.Text = c.Mine.Get(r[0]);
                string k = r[0];
                saves.Add(() => c.Mine.Set(k, b.Text));
                y += T.S(26);
            }
            y += T.S(6);
            Button("Save", x + lw + bw - T.S(200), y, T.S(96), () => { foreach (Action a in saves) a(); DialogResult = DialogResult.OK; Close(); });
            Button("Cancel", x + lw + bw - T.S(96), y, T.S(96), Close);
            ClientSize = new Size(x * 2 + lw + bw, y + T.S(44));
        }
    }

    // the Cabrillo file: your categories (remembered), then save
    internal class CabrilloForm : KDialog
    {
        public CabrilloForm(Contesting c, List<Qso> session) : base(c.Book.Host.Theme, "Cabrillo · " + c.Current.Name)
        {
            IKainosHost h = c.Book.Host;
            int x = T.S(16), y = T.S(14), lw = T.S(170), bw = T.S(300);
            Dictionary<string, Func<string>> fields = new Dictionary<string, Func<string>>();
            Action<string, string[], string> combo = (name, items, def) =>
            {
                Controls.Add(new Label { Text = name, Location = new Point(x, y + 3), Size = new Size(lw, T.S(20)), ForeColor = T.Dim });
                ComboBox cb = Combo(x + lw, y, T.S(180), items);
                string v = h.GetSetting("cab." + name, def);
                cb.SelectedIndex = Math.Max(0, Array.IndexOf(items, v));
                fields[name] = () => (string)cb.SelectedItem;
                y += T.S(28);
            };
            Action<string, string, int> text = (name, def, lines) =>
            {
                Controls.Add(new Label { Text = name, Location = new Point(x, y + 3), Size = new Size(lw, T.S(20)), ForeColor = T.Dim });
                TextBox b = Box(x + lw, y, bw);
                if (lines > 1) { b.Multiline = true; b.Height = T.S(20) * lines; }
                b.Text = h.GetSetting("cab." + name, def);
                fields[name] = () => b.Text.Trim();
                y += lines > 1 ? T.S(20) * lines + T.S(8) : T.S(28);
            };
            string mode = c.Current.Modes.Length > 1 ? "MIXED" : c.Current.Modes[0] == "PH" ? "SSB" : c.Current.Modes[0] == "RY" ? "RTTY" : c.Current.Modes[0] == "DG" ? "DIGI" : "CW";
            combo("CATEGORY-OPERATOR", new[] { "SINGLE-OP", "MULTI-OP", "CHECKLOG" }, "SINGLE-OP");
            combo("CATEGORY-ASSISTED", new[] { "NON-ASSISTED", "ASSISTED" }, "NON-ASSISTED");
            combo("CATEGORY-BAND", new[] { "ALL", "160M", "80M", "40M", "20M", "15M", "10M", "6M", "2M" }, "ALL");
            combo("CATEGORY-POWER", new[] { "LOW", "HIGH", "QRP" }, "LOW");
            combo("CATEGORY-MODE", new[] { "CW", "SSB", "RTTY", "DIGI", "FM", "MIXED" }, mode);
            combo("CATEGORY-STATION", new[] { "FIXED", "PORTABLE", "MOBILE", "HQ", "SCHOOL" }, "FIXED");
            combo("CATEGORY-TRANSMITTER", new[] { "ONE", "TWO", "LIMITED", "UNLIMITED", "SWL" }, "ONE");
            text("LOCATION", c.Mine.Get("sec").Length > 0 ? c.Mine.Get("sec") : c.Mine.Get("state"), 1);
            text("OPERATORS", c.Book.MyCall, 1);
            text("NAME", "", 1);
            text("ADDRESS", "", 2);
            text("EMAIL", "", 1);
            text("CLUB", "", 1);
            text("SOAPBOX", "", 2);
            Label info = new Label { Location = new Point(x, y + 2), Size = new Size(lw + bw, T.S(36)), ForeColor = T.Dim, Text = session.Count + " QSOs, claimed score " + c.Compute(session).Total.ToString("N0", CultureInfo.InvariantCulture) + ". Send it in on the contest's web page." };
            Controls.Add(info);
            y += T.S(40);
            Button("Save…", x + lw + bw - T.S(200), y, T.S(96), () =>
            {
                Dictionary<string, string> head = new Dictionary<string, string>();
                foreach (KeyValuePair<string, Func<string>> kv in fields) { head[kv.Key] = kv.Value(); h.SetSetting("cab." + kv.Key, kv.Value()); }
                using (SaveFileDialog d = new SaveFileDialog { Filter = "Cabrillo|*.log;*.cbr|All files|*.*", FileName = c.Book.MyCall + "-" + c.Current.Cabrillo + ".log" })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    File.WriteAllText(d.FileName, c.Cabrillo(session, head, c.Book.Version), new UTF8Encoding(false));
                    info.ForeColor = Draw.Ok;
                    info.Text = "Saved " + Path.GetFileName(d.FileName) + ". Send it in on the contest's web page.";
                }
            });
            Button("Close", x + lw + bw - T.S(96), y, T.S(96), Close);
            ClientSize = new Size(x * 2 + lw + bw, y + T.S(44));
        }
    }
}
