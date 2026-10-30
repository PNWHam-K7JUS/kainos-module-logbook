/*  ClubLog.cs

Club Log's real-time upload (realtime.php): each QSO with your Club Log email, an application password and the
callsign whose log it goes in, plus an API key (Club Log gives each program one; paste yours). Club Log answers
with the HTTP status: 200 taken, 400 rejected (the reason in the body), 403 the login or key was refused. An
edited QSO is sent again and Club Log updates it. Club Log doesn't confirm QSOs, so nothing comes back.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Kainos.Logbook.Lookup;
using Kainos.Modules;

namespace Kainos.Logbook.Services
{
    internal sealed class ClubLog : Service
    {
        private readonly Func<string> _myCall;

        public ClubLog(IKainosHost host, HttpClient http, Func<string> myCall) : base(host, http) { _myCall = myCall; }

        public override string Key { get { return "clublog"; } }
        public override string Name { get { return "Club Log"; } }
        public override bool Confirms { get { return false; } }
        public override int BatchSize { get { return 1; } }

        public override bool IsSent(Qso q) { return q.Get("CLUBLOG_QSO_UPLOAD_STATUS") == "Y"; }
        public override void MarkSent(Qso q) { q.Set("CLUBLOG_QSO_UPLOAD_STATUS", "Y"); q.Set("CLUBLOG_QSO_UPLOAD_DATE", Date(DateTime.UtcNow)); }
        public override bool HasLogin { get { return Secrets.Get(Host, "clublog.email").Length > 0 && Secrets.Get(Host, "clublog.pass").Length > 0 && Secrets.Get(Host, "clublog.api").Length > 0; } }

        private string call { get { string c = Host.GetSetting("svc.clublog.call", ""); return c.Length > 0 ? c : _myCall(); } }

        private Dictionary<string, string> form(Qso q)
        {
            return new Dictionary<string, string>
            {
                { "email", Secrets.Get(Host, "clublog.email") }, { "password", Secrets.Get(Host, "clublog.pass") },
                { "callsign", call }, { "api", Secrets.Get(Host, "clublog.api") }, { "adif", Record(q) },
            };
        }

        public override string Preview(List<Qso> qsos)
        {
            string p = Secrets.Get(Host, "clublog.pass"), k = Secrets.Get(Host, "clublog.api");
            StringBuilder sb = new StringBuilder("Each QSO is posted to " + Url("https://clublog.org", "/realtime.php") + ":\r\n\r\n");
            foreach (Qso q in qsos) sb.Append(string.Join("&", form(q).Select(kv => kv.Key + "=" + Mask(kv.Value, p, k)))).Append("\r\n");
            return sb.ToString();
        }

        public override async Task<SendResult> SendAsync(List<Qso> qsos)
        {
            SendResult r = new SendResult();
            foreach (Qso q in qsos)
            {
                HttpResponseMessage resp;
                string body;
                try
                {
                    resp = await Http.PostAsync(Url("https://clublog.org", "/realtime.php"), new FormUrlEncodedContent(form(q))).ConfigureAwait(false);
                    body = (await resp.Content.ReadAsStringAsync().ConfigureAwait(false)).Trim();
                }
                catch (Exception ex) { r.ByQso[q.Id] = Outcome.Failed; r.Message = "couldn't reach Club Log: " + (ex.InnerException ?? ex).Message; continue; }
                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    bool dupe = body.IndexOf("dupe", StringComparison.OrdinalIgnoreCase) >= 0;
                    r.ByQso[q.Id] = dupe ? Outcome.Duplicate : Outcome.Sent;
                    r.Message = dupe ? "already on Club Log" : "sent";
                }
                else if (resp.StatusCode == HttpStatusCode.Forbidden)
                { r.All(qsos.Where(x => !r.ByQso.ContainsKey(x.Id)), Outcome.LoginFailed); r.Message = "Club Log refused the login or API key" + (body.Length > 0 ? " (" + shorten(body) + ")" : ""); return r; }
                else { r.ByQso[q.Id] = Outcome.Failed; r.Message = "Club Log: " + (body.Length > 0 ? shorten(body) : ((int)resp.StatusCode).ToString()); }
            }
            return r;
        }

        private static string shorten(string s) { return s.Length > 160 ? s.Substring(0, 160) + "…" : s; }
    }
}
