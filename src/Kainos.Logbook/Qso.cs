/*  Qso.cs

One QSO: its ADIF fields, by name (so nothing another program wrote is lost on the way through), with the ones the
logbook uses as properties. Each QSO has an id of its own (APP_KAINOS_ID) so edits and deletes find it again.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kainos.Logbook
{
    internal sealed class Qso
    {
        public readonly Dictionary<string, string> F = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Get(string key) { string v; return F.TryGetValue(key, out v) ? v : ""; }
        public void Set(string key, string value)
        {
            if (string.IsNullOrEmpty(value)) F.Remove(key);
            else F[key.ToUpperInvariant()] = value;
        }

        public Qso Clone()
        {
            Qso q = new Qso();
            foreach (KeyValuePair<string, string> kv in F) q.F[kv.Key] = kv.Value;
            return q;
        }

        public string Id { get { return Get("APP_KAINOS_ID"); } set { Set("APP_KAINOS_ID", value); } }
        public string Call { get { return Get("CALL").ToUpperInvariant(); } set { Set("CALL", value == null ? null : value.ToUpperInvariant()); } }
        public string Band { get { return Get("BAND").ToLowerInvariant(); } set { Set("BAND", value); } }
        public string Mode { get { return Get("MODE").ToUpperInvariant(); } set { Set("MODE", value); } }
        public string Submode { get { return Get("SUBMODE").ToUpperInvariant(); } set { Set("SUBMODE", value); } }
        public string RstSent { get { return Get("RST_SENT"); } set { Set("RST_SENT", value); } }
        public string RstRcvd { get { return Get("RST_RCVD"); } set { Set("RST_RCVD", value); } }
        public string Name { get { return Get("NAME"); } set { Set("NAME", value); } }
        public string Qth { get { return Get("QTH"); } set { Set("QTH", value); } }
        public string State { get { return Get("STATE").ToUpperInvariant(); } set { Set("STATE", value); } }
        public string Grid { get { return Get("GRIDSQUARE").ToUpperInvariant(); } set { Set("GRIDSQUARE", value); } }
        public string Country { get { return Get("COUNTRY"); } set { Set("COUNTRY", value); } }
        public string Comment { get { return Get("COMMENT"); } set { Set("COMMENT", value); } }
        public string PotaRef { get { string p = Get("POTA_REF"); if (p.Length == 0 && Get("SIG").Equals("POTA", StringComparison.OrdinalIgnoreCase)) p = Get("SIG_INFO"); return p.ToUpperInvariant(); } }

        public int CqZone { get { int z; return int.TryParse(Get("CQZ"), out z) ? z : 0; } set { Set("CQZ", value > 0 ? value.ToString(CultureInfo.InvariantCulture) : null); } }
        public int Dxcc { get { int z; return int.TryParse(Get("DXCC"), out z) ? z : 0; } }

        // the mode as the log shows it: FT4 is MFSK with a submode in ADIF
        public string ModeShown { get { return Submode.Length > 0 ? Submode : Mode; } }
        public ModeClass Class { get { return Bands.ClassOf(ModeShown); } }

        public double FreqMhz
        {
            get { double f; return double.TryParse(Get("FREQ"), NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : 0; }
            set { Set("FREQ", value > 0 ? value.ToString("0.000000", CultureInfo.InvariantCulture) : null); }
        }

        // QSO_DATE + TIME_ON, UTC; MinValue if missing
        public DateTime TimeOn
        {
            get
            {
                string d = Get("QSO_DATE"), t = Get("TIME_ON");
                if (d.Length != 8) return DateTime.MinValue;
                if (t.Length == 4) t += "00";
                if (t.Length != 6) t = "000000";
                DateTime r;
                return DateTime.TryParseExact(d + t, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out r) ? r : DateTime.MinValue;
            }
            set
            {
                Set("QSO_DATE", value.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                Set("TIME_ON", value.ToString("HHmmss", CultureInfo.InvariantCulture));
            }
        }

        // QSL state per service: 'c' confirmed, 'u' sent (uploaded), '' neither
        public char Lotw { get { return Get("LOTW_QSL_RCVD") == "Y" ? 'c' : Get("LOTW_QSL_SENT") == "Y" ? 'u' : '\0'; } }
        public char Eqsl { get { return Get("EQSL_QSL_RCVD") == "Y" ? 'c' : Get("EQSL_QSL_SENT") == "Y" ? 'u' : '\0'; } }
        public char QrzLog { get { return Get("APP_KAINOS_QRZ_CONF") == "Y" ? 'c' : Get("QRZCOM_QSO_UPLOAD_STATUS") == "Y" ? 'u' : '\0'; } }
        public char ClubLog { get { return Get("CLUBLOG_QSO_UPLOAD_STATUS") == "Y" ? 'u' : '\0'; } }
        public bool CardReceived { get { return Get("QSL_RCVD") == "Y"; } set { Set("QSL_RCVD", value ? "Y" : null); if (value) Set("QSLRDATE", DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); else Set("QSLRDATE", null); } }

        // logged here (the LOG page, or a QSO from the FT8 / SSTV modules), not imported: only these are uploaded by
        // themselves; an imported log is sent only if asked for
        public bool LoggedHere { get { return Get("APP_KAINOS_NEW") == "Y"; } set { Set("APP_KAINOS_NEW", value ? "Y" : null); } }

        public DateTime AddedUtc
        {
            get
            {
                DateTime r;
                return DateTime.TryParseExact(Get("APP_KAINOS_ADDED"), "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out r) ? r : DateTime.MinValue;
            }
        }

        public static string NewId() { return Guid.NewGuid().ToString("N").Substring(0, 16); }
    }
}
