/*  Program.cs (CompatCheck)

Starts the Logbook and Voice Keyer modules against Kainos 1.0.14's module API (api-1.0.14\Kainos.ModuleApi.dll,
without IKainosKeyer), to check they still load and run on a Kainos older than 1.0.15: a method that names a type
the API doesn't have can't be compiled there (TypeLoadException). A stand-in host; nothing is keyed (SetMox throws).
Build the two modules first, then: MSBuild CompatCheck.csproj, bin\CompatCheck.exe (exit code 0: both passed).

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Kainos.Modules;

class Host : IKainosHost, IKainosRadio, IKainosAudio, IKainosVoice, IKainosDock, IKainosTheme
{
    public readonly Dictionary<string, string> Set = new Dictionary<string, string>();
    public readonly List<Action> Clicks = new List<Action>();
    public readonly List<Control> Panes = new List<Control>();
    public string Id;
    public Host(string id, string root) { Id = id; DataFolder = Path.Combine(root, id); Directory.CreateDirectory(DataFolder); }
    public int ApiVersion { get { return KainosModuleApi.Version; } }
    public string KainosVersion { get { return "1.0.14"; } }
    public string ModuleId { get { return Id; } }
    public string ModuleFolder { get { return DataFolder; } }
    public string DataFolder { get; private set; }
    public string Callsign { get { return "K7JUS"; } }
    public string Grid { get { return "CN87"; } }
    public IKainosRadio Radio { get { return this; } }
    public IKainosAudio Audio { get { return this; } }
    public IKainosTheme Theme { get { return this; } }
    class Item : IKainosMenuItem { public bool Checked { get; set; } public bool Enabled { get; set; } public string Text { get; set; } public void Remove() { } }
    class Pane : IKainosPane { public bool IsOpen { get; private set; } public bool Popped { get; set; } public void Open() { IsOpen = true; } public void Close() { IsOpen = false; } public event EventHandler Closed; }
    public IKainosMenuItem AddMenuItem(string text, string tip, Action click) { Clicks.Add(click); return new Item { Text = text, Enabled = true }; }
    public IKainosMenuItem AddDockButton(string l, string m, string tip, Action click) { return AddMenuItem(m, tip, click); }
    public IKainosPane CreatePane(string title, Control content) { Panes.Add(content); return new Pane(); }
    public void SetMarkers(int rx, KainosMarker[] m) { }
    public string GetSetting(string k, string f) { string v; return Set.TryGetValue(k, out v) ? v : f; }
    public void SetSetting(string k, string v) { Set[k] = v; }
    public void Log(string m) { Console.WriteLine("  [" + Id + "] " + m); }
    public void BeginInvoke(Action a) { a(); }
    public bool PowerOn { get { return true; } }
    public bool Transmitting { get { return false; } }
    public int TxReceiver { get { return 0; } }
    public double VfoHz(int rx) { return 14025000; }
    public void TuneVfo(int rx, double hz) { }
    public string Mode(int rx) { return "CWU"; }
    public void SetMode(int rx, string m) { }
    public void SetMox(bool on) { throw new InvalidOperationException("never key in this check"); }
    public bool AcquireRx(int rx, Action r, out string p) { p = null; return true; }
    public void ReleaseRx() { }
    public int RxRate { get { return 48000; } }
    public int ReadRx(float[] d, int m) { return 0; }
    public int TxRate { get { return 48000; } }
    public void TxStart() { } public int TxWrite(float[] s, int n) { return 0; } public int TxQueued { get { return 0; } } public void TxClear() { } public void TxStop() { }
    public void MicStart() { } public void MicStop() { } public int MicRate { get { return 48000; } } public int ReadMic(float[] d, int m) { return 0; }
    public int PreviewRate { get { return 48000; } } public void PreviewStart() { } public int PreviewWrite(float[] s, int n) { return 0; } public int PreviewQueued { get { return 0; } } public void PreviewClear() { } public void PreviewStop() { }
    public void SetKeyHandler(Func<Keys, bool> h) { }
    public Color Bg { get { return Color.Black; } } public Color Surface { get { return Color.Black; } } public Color Panel { get { return Color.Black; } } public Color Raised { get { return Color.DimGray; } }
    public Color Line { get { return Color.Gray; } } public Color Text { get { return Color.White; } } public Color Dim { get { return Color.Silver; } } public Color Faint { get { return Color.Gray; } }
    public Color Ice { get { return Color.LightBlue; } } public Color Gold { get { return Color.Gold; } } public Color Violet { get { return Color.Violet; } } public Color Tx { get { return Color.Red; } } public Color Selected { get { return Color.DarkSlateGray; } }
    public float Scale { get { return 1; } } public int S(float px) { return (int)Math.Round(px); }
    public Font Font(float px, bool bold) { return new Font("Segoe UI", px, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel); }
    public void DrawButton(Graphics g, RectangleF r, string t, bool on, bool en, bool hover, float px) { }
    public void Style(Control c) { }
}

static class Program
{
    [STAThread]
    static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "kainos-compat-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        int fails = 0;
        foreach (string which in new[] { "logbook", "dvk" })
        {
            Host h = new Host(which, root);
            h.Set["cty.auto"] = "0";
            h.Set["lookup.callook"] = "0";
            try
            {
                IKainosModule m = which == "logbook" ? (IKainosModule)new Kainos.Logbook.LogbookModule() : new Kainos.Dvk.DvkModule();
                m.Start(h);
                h.Clicks[0]();          // open it (the logbook builds its CONTEST page, which asks for the keyer)
                using (Form f = new Form { Width = 1200, Height = 400, Location = new Point(-4000, -4000), StartPosition = FormStartPosition.Manual, ShowInTaskbar = false })
                {
                    h.Panes[0].Dock = DockStyle.Fill;
                    f.Controls.Add(h.Panes[0]);
                    f.Show();
                    Application.DoEvents();
                    f.Controls.Remove(h.Panes[0]);
                }
                m.Stop();
                m.Dispose();
                Console.WriteLine("PASS  " + which + " starts, opens and stops on Kainos 1.0.14's API");
            }
            catch (Exception ex) { fails++; Console.WriteLine("FAIL  " + which + ": " + ex); }
        }
        try { Directory.Delete(root, true); } catch { }
        return fails;
    }
}
