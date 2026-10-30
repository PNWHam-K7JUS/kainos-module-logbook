/*  Messages.cs

Sending the contest messages (F1 CQ, F2 their call and your exchange, F3 TU) when you press Enter or F1 - F3 on the
CONTEST page, as contest loggers do. In CW they go through Kainos's keyer (CWX, as the CW terminal sends), with
{MYCALL}, {CALL} and {EXCH} filled in; on phone the Voice Keyer module sends a slot (1, 2, 3 unless changed). Not in
the digital modes. Off until switched on (the CONTEST page's Messages window); it needs Kainos 1.0.15
(IKainosKeyer), and the Voice Keyer module 0.3 for phone.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Globalization;
using Kainos.Modules;

namespace Kainos.Logbook.Contest
{
    internal sealed class Messages
    {
        private readonly Contesting _c;
        private readonly IKainosHost _h;
        public DateTime SendingSince = DateTime.MinValue;   // something was started (Esc stops it first)

        public Messages(Contesting c) { _c = c; _h = c.Book.Host; }

        public IKainosKeyer Keyer { get { return _h as IKainosKeyer; } }
        public IKainosTheme Theme { get { return _h.Theme; } }

        public bool On { get { return _h.GetSetting("esm.on", "0") == "1"; } set { _h.SetSetting("esm.on", value ? "1" : "0"); } }

        public static readonly string[] CwDefaults = { "CQ TEST {MYCALL} {MYCALL} TEST", "{CALL} {EXCH}", "TU {MYCALL}" };

        public string Cw(int f) { return _h.GetSetting("esm.cw" + f, CwDefaults[f - 1]); }
        public void SetCw(int f, string text) { _h.SetSetting("esm.cw" + f, text ?? ""); }
        public int Slot(int f) { int n; return int.TryParse(_h.GetSetting("esm.slot" + f, f.ToString(CultureInfo.InvariantCulture)), out n) && n >= 1 && n <= 12 ? n : f; }
        public void SetSlot(int f, int n) { _h.SetSetting("esm.slot" + f, n.ToString(CultureInfo.InvariantCulture)); }

        public string Expand(string text, string call, int serial)
        {
            return (text ?? "").Replace("{MYCALL}", _c.Book.MyCall).Replace("{CALL}", (call ?? "").ToUpperInvariant()).Replace("{EXCH}", _c.SentText(serial)).Trim();
        }

        // F1 - F3: returns what to show (what was sent, or why not)
        public string Send(int f, string call, int serial)
        {
            if (f < 1 || f > 3) return null;
            IKainosKeyer k = Keyer;
            if (k == null) return "Sending messages needs Kainos 1.0.15 or later";
            IKainosRadio r = _h.Radio;
            string mode = Contesting.ModeOf(r.Mode(r.TxReceiver));
            string name = f == 1 ? "CQ" : f == 2 ? "exchange" : "TU";
            if (mode == "CW")
            {
                string text = Expand(Cw(f), call, serial);
                if (text.Length == 0) return "F" + f + " has no CW message (Messages…)";
                string problem = k.SendCw(text);
                if (problem != null) return problem;
                SendingSince = DateTime.UtcNow;
                return "F" + f + " " + name + ": " + text;
            }
            if (mode == "PH")
            {
                int slot = Slot(f);
                if (k.Publish("dvk.send", slot.ToString(CultureInfo.InvariantCulture)) == 0) return "Phone messages need the Voice Keyer module (0.3 or later)";
                SendingSince = DateTime.UtcNow;
                return "F" + f + " " + name + ": voice keyer slot F" + slot;
            }
            return "Messages aren't sent in the digital modes";
        }

        // Esc: stop what's going out; true if there was something to stop
        public bool Stop()
        {
            IKainosKeyer k = Keyer;
            if (k == null) return false;
            // CW still going, or a voice message started in the last 30 s (the keyer's messages are short)
            if (!k.CwSending && (DateTime.UtcNow - SendingSince).TotalSeconds > 30) return false;
            SendingSince = DateTime.MinValue;
            k.StopCw();
            k.Publish("dvk.stop", "");
            return true;
        }
    }
}
