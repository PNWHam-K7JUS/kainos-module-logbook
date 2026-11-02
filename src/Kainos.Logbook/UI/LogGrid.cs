/*  LogGrid.cs

The log as a table, newest first: time, call, band, mode, reports, country, name / park / note, and the QSL
badges (L LoTW, E eQSL, Q QRZ, C Club Log: dim not sent, ice sent, gold confirmed; P a paper card, click it when
one arrives). Virtual, so a log of tens of thousands of QSOs scrolls at once. Double-click edits; right-click for
edit, look up, card received and delete. Filter shows only the QSOs a search matches.

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
    internal class LogGrid : DataGridView
    {
        private readonly IKainosTheme _t;
        private List<Qso> _rows = new List<Qso>(), _all = new List<Qso>();
        private string[] _terms = new string[0];
        private const int QslCol = 8;

        public event Action<Qso> EditWanted, DeleteWanted, LookupWanted, CardToggled, SameCallWanted;

        public LogGrid(IKainosTheme t)
        {
            _t = t;
            VirtualMode = true;
            ReadOnly = true;
            AllowUserToAddRows = AllowUserToDeleteRows = AllowUserToResizeRows = false;
            RowHeadersVisible = false;
            SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            MultiSelect = false;
            BorderStyle = BorderStyle.None;
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            EnableHeadersVisualStyles = false;
            BackgroundColor = t.Panel;
            GridColor = t.Line;
            DoubleBuffered = true;
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            ColumnHeadersHeight = t.S(24);
            RowTemplate.Height = t.S(22);
            Font mono = new Font("Consolas", t.S(12), FontStyle.Regular, GraphicsUnit.Pixel);
            DefaultCellStyle = new DataGridViewCellStyle { BackColor = t.Panel, ForeColor = t.Text, SelectionBackColor = Draw.Blend(t.Panel, t.Ice, 0.18f), SelectionForeColor = t.Text, Font = mono, Padding = new Padding(4, 0, 4, 0) };
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = t.Surface, ForeColor = t.Faint, Font = t.Font(t.S(10), true), SelectionBackColor = t.Surface, SelectionForeColor = t.Faint, Padding = new Padding(4, 0, 4, 0) };

            string[] heads = { "UTC", "CALL", "BAND", "MODE", "SENT", "RCVD", "COUNTRY", "NAME · PARK · NOTE", "QSL" };
            int[] widths = { 132, 96, 50, 56, 46, 46, 130, 200, 112 };
            for (int i = 0; i < heads.Length; i++)
            {
                DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn { HeaderText = heads[i], Width = t.S(widths[i]), SortMode = DataGridViewColumnSortMode.NotSortable };
                if (i == 7) c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                if (i == 1) c.DefaultCellStyle = new DataGridViewCellStyle { ForeColor = t.Gold, Font = new Font("Consolas", t.S(12), FontStyle.Bold, GraphicsUnit.Pixel) };
                if (i == 6 || i == 7) c.DefaultCellStyle = new DataGridViewCellStyle { ForeColor = t.Dim, Font = t.Font(t.S(12), false) };
                Columns.Add(c);
            }

            ContextMenuStrip menu = new ContextMenuStrip { BackColor = t.Raised, ForeColor = t.Text, ShowImageMargin = false };
            menu.Items.Add("Edit…", null, (s, e) => { Qso q = Current; if (q != null) EditWanted?.Invoke(q); });
            menu.Items.Add("Look up this call", null, (s, e) => { Qso q = Current; if (q != null) LookupWanted?.Invoke(q); });
            menu.Items.Add("Every QSO with this call", null, (s, e) => { Qso q = Current; if (q != null) SameCallWanted?.Invoke(q); });
            ToolStripMenuItem card = new ToolStripMenuItem("Paper card received");
            card.Click += (s, e) => { Qso q = Current; if (q != null) CardToggled?.Invoke(q); };
            menu.Items.Add(card);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Delete…", null, (s, e) => { Qso q = Current; if (q != null) DeleteWanted?.Invoke(q); });
            menu.Opening += (s, e) => { Qso q = Current; card.Checked = q != null && q.CardReceived; e.Cancel = q == null; };
            ContextMenuStrip = menu;
        }

        public Qso Current { get { int i = CurrentCell != null ? CurrentCell.RowIndex : -1; return i >= 0 && i < _rows.Count ? _rows[i] : null; } }

        public int Total { get { return _all.Count; } }
        public int Shown { get { return _rows.Count; } }
        public bool Filtered { get { return _terms.Length > 0; } }

        public void SetRows(List<Qso> oldestFirst)
        {
            _all = Enumerable.Reverse(oldestFirst).ToList();
            show();
        }

        // what's typed in the search box: each word must match the QSO somewhere (see Matches)
        public string Filter
        {
            set
            {
                _terms = (value ?? "").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.ToUpperInvariant()).ToArray();
                show();
            }
        }

        private void show()
        {
            string keep = Current != null ? Current.Id : null;
            _rows = _terms.Length == 0 ? _all : _all.Where(q => Matches(q, _terms)).ToList();
            RowCount = 0;
            RowCount = _rows.Count;
            if (keep != null)
            {
                int i = _rows.FindIndex(q => q.Id == keep);
                if (i >= 0 && i < RowCount) CurrentCell = Rows[i].Cells[1];
            }
            Invalidate();
        }

        // A word matches when it's in the call (so "K7J" or "/P" finds those), the name, QTH, country, state,
        // county, grid, either park, the comment or the notes; or it is the band ("20M", "20"), the mode ("FT8",
        // "SSB"), or the start of the date ("2026", "2026-10", "2026-10-31"). Words are uppercase.
        public static bool Matches(Qso q, string[] terms)
        {
            string date = q.TimeOn == DateTime.MinValue ? "" : q.TimeOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string band = q.Band.ToUpperInvariant();
            string[] text = { q.Call, q.Name, q.Get("QTH"), q.Country, q.State, q.Get("CNTY"), q.Grid, q.PotaRef, q.Get("MY_POTA_REF"), q.Comment, q.Get("NOTES") };
            foreach (string t in terms)
            {
                bool hit = date.StartsWith(t, StringComparison.Ordinal) || band == t || band == t + "M"
                    || q.Mode.Equals(t, StringComparison.OrdinalIgnoreCase) || q.Submode.Equals(t, StringComparison.OrdinalIgnoreCase);
                for (int i = 0; !hit && i < text.Length; i++)
                    hit = text[i].Length > 0 && text[i].IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!hit) return false;
            }
            return true;
        }

        protected override void OnCellValueNeeded(DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;
            Qso q = _rows[e.RowIndex];
            switch (e.ColumnIndex)
            {
                case 0: DateTime t = q.TimeOn; e.Value = t == DateTime.MinValue ? "" : t.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture); break;
                case 1: e.Value = q.Call; break;
                case 2: e.Value = q.Band; break;
                case 3: e.Value = q.ModeShown; break;
                case 4: e.Value = q.RstSent; break;
                case 5: e.Value = q.RstRcvd; break;
                case 6: e.Value = q.Country + (q.State.Length > 0 ? " · " + q.State : ""); break;
                case 7:
                    List<string> p = new List<string>();
                    if (q.Name.Length > 0) p.Add(q.Name);
                    if (q.PotaRef.Length > 0) p.Add("park " + q.PotaRef);
                    if (q.Get("MY_POTA_REF").Length > 0) p.Add("from " + q.Get("MY_POTA_REF"));
                    if (q.Comment.Length > 0) p.Add(q.Comment);
                    e.Value = string.Join(" · ", p);
                    break;
                default: e.Value = ""; break;
            }
            base.OnCellValueNeeded(e);
        }

        // the QSL badges
        protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != QslCol || e.RowIndex >= _rows.Count) { base.OnCellPainting(e); return; }
            e.PaintBackground(e.CellBounds, (e.State & DataGridViewElementStates.Selected) != 0);
            Qso q = _rows[e.RowIndex];
            char[] st = { q.Lotw, q.Eqsl, q.QrzLog, q.ClubLog, q.CardReceived ? 'c' : '\0' };
            string[] lab = { "L", "E", "Q", "C", "P" };
            int s = _t.S(16), gap = _t.S(3);
            int x = e.CellBounds.X + _t.S(4), y = e.CellBounds.Y + (e.CellBounds.Height - s) / 2;
            using (Font f = _t.Font(_t.S(9), true))
                for (int i = 0; i < 5; i++)
                {
                    Rectangle r = new Rectangle(x + i * (s + gap), y, s, s);
                    Color edge = st[i] == 'c' ? _t.Gold : st[i] == 'u' ? _t.Ice : _t.Line;
                    Color fore = st[i] == 'c' ? Draw.Blend(_t.Gold, Color.White, 0.3f) : st[i] == 'u' ? _t.Ice : _t.Faint;
                    if (st[i] == 'c') using (Brush b = new SolidBrush(Draw.Blend(_t.Panel, _t.Gold, 0.3f))) e.Graphics.FillRectangle(b, r);
                    using (Pen p = new Pen(edge)) e.Graphics.DrawRectangle(p, r);
                    TextRenderer.DrawText(e.Graphics, lab[i], f, r, fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            e.Handled = true;
        }

        public static string BadgeTip(int i, Qso q)
        {
            string[] names = { "LoTW", "eQSL", "QRZ Logbook", "Club Log", "Paper card" };
            char[] st = { q.Lotw, q.Eqsl, q.QrzLog, q.ClubLog, q.CardReceived ? 'c' : '\0' };
            if (i == 4) return q.CardReceived ? "Paper card received (click to untick)" : "Paper card: click when it arrives";
            return names[i] + ": " + (st[i] == 'c' ? "confirmed" : st[i] == 'u' ? "sent" : "not sent");
        }

        // a click on the P badge ticks or unticks the paper card
        protected override void OnCellMouseClick(DataGridViewCellMouseEventArgs e)
        {
            base.OnCellMouseClick(e);
            if (e.Button != MouseButtons.Left || e.RowIndex < 0 || e.ColumnIndex != QslCol || e.RowIndex >= _rows.Count) return;
            int s = _t.S(16), gap = _t.S(3), x0 = _t.S(4) + 4 * (s + gap);
            if (e.X >= x0 && e.X <= x0 + s) CardToggled?.Invoke(_rows[e.RowIndex]);
        }

        protected override void OnCellToolTipTextNeeded(DataGridViewCellToolTipTextNeededEventArgs e)
        {
            base.OnCellToolTipTextNeeded(e);
            if (e.RowIndex < 0 || e.ColumnIndex != QslCol || e.RowIndex >= _rows.Count) return;
            Qso q = _rows[e.RowIndex];
            e.ToolTipText = string.Join("\n", Enumerable.Range(0, 5).Select(i => BadgeTip(i, q)));
        }

        protected override void OnCellDoubleClick(DataGridViewCellEventArgs e)
        {
            base.OnCellDoubleClick(e);
            if (e.RowIndex >= 0 && e.RowIndex < _rows.Count && e.ColumnIndex != QslCol) EditWanted?.Invoke(_rows[e.RowIndex]);
        }

        protected override void OnCellMouseDown(DataGridViewCellMouseEventArgs e)
        {
            // a right click picks the row under the mouse first
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.RowIndex < RowCount) CurrentCell = Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
            base.OnCellMouseDown(e);
        }
    }
}
