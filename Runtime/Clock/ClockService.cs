using System;
using System.Threading;
using Toaba.Http;
using UnityEngine;

namespace Toaba
{
    /// <summary>Toaba's time, which players can't move by changing the device clock.</summary>
    public sealed class ClockService
    {
        const double MaxRoundTripSeconds = 3;
        static readonly TimeSpan JumpThreshold = TimeSpan.FromSeconds(5);

        readonly StatusService status;
        readonly ApiClient api;
        readonly Func<DateTimeOffset> device;
        DateTimeOffset? anchor;
        double anchorSeconds;
        DateTimeOffset deviceMark;
        double realtimeMark;

        public event Action Synced;

        /// <summary>
        /// Raised on resume or at a sync when the device clock moved by more than 5 seconds
        /// against real time since the last check, with how far: positive when moved forward. On
        /// consoles, where real time may not count a suspension, a resume can read as a jump forward.
        /// </summary>
        public event Action<TimeSpan> DeviceClockChanged;

        /// <summary>
        /// False before the first sync, and from a resume that found the device clock moved until
        /// the next sync lands.
        /// </summary>
        public bool IsSynced { get; private set; }

        /// <summary>How far the device clock was ahead of Toaba's at the last sync; null before it.</summary>
        public TimeSpan? DeviceSkew { get; private set; }

        /// <summary>
        /// For display: Toaba's time, or the device clock before the first sync. For anything a
        /// player gains from, use <see cref="TryGetUtcNow"/>.
        /// </summary>
        public DateTimeOffset UtcNow => ServerNow ?? device().ToUniversalTime();

        DateTimeOffset? ServerNow => anchor?.AddSeconds(api.Realtime - anchorSeconds);

        DateTimeOffset SyncedNow => TryGetUtcNow(out var now)
            ? now
            : throw new InvalidOperationException("The clock isn't synced: check IsSynced first.");

        internal ClockService(StatusService status, ApiClient api, Func<DateTimeOffset> device = null)
        {
            this.status = status;
            this.api = api;
            this.device = device ?? (() => DateTimeOffset.UtcNow);
            deviceMark = this.device();
            realtimeMark = api.Realtime;
        }

        /// <summary>Toaba's time, never the device clock's. False until synced.</summary>
        public bool TryGetUtcNow(out DateTimeOffset now)
        {
            now = default;
            if (!IsSynced)
                return false;
            now = ServerNow.Value;
            return true;
        }

        /// <summary>
        /// Syncs the clock and <see cref="StatusService.InMaintenance"/>, joining the sync in flight,
        /// if any. Cancelling stops waiting, not the sync.
        /// </summary>
        public Awaitable SyncAsync(CancellationToken cancellation = default) => status.SyncAsync(cancellation);

        /// <summary>The next daily reset at <paramref name="timeOfDay"/> in <paramref name="utcOffset"/>. Throws until synced.</summary>
        public DateTimeOffset NextReset(TimeSpan timeOfDay, TimeSpan utcOffset) => Next(null, timeOfDay, utcOffset);

        /// <summary>The next weekly reset on <paramref name="day"/> at <paramref name="timeOfDay"/> in <paramref name="utcOffset"/>. Throws until synced.</summary>
        public DateTimeOffset NextReset(DayOfWeek day, TimeSpan timeOfDay, TimeSpan utcOffset) =>
            Next(day, timeOfDay, utcOffset);

        /// <summary>Whether now is from <paramref name="start"/> up to, not including, <paramref name="end"/>. Throws until synced.</summary>
        public bool IsOpen(DateTimeOffset start, DateTimeOffset end)
        {
            var now = SyncedNow;
            return start <= now && now < end;
        }

        /// <summary>The time left until <paramref name="end"/>, or zero once it's passed. Throws until synced.</summary>
        public TimeSpan Remaining(DateTimeOffset end)
        {
            var left = end - SyncedNow;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }

        // sent and received are in ApiClient.Realtime seconds. A slow answer could put the clock off
        // by up to half its round trip, so it's skipped.
        internal void Set(DateTimeOffset serverTime, double sent, double received)
        {
            if (received - sent > MaxRoundTripSeconds)
                return;
            var at = (sent + received) / 2;
            anchor = serverTime.ToUniversalTime();
            anchorSeconds = at;
            IsSynced = true;
            var deviceNow = device();
            var realtime = api.Realtime;
            DeviceSkew = deviceNow - anchor.Value.AddSeconds(realtime - at);
            CheckDeviceClock(deviceNow, realtime);
            Events.Raise(Synced);
        }

        // A moved device clock may hide time real time didn't count, so wait for a sync.
        internal void Resume()
        {
            if (CheckDeviceClock(device(), api.Realtime))
                IsSynced = false;
        }

        bool CheckDeviceClock(DateTimeOffset deviceNow, double realtime)
        {
            var jump = deviceNow - deviceMark - TimeSpan.FromSeconds(realtime - realtimeMark);
            deviceMark = deviceNow;
            realtimeMark = realtime;
            if (jump.Duration() <= JumpThreshold)
                return false;
            Events.Raise(DeviceClockChanged, jump);
            return true;
        }

        DateTimeOffset Next(DayOfWeek? day, TimeSpan timeOfDay, TimeSpan utcOffset)
        {
            if (timeOfDay < TimeSpan.Zero || timeOfDay >= TimeSpan.FromDays(1))
                throw new ArgumentOutOfRangeException(nameof(timeOfDay), "Must be from 00:00 up to 24:00.");
            var now = SyncedNow.ToOffset(utcOffset);
            var date = day is { } weekday ? now.Date.AddDays(((int)weekday - (int)now.DayOfWeek + 7) % 7) : now.Date;
            var reset = new DateTimeOffset(date + timeOfDay, utcOffset);
            return reset > now ? reset : reset.AddDays(day == null ? 1 : 7);
        }
    }
}
