using System.Reflection;
using System.Text.Json;
using TelegramAutoChat.Core;

namespace TelegramAutoChat.App;

/// <summary>
/// 실행기. 설정을 읽고(없으면 콘솔 마법사로 받고), 어셈블리에서 IChatAutomation 구현을
/// 자동 발견해 등록한 뒤 엔진을 돌린다.
/// 새 자동화를 추가하려면 Automations/ 에 클래스 파일 하나만 떨구면 된다 (설정 항목은 Settings 로 선언).
/// </summary>
internal static class Program {
	private const string ConfigFileName = "appsettings.json";

	private static async Task<int> Main(string[] args) {
		Console.OutputEncoding = System.Text.Encoding.UTF8;
		Console.Title = "TelegramAutoChat";
		WTelegram.Helpers.Log = (lvl, msg) => { if (lvl >= 4) Console.Error.WriteLine($"[WT] {msg}"); };

		Banner();

		var automations = DiscoverAutomations();
		Log($"발견된 자동화: {(automations.Count == 0 ? "(없음)" : string.Join(", ", automations.Select(a => a.Name)))}\n");

		var configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
		bool runSetup = args.Contains("--setup") || !File.Exists(configPath);

		HostConfig cfg;
		try {
			if (runSetup) {
				if (Console.IsInputRedirected) {
					Log("입력이 리다이렉트되어 마법사를 건너뜁니다. 기본 설정 파일을 생성합니다.");
					cfg = BuildDefaultConfig(automations);
				} else {
					cfg = RunSetupWizard(automations);
				}
				File.WriteAllText(configPath, cfg.ToJson());
				Log($"✅ 설정 저장됨: {configPath}\n");
			} else {
				cfg = HostConfig.Load(configPath);
			}
		} catch (Exception ex) {
			Log($"❌ 설정 처리 실패: {ex.Message}");
			return 1;
		}

		if (cfg.ApiId == 0 || string.IsNullOrWhiteSpace(cfg.ApiHash)) {
			Log("❌ api_id / api_hash 가 설정되지 않았습니다.");
			Console.WriteLine("   https://my.telegram.org → API development tools 에서 본인 키를 발급받아");
			Console.WriteLine("   appsettings.json 에 넣거나 `--setup` 으로 다시 설정해 주세요.");
			return 1;
		}

		var host = new AutomationHost(cfg, automations, Prompt, Log);

		using var cts = new CancellationTokenSource();
		Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

		Console.WriteLine("로그인을 진행합니다. (최초 1회만 인증코드 입력, 이후 세션 자동 복원)\n");
		try {
			await host.RunAsync(cts.Token);
		} catch (Exception ex) {
			Log($"❌ 실행 오류: {ex.Message}");
			return 1;
		}
		return 0;
	}

	/// <summary>로드된 어셈블리에서 파라미터 없는 생성자를 가진 IChatAutomation 구현을 모두 찾는다.</summary>
	private static List<IChatAutomation> DiscoverAutomations() {
		var result = new List<IChatAutomation>();
		foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
			Type[] types;
			try { types = asm.GetTypes(); }
			catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }

