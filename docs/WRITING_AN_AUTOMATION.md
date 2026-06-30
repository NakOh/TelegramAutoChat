# Writing an automation

This guide is written to be **self-contained** — a human or an AI agent can follow it without reading the engine source.

## The mental model

- You target **one peer** (`Target`) — a bot, a user, a group, a channel, or a supergroup. Use `AutomationTarget.ByUsername("name")` for anything with a public @username, or `AutomationTarget.ById(id)` for a private group/channel you're already in.
- The engine calls `OnTickAsync` on a schedule (optional) and `OnMessageAsync` for every message that arrives **from that target** (incoming channel/group posts included — you must be a member/subscriber).
- You act through `ctx`: send messages, notify yourself, read/write small state.
- Your config lives in `appsettings.json` under a section named exactly like your automation's `Name`.

That's it. No DI, no manual registration — drop a class in `Automations/` and it's discovered by reflection.

## Minimal template

Create `src/TelegramAutoChat.App/Automations/MyAutomation.cs`:

```csharp
using TelegramAutoChat.Core;

namespace TelegramAutoChat.App.Automations;

public sealed class MyAutomation : ChatAutomationBase {
    // 1) Config section key. Must match appsettings.json.
    public override string Name => "myrule";

    // 2) Which chat/bot. Username (no @) or AutomationTarget.ById(123).
    public override AutomationTarget Target => AutomationTarget.ByUsername(Cfg("target", "SomeBot"));

    // 3) Optional periodic action. Return null to disable.
    public override TimeSpan? TickInterval => TimeSpan.FromMinutes(CfgInt("intervalMinutes", 30));

    // 4) Optional: declare your config so the setup wizard prompts for it.
    public override IEnumerable<SettingSpec> Settings => new[] {
        new SettingSpec("target", "Bot username to watch (no @)", "SomeBot"),
        new SettingSpec("intervalMinutes", "Poll interval (minutes)", "30", SettingKind.Int),
    };

    public override async Task OnTickAsync(AutomationContext ctx) {
        await ctx.SendAsync("/ping");           // runs immediately on start, then every interval
    }

    public override async Task OnMessageAsync(AutomationContext ctx, IncomingMessage msg) {
        if (msg.Outgoing) return;               // ignore your own messages
        if (msg.Text.Contains("pong")) {
            await ctx.NotifyAsync("Got pong", msg.Text);   // → your Saved Messages + log
        }
    }
}
```

Add the config section:

```jsonc
"Automations": {
  "myrule": {
    "enabled": true,
    "target": "SomeBot",
    "intervalMinutes": 30
  }
}
```

Run `dotnet run --project src/TelegramAutoChat.App`. Done.

## API reference

### `ChatAutomationBase` (extend this)

| Member | Purpose |
|---|---|
| `string Name` | Config section key + display name. Make it a constant. |
| `AutomationTarget Target` | `AutomationTarget.ByUsername("Bot")` or `.ById(channelId)`. |
| `TimeSpan? TickInterval` | Periodic action interval, or `null`. |
| `IEnumerable<SettingSpec> Settings` | Declare your config keys here so the setup wizard prompts for them. Optional. |
| `OnStartAsync(ctx)` | Called once after the target is resolved. Load config into fields here. |
| `OnTickAsync(ctx)` | Called immediately on start, then every `TickInterval`. |
| `OnMessageAsync(ctx, msg)` | Called for each message from the target. |
| `Cfg(key, fallback)` | Read a string from your config section. |
| `CfgBool(key, fallback)` | Read a bool. |
| `CfgInt(key, fallback)` | Read an int. |

### `AutomationContext ctx`

| Member | Purpose |
|---|---|
| `SendAsync(text)` | Send text to the target chat. |
| `NotifyAsync(title, body)` | Send to your **Saved Messages** (phone push, no bot token) + log. |
| `GetHistoryAsync(limit)` | Read recent messages from the target. |
| `Store.Get/Set(key, value)` | Tiny persistent key/value (`state/<name>.json`). |
| `Log(message)` | Console/file log, prefixed with the automation name. |
| `Client`, `TargetPeer` | Raw WTelegramClient + peer — escape hatch for advanced calls. |

### `IncomingMessage msg`

| Field | Meaning |
|---|---|
| `Text` | Message text. |
| `Outgoing` | `true` if you sent it (usually skip these). |
| `Id` | Message id — handy for dedup (`if (msg.Id <= _lastId) return;`). |
| `FromId` | Sender user id (useful in groups). |

## Patterns

**Multi-step conversation (state machine):** keep instance fields on your class — the engine keeps a single instance alive for the whole run. Classify each incoming message, track where you are, and decide the next reply. (e.g. step A asks → you answer → step B asks → you answer → done.)

See the bundled [`KeywordAlertAutomation`](../src/TelegramAutoChat.App/Automations/KeywordAlertAutomation.cs) for a minimal, single-step reference.

**Avoid duplicate handling:** track the last handled `msg.Id` (in a field, or `ctx.Store` to survive restarts).

**Don't disrupt a pending manual step:** if you're waiting on the user (e.g. an SMS code), have `OnTickAsync` skip its action until that resolves (with a timeout so it can't hang forever).

**Notify, don't guess:** anything you can't safely automate (codes sent by SMS, captchas, unexpected prompts) → `ctx.NotifyAsync(...)` so the human can step in.

## Gotchas

- `Target`/`TickInterval` are read **after** `Configure` but **before** `OnStartAsync`, so they can depend on config but not on `ctx`.
- SMS verification codes arrive on the phone, not in Telegram — you cannot read them. Notify instead.
- Keep `TickInterval` ≥ ~1 hour for `/start`-style polling to stay friendly to Telegram's ToS.
