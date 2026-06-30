using System.Text.Json;
using TL;
using WTelegram;

namespace TelegramAutoChat.Core;

/// <summary>
/// 엔진. 텔레그램 로그인/세션을 관리하고, 들어오는 메시지를 해당 자동화로 라우팅하며,
/// 주기 작업 타이머를 돌린다. 자동화는 이걸 직접 다룰 필요 없이 <see cref="IChatAutomation"/> 만 구현하면 된다.
/// </summary>
public sealed class AutomationHost {
	private readonly HostConfig _cfg;
	private readonly IReadOnlyList<IChatAutomation> _automations;
	private readonly Func<string, string?> _prompt;   // 인증코드/2FA 등 대화형 입력
	private readonly Action<string> _log;
	private readonly string _stateDir;

	private Client _client = null!;
	private CancellationToken _ct;
	private readonly Dictionary<string, (IChatAutomation a, AutomationContext ctx)> _byPeerKey = new();

	public AutomationHost(
		HostConfig cfg,
		IEnumerable<IChatAutomation> automations,
		Func<string, string?> interactiveInput,
		Action<string>? log = null,
		string? stateDir = null) {
		_cfg = cfg;
		_automations = automations.ToList();
		_prompt = interactiveInput;
		_log = log ?? Console.WriteLine;
		_stateDir = stateDir ?? Path.Combine(AppContext.BaseDirectory, "state");
	}

	/// <summary>로그인 후 모든 활성 자동화를 시작하고, 취소될 때까지 실행한다.</summary>
	public async Task RunAsync(CancellationToken ct = default) {
		_ct = ct;
		_client = new Client(Config);
		_client.OnUpdates += OnUpdates;

		var me = await _client.LoginUserIfNeeded();
		_log($"✅ 로그인 완료: {me.first_name} {me.last_name} (@{me.username})");

		int started = 0;
		foreach (var a in _automations) {
			if (!_cfg.Automations.TryGetValue(a.Name, out var section)) {
				_log($"⏭️  '{a.Name}': 설정 섹션 없음 — 건너뜀");
				continue;
			}
			if (IsDisabled(section)) {
				_log($"⏸️  '{a.Name}': enabled=false — 건너뜀");
				continue;
			}

			try {
				a.Configure(section);
				var peer = await ResolveAsync(a.Target);
				if (peer == null) {
					_log($"❌ '{a.Name}': 대상 {a.Target} 해석 실패 — 건너뜀");
					continue;
				}

				var ctx = new AutomationContext(_client, peer, a.Name, new JsonFileStateStore(_stateDir, a.Name), _log);
				_byPeerKey[PeerKey(peer)] = (a, ctx);

				await a.OnStartAsync(ctx);
				if (a.TickInterval is { } interval) StartTicking(a, ctx, interval);

				_log($"▶️  '{a.Name}' 시작 → {a.Target}" + (a.TickInterval is { } iv ? $" (주기 {iv.TotalMinutes:0}분)" : ""));
				started++;
			} catch (Exception ex) {
				_log($"❌ '{a.Name}' 시작 실패: {ex.Message}");
			}
		}

		if (started == 0) _log("⚠️ 활성화된 자동화가 없습니다. appsettings.json 의 automations 설정을 확인하세요.");

		try { await Task.Delay(Timeout.Infinite, ct); }
		catch (OperationCanceledException) { /* 정상 종료 */ }
	}

	// ── 주기 작업 ────────────────────────────────────────────────────────────

	private void StartTicking(IChatAutomation a, AutomationContext ctx, TimeSpan interval) {
		_ = Task.Run(async () => {
			await SafeTick(a, ctx);                          // 시작 즉시 1회
			using var timer = new PeriodicTimer(interval);
			try {
				while (await timer.WaitForNextTickAsync(_ct))
					await SafeTick(a, ctx);
			} catch (OperationCanceledException) { }
		}, _ct);
	}

	private async Task SafeTick(IChatAutomation a, AutomationContext ctx) {
		try { await a.OnTickAsync(ctx); }
		catch (Exception ex) { _log($"[{a.Name}] tick 오류: {ex.Message}"); }
	}

