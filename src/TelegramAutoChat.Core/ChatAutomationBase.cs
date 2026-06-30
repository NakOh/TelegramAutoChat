using System.Text.Json;

namespace TelegramAutoChat.Core;

/// <summary>
/// <see cref="IChatAutomation"/> 편의 베이스. 필요한 멤버만 override 하면 된다.
/// 설정값은 <see cref="Cfg"/>/<see cref="CfgBool"/>/<see cref="CfgInt"/> 로 읽는다.
/// </summary>
public abstract class ChatAutomationBase : IChatAutomation {
	/// <summary>이 자동화의 설정 섹션(JSON). <see cref="Configure"/> 시점에 채워진다.</summary>
	protected JsonElement Config { get; private set; }

	public virtual string Name => GetType().Name;

	public abstract AutomationTarget Target { get; }

	public virtual TimeSpan? TickInterval => null;

	public virtual IEnumerable<SettingSpec> Settings => Array.Empty<SettingSpec>();

	public virtual void Configure(JsonElement config) => Config = config;

	public virtual Task OnStartAsync(AutomationContext ctx) => Task.CompletedTask;
	public virtual Task OnTickAsync(AutomationContext ctx) => Task.CompletedTask;
	public virtual Task OnMessageAsync(AutomationContext ctx, IncomingMessage msg) => Task.CompletedTask;

	// ── 설정 읽기 헬퍼 ─────────────────────────────────────────────────────

	protected string Cfg(string key, string fallback = "") =>
		Config.ValueKind == JsonValueKind.Object && Config.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
			? v.GetString() ?? fallback
			: fallback;

	protected bool CfgBool(string key, bool fallback = false) {
		if (Config.ValueKind == JsonValueKind.Object && Config.TryGetProperty(key, out var v)) {
			if (v.ValueKind == JsonValueKind.True) return true;
			if (v.ValueKind == JsonValueKind.False) return false;
		}
		return fallback;
	}

	protected int CfgInt(string key, int fallback = 0) =>
		Config.ValueKind == JsonValueKind.Object && Config.TryGetProperty(key, out var v) && v.TryGetInt32(out var n)
			? n
			: fallback;
}
