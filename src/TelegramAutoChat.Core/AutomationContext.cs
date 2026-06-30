using TL;
using WTelegram;

namespace TelegramAutoChat.Core;

/// <summary>
/// 자동화가 행동할 때 쓰는 도구 모음. 대상 방으로의 전송·자기 알림·로깅·상태저장을 제공한다.
/// (귀찮은 peer 핸들링은 전부 엔진이 처리하므로 자동화는 이것만 쓰면 된다.)
/// </summary>
public sealed class AutomationContext {
	private readonly Client _client;
	private readonly InputPeer _target;
	private readonly Action<string> _log;

	internal AutomationContext(Client client, InputPeer target, string name, IStateStore store, Action<string> log) {
		_client = client;
		_target = target;
		Name = name;
		Store = store;
		_log = log;
	}

	/// <summary>이 자동화의 이름.</summary>
	public string Name { get; }

	/// <summary>자동화별 영속 상태 저장소.</summary>
	public IStateStore Store { get; }

	/// <summary>고급 사용을 위한 원본 클라이언트(WTelegramClient).</summary>
	public Client Client => _client;

	/// <summary>대상 방의 InputPeer (고급 사용).</summary>
	public InputPeer TargetPeer => _target;

	/// <summary>대상 방/봇에 텍스트 전송.</summary>
	public Task<Message> SendAsync(string text) => _client.SendMessageAsync(_target, text);

	/// <summary>대상 방의 최근 메시지 N개 조회 (필요 시).</summary>
	public Task<Messages_MessagesBase> GetHistoryAsync(int limit = 10) =>
		_client.Messages_GetHistory(_target, limit: limit);

	/// <summary>
	/// 내 텔레그램 "Saved Messages"로 알림 전송(폰까지 푸시) + 로그.
	/// 봇 토큰 없이 동작하는 기본 알림 채널.
	/// </summary>
	public async Task NotifyAsync(string title, string body) {
		Log($"🔔 {title} — {body.Replace("\n", " ")}");
		try {
			await _client.SendMessageAsync(new InputPeerSelf(), $"{title}\n{body}");
		} catch (Exception ex) {
			Log($"(자기 알림 전송 실패: {ex.Message})");
		}
	}

	/// <summary>콘솔/파일 로그 (자동화 이름이 접두로 붙는다).</summary>
	public void Log(string message) => _log($"[{Name}] {message}");
}
