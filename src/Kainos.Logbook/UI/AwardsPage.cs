/*  AwardsPage.cs

The AWARDS page, under the entry row: what counts as confirmed (LoTW + cards, eQSL, QRZ, any; the same choice as
the worked-before grid), a band and a mode, then DXCC, WAS, WAZ and grids side by side: worked and confirmed, the
way to the award, and the countries, states, zones and squares (blue worked, gold confirmed).

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kainos.Modules;

namespace Kainos.Logbook.UI
{
    internal class AwardsPage : Panel
    {
        private static readonly string[] SrcKeys = { "lotw", "eqsl", "qrz", "any" };
        private readonly Book _book;
        private readonly IKainosTheme _t;
        private readonly KButton[] _src;
        private readonly ComboBox _band, _mode;
        private readonly Label _note;
        private readonly AwardsView _view;

        public event Action ConfirmChanged;

        public AwardsPage(Book book)
        {
            _book = book;
            _t = book.Host.Theme;
            BackColor = _t.Bg;
            SuspendLayout();
            _src = SrcKeys.Select(k =>
            {
                KButton b = new KButton(_t, Book.ConfirmName(k)) { On = book.ConfirmSource == k };
                b.Click += (s, e) => { _book.ConfirmSource = k; foreach (KButton x in _src) x.On = x == s; refresh(); ConfirmChanged?.Invoke(); };
                Controls.Add(b);
                return b;
            }).ToArray();
            _band = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = _t.Surface, ForeColor = _t.Text };
            _band.Items.Add("All bands");
            _band.Items.AddRange(Bands.Names);
            _band.SelectedIndex = 0;
            _mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = _t.Surface, ForeColor = _t.Text };
            _mode.Items.AddRange(new object[] { "Any mode", "CW", "Phone", "Digital" });
            _mode.SelectedIndex = 0;
            _band.SelectedIndexChanged += (s, e) => refresh();
            _mode.SelectedIndexChanged += (s, e) => refresh();
            Controls.Add(_band); Controls.Add(_mode);
            _note = new Label { ForeColor = _t.Faint, BackColor = _t.Bg, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            Controls.Add(_note);
            _view = new AwardsView(_t);
            Controls.Add(_view);
            _book.Store.Changed += () => _book.Host.BeginInvoke(() => { if (!IsDisposed && Visible) refresh(); });
            ResumeLayout(false);
        }

        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (Visible) { foreach (KButton x in _src) x.On = SrcKeys[Array.IndexOf(_src, x)] == _book.ConfirmSource; refresh(); } }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (_view == null) return;
            int x = 0, bh = _t.S(24);
            foreach (KButton b in _src) { int w = _t.S(b.Text.Length > 6 ? 120 : 64); b.SetBounds(x, 0, w, bh); x += w + _t.S(4); }
            x += _t.S(8);
            _band.SetBounds(x, 1, _t.S(90), bh); x += _t.S(96);
            _mode.SetBounds(x, 1, _t.S(90), bh); x += _t.S(100);
            _note.SetBounds(x, 0, Math.Max(10, ClientSize.Width - x), bh);
            _view.SetBounds(0, bh + _t.S(6), ClientSize.Width, Math.Max(_t.S(40), ClientSize.Height - bh - _t.S(6)));
        }

        private void refresh()
        {
            string band = _band.SelectedIndex > 0 ? (string)_band.SelectedItem : "";
            ModeClass? cls = _mode.SelectedIndex > 0 ? (ModeClass?)(ModeClass)(_mode.SelectedIndex - 1) : null;
            _view.Set(Awards.Compute(_book, _book.Store.All(), band, cls), band, cls);
            string src = _book.ConfirmSource;
            _note.ForeColor = src == "lotw" ? _t.Faint : Color.FromArgb(0xe0, 0xb2, 0x5c);
            _note.Text = src == "lotw" ? "Confirmed: LoTW matches and paper cards, what ARRL (DXCC, WAS, VUCC) and CQ (WAZ) credit."
                                       : "Counting " + Book.ConfirmName(src) + ": ARRL and CQ don't credit it for these awards.";
        }
    }

    internal class AwardsView : Control
    {
        private readonly IKainosTheme _t;
        private AwardTotals _a = new AwardTotals();
        private string _band = "";

        public AwardsView(IKainosTheme t)
        {
            _t = t;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = t.Bg;
        }

        public void Set(AwardTotals a, string band, ModeClass? cls) { _a = a; _band = band; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            int gap = _t.S(8), n = 4;
            int w = (Width - gap * (n - 1)) / n;
            Rectangle[] cols = Enumerable.Range(0, n).Select(i => new Rectangle(i * (w + gap), 0, w, Height - 1)).ToArray();
            dxcc(g, cols[0]);
            tiles(g, cols[1], "WAS · 50 STATES", _a.Was, Awards.States, 50, 10, "Worked All States: 50 confirmed");
            tiles(g, cols[2], "WAZ · 40 CQ ZONES", _a.Waz, Enumerable.Range(1, 40).Select(z => z.ToString(CultureInfo.InvariantCulture)).ToArray(), 40, 8, "Worked All Zones: 40 confirmed");
            grids(g, cols[3]);
        }

        private int frame(Graphics g, Rectangle r, string title, Tally t, int target, string goal)
        {
            using (Brush b = new SolidBrush(_t.Panel)) g.FillRectangle(b, r);
            using (Pen p = new Pen(_t.Line)) g.DrawRectangle(p, r);
            int pad = _t.S(8), y = r.Y + pad, x = r.X + pad, w = r.Width - 2 * pad;
            using (Font f = _t.Font(_t.S(10), true)) TextRenderer.DrawText(g, title + (_band.Length > 0 ? " · " + _band : ""), f, new Point(x, y), _t.Dim, TextFormatFlags.NoPadding);
            y += _t.S(16);
            using (Font big = new Font("Consolas", _t.S(20), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font small = _t.Font(_t.S(11), false))
            {
                string wk = t.Worked.Count.ToString(CultureInfo.InvariantCulture), cf = t.Confirmed.Count.ToString(CultureInfo.InvariantCulture);
                TextRenderer.DrawText(g, wk, big, new Point(x, y), _t.Text, TextFormatFlags.NoPadding);
                int wx = x + TextRenderer.MeasureText(g, wk, big, Size.Empty, TextFormatFlags.NoPadding).Width;
                TextRenderer.DrawText(g, " worked · ", small, new Point(wx, y + _t.S(8)), _t.Faint, TextFormatFlags.NoPadding);
                wx += TextRenderer.MeasureText(g, " worked · ", small, Size.Empty, TextFormatFlags.NoPadding).Width;
                TextRenderer.DrawText(g, cf, big, new Point(wx, y), _t.Gold, TextFormatFlags.NoPadding);
                wx += TextRenderer.MeasureText(g, cf, big, Size.Empty, TextFormatFlags.NoPadding).Width;
                TextRenderer.DrawText(g, " confirmed", small, new Point(wx, y + _t.S(8)), _t.Faint, TextFormatFlags.NoPadding);
            }
            y += _t.S(28);
            Rectangle bar = new Rectangle(x, y, w, _t.S(6));
            using (Brush b = new SolidBrush(_t.Surface)) g.FillRectangle(b, bar);
            int done = Math.Min(target, t.Confirmed.Count);
            using (Brush b = new SolidBrush(done >= target ? Draw.Ok : _t.Gold)) g.FillRectangle(b, bar.X, bar.Y, bar.Width * done / Math.Max(1, target), bar.Height);
            y += _t.S(10);
            using (Font f = _t.Font(_t.S(10.5f), false)) TextRenderer.DrawText(g, goal, f, new Rectangle(x, y, w, _t.S(16)), _t.Faint, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            return y + _t.S(18);
        }

        private void dxcc(Graphics g, Rectangle r)
        {
            int y = frame(g, r, "DXCC", _a.Dxcc, 100, "DXCC: 100 confirmed (" + _a.EntitiesTotal + " countries on the list)");
            int pad = _t.S(8), x = r.X + pad, w = r.Width - 2 * pad;
            using (Font f = _t.Font(_t.S(10.5f), false))
            {
                string conts = string.Join(" · ", new[] { "NA", "SA", "EU", "AF", "AS", "OC" }.Select(c => c + " " + _a.ContinentOf.Count(kv => kv.Value == c && _a.Dxcc.Worked.Contains(kv.Key))));
                TextRenderer.DrawText(g, conts, f, new Rectangle(x, y, w, _t.S(16)), _t.Dim, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                y += _t.S(16);
                string five = "5BDXCC: " + string.Join(" · ", Awards.FiveBands.Select(b => b.Replace("m", "") + " " + _a.FiveBand[b].Confirmed.Count)) + " (100 each)";
                TextRenderer.DrawText(g, five, f, new Rectangle(x, y, w, _t.S(16)), _t.Dim, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                y += _t.S(16);
                TextRenderer.DrawText(g, "Challenge: " + _a.Challenge.Confirmed.Count + " band-countries confirmed (1,000)", f, new Rectangle(x, y, w, _t.S(16)), _t.Dim, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                y += _t.S(20);
            }
            chips(g, new Rectangle(x, y, w, r.Bottom - y - pad), _a.Dxcc, _a.Dxcc.Worked.OrderBy(n => _a.ContinentOf.ContainsKey(n) ? _a.ContinentOf[n] : "").ThenBy(n => n).ToList());
        }

        private void grids(Graphics g, Rectangle r)
        {
            int y = frame(g, r, "GRIDS", _a.Grids, 100, "VUCC: 100 confirmed on a band (6 m and up)");
            int pad = _t.S(8), x = r.X + pad, w = r.Width - 2 * pad;
            using (Font f = _t.Font(_t.S(10.5f), false))
            {
                TextRenderer.DrawText(g, "6 m: " + _a.Grids6.Worked.Count + " worked · " + _a.Grids6.Confirmed.Count + " confirmed", f, new Point(x, y), _t.Dim, TextFormatFlags.NoPadding);
                y += _t.S(16);
                TextRenderer.DrawText(g, "2 m: " + _a.Grids2.Worked.Count + " worked · " + _a.Grids2.Confirmed.Count + " confirmed", f, new Point(x, y), _t.Dim, TextFormatFlags.NoPadding);
                y += _t.S(20);
            }
            chips(g, new Rectangle(x, y, w, r.Bottom - y - pad), _a.Grids, _a.Grids.Worked.OrderBy(s => s).ToList());
        }

        // a wrapping list of names, gold when confirmed; "+N more" when it runs out of room
        private void chips(Graphics g, Rectangle area, Tally t, List<string> items)
        {
            if (items.Count == 0)
            {
                using (Font f = _t.Font(_t.S(11), false)) TextRenderer.DrawText(g, "Nothing yet" + (_band.Length > 0 ? " on " + _band : "") + ".", f, area.Location, _t.Faint, TextFormatFlags.NoPadding);
                return;
            }
            using (Font f = _t.Font(_t.S(10), false))
            {
                int x = area.X, y = area.Y, h = _t.S(18), shown = 0;
                foreach (string it in items)
                {
                    Size s = TextRenderer.MeasureText(g, it, f, Size.Empty, TextFormatFlags.NoPadding);
                    int cw = s.Width + _t.S(10);
                    if (x + cw > area.Right) { x = area.X; y += h + _t.S(3); }
                    if (y + h * 2 > area.Bottom && shown < items.Count - 1)
                    {
                        TextRenderer.DrawText(g, "+" + (items.Count - shown) + " more", f, new Point(x, y + _t.S(2)), _t.Faint, TextFormatFlags.NoPadding);
                        return;
                    }
                    bool c = t.Confirmed.Contains(it);
                    Rectangle rr = new Rectangle(x, y, cw, h);
                    using (Brush b = new SolidBrush(Draw.Blend(_t.Panel, c ? _t.Gold : _t.Ice, c ? 0.28f : 0.18f))) g.FillRectangle(b, rr);
                    TextRenderer.DrawText(g, it, f, new Point(x + _t.S(5), y + (h - s.Height) / 2), c ? Draw.Blend(_t.Gold, Color.White, 0.3f) : _t.Ice, TextFormatFlags.NoPadding);
                    x += cw + _t.S(4);
                    shown++;
                }
            }
        }

        private void tiles(Graphics g, Rectangle r, string title, Tally t, string[] all, int target, int perRow, string goal)
        {
            int y = frame(g, r, title, t, target, goal);
            int pad = _t.S(8), x0 = r.X + pad, w = r.Width - 2 * pad;
            int gap = _t.S(3), tw = (w - gap * (perRow - 1)) / perRow;
            int rows = (all.Length + perRow - 1) / perRow;
            int th = Math.Max(_t.S(14), Math.Min(_t.S(24), (r.Bottom - pad - y - gap * rows) / Math.Max(1, rows)));
            using (Font f = new Font("Consolas", _t.S(10), FontStyle.Bold, GraphicsUnit.Pixel))
                for (int i = 0; i < all.Length; i++)
                {
                    Rectangle tr = new Rectangle(x0 + (i % perRow) * (tw + gap), y + (i / perRow) * (th + gap), tw, th);
                    if (tr.Bottom > r.Bottom - 2) break;
                    bool c = t.Confirmed.Contains(all[i]), wk = t.Worked.Contains(all[i]);
                    Color fill = c ? Draw.Blend(_t.Panel, _t.Gold, 0.35f) : wk ? Draw.Blend(_t.Panel, _t.Ice, 0.30f) : _t.Bg;
                    using (Brush b = new SolidBrush(fill)) g.FillRectangle(b, tr);
                    using (Pen p = new Pen(c ? _t.Gold : wk ? _t.Ice : _t.Line)) g.DrawRectangle(p, tr);
                    TextRenderer.DrawText(g, all[i], f, tr, c ? Draw.Blend(_t.Gold, Color.White, 0.3f) : wk ? _t.Ice : _t.Faint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
        }
    }
}
