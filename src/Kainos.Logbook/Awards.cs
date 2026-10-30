/*  Awards.cs

What the log adds up to, worked and confirmed (as "Confirmed means" is set: LoTW + paper cards unless changed),
for all bands or one, any mode or CW / phone / digital:
- DXCC: the countries (cty.dat's entities, not its WAE-only ones), by continent; band-countries for the DXCC
  Challenge (160 to 6 m); and 5BDXCC's five bands.
- WAS: the 50 states (from the QSO's state, for the US, Alaska and Hawaii).
- WAZ: the 40 CQ zones.
- Grids: four-character squares, all bands, and on 6 m and 2 m for VUCC (100 each).

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kainos.Logbook
{
    internal sealed class Tally
    {
        public readonly HashSet<string> Worked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Confirmed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public void Add(string key, bool conf) { if (string.IsNullOrEmpty(key)) return; Worked.Add(key); if (conf) Confirmed.Add(key); }
    }

    internal sealed class AwardTotals
    {
        public Tally Dxcc = new Tally(), Was = new Tally(), Waz = new Tally(), Grids = new Tally(), Grids6 = new Tally(), Grids2 = new Tally(), Challenge = new Tally();
        public readonly Dictionary<string, string> ContinentOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, Tally> FiveBand = new Dictionary<string, Tally>();
        public int EntitiesTotal;
        public int Qsos;
    }

    internal static class Awards
    {
        public static readonly string[] States = "AL AK AZ AR CA CO CT DE FL GA HI ID IL IN IA KS KY LA ME MD MA MI MN MS MO MT NE NV NH NJ NM NY NC ND OH OK OR PA RI SC SD TN TX UT VT VA WA WV WI WY".Split(' ');
        public static readonly string[] FiveBands = { "80m", "40m", "20m", "15m", "10m" };
        private static readonly string[] ChallengeBands = { "160m", "80m", "40m", "30m", "20m", "17m", "15m", "12m", "10m", "6m" };

        // band: "" for all; cls: null for any mode
        public static AwardTotals Compute(Book book, List<Qso> log, string band, ModeClass? cls)
        {
            AwardTotals a = new AwardTotals { EntitiesTotal = book.Cty.Entities.Count(e => !e.WaeOnly) };
            foreach (string b in FiveBands) a.FiveBand[b] = new Tally();
            foreach (Qso q in log)
            {
                if (cls.HasValue && q.Class != cls.Value) continue;
                bool conf = book.Confirmed(q);
                CtyInfo ci = book.Cty.Resolve(q.Call);
                string ent = ci != null && !ci.Entity.WaeOnly ? ci.Name : null;
                // these count on any band, so they're kept before the band filter
                if (ent != null && ChallengeBands.Contains(q.Band)) a.Challenge.Add(ent + " " + q.Band, conf);
                if (ent != null && a.FiveBand.ContainsKey(q.Band)) a.FiveBand[q.Band].Add(ent, conf);
                if (band.Length > 0 && q.Band != band) continue;
                a.Qsos++;
                if (ent != null) { a.Dxcc.Add(ent, conf); a.ContinentOf[ent] = ci.Cont; }
                string st = q.State;
                if (ci != null && (ci.Name == "United States" || ci.Name == "Alaska" || ci.Name == "Hawaii"))
                {
                    if (ci.Name == "Alaska") st = "AK";
                    if (ci.Name == "Hawaii") st = "HI";
                    if (States.Contains(st)) a.Was.Add(st, conf);
                }
                int z = q.CqZone > 0 ? q.CqZone : ci != null ? ci.Cq : 0;
                if (z >= 1 && z <= 40) a.Waz.Add(z.ToString(System.Globalization.CultureInfo.InvariantCulture), conf);
                string g = q.Grid.Length >= 4 ? q.Grid.Substring(0, 4).ToUpperInvariant() : "";
                if (Geo.IsGrid(g))
                {
                    a.Grids.Add(g, conf);
                    if (q.Band == "6m") a.Grids6.Add(g, conf);
                    if (q.Band == "2m") a.Grids2.Add(g, conf);
                }
            }
            return a;
        }
    }
}
