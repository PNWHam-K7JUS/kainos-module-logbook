/*  EditForm.cs

Editing one QSO: the fields the log shows, plus the paper card. Other ADIF fields the QSO came with are kept as
they are.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kainos.Modules;

namespace Kainos.Logbook.UI
{
    internal class EditForm : KDialog
    {
        public Qso Result;
        private readonly Qso _q;
        private readonly Dictionary<string, TextBox> _boxes = new Dictionary<string, TextBox>();
        private readonly CheckBox _card;
        private readonly Label _problem;

        // label, ADIF field (or a special: DATE, TIME, FREQ)
        private static readonly string[][] Rows =
        {
            new[] { "Call", "CALL" }, new[] { "Date (UTC, yyyy-mm-dd)", "DATE" }, new[] { "Time (UTC, hh:mm)", "TIME" },
            new[] { "Frequency (MHz)", "FREQ" }, new[] { "Band", "BAND" }, new[] { "Mode", "MODE" }, new[] { "Submode", "SUBMODE" },
            new[] { "RST sent", "RST_SENT" }, new[] { "RST received", "RST_RCVD" }, new[] { "Name", "NAME" }, new[] { "QTH", "QTH" },
            new[] { "State / province", "STATE" }, new[] { "Grid", "GRIDSQUARE" }, new[] { "Country", "COUNTRY" }, new[] { "CQ zone", "CQZ" },
            new[] { "Their park (POTA)", "POTA_REF" }, new[] { "Comment", "COMMENT" },
        };

        public EditForm(IKainosTheme t, Qso q) : base(t, "Edit QSO · " + q.Call)
        {
            _q = q.Clone();
            int x = t.S(16), y = t.S(14), lw = t.S(160), bw = t.S(240);
            foreach (string[] r in Rows)
            {
                Controls.Add(new Label { Text = r[0], Location = new Point(x, y + 3), Size = new Size(lw, t.S(20)), ForeColor = t.Dim });
                TextBox b = Box(x + lw, y, bw);
                b.Text = valueOf(r[1]);
                if (r[1] == "CALL" || r[1] == "GRIDSQUARE" || r[1] == "STATE" || r[1] == "POTA_REF" || r[1] == "MODE" || r[1] == "SUBMODE") b.CharacterCasing = CharacterCasing.Upper;
                _boxes[r[1]] = b;
                y += t.S(26);
            }
            _card = Check("Paper QSL card received", x + lw, y + 2, q.CardReceived);
            y += t.S(30);
            _problem = new Label { Location = new Point(x, y), Size = new Size(lw + bw, t.S(18)), ForeColor = t.Tx };
            Controls.Add(_problem);
            y += t.S(22);
            Button("Save", x + lw + bw - t.S(200), y, t.S(96), save);
            Button("Cancel", x + lw + bw - t.S(96), y, t.S(96), () => { DialogResult = DialogResult.Cancel; Close(); });
            ClientSize = new Size(x * 2 + lw + bw, y + t.S(44));
        }

        private string valueOf(string f)
        {
            DateTime t = _q.TimeOn;
            switch (f)
            {
                case "DATE": return t == DateTime.MinValue ? "" : t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                case "TIME": return t == DateTime.MinValue ? "" : t.ToString("HH:mm", CultureInfo.InvariantCulture);
                case "FREQ": return _q.FreqMhz > 0 ? _q.FreqMhz.ToString("0.000###", CultureInfo.InvariantCulture) : "";
                case "POTA_REF": return _q.PotaRef;
                default: return _q.Get(f);
            }
        }

        private void save()
        {
            DateTime d;
            if (!DateTime.TryParseExact(_boxes["DATE"].Text.Trim() + " " + _boxes["TIME"].Text.Trim(), new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm", "yyyy-MM-dd HHmm" }, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out d))
            { _problem.Text = "The date or time isn't right (yyyy-mm-dd and hh:mm, UTC)"; return; }
            if (_boxes["CALL"].Text.Trim().Length < 3) { _problem.Text = "Enter the call"; return; }
            double mhz = 0;
            string fs = _boxes["FREQ"].Text.Trim();
            if (fs.Length > 0 && !double.TryParse(fs, NumberStyles.Float, CultureInfo.InvariantCulture, out mhz)) { _problem.Text = "The frequency should be in MHz, like 14.074"; return; }

            _q.TimeOn = d;
            _q.FreqMhz = mhz;
            foreach (KeyValuePair<string, TextBox> kv in _boxes)
            {
                if (kv.Key == "DATE" || kv.Key == "TIME" || kv.Key == "FREQ") continue;
                string v = kv.Value.Text.Trim();
                if (kv.Key == "POTA_REF")
                {
                    _q.Set("POTA_REF", v);
                    if (v.Length > 0) { _q.Set("SIG", "POTA"); _q.Set("SIG_INFO", v); }
                    else if (_q.Get("SIG").Equals("POTA", StringComparison.OrdinalIgnoreCase)) { _q.Set("SIG", null); _q.Set("SIG_INFO", null); }
                    continue;
                }
                _q.Set(kv.Key, v);
            }
            if (_q.Band.Length == 0 && mhz > 0) _q.Band = Bands.Of(mhz);
            _q.CardReceived = _card.Checked;
            Result = _q;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
