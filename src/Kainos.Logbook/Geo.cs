/*  Geo.cs

Maidenhead grid squares, great-circle distance and beam heading.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Text.RegularExpressions;

namespace Kainos.Logbook
{
    internal static class Geo
    {
        private static readonly Regex GridRx = new Regex("^[A-R]{2}[0-9]{2}([A-X]{2})?", RegexOptions.IgnoreCase);

        public static bool IsGrid(string g) { return !string.IsNullOrEmpty(g) && GridRx.IsMatch(g); }

        // the middle of the square (4 or 6 characters)
        public static bool GridToLatLon(string g, out double lat, out double lon)
        {
            lat = lon = 0;
            if (!IsGrid(g)) return false;
            g = g.ToUpperInvariant();
            lon = (g[0] - 'A') * 20 - 180 + (g[2] - '0') * 2;
            lat = (g[1] - 'A') * 10 - 90 + (g[3] - '0');
            if (g.Length >= 6 && char.IsLetter(g[4]) && char.IsLetter(g[5]))
            {
                lon += (g[4] - 'A') / 12.0 + 1 / 24.0;
                lat += (g[5] - 'A') / 24.0 + 1 / 48.0;
            }
            else { lon += 1; lat += 0.5; }
            return true;
        }

        public static string LatLonToGrid(double lat, double lon, int chars = 6)
        {
            lon = Math.Max(-180, Math.Min(179.9999, lon)) + 180;
            lat = Math.Max(-90, Math.Min(89.9999, lat)) + 90;
            char[] c = new char[6];
            c[0] = (char)('A' + (int)(lon / 20)); c[1] = (char)('A' + (int)(lat / 10));
            c[2] = (char)('0' + (int)(lon % 20 / 2)); c[3] = (char)('0' + (int)(lat % 10));
            c[4] = (char)('a' + (int)(lon % 2 * 12)); c[5] = (char)('a' + (int)(lat % 1 * 24));
            return new string(c, 0, chars >= 6 ? 6 : 4);
        }

        private const double R = Math.PI / 180;

        public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            double a = Math.Sin(lat1 * R) * Math.Sin(lat2 * R) + Math.Cos(lat1 * R) * Math.Cos(lat2 * R) * Math.Cos((lon2 - lon1) * R);
            return Math.Acos(Math.Max(-1, Math.Min(1, a))) * 6371.0;
        }

        // short-path heading, 0 - 359
        public static int Bearing(double lat1, double lon1, double lat2, double lon2)
        {
            double y = Math.Sin((lon2 - lon1) * R) * Math.Cos(lat2 * R);
            double x = Math.Cos(lat1 * R) * Math.Sin(lat2 * R) - Math.Sin(lat1 * R) * Math.Cos(lat2 * R) * Math.Cos((lon2 - lon1) * R);
            return ((int)Math.Round(Math.Atan2(y, x) / R) + 360) % 360;
        }
    }
}
