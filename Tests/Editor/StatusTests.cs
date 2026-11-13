using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Toaba.Http;

namespace Toaba.Tests
{
    public class StatusTests
    {
        // A Friday, half an hour before midnight UTC.
        static readonly DateTimeOffset ServerTime = new DateTimeOffset(2026, 11, 13, 23, 30, 0, TimeSpan.Zero);

        TestServer server;
        double seconds;
        bool slow;
        DateTimeOffset device;
        StatusService status;
        ClockService clock;

        [SetUp]
        public void SetUp()
        {
            server = new TestServer();
            seconds = 1000;
            slow = false;
            device = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);
            // While slow, each read of real time moves it 4 s, so a request takes 4 s.
            var api = new ApiClient("pk_test", server.Url, realtime: () => slow ? seconds += 4 : seconds);
            status = new StatusService("app_1", api, () => device);
            clock = status.Clock;
        }

        [TearDown]
        public void TearDown() => server.Dispose();

        // maintenance: null leaves the field out.
        void Answer(DateTimeOffset time, bool? maintenance = false) =>
            server.Answer(200, Json.Serialize(new
            {
                Data = new
                {
                    ServerTime = time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                    Maintenance = maintenance,
                },
            }));

        void Fail() => server.Answer(503, "{\"error\": \"unavailable\"}");

        async Task SyncAt(DateTimeOffset time)
        {
            Answer(time);
            await clock.SyncAsync();
        }

        [Test]
        public void Unsynced_only_the_display_time_reads_the_device_clock()
        {
            Assert.That(clock.UtcNow, Is.EqualTo(device));
            Assert.That(clock.TryGetUtcNow(out _), Is.False);
            Assert.That(clock.DeviceSkew, Is.Null);
            Assert.Throws<InvalidOperationException>(() => clock.NextReset(TimeSpan.Zero, TimeSpan.Zero));
            Assert.Throws<InvalidOperationException>(() => clock.NextReset(DayOfWeek.Monday, TimeSpan.Zero, TimeSpan.Zero));
            Assert.Throws<InvalidOperationException>(() => clock.IsOpen(ServerTime, ServerTime.AddHours(1)));
            Assert.Throws<InvalidOperationException>(() => clock.Remaining(ServerTime));
        }

        [Test]
        public async Task Once_synced_the_clock_follows_Toaba_and_ignores_the_device_clock()
        {
            await SyncAt(ServerTime);
            seconds += 90;
            device = device.AddYears(10);

            Assert.That(clock.TryGetUtcNow(out var now), Is.True);
            Assert.That(now, Is.EqualTo(ServerTime.AddSeconds(90)));
            Assert.That(clock.UtcNow, Is.EqualTo(now));
        }

        [Test]
        public async Task Device_clock_jumps_over_5_seconds_are_reported_once()
        {
            var jumps = new List<TimeSpan>();
            clock.DeviceClockChanged += jumps.Add;
            await SyncAt(ServerTime);

            seconds += 60;
            device = device.AddSeconds(60).AddHours(1);
            await SyncAt(ServerTime.AddSeconds(60));
            await SyncAt(ServerTime.AddSeconds(60));
            device = device.AddHours(-2);
            await SyncAt(ServerTime.AddSeconds(60));
            device = device.AddSeconds(4);
            await SyncAt(ServerTime.AddSeconds(60));

            Assert.That(jumps, Is.EqualTo(new[] { TimeSpan.FromHours(1), TimeSpan.FromHours(-2) }));
            Assert.That(clock.TryGetUtcNow(out var now), Is.True);
            Assert.That(now, Is.EqualTo(ServerTime.AddSeconds(60)));
        }

        [Test]
        public async Task Device_skew_is_how_far_the_device_is_ahead()
        {
            device = ServerTime.AddMinutes(10);
            await SyncAt(ServerTime);
            Assert.That(clock.DeviceSkew, Is.EqualTo(TimeSpan.FromMinutes(10)));

            device = ServerTime.AddMinutes(-3);
            await SyncAt(ServerTime);
            Assert.That(clock.DeviceSkew, Is.EqualTo(TimeSpan.FromMinutes(-3)));
        }

        [Test]
        public async Task Each_sync_replaces_the_last()
        {
            await SyncAt(ServerTime);
            seconds += 10;
            await SyncAt(ServerTime.AddHours(1));
            seconds += 5;

            Assert.That(clock.UtcNow, Is.EqualTo(ServerTime.AddHours(1).AddSeconds(5)));
        }

        [Test]
        public async Task A_focus_blip_keeps_the_clock_synced()
        {
            await SyncAt(ServerTime);
            Answer(ServerTime);

            status.Resume();
            Assert.That(clock.IsSynced, Is.True);
            Assert.That(status.Syncing, Is.True);
            await clock.SyncAsync();
        }

        [Test]
        public async Task A_resume_after_the_device_clock_moved_unsyncs_until_a_sync_succeeds()
        {
            await SyncAt(ServerTime);
            var jumps = new List<TimeSpan>();
            clock.DeviceClockChanged += jumps.Add;
            device = device.AddMinutes(10);
            Answer(ServerTime.AddMinutes(1));

            status.Resume();
            Assert.That(jumps, Is.EqualTo(new[] { TimeSpan.FromMinutes(10) }));
            Assert.That(clock.TryGetUtcNow(out _), Is.False);

            await clock.SyncAsync();
            Assert.That(clock.TryGetUtcNow(out var now), Is.True);
            Assert.That(now, Is.EqualTo(ServerTime.AddMinutes(1)));
        }

