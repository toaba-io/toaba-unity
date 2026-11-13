using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Toaba.Http;
using UnityEngine;
using UnityEngine.Scripting;

namespace Toaba
{
    /// <summary>The app's status, read in the same request as the <see cref="ClockService"/>'s time.</summary>
    public sealed class StatusService
    {
        const double RefreshSeconds = 300;
        static readonly double[] RetrySeconds = { 2, 5, 15, 30, 60, 120, RefreshSeconds };

        readonly string path;
        readonly ApiClient api;
        double due;
        int misses; // syncs in a row that left the clock unsynced
        Task syncing;
        int syncs;

        /// <summary>Turned on in the dashboard; advice only. False until the first sync.</summary>
        public bool InMaintenance { get; private set; }

        /// <summary>Raised when a sync sees <see cref="InMaintenance"/> change.</summary>
        public event Action<bool> MaintenanceChanged;

        internal ClockService Clock { get; }

        internal bool Syncing => syncing is { IsCompleted: false };

        internal StatusService(string appId, ApiClient api, Func<DateTimeOffset> device = null)
        {
            path = $"/v1/apps/{Uri.EscapeDataString(appId)}/status";
            this.api = api;
            Clock = new ClockService(this, api, device);
            if (Application.isPlaying)
            {
                var app = AppLifecycle.Create();
                app.Resumed += Resume;
                app.Updated += () => Poll(app.InForeground);
                SyncInBackground();
            }
        }

        internal Awaitable SyncAsync(CancellationToken cancellation)
        {
            if (!Syncing)
                syncing = SyncOnceAsync();
            return WaitAsync(syncing, cancellation);
        }

        // A sync in flight may have measured the suspension, and on consoles real time may not count
        // it, so start a new one.
        internal void Resume()
        {
            Clock.Resume();
            syncing = null;
            SyncInBackground();
        }

        internal void Poll(bool foreground)
        {
            if (foreground && !Syncing && api.Realtime >= due)
                SyncInBackground();
        }

        async Task SyncOnceAsync()
        {
            var sync = ++syncs;
            try
            {
                var response = await api.SendAsync("GET", path);
                if (sync != syncs)
                    return;
                var status = response.Data<Response>();
                var changed = status.Maintenance != InMaintenance;
                InMaintenance = status.Maintenance;
                Clock.Set(status.ServerTime, response.Sent, response.Received);
                if (changed)
                    Events.Raise(MaintenanceChanged, InMaintenance);
            }
            finally
            {
                if (sync == syncs)
                {
                    misses = Clock.IsSynced ? 0 : misses + 1;
                    due = api.Realtime + (misses == 0 ? RefreshSeconds : RetrySeconds[Math.Min(misses, RetrySeconds.Length) - 1]);
                }
            }
        }

        async void SyncInBackground()
        {
            try
            {
                await SyncAsync(CancellationToken.None);
            }
            catch (ToabaException)
            {
            }
        }

        static async Awaitable WaitAsync(Task task, CancellationToken cancellation)
        {
            if (cancellation.CanBeCanceled)
            {
                var cancelled = new TaskCompletionSource<bool>();
                using (cancellation.Register(() => cancelled.TrySetCanceled()))
                    await Task.WhenAny(task, cancelled.Task);
                cancellation.ThrowIfCancellationRequested();
            }
            await task;
        }

        [Preserve]
        sealed class Response
        {
            [JsonProperty(Required = Required.Always)]
            public DateTimeOffset ServerTime;

            public bool Maintenance;
        }
    }
}
