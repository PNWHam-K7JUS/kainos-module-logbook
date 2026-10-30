# Kainos Logbook

A module for [Kainos](https://github.com/PNWHam-K7JUS/Kainos), the Hermes Lite 2 SDR program: a logbook inside Kainos, in the Kainos look. Install it from **Setup > Modules** if you want it. It needs **Kainos 1.0.12** or later.

This is version 0.3: the LOG page, uploads and POTA. Contests and awards are coming in the versions after it.

## Using it

Open it with the **LOG** button in the left-hand dock (lit while it's open), or **Tools > Logbook**. It docks under the panadapter like the RTTY and CW terminals, or pops out into its own window.

- **Type a call.** With each letter the lookup shows the country, continent, CQ and ITU zones, distance, beam heading (short and long path, with a compass) and their local time. When you stop typing it asks **QRZ.com**, **HamQTH** or **callook.info** for the name, QTH, grid and QSL details, and puts their first name in the Name box.
- **Worked before.** A grid of the bands (160 to 6 m) by CW, SSB and digital for the call's country: blue where you've worked it, gold where it's confirmed, and a gold outline where you are now. Above the log, the alerts: **NEW ONE**, **NEW BAND**, **NEW MODE**, worked / confirmed, and **WORKED TODAY** for a dupe.
- **Enter logs it, Esc clears.** The time is when you started typing the call, and the frequency, band and mode come from VFO A (the transmit VFO). RST starts at 59, 599 or -10 by mode. **Their park** is for a POTA activator's park (logged as SIG POTA).
- **The log**, newest first. Double-click a QSO to edit it; right-click to edit, look up the call again, tick a paper card, or delete it. The **L E Q C** badges show LoTW, eQSL, QRZ and Club Log (dim: not sent, blue: sent, gold: confirmed). Click **P** when a paper card arrives.
- **FT8 and SSTV QSOs come in by themselves.** QSOs logged by the Digital Modes and SSTV modules are added within a few seconds (while the logbook is open; any logged while it's closed come in when you open it).
- **Import…** an ADIF file from another logger, LoTW or QRZ. QSOs already in the log (same call and band within two minutes) are skipped. **Export…** saves ADIF (for LoTW through TQSL, eQSL, QRZ, Club Log, other loggers), ADX or CSV, with dates, band and mode to choose from.

## Uploads

**Uploads…** on the toolbar (it shows how many QSOs are waiting) has LoTW, eQSL, QRZ Logbook and Club Log. **Each one starts switched off**, and nothing is sent to a service that's off.

- **Switched on, a service sends the QSOs you log from then on**: on the LOG page, and from the FT8 and SSTV modules. An imported log, or QSOs from before you switched it on, go only if you press **Send older QSOs…** (it asks first, and says how many).
- **Send each QSO as I log it**, or untick it and press **Send now** when you're ready.
- **Preview…** shows exactly what would be sent, with passwords and keys hidden. It sends nothing.
- **Get confirmations** fetches the QSLs received since the last time (LoTW, eQSL and QRZ; Club Log doesn't confirm) and matches them to the log: same call and band, the same kind of mode, within 30 minutes. It's only ever done when you press it.
- The **L E Q C** badges in the log turn blue when a QSO is sent and gold when it's confirmed.
- If a service can't be reached it tries again after 1, 5 and then 15 minutes. If it refuses the login it stops until you save its settings again.

| Service | How |
|---|---|
| **LoTW** | TQSL (ARRL's program, which holds your certificate) signs and uploads: the logbook runs `tqsl -q -d -u -x -a compliant -l "<station location>" <file>`. Pick the station location as it's named in TQSL. Confirmations use your LoTW website login. |
| **eQSL** | Each QSO is posted to eQSL's ADIF import with your eQSL login. Confirmations are your eQSL inbox. |
| **QRZ Logbook** | Your logbook's API key (QRZ.com > Logbook > Settings). An edited QSO is sent again and replaces the one on QRZ. |
| **Club Log** | Your Club Log email, an application password (Club Log > Settings) and an API key (Club Log gives each program one). An edited QSO is sent again and Club Log updates it. |

LoTW and eQSL keep what's sent to them (LoTW can't delete a QSO), so an edit there isn't sent again.

## POTA

The **POTA** tab, next to LOG, keeps the entry row and shows Parks on the Air in place of the log:

- **Hunting:** the activators on the air now, from pota.app, refreshed every minute while the page is open. Filter by CW, SSB or digital and by band, and hide the ones you've already logged today. **NEW PARK** marks a park you've never worked. **Double-click a spot** (or press Enter on it) to tune VFO A to it in its mode (SSB as LSB below 10 MHz, USB above; CW; digital as DIGU) and fill in the call and park. Logged, it's a hunter QSO (SIG POTA). Hunters needn't upload: pota.app credits you from the activator's log.
- **Their park**, typed on either page, shows the park's name on the lookup card.
- **Activating:** enter your park (two or more for a two-fer) and your grid there, and press **Start activation**. Until you press **End activation**, every QSO you log (here, and from the FT8 and SSTV modules) gets your park(s) and grid: MY_SIG POTA, MY_SIG_INFO, MY_POTA_REF, MY_GRIDSQUARE. The bar counts to the 10 QSOs that make it count, and park-to-park QSOs are counted too.
- **Spot me…** opens a window with the spot (your call, VFO A's frequency and mode, the park, a comment). Nothing is sent until you press **Send**.
- **Export for pota.app…** saves the activation as pota.app wants it: one file per park and UTC day, named `CALL@PARK-YYYYMMDD.adi`. Upload them at pota.app > My Logs.

## Settings

- **Station:** your call, grid and power. Kainos's own call and grid are used unless you change them here.
- **Lookups:** QRZ.com (username and password; a QRZ XML subscription gives the full record, without one QRZ gives the name and country), HamQTH (free, with a free account at hamqth.com), callook.info (US calls, free, no account). Each has a **Test** button. Passwords are kept encrypted for your Windows account only (DPAPI).
- **Confirmed means:** LoTW + paper cards (what ARRL and CQ credit, the default), eQSL, QRZ, or any confirmation.
- **Countries:** AD1C's cty.dat from country-files.com, downloaded the first time and every two weeks after. Until it's downloaded a short built-in list covers the common prefixes.

## Where the log is

`logbook.adi` in the module's data folder (`%APPDATA%\OpenHPSDR\Kainos-x64\ModuleData\logbook\`). It's plain ADIF, so any logging program can read it. Before each edit or delete the previous version is kept as `logbook.adi.bak`.

## How it works

| Part | What it does |
|---|---|
| `LogbookModule.cs` | The module: its LOG dock button (`IKainosDock`, with a Tools menu item) and the pane. |
| `Book.cs` | The log, countries and lookups together: worked-before, what counts as confirmed, a QSO from VFO A. |
| `LogStore.cs` | The log file: appended for a new QSO, rewritten (with a .bak) for an edit or delete; duplicates skipped on import. |
| `Adif.cs`, `Export.cs` | ADIF read (by field length) and write; ADX and CSV. |
| `Cty.cs` | cty.dat: prefixes, whole calls, the zone / place / continent overrides, portable calls (EA8/DL1ABC, K7JUS/4). |
| `Lookup/` | QRZ.com XML, HamQTH and callook.info; passwords kept with DPAPI. |
| `Feeds.cs` | QSOs from the Digital Modes and SSTV modules' ADIF logs. |
| `Pota.cs`, `UI/PotaPage.cs` | POTA: spots and park names from api.pota.app, the activation (the park on each QSO), the files for pota.app, spotting yourself. |
| `Services/` | The uploads: LoTW (through TQSL), eQSL, QRZ Logbook and Club Log; what's waiting, sending and retrying, and confirmations matched to the log. |
| `UI/` | The pane, the lookup card (drawn), the log table, and the settings, edit and export windows. |

## Building

```
.\build.ps1
```

It builds the module and its self-test against `Kainos.ModuleApi.dll` from a Kainos build next to this folder (or `-KainosBin <folder>`), runs the self-test (ADIF, cty.dat, grids and headings, the log file, worked-before, the FT8 feed, the exports, POTA (spots, activations, the files, spotting) and uploads and confirmations against stand-ins for every service and for TQSL (a little web server on this PC, so nothing reaches a real log), and the pane and the Uploads window, drawn to PNGs), and writes `dist\kainos-logbook-<version>.zip` and `dist\registry-entry.json`. `Kainos.Logbook.SelfTest.exe --online` also downloads cty.dat and looks up W1AW on callook.info.

## Licence

GPL-3.0-or-later. Copyright (C) 2026 Justin Cron K7JUS.
