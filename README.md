# Toaba for Unity

The Unity SDK for [Toaba](https://toaba.io), the PaaS for live games. It requires Unity 6 or
later. The SDK currently handles player sign-in and server time; content delivery is planned.

## Install

In Unity, open **Window → Package Manager**, select **+ → Install package from git URL**, and enter:

```text
https://github.com/toaba-io/toaba-unity.git
```

Then open **Tools → Toaba → Settings**, select **Set up Toaba**, and enter your app's ID and
publishable key from the [dashboard](https://app.toaba.io).

## Sign players in

```csharp
await Toaba.Players.ReadyAsync();
if (!Toaba.Players.IsSignedIn)
    await Toaba.Players.SignInAnonymouslyAsync();
Debug.Log(Toaba.Players.PlayerId);
```

The SDK saves the session on the device and refreshes it when needed. The
[Unity guide](https://docs.toaba.io/docs/engines/unity) covers email codes, Google and Apple, and
keeping a guest's progress.

> [!WARNING]
> Guest accounts are stored on a single device and, by default, are deleted after 30 days of
> inactivity. Anyone with your publishable key can create them. See
> [anonymous players](https://docs.toaba.io/docs/players/anonymous).

## Server time

`Toaba.Clock` gives Toaba's time, so players can't move daily resets, event windows, or
countdowns by changing the device clock. It syncs on first use, whenever the game resumes, and
every 5 minutes while the game is in the foreground. Until it's synced, it retries sooner: after
2, 5, 15, 30, 60, and 120 seconds, then every 5 minutes.

```csharp
await Toaba.Clock.SyncAsync();
var reset = Toaba.Clock.NextReset(TimeSpan.Zero, TimeSpan.Zero); // midnight UTC
Debug.Log($"Next reset in {Toaba.Clock.Remaining(reset)}");
if (Toaba.Clock.IsOpen(eventStart, eventEnd))
    ShowEvent();
```

## Time and cheating

Players move the device clock forward to skip timers and claim daily rewards again. Use
`Toaba.Clock`, not `DateTime.Now`, for anything time-gated:

- Gate rewards, timers, and event access on `TryGetUtcNow`. It returns only Toaba's time. It
  returns false before the first sync, and after a resume that found the device clock moved, until
  the next sync lands. `UtcNow` falls back to the device clock and is for display. `NextReset`,
  `IsOpen`, and `Remaining` throw until the clock is synced, so check `IsSynced` first.
- `DeviceSkew` is how far the device clock was ahead of Toaba's at the last sync.
  `DeviceClockChanged` is raised when, on resume or at a sync, the device clock has moved by more
  than 5 seconds against real time. Both are signals; the game decides what to do about them.

```csharp
if (Toaba.Clock.TryGetUtcNow(out var now) && now >= nextDailyReward)
    ClaimDailyReward();
```

A check in the game raises the bar, but a modified game can skip it. Check anything of real value
on a server too.

## Maintenance

Turn on maintenance for the app in the dashboard, and the game learns it with the clock's next
sync. It's advice only: Toaba keeps answering sign-in and content requests, so the game decides
what to show.

```csharp
Toaba.Status.MaintenanceChanged += inMaintenance => maintenanceBanner.SetActive(inMaintenance);
```

`Toaba.Status.InMaintenance` is false until the first sync.

## Development

`TestProject~` is a small Unity project that loads the SDK from this folder. To run the tests
without opening the Editor:

```sh
Unity -batchmode -nographics -projectPath TestProject~ -runTests -testPlatform EditMode \
  -testResults TestProject~/Logs/results.xml -logFile -
```

Or open `TestProject~` in Unity and use **Window → General → Test Runner**.

## License

Apache-2.0, see [LICENSE.md](LICENSE.md). The license covers this SDK only; it grants no right to
use the Toaba name or logo.
