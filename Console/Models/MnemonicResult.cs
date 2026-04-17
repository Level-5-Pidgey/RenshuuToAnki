namespace Console.Models;

public record MnemonicResult(
    string Kanji,
    string ImageUrl,
    string Text,
    string Author)
{
    public string FormattedMnemonic =>
        $"<img src=\"{ImageUrl}\"/><br/>{Text}";
}