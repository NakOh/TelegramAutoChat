# 💬 TelegramAutoChat

A small, extensible base engine for **automating conversations on a Telegram user account** — with any bot, group, or channel.

Implement **one interface** to add a new automation; the engine handles login, session persistence, update routing, and scheduling for you. Designed to be easy to fork and extend (including by AI agents).

> Built on [WTelegramClient](https://github.com/wiz0u/WTelegramClient) (MTProto user-account client).

---

## Why

Some Telegram interactions are repetitive or time-sensitive — watch a channel for a keyword, send a command on a schedule, answer a prompt within a short window. Doing that by hand (especially across multiple accounts) is tedious. TelegramAutoChat lets you describe such an interaction **once, as code**, and run it unattended.

The bundled example ([`KeywordAlertAutomation`](src/TelegramAutoChat.App/Automations/KeywordAlertAutomation.cs)) watches a chat and pings you when a keyword shows up. The engine itself is generic — "react to messages from a target chat + optionally run periodic actions" — so you can build whatever fits your own use.

## Architecture

```
TelegramAutoChat.Core   ← reusable engine + the IChatAutomation extension point
        ▲
        │ references
        │
TelegramAutoChat.App     ← console runner; auto-discovers automations in Automations/
        └── Automations/KeywordAlertAutomation.cs   ← example
```

The whole extension surface is one interface:

```csharp
public interface IChatAutomation {
    string Name { get; }                 // config section key (e.g. "myrule")
    AutomationTarget Target { get; }      // which chat/bot — AutomationTarget.ByUsername("...")
    TimeSpan? TickInterval { get; }       // periodic action interval (null = none)
    IEnumerable<SettingSpec> Settings { get; }  // config the setup wizard prompts for
    void Configure(JsonElement config);   // your config section is injected here
    Task OnStartAsync(AutomationContext ctx);
    Task OnTickAsync(AutomationContext ctx);                       // e.g. send a command
    Task OnMessageAsync(AutomationContext ctx, IncomingMessage m); // react to messages
}
```

`AutomationContext` gives you everything you need:

```csharp
await ctx.SendAsync("/start");              // send to the target chat
await ctx.NotifyAsync("title", "body");     // push to your Telegram "Saved Messages" + log
await ctx.GetHistoryAsync(10);              // read recent messages
ctx.Store.Set("lastId", "123");             // tiny persistent key/value
ctx.Client / ctx.TargetPeer                 // escape hatch to raw WTelegramClient
```

Notifications go to your own **Saved Messages**, so they reach your phone with **no bot token required**.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- A Telegram account
- **Your own `api_id` / `api_hash`** from [my.telegram.org](https://my.telegram.org) → *API development tools*. These are per-developer credentials — get your own, don't share one across users (Telegram may rate-limit/ban an `api_id` used by many unrelated accounts).

## Quick start

```bash
git clone https://github.com/NakOh/TelegramAutoChat
cd TelegramAutoChat
dotnet run --project src/TelegramAutoChat.App
```

On first run, an **interactive setup wizard** asks you everything in the console (your `api_id`/`api_hash`, login phone, which automations to enable, and each automation's settings) and writes `appsettings.json` for you — then logs in and starts immediately. Just press Enter to accept the `[default]` shown for each question. Re-run the wizard anytime with `--setup`:

```bash
dotnet run --project src/TelegramAutoChat.App -- --setup
```

The wizard produces a config like this (you can also hand-edit it):

```jsonc
{
  "ApiId": 1234567,                 // your own, from my.telegram.org
  "ApiHash": "your_api_hash_here",
  "LoginPhone": "821012345678",     // this account's number (intl). blank → prompted at login
  "SessionPath": "telegramautochat.session",
  "Automations": {
    "keywordAlert": {
      "enabled": true,
      "chat": "durov",                  // chat/channel username to watch (no @)
      "keywords": "telegram, release"   // comma-separated; notifies you on a match
    }
  }
}
```

On first login you enter the SMS/app code once; the session is reused after that.

## Add your own automation

See **[docs/WRITING_AN_AUTOMATION.md](docs/WRITING_AN_AUTOMATION.md)**. In short:

1. Add a class in `src/TelegramAutoChat.App/Automations/` that extends `ChatAutomationBase`.
2. Declare your config keys via the `Settings` property — the setup wizard then asks for them automatically (or just hand-add a section under `Automations` in `appsettings.json`).
3. Run. The runner auto-discovers it by reflection — no registration needed.

## Running multiple accounts

Each account = its own working directory (own `appsettings.json` + `*.session`). Publish once and copy the folder per account:

```bash
dotnet publish src/TelegramAutoChat.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## ⚠️ Notes & limits

- **SMS codes can't be auto-read** (they arrive on your phone, not in Telegram). Automations should *notify* and let you enter them.
- Automating a **user account** is a gray area under Telegram's ToS. Keep intervals reasonable (≥ 1 hour) and don't spam.
- `appsettings.json` and `*.session` hold credentials/personal data — they're git-ignored. **Never commit them.**
- This is a tool for automating *your own* accounts and *your own* interactions. Use responsibly.

## License

MIT — see [LICENSE](LICENSE).
