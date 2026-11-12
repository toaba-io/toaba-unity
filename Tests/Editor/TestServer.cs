using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Toaba.Tests
{
    /// <summary>An HTTP server on localhost that answers with queued responses.</summary>
    internal sealed class TestServer : IDisposable
    {
        public sealed class Request
        {
            public string Method;
            public string Path;
            public string Query;
            public Dictionary<string, string> Headers;
            public string Body;
        }

        readonly HttpListener listener = new HttpListener();
        readonly Queue<(int Status, string Body, (string, string)[] Headers)> answers =
            new Queue<(int, string, (string, string)[])>();
        readonly List<Request> requests = new List<Request>();

        public string Url { get; }

        public TestServer()
        {
            Url = $"http://127.0.0.1:{FreePort()}";
            listener.Prefixes.Add(Url + "/");
            listener.Start();
            new Thread(Serve) { IsBackground = true }.Start();
        }

        public void Answer(int status, string body, params (string Name, string Value)[] headers)
        {
            lock (answers)
                answers.Enqueue((status, body, headers));
        }

        public IReadOnlyList<Request> Requests
        {
            get
            {
                lock (requests)
                    return requests.ToList();
            }
        }

        public void Dispose() => listener.Close();

        void Serve()
        {
            while (true)
            {
                HttpListenerContext context;
                try
                {
                    context = listener.GetContext();
                }
                catch (Exception error) when (error is HttpListenerException || error is ObjectDisposedException)
                {
                    return;
                }

                var request = context.Request;
                using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
                    lock (requests)
                        requests.Add(new Request
                        {
                            Method = request.HttpMethod,
                            Path = request.Url.AbsolutePath,
                            Query = request.Url.Query,
                            Headers = request.Headers.AllKeys.ToDictionary(
                                name => name, name => request.Headers[name], StringComparer.OrdinalIgnoreCase),
                            Body = reader.ReadToEnd(),
                        });

                (int Status, string Body, (string Name, string Value)[] Headers) answer;
                lock (answers)
                    answer = answers.Dequeue();
                var response = context.Response;
                response.StatusCode = answer.Status;
                foreach (var (name, value) in answer.Headers)
                    response.Headers[name] = value;
                var bytes = Encoding.UTF8.GetBytes(answer.Body);
                response.ContentLength64 = bytes.Length;
                response.OutputStream.Write(bytes, 0, bytes.Length);
                response.Close();
            }
        }

        static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }
    }
}
