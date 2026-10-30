/*  PotaTests.cs (self-test)

POTA against a stand-in for api.pota.app (FakeServer; nothing reaches pota.app): spots read (a bad one skipped),
the mode to tune for a spot, park names (asked once), an activation (bad refs refused; a two-fer; the parks and
grid on QSOs logged during it, from the FT8 module too, not before it or after it ends), the files for pota.app
(one per park and day, named CALL@PARK-YYYYMMDD.adi), and a spot sent only when asked.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Kainos.Logbook.Lookup;

namespace Kainos.Logbook.SelfTest
{
    internal static class PotaTests
    {
        public const string SpotsJson = "[" +
            "{\"spotId\":1,\"activator\":\"K4ABC\",\"frequency\":\"14286.0\",\"mode\":\"SSB\",\"reference\":\"US-1234\",\"parkName\":\"Example State Park\",\"name\":\"Example State Park\",\"spotTime\":\"2026-10-30T18:02:00\",\"spotter\":\"N0XYZ\",\"comments\":\"QRT 19z\",\"locationDesc\":\"US-VA\",\"grid6\":\"FM17ab\"}," +
            "{\"spotId\":2,\"activator\":\"N7XYZ\",\"frequency\":\"7062.0\",\"mode\":\"CW\",\"reference\":\"US-5678\",\"name\":\"Example National Forest\",\"spotTime\":\"2026-10-30T17:58:00\",\"locationDesc\":\"US-OR\"}," +
            "{\"spotId\":3,\"activator\":\"\",\"frequency\":\"x\",\"mode\":\"FT8\",\"reference\":\"US-0001\"}," +
            "{\"spotId\":4,\"activator\":\"VE3ABC\",\"frequency\":\"7074.0\",\"mode\":\"FT8\",\"reference\":\"CA-0001\",\"name\":\"Example Provincial Park\",\"spotTime\":\"2026-10-30T17:55:00\",\"locationDesc\":\"CA-ON\"}]";

        public static Tuple<int, string> Serve(FakeRequest r, List<string> spotsPosted)
        {
            if (r.Path == "/pota/spot/activator") return Tuple.Create(200, SpotsJson);
            if (r.Path == "/pota/park/US-9999") return Tuple.Create(200, "{\"reference\":\"US-9999\",\"name\":\"Example Lake Park\",\"locationDesc\":\"US-WA\",\"grid6\":\"CN87ab\"}");
            if (r.Path == "/pota/spot" && r.Method == "POST") { lock (spotsPosted) spotsPosted.Add(r.Body); return Tuple.Create(200, "{}"); }
            return null;
        }

        public static void Run(string root, Action<bool, string> check)
        {
            List<string> posted = new List<string>();
            using (FakeServer srv = new FakeServer())
            {
                srv.Handle = r => Serve(r, posted) ?? Tuple.Create(404, "no");
                FakeHost h = new FakeHost(Path.Combine(root, "pota"));
                h.SetSetting("svc.base", srv.Base);
                using (System.Net.Http.HttpClient http = CallLookup.NewHttp("selftest"))
                {
                    Pota p = new Pota(h, http);
                    List<PotaSpot> spots = p.SpotsAsync().Result;
                    check(spots.Count == 3 && spots[0].Activator == "K4ABC" && spots[0].Band == "20m" && spots[0].ParkName == "Example State Park", "POTA spots: read (newest first), a bad one skipped");
                    check(Pota.KainosMode(spots[0]) == "USB" && Pota.KainosMode(new PotaSpot { Mode = "SSB", Khz = 7200 }) == "LSB" && Pota.KainosMode(spots[1]) == "CWU" && Pota.KainosMode(spots[2]) == "DIGU", "POTA spots: tuned in USB above 10 MHz, LSB below, CWU, DIGU");
                    check(p.Known("US-1234")?.Name == "Example State Park", "POTA: a park's name known from the spots");
                    PotaPark park = p.ParkAsync("us-9999").Result;
                    PotaPark again = p.ParkAsync("US-9999").Result;
                    check(park != null && park.Name == "Example Lake Park" && srv.Count("/pota/park/US-9999") == 1, "POTA: a park's name looked up once");

                    check(p.Start("", "") != null && p.Start("US-12", "") != null && p.Start("US-1234", "ZZ99") != null && !p.Active, "POTA activation: an empty or bad park, or a bad grid, is refused");
                    LogStore store = new LogStore(h.DataFolder, "0.1.0");
                    store.Load();
                    Qso before = Adif.Parse("<CALL:6>W1BEFO<QSO_DATE:8>20261030<TIME_ON:4>0100<BAND:3>20m<MODE:3>SSB<EOR>")[0];
                    store.Add(before);
                    check(p.Start("us-1234, US-5678", "cn87ab") == null && p.Active && p.Parks.SequenceEqual(new[] { "US-1234", "US-5678" }), "POTA activation: a two-fer started");
                    Qso q = new Qso();
                    q.Call = "K1ABC"; q.TimeOn = DateTime.UtcNow; q.Band = "20m"; q.Mode = "SSB"; q.Set("STATION_CALLSIGN", "K7JUS");
                    p.Apply(q);
                    check(q.Get("MY_SIG") == "POTA" && q.Get("MY_POTA_REF") == "US-1234,US-5678" && q.Get("MY_GRIDSQUARE") == "CN87AB", "POTA activation: a QSO gets both parks and the grid");
                    p.Apply(before);
                    check(before.Get("MY_SIG").Length == 0, "POTA activation: not a QSO from before it started");
                    store.Add(q);
                    Qso p2p = q.Clone(); p2p.Id = ""; p2p.Call = "N7XYZ"; p2p.TimeOn = DateTime.UtcNow.AddSeconds(5); p2p.Set("SIG", "POTA"); p2p.Set("SIG_INFO", "US-5678"); p2p.Set("POTA_REF", "US-5678");
                    store.Add(p2p);

                    // an FT8 QSO from the Digital Modes module during the activation
                    Directory.CreateDirectory(Path.Combine(root, "pota", "digimodes"));
                    string t = DateTime.UtcNow.AddSeconds(10).ToString("HHmmss");
                    File.WriteAllText(Path.Combine(root, "pota", "digimodes", "kainos-digimodes.adi"), "<EOH>\r\n<CALL:5>JA1AB <QSO_DATE:8>" + DateTime.UtcNow.ToString("yyyyMMdd") + " <TIME_ON:6>" + t + " <BAND:3>20m <MODE:3>FT8 <EOR>\r\n");
                    Feeds f = new Feeds(h, store) { Adjust = p.Apply };
                    f.Poll();
                    check(store.All().First(x => x.Call == "JA1AB").Get("MY_POTA_REF") == "US-1234,US-5678", "POTA activation: an FT8 QSO from the Digital Modes module gets the parks too");
                    List<Qso> act = p.ActivationQsos(store.All());
                    check(act.Count == 3, "POTA activation: three QSOs in it (" + act.Count + ")");

                    Dictionary<string, string> files = Pota.ExportFiles(act, "K7JUS", "0.1.0");
                    string day = DateTime.UtcNow.ToString("yyyyMMdd");
                    check(files.Count == 2 && files.ContainsKey("K7JUS@US-1234-" + day + ".adi") && files.ContainsKey("K7JUS@US-5678-" + day + ".adi"), "POTA export: a file per park, named K7JUS@PARK-YYYYMMDD.adi");
                    List<Qso> one = Adif.Parse(files["K7JUS@US-1234-" + day + ".adi"]);
                    check(one.Count == 3 && one.All(x => x.Get("MY_SIG_INFO") == "US-1234" && x.Get("MY_POTA_REF") == "US-1234" && x.Get("STATION_CALLSIGN") == "K7JUS") && one.Any(x => x.Get("SIG_INFO") == "US-5678"), "POTA export: each file has its one park, the park-to-park kept");
                    check(!files.Values.Any(v => v.Contains("APP_KAINOS")), "POTA export: no logbook bookkeeping in the files");
                    check(Pota.ExportFiles(act, "K7JUS/P", "0.1.0").Keys.All(k => k.StartsWith("K7JUS-P@")), "POTA export: a / in the call isn't put in a file name");

                    check(posted.Count == 0, "POTA spot: nothing posted until asked");
                    string json = Pota.SpotJson("K7JUS", "K7JUS", 14286.0, "SSB", "US-1234", "QRT 19z");
                    string problem = p.PostSpotAsync(json).Result;
                    check(problem == null && posted.Count == 1 && posted[0].Contains("\"activator\":\"K7JUS\"") && posted[0].Contains("\"reference\":\"US-1234\"") && posted[0].Contains("\"frequency\":\"14286.0\""), "POTA spot: posted when asked, with the call, park and frequency");

                    p.End();
                    Qso after = new Qso(); after.Call = "G4ABC"; after.TimeOn = DateTime.UtcNow;
                    p.Apply(after);
                    check(!p.Active && after.Get("MY_SIG").Length == 0 && p.ActivationQsos(store.All()).Count == 3, "POTA activation: ended, no more parks added; the last one's QSOs still there to export");
                }
            }
        }
    }
}
