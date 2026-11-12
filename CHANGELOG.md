# Changelog

## Unreleased

- Project Settings → Toaba (also **Tools → Toaba → Settings**) for the app ID and publishable key.
- `Toaba.Players`: sign in as a guest, with an email code, with a Google or Apple ID token, or in
  the browser on desktop and WebGL; add an email, Google or Apple to the signed-in player.
- The session is restored at startup, refreshed before it expires, and saved per profile in the
  Windows DPAPI, the macOS Keychain, an owner-only file on Linux, or localStorage on WebGL.
- Read and update the player, delete the account, and sign out on this device or everywhere.
