/*  Pota.cs

Parks on the Air, through pota.app's public API (api.pota.app):
- Hunting: the activators on the air now (/spot/activator), and park names (/park/<ref>) for a park typed in.
- Activating: while an activation is on, every QSO logged (here, or by the FT8 and SSTV modules) gets your park(s)
  as MY_SIG POTA / MY_SIG_INFO / MY_POTA_REF (a two-fer lists both); afterwards, a file per park and UTC day,
  named as pota.app wants (CALL@PARK-YYYYMMDD.adi), for you to upload.
- Spotting yourself (POST /spot), only when you press Send in the spot window.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Kainos.Modules;

namespace Kainos.Logbook
{
    internal sealed class PotaSpot
    {
        public string Activator, Reference, ParkName, Location, Mode, Comments, Spotter, Grid;
        public double Khz;
        public DateTime Time;
        public string Band { get { return Bands.Of(Khz / 1000.0); } }
        public ModeClass Class { get { return Bands.ClassOf(Mode == "SSB" ? "SSB" : Mode); } }
    }

    internal sealed class PotaPark
    {
        public string Reference, Name, Location, Grid;
    }

    internal sealed class Pota
    {
        private readonly IKainosHost _host;
        private readonly HttpClient _http;
        private readonly ConcurrentDictionary<string, PotaPark> _parks = new ConcurrentDictionary<string, PotaPark>(StringComparer.OrdinalIgnoreCase);
        public static readonly Regex RefRx = new Regex("^[A-Z0-9]{1,4}-[0-9]{4,5}$");

        public Pota(IKainosHost host, HttpClient http) { _host = host; _http = http; }

        private string api(string path)
        {
            string b = _host.GetSetting("svc.base", "");
            return (b.Length > 0 ? b.TrimEnd('/') + "/pota" : "https://api.pota.app") + path;
        }

        // ---- hunting ----
        public async Task<List<PotaSpot>> SpotsAsync()
        {
            string json = await _http.GetStringAsync(api("/spot/activator")).ConfigureAwait(false);
            object[] arr = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 }.Deserialize<object[]>(json) ?? new object[0];
            List<PotaSpot> r = new List<PotaSpot>();
            foreach (Dictionary<string, object> d in arr.OfType<Dictionary<string, object>>())
            {
                double khz;
                if (!double.TryParse(s(d, "frequency"), NumberStyles.Float, CultureInfo.InvariantCulture, out khz) || khz <= 0) continue;
                DateTime t;
                DateTime.TryParse(s(d, "spotTime"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out t);
                PotaSpot p = new PotaSpot
                {
                    Activator = s(d, "activator").ToUpperInvariant(), Reference = s(d, "reference").ToUpperInvariant(), ParkName = s(d, "name").Length > 0 ? s(d, "name") : s(d, "parkName"),
                    Location = s(d, "locationDesc"), Mode = s(d, "mode").ToUpperInvariant(), Comments = s(d, "comments"), Spotter = s(d, "spotter"),
                    Grid = s(d, "grid6").Length > 0 ? s(d, "grid6") : s(d, "grid4"), Khz = khz, Time = t,
                };
                if (p.Activator.Length == 0 || p.Reference.Length == 0) continue;
                _parks.TryAdd(p.Reference, new PotaPark { Reference = p.Reference, Name = p.ParkName, Location = p.Location, Grid = p.Grid });
                r.Add(p);
            }
            return r.OrderByDescending(x => x.Time).ToList();
        }

        private static string s(Dictionary<string, object> d, string k) { object o; return d.TryGetValue(k, out o) && o != null ? Convert.ToString(o, CultureInfo.InvariantCulture).Trim() : ""; }

        public PotaPark Known(string reference) { PotaPark p; return _parks.TryGetValue(reference ?? "", out p) ? p : null; }

        public async Task<PotaPark> ParkAsync(string reference)
        {
            reference = (reference ?? "").Trim().ToUpperInvariant();
            if (!RefRx.IsMatch(reference)) return null;
            PotaPark hit;
            if (_parks.TryGetValue(reference, out hit) && hit.Name.Length > 0) return hit;
            string json = await _http.GetStringAsync(api("/park/" + Uri.EscapeDataString(reference))).ConfigureAwait(false);
            Dictionary<string, object> d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            if (d == null || s(d, "name").Length == 0) return null;
            PotaPark p = new PotaPark { Reference = reference, Name = s(d, "name"), Location = s(d, "locationDesc"), Grid = s(d, "grid6") };
            _parks[reference] = p;
            return p;
        }

        // Kainos mode for a spot: SSB as LSB below 10 MHz, USB above; CW upper; the digital modes DIGU
        public static string KainosMode(PotaSpot sp)
        {
            switch (sp.Mode)
            {
                case "SSB": case "USB": case "LSB": return sp.Khz < 10000 ? "LSB" : "USB";
                case "CW": return "CWU";
                case "AM": return "AM";
                case "FM": return "FM";
                default: return "DIGU";
            }
        }

        // ---- activating ----
        public bool Active { get { return _host.GetSetting("pota.active", "0") == "1"; } }
        public List<string> Parks { get { return (_host.GetSetting("pota.parks", "")).Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().ToUpperInvariant()).ToList(); } }
        public string ActivationGrid { get { return _host.GetSetting("pota.grid", ""); } }
        public DateTime Started
        {
            get { DateTime t; return DateTime.TryParseExact(_host.GetSetting("pota.started", ""), "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out t) ? t : DateTime.MinValue; }
        }

        // returns a problem, or null when started
        public string Start(string parks, string grid)
        {
            List<string> p = parks.ToUpperInvariant().Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Distinct().ToList();
            if (p.Count == 0) return "Enter your park, like US-1234";
            string bad = p.FirstOrDefault(x => !RefRx.IsMatch(x));
            if (bad != null) return bad + " doesn't look like a park reference (like US-1234)";
            if (grid.Length > 0 && !Geo.IsGrid(grid)) return grid + " isn't a grid square";
            _host.SetSetting("pota.parks", string.Join(",", p));
            _host.SetSetting("pota.grid", grid.ToUpperInvariant());
            _host.SetSetting("pota.started", DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture));
            _host.SetSetting("pota.active", "1");
            return null;
        }

        public void End() { _host.SetSetting("pota.active", "0"); }

        // a QSO logged during the activation carries the park(s)
        public void Apply(Qso q)
        {
            if (!Active || q.TimeOn < Started.AddMinutes(-1)) return;
            string parks = string.Join(",", Parks);
            if (parks.Length == 0) return;
            q.Set("MY_SIG", "POTA");
            q.Set("MY_SIG_INFO", parks);
            q.Set("MY_POTA_REF", parks);
            if (ActivationGrid.Length > 0) q.Set("MY_GRIDSQUARE", ActivationGrid);
        }

        // the QSOs of the activation now on (or the last one): from its start, with its parks
        public List<Qso> ActivationQsos(IEnumerable<Qso> log)
        {
            DateTime from = Started;
            List<string> parks = Parks;
            if (from == DateTime.MinValue || parks.Count == 0) return new List<Qso>();
            return log.Where(q => q.TimeOn >= from.AddMinutes(-1) && myParks(q).Any(parks.Contains)).ToList();
        }

        public static List<string> myParks(Qso q)
        {
            string v = q.Get("MY_POTA_REF");
            if (v.Length == 0 && q.Get("MY_SIG").Equals("POTA", StringComparison.OrdinalIgnoreCase)) v = q.Get("MY_SIG_INFO");
            return v.ToUpperInvariant().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
        }

        // pota.app's files: one per park and UTC day, CALL@PARK-YYYYMMDD.adi (the call without / for the file name)
        public static Dictionary<string, string> ExportFiles(IEnumerable<Qso> qsos, string myCall, string version)
        {
            Dictionary<string, string> files = new Dictionary<string, string>();
            string call = myCall.Replace("/", "-");
            foreach (var g in qsos.SelectMany(q => myParks(q).Select(p => new { Park = p, Q = q })).GroupBy(x => x.Park + "|" + x.Q.TimeOn.ToString("yyyyMMdd", CultureInfo.InvariantCulture)))
            {
                string[] k = g.Key.Split('|');
                List<Qso> list = g.Select(x =>
                {
                    Qso c = x.Q.Clone();
                    // each file is for one park
                    c.Set("MY_SIG", "POTA"); c.Set("MY_SIG_INFO", k[0]); c.Set("MY_POTA_REF", k[0]);
                    if (c.Get("STATION_CALLSIGN").Length == 0) c.Set("STATION_CALLSIGN", myCall);
                    if (c.Get("OPERATOR").Length == 0) c.Set("OPERATOR", myCall);
                    return c;
                }).OrderBy(c => c.TimeOn).ToList();
                files[call + "@" + k[0] + "-" + k[1] + ".adi"] = Adif.Write(list, version, Adif.Public);
            }
            return files;
        }

        // ---- spotting yourself: only when the user presses Send ----
        public static string SpotJson(string activator, string spotter, double khz, string mode, string reference, string comments)
        {
            Dictionary<string, object> d = new Dictionary<string, object>
            {
                { "activator", activator }, { "spotter", spotter }, { "frequency", khz.ToString("0.0", CultureInfo.InvariantCulture) },
                { "reference", reference }, { "mode", mode }, { "source", "Kainos Logbook" }, { "comments", comments ?? "" },
            };
            return new JavaScriptSerializer().Serialize(d);
        }

        public async Task<string> PostSpotAsync(string json)
        {
            HttpResponseMessage r = await _http.PostAsync(api("/spot"), new StringContent(json, Encoding.UTF8, "application/json")).ConfigureAwait(false);
            string body = await r.Content.ReadAsStringAsync().ConfigureAwait(false);
            return r.IsSuccessStatusCode ? null : "pota.app said " + (int)r.StatusCode + (body.Length > 0 ? ": " + (body.Length > 160 ? body.Substring(0, 160) : body) : "");
        }

        // ADIF mode for a spot (what pota.app expects): SSB / CW / FT8 / FT4 ...
        public static string ModeFor(string kainosMode)
        {
            string m, sub;
            Bands.ToAdif(kainosMode, out m, out sub);
            return m;
        }
    }
}
