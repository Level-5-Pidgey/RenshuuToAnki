using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Console;

public class CommandSettings : Spectre.Console.Cli.CommandSettings
{
	[CommandOption("-q|--query")]
	[Description("Anki search query to find notes. E.g. 'tag:Languages::Japanese::Writing::Kanji' or 'deck:Kanji'")]
	public required string Query { get; set; }

	[CommandOption("-u|--anki-url")]
	[Description("AnkiConnect HTTP URL.")]
	[DefaultValue("http://localhost:8765")]
	public string AnkiConnectUrl { get; set; } = "http://localhost:8765";

	[CommandOption("-l|--rpm")]
	[Description("Max requests per minute to Renshuu.")]
	[DefaultValue(120)]
	public int RequestsPerMinute { get; set; }

	[CommandOption("-r|--read-only")]
	[Description("Preview changes without writing to Anki.")]
	[DefaultValue(false)]
	public bool ReadOnly { get; set; }

	[CommandOption("-k|--kanji-field")]
	[Description("Field containing the kanji character to look up.")]
	[DefaultValue("Kanji")]
	public string KanjiField { get; set; } = "Kanji";

	[CommandOption("-m|--mnemonic-field")]
	[Description("Field to write fetched mnemonics into.")]
	[DefaultValue("Mnemonic")]
	public string MnemonicField { get; set; } = "Mnemonic";

	[CommandOption("-o|--overwrite")]
	[Description("Overwrite existing mnemonic values.")]
	[DefaultValue(false)]
	public bool Overwrite { get; set; }

	[CommandOption("-c|--mnemonic-kanji-class")]
	[Description("CSS class name for kanji spans in cleaned mnemonics (e.g., 'kanji'). If empty, preserves original span structure with data-klook removed.")]
	public string? MnemonicKanjiClass { get; set; }

	public override ValidationResult Validate()
	{
		return RequestsPerMinute <= 0
			? ValidationResult.Error($"--rpm must be greater than 0, got {RequestsPerMinute}.")
			: ValidationResult.Success();
	}
}