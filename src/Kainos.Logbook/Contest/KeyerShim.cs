/*  KeyerShim.cs

IKainosKeyer is in Kainos 1.0.15's module API. On an older Kainos the type doesn't exist, and a method that names it
can't even be compiled (TypeLoadException), so only these methods name it: each is compiled when it's first called,
and Has() turns the missing type into "no keyer".

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Runtime.CompilerServices;
using Kainos.Modules;

namespace Kainos.Logbook.Contest
{
    internal static class KeyerShim
    {
        private static int _has = -1;      // -1 not known yet

        public static bool Has(IKainosHost h)
        {
            if (_has < 0)
            {
                try { _has = isKeyer(h) ? 1 : 0; }
                catch (TypeLoadException) { _has = 0; }
                catch (MissingMethodException) { _has = 0; }
            }
            return _has == 1;
        }

        internal static void Forget() { _has = -1; }       // the self-test's hosts differ

        [MethodImpl(MethodImplOptions.NoInlining)] private static bool isKeyer(IKainosHost h) { return h is IKainosKeyer; }
        [MethodImpl(MethodImplOptions.NoInlining)] public static string SendCw(IKainosHost h, string text) { return ((IKainosKeyer)h).SendCw(text); }
        [MethodImpl(MethodImplOptions.NoInlining)] public static bool CwSending(IKainosHost h) { return ((IKainosKeyer)h).CwSending; }
        [MethodImpl(MethodImplOptions.NoInlining)] public static void StopCw(IKainosHost h) { ((IKainosKeyer)h).StopCw(); }
        [MethodImpl(MethodImplOptions.NoInlining)] public static int Publish(IKainosHost h, string topic, string message) { return ((IKainosKeyer)h).Publish(topic, message); }
        [MethodImpl(MethodImplOptions.NoInlining)] public static void Subscribe(IKainosHost h, string topic, Action<string> handler) { ((IKainosKeyer)h).Subscribe(topic, handler); }
    }
}
