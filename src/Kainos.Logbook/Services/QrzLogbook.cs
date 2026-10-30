/*  QrzLogbook.cs

The QRZ.com Logbook, with your logbook's API key (QRZ.com > Logbook > Settings): ACTION=INSERT for each QSO
(OPTION=REPLACE when it was sent before and edited since). QRZ answers RESULT=OK with a LOGID, or RESULT=FAIL with
a reason (a duplicate counts as sent).

Confirmations: ACTION=FETCH with STATUS:CONFIRMED, modified since the last time, 250 at a time.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Kainos.Logbook.Lookup;
using Kainos.Modules;

namespace Kainos.Logbook.Services
{
    internal sealed class QrzLogbook : Service
    {
        public QrzLogbook(IKainosHost host, HttpClient http) : base(host, http) { }

        public override string Key { get { return "qrz"; } }
        public override string Name { get { return "QRZ Logbook"; } }
        public override bool Confirms { get { return true; } }
        public override int BatchSize { get { return 1; } }

        public override bool IsSent(Qso q) { return q.Get("QRZCOM_QSO_UPLOAD_STATUS") == "Y" && q.Get("APP_KAINOS_QRZ_DIRTY") != "Y"; }
        public override void MarkSent(Qso q) { q.Set("QRZCOM_QSO_UPLOAD_STATUS", "Y"); q.Set("QRZCOM_QSO_UPLOAD_DATE", Date(DateTime.UtcNow)); q.Set("APP_KAINOS_QRZ_DIRTY", null); }
        public override bool HasLogin { get { return Secrets.Get(Host, "qrzlog.key").Length > 0; } }

        private string api { get { return Url("https://logbook.qrz.com", "/api"); } }

        private static Dictionary<string, string> form(Qso q, string key)
        {
            Dictionary<string, string> f = new Dictionary<string, string> { { "KEY", key }, { "ACTION", "INSERT" }, { "ADIF", Record(q) } };
            if (q.Get("QRZCOM_QSO_UPLOAD_STATUS") == "Y") f["OPTION"] = "REPLACE";
            return f;
        }

        public override string Preview(List<Qso> qsos)
        {
            string key = Secrets.Get(Host, "qrzlog.key");
            StringBuilder sb = new StringBuilder("Each QSO is posted to " + api + ":\r\n\r\n");
            foreach (Qso q in qsos)
                sb.Append(string.Join("&", form(q, key).Select(kv => kv.Key + "=" + Mask(kv.Value, key)))).Append("\r\n");
            return sb.ToString();
        }

        // QRZ's answers are form-encoded: RESULT=OK&LOGID=..&COUNT=1 (the ADIF of a FETCH comes last)
        internal static Dictionary<string, string> Parse(string body)
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int adif = body.IndexOf("ADIF=", StringComparison.OrdinalIgnoreCase);
            string head = adif >= 0 ? body.Substring(0, adif) : body;
            foreach (string part in head.Split('&'))
            {
                int eq = part.IndexOf('=');
                if (eq > 0) d[part.Substring(0, eq).Trim()] = WebUtility.UrlDecode(part.Substring(eq + 1));
            }
            if (adif >= 0)
            {
                string a = WebUtility.UrlDecode(body.Substring(adif + 5));
                if (a.IndexOf("&lt;", StringComparison.OrdinalIgnoreCase) >= 0) a = WebUtility.HtmlDecode(a);
                d["ADIF"] = a;
            }
            return d;
        }

        private async Task<Dictionary<string, string>> post(Dictionary<string, string> f)
        {
            HttpResponseMessage resp = await Http.PostAsync(api, new FormUrlEncodedContent(f)).ConfigureAwait(false);
            return Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
        }

        public override async Task<SendResult> SendAsync(List<Qso> qsos)
        {
            SendResult r = new SendResult();
            string key = Secrets.Get(Host, "qrzlog.key");
            foreach (Qso q in qsos)
            {
                Dictionary<string, string> a;
                try { a = await post(form(q, key)).ConfigureAwait(false); }
                catch (Exception ex) { r.ByQso[q.Id] = Outcome.Failed; r.Message = "couldn't reach QRZ: " + (ex.InnerException ?? ex).Message; continue; }
                string result, reason;
                a.TryGetValue("RESULT", out result);
                a.TryGetValue("REASON", out reason);
                reason = reason ?? "";
                if (result == "OK" || result == "REPLACE") { r.ByQso[q.Id] = Outcome.Sent; r.Message = "sent"; string id; if (a.TryGetValue("LOGID", out id)) r.Keep(q.Id, "APP_KAINOS_QRZ_LOGID", id); }
                else if (reason.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0) { r.ByQso[q.Id] = Outcome.Duplicate; r.Message = "already in the QRZ logbook"; }
                else if (result == "AUTH" || reason.IndexOf("invalid api key", StringComparison.OrdinalIgnoreCase) >= 0 || reason.IndexOf("access denied", StringComparison.OrdinalIgnoreCase) >= 0)
                { r.All(qsos.Where(x => !r.ByQso.ContainsKey(x.Id)), Outcome.LoginFailed); r.Message = "QRZ didn't accept the API key" + (reason.Length > 0 ? " (" + reason + ")" : ""); return r; }
                else { r.ByQso[q.Id] = Outcome.Failed; r.Message = "QRZ: " + (reason.Length > 0 ? reason : "no reason given"); }
            }
            return r;
        }

        public override async Task<Tuple<List<Qso>, string>> FetchConfirmationsAsync()
        {
            string key = Secrets.Get(Host, "qrzlog.key");
            if (key.Length == 0) return Tuple.Create(new List<Qso>(), "set your QRZ logbook API key first");
            string since = Host.GetSetting("svc.qrz.since.conf", "1900-01-01");
            string started = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            List<Qso> all = new List<Qso>();
            string after = "0";
            for (int page = 0; page < 200; page++)
            {
                Dictionary<string, string> a = await post(new Dictionary<string, string> { { "KEY", key }, { "ACTION", "FETCH" }, { "OPTION", "STATUS:CONFIRMED,MODSINCE:" + since + ",MAX:250,AFTERLOGID:" + after } }).ConfigureAwait(false);
                string result, reason, adif;
                a.TryGetValue("RESULT", out result);
                a.TryGetValue("REASON", out reason);
                if (result != "OK")
                {
                    if ((reason ?? "").IndexOf("no log entries", StringComparison.OrdinalIgnoreCase) >= 0 || (reason ?? "").IndexOf("no records", StringComparison.OrdinalIgnoreCase) >= 0) break;
                    return Tuple.Create(all, "QRZ: " + (reason ?? "no answer"));
                }
                if (!a.TryGetValue("ADIF", out adif) || adif.Length == 0) break;
                List<Qso> got = Adif.Parse(adif);
                all.AddRange(got);
                if (got.Count < 250) break;
                after = got.Max(q => { long id; return long.TryParse(q.Get("APP_QRZLOG_LOGID"), out id) ? id : 0; }).ToString(CultureInfo.InvariantCulture);
            }
            Host.SetSetting("svc.qrz.since.conf", started);
            return Tuple.Create(all, "");
        }

        public override void MarkConfirmed(Qso q, Qso from)
        {
            q.Set("APP_KAINOS_QRZ_CONF", "Y");
            if (q.Get("QRZCOM_QSO_UPLOAD_STATUS").Length == 0) q.Set("QRZCOM_QSO_UPLOAD_STATUS", "Y");
        }
    }
}
