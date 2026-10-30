/*  FakeServices.cs (self-test)

Stand-ins for the upload services, so the self-test never touches a real log: a small web server on this PC
(127.0.0.1) answering as eQSL, QRZ's logbook API, Club Log and LoTW's report do, recording every request; and a
stand-in TQSL (this test program, run with KAINOS_FAKE_TQSL_OUT set) that records its command line and file and
exits with the code the test chose.

This file is part of the Kainos Logbook module. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Kainos.Logbook.SelfTest
{
    internal sealed class FakeRequest
    {
        public string Method, Path, Query, Body;
        public Dictionary<string, string> Form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Arg(string k)
        {
            string v;
            if (Form.TryGetValue(k, out v)) return v;
            foreach (string part in (Query ?? "").Split('&'))
            {
                int eq = part.IndexOf('=');
                if (eq > 0 && part.Substring(0, eq).Equals(k, StringComparison.OrdinalIgnoreCase)) return WebUtility.UrlDecode(part.Substring(eq + 1));
            }
            return "";
        }
    }

    internal sealed class FakeServer : IDisposable
    {
        private readonly TcpListener _l;
        private readonly Thread _t;
        private volatile bool _stop;
        public readonly List<FakeRequest> Requests = new List<FakeRequest>();
        public Func<FakeRequest, Tuple<int, string>> Handle = r => Tuple.Create(404, "not here");
        public string Base { get; private set; }

        public FakeServer()
        {
            _l = new TcpListener(IPAddress.Loopback, 0);
            _l.Start();
            Base = "http://127.0.0.1:" + ((IPEndPoint)_l.LocalEndpoint).Port;
            _t = new Thread(loop) { IsBackground = true };
            _t.Start();
        }

        public int Count(string path) { lock (Requests) return Requests.FindAll(r => r.Path == path).Count; }
        public FakeRequest Last(string path) { lock (Requests) return Requests.FindLast(r => r.Path == path); }

        private void loop()
        {
            while (!_stop)
            {
                TcpClient c;
                try { c = _l.AcceptTcpClient(); } catch { return; }
                try { serve(c); } catch { }
                finally { c.Close(); }
            }
        }

        private void serve(TcpClient c)
        {
            NetworkStream s = c.GetStream();
            byte[] buf = new byte[1 << 20];
            int got = 0, headEnd = -1;
            while (headEnd < 0)
            {
                int n = s.Read(buf, got, buf.Length - got);
                if (n <= 0) return;
                got += n;
                headEnd = Encoding.ASCII.GetString(buf, 0, got).IndexOf("\r\n\r\n", StringComparison.Ordinal);
            }
            string head = Encoding.ASCII.GetString(buf, 0, headEnd);
            string[] lines = head.Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] first = lines[0].Split(' ');
            int len = 0;
            foreach (string l in lines)
                if (l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) int.TryParse(l.Substring(15).Trim(), out len);
            while (got - headEnd - 4 < len) { int n = s.Read(buf, got, buf.Length - got); if (n <= 0) break; got += n; }
            string body = Encoding.UTF8.GetString(buf, headEnd + 4, Math.Max(0, got - headEnd - 4));
            string target = first[1];
            int qm = target.IndexOf('?');
            FakeRequest r = new FakeRequest { Method = first[0], Path = qm >= 0 ? target.Substring(0, qm) : target, Query = qm >= 0 ? target.Substring(qm + 1) : "", Body = body };
            foreach (string part in body.Split('&'))
            {
                int eq = part.IndexOf('=');
                if (eq > 0) r.Form[WebUtility.UrlDecode(part.Substring(0, eq))] = WebUtility.UrlDecode(part.Substring(eq + 1));
            }
            lock (Requests) Requests.Add(r);
            Tuple<int, string> resp = Handle(r);
            byte[] rb = Encoding.UTF8.GetBytes(resp.Item2);
            string h = "HTTP/1.1 " + resp.Item1 + " X\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: " + rb.Length + "\r\nConnection: close\r\n\r\n";
            byte[] hb = Encoding.ASCII.GetBytes(h);
            s.Write(hb, 0, hb.Length);
            s.Write(rb, 0, rb.Length);
        }

        public void Dispose() { _stop = true; _l.Stop(); }
    }

    internal static class FakeTqsl
    {
        // when the test program is started as TQSL: record and exit
        public static bool RunIfAsked(string[] args)
        {
            string outFile = Environment.GetEnvironmentVariable("KAINOS_FAKE_TQSL_OUT");
            if (string.IsNullOrEmpty(outFile)) return false;
            string adi = args.Length > 0 && File.Exists(args[args.Length - 1]) ? File.ReadAllText(args[args.Length - 1]) : "";
            File.WriteAllText(outFile, string.Join(" ", args) + "\n----\n" + adi);
            int code;
            int.TryParse(Environment.GetEnvironmentVariable("KAINOS_FAKE_TQSL_EXIT"), out code);
            Environment.Exit(code);
            return true;
        }
    }
}
