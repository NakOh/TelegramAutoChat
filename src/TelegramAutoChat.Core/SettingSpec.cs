namespace TelegramAutoChat.Core;

public enum SettingKind { Text, Bool, Int }

/// <summary>
/// 자동화가 "내 설정 항목은 이렇다"라고 선언하는 단위. 실행기의 초기 설정 마법사가
/// 이 목록을 보고 콘솔에서 하나씩 물어본다.
/// </summary>
/// <param name="Key">설정 키 (config 섹션의 속성명).</param>
/// <param name="Prompt">사용자에게 보여줄 질문.</param>
/// <param name="Default">기본값 (엔터만 누르면 적용).</param>
/// <param name="Kind">값 종류 (Text/Bool/Int).</param>
/// <param name="OnlyIf">지정 시, 같은 자동화의 이 Bool 키가 true 일 때만 질문한다.</param>
public sealed record SettingSpec(
	string Key,
	string Prompt,
	string Default = "",
	SettingKind Kind = SettingKind.Text,
	string? OnlyIf = null);
