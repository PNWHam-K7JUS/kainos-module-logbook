/*  Program.cs (self-test)

The logbook against a stand-in Kainos: ADIF in and out, cty.dat (prefixes, whole calls, overrides, portable calls),
grid squares, distance and heading, the log store (add, edit, delete, duplicates, the .bak), worked-before and what
counts as confirmed, QSOs from the Digital Modes module's log, the exports, and the module and its pane (drawn to
a PNG to look at). With --online it also downloads cty.dat and looks up W1AW on callook.info.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Kainos.Logbook.Lookup;
using Kainos.Logbook.UI;

namespace Kainos.Logbook.SelfTest
{
    internal static class Program
    {
        private static int _fail;

        private static void check(bool ok, string what)
        {
            Console.WriteLine((ok ? "PASS  " : "FAIL  ") + what);
            if (!ok) _fail++;
        }

        private const string SampleCty =
"United States:            05:  08:  NA:   37.53:    91.67:     5.0:  K:\n" +
"    AA,AB,K,N,W,=K7JUS(3)[6],KH6(31)[61]<21.3/157.8>{OC}~10~;\n" +
"Spain:                    14:  37:  EU:   40.32:     3.43:    -1.0:  EA:\n" +
"    AM,AN,AO,EA,EB,EC,ED,EE,EF,EG,EH;\n" +
"Canary Islands:           33:  36:  AF:   28.32:    15.85:     0.0:  EA8:\n" +
"    AM8,AN8,AO8,EA8,EB8,EC8,ED8,EE8,EF8,EG8,EH8;\n" +
"Sov Mil Order of Malta:   15:  28:  EU:   41.90:   -12.43:    -1.0:  1A:\n" +
"    1A;\n";

        private static Qso qso(string call, string date, string time, string band, string mode, string extra = "")
        {
            Qso q = Adif.Parse("<CALL:" + call.Length + ">" + call + "<QSO_DATE:8>" + date + "<TIME_ON:4>" + time + "<BAND:" + band.Length + ">" + band + "<MODE:" + mode.Length + ">" + mode + extra + "<EOR>")[0];
            return q;
        }

        [STAThread]
        private static int Main(string[] args)
        {
            if (FakeTqsl.RunIfAsked(args)) return 0;      // started by the LoTW test as a stand-in TQSL
            bool online = args.Contains("--online");
            string root = Path.Combine(Path.GetTempPath(), "kainos-logbook-selftest-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(root);
            try
            {
                adif();
                cty();
                geo();
                store(root);
                worked(root);
                feeds(root);
                exports();
                UploadTests.Run(root, check);
                PotaTests.Run(root, check);
                ContestTests.Run(root, check);
                AwardTests.Run(root, check);
                MessageTests.Run(root, check);
                module(root);
                if (online) onlineTests(root);
            }
            catch (Exception ex) { check(false, "unexpected: " + ex); }
            finally { try { Directory.Delete(root, true); } catch { } }
            Console.WriteLine(_fail == 0 ? "\nAll passed." : "\n" + _fail + " FAILED");
            return _fail == 0 ? 0 : 1;
        }

        private static void adif()
        {
            string text = "Some header text <PROGRAMID:4>Test\r\n<EOH>\r\n" +
                          "<call:6>DL1ABC <qso_date:8>20261030 <time_on:6>123456 <band:3>20m <mode:3>SSB <comment:9>a <b> c\nd <name:4:S>Hans <eor>\r\n" +
                          "<CALL:5>JA1XY<QSO_DATE:8>20261029<TIME_ON:4>0102<BAND:3>15m<MODE:4>MFSK<SUBMODE:3>FT4<EOR>";
            List<Qso> l = Adif.Parse(text);
            check(l.Count == 2, "ADIF: two records past a header");
            check(l[0].Call == "DL1ABC" && l[0].Comment == "a <b> c\nd" && l[0].Name == "Hans", "ADIF: fields read by length (angle brackets, a new line, a type indicator)");
            check(l[0].TimeOn == new DateTime(2026, 10, 30, 12, 34, 56, DateTimeKind.Utc), "ADIF: date and time");
            check(l[1].ModeShown == "FT4" && l[1].Class == ModeClass.Digital, "ADIF: FT4 as MFSK + SUBMODE");
            List<Qso> back = Adif.Parse(Adif.Write(l, "0.1.0"));
            check(back.Count == 2 && back[0].Comment == l[0].Comment && back[1].Get("SUBMODE") == "FT4", "ADIF: written and read back the same");
            check(Adif.Parse("<CALL:5>W1AW <QSO_DATE:8>20261030 <TIME_ON:4>1200 <EOR>").Count == 1, "ADIF: a file with no header");
        }

        private static void cty()
        {
            Cty c = Cty.Parse(SampleCty);
            check(c.Entities.Count == 4, "cty.dat: four entities");
            CtyInfo k = c.Resolve("K7JUS");
            check(k != null && k.Name == "United States" && k.Cq == 3 && k.Itu == 6, "cty.dat: a whole call (=K7JUS) with its zones");
            CtyInfo h = c.Resolve("KH6ABC");
            check(h != null && h.Cont == "OC" && h.Cq == 31 && Math.Abs(h.Lat - 21.3) < 0.01 && Math.Abs(h.Lon + 157.8) < 0.01 && h.UtcOffset == -10, "cty.dat: overrides (zones, place, continent, time)");
            CtyInfo ea8 = c.Resolve("EA8/DL1ABC");
            check(ea8 != null && ea8.Name == "Canary Islands", "cty.dat: EA8/DL1ABC is the Canary Islands");
            check(c.Resolve("EA5XY")?.Name == "Spain", "cty.dat: the longest prefix wins (EA5 Spain, EA8 Canaries)");
            check(c.Resolve("W1AW/P")?.Name == "United States", "cty.dat: /P is ignored");
            CtyInfo area = c.Resolve("K7JUS/4");
            check(area != null && area.Name == "United States" && area.CallArea == "4" && area.Cq == 5, "cty.dat: K7JUS/4 is call area 4 (not the =K7JUS entry)");
            check(c.Resolve("DL1ABC") == null, "cty.dat: an unknown prefix is null");
            Cty b = Cty.Parse(BuiltIn.Text);
            check(b.Entities.Count > 100 && b.Resolve("JA1ABC")?.Name == "Japan" && b.Resolve("VP8XYZ")?.Name == "Falkland Islands" && b.Resolve("KH6ABC")?.Name == "Hawaii", "built-in list: Japan, Falklands, Hawaii");
            string p;
            check(Cty.BaseCall("VP2E/K7JUS", out p) == "K7JUS" && p == "VP2E", "base call: VP2E/K7JUS");
        }

        private static void geo()
        {
            double lat, lon;
            check(Geo.GridToLatLon("CN87", out lat, out lon) && Math.Abs(lat - 47.5) < 0.01 && Math.Abs(lon + 123) < 0.01, "grid: CN87 is 47.5 N 123 W");
            check(Geo.LatLonToGrid(47.5, -123, 4) == "CN87", "grid: back to CN87");
            check(Geo.GridToLatLon("FN31pr", out lat, out lon) && Math.Abs(lat - 41.7) < 0.05 && Math.Abs(lon + 72.7) < 0.05, "grid: FN31pr (W1AW)");
            double km = Geo.DistanceKm(47.5, -123, 35.7, 139.7);
            check(km > 7500 && km < 7900, "distance: Seattle area to Tokyo about 7,700 km (" + (int)km + ")");
            int b = Geo.Bearing(47.5, -123, 35.7, 139.7);
            check(b > 295 && b < 315, "heading: to Japan about 305° (" + b + ")");
        }

        private static void store(string root)
        {
            string dir = Path.Combine(root, "store");
            Directory.CreateDirectory(dir);
            LogStore s = new LogStore(dir, "0.1.0");
            s.Load();
            int changed = 0;
            s.Changed += () => changed++;
            s.Add(qso("DL1ABC", "20261030", "1200", "20m", "SSB"));
            s.Add(qso("JA1XY", "20261030", "1300", "15m", "CW"));
            s.Add(qso("VK2ABC", "20261029", "0500", "20m", "FT8"));      // older: the file is rewritten in order
            check(s.Count == 3 && changed == 3, "store: three added");
            LogStore again = new LogStore(dir, "0.1.0");
            again.Load();
            List<Qso> all = again.All();
            check(all.Count == 3 && all[0].Call == "VK2ABC" && all[2].Call == "JA1XY", "store: read back in time order");
            check(all.All(q => q.Id.Length > 0), "store: every QSO has an id");
            Qso e = all[1].Clone();
            e.Name = "Hans";
            again.Update(e);
            check(new LogStore(dir, "0.1.0").Let(x => { x.Load(); return x.Find(e.Id)?.Name == "Hans"; }), "store: an edit is saved");
            check(File.Exists(Path.Combine(dir, "logbook.adi.bak")), "store: the previous log kept as logbook.adi.bak");
            again.Delete(all[0].Id);
            check(new LogStore(dir, "0.1.0").Let(x => { x.Load(); return x.Count == 2; }), "store: a delete is saved");
            int n = again.AddMany(new[] { qso("DL1ABC", "20261030", "1201", "20m", "SSB"), qso("DL1ABC", "20261030", "1201", "40m", "SSB"), qso("G4ABC", "20261030", "1400", "20m", "SSB") });
            check(n == 2, "store: an import skips a QSO already there (same call and band within 2 minutes), keeps the others");
        }

        private static void worked(string root)
        {
            FakeHost h = new FakeHost(Path.Combine(root, "w"));
            Book book = new Book(h, "0.1.0");
            book.Cty = Cty.Parse(BuiltIn.Text);
            book.Store.Load();
            book.Store.Add(qso("JA1XYZ", "20261002", "1841", "15m", "SSB", "<LOTW_QSL_RCVD:1>Y"));
            book.Store.Add(qso("JA2ABC", "20261003", "1841", "20m", "CW", "<EQSL_QSL_RCVD:1>Y"));
            Worked w = book.Analyse("JA7QQ", "20m", ModeClass.Phone);
            check(!w.NewOne && !w.NewBand && !w.NewMode && w.EntityConfirmed, "worked: Japan on 20 m SSB: not new, confirmed (LoTW)");
            check(w.Grid["15m"][(int)ModeClass.Phone] == Slot.Confirmed && w.Grid["20m"][(int)ModeClass.CW] == Slot.Worked, "worked: grid (15 m SSB confirmed by LoTW, 20 m CW only by eQSL so worked)");
            Worked w2 = book.Analyse("JA7QQ", "40m", ModeClass.Digital);
            check(w2.NewBand && w2.NewMode, "worked: Japan on 40 m FT8 is a new band and a new mode");
            check(book.Analyse("VP8XYZ", "20m", ModeClass.Phone).NewOne, "worked: the Falklands are a new one");
            book.ConfirmSource = "eqsl";
            check(book.Analyse("JA7QQ", "20m", ModeClass.Phone).Grid["20m"][(int)ModeClass.CW] == Slot.Confirmed, "confirmed: eQSL counts when it's chosen");
            book.ConfirmSource = "lotw";
            Qso card = book.Store.All().First(q => q.Call == "JA2ABC").Clone();
            card.CardReceived = true;
            book.Store.Update(card);
            check(book.Analyse("JA7QQ", "20m", ModeClass.Phone).Grid["20m"][(int)ModeClass.CW] == Slot.Confirmed, "confirmed: a paper card counts with LoTW");
            Qso fresh = book.NewFromRadio();
            check(fresh.Band == "20m" && fresh.Mode == "SSB" && fresh.Submode == "USB" && fresh.Get("STATION_CALLSIGN") == "K7JUS" && fresh.Get("MY_GRIDSQUARE") == "CN87", "a QSO from the radio: 20 m USB, my call and grid");
            fresh.Call = "VP8XYZ";
            book.Fill(fresh, new CallInfo { Name = "Ann Example", Grid = "GD18", Country = "Falkland Islands" });
            check(fresh.Country == "Falkland Islands" && fresh.CqZone == 13 && fresh.Get("CONT") == "SA" && fresh.Name == "Ann Example" && fresh.Grid == "GD18", "filled in from cty and a lookup");
        }

        private static void feeds(string root)
        {
            string r = Path.Combine(root, "feeds");
            FakeHost h = new FakeHost(r);
            Directory.CreateDirectory(Path.Combine(r, "digimodes"));
            string adi = Path.Combine(r, "digimodes", "kainos-digimodes.adi");
            File.WriteAllText(adi, "Kainos Digital Modes\r\n<ADIF_VER:5>3.1.4 <EOH>\r\n<CALL:6>DL1ABC <QSO_DATE:8>20261030 <TIME_ON:6>120015 <BAND:3>20m <MODE:3>FT8 <EOR>\r\n");
            LogStore s = new LogStore(h.DataFolder, "0.1.0");
            s.Load();
            Feeds f = new Feeds(h, s);
            int added = 0;
            f.Added += (n, from) => added += n;
            f.Poll();
            check(s.Count == 1 && added == 1 && s.All()[0].Get("APP_KAINOS_FROM") == "digimodes", "feeds: an FT8 QSO comes in from kainos-digimodes.adi");
            f.Poll();
            check(s.Count == 1, "feeds: and only once");
            File.AppendAllText(adi, "<CALL:5>G4ABC <QSO_DATE:8>20261030 <TIME_ON:6>121530 <BAND:3>20m <MODE:3>FT8 <EOR>\r\n<CALL:5>JA1AB <QSO_DATE:8>20261030 <TIME_ON:6>1220");
            f.Poll();
            check(s.Count == 2, "feeds: the next one comes in; a half-written one waits");
            File.AppendAllText(adi, "00 <BAND:3>15m <MODE:3>FT4 <EOR>\r\n");
            f.Poll();
            check(s.Count == 3 && s.All().Last().Call == "JA1AB", "feeds: then the rest of it");
            f.On = false;
            File.AppendAllText(adi, "<CALL:5>W1ABC <QSO_DATE:8>20261030 <TIME_ON:6>130000 <BAND:3>20m <MODE:3>FT8 <EOR>\r\n");
            f.Poll();
            check(s.Count == 3, "feeds: off means off");
        }

        private static void exports()
        {
            Qso q = qso("DL1ABC", "20261030", "1200", "20m", "SSB", "<COMMENT:15>hi, \"Hans\" here<APP_KAINOS_ID:4>abcd<APP_OTHER:1>x<LOTW_QSL_RCVD:1>Y");
            string a = Export.Make("adif", new[] { q }, "0.1.0");
            check(!a.Contains("APP_KAINOS_ID") && a.Contains("APP_OTHER") && a.Contains("<CALL:6>DL1ABC"), "export ADIF: the logbook's own fields left out, others kept");
            string csv = Export.Csv(new[] { q });
            check(csv.Contains("\"hi, \"\"Hans\"\" here\"") && csv.Contains(",confirmed,"), "export CSV: quotes and commas escaped, LoTW confirmed");
            string adx = Export.Adx(new[] { q }, "0.1.0");
            check(adx.Contains("<CALL>DL1ABC</CALL>") && adx.Contains("&quot;Hans&quot;") && !adx.Contains("APP_"), "export ADX: escaped, no application fields");
            ExportFilter f = new ExportFilter { Band = "20m", Class = ModeClass.Phone, From = new DateTime(2026, 10, 30) };
            check(f.Apply(new[] { q, qso("JA1XY", "20261029", "1200", "20m", "SSB"), qso("G4X", "20261030", "1200", "40m", "SSB") }).Count() == 1, "export filter: date, band and mode");
        }

        private static void module(string root)
        {
            FakeHost h = new FakeHost(Path.Combine(root, "m"));
            h.SetSetting("cty.auto", "0");      // offline: the built-in list, no download
            List<string> posted = new List<string>();
            FakeServer srv = new FakeServer { Handle = r => PotaTests.Serve(r, posted) ?? Tuple.Create(404, "no") };
            h.SetSetting("svc.base", srv.Base);    // POTA spots from the stand-in
            h.SetSetting("lookup.callook", "0");   // and no lookups
            LogbookModule m = new LogbookModule();
            m.Start(h);
            check(h.DockLabel == "LOG" && h.Panes.Count == 1, "module: a LOG dock button and a pane");
            FakeMenu menu = h.Menus[0];
            menu.Click();
            check(h.Panes[0].IsOpen && menu.Checked && h.GetSetting("open", "0") == "1", "module: the button opens it");

            LogbookPane pane = (LogbookPane)h.Panes[0].Content;
            using (Form f = new Form { Width = 1320, Height = 420, StartPosition = FormStartPosition.Manual, Location = new Point(-4000, -4000), ShowInTaskbar = false })
            {
                f.Controls.Add(pane);
                pane.Dock = DockStyle.Fill;
                f.Show();
                Application.DoEvents();
                List<Field> fields = Controls(pane).OfType<Field>().ToList();
                Field call = fields[0];
                // log two QSOs through the entry row, as typed
                call.Text = "JA1XYZ";
                Application.DoEvents();
                press(call, Keys.Enter);
                h.Hz = 7074000; h.ModeName = "DIGU";
                call.Text = "DL1ABC";
                fields[3].Text = "Hans";
                press(call, Keys.Enter);
                h.Hz = 14225000; h.ModeName = "USB";
                Application.DoEvents();
                string adi = File.ReadAllText(Path.Combine(h.DataFolder, "logbook.adi"));
                List<Qso> l = Adif.Parse(adi);
                check(l.Count == 2 && l[0].Call == "JA1XYZ" && l[0].Band == "20m" && l[0].Get("SUBMODE") == "USB" && l[0].RstSent == "59" && l[0].Country == "Japan", "pane: Enter logs JA1XYZ on 20 m USB, 59, Japan");
                check(l[1].Call == "DL1ABC" && l[1].Band == "40m" && l[1].Name == "Hans", "pane: then DL1ABC on 40 m with the name typed");
                check(call.Text == "", "pane: the entry is cleared after logging");
                call.Text = "JA7QQ";
                pane.Running = true;            // a tick: the radio read, the alerts worked out
                Application.DoEvents();
                using (Bitmap bmp = new Bitmap(pane.Width, pane.Height))
                {
                    pane.DrawToBitmap(bmp, new Rectangle(0, 0, pane.Width, pane.Height));
                    string png = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "logbook-pane.png");
                    bmp.Save(png, ImageFormat.Png);
                    check(File.Exists(png), "pane: drawn to " + png);
                }
                press(call, Keys.Escape);
                check(call.Text == "", "pane: Esc clears");

                // the POTA page: spots from the stand-in, drawn; a double-click tunes to one
                h.SetSetting("page", "pota");
                pane.RestorePage();
                PotaPage pp = Controls(pane).OfType<PotaPage>().First();
                DataGridView sg = Controls(pp).OfType<DataGridView>().First();
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                while (sg.Rows.Count == 0 && sw.ElapsedMilliseconds < 5000) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
                check(sg.Rows.Count == 3 && (string)sg.Rows[0].Cells[1].Value == "K4ABC", "POTA page: the spots shown");
                using (Bitmap bmp = new Bitmap(pane.Width, pane.Height))
                {
                    pane.DrawToBitmap(bmp, new Rectangle(0, 0, pane.Width, pane.Height));
                    bmp.Save(Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "logbook-pota.png"), ImageFormat.Png);
                }
                typeof(DataGridView).GetMethod("OnCellDoubleClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(sg, new object[] { new DataGridViewCellEventArgs(1, 1) });
                Application.DoEvents();
                check(Math.Abs(h.Hz - 7062000) < 1 && h.ModeName == "CWU" && call.Text == "N7XYZ" && fields[4].Text == "US-5678", "POTA page: a double-click tunes to 7062.0 CW and fills in N7XYZ at US-5678");
                press(call, Keys.Enter);
                Qso hunted = Adif.Parse(File.ReadAllText(Path.Combine(h.DataFolder, "logbook.adi"))).Last();
                check(hunted.Call == "N7XYZ" && hunted.PotaRef == "US-5678" && hunted.Get("SIG") == "POTA" && hunted.Band == "40m" && hunted.Mode == "CW" && hunted.RstSent == "599", "POTA page: logged as a hunter QSO (SIG POTA US-5678, 40 m CW, 599)");
                check(posted.Count == 0, "POTA page: nothing posted to pota.app");
                // the CONTEST page: CQ WW SSB, DL1ABC with 59 and zone 14 filled in, Enter logs it
                h.Hz = 14225000; h.ModeName = "USB";
                h.SetSetting("page", "contest");
                pane.RestorePage();
                ContestPage cp = Controls(pane).OfType<ContestPage>().First();
                List<Field> cf = Controls(cp).OfType<Field>().ToList();
                check(cp.Visible && !call.Visible && cf.Count == 3, "CONTEST page: shown in place of the LOG entry row, with call, RST and zone");
                cf[0].Text = "DL1ABC";
                Application.DoEvents();
                check(cf[1].Text == "59" && cf[2].Text == "14", "CONTEST page: 59 and zone 14 filled in from the call");
                press(cf[0], Keys.Enter);
                Qso cq = Adif.Parse(File.ReadAllText(Path.Combine(h.DataFolder, "logbook.adi"))).Last();
                check(cq.Call == "DL1ABC" && cq.Get("CONTEST_ID") == "CQ-WW-SSB" && cq.Get("SRX_STRING") == "59 14" && cf[0].Text == "", "CONTEST page: Enter logged DL1ABC (CQ-WW-SSB, 59 14) and cleared");
                cf[0].Text = "JA1ABC";
                cf[2].Text = "";
                press(cf[0], Keys.Enter);
                check(cf[0].Text == "JA1ABC", "CONTEST page: with the zone missing, Enter doesn't log");
                cf[2].Text = "25";
                press(cf[2], Keys.Enter);
                cf[0].Text = "DL1ABC";
                Application.DoEvents();
                using (Bitmap bmp = new Bitmap(pane.Width, pane.Height))
                {
                    pane.DrawToBitmap(bmp, new Rectangle(0, 0, pane.Width, pane.Height));
                    bmp.Save(Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "logbook-contest.png"), ImageFormat.Png);
                }
                press(cf[0], Keys.Escape);
                h.SetSetting("page", "log");
                pane.RestorePage();
                check(call.Visible, "CONTEST page: back on LOG, its entry row is back");
                h.SetSetting("page", "awards");
                pane.RestorePage();
                Application.DoEvents();
                check(Controls(pane).OfType<AwardsPage>().First().Visible && call.Visible, "AWARDS page: shown under the entry row");
                using (Bitmap bmp = new Bitmap(pane.Width, pane.Height))
                {
                    pane.DrawToBitmap(bmp, new Rectangle(0, 0, pane.Width, pane.Height));
                    bmp.Save(Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "logbook-awards.png"), ImageFormat.Png);
                }
                h.SetSetting("page", "log");
                pane.RestorePage();
                h.Hz = 14225000; h.ModeName = "USB";
                Book book = new Book(h, "0.1.0");
                book.Store.Load();
                Kainos.Logbook.Services.Uploader upl = new Kainos.Logbook.Services.Uploader(h, book.Store, book.Http, () => "K7JUS");
                upl.Get("eqsl").Enabled = true;
                using (ServicesForm sf = new ServicesForm(book, upl) { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, -4000), ShowInTaskbar = false })
                {
                    sf.Show();
                    Application.DoEvents();
                    using (Bitmap bmp = new Bitmap(sf.Width, sf.Height))
                    {
                        sf.DrawToBitmap(bmp, new Rectangle(0, 0, sf.Width, sf.Height));
                        string png = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "logbook-uploads.png");
                        bmp.Save(png, ImageFormat.Png);
                        check(File.Exists(png), "uploads window: drawn to " + png);
                    }
                    sf.Close();
                }
                upl.Get("eqsl").Enabled = false;
                pane.Running = false;
                f.Controls.Remove(pane);
            }
            m.Stop();
            m.Dispose();
            srv.Dispose();
            check(true, "module: stopped and disposed");
        }

        private static void press(Field f, Keys k)
        {
            MethodInfoHolder.Raise(f.Box, new KeyEventArgs(k));
            Application.DoEvents();
        }

        internal static IEnumerable<Control> AllControls(Control c) { return Controls(c); }

        private static IEnumerable<Control> Controls(Control c)
        {
            foreach (Control x in c.Controls) { yield return x; foreach (Control y in Controls(x)) yield return y; }
        }

        private static void onlineTests(string root)
        {
            using (System.Net.Http.HttpClient http = CallLookup.NewHttp("selftest"))
            {
                string d = Path.Combine(root, "online");
                Directory.CreateDirectory(d);
                bool ok = Cty.DownloadAsync(d, http).GetAwaiter().GetResult();
                bool stale;
                Cty c = Cty.Load(d, out stale);
                check(ok && c.Entities.Count > 300 && !stale, "online: cty.dat downloaded (" + c.Entities.Count + " entities, " + c.Source + ")");
                check(c.Resolve("K7JUS")?.Name == "United States" && c.Resolve("EA8/DL1ABC")?.Name == "Canary Islands" && c.Resolve("ZL7AB")?.Name == "Chatham Islands", "online: K7JUS, EA8/DL1ABC, ZL7AB from the real cty.dat");
                FakeHost h = new FakeHost(Path.Combine(root, "o"));
                CallLookup l = new CallLookup(h, http);
                CallInfo w = l.FindAsync("W1AW", true).GetAwaiter().GetResult();
                check(w != null && w.Source == "callook.info" && w.State == "CT" && w.Grid.StartsWith("FN31", StringComparison.OrdinalIgnoreCase), "online: callook.info knows W1AW (" + (w == null ? l.LastProblem : w.Name + ", " + w.City + " " + w.State + ", " + w.Grid) + ")");
            }
        }
    }

    internal static class Ext
    {
        public static TR Let<T, TR>(this T x, Func<T, TR> f) { return f(x); }
    }

    // raises a TextBox's KeyDown as if the key were pressed
    internal static class MethodInfoHolder
    {
        public static void Raise(TextBox b, KeyEventArgs e)
        {
            typeof(Control).GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(b, new object[] { e });
        }
    }
}
