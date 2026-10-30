/*  AwardTests.cs (self-test)

Awards from a small log worked out by hand: DXCC, WAS (Hawaii from its own entity, a Canadian province and a bad
state not counted), WAZ (zones logged or from the call), grids (6 m for VUCC), by band and mode, 5BDXCC and the
Challenge, and what counts as confirmed (LoTW + cards, eQSL, any).

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.IO;

namespace Kainos.Logbook.SelfTest
{
    internal static class AwardTests
    {
        private static void add(Book b, string call, string band, string mode, string extra)
        {
            Qso q = Adif.Parse("<CALL:" + call.Length + ">" + call + "<QSO_DATE:8>20261001<TIME_ON:4>" + (1000 + b.Store.Count) + "<BAND:" + band.Length + ">" + band + "<MODE:" + mode.Length + ">" + mode + extra + "<EOR>")[0];
            b.Store.Add(q);
        }

        public static void Run(string root, Action<bool, string> check)
        {
            FakeHost h = new FakeHost(Path.Combine(root, "awards"));
            Book b = new Book(h, "0.5.0") { Cty = Cty.Parse(BuiltIn.Text) };
            b.Store.Load();
            add(b, "JA1ABC", "20m", "SSB", "<CQZ:2>25<GRIDSQUARE:6>PM95ab<LOTW_QSL_RCVD:1>Y");
            add(b, "JA2ABC", "15m", "CW", "<EQSL_QSL_RCVD:1>Y");
            add(b, "DL1ABC", "20m", "FT8", "<GRIDSQUARE:4>JO62");
            add(b, "W1AW", "20m", "CW", "<STATE:2>CT<LOTW_QSL_RCVD:1>Y");
            add(b, "K6ABC", "40m", "SSB", "<STATE:2>CA<QSL_RCVD:1>Y");
            add(b, "KH6ABC", "20m", "SSB", "");
            add(b, "W7ABC", "6m", "FT8", "<STATE:2>WA<GRIDSQUARE:4>CN87<LOTW_QSL_RCVD:1>Y");
            add(b, "VE3ABC", "20m", "SSB", "<STATE:2>ON");
            add(b, "K5XYZ", "20m", "SSB", "<STATE:2>XX");

            AwardTotals a = Awards.Compute(b, b.Store.All(), "", null);
            check(a.Qsos == 9 && a.Dxcc.Worked.Count == 5 && a.Dxcc.Confirmed.Count == 2, "DXCC: 5 worked (Japan, Germany, US, Hawaii, Canada), 2 confirmed by LoTW + cards");
            check(a.EntitiesTotal > 100 && a.ContinentOf["Japan"] == "AS", "DXCC: the country list and continents");
            check(a.Was.Worked.SetEquals(new[] { "CT", "CA", "HI", "WA" }) && a.Was.Confirmed.SetEquals(new[] { "CT", "CA", "WA" }), "WAS: CT CA HI WA worked (Hawaii from its entity; ON and XX not), CT CA (card) WA confirmed");
            check(a.Waz.Worked.SetEquals(new[] { "25", "14", "5", "3", "31", "4" }) && a.Waz.Confirmed.SetEquals(new[] { "25", "5", "3" }), "WAZ: 6 zones worked (logged or from the call), 3 confirmed");
            check(a.Grids.Worked.Count == 3 && a.Grids.Confirmed.Count == 2 && a.Grids6.Confirmed.Contains("CN87"), "grids: PM95 JO62 CN87, two confirmed; CN87 on 6 m for VUCC");
            check(a.FiveBand["20m"].Confirmed.SetEquals(new[] { "Japan", "United States" }) && a.Challenge.Confirmed.Count == 4, "5BDXCC 20 m: Japan and the US; Challenge: 4 band-countries");

            AwardTotals on20 = Awards.Compute(b, b.Store.All(), "20m", null);
            check(on20.Dxcc.Worked.Count == 5 && on20.Was.Worked.SetEquals(new[] { "CT", "HI" }), "on 20 m: 5 countries, CT and HI");
            AwardTotals cw = Awards.Compute(b, b.Store.All(), "", ModeClass.CW);
            check(cw.Dxcc.Worked.SetEquals(new[] { "Japan", "United States" }) && cw.Dxcc.Confirmed.SetEquals(new[] { "United States" }), "CW only: Japan and the US worked, the US confirmed");

            b.ConfirmSource = "eqsl";
            AwardTotals e = Awards.Compute(b, b.Store.All(), "", null);
            check(e.Dxcc.Confirmed.SetEquals(new[] { "Japan" }) && e.Was.Confirmed.Count == 0, "eQSL counting: Japan (JA2ABC) confirmed, no states");
            b.ConfirmSource = "any";
            AwardTotals any = Awards.Compute(b, b.Store.All(), "", null);
            check(any.Dxcc.Confirmed.Count == 2 && any.Waz.Confirmed.Count == 3, "any confirmation: LoTW, eQSL and cards together");
            b.ConfirmSource = "lotw";
        }
    }
}
