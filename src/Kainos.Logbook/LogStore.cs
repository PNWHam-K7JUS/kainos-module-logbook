/*  LogStore.cs

The log: an ADIF file in the module's data folder (logbook.adi), so it's never locked away in a format only this
module reads. A new QSO is appended; an edit or delete rewrites the file (to a temporary file, then swapped in, with
the previous one kept as logbook.adi.bak). Kept sorted by time in memory. Thread-safe; Changed is raised on whatever
thread made the change.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Kainos.Logbook
{
    internal sealed class LogStore
    {
        private readonly object _lock = new object();
        private readonly List<Qso> _qsos = new List<Qso>();
        private readonly string _version;
        public readonly string Path;

        public event Action Changed;

        public LogStore(string folder, string version)
        {
            Path = System.IO.Path.Combine(folder, "logbook.adi");
            _version = version;
        }

        public int Count { get { lock (_lock) return _qsos.Count; } }

        // a copy, oldest first
        public List<Qso> All() { lock (_lock) return _qsos.ToList(); }

        public void Load()
        {
            List<Qso> read = File.Exists(Path) ? Adif.Parse(File.ReadAllText(Path, Encoding.UTF8)) : new List<Qso>();
            bool fixedIds = false;
            foreach (Qso q in read) if (q.Id.Length == 0) { q.Id = Qso.NewId(); fixedIds = true; }
            lock (_lock)
            {
                _qsos.Clear();
                _qsos.AddRange(read.OrderBy(q => q.TimeOn));
            }
            if (fixedIds) save();
            raise();
        }

        public Qso Find(string id) { lock (_lock) return _qsos.FirstOrDefault(q => q.Id == id); }

        public void Add(Qso q)
        {
            if (q.Id.Length == 0) q.Id = Qso.NewId();
            stampAdded(q);
            lock (_lock)
            {
                bool last = _qsos.Count == 0 || q.TimeOn >= _qsos[_qsos.Count - 1].TimeOn;
                insertSorted(q);
                if (last && File.Exists(Path)) File.AppendAllText(Path, Adif.Record(q) + "\r\n", Encoding.UTF8);
                else save();
            }
            raise();
        }

        // QSOs from elsewhere (an import, another module's log): those already here are skipped. Returns how many
        // were added.
        public int AddMany(IEnumerable<Qso> qsos)
        {
            int n = 0;
            lock (_lock)
            {
                foreach (Qso q in qsos)
                {
                    if (q.Call.Length == 0 || q.TimeOn == DateTime.MinValue || isDupe(q)) continue;
                    if (q.Id.Length == 0 || _qsos.Any(x => x.Id == q.Id)) q.Id = Qso.NewId();
                    stampAdded(q);
                    insertSorted(q);
                    n++;
                }
                if (n > 0) save();
            }
            if (n > 0) raise();
            return n;
        }

        // the same QSO: same call and band, within two minutes (programs round the time differently)
        private bool isDupe(Qso q)
        {
            DateTime t = q.TimeOn;
            string call = q.Call, band = q.Band;
            return _qsos.Any(x => x.Call == call && (band.Length == 0 || x.Band.Length == 0 || x.Band == band) && Math.Abs((x.TimeOn - t).TotalMinutes) <= 2);
        }

        public void Update(Qso q)
        {
            lock (_lock)
            {
                int i = _qsos.FindIndex(x => x.Id == q.Id);
                if (i < 0) return;
                _qsos.RemoveAt(i);
                insertSorted(q);
                save();
            }
            raise();
        }

        // changes to QSOs already in the log (an upload's result, a confirmation), made to the QSO as it is now so an
        // edit made meanwhile isn't lost. Returns how many were changed.
        public int Modify(IEnumerable<string> ids, Action<Qso> change)
        {
            HashSet<string> want = new HashSet<string>(ids);
            int n = 0;
            lock (_lock)
            {
                foreach (Qso q in _qsos)
                    if (want.Contains(q.Id)) { change(q); n++; }
                if (n > 0) save();
            }
            if (n > 0) raise();
            return n;
        }

        // when the QSO came into this log (UTC), so uploads switched on later don't send the whole old log
        private static void stampAdded(Qso q)
        {
            if (q.Get("APP_KAINOS_ADDED").Length == 0) q.Set("APP_KAINOS_ADDED", DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture));
        }

        public void Delete(string id)
        {
            lock (_lock)
            {
                if (_qsos.RemoveAll(x => x.Id == id) == 0) return;
                save();
            }
            raise();
        }

        private void insertSorted(Qso q)
        {
            int i = _qsos.Count;
            while (i > 0 && _qsos[i - 1].TimeOn > q.TimeOn) i--;
            _qsos.Insert(i, q);
        }

        private void save()
        {
            string tmp = Path + ".tmp";
            File.WriteAllText(tmp, Adif.Write(_qsos, _version), Encoding.UTF8);
            if (File.Exists(Path))
            {
                string bak = Path + ".bak";
                if (File.Exists(bak)) File.Delete(bak);
                File.Replace(tmp, Path, bak);
            }
            else File.Move(tmp, Path);
        }

        private void raise() { Changed?.Invoke(); }
    }
}
