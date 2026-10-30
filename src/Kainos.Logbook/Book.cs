/*  Book.cs

The logbook as the pages use it: the log (LogStore), what country a call is (Cty), online lookups (CallLookup), and
what's been worked: for a call being typed, its country's QSOs by band and mode, and whether it's a new one, a new
band or a new mode. "Confirmed" follows the choice in the settings (LoTW + paper cards by default: what ARRL and CQ
credit).

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Kainos.Logbook.Lookup;
using Kainos.Modules;

namespace Kainos.Logbook
{
    internal enum Slot { None, Worked, Confirmed }

    internal sealed class Worked
    {
        public CtyInfo Cty;
        public List<Qso> SameCall = new List<Qso>();
        public List<Qso> SameEntity = new List<Qso>();
        public bool NewOne, NewBand, NewMode, EntityConfirmed, DupeToday;
        public readonly Dictionary<string, Slot[]> Grid = new Dictionary<string, Slot[]>();     // band -> [CW, Phone, Digital]
    }

    internal sealed class Book
    {
        public readonly IKainosHost Host;
        public readonly LogStore Store;
        public readonly HttpClient Http;
        public readonly CallLookup Lookup;
        public Cty Cty;
        public readonly string Version;

        public Book(IKainosHost host, string version)
        {
            Host = host;
            Version = version;
            Store = new LogStore(host.DataFolder, version);
            Http = CallLookup.NewHttp(version);
            Lookup = new CallLookup(host, Http);
            bool stale;
            Cty = Cty.Load(host.DataFolder, out stale);
            if (stale && host.GetSetting("cty.auto", "1") == "1") Task.Run(refreshCty);
        }

        public event Action CtyChanged;

        private async Task refreshCty()
        {
            try
            {
                if (await Cty.DownloadAsync(Host.DataFolder, Http).ConfigureAwait(false))
                {
                    bool s;
                    Cty = Cty.Load(Host.DataFolder, out s);
                    Host.Log("cty.dat updated: " + Cty.Entities.Count + " entities");
                    CtyChanged?.Invoke();
                }
            }
            catch (Exception ex) { Host.Log("cty.dat download: " + (ex.InnerException ?? ex).Message); }
        }

        public Task RefreshCtyNow() { return Task.Run(refreshCty); }

        // ---- the station ----
        public string MyCall { get { string c = Host.GetSetting("station.call", ""); return (c.Length > 0 ? c : Host.Callsign ?? "").ToUpperInvariant(); } }
        public string MyGrid { get { string g = Host.GetSetting("station.grid", ""); return (g.Length > 0 ? g : Host.Grid ?? "").ToUpperInvariant(); } }
        public string MyPower { get { return Host.GetSetting("station.power", "100"); } }

        public bool MyLatLon(out double lat, out double lon)
        {
            if (Geo.GridToLatLon(MyGrid, out lat, out lon)) return true;
            CtyInfo me = Cty.Resolve(MyCall);
            if (me != null) { lat = me.Lat; lon = me.Lon; return true; }
            return false;
        }

        // ---- confirmations: which count ("lotw" = LoTW + paper cards, "eqsl", "qrz", "any") ----
        public string ConfirmSource { get { return Host.GetSetting("confirm", "lotw"); } set { Host.SetSetting("confirm", value); } }

        public bool Confirmed(Qso q)
        {
            switch (ConfirmSource)
            {
                case "eqsl": return q.Eqsl == 'c';
                case "qrz": return q.QrzLog == 'c';
                case "any": return q.Lotw == 'c' || q.Eqsl == 'c' || q.QrzLog == 'c' || q.CardReceived;
                default: return q.Lotw == 'c' || q.CardReceived;
            }
        }

        public static string ConfirmName(string src)
        {
            switch (src) { case "eqsl": return "eQSL"; case "qrz": return "QRZ"; case "any": return "Any confirmation"; default: return "LoTW + cards"; }
        }

        // ---- worked-before, for a call on a band and mode ----
        public Worked Analyse(string call, string band, ModeClass cls)
        {
            Worked w = new Worked { Cty = Cty.Resolve(call) };
            string bare = Cty.BaseCall(call, out string _);
            List<Qso> all = Store.All();
            w.SameCall = all.Where(q => Cty.BaseCall(q.Call, out string _) == bare).ToList();
            foreach (string b in Bands.Grid) w.Grid[b] = new Slot[3];
            if (w.Cty == null) return w;
            string ent = w.Cty.Name;
            w.SameEntity = all.Where(q => entityOf(q) == ent).ToList();
            foreach (Qso q in w.SameEntity)
            {
                Slot[] row;
                if (!w.Grid.TryGetValue(q.Band, out row)) continue;
                int c = (int)q.Class;
                Slot s = Confirmed(q) ? Slot.Confirmed : Slot.Worked;
                if (s > row[c]) row[c] = s;
            }
            w.NewOne = w.SameEntity.Count == 0;
            w.NewBand = !w.NewOne && band.Length > 0 && !w.SameEntity.Any(q => q.Band == band);
            w.NewMode = !w.NewOne && !w.SameEntity.Any(q => q.Class == cls);
            w.EntityConfirmed = w.SameEntity.Any(Confirmed);
            DateTime today = DateTime.UtcNow.Date;
            w.DupeToday = w.SameCall.Any(q => q.Band == band && q.Class == cls && q.TimeOn.Date == today);
            return w;
        }

        // the entity a logged QSO is in: what was logged, or worked out from its call
        public string entityOf(Qso q)
        {
            CtyInfo c = Cty.Resolve(q.Call);
            return c != null ? c.Name : q.Country;
        }

        // ---- a new QSO from the radio's VFO A ----
        public Qso NewFromRadio()
        {
            Qso q = new Qso { Id = Qso.NewId() };
            q.TimeOn = DateTime.UtcNow;
            int rx = Host.Radio.TxReceiver;
            double mhz = Host.Radio.VfoHz(rx) / 1e6;
            q.FreqMhz = mhz;
            q.Band = Bands.Of(mhz);
            string mode, sub;
            Bands.ToAdif(Host.Radio.Mode(rx), out mode, out sub);
            q.Mode = mode;
            q.Submode = sub;
            stationFields(q);
            return q;
        }

        public void stationFields(Qso q)
        {
            if (MyCall.Length > 0) { q.Set("STATION_CALLSIGN", MyCall); q.Set("OPERATOR", MyCall); }
            if (MyGrid.Length > 0) q.Set("MY_GRIDSQUARE", MyGrid);
            if (MyPower.Length > 0) q.Set("TX_PWR", MyPower);
        }

        // country, zones and continent from cty.dat, and from a lookup what it found
        public void Fill(Qso q, CallInfo info)
        {
            CtyInfo c = Cty.Resolve(q.Call);
            if (c != null)
            {
                if (q.Country.Length == 0) q.Country = c.Name;
                if (q.CqZone == 0) q.CqZone = c.Cq;
                if (q.Get("ITUZ").Length == 0 && c.Itu > 0) q.Set("ITUZ", c.Itu.ToString(CultureInfo.InvariantCulture));
                if (q.Get("CONT").Length == 0) q.Set("CONT", c.Cont);
            }
            if (info == null) return;
            if (q.Name.Length == 0) q.Name = info.Name;
            if (q.Qth.Length == 0) q.Qth = info.City;
            if (q.State.Length == 0 && (info.Country == "United States" || info.Country == "USA" || info.Country == "Canada")) q.State = info.State;
            if (q.Get("CNTY").Length == 0 && !string.IsNullOrEmpty(info.County) && !string.IsNullOrEmpty(info.State)) q.Set("CNTY", info.State + "," + info.County);
            if (q.Grid.Length == 0) q.Grid = info.Grid;
            if (q.Get("EMAIL").Length == 0) q.Set("EMAIL", info.Email);
            if (q.Get("QSL_VIA").Length == 0) q.Set("QSL_VIA", info.QslVia);
        }
    }
}
