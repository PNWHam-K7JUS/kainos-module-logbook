/*  LogbookModule.cs

Kainos Logbook: a logbook in the Kainos look, as a downloadable module (Setup > Modules). Lookup as you type
(country, zones, beam heading, distance from cty.dat; name, QTH and grid from QRZ.com, HamQTH or callook.info),
worked-before by band and mode, QSOs from VFO A and from the Digital Modes and SSTV modules, ADIF import and
ADIF / ADX / CSV export. Opened from its LOG button in the left-hand dock, or Tools > Logbook.

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

Copyright (C) 2026 Justin Cron K7JUS
*/

using System;
using Kainos.Logbook.UI;
using Kainos.Modules;

namespace Kainos.Logbook
{
    public class LogbookModule : IKainosModule
    {
        private IKainosHost _host;
        private Book _book;
        private Feeds _feeds;
        private LogbookPane _view;
        private IKainosPane _pane;
        private IKainosMenuItem _menu;

        public void Start(IKainosHost host)
        {
            _host = host;
            _book = new Book(host, ModuleVersion);
            try { _book.Store.Load(); }
            catch (Exception ex) { host.Log("couldn't read logbook.adi: " + ex.Message); }
            _feeds = new Feeds(host, _book.Store);
            _view = new LogbookPane(_book, _feeds);
            _pane = host.CreatePane("Kainos Logbook", _view);
            _pane.Closed += (s, e) => close();
            _view.PopToggled += () => _pane.Popped = !_pane.Popped;
            Action toggle = () => { if (_pane.IsOpen) close(); else open(); };
            const string tip = "The logbook: lookup, worked-before, the log. Open / close.";
            IKainosDock dock = host as IKainosDock;
            _menu = dock != null ? dock.AddDockButton("LOG", "Logbook", tip, toggle) : null;
            if (_menu == null) _menu = host.AddMenuItem("Logbook", tip, toggle);
            host.Log("Logbook " + ModuleVersion + " started: " + _book.Store.Count + " QSOs, countries from " + _book.Cty.Source);
            if (host.GetSetting("open", "0") == "1") open();
        }

        internal static string ModuleVersion { get { Version v = typeof(LogbookModule).Assembly.GetName().Version; return v.Major + "." + v.Minor + "." + v.Build; } }

        private void open()
        {
            _pane.Open();
            _view.Running = true;
            _view.FocusCall();
            _menu.Checked = true;
            _host.SetSetting("open", "1");
        }

        private void close()
        {
            _view.Running = false;
            _pane.Close();
            _menu.Checked = false;
            _host.SetSetting("open", "0");
        }

        public void Stop()
        {
            if (_view != null) _view.Running = false;
        }

        public void Dispose()
        {
            if (_view != null && !_view.IsDisposed) _view.Dispose();
            if (_book != null) _book.Http.Dispose();
        }
    }
}
