/*  Bands.cs

Bands by frequency (the ADIF band names), and modes: Kainos's mode names to ADIF, and each mode's class (CW,
phone or digital) for worked-before and awards.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Linq;

namespace Kainos.Logbook
{
    internal enum ModeClass { CW, Phone, Digital }

    internal static class Bands
    {
        // name, low MHz, high MHz (ADIF's band edges)
        public static readonly string[] Names = { "2190m", "630m", "160m", "80m", "60m", "40m", "30m", "20m", "17m", "15m", "12m", "10m", "6m", "4m", "2m", "1.25m", "70cm" };
        private static readonly double[][] Edges =
        {
            new[] { 0.1357, 0.1378 }, new[] { 0.472, 0.479 }, new[] { 1.8, 2.0 }, new[] { 3.5, 4.0 }, new[] { 5.06, 5.45 }, new[] { 7.0, 7.3 },
            new[] { 10.1, 10.15 }, new[] { 14.0, 14.35 }, new[] { 18.068, 18.168 }, new[] { 21.0, 21.45 }, new[] { 24.89, 24.99 }, new[] { 28.0, 29.7 },
            new[] { 50.0, 54.0 }, new[] { 70.0, 71.0 }, new[] { 144.0, 148.0 }, new[] { 222.0, 225.0 }, new[] { 420.0, 450.0 },
        };

        // the bands shown in worked-before (HF and 6 m: what Kainos's radios cover)
        public static readonly string[] Grid = { "160m", "80m", "60m", "40m", "30m", "20m", "17m", "15m", "12m", "10m", "6m" };

        public static string Of(double mhz)
        {
            for (int i = 0; i < Names.Length; i++)
                if (mhz >= Edges[i][0] && mhz <= Edges[i][1]) return Names[i];
            return "";
        }

        public static int Index(string band) { return Array.IndexOf(Names, (band ?? "").ToLowerInvariant()); }

        // Kainos (Thetis) mode name to ADIF mode / submode
        public static void ToAdif(string kainosMode, out string mode, out string submode)
        {
            submode = null;
            switch ((kainosMode ?? "").ToUpperInvariant())
            {
                case "USB": mode = "SSB"; submode = "USB"; break;
                case "LSB": mode = "SSB"; submode = "LSB"; break;
                case "CWU": case "CWL": mode = "CW"; break;
                case "AM": case "SAM": case "DSB": mode = "AM"; break;
                case "FM": mode = "FM"; break;
                case "DIGU": case "DIGL": mode = "FT8"; break;     // a guess: the FT8 module logs its own QSOs
                case "DRM": mode = "DIGITALVOICE"; break;
                default: mode = (kainosMode ?? "").ToUpperInvariant(); break;
            }
        }

        // the RST to start with, by class
        public static string DefaultRst(ModeClass c) { return c == ModeClass.Phone ? "59" : c == ModeClass.CW ? "599" : "-10"; }

        private static readonly string[] Phone = { "SSB", "USB", "LSB", "AM", "FM", "DIGITALVOICE", "FREEDV", "C4FM", "DSTAR", "DMR" };

        public static ModeClass ClassOf(string mode)
        {
            string m = (mode ?? "").ToUpperInvariant();
            if (m == "CW") return ModeClass.CW;
            if (Phone.Contains(m)) return ModeClass.Phone;
            return ModeClass.Digital;
        }

        public static string ClassName(ModeClass c) { return c == ModeClass.CW ? "CW" : c == ModeClass.Phone ? "SSB" : "DIG"; }
    }
}
