using System.Text.Json;

namespace TelegramAutoChat.Core;

/// <summary>
/// 하나의 자동화 단위. 이 인터페이스(또는 <see cref="ChatAutomationBase"/>)만 구현하면
/// 엔진이 로그인·세션·업데이트 라우팅·스케줄링을 모두 대신 처리한다.
///
/// 호출 순서(엔진이 보장):
///   1. <see cref="Name"/> 로 설정 섹션을 찾고  2. <see cref="Configure"/> 로 설정 주입  →
///   3. <see cref="Target"/>/<see cref="TickInterval"/> 읽어 대상 해석  →
///   4. <see cref="OnStartAsync"/> 1회  →  이후 <see cref="OnTickAsync"/>(주기)·<see cref="OnMessageAsync"/>(수신 시).
/// </summary>
public interface IChatAutomation {
	/// <summary>설정 파일의 섹션 키이자 표시 이름 (예: "myrule"). 고정 문자열이어야 한다.</summary>
	string Name { get; }

	/// <summary>대상 방/봇.</summary>
	AutomationTarget Target { get; }

	/// <summary>주기 작업 간격. null 이면 주기 작업 없음(수신 반응만).</summary>
	TimeSpan? TickInterval { get; }

	/// <summary>초기 설정 마법사가 물어볼 설정 항목들. 없으면 빈 목록.</summary>
	IEnumerable<SettingSpec> Settings => Array.Empty<SettingSpec>();

	/// <summary>설정 섹션 주입 (Target/TickInterval 읽기 전에 호출됨).</summary>
	void Configure(JsonElement config);

	/// <summary>대상 해석 후 1회 호출. 초기화용.</summary>
	Task OnStartAsync(AutomationContext ctx);

	/// <summary><see cref="TickInterval"/> 마다 호출 (시작 직후 1회 포함).</summary>
	Task OnTickAsync(AutomationContext ctx);

	/// <summary>대상 방의 메시지가 올 때마다 호출.</summary>
	Task OnMessageAsync(AutomationContext ctx, IncomingMessage msg);
}
