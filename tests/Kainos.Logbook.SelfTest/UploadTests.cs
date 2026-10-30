/*  UploadTests.cs (self-test)

Uploads and confirmations against the stand-ins in FakeServices.cs (nothing reaches a real service): nothing is
sent while a service is off; only QSOs logged here after it was switched on go, until Send older; eQSL, QRZ
(INSERT, REPLACE after an edit, duplicates, a bad key), Club Log (taken, refused, rejected and retried later) and
LoTW through a stand-in TQSL (its command line, uploaded, duplicates, can't reach LoTW); passwords hidden in the
preview; confirmations from LoTW, eQSL and QRZ matched to the log.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Kainos.Logbook.Lookup;
using Kainos.Logbook.Services;

namespace Kainos.Logbook.SelfTest
{
    internal static class UploadTests
    {
        private static Action<bool, string> check;

        private static Qso logged(string call, string date, string time, string band, string mode)
        {
            Qso q = Adif.Parse("<CALL:" + call.Length + ">" + call + "<QSO_DATE:8>" + date + "<TIME_ON:4>" + time + "<BAND:" + band.Length + ">" + band + "<MODE:" + mode.Length + ">" + mode + "<EOR>")[0];
            q.LoggedHere = true;
            return q;
        }

        public static void Run(string root, Action<bool, string> chk)
        {
            check = chk;
            using (FakeServer srv = new FakeServer())
            {
                FakeHost h = new FakeHost(Path.Combine(root, "up"));
                h.SetSetting("svc.base", srv.Base);         // every service talks to the stand-in
                LogStore store = new LogStore(h.DataFolder, "0.1.0");
                store.Load();
                using (System.Net.Http.HttpClient http = CallLookup.NewHttp("selftest"))
                {
                    Uploader up = new Uploader(h, store, http, () => "K7JUS");
                    string eqslPass = "s3cret-eqsl";
                    Secrets.Set(h, "eqsl.user", "K7JUS"); Secrets.Set(h, "eqsl.pass", eqslPass);
                    Secrets.Set(h, "qrzlog.key", "ABCD-1234-QRZK");
                    Secrets.Set(h, "clublog.email", "me@example.com"); Secrets.Set(h, "clublog.pass", "clpass"); Secrets.Set(h, "clublog.api", "apikey");
                    check(Secrets.Get(h, "eqsl.pass") == eqslPass && !h.GetSetting("secret.eqsl.pass", "").Contains("s3cret"), "secrets: stored encrypted, read back");

                    // an old imported QSO, and a QSO logged here: nothing goes while every service is off
                    Qso imported = Adif.Parse("<CALL:5>G4OLD<QSO_DATE:8>20250101<TIME_ON:4>1200<BAND:3>20m<MODE:3>SSB<EOR>")[0];
                    store.AddMany(new[] { imported });
                    store.Add(logged("DL1ABC", "20261030", "1200", "20m", "SSB"));
                    up.RunAsync(null).Wait();
                    check(srv.Requests.Count == 0, "uploads: nothing is sent while the services are off");

                    srv.Handle = r =>
                    {
                        switch (r.Path)
                        {
                            case "/qslcard/importADIF.cfm":
                                string a = r.Arg("ADIFData");
                                if (!a.Contains("<EQSL_PSWD:" + eqslPass.Length + ">" + eqslPass)) return Tuple.Create(200, "<HTML>Error: No match on eQSL_User/eQSL_Pswd</HTML>");
                                if (a.Contains("<CALL:4>DUPE")) return Tuple.Create(200, "<HTML>Warning: Y=2026 M=10 D=30 DUPE 20M SSB Bad record: Duplicate<BR>Result: 0 out of 1 records added</HTML>");
                                return Tuple.Create(200, "<HTML>Result: 1 out of 1 records added<BR></HTML>");
                            case "/api":
                                if (r.Arg("KEY") != "ABCD-1234-QRZK") return Tuple.Create(200, "RESULT=AUTH&REASON=invalid api key");
                                if (r.Arg("ACTION") == "FETCH")
                                {
                                    string adif = "<CALL:6>DL1ABC<QSO_DATE:8>20261030<TIME_ON:4>1205<BAND:3>20m<MODE:3>SSB<APP_QRZLOG_STATUS:1>C<APP_QRZLOG_LOGID:3>777<EOR>";
                                    return Tuple.Create(200, "RESULT=OK&COUNT=1&ADIF=" + WebUtility.UrlEncode(WebUtility.HtmlEncode(adif)));
                                }
                                if (r.Arg("ADIF").Contains("<CALL:4>DUPE")) return Tuple.Create(200, "RESULT=FAIL&REASON=Unable to add QSO to database: duplicate&EXTENDED=");
                                return Tuple.Create(200, "RESULT=OK&LOGID=" + (1000 + srv.Count("/api")) + "&COUNT=1");
                            case "/realtime.php":
                                if (r.Arg("password") != "clpass" || r.Arg("api") != "apikey") return Tuple.Create(403, "Invalid password");
                                if (r.Arg("adif").Contains("<CALL:5>BADCL")) return Tuple.Create(400, "Rejected: invalid ADIF");
                                return Tuple.Create(200, "OK");
                            case "/lotwuser/lotwreport.adi":
                                if (r.Arg("password") != "lotwpw") return Tuple.Create(200, "<html>Username/password incorrect</html>");
                                return Tuple.Create(200, "ARRL Logbook of the World Status Report\n<PROGRAMID:4>LoTW\n<APP_LoTW_LASTQSL:19>2026-10-30 18:00:00\n<eoh>\n" +
                                    "<CALL:6>DL1ABC<BAND:3>20M<MODE:3>SSB<QSO_DATE:8>20261030<TIME_ON:6>121500<QSL_RCVD:1>Y<QSLRDATE:8>20261030<DXCC:3>230<CQZ:2>14<eor>\n");
                            case "/qslcard/DownloadInBox.cfm":
                                return Tuple.Create(200, "<HTML>Your ADIF log file has been built. <A HREF=\"../downloadedfiles/abc123.adi\">.ADI file</A></HTML>");
                            case "/downloadedfiles/abc123.adi":
                                return Tuple.Create(200, "eQSL.cc DownloadInBox\n<EOH>\n<CALL:6>DL1ABC<QSO_DATE:8>20261030<TIME_ON:4>1200<BAND:3>20M<MODE:3>SSB<QSL_SENT:1>Y<EOR>\n");
                        }
                        return Tuple.Create(404, "no");
                    };

                    // eQSL on: the QSO logged before it was switched on doesn't go, a new one does
                    Service eqsl = up.Get("eqsl");
                    Thread.Sleep(1100);     // switched on a second after the QSO before (times are kept to the second)
                    eqsl.Enabled = true;
                    Thread.Sleep(1100);     // the switch-on time, to the second
                    check(up.Waiting(eqsl).Count == 0, "eQSL: switched on, the QSOs from before aren't waiting");
                    store.Add(logged("JA1XYZ", "20261030", "1300", "15m", "CW"));
                    check(up.Waiting(eqsl).Count == 1, "eQSL: a QSO logged after is waiting");
                    string pv = eqsl.Preview(up.Waiting(eqsl));
                    check(pv.Contains("<CALL:6>JA1XYZ") && !pv.Contains(eqslPass) && srv.Requests.Count == 0, "eQSL: the preview shows the QSO, hides the password, and sends nothing");
                    up.RunAsync(null).Wait();
                    FakeRequest er = srv.Last("/qslcard/importADIF.cfm");
                    check(er != null && er.Method == "POST" && er.Arg("ADIFData").Contains("<EQSL_USER:5>K7JUS") && er.Arg("ADIFData").Contains("<CALL:6>JA1XYZ"), "eQSL: posted with the login in the ADIF header");
                    check(!er.Arg("ADIFData").Contains("APP_KAINOS"), "eQSL: the logbook's own fields aren't sent");
                    check(store.All().First(q => q.Call == "JA1XYZ").Eqsl == 'u' && up.Waiting(eqsl).Count == 0, "eQSL: marked sent, nothing waiting");
                    store.Add(logged("DUPE", "20261030", "1310", "20m", "SSB"));
                    up.RunAsync(null).Wait();
                    check(store.All().First(q => q.Call == "DUPE").Eqsl == 'u', "eQSL: a duplicate counts as sent");
                    Secrets.Set(h, "eqsl.pass", "wrong");
                    store.Add(logged("G4ABC", "20261030", "1320", "20m", "SSB"));
                    up.RunAsync(null).Wait();
                    int before = srv.Count("/qslcard/importADIF.cfm");
                    check(up.State(eqsl).LoginRefused && store.All().First(q => q.Call == "G4ABC").Eqsl == '\0', "eQSL: a refused login stops it, the QSO stays waiting");
                    up.RunAsync(null).Wait();
                    check(srv.Count("/qslcard/importADIF.cfm") == before, "eQSL: and it doesn't try again by itself");
                    Secrets.Set(h, "eqsl.pass", eqslPass);
                    up.Reset();
                    up.RunAsync(null).Wait();
                    check(store.All().First(q => q.Call == "G4ABC").Eqsl == 'u', "eQSL: after the settings are saved it goes");
                    eqsl.Auto = false;
                    store.Add(logged("F5ABC", "20261030", "1330", "20m", "SSB"));
                    up.RunAsync(null).Wait();
                    check(store.All().First(q => q.Call == "F5ABC").Eqsl == '\0', "eQSL: set to wait for Send now, it waits");
                    up.SendNowAsync(eqsl).Wait();
                    check(store.All().First(q => q.Call == "F5ABC").Eqsl == 'u', "eQSL: Send now sends it");

                    // QRZ Logbook: insert (log id kept), an edit goes again as REPLACE, a duplicate, a bad key
                    Service qrz = up.Get("qrz");
                    qrz.Enabled = true;
                    qrz.Since = DateTime.MinValue;      // Send older QSOs: everything not yet sent
                    check(up.Waiting(qrz).Any(q => q.Call == "G4OLD"), "QRZ: Send older QSOs includes the imported one");
                    up.RunAsync(null).Wait();
                    Qso dl = store.All().First(q => q.Call == "DL1ABC");
                    check(dl.QrzLog == 'u' && dl.Get("APP_KAINOS_QRZ_LOGID").Length > 0 && store.All().First(q => q.Call == "DUPE").QrzLog == 'u', "QRZ: inserted (log id kept), the duplicate counted as sent");
                    check(srv.Requests.Where(r => r.Path == "/api").All(r => r.Arg("ACTION") == "INSERT" && r.Arg("OPTION") == ""), "QRZ: ACTION=INSERT");
                    Qso edited = dl.Clone();
                    edited.Name = "Hans";
                    Uploader.Edited(dl, edited);
                    store.Update(edited);
                    check(up.Waiting(qrz).Count == 1, "QRZ: an edited QSO waits to go again");
                    up.RunAsync(null).Wait();
                    FakeRequest rep = srv.Last("/api");
                    check(rep.Arg("OPTION") == "REPLACE" && rep.Arg("ADIF").Contains("<NAME:4>Hans") && store.All().First(q => q.Call == "DL1ABC").QrzLog == 'u', "QRZ: sent again with OPTION=REPLACE");
                    Secrets.Set(h, "qrzlog.key", "bad");
                    store.Add(logged("W1ABC", "20261030", "1340", "20m", "SSB"));
                    up.RunAsync(null).Wait();
                    check(up.State(qrz).LoginRefused, "QRZ: an invalid key stops it");
                    Secrets.Set(h, "qrzlog.key", "ABCD-1234-QRZK");
                    up.Reset();

                    // Club Log: taken, rejected (retried later), refused
                    Service cl = up.Get("clublog");
                    cl.Enabled = true;
                    Thread.Sleep(1100);
                    store.Add(logged("VK2ABC", "20261030", "1350", "20m", "FT8"));
                    up.RunAsync(null).Wait();
                    FakeRequest cr = srv.Last("/realtime.php");
                    check(cr != null && cr.Arg("callsign") == "K7JUS" && cr.Arg("email") == "me@example.com" && store.All().First(q => q.Call == "VK2ABC").ClubLog == 'u', "Club Log: posted with the call and login, marked sent");
                    store.Add(logged("BADCL", "20261030", "1400", "20m", "SSB"));
                    up.RunAsync(null).Wait();
                    ServiceState cs = up.State(cl);
                    check(store.All().First(q => q.Call == "BADCL").ClubLog == '\0' && cs.RetryAt > DateTime.UtcNow.AddSeconds(30) && cs.Last.Contains("Rejected"), "Club Log: a rejected QSO waits, and Club Log is tried again in a minute");
                    int n = srv.Count("/realtime.php");
                    up.RunAsync(null).Wait();
                    check(srv.Count("/realtime.php") == n, "Club Log: not before then");
                    string clPv = cl.Preview(up.Waiting(cl));
                    check(!clPv.Contains("clpass") && !clPv.Contains("apikey"), "Club Log: the preview hides the password and key");

                    // LoTW through a stand-in TQSL
                    Lotw lotw = (Lotw)up.Get("lotw");
                    string tqslOut = Path.Combine(root, "tqsl-run.txt");
                    lotw.TqslPath = typeof(Program).Assembly.Location;
                    lotw.Location = "Home QTH";
                    lotw.Enabled = true;
                    Thread.Sleep(1100);
                    store.Add(logged("ZS6ABC", "20261030", "1410", "20m", "SSB"));
                    Environment.SetEnvironmentVariable("KAINOS_FAKE_TQSL_OUT", tqslOut);
                    Environment.SetEnvironmentVariable("KAINOS_FAKE_TQSL_EXIT", "0");
                    try
                    {
                        up.RunAsync(null).Wait();
                        string run = File.Exists(tqslOut) ? File.ReadAllText(tqslOut) : "";
                        check(run.StartsWith("-q -d -u -x -a compliant -l Home QTH ") && run.Contains("<CALL:6>ZS6ABC") && !run.Contains("APP_KAINOS"), "LoTW: TQSL run with the station location and the QSO's file");
                        check(store.All().First(q => q.Call == "ZS6ABC").Lotw == 'u', "LoTW: uploaded (exit 0), marked sent");
                        Environment.SetEnvironmentVariable("KAINOS_FAKE_TQSL_EXIT", "8");
                        store.Add(logged("PY2ABC", "20261030", "1420", "20m", "SSB"));
                        up.RunAsync(null).Wait();
                        check(store.All().First(q => q.Call == "PY2ABC").Lotw == 'u', "LoTW: all duplicates (exit 8) counts as sent");
                        Environment.SetEnvironmentVariable("KAINOS_FAKE_TQSL_EXIT", "11");
                        store.Add(logged("EA8ABC", "20261030", "1430", "20m", "SSB"));
                        up.RunAsync(null).Wait();
                        check(store.All().First(q => q.Call == "EA8ABC").Lotw == '\0' && up.State(lotw).Last.Contains("couldn't reach LoTW"), "LoTW: can't reach LoTW (exit 11): waits, says why");
                    }
                    finally
                    {
                        Environment.SetEnvironmentVariable("KAINOS_FAKE_TQSL_OUT", null);
                        Environment.SetEnvironmentVariable("KAINOS_FAKE_TQSL_EXIT", null);
                    }

                    // confirmations, matched to the log (DL1ABC 20 m SSB 12:00; the confirmations say 12:15, 12:05, 12:00)
                    Secrets.Set(h, "lotw.user", "k7jus"); Secrets.Set(h, "lotw.pass", "nope");
                    string bad = up.FetchAsync(lotw).Result;
                    check(bad.Contains("didn't accept the login"), "LoTW confirmations: a bad login says so");
                    Secrets.Set(h, "lotw.pass", "lotwpw");
                    string got = up.FetchAsync(lotw).Result;
                    Qso c = store.All().First(q => q.Call == "DL1ABC");
                    check(got.Contains("1 matched") && c.Lotw == 'c' && c.Get("DXCC") == "230" && h.GetSetting("svc.lotw.lastqsl", "").StartsWith("2026-10-30"), "LoTW confirmations: matched within 30 minutes, DXCC filled in, the date kept for next time");
                    check(up.FetchAsync(eqsl).Result.Contains("1 matched") && store.All().First(q => q.Call == "DL1ABC").Eqsl == 'c', "eQSL confirmations: the inbox file followed and matched");
                    check(up.FetchAsync(qrz).Result.Contains("1 matched") && store.All().First(q => q.Call == "DL1ABC").QrzLog == 'c', "QRZ confirmations: FETCH (HTML-escaped ADIF) matched");
                    check(srv.Requests.All(r => r.Path != "/" ), "all requests went to the stand-in");
                }
            }
        }
    }
}
