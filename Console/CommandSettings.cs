using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Console;

public class CommandSettings : Spectre.Console.Cli.CommandSettings
{
	private static readonly string[] SupportedSources =
		["kanji", "kunyomi", "onyomi", "radical", "meaning", "strokes", "mnemonic", "jlpt", "kentei"];

	[CommandOption("-q|--query")]
	[Description("Anki search query for cards to update. E.g. 'tag:Languages::Japanese::Writing::Kanji' or 'deck:Kanji'")]
	public required string Query { get; set; }

	[CommandOption("-u|--anki-url")]
	[Description("AnkiConnect HTTP URL.")]
	[DefaultValue("http://localhost:8765")]
	public string AnkiConnectUrl { get; set; } = "http://localhost:8765";

	[CommandOption("-l|--rpm")]
	[Description("Max requests per minute to Renshuu.")]
	[DefaultValue(120)]
	public int RequestsPerMinute { get; set; }

	public enum UpdateMode
	{
		[Description("Preview changes without writing to Anki")]
		ReadOnly,
		[Description("Update fields even if they already have values")]
		Replace,
		[Description("Only add values to empty fields")]
		AddEmpty
	}

	[CommandOption("-m|--mode")]
	[Description("Operation mode: readonly (preview only), replace (update all), addempty (only fill empty fields).")]
	[DefaultValue(UpdateMode.AddEmpty)]
	public UpdateMode Mode { get; set; } = UpdateMode.AddEmpty;

	[CommandOption("-c|--mnemonic-kanji-class")]
	[Description("CSS class name for kanji spans in cleaned mnemonics (e.g., 'kanji'). If empty, preserves original span structure with data-klook removed.")]
	public string? MnemonicKanjiClass { get; set; }

	[CommandOption("--field")]
	[Description("Field mapping in source=destination format. Source must be one of: kanji, kunyomi, onyomi, radical, meaning, strokes, mnemonic, jlpt, kentei. Destination is the Anki field name. Can be specified multiple times.")]
	public IReadOnlyDictionary<string, string>? FieldMap { get; set; }

	public override ValidationResult Validate()
	{
		if (RequestsPerMinute <= 0)
			return ValidationResult.Error($"--rpm must be greater than 0, got {RequestsPerMinute}.");

		if (FieldMap == null)
		{
			return ValidationResult.Success();
		}

		foreach (var key in FieldMap.Keys)
		{
			if (!SupportedSources.Contains(key))
			{
				return ValidationResult.Error(
					$"Invalid field source '{key}'. Supported sources: {string.Join(", ", SupportedSources)}");
			}
		}

		return ValidationResult.Success();
	}
}