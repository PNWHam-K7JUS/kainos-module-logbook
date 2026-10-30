/*  SettingsForm.cs

The logbook's settings: the station (call, grid, power), the lookups (QRZ.com, HamQTH, callook.info, each with a
Test button), what counts as confirmed, QSOs from the other modules, and cty.dat.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kainos.Logbook.Lookup;

namespace Kainos.Logbook.UI
{
    internal class SettingsForm : KDialog
    {
        private readonly Book _book;
        private readonly Feeds _feeds;
        private readonly TextBox _call, _grid, _power, _qrzU, _qrzP, _hqU, _hqP;
        private readonly CheckBox _qrz, _hq, _callook, _feedsOn;
        private readonly ComboBox _confirm;
        private readonly Label _qrzTest, _hqTest, _cty;

        private static readonly string[] ConfirmKeys = { "lotw", "eqsl", "qrz", "any" };

        public SettingsForm(Book book, Feeds feeds) : base(book.Host.Theme, "Logbook settings")
        {
            _book = book;
            _feeds = feeds;
            int x = T.S(16), y = T.S(14), lw = T.S(120), w = T.S(520);

            Heading("Station", x, y); y += T.S(24);
            Func<string, int, Label> lab = (s, yy) => { Label l = new Label { Text = s, Location = new Point(x, yy + 3), Size = new Size(lw, T.S(20)), ForeColor = T.Dim }; Controls.Add(l); return l; };
            lab("Call", y); _call = Box(x + lw, y, T.S(120)); _call.CharacterCasing = CharacterCasing.Upper; _call.Text = book.MyCall;
            lab("Grid", y + T.S(28)); _grid = Box(x + lw, y + T.S(28), T.S(120)); _grid.CharacterCasing = CharacterCasing.Upper; _grid.Text = book.MyGrid;
            lab("Power (W)", y + T.S(56)); _power = Box(x + lw, y + T.S(56), T.S(60)); _power.Text = book.MyPower;
            Note("Kainos's own call and grid (Setup > DSP > FreeDV) are used unless you set them here. They go in each QSO as STATION_CALLSIGN, MY_GRIDSQUARE and TX_PWR.", x + lw + T.S(140), y, w - lw - T.S(140));
            y += T.S(92);

            Heading("Lookups", x, y); y += T.S(24);
            _qrz = Check("QRZ.com", x, y, book.Host.GetSetting("lookup.qrz", "0") == "1");
            _qrzU = Box(x + lw, y, T.S(130)); _qrzU.Text = Secrets.Get(book.Host, "qrz.user");
            _qrzP = Box(x + lw + T.S(138), y, T.S(130), true); _qrzP.Text = Secrets.Get(book.Host, "qrz.pass");
            Button("Test", x + lw + T.S(276), y - 2, T.S(60), () => test("qrz"));
            _qrzTest = new Label { Location = new Point(x + lw + T.S(342), y + 3), Size = new Size(w - lw - T.S(342), T.S(18)), ForeColor = T.Faint, AutoEllipsis = true };
            Controls.Add(_qrzTest);
            y += T.S(28);
            Note("Username and password. A QRZ XML subscription gives the full record (name, address, grid, photo); without one QRZ gives the name and country.", x + lw, y, w - lw);
            y += T.S(36);
            _hq = Check("HamQTH", x, y, book.Host.GetSetting("lookup.hamqth", "0") == "1");
            _hqU = Box(x + lw, y, T.S(130)); _hqU.Text = Secrets.Get(book.Host, "hamqth.user");
            _hqP = Box(x + lw + T.S(138), y, T.S(130), true); _hqP.Text = Secrets.Get(book.Host, "hamqth.pass");
            Button("Test", x + lw + T.S(276), y - 2, T.S(60), () => test("hamqth"));
            _hqTest = new Label { Location = new Point(x + lw + T.S(342), y + 3), Size = new Size(w - lw - T.S(342), T.S(18)), ForeColor = T.Faint, AutoEllipsis = true };
            Controls.Add(_hqTest);
            y += T.S(28);
            Note("Free, with a free account at hamqth.com. Used when QRZ isn't set up or doesn't know the call.", x + lw, y, w - lw);
            y += T.S(24);
            _callook = Check("callook.info (US calls, free, no account)", x, y, book.Host.GetSetting("lookup.callook", "1") == "1");
            y += T.S(26);
            Note("Passwords are kept encrypted for your Windows account only. Lookups ask for the call you typed and nothing else.", x, y, w);
            y += T.S(30);

            Heading("Confirmed means", x, y); y += T.S(24);
            _confirm = Combo(x, y, T.S(200), "LoTW + paper cards", "eQSL", "QRZ", "Any confirmation");
            _confirm.SelectedIndex = Math.Max(0, Array.IndexOf(ConfirmKeys, book.ConfirmSource));
            Note("LoTW + paper cards is what ARRL (DXCC, WAS) and CQ (WAZ) credit. Used for worked-before and awards.", x + T.S(210), y, w - T.S(210));
            y += T.S(36);

            Heading("Other modules", x, y); y += T.S(24);
            _feedsOn = Check("Bring in QSOs logged by the Digital Modes (FT8 ...) and SSTV modules", x, y, feeds.On);
            y += T.S(30);

            Heading("Countries", x, y); y += T.S(22);
            _cty = new Label { Location = new Point(x, y + 3), Size = new Size(w - T.S(130), T.S(18)), ForeColor = T.Dim, Text = "From " + book.Cty.Source + " (" + book.Cty.Entities.Count + " countries)" };
            Controls.Add(_cty);
            Button("Update now", x + w - T.S(120), y - 2, T.S(120), updateCty);
            y += T.S(26);
            Note("AD1C's cty.dat from country-files.com, the file contest loggers use. Updated by itself every two weeks.", x, y, w);
            y += T.S(30);

            Button("Save", x + w - T.S(200), y, T.S(96), save);
            Button("Cancel", x + w - T.S(96), y, T.S(96), () => { DialogResult = DialogResult.Cancel; Close(); });
            ClientSize = new Size(x * 2 + w, y + T.S(44));
        }

        private void storeLookups()
        {
            Secrets.Set(_book.Host, "qrz.user", _qrzU.Text.Trim());
            Secrets.Set(_book.Host, "qrz.pass", _qrzP.Text);
            Secrets.Set(_book.Host, "hamqth.user", _hqU.Text.Trim());
            Secrets.Set(_book.Host, "hamqth.pass", _hqP.Text);
            _book.Host.SetSetting("lookup.qrz", _qrz.Checked ? "1" : "0");
            _book.Host.SetSetting("lookup.hamqth", _hq.Checked ? "1" : "0");
            _book.Host.SetSetting("lookup.callook", _callook.Checked ? "1" : "0");
            _book.Lookup.Forget();
        }

        private async void test(string which)
        {
            Label l = which == "qrz" ? _qrzTest : _hqTest;
            storeLookups();
            l.ForeColor = T.Faint;
            l.Text = "Trying…";
            string own = _call.Text.Trim().Length > 0 ? _call.Text.Trim() : "W1AW";
            string r = await _book.Lookup.TestAsync(which, own);
            if (IsDisposed) return;
            l.ForeColor = r.StartsWith("Works") ? Draw.Ok : T.Tx;
            l.Text = r;
        }

        private async void updateCty()
        {
            _cty.Text = "Downloading cty.dat…";
            await _book.RefreshCtyNow();
            if (IsDisposed) return;
            _cty.Text = "From " + _book.Cty.Source + " (" + _book.Cty.Entities.Count + " countries)";
        }

        private void save()
        {
            _book.Host.SetSetting("station.call", _call.Text.Trim().ToUpperInvariant() == (_book.Host.Callsign ?? "").ToUpperInvariant() ? "" : _call.Text.Trim());
            _book.Host.SetSetting("station.grid", _grid.Text.Trim().ToUpperInvariant() == (_book.Host.Grid ?? "").ToUpperInvariant() ? "" : _grid.Text.Trim());
            _book.Host.SetSetting("station.power", _power.Text.Trim());
            storeLookups();
            _book.ConfirmSource = ConfirmKeys[Math.Max(0, _confirm.SelectedIndex)];
            _feeds.On = _feedsOn.Checked;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
