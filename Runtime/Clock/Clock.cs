using System;
using System.Threading;
using UnityEngine;

namespace Toaba
{
    /// <summary>Toaba's time, for the app set in Project Settings → Toaba.</summary>
    public static class Clock
    {
        static ClockService Service => ToabaClient.Shared.Clock;

        /// <inheritdoc cref="ClockService.Synced"/>
        public static event Action Synced
        {
            add => Service.Synced += value;
            remove => Service.Synced -= value;
        }

        /// <inheritdoc cref="ClockService.DeviceClockChanged"/>
        public static event Action<TimeSpan> DeviceClockChanged
        {
            add => Service.DeviceClockChanged += value;
            remove => Service.DeviceClockChanged -= value;
        }

        /// <inheritdoc cref="ClockService.UtcNow"/>
        public static DateTimeOffset UtcNow => Service.UtcNow;

        /// <inheritdoc cref="ClockService.TryGetUtcNow"/>
        public static bool TryGetUtcNow(out DateTimeOffset now) => Service.TryGetUtcNow(out now);

        /// <inheritdoc cref="ClockService.IsSynced"/>
        public static bool IsSynced => Service.IsSynced;

        /// <inheritdoc cref="ClockService.DeviceSkew"/>
        public static TimeSpan? DeviceSkew => Service.DeviceSkew;

        /// <inheritdoc cref="ClockService.SyncAsync"/>
        public static Awaitable SyncAsync(CancellationToken cancellation = default) => Service.SyncAsync(cancellation);

        /// <inheritdoc cref="ClockService.NextReset(TimeSpan, TimeSpan)"/>
        public static DateTimeOffset NextReset(TimeSpan timeOfDay, TimeSpan utcOffset) =>
            Service.NextReset(timeOfDay, utcOffset);

        /// <inheritdoc cref="ClockService.NextReset(DayOfWeek, TimeSpan, TimeSpan)"/>
        public static DateTimeOffset NextReset(DayOfWeek day, TimeSpan timeOfDay, TimeSpan utcOffset) =>
            Service.NextReset(day, timeOfDay, utcOffset);

        /// <inheritdoc cref="ClockService.IsOpen"/>
        public static bool IsOpen(DateTimeOffset start, DateTimeOffset end) => Service.IsOpen(start, end);

        /// <inheritdoc cref="ClockService.Remaining"/>
        public static TimeSpan Remaining(DateTimeOffset end) => Service.Remaining(end);
    }
}
