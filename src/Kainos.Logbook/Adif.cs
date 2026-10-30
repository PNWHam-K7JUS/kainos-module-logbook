/*  Adif.cs

ADIF (.adi) reading and writing. Reading is by each field's length, so values may hold anything (angle brackets,
new lines); a field's type indicator (<CALL:5:S>) is accepted and ignored. Writing puts each QSO on one line.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kainos.Logbook
{
    internal static class Adif
    {
        public static List<Qso> Parse(string text)
        {
            List<Qso> list = new List<Qso>();
            if (string.IsNullOrEmpty(text)) return list;
            int i = 0;
            // a header is anything before <EOH>; a file without one starts with a field
            if (text.Length > 0 && text[0] != '<')
            {
                int eoh = text.IndexOf("<eoh>", StringComparison.OrdinalIgnoreCase);
                if (eoh >= 0) i = eoh + 5;
            }
            else
            {
                int eoh = text.IndexOf("<eoh>", StringComparison.OrdinalIgnoreCase);
                int firstCall = text.IndexOf("<call:", StringComparison.OrdinalIgnoreCase);
                if (eoh >= 0 && (firstCall < 0 || eoh < firstCall)) i = eoh + 5;
            }
            Qso cur = new Qso();
            while (i < text.Length)
            {
                int lt = text.IndexOf('<', i);
                if (lt < 0) break;
                int gt = text.IndexOf('>', lt + 1);
                if (gt < 0) break;
                string tag = text.Substring(lt + 1, gt - lt - 1);
                i = gt + 1;
                string[] parts = tag.Split(':');
                string name = parts[0].Trim().ToUpperInvariant();
                if (name == "EOR")
                {
                    if (cur.F.Count > 0) list.Add(cur);
                    cur = new Qso();
                    continue;
                }
                int len;
                if (parts.Length < 2 || !int.TryParse(parts[1].Trim(), out len) || len < 0) continue;
                if (i + len > text.Length) len = text.Length - i;
                string value = text.Substring(i, len);
                i += len;
                if (name.Length > 0) cur.F[name] = value;
            }
            return list;
        }

        public static string Header(string programVersion)
        {
            return "Kainos Logbook\r\n<ADIF_VER:5>3.1.4 <PROGRAMID:14>Kainos Logbook <PROGRAMVERSION:" + programVersion.Length + ">" + programVersion + " <EOH>\r\n";
        }

        // one QSO, one line; the fields in a familiar order, then the rest
        private static readonly string[] Order = { "CALL", "QSO_DATE", "TIME_ON", "TIME_OFF", "BAND", "FREQ", "MODE", "SUBMODE", "RST_SENT", "RST_RCVD", "NAME", "QTH", "STATE", "CNTY", "GRIDSQUARE", "COUNTRY", "DXCC", "CQZ", "ITUZ", "CONT" };

        public static string Record(Qso q, Func<string, bool> include = null)
        {
            StringBuilder sb = new StringBuilder();
            HashSet<string> done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string k in Order.Concat(q.F.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)))
            {
                if (!done.Add(k)) continue;
                if (include != null && !include(k)) continue;
                string v;
                if (!q.F.TryGetValue(k, out v) || string.IsNullOrEmpty(v)) continue;
                sb.Append('<').Append(k.ToUpperInvariant()).Append(':').Append(v.Length).Append('>').Append(v).Append(' ');
            }
            sb.Append("<EOR>");
            return sb.ToString();
        }

        public static string Write(IEnumerable<Qso> qsos, string programVersion, Func<string, bool> include = null)
        {
            StringBuilder sb = new StringBuilder(Header(programVersion));
            foreach (Qso q in qsos) sb.Append(Record(q, include)).Append("\r\n");
            return sb.ToString();
        }

        // fields that are the logbook's own bookkeeping, left out of exports for other programs
        public static bool Public(string field) { return !field.StartsWith("APP_KAINOS_", StringComparison.OrdinalIgnoreCase); }
    }
}
