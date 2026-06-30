namespace TelegramAutoChat.Core;

/// <summary>
/// 어떤 대화방/봇을 대상으로 할지 가리키는 값. 보통 <see cref="ByUsername"/>(예: "SomeBot")를 쓴다.
/// </summary>
public sealed class AutomationTarget {
	/// <summary>'@' 없는 username (예: "SomeBot"). id 대상이면 null.</summary>
	public string? Username { get; }

	/// <summary>username이 없는 경우의 채널/그룹 id. username 대상이면 null.</summary>
	public long? PeerId { get; }

	private AutomationTarget(string? username, long? peerId) {
		Username = username;
		PeerId = peerId;
	}

	/// <summary>@username 으로 대상 지정 (봇·공개 채널·공개 그룹).</summary>
	public static AutomationTarget ByUsername(string username) => new(username.TrimStart('@'), null);

	/// <summary>채널/그룹 id 로 대상 지정 (내가 이미 참여 중인 방).</summary>
	public static AutomationTarget ById(long peerId) => new(null, peerId);

	public override string ToString() => Username is not null ? $"@{Username}" : $"id:{PeerId}";
}
