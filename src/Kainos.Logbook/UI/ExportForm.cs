/*  ExportForm.cs

Export: a format, which QSOs (dates, band, mode), then where to save it, and what the file is for.

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

namespace Kainos.Logbook.UI
{
    internal class ExportForm : KDialog
    {
        private readonly Book _book;
        private readonly ComboBox _fmt, _band, _mode;
        private readonly TextBox _from, _to;
        private readonly Label _info;

        private static readonly Dictionary<string, string> What = new Dictionary<string, string>
        {
            { "adif", "For LoTW (sign and upload it in TQSL), eQSL (Upload ADIF), QRZ Logbook (Import), Club Log (Upload log), and any other logging program." },
            { "adx", "The same fields as ADIF, as XML, for programs that prefer it." },
            { "csv", "Opens in Excel or Google Sheets." },
        };

        public ExportForm(Book book) : base(book.Host.Theme, "Export the log")
        {
            _book = book;
            int x = T.S(16), y = T.S(16), lw = T.S(110), w = T.S(250);
            Func<string, Label> lab = s => { Label l = new Label { Text = s, Location = new Point(x, y + 3), Size = new Size(lw, T.S(20)), ForeColor = T.Dim }; Controls.Add(l); return l; };
            lab("Format");
            _fmt = Combo(x + lw, y, w, Export.Formats.Select(f => f[1]).ToArray());
            _fmt.SelectedIndex = Math.Max(0, Array.FindIndex(Export.Formats, f => f[0] == book.Host.GetSetting("export.format", "adif")));
            y += T.S(30);
            lab("From (UTC date)");
            _from = Box(x + lw, y, T.S(110));
            Controls.Add(new Label { Text = "yyyy-mm-dd, or empty for all", Location = new Point(x + lw + T.S(118), y + 3), AutoSize = true, ForeColor = T.Faint });
            y += T.S(28);
            lab("To (UTC date)");
            _to = Box(x + lw, y, T.S(110));
            y += T.S(30);
            lab("Band");
            _band = Combo(x + lw, y, T.S(110), new[] { "All" }.Concat(Bands.Names).ToArray());
            _band.SelectedIndex = 0;
            y += T.S(30);
            lab("Mode");
            _mode = Combo(x + lw, y, T.S(110), "All", "CW", "Phone", "Digital");
            _mode.SelectedIndex = 0;
            y += T.S(34);
            _info = new Label { Location = new Point(x, y), Size = new Size(lw + w + T.S(60), T.S(54)), ForeColor = T.Dim };
            Controls.Add(_info);
            y += T.S(58);
            Button("Save…", x + lw + w + T.S(60) - T.S(200), y, T.S(96), save);
            Button("Close", x + lw + w + T.S(60) - T.S(96), y, T.S(96), Close);
            ClientSize = new Size(x * 2 + lw + w + T.S(60), y + T.S(44));
            EventHandler upd = (s, e) => update();
            _fmt.SelectedIndexChanged += upd; _band.SelectedIndexChanged += upd; _mode.SelectedIndexChanged += upd;
            _from.TextChanged += upd; _to.TextChanged += upd;
            update();
        }

        private ExportFilter filter(out string problem)
        {
            problem = null;
            ExportFilter f = new ExportFilter();
            DateTime d;
            if (_from.Text.Trim().Length > 0) { if (DateTime.TryParseExact(_from.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) f.From = d; else problem = "The From date should look like 2026-10-30"; }
            if (_to.Text.Trim().Length > 0) { if (DateTime.TryParseExact(_to.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) f.To = d; else problem = "The To date should look like 2026-10-31"; }
            f.Band = _band.SelectedIndex > 0 ? (string)_band.SelectedItem : "";
            if (_mode.SelectedIndex > 0) f.Class = (ModeClass)(_mode.SelectedIndex - 1);
            return f;
        }

        private string[] format { get { return Export.Formats[Math.Max(0, _fmt.SelectedIndex)]; } }

        private void update()
        {
            string problem;
            ExportFilter f = filter(out problem);
            int n = f.Apply(_book.Store.All()).Count();
            _info.ForeColor = problem != null ? T.Tx : T.Dim;
            _info.Text = problem ?? (n + " QSO" + (n == 1 ? "" : "s") + ". " + What[format[0]]);
        }

        private void save()
        {
            string problem;
            ExportFilter f = filter(out problem);
            if (problem != null) return;
            List<Qso> list = f.Apply(_book.Store.All()).ToList();
            string ext = format[2];
            using (SaveFileDialog d = new SaveFileDialog { Filter = format[3], FileName = _book.MyCall + "-" + DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "." + ext, Title = "Save the log as " + format[1] })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(d.FileName, Export.Make(format[0], list, _book.Version), new UTF8Encoding(false));
                _book.Host.SetSetting("export.format", format[0]);
                _info.ForeColor = Draw.Ok;
                _info.Text = "Saved " + list.Count + " QSOs to " + Path.GetFileName(d.FileName);
            }
        }
    }
}
