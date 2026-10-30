/*  FakeHost.cs (self-test)

A stand-in for Kainos 1.0.12: settings, the dock, a pane, and a radio whose VFO and mode the tests set.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Kainos.Modules;

namespace Kainos.Logbook.SelfTest
{
    internal class FakeHost : IKainosHost, IKainosRadio, IKainosAudio, IKainosDock
    {
        public readonly Dictionary<string, string> Settings = new Dictionary<string, string>();
        public readonly List<string> LogLines = new List<string>();
        public readonly List<FakeMenu> Menus = new List<FakeMenu>();
        public readonly List<FakePane> Panes = new List<FakePane>();
        public double Hz = 14225000;
        public string ModeName = "USB";
        public string DockLabel;

        // the data folder sits in a modules folder, like Kainos's ModuleData\logbook
        public FakeHost(string root) { DataFolder = Path.Combine(root, "logbook"); Directory.CreateDirectory(DataFolder); }

        public int ApiVersion { get { return KainosModuleApi.Version; } }
        public string KainosVersion { get { return "1.0.14"; } }
        public string ModuleId { get { return "logbook"; } }
        public string ModuleFolder { get { return DataFolder; } }
        public string DataFolder { get; private set; }
        public string Callsign { get { return "K7JUS"; } }
        public string Grid { get { return "CN87"; } }
        public IKainosRadio Radio { get { return this; } }
        public IKainosAudio Audio { get { return this; } }
        public IKainosTheme Theme { get { return FakeTheme.Instance; } }

        public IKainosMenuItem AddMenuItem(string text, string toolTip, Action click) { FakeMenu m = new FakeMenu { Text = text, Click = click }; Menus.Add(m); return m; }
        public IKainosMenuItem AddDockButton(string label, string menuText, string toolTip, Action click) { DockLabel = label; return AddMenuItem(menuText, toolTip, click); }
        public IKainosPane CreatePane(string title, Control content) { FakePane p = new FakePane(content); Panes.Add(p); return p; }
        public void SetMarkers(int rx, KainosMarker[] markers) { }
        public string GetSetting(string key, string fallback) { lock (Settings) { string v; return Settings.TryGetValue(key, out v) ? v : fallback; } }
        public void SetSetting(string key, string value) { lock (Settings) Settings[key] = value; }
        public void Log(string message) { lock (LogLines) LogLines.Add(message); }
        public void BeginInvoke(Action action) { action(); }

        public bool PowerOn { get { return true; } }
        public bool Transmitting { get { return false; } }
        public int TxReceiver { get { return 0; } }
        public double VfoHz(int rx) { return Hz; }
        public void TuneVfo(int rx, double hz) { Hz = hz; }
        public string Mode(int rx) { return ModeName; }
        public void SetMode(int rx, string mode) { ModeName = mode; }
        public void SetMox(bool on) { throw new InvalidOperationException("the logbook must never key the radio"); }

        public bool AcquireRx(int rx, Action released, out string problem) { problem = null; return true; }
        public void ReleaseRx() { }
        public int RxRate { get { return 48000; } }
        public int ReadRx(float[] dst, int max) { return 0; }
        public int TxRate { get { return 48000; } }
        public void TxStart() { }
        public int TxWrite(float[] src, int n) { return 0; }
        public int TxQueued { get { return 0; } }
        public void TxClear() { }
        public void TxStop() { }
    }

    internal class FakeTheme : IKainosTheme
    {
        public static readonly FakeTheme Instance = new FakeTheme();
        public Color Bg { get { return Color.FromArgb(0x05, 0x0b, 0x13); } }
        public Color Surface { get { return Color.FromArgb(0x0a, 0x15, 0x20); } }
        public Color Panel { get { return Color.FromArgb(0x0e, 0x1b, 0x29); } }
        public Color Raised { get { return Color.FromArgb(0x14, 0x25, 0x38); } }
        public Color Line { get { return Color.FromArgb(0x1f, 0x33, 0x46); } }
        public Color Text { get { return Color.FromArgb(0xe9, 0xee, 0xf3); } }
        public Color Dim { get { return Color.FromArgb(0x94, 0xa7, 0xb2); } }
        public Color Faint { get { return Color.FromArgb(0x5f, 0x75, 0x84); } }
        public Color Ice { get { return Color.FromArgb(0x7f, 0xb0, 0xcc); } }
        public Color Gold { get { return Color.FromArgb(0xd4, 0xad, 0x6a); } }
        public Color Violet { get { return Color.FromArgb(0xb5, 0xa8, 0xe0); } }
        public Color Tx { get { return Color.FromArgb(0xe2, 0x57, 0x4c); } }
        public Color Selected { get { return Color.FromArgb(0x1d, 0x2a, 0x33); } }
        public float Scale { get { return 1f; } }
        public int S(float designPx) { return (int)Math.Round(designPx); }
        public Font Font(float px, bool bold) { return new Font("Segoe UI", px, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel); }
        public void DrawButton(Graphics g, RectangleF r, string text, bool on, bool enabled, bool hover, float fontPx)
        {
            using (Brush b = new SolidBrush(on ? Selected : Raised)) g.FillRectangle(b, r);
            using (Pen p = new Pen(on ? Gold : Line)) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
            using (Font f = Font(fontPx, true)) TextRenderer.DrawText(g, text, f, Rectangle.Round(r), !enabled ? Faint : on ? Gold : Dim);
        }
        public void Style(Control root) { root.BackColor = Bg; root.ForeColor = Text; }
    }

    internal class FakeMenu : IKainosMenuItem
    {
        public Action Click;
        public bool Checked { get; set; }
        public bool Enabled { get; set; } = true;
        public string Text { get; set; }
        public void Remove() { }
    }

    internal class FakePane : IKainosPane
    {
        public readonly Control Content;
        public FakePane(Control content) { Content = content; }
        public bool IsOpen { get; private set; }
        public bool Popped { get; set; }
        public void Open() { IsOpen = true; }
        public void Close() { IsOpen = false; }
        public event EventHandler Closed;
        public void UserCloses() { IsOpen = false; Closed?.Invoke(this, EventArgs.Empty); }
    }
}
