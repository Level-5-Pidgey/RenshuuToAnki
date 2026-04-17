using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Console.Services;

public class AnkiConnector
{
	private readonly HttpClient HttpClient;
	private readonly string AnkiConnectUrl;

	public AnkiConnector(HttpClient httpClient, string ankiConnectUrl)
	{
		HttpClient = httpClient;
		AnkiConnectUrl = ankiConnectUrl;
	}

	public async Task<NoteInfo[]> NotesInfoAsync(string query, CancellationToken ct = default)
	{
		var request = new AnkiRequest("notesInfo", new { query });
		var (result, _) = await PostAsync(request, ct);
		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var deserializeResult = JsonSerializer.Deserialize<NoteInfo[]>(result.GetRawText(), options);
		if (deserializeResult == null) return [];
		for (var i = 0; i < deserializeResult.Length; i++)
		{
			deserializeResult[i] = deserializeResult[i] with
			{
				Fields = new Dictionary<string, NoteField>(deserializeResult[i].Fields,
					StringComparer.OrdinalIgnoreCase)
			};
		}

		return deserializeResult;
	}

	internal async Task<bool> UpdateNoteAsync(long noteId, string value, CancellationToken ct = default)
	{
		var request = new AnkiRequest("updateNote", new
		{
			note = new { id = noteId, fields = new { field = value } }
		});

		for (var attempt = 0; attempt < 2; attempt++)
		{
			try
			{
				var (result, _) = await PostAsync(request, ct);
				var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
				var boolResult = JsonSerializer.Deserialize<bool[]>(result.GetRawText(), options);
				return boolResult?.FirstOrDefault() ?? false;
			}
			catch (HttpRequestException) when (attempt == 0)
			{
				await Task.Delay(500, ct);
			}
		}

		return false;
	}

	public async Task<bool> UpdateNoteFieldsAsync(long noteId, Dictionary<string, string> fields, CancellationToken ct = default)
	{
		var request = new AnkiRequest("updateNote", new
		{
			note = new { id = noteId, fields }
		});

		for (var attempt = 0; attempt < 2; attempt++)
		{
			try
			{
				var (_, error) = await PostAsync(request, ct);

				return error == null;
			}
			catch (HttpRequestException) when (attempt == 0)
			{
				await Task.Delay(500, ct);
			}
		}

		return false;
	}

	private async Task<(JsonElement Result, string? Error)> PostAsync(AnkiRequest request, CancellationToken ct)
	{
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		};

		var jsonPayload = JsonSerializer.Serialize(request, options);
		var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

		var response = await HttpClient.PostAsync(AnkiConnectUrl, content, ct);
		response.EnsureSuccessStatusCode();

		var responseBody = await response.Content.ReadAsStringAsync(ct);
		using var jsonDoc = JsonDocument.Parse(responseBody);
		var root = jsonDoc.RootElement;

		var error = root.TryGetProperty("error", out var errorElement) && errorElement.ValueKind != JsonValueKind.Null
			? errorElement.GetString()
			: null;

		if (error != null)
		{
			throw new Exception($"AnkiConnect error: {error}");
		}

		var result = root.TryGetProperty("result", out var resultElement) ? resultElement.Clone() : default;
		return (result, error);
	}
}

internal record AnkiRequest(string Action, object? Params = null, int Version = 6);

public record NoteInfo(
	long NoteId,
	string ModelName,
	string[] Tags,
	Dictionary<string, NoteField> Fields,
	long Mod,
	long[] Cards);

public record NoteField(string Value, int Order);