			foreach (var t in types) {
				if (t is { IsClass: true, IsAbstract: false } &&
					typeof(IChatAutomation).IsAssignableFrom(t) &&
					t.GetConstructor(Type.EmptyTypes) != null) {
					try { result.Add((IChatAutomation)Activator.CreateInstance(t)!); }
					catch (Exception ex) { Log($"⚠️ {t.Name} 인스턴스화 실패: {ex.Message}"); }
				}
			}
		}
		return result;
	}

	// ── 초기 설정 마법사 ───────────────────────────────────────────────────────

	private static HostConfig RunSetupWizard(List<IChatAutomation> automations) {
		Console.WriteLine("════════ 초기 설정 ════════");
		Console.WriteLine("엔터만 누르면 [기본값]이 적용됩니다. 나중에 다시 하려면 --setup 옵션으로 실행하세요.\n");

		var cfg = new HostConfig();

		Console.WriteLine("• Telegram API 키  (https://my.telegram.org → API development tools 에서 본인이 발급)");
		cfg.ApiId = AskInt("  api_id", 0);
		cfg.ApiHash = AskText("  api_hash", "");

		Console.WriteLine("\n• 텔레그램 계정");
		cfg.LoginPhone = AskText("  전화번호 (국가코드 포함, 예: 821012345678) — 비우면 로그인 단계에서 물어봄", "");

		foreach (var a in automations) {
			Console.WriteLine($"\n• 자동화 '{a.Name}'");
			var section = new Dictionary<string, object>();
			bool enable = AskBool("  이 자동화를 사용할까요?", true);
			section["enabled"] = enable;

			if (enable) {
				var answered = new Dictionary<string, string>();
				foreach (var s in a.Settings) {
					if (s.OnlyIf is { } dep && (!answered.TryGetValue(dep, out var dv) || dv != "true"))
						continue; // 선행 Bool 이 false 면 건너뜀

					switch (s.Kind) {
						case SettingKind.Bool:
							var b = AskBool("  " + s.Prompt, s.Default.Equals("true", StringComparison.OrdinalIgnoreCase));
							section[s.Key] = b;
							answered[s.Key] = b ? "true" : "false";
							break;
						case SettingKind.Int:
							var n = AskInt("  " + s.Prompt, int.TryParse(s.Default, out var d) ? d : 0);
							section[s.Key] = n;
							answered[s.Key] = n.ToString();
							break;
						default:
							var t = AskText("  " + s.Prompt, s.Default);
							section[s.Key] = t;
							answered[s.Key] = t;
							break;
					}
				}
			}
			cfg.Automations[a.Name] = JsonSerializer.SerializeToElement(section);
		}

		Console.WriteLine("\n═══════════════════════════\n");
		return cfg;
	}

	private static HostConfig BuildDefaultConfig(List<IChatAutomation> automations) {
		var cfg = new HostConfig();
		foreach (var a in automations) {
			var section = new Dictionary<string, object> { ["enabled"] = true };
			foreach (var s in a.Settings) {
				section[s.Key] = s.Kind switch {
					SettingKind.Bool => s.Default.Equals("true", StringComparison.OrdinalIgnoreCase),
					SettingKind.Int => int.TryParse(s.Default, out var d) ? d : 0,
					_ => s.Default
				};
			}
			cfg.Automations[a.Name] = JsonSerializer.SerializeToElement(section);
		}
		return cfg;
	}

	private static string AskText(string prompt, string @default) {
		Console.Write(string.IsNullOrEmpty(@default) ? $"{prompt}: " : $"{prompt} [{@default}]: ");
		var input = Console.ReadLine()?.Trim();
		return string.IsNullOrEmpty(input) ? @default : input;
	}

	private static bool AskBool(string prompt, bool @default) {
		Console.Write($"{prompt} (y/n) [{(@default ? "y" : "n")}]: ");
		var input = Console.ReadLine()?.Trim().ToLowerInvariant();
		if (string.IsNullOrEmpty(input)) return @default;
		return input is "y" or "yes" or "true" or "1";
	}

	private static int AskInt(string prompt, int @default) {
		Console.Write($"{prompt} [{@default}]: ");
		var input = Console.ReadLine()?.Trim();
		return int.TryParse(input, out var n) ? n : @default;
	}

	private static string? Prompt(string label) {
		Console.Write(label);
		return Console.ReadLine()?.Trim();
	}

	private static void Banner() {
		Console.WriteLine("┌────────────────────────────────────────────┐");
		Console.WriteLine("│   💬  TelegramAutoChat                        │");
		Console.WriteLine("│   규칙 기반 텔레그램 자동 대화 엔진              │");
		Console.WriteLine("└────────────────────────────────────────────┘\n");
	}

	internal static void Log(string msg) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
}
