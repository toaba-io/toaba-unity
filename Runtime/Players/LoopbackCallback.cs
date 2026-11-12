using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Toaba.Http;

namespace Toaba
{
    // Receives the browser's return at http://127.0.0.1:<port>/callback, as RFC 8252 recommends for
    // desktop apps: there's no URL scheme to register, and only this machine can reach it.
    internal sealed class LoopbackCallback : IDisposable
    {
        static readonly byte[] Page = Encoding.UTF8.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nConnection: close\r\n\r\n" +
            "<!doctype html><meta charset=utf-8><title>Toaba</title><p>You can close this tab and return to the game.");

        // Browsers also open connections that send nothing, which mustn't hold up the real one.
        static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

        readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);

        public string Url { get; }

        public LoopbackCallback()
        {
            listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/callback";
        }

        /// <summary>The callback's query parameters, once the browser arrives.</summary>
        public async Task<NameValueCollection> WaitAsync(CancellationToken cancellation)
        {
            using (cancellation.Register(listener.Stop))
            {
                while (true)
                {
                    TcpClient client;
                    try
                    {
                        client = await listener.AcceptTcpClientAsync();
                    }
                    catch (Exception) when (cancellation.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellation);
                    }
                    using (client)
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                    using (timeout.Token.Register(client.Close))
                    {
                        timeout.CancelAfter(RequestTimeout);
                        string target;
                        try
                        {
                            target = await ReadTargetAsync(client.GetStream());
                        }
                        catch (Exception) when (timeout.IsCancellationRequested)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            continue;
                        }
                        if (target?.Split('?')[0] != "/callback")
                            continue;
                        await client.GetStream().WriteAsync(Page, 0, Page.Length);
                        return ApiClient.ParseQuery(target);
                    }
                }
            }
        }

        public void Dispose() => listener.Stop();

        // The target of "GET /callback?code=… HTTP/1.1". It reads the whole head: closing with
        // unread data can reset the connection before the browser shows the page.
        static async Task<string> ReadTargetAsync(NetworkStream stream)
        {
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            var parts = (await reader.ReadLineAsync())?.Split(' ');
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync()))
            {
            }
            return parts?.Length == 3 && parts[0] == "GET" ? parts[1] : null;
        }
    }
}
