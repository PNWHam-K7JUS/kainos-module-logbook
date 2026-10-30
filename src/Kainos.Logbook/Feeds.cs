/*  Feeds.cs

QSOs logged by Kainos's other modules come into the logbook by themselves: the Digital Modes module (FT8, FT4 ...)
appends to kainos-digimodes.adi and the SSTV module to kainos-sstv.adi, each in its own data folder next to this
module's. Every few seconds the new end of each file is read (from where the last read stopped, kept in the
settings) and added; QSOs already in the log are skipped, so nothing comes in twice.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Kainos.Modules;

namespace Kainos.Logbook
{
    internal sealed class Feeds
    {
        public static readonly string[][] Sources =
        {
            new[] { "digimodes", "kainos-digimodes.adi", "Digital Modes (FT8, FT4 ...)" },
            new[] { "sstv", "kainos-sstv.adi", "SSTV" },
        };

        private readonly IKainosHost _host;
        private readonly LogStore _store;
        private readonly string _root;

        public event Action<int, string> Added;     // how many, from which module
        public Action<Qso> Adjust;                  // before they're added (a POTA activation adds the park)

        public Feeds(IKainosHost host, LogStore store)
        {
            _host = host;
            _store = store;
            _root = Path.GetDirectoryName(host.DataFolder.TrimEnd('\\', '/'));
        }

        public bool On { get { return _host.GetSetting("feeds", "1") == "1"; } set { _host.SetSetting("feeds", value ? "1" : "0"); } }

        public string FileOf(string[] src) { return Path.Combine(_root, src[0], src[1]); }

        // called on a timer (any thread)
        public void Poll()
        {
            if (!On) return;
            foreach (string[] src in Sources)
            {
                try { pollOne(src); }
                catch (Exception ex) { _host.Log("feed " + src[0] + ": " + ex.Message); }
            }
        }

        private void pollOne(string[] src)
        {
            string f = FileOf(src);
            if (!File.Exists(f)) return;
            string key = "feed." + src[0] + ".pos";
            long pos;
            long.TryParse(_host.GetSetting(key, "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out pos);
            long len = new FileInfo(f).Length;
            if (len < pos) pos = 0;         // the file was replaced or emptied: read it again (dupes are skipped)
            if (len == pos) return;
            string text;
            using (FileStream fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                fs.Seek(pos, SeekOrigin.Begin);
                byte[] b = new byte[len - pos];
                int got = 0;
                while (got < b.Length) { int n = fs.Read(b, got, b.Length - got); if (n <= 0) break; got += n; }
                // stop at the last complete record, so one being written isn't read in half
                string all = Encoding.UTF8.GetString(b, 0, got);
                int end = all.LastIndexOf("<eor>", StringComparison.OrdinalIgnoreCase);
                if (end < 0) return;
                text = all.Substring(0, end + 5);
                pos += Encoding.UTF8.GetByteCount(text);
            }
            // the file's header is only in its first read; later reads start mid-file with a record
            List<Qso> qsos = Adif.Parse(text.IndexOf("<eoh>", StringComparison.OrdinalIgnoreCase) >= 0 ? text : "<EOH>" + text);
            foreach (Qso q in qsos) { q.Set("APP_KAINOS_FROM", src[0]); q.LoggedHere = true; Adjust?.Invoke(q); }
            int added = _store.AddMany(qsos);
            _host.SetSetting(key, pos.ToString(CultureInfo.InvariantCulture));
            if (added > 0) { _host.Log("feed " + src[0] + ": " + added + " QSO(s) added"); Added?.Invoke(added, src[2]); }
        }
    }
}
