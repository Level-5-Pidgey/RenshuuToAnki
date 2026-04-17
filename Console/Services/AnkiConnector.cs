using System.Net.Http.Json;
using System.Text.Json;

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
        var response = await PostAsync(request, ct);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = JsonSerializer.Deserialize<NoteInfo[]>(response.RootElement.GetRawText(), options);
        return result ?? [];
    }

    public async Task<bool> UpdateNoteAsync(long noteId, string fieldName, string value, CancellationToken ct = default)
    {
        var request = new AnkiRequest("updateNote", new
        {
            note = new { id = noteId, fields = new { @field = value } }
        });

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var response = await PostAsync(request, ct);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<bool[]>(response.RootElement.GetRawText(), options);
                return result?.FirstOrDefault() ?? false;
            }
            catch (HttpRequestException) when (attempt == 0)
            {
                await Task.Delay(500, ct);
            }
        }

        return false;
    }

    private async Task<JsonDocument> PostAsync(AnkiRequest request, CancellationToken ct)
    {
        var response = await HttpClient.PostAsJsonAsync(
            $"{AnkiConnectUrl}/",
            request,
            cancellationToken: ct);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(content);
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