        [Test]
        public async Task Maintenance_follows_each_sync_and_is_raised_on_change()
        {
            var changes = new List<bool>();
            status.MaintenanceChanged += changes.Add;

            foreach (var maintenance in new bool?[] { false, true, true })
            {
                Answer(ServerTime, maintenance);
                await clock.SyncAsync();
            }
            Answer(ServerTime, maintenance: null);
            await clock.SyncAsync();

            Assert.That(status.InMaintenance, Is.False);
            Assert.That(changes, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public async Task A_failed_sync_keeps_the_time_and_maintenance()
        {
            Answer(ServerTime, maintenance: true);
            await clock.SyncAsync();
            seconds += 10;

            Fail();
            await Expect.Throws<ToabaException>(async () => await clock.SyncAsync());

            Assert.That(status.InMaintenance, Is.True);
            Assert.That(clock.UtcNow, Is.EqualTo(ServerTime.AddSeconds(10)));
        }

        [Test]
        public async Task A_slow_answer_updates_maintenance_but_not_the_clock()
        {
            await SyncAt(ServerTime);
            slow = true;
            Answer(ServerTime.AddHours(1), maintenance: true);
            await clock.SyncAsync();
            slow = false;

            Assert.That(status.InMaintenance, Is.True);
            Assert.That(clock.UtcNow, Is.EqualTo(ServerTime.AddSeconds(seconds - 1000)));
        }

        [Test]
        public async Task It_refreshes_every_5_minutes_in_the_foreground_only()
        {
            await SyncAt(ServerTime);
            seconds += 299;
            status.Poll(foreground: true);
            seconds += 1;
            status.Poll(foreground: false);
            Assert.That(status.Syncing, Is.False);

            Answer(ServerTime.AddMinutes(5));
            status.Poll(foreground: true);
            Assert.That(status.Syncing, Is.True);
            await clock.SyncAsync();
        }

        [Test]
        public async Task Until_synced_failed_syncs_are_retried_with_backoff_up_to_5_minutes()
        {
            Fail();
            await Expect.Throws<ToabaException>(async () => await clock.SyncAsync());

            foreach (var wait in new[] { 2, 5, 15, 30, 60, 120, 300, 300 })
            {
                seconds += wait - 0.5;
                status.Poll(foreground: true);
                Assert.That(status.Syncing, Is.False, $"before {wait} s");

                seconds += 0.5;
                Fail();
                status.Poll(foreground: true);
                Assert.That(status.Syncing, Is.True, $"after {wait} s");
                await Expect.Throws<ToabaException>(async () => await clock.SyncAsync());
            }
        }

        [Test]
        public async Task Resets_fall_in_the_games_offset_across_midnight_and_weeks()
        {
            await SyncAt(ServerTime);
            var plusTwo = TimeSpan.FromHours(2); // Saturday 01:30 there
            var minusFive = TimeSpan.FromHours(-5); // Friday 18:30 there

            Assert.That(clock.NextReset(TimeSpan.Zero, plusTwo), Is.EqualTo(At(2026, 11, 15, 0, plusTwo)));
            Assert.That(clock.NextReset(TimeSpan.FromHours(4), plusTwo), Is.EqualTo(At(2026, 11, 14, 4, plusTwo)));
            Assert.That(clock.NextReset(new TimeSpan(18, 30, 0), minusFive),
                Is.EqualTo(At(2026, 11, 14, 18, minusFive).AddMinutes(30)));

            Assert.That(clock.NextReset(DayOfWeek.Monday, TimeSpan.Zero, plusTwo), Is.EqualTo(At(2026, 11, 16, 0, plusTwo)));
            Assert.That(clock.NextReset(DayOfWeek.Saturday, TimeSpan.FromHours(3), plusTwo),
                Is.EqualTo(At(2026, 11, 14, 3, plusTwo)));
            Assert.That(clock.NextReset(DayOfWeek.Saturday, TimeSpan.Zero, plusTwo), Is.EqualTo(At(2026, 11, 21, 0, plusTwo)));
        }

        [Test]
        public async Task Windows_open_at_the_start_and_close_at_the_end()
        {
            await SyncAt(ServerTime);

            Assert.That(clock.IsOpen(ServerTime, ServerTime.AddHours(1)), Is.True);
            Assert.That(clock.IsOpen(ServerTime.AddHours(-1), ServerTime), Is.False);
            Assert.That(clock.Remaining(ServerTime.AddMinutes(5)), Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(clock.Remaining(ServerTime.AddMinutes(-5)), Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public async Task The_status_request_carries_no_client_context()
        {
            Answer(ServerTime);
            await new ToabaClient("app_1", new ApiClient("pk_test", server.Url), new MemoryStore()).Clock.SyncAsync();

            var request = server.Requests.Single();
            Assert.That(request.Path, Is.EqualTo("/v1/apps/app_1/status"));
            Assert.That(request.Headers.Keys, Has.None.StartsWith("x-toaba-"));
        }

        static DateTimeOffset At(int year, int month, int day, int hour, TimeSpan offset) =>
            new DateTimeOffset(year, month, day, hour, 0, 0, offset);
    }
}
