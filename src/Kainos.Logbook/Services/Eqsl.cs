/*  Eqsl.cs

eQSL.cc: each QSO is sent to its ADIF import (importADIF.cfm) with your eQSL login in the ADIF header. eQSL
answers in a web page: "1 out of 1 records added", a duplicate warning, or an error.

Confirmations are the eQSLs in your inbox (DownloadInBox.cfm): eQSL makes an ADIF file of those received since
the last time and links to it.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Kainos.Logbook.Lookup;
using Kainos.Modules;

namespace Kainos.Logbook.Services
{
    internal sealed class Eqsl : Service
    {
        public Eqsl(IKainosHost host, HttpClient http) : base(host, http) { }

        public override string Key { get { return "eqsl"; } }
        public override string Name { get { return "eQSL"; } }
        public override bool Confirms { get { return true; } }
        public override int BatchSize { get { return 1; } }

        public override bool IsSent(Qso q) { return q.Get("EQSL_QSL_SENT") == "Y" || q.Get("EQSL_QSL_RCVD") == "Y"; }
        public override void MarkSent(Qso q) { q.Set("EQSL_QSL_SENT", "Y"); q.Set("EQSL_QSLSDATE", Date(DateTime.UtcNow)); }
        public override bool HasLogin { get { return Secrets.Get(Host, "eqsl.user").Length > 0 && Secrets.Get(Host, "eqsl.pass").Length > 0; } }

        private static string field(string name, string v) { return "<" + name + ":" + v.Length + ">" + v; }

        private string payload(Qso q, string user, string pass)
        {
            return field("ADIF_VER", "3.1.4") + field("EQSL_USER", user) + field("EQSL_PSWD", pass) + "<EOH>" + Record(q);
        }

        public override string Preview(List<Qso> qsos)
        {
            string u = Secrets.Get(Host, "eqsl.user"), p = Secrets.Get(Host, "eqsl.pass");
            StringBuilder sb = new StringBuilder("Each QSO is posted to " + Url("https://www.eqsl.cc", "/qslcard/importADIF.cfm") + " as ADIFData:\r\n\r\n");
            foreach (Qso q in qsos) sb.Append(Mask(payload(q, u, p), p)).Append("\r\n");
            return sb.ToString();
        }

        public override async Task<SendResult> SendAsync(List<Qso> qsos)
        {
            SendResult r = new SendResult();
            string u = Secrets.Get(Host, "eqsl.user"), p = Secrets.Get(Host, "eqsl.pass");
            foreach (Qso q in qsos)
            {
                string page;
                try
                {
                    HttpResponseMessage resp = await Http.PostAsync(Url("https://www.eqsl.cc", "/qslcard/importADIF.cfm"),
                        new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("ADIFData", payload(q, u, p)) })).ConfigureAwait(false);
                    page = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                catch (Exception ex) { r.ByQso[q.Id] = Outcome.Failed; r.Message = "couldn't reach eQSL: " + (ex.InnerException ?? ex).Message; continue; }
                string text = Regex.Replace(page, "<[^>]+>", " ");
                if (Regex.IsMatch(text, @"\b1 out of 1 records? added", RegexOptions.IgnoreCase)) { r.ByQso[q.Id] = Outcome.Sent; r.Message = "sent"; }
                else if (text.IndexOf("Duplicate", StringComparison.OrdinalIgnoreCase) >= 0) { r.ByQso[q.Id] = Outcome.Duplicate; r.Message = "already on eQSL"; }
                else if (text.IndexOf("No match on eQSL_User", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0)
                { r.All(qsos.Where(x => !r.ByQso.ContainsKey(x.Id)), Outcome.LoginFailed); r.Message = "eQSL didn't accept the username or password"; return r; }
                else
                {
                    Match m = Regex.Match(text, @"Error:\s*([^\r\n]{1,160})", RegexOptions.IgnoreCase);
                    r.ByQso[q.Id] = Outcome.Failed;
                    r.Message = m.Success ? "eQSL: " + m.Groups[1].Value.Trim() : "eQSL gave an answer the logbook didn't understand";
                }
            }
            return r;
        }

        public override async Task<Tuple<List<Qso>, string>> FetchConfirmationsAsync()
        {
            string u = Secrets.Get(Host, "eqsl.user"), p = Secrets.Get(Host, "eqsl.pass");
            if (u.Length == 0 || p.Length == 0) return Tuple.Create(new List<Qso>(), "set your eQSL login first");
            string since = Host.GetSetting("svc.eqsl.since.conf", "190001010000");
            string started = DateTime.UtcNow.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
            string baseUrl = Url("https://www.eqsl.cc", "/qslcard/");
            string page = await Http.GetStringAsync(baseUrl + "DownloadInBox.cfm?UserName=" + Uri.EscapeDataString(u) + "&Password=" + Uri.EscapeDataString(p) + "&RcvdSince=" + since).ConfigureAwait(false);
            Match link = Regex.Match(page, "href=\"([^\"]+\\.adi)\"", RegexOptions.IgnoreCase);
            if (!link.Success)
            {
                if (page.IndexOf("You have no log entries", StringComparison.OrdinalIgnoreCase) >= 0 || page.IndexOf("no QSLs", StringComparison.OrdinalIgnoreCase) >= 0)
                { Host.SetSetting("svc.eqsl.since.conf", started); return Tuple.Create(new List<Qso>(), ""); }
                return Tuple.Create(new List<Qso>(), "eQSL didn't give a file (check the username and password)");
            }
            string adi = await Http.GetStringAsync(new Uri(new Uri(baseUrl), link.Groups[1].Value)).ConfigureAwait(false);
            Host.SetSetting("svc.eqsl.since.conf", started);
            return Tuple.Create(Adif.Parse(adi), "");
        }

        public override void MarkConfirmed(Qso q, Qso from)
        {
            q.Set("EQSL_QSL_RCVD", "Y");
            q.Set("EQSL_QSL_SENT", q.Get("EQSL_QSL_SENT").Length > 0 ? q.Get("EQSL_QSL_SENT") : "Y");
            q.Set("EQSL_QSLRDATE", Date(DateTime.UtcNow));
        }
    }
}
