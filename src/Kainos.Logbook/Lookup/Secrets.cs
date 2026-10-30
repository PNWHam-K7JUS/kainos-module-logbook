/*  Secrets.cs

Passwords and API keys, kept in Kainos's settings encrypted with Windows' DPAPI for this Windows user only: the
settings file on its own (a backup, a copy sent with a bug report) doesn't give them away.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Security.Cryptography;
using System.Text;
using Kainos.Modules;

namespace Kainos.Logbook.Lookup
{
    internal static class Secrets
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Kainos.Logbook");

        public static string Get(IKainosHost host, string key)
        {
            string v = host.GetSetting("secret." + key, "");
            if (v.Length == 0) return "";
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(v), Entropy, DataProtectionScope.CurrentUser)); }
            catch { return ""; }
        }

        public static void Set(IKainosHost host, string key, string value)
        {
            if (string.IsNullOrEmpty(value)) { host.SetSetting("secret." + key, ""); return; }
            byte[] b = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser);
            host.SetSetting("secret." + key, Convert.ToBase64String(b));
        }
    }
}
