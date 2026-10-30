# Third-party code and data

The Logbook module is all its own code, under GPL-3.0-or-later. It runs inside Kainos (GPL-3.0-or-later), which is based on Thetis.

It doesn't ship anyone else's data. When it runs, it downloads and uses:

- **cty.dat** by Jim Reisert AD1C, from https://www.country-files.com (the country file contest loggers use).

When you set them up in its settings, it asks these services about the calls you type:

- **QRZ.com** XML data service (https://www.qrz.com), with your QRZ account.
- **HamQTH** (https://www.hamqth.com), with your HamQTH account.
- **callook.info** (https://callook.info), for US calls, from the FCC's licence data.
