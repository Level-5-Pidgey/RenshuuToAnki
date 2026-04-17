namespace Console.Models;

public record Reading(string Text, string SchoolLevel);

public record RadicalInfo(string Character, List<string> Names);

public record MnemonicInfo(string Text, string ImageUrl, string Author);

public record KanjiResult(
	string Kanji,
	string ImageUrl,
	string Meaning,
	List<Reading> Kunyomi,
	List<Reading> Onyomi,
	RadicalInfo? Radical,
	int Strokes,
	MnemonicInfo? Mnemonic,
	string? Jlpt,
	string? Kentei)
{
	public string FormattedMnemonic => Mnemonic != null
		? $"<img src=\"{Mnemonic.ImageUrl}\"/><br/>{Mnemonic.Text}"
		: "";
}