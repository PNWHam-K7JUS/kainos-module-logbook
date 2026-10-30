/*  MessageTests.cs (self-test)

Contest messages (Enter and F1 - F3 on the CONTEST page) against a stand-in keyer that only records them: nothing
while they're off; in CW, CQ on an empty Enter, their call and your exchange once per call, TU as it logs, F-keys,
Esc stopping; on phone, the Voice Keyer's slots on the message bus; nothing in the digital modes; and a Kainos
without the keyer.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kainos.Logbook.Contest;
using Kainos.Logbook.UI;

namespace Kainos.Logbook.SelfTest
{
    internal static class MessageTests
    {
        private static void press(TextBox b, Keys k)
        {
            typeof(Control).GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(b, new object[] { new KeyEventArgs(k) });
            Application.DoEvents();
        }

        public static void Run(string root, Action<bool, string> check)
        {
            FakeHost h = new FakeHost(Path.Combine(root, "esm"));
            Book book = new Book(h, "0.6.0") { Cty = Cty.Parse(BuiltIn.Text) };
            book.Store.Load();
            Contesting c = new Contesting(book);
            c.Current = c.Defs.First(d => d.Id == "cqwwcw");
            h.Hz = 14025000; h.ModeName = "CWU";
            List<string> said = new List<string>();
            using (Form f = new Form { Width = 1200, Height = 380, StartPosition = FormStartPosition.Manual, Location = new Point(-4000, -4000), ShowInTaskbar = false })
            {
                ContestPage page = new ContestPage(c) { Dock = DockStyle.Fill };
                page.Status += s => said.Add(s ?? "");
                f.Controls.Add(page);
                f.Show();
                Application.DoEvents();
                List<Kainos.Logbook.UI.Field> fl = Program.AllControls(page).OfType<Kainos.Logbook.UI.Field>().ToList();
                TextBox call = fl[0].Box, rst = fl[1].Box, zone = fl[2].Box;

                press(call, Keys.Enter);
                check(h.CwSent.Count == 0 && h.Published.Count == 0, "messages off: Enter sends nothing");

                new Messages(c).On = true;
                press(call, Keys.Enter);
                check(h.CwSent.LastOrDefault() == "CQ TEST K7JUS K7JUS TEST", "CW: Enter with no call sends CQ (F1)");
                call.Text = "DL1ABC";
                Application.DoEvents();
                press(call, Keys.Enter);
                check(h.CwSent.Skip(1).SequenceEqual(new[] { "DL1ABC 599 3" }) && book.Store.Count == 0, "CW: Enter on a call sends their call and your exchange, and waits for theirs (even with it filled in)");
                press(zone, Keys.Enter);
                check(h.CwSent.Skip(1).SequenceEqual(new[] { "DL1ABC 599 3", "TU K7JUS" }) && book.Store.Count == 1, "CW: the next Enter sends TU and logs");
                h.CwSent.Clear();
                call.Text = "JA1ABC";
                Application.DoEvents();
                zone.Text = "";
                press(call, Keys.Enter);
                check(h.CwSent.SequenceEqual(new[] { "JA1ABC 599 3" }) && book.Store.Count == 1, "CW: the exchange missing: F2 sent, not logged");
                press(zone, Keys.Enter);
                check(h.CwSent.Count == 1, "CW: Enter again: F2 isn't sent twice");
                zone.Text = "25";
                press(zone, Keys.Enter);
                check(h.CwSent.Last() == "TU K7JUS" && h.CwSent.Count == 2 && book.Store.Count == 2, "CW: filled in, Enter sends TU and logs");
                h.CwSent.Clear();
                press(call, Keys.F2);
                check(h.CwSent.SequenceEqual(new[] { " 599 3".Trim() }), "CW: F2 with no call sends the exchange alone");
                h.CwBusy = true;
                press(call, Keys.Escape);
                check(h.CwStops == 1 && h.Published.Contains("dvk.stop "), "Esc while sending: stops CW and the voice keyer");

                h.Hz = 14250000; h.ModeName = "USB";
                c.Current = c.Defs.First(d => d.Id == "cqwwssb");
                h.Published.Clear();
                press(call, Keys.Enter);
                check(h.Published.SequenceEqual(new[] { "dvk.send 1" }) && h.CwSent.Count == 1, "phone: Enter with no call sends Voice Keyer slot F1");
                h.Listeners = 0;
                said.Clear();
                press(call, Keys.F3);
                check(said.Any(s => s.Contains("Voice Keyer module")), "phone: with no Voice Keyer listening, it says so");
                h.Listeners = 1;

                h.Hz = 14074000; h.ModeName = "DIGU";
                h.Published.Clear(); said.Clear();
                press(call, Keys.F1);
                check(h.Published.Count == 0 && said.Any(s => s.Contains("digital")), "digital: nothing sent, and it says why");
                f.Controls.Remove(page);
            }
        }
    }
}
