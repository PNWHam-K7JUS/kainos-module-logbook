/*  Controls.cs

The logbook's controls in the Kainos colours: a button, an entry box with its label, and a dark dialog base.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kainos.Modules;

namespace Kainos.Logbook.UI
{
    internal class KButton : Control
    {
        private readonly IKainosTheme _t;
        private bool _on, _hover;
        public float FontPx = 11;

        public KButton(IKainosTheme t, string text)
        {
            _t = t;
            Text = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = t.Bg;
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        public bool On { get { return _on; } set { if (_on != value) { _on = value; Invalidate(); } } }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            _t.DrawButton(e.Graphics, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Text, _on, Enabled, _hover, _t.S(FontPx));
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    }

    // A label over a text box, the box drawn flat on the navy
    internal class Field : Panel
    {
        public readonly TextBox Box;
        private readonly Label _label;
        private readonly IKainosTheme _t;

        public Field(IKainosTheme t, string label, float fontPx, bool upper, Color fore)
        {
            _t = t;
            BackColor = t.Bg;
            _label = new Label { Text = label.ToUpperInvariant(), ForeColor = t.Faint, BackColor = t.Bg, Font = t.Font(t.S(9.5f), true), AutoSize = false, TextAlign = ContentAlignment.BottomLeft };
            Box = new TextBox { BorderStyle = BorderStyle.FixedSingle, BackColor = t.Surface, ForeColor = fore, Font = t.Font(t.S(fontPx), fontPx > 15), CharacterCasing = upper ? CharacterCasing.Upper : CharacterCasing.Normal };
            Controls.Add(_label);
            Controls.Add(Box);
        }

        public override string Text { get { return Box.Text; } set { Box.Text = value; } }

        protected override void OnLayout(LayoutEventArgs e)
        {
            int lh = _t.S(14);
            _label.SetBounds(0, 0, Width, lh);
            Box.SetBounds(0, lh + 1, Width, Math.Max(10, Height - lh - 1));
            base.OnLayout(e);
        }
    }

    // A small dark window for settings, edits and exports
    internal class KDialog : Form
    {
        protected readonly IKainosTheme T;

        public KDialog(IKainosTheme t, string title)
        {
            T = t;
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = t.Panel;
            ForeColor = t.Text;
            Font = t.Font(t.S(12), false);
            AutoScaleMode = AutoScaleMode.None;
        }

        protected Label Heading(string text, int x, int y)
        {
            Label l = new Label { Text = text.ToUpperInvariant(), Location = new Point(x, y), AutoSize = true, ForeColor = T.Dim, Font = T.Font(T.S(10.5f), true) };
            Controls.Add(l);
            return l;
        }

        protected Label Note(string text, int x, int y, int w)
        {
            Label l = new Label { Text = text, Location = new Point(x, y), Size = new Size(w, 10), ForeColor = T.Faint, AutoSize = false };
            l.Height = TextRenderer.MeasureText(text, l.Font, new Size(w, 1000), TextFormatFlags.WordBreak).Height + 2;
            Controls.Add(l);
            return l;
        }

        protected TextBox Box(int x, int y, int w, bool password = false)
        {
            TextBox b = new TextBox { Location = new Point(x, y), Width = w, BorderStyle = BorderStyle.FixedSingle, BackColor = T.Surface, ForeColor = T.Text, UseSystemPasswordChar = password };
            Controls.Add(b);
            return b;
        }

        protected CheckBox Check(string text, int x, int y, bool on)
        {
            CheckBox c = new CheckBox { Text = text, Location = new Point(x, y), AutoSize = true, Checked = on, ForeColor = T.Text, BackColor = T.Panel };
            Controls.Add(c);
            return c;
        }

        protected KButton Button(string text, int x, int y, int w, Action click)
        {
            KButton b = new KButton(T, text) { Location = new Point(x, y), Size = new Size(w, T.S(28)), BackColor = T.Panel };
            b.Click += (s, e) => click();
            Controls.Add(b);
            return b;
        }

        protected ComboBox Combo(int x, int y, int w, params string[] items)
        {
            ComboBox c = new ComboBox { Location = new Point(x, y), Width = w, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = T.Surface, ForeColor = T.Text };
            c.Items.AddRange(items);
            Controls.Add(c);
            return c;
        }
    }

    internal static class Draw
    {
        public static Color Blend(Color a, Color b, float k)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * k), (int)(a.G + (b.G - a.G) * k), (int)(a.B + (b.B - a.B) * k));
        }

        public static GraphicsPath Rounded(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // a flag like "NEW ONE · Japan": gold for new, ice for information, red for a dupe, green for confirmed
        public static int Chip(Graphics g, IKainosTheme t, Font f, string text, Color c, int x, int y)
        {
            Size s = TextRenderer.MeasureText(g, text, f, Size.Empty, TextFormatFlags.NoPadding);
            Rectangle r = new Rectangle(x, y, s.Width + t.S(12), s.Height + t.S(6));
            using (Brush b = new SolidBrush(Blend(t.Bg, c, 0.12f))) g.FillRectangle(b, r);
            using (Pen p = new Pen(c)) g.DrawRectangle(p, r);
            TextRenderer.DrawText(g, text, f, new Point(x + t.S(6), y + t.S(3)), c, TextFormatFlags.NoPadding);
            return r.Width;
        }

        public static readonly Color Ok = Color.FromArgb(0x7f, 0xc7, 0x9a);
    }
}
