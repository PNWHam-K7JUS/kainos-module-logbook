/*  Lotw.cs

Logbook of the World. QSOs are signed and uploaded by TQSL (ARRL's program, which holds your certificate): the
logbook writes them to an ADIF file and runs TQSL on it with your station location, as other loggers do
(tqsl -q -d -u -x -a compliant -l "<location>" <file>). TQSL's exit code says how it went.

Confirmations come from LoTW's report (lotwreport.adi), with your LoTW website login (not the certificate): the
QSLs received since the last time, matched to the log.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Kainos.Logbook.Lookup;
using Kainos.Modules;

namespace Kainos.Logbook.Services
{
    internal sealed class Lotw : Service
    {
        public Lotw(IKainosHost host, HttpClient http) : base(host, http) { }

        public override string Key { get { return "lotw"; } }
        public override string Name { get { return "LoTW"; } }
        public override bool Confirms { get { return true; } }
        public override int BatchSize { get { return 200; } }

        public override bool IsSent(Qso q) { return q.Get("LOTW_QSL_SENT") == "Y" || q.Get("LOTW_QSL_RCVD") == "Y"; }
        public override void MarkSent(Qso q) { q.Set("LOTW_QSL_SENT", "Y"); q.Set("LOTW_QSLSDATE", Date(DateTime.UtcNow)); }

        public string TqslPath
        {
            get { string p = Host.GetSetting("svc.lotw.tqsl", ""); return p.Length > 0 ? p : FindTqsl(); }
            set { Host.SetSetting("svc.lotw.tqsl", value); }
        }
        public string Location { get { return Host.GetSetting("svc.lotw.location", ""); } set { Host.SetSetting("svc.lotw.location", value); } }

        public override bool HasLogin { get { return File.Exists(TqslPath) && Location.Length > 0; } }

        public static string FindTqsl()
        {
            foreach (string pf in new[] { Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("ProgramFiles") })
            {
                if (string.IsNullOrEmpty(pf)) continue;
                string p = Path.Combine(pf, "TrustedQSL", "tqsl.exe");
                if (File.Exists(p)) return p;
            }
            return "";
        }

        // the station locations set up in TQSL (its station_data file)
        public static List<string> Locations()
        {
            List<string> r = new List<string>();
            try
            {
                string f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrustedQSL", "station_data");
                if (!File.Exists(f)) return r;
                foreach (XElement e in XDocument.Load(f).Descendants("StationData"))
                {
                    string n = (string)e.Attribute("name");
                    if (!string.IsNullOrEmpty(n)) r.Add(n);
                }
            }
            catch { }
            return r;
        }

        private string args(string file)
        {
            return "-q -d -u -x -a compliant -l \"" + Location.Replace("\"", "") + "\" \"" + file + "\"";
        }

        public override string Preview(List<Qso> qsos)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("TQSL signs and uploads this file:\r\n\r\n  \"").Append(TqslPath.Length > 0 ? TqslPath : "tqsl.exe (not found: set it below)").Append("\" ")
              .Append(args("<the file below>")).Append("\r\n\r\n");
            sb.Append(Adif.Write(qsos, LogbookModule.ModuleVersion, Adif.Public));
            return sb.ToString();
        }

        // TQSL's exit codes
        private static string meaning(int code)
        {
            switch (code)
            {
                case 0: return "uploaded";
                case 1: return "cancelled";
                case 2: return "rejected by LoTW";
                case 3: return "an unexpected answer from LoTW";
                case 4: return "TQSL error";
                case 5: return "TQSL library error";
                case 6: return "TQSL couldn't open the file";
                case 7: return "TQSL couldn't write its output";
                case 8: return "all already uploaded (duplicates)";
                case 9: return "some already uploaded (duplicates), the rest uploaded";
                case 10: return "TQSL didn't understand the command (check the station location)";
                case 11: return "couldn't reach LoTW";
                default: return "TQSL exit code " + code;
            }
        }

        public override async Task<SendResult> SendAsync(List<Qso> qsos)
        {
            SendResult r = new SendResult();
            string file = Path.Combine(Path.GetTempPath(), "kainos-lotw-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".adi");
            try
            {
                File.WriteAllText(file, Adif.Write(qsos, LogbookModule.ModuleVersion, Adif.Public), new UTF8Encoding(false));
                int code = await Task.Run(() => runTqsl(file)).ConfigureAwait(false);
                r.Message = meaning(code);
                if (code == 0 || code == 9) r.All(qsos, Outcome.Sent);
                else if (code == 8) r.All(qsos, Outcome.Duplicate);
                else r.All(qsos, Outcome.Failed);
            }
            catch (Exception ex) { r.Message = "couldn't run TQSL: " + ex.Message; r.All(qsos, Outcome.Failed); }
            finally { try { File.Delete(file); } catch { } }
            return r;
        }

        private int runTqsl(string file)
        {
            ProcessStartInfo psi = new ProcessStartInfo(TqslPath, args(file)) { UseShellExecute = false, CreateNoWindow = true };
            using (Process p = Process.Start(psi))
            {
                if (!p.WaitForExit(180000)) { try { p.Kill(); } catch { } return -1; }
                return p.ExitCode;
            }
        }

        // ---- confirmations ----
        public string User { get { return Secrets.Get(Host, "lotw.user"); } }

        public override async Task<Tuple<List<Qso>, string>> FetchConfirmationsAsync()
        {
            string u = Secrets.Get(Host, "lotw.user"), p = Secrets.Get(Host, "lotw.pass");
            if (u.Length == 0 || p.Length == 0) return Tuple.Create(new List<Qso>(), "set your LoTW website login first");
            string since = Host.GetSetting("svc.lotw.lastqsl", "1900-01-01");
            string url = Url("https://lotw.arrl.org", "/lotwuser/lotwreport.adi") + "?login=" + Uri.EscapeDataString(u) + "&password=" + Uri.EscapeDataString(p)
                       + "&qso_query=1&qso_qsl=yes&qso_qsldetail=yes&qso_qslsince=" + Uri.EscapeDataString(since);
            string text = await Http.GetStringAsync(url).ConfigureAwait(false);
            if (text.IndexOf("<eoh>", StringComparison.OrdinalIgnoreCase) < 0)
                return Tuple.Create(new List<Qso>(), "LoTW didn't accept the login (check the LoTW website username and password)");
            Match m = Regex.Match(text, @"<APP_LoTW_LASTQSL:\d+>([0-9-]+)", RegexOptions.IgnoreCase);
            List<Qso> got = Adif.Parse(text).Where(q => q.Get("QSL_RCVD") == "Y").ToList();
            if (m.Success) Host.SetSetting("svc.lotw.lastqsl", m.Groups[1].Value);
            return Tuple.Create(got, "");
        }

        public override void MarkConfirmed(Qso q, Qso from)
        {
            q.Set("LOTW_QSL_RCVD", "Y");
            q.Set("LOTW_QSL_SENT", "Y");
            q.Set("LOTW_QSLRDATE", from.Get("QSLRDATE").Length > 0 ? from.Get("QSLRDATE") : Date(DateTime.UtcNow));
            // LoTW knows the other station's details: fill in what's missing
            foreach (string f in new[] { "DXCC", "CQZ", "ITUZ", "STATE", "CNTY", "GRIDSQUARE" })
                if (q.Get(f).Length == 0 && from.Get(f).Length > 0) q.Set(f, from.Get(f));
        }
    }
}
