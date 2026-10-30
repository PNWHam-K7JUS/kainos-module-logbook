/*  LookupCard.cs

What's known about the call being typed: name and QTH (from a lookup), country, continent, zones, grid, distance,
beam heading (with a compass), their local time, QSL details; then worked-before for its country by band and
mode, and how often this call's been worked. Drawn, not built from controls, so it redraws at once as each letter
is typed.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kainos.Logbook.Lookup;
using Kainos.Modules;

namespace Kainos.Logbook.UI
{
    internal class LookupCard : Control
    {
        private readonly IKainosTheme _t;
        private readonly Book _book;

        public string Call = "";
        public Worked Worked;
        public CallInfo Info;
        public bool Looking;
        public string Band = "";
        public ModeClass Class;
        public string Problem = "";
        public string Park = "";       // their park, when one is typed: "US-1234 · name, where"

        public LookupCard(IKainosTheme t, Book book)
        {
            _t = t;
            _book = book;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = t.Panel;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int pad = _t.S(10), y = pad, w = Width - 2 * pad;
            using (Pen p = new Pen(_t.Line)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            using (Font head = _t.Font(_t.S(10), true))
                TextRenderer.DrawText(g, "LOOKUP", head, new Point(pad, y), _t.Dim, TextFormatFlags.NoPadding);
            using (Font small = _t.Font(_t.S(10), false))
            {
                string src = Call.Length < 2 ? "" : Looking ? "looking up..." : Info != null ? Info.Source + " · " + _book.Cty.Source : _book.Cty.Source;
                Size ss = TextRenderer.MeasureText(src, small);
                TextRenderer.DrawText(g, src, small, new Point(Width - pad - ss.Width, y), _t.Faint, TextFormatFlags.NoPadding);
            }
            y += _t.S(20);

            if (Call.Length < 2 || Worked == null || Worked.Cty == null)
            {
                string msg = Call.Length < 2
                    ? "Type a call. With each letter the logbook works out the country, zones, beam heading and distance, and checks your log for new ones, new bands and new modes. When the call is complete it asks " + lookupNames() + " for the name and QTH."
                    : "No country found for " + Call + " yet.";
                using (Font f = _t.Font(_t.S(12), false))
                    TextRenderer.DrawText(g, msg, f, new Rectangle(pad, y, w, Height - y - pad), _t.Dim, TextFormatFlags.WordBreak);
                return;
            }

            CtyInfo c = Worked.Cty;
            int compass = Math.Min(_t.S(104), Math.Max(_t.S(70), w / 3));
            int textW = w - compass - _t.S(10);
            using (Font callF = _t.Font(_t.S(22), true))
                TextRenderer.DrawText(g, Call, callF, new Point(pad, y), _t.Gold, TextFormatFlags.NoPadding);
            y += _t.S(28);
            string title = Info != null && !string.IsNullOrEmpty(Info.Name) ? Info.Name : c.Name;
            using (Font f = _t.Font(_t.S(14), true))
                TextRenderer.DrawText(g, title, f, new Rectangle(pad, y, textW, _t.S(20)), _t.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            y += _t.S(20);
            if (Park.Length > 0)
            {
                using (Font f = _t.Font(_t.S(12), true))
                    TextRenderer.DrawText(g, "PARK " + Park, f, new Rectangle(pad, y, textW, _t.S(18)), Draw.Ok, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                y += _t.S(18);
            }
            if (Info != null && Info.Qth.Length > 0)
            {
                using (Font f = _t.Font(_t.S(12), false))
                    TextRenderer.DrawText(g, Info.Qth, f, new Rectangle(pad, y, textW, _t.S(18)), _t.Dim, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                y += _t.S(18);
            }
            y += _t.S(4);

            // the facts
            double myLat, myLon;
            bool haveMe = _book.MyLatLon(out myLat, out myLon);
            double lat = Info != null && Info.HasLatLon ? Info.Lat : c.Lat, lon = Info != null && Info.HasLatLon ? Info.Lon : c.Lon;
            int sp = haveMe ? Geo.Bearing(myLat, myLon, lat, lon) : -1;
            double km = haveMe ? Geo.DistanceKm(myLat, myLon, lat, lon) : 0;
            List<string[]> rows = new List<string[]>
            {
                new[] { "Country", c.Name + (c.CallArea != null && c.Name == "United States" ? " · call area " + c.CallArea : "") },
                new[] { "Zones", "CQ " + c.Cq + " · ITU " + c.Itu + " · " + c.Cont },
                new[] { "Grid", Info != null && !string.IsNullOrEmpty(Info.Grid) ? Info.Grid : Geo.LatLonToGrid(c.Lat, c.Lon, 4) + " (country)" },
            };
            if (haveMe)
            {
                rows.Add(new[] { "Distance", ((int)km).ToString("N0", CultureInfo.InvariantCulture) + " km · " + ((int)(km * 0.6214)).ToString("N0", CultureInfo.InvariantCulture) + " mi" });
                rows.Add(new[] { "Beam", sp + "° short · " + (sp + 180) % 360 + "° long" });
            }
            DateTime their = DateTime.UtcNow.AddHours(c.UtcOffset);
            rows.Add(new[] { "Their time", their.ToString("HH:mm", CultureInfo.InvariantCulture) + " (UTC" + (c.UtcOffset >= 0 ? "+" : "") + c.UtcOffset.ToString("0.#", CultureInfo.InvariantCulture) + ")" });
            if (Info != null)
            {
                List<string> qsl = new List<string>();
                if (Info.Lotw == true) qsl.Add("LoTW");
                if (Info.Eqsl == true) qsl.Add("eQSL");
                if (!string.IsNullOrEmpty(Info.QslVia)) qsl.Add("via " + Info.QslVia);
                if (qsl.Count > 0) rows.Add(new[] { "QSL", string.Join(" · ", qsl) });
                if (!string.IsNullOrEmpty(Info.Class)) rows.Add(new[] { "Licence", Info.Class });
            }
            int y0 = y;
            using (Font k = _t.Font(_t.S(11.5f), false))
            using (Font v = new Font("Consolas", _t.S(12), FontStyle.Regular, GraphicsUnit.Pixel))
            {
                int kw = _t.S(70);
                foreach (string[] r in rows)
                {
                    TextRenderer.DrawText(g, r[0], k, new Point(pad, y), _t.Faint, TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(g, r[1], v, new Rectangle(pad + kw, y, textW - kw, _t.S(17)), _t.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                    y += _t.S(17);
                }
            }
            if (sp >= 0) drawCompass(g, new Rectangle(Width - pad - compass, y0 - _t.S(48), compass, compass), sp);
            y = Math.Max(y, y0 - _t.S(48) + compass) + _t.S(8);

            // worked-before by band and mode
            y = drawGrid(g, pad, y, w);
            y += _t.S(6);
            if (Worked.SameCall.Count > 0)
            {
                string last = string.Join(" · ", Worked.SameCall.OrderByDescending(q => q.TimeOn).Take(3).Select(q => q.TimeOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " " + q.Band + " " + q.ModeShown));
                using (Font f = _t.Font(_t.S(11.5f), false))
                    TextRenderer.DrawText(g, "Worked " + Worked.SameCall.Count + "× before: " + last, f, new Rectangle(pad, y, w, _t.S(32)), _t.Dim, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
                y += _t.S(34);
            }
            if (Info == null && !Looking)
            {
                string why = !_book.Lookup.Any ? "Set up QRZ.com or HamQTH in Settings for the name, QTH and grid."
                    : Problem.Length > 0 ? "Lookup: " + Problem
                    : c.Name != "United States" && !_book.Lookup.UseQrz && !_book.Lookup.UseHamQth ? "callook.info only knows US calls. Set up QRZ.com or HamQTH in Settings for the rest of the world."
                    : "";
                if (why.Length > 0)
                    using (Font f = _t.Font(_t.S(11), false))
                        TextRenderer.DrawText(g, why, f, new Rectangle(pad, y, w, Height - y - pad), _t.Faint, TextFormatFlags.WordBreak);
            }
        }

        private string lookupNames()
        {
            List<string> n = new List<string>();
            if (_book.Lookup.UseQrz) n.Add("QRZ.com");
            if (_book.Lookup.UseHamQth) n.Add("HamQTH");
            if (_book.Lookup.UseCallook) n.Add("callook.info (US)");
            return n.Count > 0 ? string.Join(" or ", n) : "QRZ.com or HamQTH (set them up in Settings)";
        }

        private int drawGrid(Graphics g, int x, int y, int w)
        {
            string[] bands = Bands.Grid;
            int lw = _t.S(30);
            int cw = Math.Max(_t.S(18), (w - lw) / bands.Length), rh = _t.S(18);
            using (Font f = new Font("Consolas", _t.S(10.5f), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Pen line = new Pen(_t.Line))
            using (Brush worked = new SolidBrush(Draw.Blend(_t.Panel, _t.Ice, 0.45f)))
            using (Brush conf = new SolidBrush(Draw.Blend(_t.Panel, _t.Gold, 0.40f)))
            {
                for (int i = 0; i < bands.Length; i++)
                    TextRenderer.DrawText(g, bands[i].Replace("m", ""), f, new Rectangle(x + lw + i * cw, y, cw, rh), _t.Faint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                y += rh;
                string[] rowNames = { "CW", "SSB", "DIG" };
                for (int r = 0; r < 3; r++)
                {
                    TextRenderer.DrawText(g, rowNames[r], f, new Rectangle(x, y, lw, rh), _t.Faint, TextFormatFlags.VerticalCenter);
                    for (int i = 0; i < bands.Length; i++)
                    {
                        Rectangle cell = new Rectangle(x + lw + i * cw, y, cw - 1, rh - 1);
                        Slot s = Worked.Grid[bands[i]][r];
                        if (s == Slot.Confirmed) g.FillRectangle(conf, cell);
                        else if (s == Slot.Worked) g.FillRectangle(worked, cell);
                        g.DrawRectangle(line, cell);
                        if (bands[i] == Band && (int)Class == r)
                            using (Pen now = new Pen(_t.Gold, 2)) g.DrawRectangle(now, cell.X + 1, cell.Y + 1, cell.Width - 2, cell.Height - 2);
                    }
                    y += rh;
                }
            }
            using (Font f = _t.Font(_t.S(10), false))
            {

                int lx = x;
                TextRenderer.DrawText(g, "■", f, new Point(lx, y + 3), _t.Ice, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, " worked", f, new Point(lx + _t.S(8), y + 3), _t.Faint, TextFormatFlags.NoPadding);
                lx += _t.S(60);
                TextRenderer.DrawText(g, "■", f, new Point(lx, y + 3), _t.Gold, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, " confirmed (" + Book.ConfirmName(_book.ConfirmSource) + ")", f, new Point(lx + _t.S(8), y + 3), _t.Faint, TextFormatFlags.NoPadding);
                y += _t.S(16);
            }
            return y;
        }

        private void drawCompass(Graphics g, Rectangle r, int sp)
        {
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, rad = r.Width / 2f - 2;
            using (Brush b = new SolidBrush(_t.Bg)) g.FillEllipse(b, cx - rad, cy - rad, rad * 2, rad * 2);
            using (Pen p = new Pen(_t.Line)) g.DrawEllipse(p, cx - rad, cy - rad, rad * 2, rad * 2);
            using (Pen tick = new Pen(Color.FromArgb(0x46, 0x5f, 0x6f)))
                for (int d = 0; d < 360; d += 30)
                {
                    double a = d * Math.PI / 180;
                    float r1 = rad * (d % 90 == 0 ? 0.80f : 0.88f);
                    g.DrawLine(tick, cx + (float)(Math.Sin(a) * r1), cy - (float)(Math.Cos(a) * r1), cx + (float)(Math.Sin(a) * rad * 0.97), cy - (float)(Math.Cos(a) * rad * 0.97));
                }
            using (Font f = _t.Font(_t.S(9), true))
            {
                string[] n = { "N", "E", "S", "W" };
                for (int i = 0; i < 4; i++)
                {
                    double a = i * Math.PI / 2;
                    float tx = cx + (float)(Math.Sin(a) * rad * 0.62), ty = cy - (float)(Math.Cos(a) * rad * 0.62);
                    Size s = TextRenderer.MeasureText(n[i], f);
                    TextRenderer.DrawText(g, n[i], f, new Point((int)(tx - s.Width / 2f), (int)(ty - s.Height / 2f)), _t.Dim, TextFormatFlags.NoPadding);
                }
            }
            double lp = (sp + 180) * Math.PI / 180, spa = sp * Math.PI / 180;
            using (Pen p = new Pen(_t.Faint, 2) { DashStyle = DashStyle.Dash })
                g.DrawLine(p, cx, cy, cx + (float)(Math.Sin(lp) * rad * 0.7), cy - (float)(Math.Cos(lp) * rad * 0.7));
            using (Pen p = new Pen(_t.Gold, 3) { EndCap = LineCap.Round })
                g.DrawLine(p, cx, cy, cx + (float)(Math.Sin(spa) * rad * 0.85), cy - (float)(Math.Cos(spa) * rad * 0.85));
            using (Brush b = new SolidBrush(_t.Gold)) g.FillEllipse(b, cx - 3, cy - 3, 6, 6);
        }
    }
}
