/*  Service.cs

What every upload service has: switched on or not (off until you switch it on), whether it sends each QSO as it's
logged or waits for Send now, which QSOs are waiting, sending them, a preview of exactly what would be sent (with
passwords hidden), and, for the ones that confirm, fetching confirmations.

Which QSOs go: those logged here (the LOG page, the FT8 and SSTV modules) since the service was switched on, and
not yet sent. An imported log, or QSOs from before, go only if you ask ("Send older QSOs").

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Kainos.Modules;

namespace Kainos.Logbook.Services
{
    internal enum Outcome { Sent, Duplicate, Failed, LoginFailed }

    internal sealed class SendResult
    {
        public readonly Dictionary<string, Outcome> ByQso = new Dictionary<string, Outcome>();
        public string Message = "";
        public readonly List<Tuple<string, string, string>> Fields = new List<Tuple<string, string, string>>();     // id, field, value to keep
        public void All(IEnumerable<Qso> qsos, Outcome o) { foreach (Qso q in qsos) ByQso[q.Id] = o; }
        public void Keep(string id, string field, string value) { Fields.Add(Tuple.Create(id, field, value)); }
    }

    internal abstract class Service
    {
        protected readonly IKainosHost Host;
        protected readonly HttpClient Http;

        protected Service(IKainosHost host, HttpClient http) { Host = host; Http = http; }

        public abstract string Key { get; }         // "lotw", "eqsl", "qrz", "clublog"
        public abstract string Name { get; }
        public abstract bool Confirms { get; }      // can fetch confirmations
        public virtual int BatchSize { get { return 50; } }

        public abstract bool IsSent(Qso q);
        public abstract void MarkSent(Qso q);
        public abstract bool HasLogin { get; }      // enough set up to send
        public abstract Task<SendResult> SendAsync(List<Qso> qsos);
        public abstract string Preview(List<Qso> qsos);
        public virtual Task<Tuple<List<Qso>, string>> FetchConfirmationsAsync() { return Task.FromResult(Tuple.Create(new List<Qso>(), "")); }
        public virtual void MarkConfirmed(Qso q, Qso from) { }

        public bool Enabled
        {
            get { return Host.GetSetting("svc." + Key + ".on", "0") == "1"; }
            set
            {
                if (value && !Enabled && Since == DateTime.MaxValue) Since = DateTime.UtcNow;
                Host.SetSetting("svc." + Key + ".on", value ? "1" : "0");
            }
        }

        public bool Auto { get { return Host.GetSetting("svc." + Key + ".auto", "1") == "1"; } set { Host.SetSetting("svc." + Key + ".auto", value ? "1" : "0"); } }

        // QSOs added from this time on are sent; MinValue: everything not yet sent, imported or not
        public DateTime Since
        {
            get
            {
                string s = Host.GetSetting("svc." + Key + ".since", "");
                if (s == "all") return DateTime.MinValue;
                DateTime r;
                return DateTime.TryParseExact(s, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out r) ? r : DateTime.MaxValue;
            }
            set { Host.SetSetting("svc." + Key + ".since", value == DateTime.MinValue ? "all" : value.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)); }
        }

        public bool Waiting(Qso q)
        {
            if (IsSent(q)) return false;
            DateTime since = Since;
            if (since == DateTime.MinValue) return true;
            return q.LoggedHere && q.AddedUtc >= since;
        }

        // not sent, and not counted by Waiting: what "Send older QSOs" would add
        public bool Older(Qso q) { return !IsSent(q) && !Waiting(q); }

        // the address of the service: its real one, or a stand-in (the self-test's, set in "svc.base")
        protected string Url(string realBase, string path)
        {
            string b = Host.GetSetting("svc.base", "");
            return (b.Length > 0 ? b.TrimEnd('/') : realBase.TrimEnd('/')) + path;
        }

        protected static string Mask(string text, params string[] secrets)
        {
            foreach (string s in secrets) if (!string.IsNullOrEmpty(s)) text = text.Replace(s, new string('•', 8));
            return text;
        }

        protected static string Date(DateTime d) { return d.ToString("yyyyMMdd", CultureInfo.InvariantCulture); }

        // the fields a QSO is sent with: what other programs read, not the logbook's own bookkeeping
        protected static string Record(Qso q) { return Adif.Record(q, Adif.Public); }
    }

    // a confirmation matches a logged QSO: the same call and band, the same kind of mode, within 30 minutes (as LoTW
    // matches them)
    internal static class QsoMatch
    {
        public static Qso Find(List<Qso> log, Qso conf)
        {
            string call = Cty.BaseCall(conf.Call, out string _);
            DateTime t = conf.TimeOn;
            string band = conf.Band;
            ModeClass cls = conf.Class;
            return log.Where(q => q.Band == band && q.Class == cls && Math.Abs((q.TimeOn - t).TotalMinutes) <= 30 && Cty.BaseCall(q.Call, out string _) == call)
                      .OrderBy(q => Math.Abs((q.TimeOn - t).TotalSeconds)).FirstOrDefault();
        }
    }
}
