using System.Text.Json;
using System.Text.Json.Serialization;

namespace TelegramAutoChat.Core;

/// <summary>전역 설정(appsettings.json). 자동화별 설정은 <see cref="Automations"/> 에 섹션으로 들어간다.</summary>
public sealed class HostConfig {
	/// <summary>https://my.telegram.org → API development tools 에서 본인이 발급받은 api_id. (공유 금지)</summary>
	public int ApiId { get; set; } = 0;

	/// <summary>본인 api_hash.</summary>
	public string ApiHash { get; set; } = "";

	/// <summary>이 텔레그램 계정 전화번호(국가코드 포함, 예: 821012345678). 비우면 실행 중 입력받음.</summary>
	public string LoginPhone { get; set; } = "";

	/// <summary>세션 파일 경로 (계정마다 다르게).</summary>
	public string SessionPath { get; set; } = "telegramautochat.session";

	/// <summary>자동화 이름 → 설정 섹션. 섹션에 <c>"enabled": false</c> 면 비활성.</summary>
	public Dictionary<string, JsonElement> Automations { get; set; } = new();

	private static readonly JsonSerializerOptions Opts = new() {
		PropertyNameCaseInsensitive = true,
		WriteIndented = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	public static HostConfig Load(string path) =>
		JsonSerializer.Deserialize<HostConfig>(File.ReadAllText(path), Opts) ?? new HostConfig();

	public string ToJson() => JsonSerializer.Serialize(this, Opts);
}
