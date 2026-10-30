/*  Export.cs

The log in the formats other programs and sites take: ADIF (LoTW through TQSL, eQSL, QRZ, Club Log, other
loggers), ADX (ADIF as XML) and CSV (a spreadsheet). The contest and award formats (Cabrillo, EDI, POTA, WWFF,
SOTA) come with those pages.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;

namespace Kainos.Logbook
{
    internal sealed class ExportFilter
    {
        public DateTime? From, To;      // dates, UTC, inclusive
        public string Band;             // "" for all
        public ModeClass? Class;

        public IEnumerable<Qso> Apply(IEnumerable<Qso> qsos)
        {
            return qsos.Where(q =>
                (!From.HasValue || q.TimeOn.Date >= From.Value.Date) &&
                (!To.HasValue || q.TimeOn.Date <= To.Value.Date) &&
                (string.IsNullOrEmpty(Band) || q.Band == Band) &&
                (!Class.HasValue || q.Class == Class.Value));
        }
    }

    internal static class Export
    {
        public static readonly string[][] Formats =
        {
            new[] { "adif", "ADIF (.adi)", "adi", "ADIF|*.adi" },
            new[] { "adx", "ADX (ADIF as XML)", "adx", "ADX|*.adx" },
            new[] { "csv", "CSV (spreadsheet)", "csv", "CSV|*.csv" },
        };

        public static string Make(string format, IEnumerable<Qso> qsos, string version)
        {
            switch (format)
            {
                case "adx": return Adx(qsos, version);
                case "csv": return Csv(qsos);
                default: return Adif.Write(qsos, version, Adif.Public);
            }
        }

        public static string Adx(IEnumerable<Qso> qsos, string version)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<ADX>\r\n <HEADER><ADIF_VER>3.1.4</ADIF_VER><PROGRAMID>Kainos Logbook</PROGRAMID><PROGRAMVERSION>")
              .Append(SecurityElement.Escape(version)).Append("</PROGRAMVERSION></HEADER>\r\n <RECORDS>\r\n");
            foreach (Qso q in qsos)
            {
                sb.Append("  <RECORD>");
                foreach (KeyValuePair<string, string> kv in q.F.OrderBy(k => k.Key))
                {
                    // application fields (APP_...) are written differently in ADX; they're left out
                    if (kv.Key.StartsWith("APP_", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(kv.Value)) continue;
                    sb.Append('<').Append(kv.Key).Append('>').Append(SecurityElement.Escape(kv.Value)).Append("</").Append(kv.Key).Append('>');
                }
                sb.Append("</RECORD>\r\n");
            }
            sb.Append(" </RECORDS>\r\n</ADX>\r\n");
            return sb.ToString();
        }

        public static string Csv(IEnumerable<Qso> qsos)
        {
            StringBuilder sb = new StringBuilder("Date,Time (UTC),Call,Band,MHz,Mode,RST sent,RST rcvd,Name,QTH,State,Grid,Country,CQ zone,Park,LoTW,eQSL,QRZ,Club Log,Paper card,Comment\r\n");
            Func<char, string> st = c => c == 'c' ? "confirmed" : c == 'u' ? "uploaded" : "";
            foreach (Qso q in qsos)
            {
                DateTime t = q.TimeOn;
                string[] cells =
                {
                    t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), t.ToString("HH:mm", CultureInfo.InvariantCulture), q.Call, q.Band,
                    q.FreqMhz > 0 ? q.FreqMhz.ToString("0.0000", CultureInfo.InvariantCulture) : "", q.ModeShown, q.RstSent, q.RstRcvd, q.Name, q.Qth,
                    q.State, q.Grid, q.Country, q.CqZone > 0 ? q.CqZone.ToString(CultureInfo.InvariantCulture) : "", q.PotaRef,
                    st(q.Lotw), st(q.Eqsl), st(q.QrzLog), st(q.ClubLog), q.CardReceived ? "received" : "", q.Comment,
                };
                sb.Append(string.Join(",", cells.Select(csvCell))).Append("\r\n");
            }
            return sb.ToString();
        }

        private static string csvCell(string v)
        {
            v = v ?? "";
            return v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }
    }
}
