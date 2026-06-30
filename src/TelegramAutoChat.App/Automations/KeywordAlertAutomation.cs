using TelegramAutoChat.Core;

namespace TelegramAutoChat.App.Automations;

/// <summary>
/// 예제 자동화: 특정 방/채널의 메시지를 지켜보다가, 내가 지정한 키워드가 들어오면
/// 내 텔레그램(Saved Messages)으로 알림을 보낸다.
///
/// 이 한 파일이 "베이스 엔진 위에 자동화를 어떻게 얹는지"의 레퍼런스다.
/// 주기 작업 없이 수신 반응만 하는 가장 단순한 형태(= <see cref="TickInterval"/> 없음).
///
/// 설정(appsettings.json 의 "keywordAlert" 섹션):
///   { "enabled": true, "chat": "durov", "keywords": "telegram, update, release" }
/// </summary>
public sealed class KeywordAlertAutomation : ChatAutomationBase {
	public override string Name => "keywordAlert";

	public override AutomationTarget Target => AutomationTarget.ByUsername(Cfg("chat", ""));

	// 초기 설정 마법사가 물어볼 항목들
	public override IEnumerable<SettingSpec> Settings => new[] {
		new SettingSpec("chat", "지켜볼 방/채널 username (@ 없이)", ""),
		new SettingSpec("keywords", "알림 키워드 (쉼표로 구분)", ""),
	};

	private string[] _keywords = Array.Empty<string>();

	public override Task OnStartAsync(AutomationContext ctx) {
		_keywords = Cfg("keywords")
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		ctx.Log(_keywords.Length == 0 ? "키워드가 비어있음 — 모든 메시지 무시" : $"키워드 {_keywords.Length}개 감시");
		return Task.CompletedTask;
	}

	public override async Task OnMessageAsync(AutomationContext ctx, IncomingMessage msg) {
		if (msg.Outgoing || _keywords.Length == 0 || string.IsNullOrWhiteSpace(msg.Text)) return;

		var hit = _keywords.FirstOrDefault(k => msg.Text.Contains(k, StringComparison.OrdinalIgnoreCase));
		if (hit == null) return;

		// 같은 메시지 중복 알림 방지 (재시작에도 유지)
		if (ctx.Store.Get("lastId") is { } last && int.TryParse(last, out var lastId) && msg.Id <= lastId) return;
		ctx.Store.Set("lastId", msg.Id.ToString());

		await ctx.NotifyAsync($"🔔 키워드 '{hit}' 감지", Trim(msg.Text));
	}

	private static string Trim(string text) => text.Length > 300 ? text[..300] + "…" : text;
}
