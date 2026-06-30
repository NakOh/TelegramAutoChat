using System.Collections.Concurrent;
using System.Text.Json;

namespace TelegramAutoChat.Core;

/// <summary>자동화별 간단한 영속 key/value 저장소 (예: 마지막 처리한 메시지 id).</summary>
public interface IStateStore {
	string? Get(string key);
	void Set(string key, string value);
}

/// <summary><c>state/{name}.json</c> 에 저장하는 기본 구현.</summary>
public sealed class JsonFileStateStore : IStateStore {
	private readonly string _path;
	private readonly ConcurrentDictionary<string, string> _data;
	private readonly object _lock = new();

	public JsonFileStateStore(string directory, string name) {
		Directory.CreateDirectory(directory);
		_path = Path.Combine(directory, $"{name}.json");
		_data = File.Exists(_path)
			? new(JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path)) ?? new())
			: new();
	}

	public string? Get(string key) => _data.TryGetValue(key, out var v) ? v : null;

	public void Set(string key, string value) {
		_data[key] = value;
		lock (_lock) {
			File.WriteAllText(_path, JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
		}
	}
}
