# Changelog

## 0.1.0

- Project Settings → Toaba (also **Tools → Toaba → Settings**) for the app ID and publishable key.
- `Toaba.Players`: sign in as a guest, with an email code, with a Google or Apple ID token, or in
  the browser on desktop and WebGL; add an email, Google or Apple to the signed-in player.
- The session is restored at startup, refreshed before it expires, and saved per profile:
  encrypted with DPAPI on Windows, in the Keychain on macOS, in an owner-only file on Linux, and in
  localStorage on WebGL.
- Read and update the player, delete the account, and sign out on this device or everywhere.
- `Toaba.Clock`: Toaba's time for daily and weekly resets, event windows and countdowns, synced on
  first use, when the game resumes, and every 5 minutes in the foreground. `TryGetUtcNow` never
  reads the device clock; `DeviceSkew` gives the device clock's offset, and `DeviceClockChanged`
  reports when it moves.
- `Toaba.Status`: whether the app is in maintenance, read with the clock's sync.
- Players requests describe the game: platform, OS, device, manufacturer, build, SDK version,
  locale, and an install ID.
