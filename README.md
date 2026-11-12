# Toaba for Unity

The Unity SDK for [Toaba](https://toaba.io), the PaaS for live games, for Unity 6 and later. It
signs players in today; content delivery comes next.

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

The session is saved on the device and refreshed when needed. The
[Unity guide](https://docs.toaba.io/docs/engines/unity) covers email codes, Google and Apple, and
keeping a guest's progress.

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
