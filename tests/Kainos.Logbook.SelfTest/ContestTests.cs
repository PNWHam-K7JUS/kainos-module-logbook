/*  ContestTests.cs (self-test)

Contests: the 37 definitions, exchange checking, WPX prefixes, the exchange in a QSO's ADIF fields, dupes, and the
claimed score worked by hand for CQ WW (points by continent and country, zone and country mults per band),
Sweepstakes (once, sections, DX no points) and Field Day (CW 2, phone 1, x2); the rate, the mults still to work,
Cabrillo and EDI.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kainos.Logbook.Contest;

namespace Kainos.Logbook.SelfTest
{
    internal static class ContestTests
    {
        private static Qso add(Contesting c, FakeHost h, string call, double mhz, string mode, string exch, ref DateTime t)
        {
            h.Hz = mhz * 1e6; h.ModeName = mode;
            string[] v = exch.Split(' ');
            Dictionary<string, string> rcvd = new Dictionary<string, string>();
            for (int i = 0; i < c.Current.Exch.Length && i < v.Length; i++) rcvd[c.Current.Exch[i]] = v[i];
            Qso q = c.Make(call, rcvd, c.SessionQsos().Count + 1);
            q.TimeOn = t;
            t = t.AddMinutes(3);
            c.Book.Store.Add(q);
            return q;
        }

        public static void Run(string root, Action<bool, string> check)
        {
            List<ContestDef> defs = ContestDef.All();
            check(defs.Count == 37 && defs.All(d => d.Cabrillo.Length > 0 && d.Exch.Length > 0 && d.Modes.Length > 0), "contests: 37, each with a Cabrillo name, an exchange and modes");
            check(defs.SelectMany(d => d.Exch).All(k => Field.Label(k) != k), "contests: every exchange field has a label");
            check(Field.Valid("rst", "599") && Field.Valid("rst", "59") && Field.Valid("rst", "-12") && !Field.Valid("rst", "699"), "exchange: RST 599 / 59 / -12, not 699");
            check(Field.Valid("zone", "14") && !Field.Valid("zone", "41") && Field.Valid("sec", "WWA") && !Field.Valid("sec", "XYZ") && Field.Valid("state", "WA") && Field.Valid("state", "DX") && Field.Valid("cls", "2A") && !Field.Valid("cls", "A2") && Field.Valid("grid", "CN87") && Field.Valid("prec", "A"), "exchange: zones, sections, states, classes, grids, precedence");
            check(Contesting.WpxPrefix("K7JUS") == "K7" && Contesting.WpxPrefix("DL1ABC") == "DL1" && Contesting.WpxPrefix("VP2E/K7JUS") == "VP2" && Contesting.WpxPrefix("K7JUS/4") == "K4" && Contesting.WpxPrefix("2E0ABC") == "2E0" && Contesting.WpxPrefix("RW9AX") == "RW9", "WPX prefixes: K7, DL1, VP2, K4, 2E0, RW9");

            FakeHost h = new FakeHost(Path.Combine(root, "contest"));
            Book book = new Book(h, "0.4.0") { Cty = Cty.Parse(BuiltIn.Text) };
            book.Store.Load();
            Contesting c = new Contesting(book);
            check(c.Mine.Get("zone") == "3" && c.Mine.Get("grid") == "CN87", "my exchange: zone 3 and grid CN87 from my call and grid");
            DateTime t = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

            // CQ WW SSB: K7JUS (NA)
            c.Current = defs.First(d => d.Id == "cqwwssb");
            c.NewSession();
            Dictionary<string, string> g = c.Guess("DL1ABC", "PH");
            check(g["rst"] == "59" && g["zone"] == "14", "guess: 59 and zone 14 for DL1ABC on SSB");
            Qso dl = add(c, h, "DL1ABC", 14.250, "USB", "59 14", ref t);
            check(dl.Get("CONTEST_ID") == "CQ-WW-SSB" && dl.Get("SRX_STRING") == "59 14" && dl.Get("STX_STRING") == "59 3" && dl.CqZone == 14 && dl.RstRcvd == "59", "QSO: CONTEST_ID, exchange sent and received, CQZ and RST in their ADIF fields");
            add(c, h, "JA1ABC", 14.260, "USB", "59 25", ref t);
            add(c, h, "W1AW", 14.270, "USB", "59 5", ref t);
            add(c, h, "VE3ABC", 14.280, "USB", "59 4", ref t);
            check(c.Dupe(c.SessionQsos(), "DL1ABC", "20m", "PH") != null && c.Dupe(c.SessionQsos(), "DL1ABC", "40m", "PH") == null, "dupe: DL1ABC again on 20 m is a dupe, on 40 m isn't");
            add(c, h, "DL1ABC", 14.290, "USB", "59 14", ref t);       // a dupe, logged anyway
            add(c, h, "DL1ABC", 7.150, "LSB", "59 14", ref t);
            Score s = c.Compute(c.SessionQsos());
            check(s.Qsos == 5 && s.Points == 11 && s.Mults == 10 && s.Total == 110, "CQ WW: 5 QSOs (the dupe not counted), 3+3+0+2+3 = 11 points, 10 mults, 110 (" + s.Qsos + ", " + s.Points + ", " + s.Mults + ", " + s.Total + ")");
            string what;
            List<string> need = c.Needed(s, "20m", out what);
            check(what.Contains("20m") && !need.Contains("Z14") && need.Contains("Z1") && need.Count == 36, "CQ WW: 36 zones still to work on 20 m");
            int r10, r60;
            Contesting.Rate(c.SessionQsos(), out r10, out r60);
            check(r60 == 6 && r10 == 0, "rate: 6 in the last hour (fewer than 10 QSOs: no rate yet)");
            string cab = c.Cabrillo(c.SessionQsos(), new Dictionary<string, string> { { "CATEGORY-OPERATOR", "SINGLE-OP" }, { "SOAPBOX", "line one\nline two" } }, "0.4.0");
            string[] lines = cab.Replace("\r", "").Split('\n');
            check(lines[0] == "START-OF-LOG: 3.0" && lines.Contains("CONTEST: CQ-WW-SSB") && lines.Contains("CALLSIGN: K7JUS") && lines.Contains("CLAIMED-SCORE: 110") && lines.Contains("SOAPBOX: line two") && lines.Contains("END-OF-LOG:"), "Cabrillo: header, claimed score, soapbox lines");
            string first = lines.First(l => l.StartsWith("QSO:"));
            check(first.StartsWith("QSO: 14250 PH 2026-10-24 1200 K7JUS") && first.Contains("DL1ABC") && first.TrimEnd().EndsWith("59  14"), "Cabrillo: QSO line (" + first.TrimEnd() + ")");
            check(lines.Count(l => l.StartsWith("QSO:")) == 6, "Cabrillo: every QSO in the session, dupes too (the sponsor removes them)");

            // Sweepstakes SSB: once per contest, sections, DX no points
            c.Current = defs.First(d => d.Id == "ssssb");
            c.NewSession();
            c.Mine.Set("sec", "WWA"); c.Mine.Set("chk", "21"); c.Mine.Set("prec", "A");
            check(c.SentText(7) == "007 A K7JUS 21 WWA", "Sweepstakes: you send 007 A K7JUS 21 WWA");
            add(c, h, "W1AW", 14.270, "USB", "123 B 36 CT", ref t);
            add(c, h, "K6ABC", 7.200, "LSB", "45 A 99 SCV", ref t);
            add(c, h, "W1AW", 7.210, "LSB", "124 B 36 CT", ref t);     // once per contest: a dupe on any band
            add(c, h, "VE7ABC", 14.280, "USB", "12 Q 80 BC", ref t);
            add(c, h, "DL1ABC", 14.290, "USB", "1 A 70 DX", ref t);
            s = c.Compute(c.SessionQsos());
            check(s.Qsos == 4 && s.Points == 6 && s.Mults == 3 && s.Total == 18, "Sweepstakes: W1AW once, DX no points, 3 sections: 6 x 3 = 18 (" + s.Points + " x " + s.Mults + ")");
            Qso sq = c.SessionQsos().First(q => q.Call == "K6ABC");
            check(sq.Get("ARRL_SECT") == "SCV" && sq.Get("PRECEDENCE") == "A" && sq.Get("CHECK") == "99" && sq.Get("SRX") == "45", "Sweepstakes: section, precedence, check and number in ADIF");
            List<string> secs = c.Needed(s, "20m", out what);
            check(!secs.Contains("CT") && !secs.Contains("SCV") && secs.Contains("WWA"), "Sweepstakes: CT and SCV no longer needed");

            // Field Day: CW 2 points, phone 1, x2; dupes by band and mode
            c.Current = defs.First(d => d.Id == "fd");
            c.NewSession();
            add(c, h, "W1AW", 14.030, "CWU", "3A CT", ref t);
            add(c, h, "W1AW", 14.250, "USB", "3A CT", ref t);
            add(c, h, "K6ABC", 7.030, "CWU", "2A SCV", ref t);
            add(c, h, "K6ABC", 7.035, "CWU", "2A SCV", ref t);           // dupe
            s = c.Compute(c.SessionQsos());
            check(s.Qsos == 3 && s.Points == 5 && s.Total == 10 && s.FieldDay, "Field Day: CW 2 + phone 1 + CW 2 = 5, x2 = 10");

            // VHF: EDI
            c.Current = defs.First(d => d.Id == "vhfjun");
            c.NewSession();
            add(c, h, "W7ABC", 50.125, "USB", "CN85", ref t);
            add(c, h, "K7XYZ", 144.200, "USB", "DN06", ref t);
            string edi = c.Edi(c.SessionQsos());
            check(edi.StartsWith("[REG1TEST;1]") && edi.Contains("PCall=K7JUS") && edi.Contains("[QSORecords;2]") && edi.Contains(";W7ABC;1;59;001;") && edi.Contains("CN85MM"), "EDI: header and QSO records");
            s = c.Compute(c.SessionQsos());
            check(s.Points == 2 && s.Mults == 2, "VHF: a point each on 6 m and 2 m, a grid mult per band");

            check(book.Store.All().Count(q => q.Get("CONTEST_ID").Length > 0) == book.Store.Count, "all contest QSOs are in the main log too");
        }
    }
}
