using System.ComponentModel;
using Spectre.Console.Cli;

namespace Console;

public class Settings : CommandSettings
{
    public Settings() { }

    public Settings(string query, string ankiConnectUrl, int requestsPerMinute, bool readOnly)
    {
        Query = query;
        AnkiConnectUrl = ankiConnectUrl;
        RequestsPerMinute = requestsPerMinute;
        ReadOnly = readOnly;
    }

    [CommandOption("--query")]
    [Description("Anki search query to find notes. E.g. 'tag:Languages::Japanese::Writing::Kanji' or 'deck:Kanji'")]
    public string Query { get; init; } = "tag:Languages::Japanese::Writing::Kanji";

    [CommandOption("--anki-url")]
    [Description("AnkiConnect HTTP URL.")]
    public string AnkiConnectUrl { get; init; } = "http://localhost:8765";

    [CommandOption("--rpm")]
    [Description("Max requests per minute to Renshuu.")]
    public int RequestsPerMinute { get; init; } = 120;

    [CommandOption("--read-only")]
    [Description("Preview changes without writing to Anki.")]
    public bool ReadOnly { get; init; } = true;
}