	// ── 수신 라우팅 ──────────────────────────────────────────────────────────

	private async Task OnUpdates(UpdatesBase updates) {
		try {
			// UpdateList 는 모든 래퍼(Updates / UpdatesCombined / UpdateShort / UpdateShort(Chat)Message)를
			// 균일하게 펼쳐준다. UpdateNewChannelMessage 는 UpdateNewMessage 의 하위형이라
			// 이 한 케이스로 유저·봇·그룹·채널·슈퍼그룹 메시지를 모두 처리한다.
			foreach (var upd in updates.UpdateList)
				if (upd is UpdateNewMessage { message: Message m })
					await Dispatch(PeerKey(m.peer_id), m.id, m.flags.HasFlag(Message.Flags.out_), m.message, FromId(m));
		} catch (Exception ex) {
			_log($"OnUpdates 오류: {ex.Message}");
		}
	}

	private async Task Dispatch(string? key, int id, bool outgoing, string? text, long fromId) {
		if (key == null || !_byPeerKey.TryGetValue(key, out var entry)) return;
		var msg = new IncomingMessage(fromId, id, outgoing, text ?? "");
		try { await entry.a.OnMessageAsync(entry.ctx, msg); }
		catch (Exception ex) { _log($"[{entry.a.Name}] 메시지 처리 오류: {ex.Message}"); }
	}

	// ── 대상 해석 ────────────────────────────────────────────────────────────

	private async Task<InputPeer?> ResolveAsync(AutomationTarget t) {
		if (t.Username is { } username) {
			var resolved = await _client.Contacts_ResolveUsername(username);
			return resolved; // Contacts_ResolvedPeer -> InputPeer 암시적 변환
		}
		if (t.PeerId is { } id) {
			// 내가 참여 중인 채널/그룹에서 id 매칭 (access_hash 확보용)
			var chats = await _client.Messages_GetAllChats();
			if (chats.chats.TryGetValue(id, out var chat)) return chat; // ChatBase -> InputPeer 암시적 변환
		}
		return null;
	}

	// ── peer 키 (수신 라우팅 매칭용) ──────────────────────────────────────────

	private static string PeerKey(InputPeer p) => p switch {
		InputPeerUser u => KeyUser(u.user_id),
		InputPeerChannel c => $"c:{c.channel_id}",
		InputPeerChat g => KeyChat(g.chat_id),
		_ => "?"
	};

	private static string? PeerKey(Peer p) => p switch {
		PeerUser u => KeyUser(u.user_id),
		PeerChannel c => $"c:{c.channel_id}",
		PeerChat g => KeyChat(g.chat_id),
		_ => null
	};

	private static string KeyUser(long uid) => $"u:{uid}";
	private static string KeyChat(long cid) => $"g:{cid}";

	private static long FromId(Message m) =>
		m.from_id is PeerUser pu ? pu.user_id : (m.peer_id is PeerUser ppu ? ppu.user_id : 0);

	private static bool IsDisabled(JsonElement section) =>
		section.ValueKind == JsonValueKind.Object &&
		section.TryGetProperty("enabled", out var e) &&
		e.ValueKind == JsonValueKind.False;

	// ── 로그인 설정 콜백 ───────────────────────────────────────────────────────

	private string? Config(string what) {
		switch (what) {
			case "api_id": return _cfg.ApiId.ToString();
			case "api_hash": return _cfg.ApiHash;
			case "session_pathname":
				return Path.IsPathRooted(_cfg.SessionPath)
					? _cfg.SessionPath
					: Path.Combine(AppContext.BaseDirectory, _cfg.SessionPath);
			case "phone_number":
				return !string.IsNullOrWhiteSpace(_cfg.LoginPhone)
					? _cfg.LoginPhone
					: _prompt("텔레그램 전화번호 (국가코드 포함, 예: 821012345678): ");
			case "verification_code": return _prompt("텔레그램으로 받은 인증코드 입력: ");
			case "password": return _prompt("2단계 인증(2FA) 비밀번호 입력: ");
			default: return null;
		}
	}
}
