/*  Uploader.cs

Sends what's waiting to each service that's switched on: every 20 seconds and straight after a QSO is logged, for
the services set to send as you log; on Send now for the rest. A QSO is marked sent when the service took it (or
already had it). After a failure the service waits a minute, then 5, then 15 before trying again; after a refused
login it stops until the settings are saved again. Nothing is ever sent for a service that's off.

Confirmations are fetched only when asked (Get confirmations), and matched to the log.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Kainos.Modules;

namespace Kainos.Logbook.Services
{
    internal sealed class ServiceState
    {
        public string Last = "";            // what happened last, for the window and the status line
        public DateTime LastAt;
        public int Failures;
        public DateTime RetryAt = DateTime.MinValue;
        public bool LoginRefused;
        public bool Busy;
    }

    internal sealed class Uploader
    {
        private readonly IKainosHost _host;
        private readonly LogStore _store;
        public readonly List<Service> Services;
        private readonly Dictionary<string, ServiceState> _state = new Dictionary<string, ServiceState>();
        private readonly SemaphoreSlim _one = new SemaphoreSlim(1, 1);
        private Timer _timer;

        public event Action<string> Status;         // "LoTW: 3 QSOs uploaded"
        public event Action Changed;                // counts or states changed

        public Uploader(IKainosHost host, LogStore store, HttpClient http, Func<string> myCall)
        {
            _host = host;
            _store = store;
            Services = new List<Service> { new Lotw(host, http), new Eqsl(host, http), new QrzLogbook(host, http), new ClubLog(host, http, myCall) };
            foreach (Service s in Services) _state[s.Key] = new ServiceState();
        }

        public Service Get(string key) { return Services.First(s => s.Key == key); }
        public ServiceState State(Service s) { return _state[s.Key]; }

        public List<Qso> Waiting(Service s) { return _store.All().Where(s.Waiting).ToList(); }
        public int WaitingTotal { get { List<Qso> all = _store.All(); return Services.Where(s => s.Enabled).Sum(s => all.Count(s.Waiting)); } }

        public void Start() { if (_timer == null) _timer = new Timer(_ => Kick(false), null, 15000, 20000); }
        public void Stop() { if (_timer != null) { _timer.Dispose(); _timer = null; } }

        // the settings were saved: try again at once
        public void Reset() { foreach (ServiceState st in _state.Values) { st.LoginRefused = false; st.Failures = 0; st.RetryAt = DateTime.MinValue; } }

        // auto services, now (after a QSO is logged, and on the timer); manual: one service, whatever its setting
        public void Kick(bool afterLog) { Task.Run(() => RunAsync(null)); }

        public Task SendNowAsync(Service s) { return RunAsync(s); }

        public async Task RunAsync(Service only)
        {
            if (!await _one.WaitAsync(0).ConfigureAwait(false)) return;    // already running
            try
            {
                foreach (Service s in Services)
                {
                    if (only != null && s != only) continue;
                    if (!s.Enabled) continue;
                    if (only == null && !s.Auto) continue;
                    ServiceState st = _state[s.Key];
                    if (only == null && (st.LoginRefused || DateTime.UtcNow < st.RetryAt)) continue;
                    if (!s.HasLogin) { if (only != null) note(s, "not set up yet"); continue; }
                    List<Qso> waiting = Waiting(s);
                    if (waiting.Count == 0) { if (only != null) note(s, "nothing waiting"); continue; }
                    st.Busy = true;
                    Changed?.Invoke();
                    try { await sendAll(s, st, waiting).ConfigureAwait(false); }
                    finally { st.Busy = false; Changed?.Invoke(); }
                }
            }
            catch (Exception ex) { _host.Log("upload: " + ex); }
            finally { _one.Release(); }
        }

        private async Task sendAll(Service s, ServiceState st, List<Qso> waiting)
        {
            int sent = 0, dupes = 0, failed = 0;
            string message = "";
            for (int i = 0; i < waiting.Count; i += s.BatchSize)
            {
                List<Qso> batch = waiting.Skip(i).Take(s.BatchSize).Select(q => q.Clone()).ToList();
                SendResult r = await s.SendAsync(batch).ConfigureAwait(false);
                message = r.Message;
                List<string> ok = r.ByQso.Where(kv => kv.Value == Outcome.Sent || kv.Value == Outcome.Duplicate).Select(kv => kv.Key).ToList();
                sent += r.ByQso.Count(kv => kv.Value == Outcome.Sent);
                dupes += r.ByQso.Count(kv => kv.Value == Outcome.Duplicate);
                failed += r.ByQso.Count(kv => kv.Value == Outcome.Failed);
                if (ok.Count > 0)
                    _store.Modify(ok, q =>
                    {
                        s.MarkSent(q);
                        foreach (Tuple<string, string, string> f in r.Fields) if (f.Item1 == q.Id) q.Set(f.Item2, f.Item3);
                    });
                if (r.ByQso.Values.Any(o => o == Outcome.LoginFailed))
                {
                    st.LoginRefused = true;
                    note(s, message + ". Nothing more is sent until you check the settings.");
                    return;
                }
                if (r.ByQso.Values.Any(o => o == Outcome.Failed)) break;
            }
            if (failed > 0)
            {
                st.Failures++;
                int wait = st.Failures == 1 ? 1 : st.Failures == 2 ? 5 : 15;
                st.RetryAt = DateTime.UtcNow.AddMinutes(wait);
                note(s, (sent + dupes > 0 ? (sent + dupes) + " sent, then " : "") + message + " (trying again in " + wait + " min)");
            }
            else
            {
                st.Failures = 0;
                note(s, sent + " QSO" + (sent == 1 ? "" : "s") + " sent" + (dupes > 0 ? ", " + dupes + " already there" : ""));
            }
        }

        // confirmations, when asked: matched to the log; returns a line for the status
        public async Task<string> FetchAsync(Service s)
        {
            if (!s.Confirms) return s.Name + " doesn't confirm QSOs";
            ServiceState st = _state[s.Key];
            st.Busy = true;
            Changed?.Invoke();
            try
            {
                Tuple<List<Qso>, string> got = await s.FetchConfirmationsAsync().ConfigureAwait(false);
                if (got.Item2.Length > 0) { note(s, got.Item2); return s.Name + ": " + got.Item2; }
                List<Qso> log = _store.All();
                Dictionary<string, Qso> hits = new Dictionary<string, Qso>();
                foreach (Qso c in got.Item1)
                {
                    Qso q = QsoMatch.Find(log, c);
                    if (q != null && !hits.ContainsKey(q.Id)) hits[q.Id] = c;
                }
                int n = _store.Modify(hits.Keys, q => s.MarkConfirmed(q, hits[q.Id]));
                string line = got.Item1.Count + " confirmation" + (got.Item1.Count == 1 ? "" : "s") + " received, " + n + " matched to the log";
                note(s, line);
                return s.Name + ": " + line;
            }
            catch (Exception ex)
            {
                string m = "couldn't get confirmations: " + (ex.InnerException ?? ex).Message;
                note(s, m);
                return s.Name + ": " + m;
            }
            finally { st.Busy = false; Changed?.Invoke(); }
        }

        // a QSO was edited: QRZ and Club Log take the change, so send it to them again
        public static void Edited(Qso before, Qso after)
        {
            if (before.Get("QRZCOM_QSO_UPLOAD_STATUS") == "Y") after.Set("APP_KAINOS_QRZ_DIRTY", "Y");
            if (before.Get("CLUBLOG_QSO_UPLOAD_STATUS") == "Y") after.Set("CLUBLOG_QSO_UPLOAD_STATUS", null);
        }

        private void note(Service s, string what)
        {
            ServiceState st = _state[s.Key];
            st.Last = what;
            st.LastAt = DateTime.UtcNow;
            _host.Log("upload " + s.Key + ": " + what);
            Status?.Invoke(s.Name + ": " + what);
            Changed?.Invoke();
        }
    }
}
