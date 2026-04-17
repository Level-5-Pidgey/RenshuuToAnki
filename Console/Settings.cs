using System.ComponentModel;
using Spectre.Console.Cli;

namespace Console;

public class Settings : CommandSettings
{
    public Settings() { }

    public Settings(string query, string ankiConnectUrl, int requestsPerMinute, bool readOnly, string kanjiField, string mnemonicField, bool overwrite)
    {
        Query = query;
        AnkiConnectUrl = ankiConnectUrl;
        RequestsPerMinute = requestsPerMinute;
        ReadOnly = readOnly;
        KanjiField = kanjiField;
        MnemonicField = mnemonicField;
        Overwrite = overwrite;
    }

    [CommandOption("--query")]
    [Description("Anki search query to find notes. E.g. 'tag:Languages::Japanese::Writing::Kanji' or 'deck:Kanji'")]
    public required string Query { get; init; }

    [CommandOption("--anki-url")]
    [Description("AnkiConnect HTTP URL.")]
    public string AnkiConnectUrl { get; init; } = "http://localhost:8765";

    [CommandOption("--rpm")]
    [Description("Max requests per minute to Renshuu.")]
    public int RequestsPerMinute { get; init; } = 120;

    [CommandOption("--read-only")]
    [Description("Preview changes without writing to Anki.")]
    public bool ReadOnly { get; init; }

    [CommandOption("--kanji-field")]
    [Description("Field containing the kanji character to look up.")]
    public string KanjiField { get; } = "Kanji";

    [CommandOption("--mnemonic-field")]
    [Description("Field to write fetched mnemonics into.")]
    public string MnemonicField { get; } = "Mnemonic";

    [CommandOption("--overwrite")]
    [Description("Overwrite existing mnemonic values.")]
    public bool Overwrite { get; }
}