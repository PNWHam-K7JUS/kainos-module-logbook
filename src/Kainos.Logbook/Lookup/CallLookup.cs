/*  CallLookup.cs

Name, address, grid and QSL details for a call, from (in this order, as set up): QRZ.com's XML service (a QRZ XML
subscription gives everything; without one QRZ gives the name and country), HamQTH (free, with a free account), and
callook.info (free, no account, US calls only). The first that knows the call wins. Answers are kept for the
session, so a call is only asked about once.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using Kainos.Modules;

namespace Kainos.Logbook.Lookup
{
    internal sealed class CallInfo
    {
        public string Call, Name, FirstName, Addr, City, State, County, Country, Grid, Email, QslVia, Class, ImageUrl, Source;
        public double Lat, Lon;
        public bool HasLatLon;
        public bool? Lotw, Eqsl;

        public string Qth
        {
            get
            {
                List<string> p = new List<string>();
                if (!string.IsNullOrEmpty(City)) p.Add(City);
                if (!string.IsNullOrEmpty(State)) p.Add(State);
                if (!string.IsNullOrEmpty(Country) && Country != "United States" && Country != "USA") p.Add(Country);
                return string.Join(", ", p);
            }
        }
    }

    internal sealed class CallLookup
    {
        private readonly IKainosHost _host;
        private readonly HttpClient _http;
        private readonly ConcurrentDictionary<string, CallInfo> _cache = new ConcurrentDictionary<string, CallInfo>(StringComparer.OrdinalIgnoreCase);
        private string _qrzKey, _hamqthId;
        private DateTime _hamqthAt;
        public string LastProblem = "";

        public CallLookup(IKainosHost host, HttpClient http) { _host = host; _http = http; }

        public bool UseQrz { get { return _host.GetSetting("lookup.qrz", "0") == "1" && Secrets.Get(_host, "qrz.user").Length > 0; } }
        public bool UseHamQth { get { return _host.GetSetting("lookup.hamqth", "0") == "1" && Secrets.Get(_host, "hamqth.user").Length > 0; } }
        public bool UseCallook { get { return _host.GetSetting("lookup.callook", "1") == "1"; } }
        public bool Any { get { return UseQrz || UseHamQth || UseCallook; } }

        public void Forget() { _cache.Clear(); _qrzKey = null; _hamqthId = null; }

        public async Task<CallInfo> FindAsync(string call, bool isUs)
        {
            call = (call ?? "").Trim().ToUpperInvariant();
            string bare = Cty.BaseCall(call, out string _);
            CallInfo hit;
            if (_cache.TryGetValue(bare, out hit)) return hit;
            LastProblem = "";
            CallInfo r = null;
            try { if (r == null && UseQrz) r = await qrz(bare).ConfigureAwait(false); } catch (Exception ex) { problem("QRZ", ex); }
            try { if (r == null && UseHamQth) r = await hamqth(bare).ConfigureAwait(false); } catch (Exception ex) { problem("HamQTH", ex); }
            try { if (r == null && UseCallook && isUs) r = await callook(bare).ConfigureAwait(false); } catch (Exception ex) { problem("callook", ex); }
            if (r != null) _cache[bare] = r;
            return r;
        }

        private void problem(string who, Exception ex)
        {
            LastProblem = who + ": " + (ex.InnerException ?? ex).Message;
            _host.Log("lookup " + LastProblem);
        }

        // ---- QRZ.com XML: log in for a session key, then ask; a new key when it runs out ----
        private static readonly XNamespace QrzNs = "http://xmldata.qrz.com";

        private async Task<CallInfo> qrz(string call)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (_qrzKey == null)
                {
                    string u = Secrets.Get(_host, "qrz.user"), p = Secrets.Get(_host, "qrz.pass");
                    XElement s = await getXml("https://xmldata.qrz.com/xml/current/?username=" + esc(u) + ";password=" + esc(p) + ";agent=KainosLogbook").ConfigureAwait(false);
                    XElement sess = s.Element(QrzNs + "Session");
                    _qrzKey = sess?.Element(QrzNs + "Key")?.Value;
                    if (_qrzKey == null) throw new Exception(sess?.Element(QrzNs + "Error")?.Value ?? "no session");
                }
                XElement x = await getXml("https://xmldata.qrz.com/xml/current/?s=" + _qrzKey + ";callsign=" + esc(call)).ConfigureAwait(false);
                XElement c = x.Element(QrzNs + "Callsign");
                string err = x.Element(QrzNs + "Session")?.Element(QrzNs + "Error")?.Value;
                if (c == null)
                {
                    if (err != null && err.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0) return null;
                    _qrzKey = null;     // expired or invalid: log in again once
                    if (attempt == 1) throw new Exception(err ?? "no answer");
                    continue;
                }
                Func<string, string> v = n => c.Element(QrzNs + n)?.Value ?? "";
                CallInfo r = new CallInfo
                {
                    Call = v("call"), FirstName = v("fname"), Name = (v("fname") + " " + v("name")).Trim(), Addr = v("addr1"), City = v("addr2"),
                    State = v("state"), County = v("county"), Country = v("country"), Grid = v("grid"), Email = v("email"),
                    QslVia = v("qslmgr"), Class = v("class"), ImageUrl = v("image"), Source = "QRZ.com",
                    Lotw = v("lotw") == "1" ? true : v("lotw") == "0" ? false : (bool?)null,
                    Eqsl = v("eqsl") == "1" ? true : v("eqsl") == "0" ? false : (bool?)null,
                };
                latlon(r, v("lat"), v("lon"));
                return r;
            }
            return null;
        }

        // ---- HamQTH: a session id for an hour ----
        private static readonly XNamespace HqNs = "https://www.hamqth.com";

        private async Task<CallInfo> hamqth(string call)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (_hamqthId == null || (DateTime.UtcNow - _hamqthAt).TotalMinutes > 55)
                {
                    string u = Secrets.Get(_host, "hamqth.user"), p = Secrets.Get(_host, "hamqth.pass");
                    XElement s = await getXml("https://www.hamqth.com/xml.php?u=" + esc(u) + "&p=" + esc(p)).ConfigureAwait(false);
                    XElement sess = s.Element(HqNs + "session");
                    _hamqthId = sess?.Element(HqNs + "session_id")?.Value;
                    _hamqthAt = DateTime.UtcNow;
                    if (_hamqthId == null) throw new Exception(sess?.Element(HqNs + "error")?.Value ?? "no session");
                }
                XElement x = await getXml("https://www.hamqth.com/xml.php?id=" + _hamqthId + "&callsign=" + esc(call) + "&prg=KainosLogbook").ConfigureAwait(false);
                XElement c = x.Element(HqNs + "search");
                if (c == null)
                {
                    string err = x.Element(HqNs + "session")?.Element(HqNs + "error")?.Value ?? "";
                    if (err.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0) return null;
                    _hamqthId = null;
                    if (attempt == 1) throw new Exception(err.Length > 0 ? err : "no answer");
                    continue;
                }
                Func<string, string> v = n => c.Element(HqNs + n)?.Value ?? "";
                CallInfo r = new CallInfo
                {
                    Call = v("callsign").ToUpperInvariant(), FirstName = v("nick"), Name = v("adr_name").Length > 0 ? v("adr_name") : v("nick"),
                    Addr = v("adr_street1"), City = v("adr_city"), State = v("us_state"), County = v("us_county"), Country = v("country"),
                    Grid = v("grid"), Email = v("email"), QslVia = v("qsl_via"), ImageUrl = v("picture"), Source = "HamQTH",
                    Lotw = v("lotw") == "Y" ? true : v("lotw") == "N" ? false : (bool?)null,
                    Eqsl = v("eqsl") == "Y" ? true : v("eqsl") == "N" ? false : (bool?)null,
                };
                latlon(r, v("latitude"), v("longitude"));
                return r;
            }
            return null;
        }

        // ---- callook.info: US calls, JSON ----
        private async Task<CallInfo> callook(string call)
        {
            string json = await _http.GetStringAsync("https://callook.info/" + Uri.EscapeDataString(call) + "/json").ConfigureAwait(false);
            Dictionary<string, object> d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            if (d == null || !"VALID".Equals(str(d, "status"), StringComparison.OrdinalIgnoreCase)) return null;
            Dictionary<string, object> addr = d.ContainsKey("address") ? d["address"] as Dictionary<string, object> : null;
            Dictionary<string, object> loc = d.ContainsKey("location") ? d["location"] as Dictionary<string, object> : null;
            Dictionary<string, object> cur = d.ContainsKey("current") ? d["current"] as Dictionary<string, object> : null;
            string name = str(d, "name");
            string line2 = addr != null ? str(addr, "line2") : "";
            string city = line2, state = "";
            // "NEWINGTON, CT 06111"
            int comma = line2.LastIndexOf(',');
            if (comma > 0) { city = line2.Substring(0, comma).Trim(); string[] st = line2.Substring(comma + 1).Trim().Split(' '); state = st[0]; }
            CallInfo r = new CallInfo
            {
                Call = call, Name = titleCase(name), FirstName = titleCase(name.Split(' ')[0]), Addr = addr != null ? titleCase(str(addr, "line1")) : "",
                City = titleCase(city), State = state, Country = "United States", Grid = loc != null ? str(loc, "gridsquare") : "",
                Class = cur != null ? str(cur, "operClass") : "", Source = "callook.info",
            };
            if (loc != null) latlon(r, str(loc, "latitude"), str(loc, "longitude"));
            return r;
        }

        private static string str(Dictionary<string, object> d, string k) { object o; return d.TryGetValue(k, out o) && o != null ? o.ToString() : ""; }

        private static string titleCase(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Any(char.IsLower)) return s ?? "";
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
        }

        private static void latlon(CallInfo r, string lat, string lon)
        {
            double a, b;
            if (double.TryParse(lat, NumberStyles.Float, CultureInfo.InvariantCulture, out a) && double.TryParse(lon, NumberStyles.Float, CultureInfo.InvariantCulture, out b) && (a != 0 || b != 0))
            { r.Lat = a; r.Lon = b; r.HasLatLon = true; }
            else if (Geo.GridToLatLon(r.Grid, out a, out b)) { r.Lat = a; r.Lon = b; r.HasLatLon = true; }
        }

        private async Task<XElement> getXml(string url)
        {
            string s = await _http.GetStringAsync(url).ConfigureAwait(false);
            return XDocument.Parse(s).Root;
        }

        private static string esc(string s) { return Uri.EscapeDataString(s ?? ""); }

        // a quick check for the settings window: log in and look up the user's own call
        public async Task<string> TestAsync(string which, string ownCall)
        {
            Forget();
            try
            {
                CallInfo r = which == "qrz" ? await qrz(ownCall).ConfigureAwait(false) : await hamqth(ownCall).ConfigureAwait(false);
                return r != null ? "Works: " + ownCall + " is " + (r.Name.Length > 0 ? r.Name : "found") : "Logged in, but " + ownCall + " wasn't found";
            }
            catch (Exception ex) { return "Didn't work: " + (ex.InnerException ?? ex).Message; }
        }

        public static HttpClient NewHttp(string version)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            HttpClient h = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            h.DefaultRequestHeaders.UserAgent.ParseAdd("KainosLogbook/" + version);
            return h;
        }
    }
}
