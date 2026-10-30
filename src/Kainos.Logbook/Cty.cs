/*  Cty.cs

Which country (DXCC entity) a call is in, with its continent, CQ and ITU zones and a point for the beam heading:
from AD1C's cty.dat (country-files.com), the file contest loggers use. Kainos downloads it the first time and every
two weeks after, into the module's data folder; until then a short built-in list covers the common prefixes.

cty.dat: an entity line (name: CQ: ITU: continent: lat: lon (west is +): UTC offset: main prefix:) then its prefixes
and calls, comma-separated, to a ';'. "=CALL" is a whole call; (n) [n] <lat/lon> {cont} ~tz~ override the entity's.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Kainos.Logbook
{
    internal sealed class Entity
    {
        public string Name, Prefix, Cont;
        public int Cq, Itu;
        public double Lat, Lon, UtcOffset;
        public bool WaeOnly;        // '*' entities (WAE countries, not DXCC)
    }

    // what a call resolves to: the entity, with any per-prefix overrides applied
    internal sealed class CtyInfo
    {
        public Entity Entity;
        public string Cont;
        public int Cq, Itu;
        public double Lat, Lon, UtcOffset;
        public string CallArea;     // the call's digit, for US / VE headings
        public string Name { get { return Entity.Name; } }
    }

    internal sealed class Cty
    {
        private readonly Dictionary<string, Tuple<Entity, Ovr>> _prefix = new Dictionary<string, Tuple<Entity, Ovr>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Tuple<Entity, Ovr>> _exact = new Dictionary<string, Tuple<Entity, Ovr>>(StringComparer.OrdinalIgnoreCase);
        private int _maxPrefix = 1;
        public readonly List<Entity> Entities = new List<Entity>();
        public string Source = "built-in list";

        private sealed class Ovr { public int Cq, Itu; public string Cont; public double? Lat, Lon, Tz; }

        public static Cty Parse(string text)
        {
            Cty c = new Cty();
            Entity cur = null;
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                if (raw.Length == 0) continue;
                if (!char.IsWhiteSpace(raw[0]))
                {
                    string[] p = raw.Split(':');
                    if (p.Length < 8) { cur = null; continue; }
                    string pfx = p[7].Trim();
                    cur = new Entity
                    {
                        Name = p[0].Trim(), Cq = toInt(p[1]), Itu = toInt(p[2]), Cont = p[3].Trim(),
                        Lat = toD(p[4]), Lon = -toD(p[5]), UtcOffset = -toD(p[6]),
                        WaeOnly = pfx.StartsWith("*"), Prefix = pfx.TrimStart('*'),
                    };
                    c.Entities.Add(cur);
                    continue;
                }
                if (cur == null) continue;
                foreach (string item in raw.Split(','))
                {
                    string s = item.Trim().TrimEnd(';').Trim();
                    if (s.Length == 0) continue;
                    c.addItem(cur, s);
                }
            }
            return c;
        }

        private static readonly Regex Ovrx = new Regex(@"\((\d+)\)|\[(\d+)\]|<([-\d.]+)/([-\d.]+)>|\{(\w+)\}|~([-\d.]+)~");

        private void addItem(Entity e, string s)
        {
            Ovr o = null;
            string key = s;
            int cut = s.IndexOfAny(new[] { '(', '[', '<', '{', '~' });
            if (cut >= 0)
            {
                key = s.Substring(0, cut);
                o = new Ovr();
                foreach (Match m in Ovrx.Matches(s.Substring(cut)))
                {
                    if (m.Groups[1].Success) o.Cq = toInt(m.Groups[1].Value);
                    else if (m.Groups[2].Success) o.Itu = toInt(m.Groups[2].Value);
                    else if (m.Groups[3].Success) { o.Lat = toD(m.Groups[3].Value); o.Lon = -toD(m.Groups[4].Value); }
                    else if (m.Groups[5].Success) o.Cont = m.Groups[5].Value;
                    else if (m.Groups[6].Success) o.Tz = -toD(m.Groups[6].Value);
                }
            }
            if (key.StartsWith("=")) { _exact[key.Substring(1)] = Tuple.Create(e, o); return; }
            if (key.Length == 0) return;
            if (!_prefix.ContainsKey(key) || (e.WaeOnly == false && _prefix[key].Item1.WaeOnly)) _prefix[key] = Tuple.Create(e, o);
            if (key.Length > _maxPrefix) _maxPrefix = key.Length;
        }

        private static int toInt(string s) { int v; return int.TryParse(s.Trim(), out v) ? v : 0; }
        private static double toD(string s) { double v; return double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0; }

        // "EA8/DL1ABC" -> prefix EA8; "K7JUS/P" -> K7JUS; "VP2E/K7JUS" -> VP2E
        private static readonly HashSet<string> Suffixes = new HashSet<string>(new[] { "P", "M", "MM", "AM", "QRP", "A", "R", "B", "LH", "J", "T" });

        public static string BaseCall(string call, out string prefix)
        {
            prefix = null;
            call = (call ?? "").Trim().ToUpperInvariant();
            if (!call.Contains("/")) return call;
            List<string> parts = call.Split('/').Where(x => x.Length > 0 && !Suffixes.Contains(x)).ToList();
            if (parts.Count == 0) return call.Split('/')[0];
            if (parts.Count == 1) return parts[0];
            string a = parts[0], b = parts[1];
            if (b.Length == 1 && char.IsDigit(b[0])) { prefix = callAreaPrefix(a, b[0]); return a; }   // K7JUS/4
            if (a.Length <= b.Length) { prefix = a; return b; }
            prefix = b; return a;
        }

        private static string callAreaPrefix(string call, char digit)
        {
            Match m = Regex.Match(call, "^([A-Z0-9]*?)[0-9]");
            return m.Success ? m.Groups[1].Value + digit : null;
        }

        public CtyInfo Resolve(string callIn)
        {
            if (string.IsNullOrWhiteSpace(callIn)) return null;
            string prefix;
            string call = BaseCall(callIn, out prefix);
            Tuple<Entity, Ovr> hit;
            if (prefix == null && _exact.TryGetValue(call, out hit)) return info(hit, call);
            if (_exact.TryGetValue(callIn.Trim().ToUpperInvariant(), out hit)) return info(hit, call);
            string key = prefix ?? call;
            for (int n = Math.Min(_maxPrefix, key.Length); n > 0; n--)
                if (_prefix.TryGetValue(key.Substring(0, n), out hit)) return info(hit, key);
            return null;
        }

        private static CtyInfo info(Tuple<Entity, Ovr> hit, string key)
        {
            Entity e = hit.Item1;
            Ovr o = hit.Item2;
            CtyInfo r = new CtyInfo
            {
                Entity = e, Cont = o != null && o.Cont != null ? o.Cont : e.Cont,
                Cq = o != null && o.Cq > 0 ? o.Cq : e.Cq, Itu = o != null && o.Itu > 0 ? o.Itu : e.Itu,
                Lat = o != null && o.Lat.HasValue ? o.Lat.Value : e.Lat, Lon = o != null && o.Lon.HasValue ? o.Lon.Value : e.Lon,
                UtcOffset = o != null && o.Tz.HasValue ? o.Tz.Value : e.UtcOffset,
            };
            Match m = Regex.Match(key, "[0-9]");
            if (m.Success) r.CallArea = m.Value;
            // the US is one point in cty.dat (Kansas): the call area gives a better heading
            if (e.Name == "United States" && (o == null || !o.Lat.HasValue) && r.CallArea != null)
            {
                double[] a = UsArea(r.CallArea);
                if (a != null) { r.Lat = a[0]; r.Lon = a[1]; }
            }
            return r;
        }

        public static double[] UsArea(string digit)
        {
            switch (digit)
            {
                case "1": return new[] { 42.5, -71.5 };
                case "2": return new[] { 41.5, -74.5 };
                case "3": return new[] { 40.0, -77.0 };
                case "4": return new[] { 34.0, -84.0 };
                case "5": return new[] { 32.0, -97.0 };
                case "6": return new[] { 37.0, -120.0 };
                case "7": return new[] { 44.0, -116.0 };
                case "8": return new[] { 40.5, -83.0 };
                case "9": return new[] { 41.5, -88.0 };
                case "0": return new[] { 40.0, -98.0 };
            }
            return null;
        }

        // ---- loading: cty.dat in the data folder, refreshed every 14 days; the built-in list until then ----

        public const string Url = "https://www.country-files.com/cty/cty.dat";

        public static Cty Load(string folder, out bool stale)
        {
            string f = Path.Combine(folder, "cty.dat");
            stale = true;
            if (File.Exists(f))
            {
                try
                {
                    Cty c = Parse(File.ReadAllText(f));
                    if (c.Entities.Count > 300)
                    {
                        DateTime when = File.GetLastWriteTimeUtc(f);
                        c.Source = "cty.dat, " + when.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        stale = (DateTime.UtcNow - when).TotalDays > 14;
                        return c;
                    }
                }
                catch { }
            }
            Cty b = Parse(BuiltIn.Text);
            b.Source = "built-in list (cty.dat not downloaded yet)";
            return b;
        }

        public static async Task<bool> DownloadAsync(string folder, HttpClient http)
        {
            string text = await http.GetStringAsync(Url).ConfigureAwait(false);
            if (Parse(text).Entities.Count < 300) return false;
            string f = Path.Combine(folder, "cty.dat");
            File.WriteAllText(f + ".tmp", text);
            if (File.Exists(f)) File.Delete(f);
            File.Move(f + ".tmp", f);
            return true;
        }
    }
}
