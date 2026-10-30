/*  ServicesForm.cs

Uploads: LoTW, eQSL, QRZ Logbook and Club Log, each switched on here (they start off), with its login, whether it
sends each QSO as it's logged, what's waiting, and buttons: Preview (exactly what would be sent, passwords
hidden, nothing sent), Send now, Get confirmations, and Send older QSOs (an imported log, or QSOs from before it
was switched on; asked first).

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kainos.Logbook.Lookup;
using Kainos.Logbook.Services;

namespace Kainos.Logbook.UI
{
    internal class ServicesForm : KDialog
    {
        private readonly Book _book;
        private readonly Uploader _up;
        private readonly List<Action> _saves = new List<Action>();
        private readonly Dictionary<string, Label> _status = new Dictionary<string, Label>();
        private readonly Dictionary<string, KButton> _older = new Dictionary<string, KButton>();
        private readonly Panel _body;
        private readonly Timer _refresh;

        public ServicesForm(Book book, Uploader up) : base(book.Host.Theme, "Uploads")
        {
            _book = book;
            _up = up;
            int w = T.S(640);
            _body = new Panel { Location = new Point(0, 0), AutoScroll = true, BackColor = T.Panel };
            Controls.Add(_body);
            int y = T.S(12), x = T.S(16);
            y = note(_body, "Each service starts switched off. Switched on, it sends the QSOs you log from then on (here, and from the FT8 and SSTV modules); an imported log or older QSOs go only if you press Send older QSOs. Preview shows exactly what would be sent and sends nothing.", x, y, w - 2 * x) + T.S(10);

            Lotw lotw = (Lotw)up.Get("lotw");
            y = card(lotw, y, w, (yy) =>
            {
                lab("TQSL", x, yy);
                TextBox path = box(x + T.S(110), yy, T.S(330), false, lotw.TqslPath);
                button("Browse…", x + T.S(448), yy - 2, T.S(80), () =>
                {
                    using (OpenFileDialog d = new OpenFileDialog { Filter = "tqsl.exe|tqsl.exe|Programs|*.exe", Title = "Where TQSL is" })
                        if (d.ShowDialog(this) == DialogResult.OK) path.Text = d.FileName;
                });
                yy += T.S(28);
                lab("Station location", x, yy);
                ComboBox loc = new ComboBox { Location = new Point(x + T.S(110), yy), Width = T.S(220), DropDownStyle = ComboBoxStyle.DropDown, FlatStyle = FlatStyle.Flat, BackColor = T.Surface, ForeColor = T.Text };
                loc.Items.AddRange(Lotw.Locations().ToArray());
                loc.Text = lotw.Location;
                _body.Controls.Add(loc);
                yy = note(_body, "As named in TQSL (Station > Station Locations).", x + T.S(340), yy + 3, w - x - T.S(356));
                yy = Math.Max(yy, loc.Bottom) + T.S(6);
                lab("LoTW website", x, yy);
                TextBox u = box(x + T.S(110), yy, T.S(150), false, Secrets.Get(_book.Host, "lotw.user"));
                TextBox p = box(x + T.S(268), yy, T.S(150), true, Secrets.Get(_book.Host, "lotw.pass"));
                yy = note(_body, "For Get confirmations only (uploads use TQSL).", x + T.S(426), yy + 3, w - x - T.S(442)) + T.S(4);
                _saves.Add(() =>
                {
                    lotw.TqslPath = path.Text.Trim(); lotw.Location = loc.Text.Trim();
                    Secrets.Set(_book.Host, "lotw.user", u.Text.Trim()); Secrets.Set(_book.Host, "lotw.pass", p.Text);
                });
                return Math.Max(yy, p.Bottom + T.S(4));
            });

            Eqsl eqsl = (Eqsl)up.Get("eqsl");
            y = card(eqsl, y, w, (yy) =>
            {
                lab("eQSL login", x, yy);
                TextBox u = box(x + T.S(110), yy, T.S(150), false, Secrets.Get(_book.Host, "eqsl.user"));
                TextBox p = box(x + T.S(268), yy, T.S(150), true, Secrets.Get(_book.Host, "eqsl.pass"));
                _saves.Add(() => { Secrets.Set(_book.Host, "eqsl.user", u.Text.Trim()); Secrets.Set(_book.Host, "eqsl.pass", p.Text); });
                return p.Bottom + T.S(6);
            });

            QrzLogbook qrz = (QrzLogbook)up.Get("qrz");
            y = card(qrz, y, w, (yy) =>
            {
                lab("API key", x, yy);
                TextBox k = box(x + T.S(110), yy, T.S(200), true, Secrets.Get(_book.Host, "qrzlog.key"));
                int b = note(_body, "QRZ.com > Logbook > Settings: the logbook's API key (not your password).", x + T.S(318), yy + 3, w - x - T.S(334));
                _saves.Add(() => Secrets.Set(_book.Host, "qrzlog.key", k.Text.Trim()));
                return Math.Max(b, k.Bottom) + T.S(6);
            });

            ClubLog cl = (ClubLog)up.Get("clublog");
            y = card(cl, y, w, (yy) =>
            {
                lab("Email", x, yy);
                TextBox e = box(x + T.S(110), yy, T.S(200), false, Secrets.Get(_book.Host, "clublog.email"));
                lab("App password", x + T.S(320), yy);
                TextBox p = box(x + T.S(420), yy, T.S(150), true, Secrets.Get(_book.Host, "clublog.pass"));
                yy += T.S(28);
                lab("API key", x, yy);
                TextBox k = box(x + T.S(110), yy, T.S(200), true, Secrets.Get(_book.Host, "clublog.api"));
                yy = note(_body, "Club Log gives each program an API key (ask at clublog.org's helpdesk). An app password is made in Club Log > Settings.", x + T.S(320), yy + 3, w - x - T.S(336));
                _saves.Add(() => { Secrets.Set(_book.Host, "clublog.email", e.Text.Trim()); Secrets.Set(_book.Host, "clublog.pass", p.Text); Secrets.Set(_book.Host, "clublog.api", k.Text.Trim()); });
                return Math.Max(yy, k.Bottom) + T.S(6);
            });

            y = note(_body, "Logins and keys are kept encrypted for your Windows account only. Confirmations are fetched only when you press Get confirmations.", x, y, w - 2 * x) + T.S(10);
            _body.AutoScrollMinSize = new Size(0, y);
            int bottom = T.S(48);
            int h = Math.Min(y, Screen.FromControl(this).WorkingArea.Height - bottom - T.S(80));
            _body.Size = new Size(w, h);
            Button("Save", w - T.S(220), h + T.S(10), T.S(100), () => { save(); DialogResult = DialogResult.OK; Close(); });
            Button("Close", w - T.S(112), h + T.S(10), T.S(96), Close);
            ClientSize = new Size(w, h + bottom);

            _refresh = new Timer { Interval = 1000 };
            _refresh.Tick += (s, e) => refresh();
            _refresh.Start();
            refresh();
        }

        private int card(Service s, int y, int w, Func<int, int> body)
        {
            int x = T.S(16);
            Panel line = new Panel { Location = new Point(x, y), Size = new Size(w - 2 * x, 1), BackColor = T.Line };
            _body.Controls.Add(line);
            y += T.S(8);
            CheckBox on = new CheckBox { Text = "Use " + s.Name, Location = new Point(x, y), AutoSize = true, Checked = s.Enabled, ForeColor = T.Gold, Font = T.Font(T.S(13), true), BackColor = T.Panel };
            CheckBox auto = new CheckBox { Text = "Send each QSO as I log it", Location = new Point(x + T.S(220), y + 3), AutoSize = true, Checked = s.Auto, ForeColor = T.Text, BackColor = T.Panel };
            _body.Controls.Add(on);
            _body.Controls.Add(auto);
            y += T.S(30);
            y = body(y);
            Label st = new Label { Location = new Point(x, y + 4), Size = new Size(w - 2 * x, T.S(18)), ForeColor = T.Dim, AutoEllipsis = true };
            _body.Controls.Add(st);
            _status[s.Key] = st;
            y += T.S(26);
            int bx = x;
            button("Preview…", bx, y, T.S(90), () => { save(); preview(s); }); bx += T.S(98);
            button("Send now", bx, y, T.S(90), async () => { save(); await _up.SendNowAsync(s); refresh(); }); bx += T.S(98);
            if (s.Confirms) { button("Get confirmations", bx, y, T.S(140), async () => { save(); string r = await _up.FetchAsync(s); refresh(); }); bx += T.S(148); }
            _older[s.Key] = button("Send older QSOs…", bx, y, T.S(150), () => older(s));
            _saves.Add(() => { s.Enabled = on.Checked; s.Auto = auto.Checked; });
            return y + T.S(40);
        }

        private void refresh()
        {
            List<Qso> all = _book.Store.All();
            foreach (Service s in _up.Services)
            {
                ServiceState st = _up.State(s);
                int waiting = s.Enabled || s.Since != DateTime.MaxValue ? all.Count(s.Waiting) : 0;
                int older = all.Count(s.Older);
                string text = !s.Enabled ? "Off." : st.Busy ? "Working…" : waiting + " waiting.";
                if (older > 0 && s.Enabled) text += " " + older + " older not sent.";
                if (st.Last.Length > 0) text += " Last: " + st.Last + " (" + st.LastAt.ToLocalTime().ToString("HH:mm") + ")";
                _status[s.Key].Text = text;
                _status[s.Key].ForeColor = st.LoginRefused ? T.Tx : T.Dim;
                _older[s.Key].Enabled = s.Enabled && older > 0;
            }
        }

        private void save()
        {
            foreach (Action a in _saves) a();
            _up.Reset();
        }

        private void preview(Service s)
        {
            List<Qso> list = _up.Waiting(s);
            if (list.Count == 0) list = _book.Store.All().Where(s.Older).Take(3).ToList();
            string head = list.Count == 0 ? "Nothing to send yet. Log a QSO first.\r\n" :
                (_up.Waiting(s).Count > 0 ? list.Count + " QSO" + (list.Count == 1 ? "" : "s") + " waiting. " : "Nothing waiting; here are up to 3 older QSOs as they would go. ") + "Nothing has been sent.\r\n\r\n";
            using (KDialog d = new KDialog(T, s.Name + ": what would be sent"))
            {
                TextBox t = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Location = new Point(T.S(12), T.S(12)), Size = new Size(T.S(760), T.S(380)), BackColor = T.Surface, ForeColor = T.Dim, Font = new Font("Consolas", T.S(11.5f), FontStyle.Regular, GraphicsUnit.Pixel), Text = head + s.Preview(list) };
                d.Controls.Add(t);
                KButton close = new KButton(T, "Close") { Location = new Point(T.S(676), T.S(402)), Size = new Size(T.S(96), T.S(28)), BackColor = T.Panel };
                close.Click += (o, e) => d.Close();
                d.Controls.Add(close);
                d.ClientSize = new Size(T.S(784), T.S(442));
                d.ShowDialog(this);
            }
        }

        private void older(Service s)
        {
            int n = _book.Store.All().Count(s.Older);
            if (MessageBox.Show(this, "Send " + n + " older QSO" + (n == 1 ? "" : "s") + " to " + s.Name + "?\n\nThese are QSOs from before you switched " + s.Name + " on, or imported from another log, that haven't been sent to it. " + s.Name + " keeps what it's sent" + (s.Key == "lotw" ? " (LoTW can't delete QSOs)" : "") + ", so check the log first.",
                    "Send older QSOs", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            save();
            s.Since = DateTime.MinValue;
            refresh();
        }

        // ---- small builders on the scrolling body ----
        private void lab(string text, int x, int y) { _body.Controls.Add(new Label { Text = text, Location = new Point(x, y + 3), Size = new Size(T.S(104), T.S(20)), ForeColor = T.Dim }); }

        private TextBox box(int x, int y, int w, bool secret, string text)
        {
            TextBox b = new TextBox { Location = new Point(x, y), Width = w, BorderStyle = BorderStyle.FixedSingle, BackColor = T.Surface, ForeColor = T.Text, UseSystemPasswordChar = secret, Text = text ?? "" };
            _body.Controls.Add(b);
            return b;
        }

        private KButton button(string text, int x, int y, int w, Action click)
        {
            KButton b = new KButton(T, text) { Location = new Point(x, y), Size = new Size(w, T.S(26)), BackColor = T.Panel };
            b.Click += (s, e) => click();
            _body.Controls.Add(b);
            return b;
        }

        private int note(Control parent, string text, int x, int y, int w)
        {
            Label l = new Label { Text = text, Location = new Point(x, y), ForeColor = T.Faint, AutoSize = false };
            l.Size = new Size(w, TextRenderer.MeasureText(text, Font, new Size(w, 1000), TextFormatFlags.WordBreak).Height + 2);
            parent.Controls.Add(l);
            return l.Bottom;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _refresh.Dispose();
            base.Dispose(disposing);
        }
    }
}
