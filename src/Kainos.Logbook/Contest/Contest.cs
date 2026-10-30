/*  Contest.cs

Contests: each one's exchange, dupe rule and scoring (ContestList.cs), a session (the contest's QSOs, kept in the
main log with CONTEST_ID, the exchange sent and received as STX / SRX / STX_STRING / SRX_STRING and in the usual
ADIF fields, and APP_KAINOS_CSESSION), the claimed score, and the files to send in: Cabrillo 3.0, and EDI for the
VHF contests.

The scoring is each contest's main rules (points by continent / country / zone / distance, its multipliers per
band or once): the claimed score, before the sponsor's log checking.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Kainos.Modules;

namespace Kainos.Logbook.Contest
{
    internal sealed class ContestDef
    {
        public string Id, Name, When, Scoring, Dupe, Cabrillo, Note;
        public string[] Modes, Exch, Sent;

        public static List<ContestDef> All()
        {
            List<ContestDef> r = new List<ContestDef>();
            foreach (string line in ContestList.Text.Replace("\r", "").Split('\n'))
            {
                string[] p = line.Split('|');
                if (p.Length < 10) continue;
                r.Add(new ContestDef
                {
                    Id = p[0], Name = p[1], When = p[2], Modes = p[3].Split(' '), Exch = p[4].Split(' '),
                    Sent = (p[5].Length > 0 ? p[5] : p[4]).Split(' '), Scoring = p[6], Dupe = p[7], Cabrillo = p[8], Note = p[9],
                });
            }
            return r;
        }

        public string ModesText { get { return string.Join(", ", Modes.Select(m => m == "PH" ? "SSB" : m == "RY" ? "RTTY" : m == "DG" ? "FT8 / FT4" : m)); } }
    }

    // one kind of exchange field: its label, where it goes in ADIF, and what it accepts
    internal static class Field
    {
        public static string Label(string k)
        {
            switch (k)
            {
                case "rst": return "RST"; case "serial": return "Nr"; case "zone": return "CQ zone"; case "itu": return "ITU zone";
                case "pwr": return "Power"; case "state": return "State / prov"; case "sec": return "Section"; case "name": return "Name";
                case "cls": return "Class"; case "grid": return "Grid"; case "chk": return "Check"; case "prec": return "Prec";
                case "num": return "Nr / QTH"; case "age": return "Age"; case "yr": return "Year"; case "loc": return "County / QTH";
            }
            return k;
        }

        public static float Width(string k) { return k == "name" || k == "num" || k == "loc" ? 1.2f : k == "prec" || k == "age" || k == "chk" ? 0.7f : 1f; }

        public static readonly string[] States = "AL AK AZ AR CA CO CT DE FL GA HI ID IL IN IA KS KY LA ME MD MA MI MN MS MO MT NE NV NH NJ NM NY NC ND OH OK OR PA RI SC SD TN TX UT VT VA WA WV WI WY DC".Split(' ');
        public static readonly string[] Provinces = "NS QC ON MB SK AB BC NT NB NL PE YT NU".Split(' ');
        public static readonly string[] Sections = "CT EMA ME NH RI VT WMA ENY NLI NNJ NNY SNJ WNY DE EPA MDC WPA AL GA KY NC NFL PR SC SFL TN VA VI WCF AR LA MS NM NTX OK STX WTX EB LAX ORG PAC SB SCV SDG SF SJV SV AK AZ EWA ID MT NV OR UT WWA WY MI OH WV IL IN WI CO IA KS MN MO ND NE SD AB BC GH MB NB NL NS ONE ONN ONS PE QC SK TER".Split(' ');

        public static bool Valid(string k, string v)
        {
            v = (v ?? "").Trim().ToUpperInvariant();
            if (v.Length == 0) return false;
            int n;
            switch (k)
            {
                case "rst": return Regex.IsMatch(v, "^([1-5][1-9][1-9]?|[+-]?[0-9]{1,2})$");
                case "serial": return Regex.IsMatch(v, "^[0-9]{1,5}$");
                case "num": return Regex.IsMatch(v, "^([0-9]{1,6}|[A-Z]{1,4})$");
                case "zone": return int.TryParse(v, out n) && n >= 1 && n <= 40;
                case "itu": return (int.TryParse(v, out n) && n >= 1 && n <= 90) || Regex.IsMatch(v, "^[A-Z0-9]{2,8}$");
                case "pwr": return Regex.IsMatch(v, "^([0-9]{1,4}|K|KW|[0-9]K)$");
                case "state": return States.Contains(v) || Provinces.Contains(v) || v == "DX" || Regex.IsMatch(v, "^[0-9]{1,5}$");
                case "sec": return Sections.Contains(v) || v == "DX";
                case "cls": return Regex.IsMatch(v, "^[0-9]{1,2}[A-FHIOM]$");
                case "grid": return Regex.IsMatch(v, "^[A-R]{2}[0-9]{2}([A-X]{2})?$");
                case "chk": case "age": case "yr": return Regex.IsMatch(v, "^[0-9]{2}$");
                case "prec": return Regex.IsMatch(v, "^[QABUMS]$");
                case "name": return Regex.IsMatch(v, "^[A-Z]{1,12}$");
                case "loc": return Regex.IsMatch(v, "^[A-Z0-9]{2,6}$");
            }
            return true;
        }

        // the ADIF field a received value also goes in
        public static string AdifOf(string k)
        {
            switch (k)
            {
                case "rst": return "RST_RCVD"; case "serial": return "SRX"; case "zone": return "CQZ"; case "itu": return "ITUZ";
                case "pwr": return "RX_PWR"; case "sec": return "ARRL_SECT"; case "name": return "NAME"; case "cls": return "CLASS";
                case "grid": return "GRIDSQUARE"; case "chk": return "CHECK"; case "prec": return "PRECEDENCE"; case "age": return "AGE";
            }
            return null;
        }
    }

    // what you send (Station settings for contests), with sensible starting values
    internal sealed class MyExchange
    {
        private readonly IKainosHost _h;
        private readonly Book _book;
        public MyExchange(Book book) { _book = book; _h = book.Host; }

        public string Get(string k)
        {
            string v = _h.GetSetting("cx." + k, "");
            if (v.Length > 0) return v;
            CtyInfo me = _book.Cty.Resolve(_book.MyCall);
            switch (k)
            {
                case "zone": return me != null ? me.Cq.ToString(CultureInfo.InvariantCulture) : "";
                case "itu": return me != null ? me.Itu.ToString(CultureInfo.InvariantCulture) : "";
                case "grid": return _book.MyGrid.Length >= 4 ? _book.MyGrid.Substring(0, 4) : "";
                case "pwr": return _book.MyPower;
                case "cls": return "1D";
                case "prec": return "A";
                case "call": return _book.MyCall;
            }
            return "";
        }

        public void Set(string k, string v) { _h.SetSetting("cx." + k, (v ?? "").Trim().ToUpperInvariant()); }

        public static readonly string[] Kinds = { "state", "sec", "name", "cls", "chk", "prec", "num", "age", "yr", "loc", "zone", "itu", "grid", "pwr" };
    }

    // a contest QSO as the scoring sees it
    internal sealed class CQso
    {
        public Qso Q;
        public string Call, Band, Mode, Entity, Cont, Prefix;
        public int Cq, Itu;
        public double Lat, Lon;
        public Dictionary<string, string> X = new Dictionary<string, string>();
        public string Get(string k) { string v; return X.TryGetValue(k, out v) ? v : ""; }
    }

    internal sealed class Score
    {
        public int Qsos, Points, Mults, Total;
        public HashSet<string> MultSet = new HashSet<string>();
        public List<string> NewMults = new List<string>();      // those the last QSO added
        public int LastPoints;
        public bool FieldDay;
    }

    internal sealed class Contesting
    {
        public readonly Book Book;
        public readonly List<ContestDef> Defs = ContestDef.All();
        public readonly MyExchange Mine;

        public Contesting(Book book) { Book = book; Mine = new MyExchange(book); }

        public ContestDef Current
        {
            get { string id = Book.Host.GetSetting("contest.id", "cqwwssb"); return Defs.FirstOrDefault(d => d.Id == id) ?? Defs[0]; }
            set { Book.Host.SetSetting("contest.id", value.Id); }
        }

        // the session: this contest from when it was started; "New session" starts again (the QSOs stay in the log)
        public string Session
        {
            get
            {
                string s = Book.Host.GetSetting("contest.session", "");
                if (!s.StartsWith(Current.Id + "-")) { s = Current.Id + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture); Book.Host.SetSetting("contest.session", s); }
                return s;
            }
        }
        public void NewSession() { Book.Host.SetSetting("contest.session", ""); string _ = Session; }

        public List<Qso> SessionQsos() { string s = Session; return Book.Store.All().Where(q => q.Get("APP_KAINOS_CSESSION") == s).ToList(); }

        public static string ModeOf(Qso q)
        {
            string m = q.ModeShown;
            if (m == "RTTY") return "RY";
            ModeClass c = q.Class;
            return c == ModeClass.CW ? "CW" : c == ModeClass.Phone ? "PH" : "DG";
        }

        public static string ModeOf(string kainosMode)
        {
            string m, sub;
            Bands.ToAdif(kainosMode, out m, out sub);
            if (m == "RTTY") return "RY";
            ModeClass c = Bands.ClassOf(sub ?? m);
            return c == ModeClass.CW ? "CW" : c == ModeClass.Phone ? "PH" : "DG";
        }

        // what you send, with the next serial number
        public string SentText(int serial)
        {
            ContestDef c = Current;
            return string.Join(" ", c.Sent.Select(k => sentValue(k, serial)));
        }

        public string sentValue(string k, int serial)
        {
            if (k == "serial") return serial.ToString("000", CultureInfo.InvariantCulture);
            if (k == "rst") return ModeOf(Book.Host.Radio.Mode(Book.Host.Radio.TxReceiver)) == "PH" ? "59" : "599";
            if (k == "call") return Book.MyCall;
            return Mine.Get(k == "county" ? "loc" : k);
        }

        // what can be filled in before the other station sends it: the RST, their zones from the call
        public Dictionary<string, string> Guess(string call, string mode)
        {
            Dictionary<string, string> g = new Dictionary<string, string>();
            g["rst"] = mode == "CW" || mode == "RY" ? "599" : mode == "PH" ? "59" : "";
            CtyInfo c = Book.Cty.Resolve(call);
            if (c != null)
            {
                g["zone"] = c.Cq.ToString(CultureInfo.InvariantCulture);
                g["itu"] = c.Itu.ToString(CultureInfo.InvariantCulture);
                if (c.Name != "United States" && c.Name != "Canada") { g["state"] = "DX"; g["sec"] = "DX"; }
            }
            return g;
        }

        // the dupe rule: already worked on this band / band and mode / mode / at all in the session
        public Qso Dupe(List<Qso> session, string call, string band, string mode)
        {
            string d = Current.Dupe;
            string bare = Cty.BaseCall(call, out string _);
            return session.FirstOrDefault(q => Cty.BaseCall(q.Call, out string _) == bare &&
                (d == "once" || (d == "band" && q.Band == band) || (d == "mode" && ModeOf(q) == mode) || (d == "bandmode" && q.Band == band && ModeOf(q) == mode)));
        }

        // a new contest QSO from the entry: the exchange in its own fields and the usual ADIF ones
        public Qso Make(string call, Dictionary<string, string> rcvd, int serial)
        {
            ContestDef c = Current;
            Qso q = Book.NewFromRadio();
            q.LoggedHere = true;
            q.Call = call.ToUpperInvariant();
            q.Set("CONTEST_ID", c.Cabrillo);
            q.Set("APP_KAINOS_CSESSION", Session);
            q.Set("STX_STRING", SentText(serial));
            if (c.Sent.Contains("serial")) q.Set("STX", serial.ToString(CultureInfo.InvariantCulture));
            q.RstSent = c.Sent.Contains("rst") ? sentValue("rst", serial) : Bands.DefaultRst(q.Class);
            List<string> got = new List<string>();
            foreach (string k in c.Exch)
            {
                string v; rcvd.TryGetValue(k, out v);
                v = (v ?? "").Trim().ToUpperInvariant();
                got.Add(v);
                q.Set("APP_KAINOS_X_" + k.ToUpperInvariant(), v);
                string a = Field.AdifOf(k);
                if (a != null && v.Length > 0 && !(k == "sec" && v == "DX")) q.Set(a, k == "grid" ? v.Substring(0, Math.Min(4, v.Length)) : v);
                if (k == "state" && (Field.States.Contains(v) || Field.Provinces.Contains(v))) q.State = v;
            }
            q.Set("SRX_STRING", string.Join(" ", got));
            if (!c.Exch.Contains("rst")) q.RstRcvd = Bands.DefaultRst(q.Class);
            Book.Fill(q, null);
            return q;
        }

        public CQso View(Qso q)
        {
            CtyInfo ci = Book.Cty.Resolve(q.Call);
            CQso v = new CQso { Q = q, Call = Cty.BaseCall(q.Call, out string _), Band = q.Band, Mode = ModeOf(q), Entity = ci != null ? ci.Name : q.Country, Cont = ci != null ? ci.Cont : q.Get("CONT"), Prefix = WpxPrefix(q.Call) };
            v.Cq = q.CqZone > 0 ? q.CqZone : ci != null ? ci.Cq : 0;
            int itu; v.Itu = int.TryParse(q.Get("ITUZ"), out itu) ? itu : ci != null ? ci.Itu : 0;
            if (!Geo.GridToLatLon(q.Grid, out v.Lat, out v.Lon) && ci != null) { v.Lat = ci.Lat; v.Lon = ci.Lon; }
            foreach (string k in Current.Exch) v.X[k] = q.Get("APP_KAINOS_X_" + k.ToUpperInvariant());
            return v;
        }

        // CQ WPX's prefix: the call's (or the portable prefix's) letters and numbers up to its last digit
        public static string WpxPrefix(string callIn)
        {
            string prefix;
            string call = Cty.BaseCall(callIn, out prefix);
            string c = prefix ?? call;
            Match m = Regex.Match(c, "^([A-Z0-9]*?[0-9])[A-Z]*$");
            if (m.Success) return m.Groups[1].Value;
            m = Regex.Match(c, "^(.*[0-9])");
            return m.Success ? m.Groups[1].Value : (c.Length >= 2 ? c.Substring(0, 2) : c) + "0";
        }

        // ---- the score ----
        public Score Compute(List<Qso> session)
        {
            ContestDef c = Current;
            Score s = new Score { FieldDay = c.Scoring == "fd" };
            CtyInfo me = Book.Cty.Resolve(Book.MyCall);
            string myEnt = me != null ? me.Name : "", myCont = me != null ? me.Cont : "";
            int myItu = me != null ? me.Itu : 0;
            double myLat, myLon;
            Book.MyLatLon(out myLat, out myLon);
            HashSet<string> seen = new HashSet<string>();
            int i = 0;
            foreach (Qso q in session.OrderBy(x => x.TimeOn))
            {
                CQso v = View(q);
                i++;
                string key = c.Dupe == "once" ? v.Call : c.Dupe == "band" ? v.Call + v.Band : c.Dupe == "mode" ? v.Call + v.Mode : v.Call + v.Band + v.Mode;
                if (!seen.Add(key)) continue;
                bool sameC = v.Entity == myEnt, sameCont = v.Cont == myCont, low = v.Band == "160m" || v.Band == "80m" || v.Band == "40m";
                bool wve = v.Entity == "United States" || v.Entity == "Canada" || v.Entity == "Alaska" || v.Entity == "Hawaii";
                string st = (v.Get("state").Length > 0 ? v.Get("state") : v.Get("sec")).ToUpperInvariant();
                Func<int> km = () => (int)Geo.DistanceKm(myLat, myLon, v.Lat, v.Lon);
                string grid2 = v.Get("grid").Length >= 2 ? v.Get("grid").Substring(0, 2) : "";
                int p = 0;
                List<string> m = new List<string>();
                switch (c.Scoring)
                {
                    case "cqww": p = sameC ? 0 : !sameCont ? 3 : (myCont == "NA" ? 2 : 1); m.Add("Z" + v.Cq + " " + v.Band); m.Add(v.Entity + " " + v.Band); break;
                    case "wpx": p = !sameCont ? (low ? 6 : 3) : sameC ? (low ? 2 : 1) : (myCont == "NA" ? (low ? 4 : 2) : (low ? 2 : 1)); m.Add(v.Prefix); break;
                    case "arrldx": p = wve ? 0 : 3; if (!wve) m.Add(v.Entity + " " + v.Band); break;
                    case "ss": p = wve ? 2 : 0; if (Field.Sections.Contains(st)) m.Add(st); break;
                    case "fd": p = v.Mode == "PH" ? 1 : 2; break;
                    case "arrl10": p = v.Mode == "PH" ? 2 : 4; m.Add((wve ? st : v.Entity) + " " + v.Mode); break;
                    case "arrl160": p = wve ? 2 : 5; m.Add(wve ? st : v.Entity); break;
                    case "rttyru": p = 1; m.Add(wve ? st : v.Entity); break;
                    case "dist500": p = 1 + km() / 500; if (c.Id != "stew" && grid2.Length == 2) m.Add(grid2 + " " + v.Band); break;
                    case "dist3000": p = 1 + km() / 3000; if (grid2.Length == 2) m.Add(grid2 + " " + v.Band); break;
                    case "vhf": p = v.Band == "6m" || v.Band == "2m" ? 1 : 2; if (v.Get("grid").Length >= 4) m.Add(v.Get("grid").Substring(0, 4) + " " + v.Band); break;
                    case "naqp": p = 1; if (v.Cont == "NA") m.Add((wve ? st : v.Entity) + " " + v.Band); break;
                    case "sprint": p = 1; if (v.Cont == "NA") m.Add(wve ? st : v.Entity); break;
                    case "iaru": p = v.Itu == myItu ? 1 : sameCont ? 3 : 5; m.Add("ITU" + v.Itu + " " + v.Band + " " + v.Mode); break;
                    case "cq160": p = sameC ? 2 : sameCont ? 5 : 10; m.Add(wve ? st : v.Entity); break;
                    case "cwt": p = 1; m.Add(v.Call); break;
                    case "sst": p = 1; m.Add(wve ? st : v.Entity); break;
                    case "qp": p = v.Mode == "PH" ? 1 : 2; if (v.Get("loc").Length > 0) m.Add(v.Get("loc")); break;
                    case "wae": p = v.Cont == "EU" ? 1 : 0; if (v.Cont == "EU") m.Add(v.Entity + " " + v.Band); break;
                    case "aadx": p = v.Cont == "AS" ? (myCont == "AS" ? 1 : 3) : 0; if (v.Cont == "AS") m.Add(v.Prefix + " " + v.Band); break;
                    case "jidx": p = v.Entity == "Japan" ? (low ? 2 : 1) : 0; if (v.Entity == "Japan") m.Add(v.Get("zone") + " " + v.Band); break;
                    default: p = sameC ? 1 : sameCont ? 2 : 3; m.Add(v.Entity + " " + v.Band); break;
                }
                s.Qsos++;
                s.Points += p;
                List<string> fresh = m.Where(x => x.Trim().Length > 0 && !x.StartsWith(" ") && s.MultSet.Add(x)).ToList();
                if (i == session.Count) { s.NewMults = fresh; s.LastPoints = p; }
            }
            s.Mults = s.MultSet.Count;
            s.Total = s.FieldDay ? s.Points * 2 : c.Id == "stew" ? s.Points : s.Points * Math.Max(1, s.Mults);
            return s;
        }

        // rate: QSOs an hour over the last 10, and in the last hour
        public static void Rate(List<Qso> session, out int last10, out int lastHour)
        {
            List<DateTime> t = session.Select(q => q.TimeOn).OrderBy(x => x).ToList();
            last10 = 0; lastHour = 0;
            if (t.Count == 0) return;
            DateTime end = t[t.Count - 1];
            lastHour = t.Count(x => (end - x).TotalMinutes <= 60);
            if (t.Count >= 10) { double min = Math.Max(1, (end - t[t.Count - 10]).TotalMinutes); last10 = (int)Math.Round(10 / min * 60); }
        }

        // the multipliers that are a fixed list, and which are still to work (on this band, for per-band ones)
        public List<string> Needed(Score s, string band, out string what)
        {
            ContestDef c = Current;
            IEnumerable<string> pool;
            bool perBand = false;
            switch (c.Scoring)
            {
                case "ss": case "arrl160": pool = Field.Sections; what = "ARRL / RAC sections"; break;
                case "naqp": case "sst": case "sprint": case "rttyru": case "cq160": case "arrl10": pool = Field.States.Concat(Field.Provinces); what = "states and provinces"; perBand = c.Scoring == "naqp"; break;
                case "cqww": pool = Enumerable.Range(1, 40).Select(z => "Z" + z); what = "CQ zones on " + band; perBand = true; break;
                default: what = ""; return new List<string>();
            }
            HashSet<string> have = new HashSet<string>(s.MultSet.Where(x => !perBand || x.EndsWith(" " + band)).Select(x => x.Split(' ')[0]));
            return pool.Where(x => !have.Contains(x)).ToList();
        }

        // ---- Cabrillo 3.0 ----
        public string Cabrillo(List<Qso> session, Dictionary<string, string> header, string version)
        {
            ContestDef c = Current;
            Score s = Compute(session);
            StringBuilder sb = new StringBuilder();
            sb.Append("START-OF-LOG: 3.0\r\n");
            sb.Append("CONTEST: ").Append(c.Cabrillo).Append("\r\n");
            sb.Append("CALLSIGN: ").Append(Book.MyCall).Append("\r\n");
            foreach (KeyValuePair<string, string> kv in header)
                if (!string.IsNullOrWhiteSpace(kv.Value))
                    foreach (string line in kv.Value.Replace("\r", "").Split('\n')) sb.Append(kv.Key).Append(": ").Append(line).Append("\r\n");
            sb.Append("CLAIMED-SCORE: ").Append(s.Total.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            if (Book.MyGrid.Length >= 4) sb.Append("GRID-LOCATOR: ").Append(Book.MyGrid).Append("\r\n");
            sb.Append("CREATED-BY: Kainos Logbook ").Append(version).Append("\r\n");
            foreach (Qso q in session.OrderBy(x => x.TimeOn))
            {
                double khz = q.FreqMhz * 1000;
                string f = khz > 0 ? ((int)Math.Round(khz)).ToString(CultureInfo.InvariantCulture) : bandFreq(q.Band);
                sb.Append("QSO: ").Append(f.PadLeft(5)).Append(' ').Append(ModeOf(q)).Append(' ')
                  .Append(q.TimeOn.ToString("yyyy-MM-dd HHmm", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(Book.MyCall.PadRight(13)).Append(' ').Append(pad(q.Get("STX_STRING"), c.Sent.Length)).Append(' ')
                  .Append(q.Call.PadRight(13)).Append(' ').Append(pad(q.Get("SRX_STRING"), c.Exch.Length)).Append("\r\n");
            }
            sb.Append("END-OF-LOG:\r\n");
            return sb.ToString();
        }

        private static string pad(string exch, int n)
        {
            string[] parts = (exch ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts.Select(p => p.PadRight(p.Length <= 3 ? 3 : 6)));
        }

        // a band's Cabrillo frequency when the exact one isn't known (VHF and up by band)
        private static string bandFreq(string band)
        {
            switch (band)
            {
                case "160m": return "1800"; case "80m": return "3500"; case "40m": return "7000"; case "20m": return "14000"; case "15m": return "21000"; case "10m": return "28000";
                case "6m": return "50"; case "2m": return "144"; case "1.25m": return "222"; case "70cm": return "432";
            }
            return "0";
        }

        // ---- EDI (REG1TEST), the European VHF format ----
        public string Edi(List<Qso> session)
        {
            List<Qso> v = session.Where(q => q.Band == "6m" || q.Band == "2m" || q.Band == "70cm" || q.Band == "1.25m").OrderBy(q => q.TimeOn).ToList();
            double myLat, myLon;
            Book.MyLatLon(out myLat, out myLon);
            string grid6 = Book.MyGrid.Length >= 6 ? Book.MyGrid : Book.MyGrid + "MM";
            string d = v.Count > 0 ? v[0].TimeOn.ToString("yyyyMMdd", CultureInfo.InvariantCulture) : DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            string bandName = v.Any(q => q.Band == "2m") ? "144 MHz" : v.Any(q => q.Band == "70cm") ? "432 MHz" : "50 MHz";
            StringBuilder sb = new StringBuilder();
            sb.Append("[REG1TEST;1]\r\nTName=").Append(Current.Name).Append("\r\nTDate=").Append(d).Append(';').Append(d).Append("\r\nPCall=").Append(Book.MyCall)
              .Append("\r\nPWWLo=").Append(grid6).Append("\r\nPSect=SINGLE\r\nPBand=").Append(bandName).Append("\r\nRCall=").Append(Book.MyCall)
              .Append("\r\nSPowe=").Append(Book.MyPower).Append("\r\nCQSOs=").Append(v.Count).Append(";1\r\n[Remarks]\r\nCreated by Kainos Logbook\r\n[QSORecords;").Append(v.Count).Append("]\r\n");
            Dictionary<string, int> modeCode = new Dictionary<string, int> { { "SSB", 1 }, { "CW", 2 }, { "AM", 5 }, { "FM", 6 }, { "RTTY", 7 } };
            int nr = 0;
            foreach (Qso q in v)
            {
                nr++;
                string g = q.Grid.Length >= 6 ? q.Grid.Substring(0, 6) : q.Grid.Length >= 4 ? q.Grid.Substring(0, 4) + "MM" : "";
                double la, lo;
                int km = Geo.GridToLatLon(g, out la, out lo) ? (int)Geo.DistanceKm(myLat, myLon, la, lo) : 0;
                int mc; modeCode.TryGetValue(q.Mode, out mc);
                string[] f = { q.TimeOn.ToString("yyMMdd", CultureInfo.InvariantCulture), q.TimeOn.ToString("HHmm", CultureInfo.InvariantCulture), q.Call, mc.ToString(CultureInfo.InvariantCulture),
                    q.RstSent, q.Get("STX").Length > 0 ? q.Get("STX").PadLeft(3, '0') : nr.ToString("000", CultureInfo.InvariantCulture), q.RstRcvd, q.Get("SRX").PadLeft(q.Get("SRX").Length > 0 ? 3 : 0, '0'), "", g, km.ToString(CultureInfo.InvariantCulture), "", "", "", "" };
                sb.Append(string.Join(";", f)).Append("\r\n");
            }
            return sb.ToString();
        }
    }
}
